namespace LeccorWindowsHealthCenter.Shared.Ipc;

/// <summary>Tipos de operação permitidas no processo elevado (allowlist fechada, seção 64 do PROMPT).</summary>
public enum ElevatedOperationType
{
    Ping = 0,
    ExecuteCommand = 1,
    QueryStorageReliability = 2,
    TerminateProcess = 3,
    Shutdown = 4,
}

/// <summary>Handshake inicial para autenticar a conexão IPC.</summary>
public sealed record IpcHandshakeRequest(string Token, int ClientProcessId);

public sealed record IpcHandshakeResponse(bool Success, string? ErrorMessage);

/// <summary>Requisição RPC enviada do processo principal ao processo elevado.</summary>
public sealed record ElevatedRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public ElevatedOperationType Type { get; init; }

    public ExecuteCommandPayload? Command { get; init; }

    public TerminateProcessPayload? Terminate { get; init; }
}

public sealed record ExecuteCommandPayload(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    int TimeoutSeconds,
    int MaxOutputChars,
    string? EncodingName);

public sealed record TerminateProcessPayload(
    int ProcessId,
    string ExecutableName,
    DateTimeOffset? ExpectedStartTime);

/// <summary>Resposta RPC do processo elevado.</summary>
public sealed record ElevatedResponse
{
    public Guid RequestId { get; init; }

    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public ElevatedCommandResultDto? CommandResult { get; init; }

    public IReadOnlyList<ElevatedStorageReliabilityDto>? StorageReliability { get; init; }

    public bool? ProcessTerminated { get; init; }
}

public sealed record ElevatedCommandResultDto(
    string CommandLine,
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    bool Cancelled);

public sealed record ElevatedStorageReliabilityDto(
    int DiskNumber,
    string? DeviceId,
    int? TemperatureCelsius,
    int? WearPercent,
    long? PowerOnHours,
    long? ReadErrorsTotal,
    long? WriteErrorsTotal);

public static class IpcConstants
{
    public const string PipePrefix = "LeccorHealthCenter-Elevated-";
    public const int DefaultIdleTimeoutSeconds = 45;
    public const int ConnectTimeoutSeconds = 20;
}

