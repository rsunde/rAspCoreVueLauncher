<script setup lang="ts">
import type { MobileSensorReading, Vector3, Vector4 } from '@/types/hardware'
import FieldRow from '@/components/diagnostics/FieldRow.vue'

defineProps<{ reading: MobileSensorReading }>()

function fmtVec(v: Vector3 | Vector4 | null | undefined): string {
  if (!v) return '—'
  const parts = [`x ${v.x}`, `y ${v.y}`, `z ${v.z}`]
  if ('w' in v && (v as Vector4).w !== undefined) parts.push(`w ${(v as Vector4).w}`)
  return parts.join('   ')
}
</script>

<template>
  <div class="space-y-4">
    <div>
      <FieldRow label="clientId" :value="reading.clientId" />
      <FieldRow label="capturedAtUtc" :value="reading.capturedAtUtc" />
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">device</h3>
      <p v-if="!reading.device" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="manufacturer" :value="reading.device.manufacturer" />
        <FieldRow label="model" :value="reading.device.model" />
        <FieldRow label="osName" :value="reading.device.osName" />
        <FieldRow label="osVersion" :value="reading.device.osVersion" />
        <FieldRow label="locale" :value="reading.device.locale" />
        <FieldRow label="timeZone" :value="reading.device.timeZone" />
        <FieldRow label="isPhysicalDevice" :value="reading.device.isPhysicalDevice" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">motion</h3>
      <p v-if="!reading.motion" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="accelerometer" :value="fmtVec(reading.motion.accelerometer)" />
        <FieldRow label="gyroscope" :value="fmtVec(reading.motion.gyroscope)" />
        <FieldRow label="magnetometer" :value="fmtVec(reading.motion.magnetometer)" />
        <FieldRow label="gravity" :value="fmtVec(reading.motion.gravity)" />
        <FieldRow label="linearAcceleration" :value="fmtVec(reading.motion.linearAcceleration)" />
        <FieldRow label="rotationVector" :value="fmtVec(reading.motion.rotationVector)" />
        <FieldRow label="userAcceleration" :value="fmtVec(reading.motion.userAcceleration)" />
        <FieldRow label="stepCount" :value="reading.motion.stepCount" />
        <FieldRow label="cadence" :value="reading.motion.cadence" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">orientation</h3>
      <p v-if="!reading.orientation" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="pitch" :value="reading.orientation.pitch" />
        <FieldRow label="roll" :value="reading.orientation.roll" />
        <FieldRow label="yaw" :value="reading.orientation.yaw" />
        <FieldRow label="compassHeading" :value="reading.orientation.compassHeading" />
        <FieldRow label="trueHeading" :value="reading.orientation.trueHeading" />
        <FieldRow label="headingAccuracyDegrees" :value="reading.orientation.headingAccuracyDegrees" />
        <FieldRow label="screenOrientation" :value="reading.orientation.screenOrientation" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">environment</h3>
      <p v-if="!reading.environment" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="ambientLightLux" :value="reading.environment.ambientLightLux" />
        <FieldRow label="proximityCm" :value="reading.environment.proximityCm" />
        <FieldRow label="isNear" :value="reading.environment.isNear" />
        <FieldRow label="ambientTemperatureCelsius" :value="reading.environment.ambientTemperatureCelsius" unit="°C" />
        <FieldRow label="relativeHumidityPercent" :value="reading.environment.relativeHumidityPercent" unit="%" />
        <FieldRow label="pressureHpa" :value="reading.environment.pressureHpa" unit="hPa" />
        <FieldRow label="altitudeMeters" :value="reading.environment.altitudeMeters" unit="m" />
        <FieldRow label="uvIndex" :value="reading.environment.uvIndex" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">location</h3>
      <p v-if="!reading.location" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="latitude" :value="reading.location.latitude" />
        <FieldRow label="longitude" :value="reading.location.longitude" />
        <FieldRow label="altitudeMeters" :value="reading.location.altitudeMeters" unit="m" />
        <FieldRow label="accuracyMeters" :value="reading.location.accuracyMeters" unit="m" />
        <FieldRow label="altitudeAccuracyMeters" :value="reading.location.altitudeAccuracyMeters" unit="m" />
        <FieldRow label="headingDegrees" :value="reading.location.headingDegrees" />
        <FieldRow label="speedMetersPerSecond" :value="reading.location.speedMetersPerSecond" unit="m/s" />
        <FieldRow label="provider" :value="reading.location.provider" />
        <FieldRow label="isMocked" :value="reading.location.isMocked" />
        <FieldRow label="satelliteCount" :value="reading.location.satelliteCount" />
        <FieldRow label="fixTimestampUtc" :value="reading.location.fixTimestampUtc" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">health</h3>
      <p v-if="!reading.health" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="heartRateBpm" :value="reading.health.heartRateBpm" unit="bpm" />
        <FieldRow label="heartRateVariabilityMs" :value="reading.health.heartRateVariabilityMs" unit="ms" />
        <FieldRow label="bloodOxygenPercent" :value="reading.health.bloodOxygenPercent" unit="%" />
        <FieldRow label="respiratoryRateBpm" :value="reading.health.respiratoryRateBpm" unit="bpm" />
        <FieldRow label="bodyTemperatureCelsius" :value="reading.health.bodyTemperatureCelsius" unit="°C" />
        <FieldRow label="skinTemperatureCelsius" :value="reading.health.skinTemperatureCelsius" unit="°C" />
        <FieldRow label="stepsToday" :value="reading.health.stepsToday" />
        <FieldRow label="distanceMetersToday" :value="reading.health.distanceMetersToday" unit="m" />
        <FieldRow label="activeEnergyKcalToday" :value="reading.health.activeEnergyKcalToday" unit="kcal" />
        <FieldRow label="vO2MaxMlPerKgPerMin" :value="reading.health.vO2MaxMlPerKgPerMin" />
        <FieldRow label="sleepStage" :value="reading.health.sleepStage" />
        <FieldRow label="stressLevel" :value="reading.health.stressLevel" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">biometric</h3>
      <p v-if="!reading.biometric" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="fingerprintAvailable" :value="reading.biometric.fingerprintAvailable" />
        <FieldRow label="faceUnlockAvailable" :value="reading.biometric.faceUnlockAvailable" />
        <FieldRow label="irisAvailable" :value="reading.biometric.irisAvailable" />
        <FieldRow label="voiceUnlockAvailable" :value="reading.biometric.voiceUnlockAvailable" />
        <FieldRow label="strongBiometricEnrolled" :value="reading.biometric.strongBiometricEnrolled" />
        <FieldRow label="authenticationStatus" :value="reading.biometric.authenticationStatus" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">connectivity</h3>
      <p v-if="!reading.connectivity" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="networkType" :value="reading.connectivity.networkType" />
        <FieldRow label="carrierName" :value="reading.connectivity.carrierName" />
        <FieldRow label="signalStrengthDbm" :value="reading.connectivity.signalStrengthDbm" unit="dBm" />
        <FieldRow label="wifiRssiDbm" :value="reading.connectivity.wifiRssiDbm" unit="dBm" />
        <FieldRow label="wifiSsid" :value="reading.connectivity.wifiSsid" />
        <FieldRow label="isMetered" :value="reading.connectivity.isMetered" />
        <FieldRow label="isRoaming" :value="reading.connectivity.isRoaming" />
        <FieldRow label="airplaneMode" :value="reading.connectivity.airplaneMode" />
        <FieldRow label="bluetoothEnabled" :value="reading.connectivity.bluetoothEnabled" />
        <FieldRow label="nfcAvailable" :value="reading.connectivity.nfcAvailable" />
        <FieldRow label="nfcEnabled" :value="reading.connectivity.nfcEnabled" />
      </template>
    </div>

    <div>
      <h3 class="mb-1 text-sm font-semibold">userInterface</h3>
      <p v-if="!reading.userInterface" class="text-xs text-muted-foreground">not reported</p>
      <template v-else>
        <FieldRow label="screenBrightness" :value="reading.userInterface.screenBrightness" />
        <FieldRow label="keyguardLocked" :value="reading.userInterface.keyguardLocked" />
        <FieldRow label="appState" :value="reading.userInterface.appState" />
        <FieldRow label="hapticsAvailable" :value="reading.userInterface.hapticsAvailable" />
        <FieldRow label="flashlightOn" :value="reading.userInterface.flashlightOn" />
        <FieldRow label="ambientNoiseDb" :value="reading.userInterface.ambientNoiseDb" unit="dB" />
        <FieldRow label="headphonesPluggedIn" :value="reading.userInterface.headphonesPluggedIn" />
        <FieldRow label="isMuted" :value="reading.userInterface.isMuted" />
      </template>
    </div>
  </div>
</template>
