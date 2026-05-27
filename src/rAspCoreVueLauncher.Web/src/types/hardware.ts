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
}
