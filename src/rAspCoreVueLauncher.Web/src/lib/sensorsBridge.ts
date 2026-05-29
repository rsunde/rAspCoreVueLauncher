// rAspCoreVueLauncher · sensors bridge — DROP-IN MODULE for BYO Vue apps.
// Copy this file into your app, then call `startSensorBridge()` once at
// startup. Zero dependencies (uses fetch + standard browser/Tauri sensor APIs).
//
// Server contract: POST /api/hardware/sensors/mobile with a MobileSensorReading
// JSON body. See rAspCoreVueLauncher.Shared.Hardware.MobileSensorReading.

export interface Vector3 { x: number; y: number; z: number }
export interface Vector4 { x: number; y: number; z: number; w: number }

export interface MobileSensorReading {
  clientId: string
  capturedAtUtc: string
  device?: Partial<{
    manufacturer: string; model: string; osName: string; osVersion: string
    locale: string; timeZone: string; isPhysicalDevice: boolean
  }> | null
  motion?: Partial<{
    accelerometer: Vector3; gyroscope: Vector3; magnetometer: Vector3
    gravity: Vector3; linearAcceleration: Vector3; rotationVector: Vector4
    userAcceleration: Vector3; stepCount: number; cadence: number
  }> | null
  orientation?: Partial<{
    pitch: number; roll: number; yaw: number; compassHeading: number
    trueHeading: number; headingAccuracyDegrees: number; screenOrientation: string
  }> | null
  environment?: Partial<{
    ambientLightLux: number; proximityCm: number; isNear: boolean
    ambientTemperatureCelsius: number; relativeHumidityPercent: number
    pressureHpa: number; altitudeMeters: number; uvIndex: number
  }> | null
  location?: Partial<{
    latitude: number; longitude: number; altitudeMeters: number
    accuracyMeters: number; altitudeAccuracyMeters: number; headingDegrees: number
    speedMetersPerSecond: number; provider: string; isMocked: boolean
    satelliteCount: number; fixTimestampUtc: string
  }> | null
  health?: Partial<Record<string, number>> | null
  biometric?: Partial<Record<string, boolean | string>> | null
  connectivity?: Partial<{
    networkType: string; carrierName: string; signalStrengthDbm: number
    wifiRssiDbm: number; wifiSsid: string; isMetered: boolean; isRoaming: boolean
    airplaneMode: boolean; bluetoothEnabled: boolean; nfcAvailable: boolean
    nfcEnabled: boolean
  }> | null
  userInterface?: Partial<{
    screenBrightness: number; keyguardLocked: boolean; appState: string
    hapticsAvailable: boolean; flashlightOn: boolean; ambientNoiseDb: number
    headphonesPluggedIn: boolean; isMuted: boolean
  }> | null
}

export interface SensorBridgeOptions {
  /** Where the API lives. Defaults to same-origin (e.g. via Vite proxy or Tauri). */
  apiBaseUrl?: string
  /** Stable identifier for this device/install. Auto-generates + persists in localStorage if omitted. */
  clientId?: string
  /** How often to POST a reading (ms). Default 2000. */
  intervalMs?: number
  /** Which sensor groups to attempt. Anything the platform doesn't expose is silently skipped. */
  enable?: {
    motion?: boolean
    orientation?: boolean
    location?: boolean
    battery?: boolean
    connectivity?: boolean
    device?: boolean
  }
  /** Optional hook — called after every successful POST. */
  onPosted?: (reading: MobileSensorReading) => void
  /** Optional hook — called when a POST fails. Default: console.warn. */
  onError?: (err: unknown) => void
}

export interface SensorBridgeHandle {
  /** Force an immediate POST. */
  flush: () => Promise<void>
  /** Stop polling and detach listeners. */
  stop: () => void
  /** Read the latest reading the bridge built (not yet POSTed if in-flight). */
  latest: () => MobileSensorReading | null
}

