namespace LeccorWindowsHealthCenter.Application.Security;

public sealed record AntivirusProductInfo
{
    public required string Name { get; init; }
    public required string InstanceGuid { get; init; }
    public required string PathToSignedProductExe { get; init; }
    public required bool IsEnabled { get; init; }
    public required bool IsUpToDate { get; init; }
    public required bool IsWindowsDefender { get; init; }
}

public sealed record FirewallProductInfo
{
    public required string Name { get; init; }
    public required string InstanceGuid { get; init; }
    public required bool IsEnabled { get; init; }
}

public sealed record SuspiciousPersistenceItem
{
    public required string Target { get; init; }
    public required string Type { get; init; } // IFEO, AppInit_DLLs, TempLocation
    public required string Description { get; init; }
    public required string Details { get; init; }
    public bool IsSuspicious { get; init; }
}

public sealed record SecurityStatusSummary
{
    public required IReadOnlyList<AntivirusProductInfo> AntivirusProducts { get; init; }
    public required IReadOnlyList<FirewallProductInfo> FirewallProducts { get; init; }
    public required bool IsUacEnabled { get; init; }
    public required bool IsRealTimeProtectionActive { get; init; }
    public required bool IsTamperProtectionActive { get; init; }
    public required IReadOnlyList<SuspiciousPersistenceItem> SuspiciousPersistences { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
}

public static class SecurityNames
{
    public const string ObservationAntivirus = "security.antivirus";
    public const string ObservationFirewall = "security.firewall";
    public const string ObservationUac = "security.uac";
    public const string ObservationPersistence = "security.persistence";

    public const string AttrName = "name";
    public const string AttrEnabled = "enabled";
    public const string AttrUpToDate = "upToDate";
    public const string AttrUacEnabled = "uacEnabled";
    public const string AttrPersistenceType = "type";
    public const string AttrTarget = "target";
    public const string AttrSuspicious = "suspicious";
}

public interface ISecurityProvider
{
    Task<SecurityStatusSummary> GetSecurityStatusAsync(CancellationToken cancellationToken);
}

