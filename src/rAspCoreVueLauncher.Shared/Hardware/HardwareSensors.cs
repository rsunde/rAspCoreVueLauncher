namespace rAspCoreVueLauncher.Shared.Hardware;

public record HardwareSensors(
    DateTimeOffset ServerTimeUtc,
    TimeSpan ProcessUptime,
    CpuSnapshot Cpu,
    MemorySnapshot Memory,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<NetworkInterfaceSnapshot> Networks,
    BatterySnapshot? Battery,
    MobileSensorReading? Mobile);

public record CpuSnapshot(
    int LogicalCores,
    double ProcessUsagePercent);

public record MemorySnapshot(
    long ProcessWorkingSetMb,
    long TotalAvailableMb);

public record DiskSnapshot(
    string Name,
    string DriveFormat,
    long TotalMb,
    long FreeMb);

public record NetworkInterfaceSnapshot(
    string Name,
    string Description,
    string Status,
    bool IsLoopback,
    IReadOnlyList<string> IpAddresses);

public record BatterySnapshot(
    int PercentRemaining,
    bool IsCharging,
    TimeSpan? EstimatedRuntime);

// =======================================================================
// Mobile sensors. Each block is nullable — clients only populate what their
// platform exposes (Android, iOS, Tauri-on-mobile, browser Web Sensors API,
// etc.). The server itself never fills these in; it receives them via
// POST /api/hardware/sensors/mobile and echoes the latest reading back.
// =======================================================================

public record MobileSensorReading(
    string ClientId,
    DateTimeOffset CapturedAtUtc,
    MobileDeviceInfo? Device,
    MotionSensors? Motion,
    OrientationSensors? Orientation,
    EnvironmentSensors? Environment,
    LocationSensors? Location,
    HealthSensors? Health,
    BiometricSensors? Biometric,
    ConnectivitySensors? Connectivity,
    UserInterfaceSensors? UserInterface);

public record MobileDeviceInfo(
    string? Manufacturer,
    string? Model,
    string? OsName,
    string? OsVersion,
    string? Locale,
    string? TimeZone,
    bool? IsPhysicalDevice);

// Motion: raw inertial sensors. Units: m/s² for acceleration, rad/s for
// angular velocity, µT for magnetometer.
public record MotionSensors(
    Vector3? Accelerometer,
    Vector3? Gyroscope,
    Vector3? Magnetometer,
    Vector3? Gravity,
    Vector3? LinearAcceleration,
    Vector4? RotationVector,
    Vector3? UserAcceleration,
    long? StepCount,
    double? Cadence);

// Orientation: derived from the motion stack (degrees).
public record OrientationSensors(
    double? Pitch,
    double? Roll,
    double? Yaw,
    double? CompassHeading,
    double? TrueHeading,
    double? HeadingAccuracyDegrees,
    string? ScreenOrientation);

public record EnvironmentSensors(
    double? AmbientLightLux,
    double? ProximityCm,
    bool? IsNear,
    double? AmbientTemperatureCelsius,
    double? RelativeHumidityPercent,
    double? PressureHpa,
    double? AltitudeMeters,
    double? UvIndex);

public record LocationSensors(
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    double? AccuracyMeters,
    double? AltitudeAccuracyMeters,
    double? HeadingDegrees,
    double? SpeedMetersPerSecond,
    string? Provider,
    bool? IsMocked,
    int? SatelliteCount,
    DateTimeOffset? FixTimestampUtc);

// Health / biometric readings (privacy-heavy; only present where the user
// has granted permission).
public record HealthSensors(
    double? HeartRateBpm,
    double? HeartRateVariabilityMs,
    double? BloodOxygenPercent,
    double? RespiratoryRateBpm,
    double? BodyTemperatureCelsius,
    double? SkinTemperatureCelsius,
    long? StepsToday,
    double? DistanceMetersToday,
    double? ActiveEnergyKcalToday,
    double? VO2MaxMlPerKgPerMin,
    int? SleepStage,
    int? StressLevel);

public record BiometricSensors(
    bool? FingerprintAvailable,
    bool? FaceUnlockAvailable,
    bool? IrisAvailable,
    bool? VoiceUnlockAvailable,
    bool? StrongBiometricEnrolled,
    string? AuthenticationStatus);

public record ConnectivitySensors(
    string? NetworkType,
    string? CarrierName,
    int? SignalStrengthDbm,
    int? WifiRssiDbm,
    string? WifiSsid,
    bool? IsMetered,
    bool? IsRoaming,
    bool? AirplaneMode,
    bool? BluetoothEnabled,
    bool? NfcAvailable,
    bool? NfcEnabled);

public record UserInterfaceSensors(
    double? ScreenBrightness,
    bool? KeyguardLocked,
    string? AppState,
    bool? HapticsAvailable,
    bool? FlashlightOn,
    double? AmbientNoiseDb,
    bool? HeadphonesPluggedIn,
    bool? IsMuted);

public record Vector3(double X, double Y, double Z);

public record Vector4(double X, double Y, double Z, double W);