const CLIENT_ID_KEY = 'rAspCoreVueLauncher:sensorBridge:clientId'

export type BridgeStatus = 'not-started' | 'running'

// Set by startSensorBridge so components can read the latest local reading
// without holding the handle. Used by the /hardware/mobile diagnostic page.
let activeBridge: SensorBridgeHandle | null = null

export function peekLatestLocalReading(): { reading: MobileSensorReading | null; status: BridgeStatus } {
  if (!activeBridge) return { reading: null, status: 'not-started' }
  return { reading: activeBridge.latest(), status: 'running' }
}

function getOrCreateClientId(): string {
  try {
    const existing = localStorage.getItem(CLIENT_ID_KEY)
    if (existing) return existing
    const id = `web-${crypto.randomUUID()}`
    localStorage.setItem(CLIENT_ID_KEY, id)
    return id
  } catch {
    return `web-${Math.random().toString(36).slice(2, 10)}`
  }
}

export function startSensorBridge(opts: SensorBridgeOptions = {}): SensorBridgeHandle {
  const base = (opts.apiBaseUrl ?? '').replace(/\/$/, '')
  const clientId = opts.clientId ?? getOrCreateClientId()
  const intervalMs = Math.max(250, opts.intervalMs ?? 2000)
  const enable = { motion: true, orientation: true, location: true, battery: true, connectivity: true, device: true, ...(opts.enable ?? {}) }
  const onError = opts.onError ?? ((err) => console.warn('[sensorsBridge] post failed', err))

  let motion: MobileSensorReading['motion'] | undefined
  let orientation: MobileSensorReading['orientation'] | undefined
  let location: MobileSensorReading['location'] | undefined
  let battery: { level?: number; charging?: boolean } | undefined
  let geoWatchId: number | undefined
  let timer: number | undefined
  let last: MobileSensorReading | null = null

  const onMotion = (ev: DeviceMotionEvent) => {
    if (!enable.motion) return
    const acc = ev.accelerationIncludingGravity
    const lin = ev.acceleration
    const rot = ev.rotationRate
    motion = {
      ...(acc?.x != null ? { accelerometer: { x: acc.x ?? 0, y: acc.y ?? 0, z: acc.z ?? 0 } } : {}),
      ...(lin?.x != null ? { linearAcceleration: { x: lin.x ?? 0, y: lin.y ?? 0, z: lin.z ?? 0 } } : {}),
      ...(rot ? { gyroscope: { x: (rot.beta ?? 0), y: (rot.gamma ?? 0), z: (rot.alpha ?? 0) } } : {}),
    }
  }

  const onOrient = (ev: DeviceOrientationEvent) => {
    if (!enable.orientation) return
    orientation = {
      ...(ev.alpha != null ? { yaw: ev.alpha } : {}),
      ...(ev.beta != null ? { pitch: ev.beta } : {}),
      ...(ev.gamma != null ? { roll: ev.gamma } : {}),
      ...(typeof screen !== 'undefined' && screen.orientation ? { screenOrientation: screen.orientation.type } : {}),
    }
  }

  if (enable.motion && typeof window !== 'undefined' && 'DeviceMotionEvent' in window) {
    // iOS gate: caller should request permission via a user gesture before importing.
    const anyMotion = DeviceMotionEvent as unknown as { requestPermission?: () => Promise<string> }
    if (typeof anyMotion.requestPermission === 'function') {
      anyMotion.requestPermission().then(state => { if (state === 'granted') window.addEventListener('devicemotion', onMotion) }).catch(() => {})
    } else {
      window.addEventListener('devicemotion', onMotion)
    }
  }
  if (enable.orientation && typeof window !== 'undefined' && 'DeviceOrientationEvent' in window) {
    const anyOrient = DeviceOrientationEvent as unknown as { requestPermission?: () => Promise<string> }
    if (typeof anyOrient.requestPermission === 'function') {
      anyOrient.requestPermission().then(state => { if (state === 'granted') window.addEventListener('deviceorientation', onOrient) }).catch(() => {})
    } else {
      window.addEventListener('deviceorientation', onOrient)
    }
  }

  if (enable.location && typeof navigator !== 'undefined' && navigator.geolocation) {
    try {
      geoWatchId = navigator.geolocation.watchPosition(pos => {
        const c = pos.coords
        location = {
          latitude: c.latitude,
          longitude: c.longitude,
          accuracyMeters: c.accuracy,
          ...(c.altitude != null ? { altitudeMeters: c.altitude } : {}),
          ...(c.altitudeAccuracy != null ? { altitudeAccuracyMeters: c.altitudeAccuracy } : {}),
          ...(c.heading != null ? { headingDegrees: c.heading } : {}),
          ...(c.speed != null ? { speedMetersPerSecond: c.speed } : {}),
          fixTimestampUtc: new Date(pos.timestamp).toISOString(),
          provider: 'browser-geolocation',
        }
      }, () => {}, { enableHighAccuracy: false, maximumAge: 5000, timeout: 10000 })
    } catch { /* permission denied or unsupported */ }
  }

  let batteryRef: { level: number; charging: boolean; addEventListener: (n: string, f: () => void) => void } | undefined
  if (enable.battery && typeof navigator !== 'undefined' && 'getBattery' in navigator) {
    (navigator as unknown as { getBattery: () => Promise<typeof batteryRef> }).getBattery!().then(b => {
      batteryRef = b
      const sync = () => { battery = b ? { level: b.level, charging: b.charging } : undefined }
      sync()
      b?.addEventListener('levelchange', sync)
      b?.addEventListener('chargingchange', sync)
    }).catch(() => {})
  }

  const buildReading = (): MobileSensorReading => {
    const connection = (navigator as unknown as { connection?: { effectiveType?: string; downlink?: number; rtt?: number; saveData?: boolean } }).connection
    return {
      clientId,
      capturedAtUtc: new Date().toISOString(),
      ...(enable.device ? {
        device: {
          ...(navigator.platform ? { model: navigator.platform } : {}),
          ...(navigator.userAgent ? { osName: navigator.userAgent } : {}),
          ...(navigator.language ? { locale: navigator.language } : {}),
          ...(Intl?.DateTimeFormat ? { timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone } : {}),
        },
      } : {}),
      ...(motion ? { motion } : {}),
      ...(orientation ? { orientation } : {}),
      ...(location ? { location } : {}),
      ...(enable.connectivity && connection ? {
        connectivity: {
          ...(connection.effectiveType ? { networkType: connection.effectiveType } : {}),
          ...(connection.saveData != null ? { isMetered: connection.saveData } : {}),
        },
      } : {}),
      ...(battery ? {
        userInterface: {} /* battery lives on the server's BatterySnapshot, not the mobile reading; left empty here */,
      } : {}),
    }
  }

  const post = async () => {
    const reading = buildReading()
    last = reading
    try {
      const res = await fetch(`${base}/api/hardware/sensors/mobile`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(reading),
      })
      if (!res.ok) throw new Error(`HTTP ${res.status}`)
      opts.onPosted?.(reading)
    } catch (err) {
      onError(err)
    }
  }

  timer = window.setInterval(post, intervalMs)
  // Fire one immediately so the server has a reading without waiting an interval.
  post()

  const handle: SensorBridgeHandle = {
    flush: post,
    stop: () => {
      if (timer != null) window.clearInterval(timer)
      if (geoWatchId != null && navigator.geolocation) navigator.geolocation.clearWatch(geoWatchId)
      window.removeEventListener('devicemotion', onMotion)
      window.removeEventListener('deviceorientation', onOrient)
      activeBridge = null
    },
    latest: () => last,
  }
  activeBridge = handle
  return handle
}
