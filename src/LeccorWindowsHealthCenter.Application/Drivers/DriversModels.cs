namespace LeccorWindowsHealthCenter.Application.Drivers;

public sealed record DriverDeviceItem
{
    public required string DeviceId { get; init; }

    public required string Name { get; init; }

    public string? ClassName { get; init; }

    public string? ClassGuid { get; init; }

    public string? Manufacturer { get; init; }

    public string? DriverProvider { get; init; }

    public string? DriverVersion { get; init; }

    public DateTimeOffset? DriverDate { get; init; }

    public uint ConfigManagerErrorCode { get; init; }

    public string? StatusMessage { get; init; }

    public bool IsProblemDevice => ConfigManagerErrorCode != 0;

    public bool IsMissingDriver => ConfigManagerErrorCode == 28;
}

public static class DriverNames
{
    public const string ObservationDevice = "drivers.device";

    public const string AttrDeviceId = "deviceId";
    public const string AttrName = "name";
    public const string AttrClass = "class";
    public const string AttrManufacturer = "manufacturer";
    public const string AttrDriverVersion = "driverVersion";
    public const string AttrDriverDate = "driverDate";
    public const string AttrErrorCode = "errorCode";
    public const string AttrStatusMessage = "statusMessage";
    public const string AttrIsProblem = "isProblem";
    public const string AttrIsMissing = "isMissing";
}

public interface IDriversProvider
{
    Task<IReadOnlyList<DriverDeviceItem>> GetDevicesAsync(CancellationToken cancellationToken);
}

