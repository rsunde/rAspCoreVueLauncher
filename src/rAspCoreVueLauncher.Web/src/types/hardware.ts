// Mirrors rAspCoreVueLauncher.Shared.Hardware.*
// In future, generate this from the API's OpenAPI document.
export interface HardwareInfo {
  osPlatform: string
  osDescription: string
  osArchitecture: string
  machineName: string
  processorCount: number
  totalMemoryMb: number
  runtimeVersion: string
}

export interface CpuSnapshot {
  logicalCores: number
  processUsagePercent: number
}

export interface MemorySnapshot {
  processWorkingSetMb: number
  totalAvailableMb: number
}

export interface DiskSnapshot {
  name: string
  driveFormat: string
  totalMb: number
  freeMb: number
}

export interface NetworkInterfaceSnapshot {
  name: string
  description: string
  status: string
  isLoopback: boolean
  ipAddresses: string[]
}

export interface BatterySnapshot {
  percentRemaining: number
  isCharging: boolean
  estimatedRuntime: string | null
}

export interface HardwareSensors {
  serverTimeUtc: string
  processUptime: string
  cpu: CpuSnapshot
  memory: MemorySnapshot
  disks: DiskSnapshot[]
  networks: NetworkInterfaceSnapshot[]
  battery: BatterySnapshot | null
  mobile: MobileSensorReading | null
}

export interface Vector3 {
  x: number
  y: number
  z: number
}

export interface Vector4 {
  x: number
  y: number
  z: number
  w: number
}

export interface MobileDeviceInfo {
  manufacturer?: string | null
  model?: string | null
  osName?: string | null
  osVersion?: string | null
  locale?: string | null
  timeZone?: string | null
  isPhysicalDevice?: boolean | null
}

export interface MotionSensors {
  accelerometer?: Vector3 | null
  gyroscope?: Vector3 | null
  magnetometer?: Vector3 | null
  gravity?: Vector3 | null
  linearAcceleration?: Vector3 | null
  rotationVector?: Vector4 | null
  userAcceleration?: Vector3 | null
  stepCount?: number | null
  cadence?: number | null
}

export interface OrientationSensors {
  pitch?: number | null
  roll?: number | null
  yaw?: number | null
  compassHeading?: number | null
  trueHeading?: number | null
  headingAccuracyDegrees?: number | null
  screenOrientation?: string | null
}

export interface EnvironmentSensors {
  ambientLightLux?: number | null
  proximityCm?: number | null
  isNear?: boolean | null
  ambientTemperatureCelsius?: number | null
  relativeHumidityPercent?: number | null
  pressureHpa?: number | null
  altitudeMeters?: number | null
  uvIndex?: number | null
}

export interface LocationSensors {
  latitude?: number | null
  longitude?: number | null
  altitudeMeters?: number | null
  accuracyMeters?: number | null
  altitudeAccuracyMeters?: number | null
  headingDegrees?: number | null
  speedMetersPerSecond?: number | null
  provider?: string | null
  isMocked?: boolean | null
  satelliteCount?: number | null
  fixTimestampUtc?: string | null
}

export interface HealthSensors {
  heartRateBpm?: number | null
  heartRateVariabilityMs?: number | null
  bloodOxygenPercent?: number | null
  respiratoryRateBpm?: number | null
  bodyTemperatureCelsius?: number | null
  skinTemperatureCelsius?: number | null
  stepsToday?: number | null
  distanceMetersToday?: number | null
  activeEnergyKcalToday?: number | null
  vO2MaxMlPerKgPerMin?: number | null
  sleepStage?: number | null
  stressLevel?: number | null
}

export interface BiometricSensors {
  fingerprintAvailable?: boolean | null
  faceUnlockAvailable?: boolean | null
  irisAvailable?: boolean | null
  voiceUnlockAvailable?: boolean | null
  strongBiometricEnrolled?: boolean | null
  authenticationStatus?: string | null
}

export interface ConnectivitySensors {
  networkType?: string | null
  carrierName?: string | null
  signalStrengthDbm?: number | null
  wifiRssiDbm?: number | null
  wifiSsid?: string | null
  isMetered?: boolean | null
  isRoaming?: boolean | null
  airplaneMode?: boolean | null
  bluetoothEnabled?: boolean | null
  nfcAvailable?: boolean | null
  nfcEnabled?: boolean | null
}

export interface UserInterfaceSensors {
  screenBrightness?: number | null
  keyguardLocked?: boolean | null
  appState?: string | null
  hapticsAvailable?: boolean | null
  flashlightOn?: boolean | null
  ambientNoiseDb?: number | null
  headphonesPluggedIn?: boolean | null
  isMuted?: boolean | null
}

export interface MobileSensorReading {
  clientId: string
  capturedAtUtc: string
  device?: MobileDeviceInfo | null
  motion?: MotionSensors | null
  orientation?: OrientationSensors | null
  environment?: EnvironmentSensors | null
  location?: LocationSensors | null
  health?: HealthSensors | null
  biometric?: BiometricSensors | null
  connectivity?: ConnectivitySensors | null
  userInterface?: UserInterfaceSensors | null
}
