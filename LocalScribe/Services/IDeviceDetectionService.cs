namespace LocalScribe.Services;

public interface IDeviceDetectionService
{
    event EventHandler<DeviceInfo>? DeviceChanged;

    void InvalidateCache();

    Task<DeviceInfo> DetectAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public sealed class DeviceInfo
{
    public string DeviceType { get; init; } = "CPU";

    public string DisplayName { get; init; } = "CPU";

    public bool IsAccelerated =>
        IsCudaAvailable || !string.Equals(DeviceType, "CPU", StringComparison.OrdinalIgnoreCase);

    public bool IsPythonAvailable { get; init; }

    public bool IsCudaAvailable { get; init; }

    public string? PythonPath { get; init; }

    public string? ProbeError { get; init; }
}