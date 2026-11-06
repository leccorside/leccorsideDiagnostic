using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Shared.Ipc;
using LeccorWindowsHealthCenter.Shared.Security;

namespace LeccorWindowsHealthCenter.Infrastructure.Security;

/// <summary>
/// Executa exclusivamente as operações autorizadas na allowlist fechada (seção 64 do PROMPT).
/// Qualquer outra operação, executável ou argumento não expressamente permitido é rejeitado.
/// </summary>
public static class ElevatedDispatcher
{
    private static readonly Regex SafeDrivePattern = new(@"^[a-zA-Z]:$", RegexOptions.Compiled);

    static ElevatedDispatcher()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static async Task<ElevatedResponse> DispatchAsync(ElevatedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return request.Type switch
            {
                ElevatedOperationType.Ping => new ElevatedResponse
                {
                    RequestId = request.Id,
                    Success = true,
                },
                ElevatedOperationType.ExecuteCommand => await HandleExecuteCommandAsync(request, cancellationToken).ConfigureAwait(false),
                ElevatedOperationType.QueryStorageReliability => HandleQueryStorageReliability(request),
                ElevatedOperationType.TerminateProcess => HandleTerminateProcess(request),
                _ => new ElevatedResponse
                {
                    RequestId = request.Id,
                    Success = false,
                    ErrorMessage = $"Operação '{request.Type}' não é reconhecida ou autorizada.",
                },
            };
        }
        catch (Exception ex)
        {
            return new ElevatedResponse
            {
                RequestId = request.Id,
                Success = false,
                ErrorMessage = ex.Message,
            };
        }
    }

    private static async Task<ElevatedResponse> HandleExecuteCommandAsync(ElevatedRequest request, CancellationToken cancellationToken)
    {
        var cmd = request.Command ?? throw new InvalidOperationException("Payload de comando ausente.");
        ValidateCommandPolicy(cmd.ExecutablePath, cmd.Arguments);

        var systemEncoding = GetEncodingByName(cmd.EncodingName, cmd.ExecutablePath);

        var psi = new ProcessStartInfo(cmd.ExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            StandardOutputEncoding = systemEncoding,
            StandardErrorEncoding = systemEncoding,
            WorkingDirectory = Path.GetDirectoryName(cmd.ExecutablePath) ?? Environment.SystemDirectory,
        };

        foreach (var arg in cmd.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        var commandLine = $"{cmd.ExecutablePath} {string.Join(" ", cmd.Arguments)}";
        var sw = Stopwatch.StartNew();

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Não foi possível iniciar o processo '{cmd.ExecutablePath}'.");
        }

        var maxChars = Math.Clamp(cmd.MaxOutputChars, 1000, 5_000_000);
        var stdoutTask = ReadStreamCappedAsync(process.StandardOutput, maxChars);
        var stderrTask = ReadStreamCappedAsync(process.StandardError, maxChars);

        var timeout = TimeSpan.FromSeconds(Math.Clamp(cmd.TimeoutSeconds, 5, 3600));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var timedOut = false;
        var cancelled = false;

        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = cancellationToken.IsCancellationRequested;
            timedOut = !cancelled;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Processo já finalizado
            }
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        sw.Stop();

        int? exitCode = process.HasExited && !timedOut && !cancelled ? process.ExitCode : null;

        return new ElevatedResponse
        {
            RequestId = request.Id,
            Success = !timedOut && !cancelled,
            CommandResult = new ElevatedCommandResultDto(
                CommandLine: SensitiveDataRedactor.Redact(commandLine),
                ExitCode: exitCode,
                StandardOutput: SensitiveDataRedactor.Redact(stdout),
                StandardError: SensitiveDataRedactor.Redact(stderr),
                Duration: sw.Elapsed,
                TimedOut: timedOut,
                Cancelled: cancelled),
        };
    }

    /// <summary>
    /// Allowlist estrita de executáveis e argumentos autorizados para o processo elevado:
    /// - DISM: /Online /Cleanup-Image /CheckHealth ou /ScanHealth
    /// - SFC: /VERIFYONLY
    /// - CHKDSK: letra de unidade (ex.: C:) sem parâmetros destrutivos
    /// </summary>
    public static void ValidateCommandPolicy(string executablePath, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
        {
            throw new SecurityException("Executável deve possuir caminho absoluto e totalmente qualificado.");
        }

        var fileName = Path.GetFileName(executablePath);
        var dir = Path.GetDirectoryName(executablePath);
        var system32 = Environment.SystemDirectory;

        if (!string.Equals(dir, system32, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"O executável '{executablePath}' não está no diretório de sistema confiável ('{system32}').");
        }

        if (string.Equals(fileName, "dism.exe", StringComparison.OrdinalIgnoreCase))
        {
            // DISM permitido: /Online /Cleanup-Image /CheckHealth OU /Online /Cleanup-Image /ScanHealth
            if (arguments.Count == 3 &&
                string.Equals(arguments[0], "/Online", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(arguments[1], "/Cleanup-Image", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(arguments[2], "/CheckHealth", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(arguments[2], "/ScanHealth", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            throw new SecurityException("Argumentos de DISM rejeitados pela política de segurança. Apenas /CheckHealth e /ScanHealth são permitidos.");
        }

        if (string.Equals(fileName, "sfc.exe", StringComparison.OrdinalIgnoreCase))
        {
            // SFC permitido: estritamente /VERIFYONLY (somente leitura)
            if (arguments.Count == 1 && string.Equals(arguments[0], "/VERIFYONLY", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new SecurityException("Argumentos de SFC rejeitados pela política de segurança. Apenas /VERIFYONLY é permitido.");
        }

        if (string.Equals(fileName, "chkdsk.exe", StringComparison.OrdinalIgnoreCase))
        {
            // CHKDSK permitido: apenas a letra do volume, ex.: "C:" (sem /F, sem /R, sem /X)
            if (arguments.Count == 1 && SafeDrivePattern.IsMatch(arguments[0]))
            {
                return;
            }

            throw new SecurityException("Argumentos de CHKDSK rejeitados pela política de segurança. Apenas verificação somente leitura por letra de unidade ('C:') é permitida.");
        }

        throw new SecurityException($"Executável '{fileName}' não está na lista de operações administrativas permitidas.");
    }

    private static Encoding GetEncodingByName(string? name, string executablePath)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch
            {
                // Fallback para codificação padrão
            }
        }

        var fileName = Path.GetFileName(executablePath);
        if (string.Equals(fileName, "sfc.exe", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.Unicode;
        }

        try
        {
            var oem = CultureInfo.InstalledUICulture.TextInfo.OEMCodePage;
            return Encoding.GetEncoding(oem);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static ElevatedResponse HandleQueryStorageReliability(ElevatedRequest request)
    {
        var list = new List<ElevatedStorageReliabilityDto>();

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT * FROM MSFT_StorageReliabilityCounter");
            using var collection = searcher.Get();

            foreach (var item in collection)
            {
                using (item)
                {
                    var deviceId = item["DeviceId"]?.ToString();
                    var temp = TryConvertInt(item["Temperature"]);
                    var wear = TryConvertInt(item["Wear"]);
                    var hours = TryConvertLong(item["PowerOnHours"]);
                    var readErrors = TryConvertLong(item["ReadErrorsTotal"]);
                    var writeErrors = TryConvertLong(item["WriteErrorsTotal"]);

                    var diskNumber = -1;
                    if (deviceId is not null && int.TryParse(deviceId, out var dn))
                    {
                        diskNumber = dn;
                    }

                    list.Add(new ElevatedStorageReliabilityDto(
                        DiskNumber: diskNumber,
                        DeviceId: deviceId,
                        TemperatureCelsius: temp,
                        WearPercent: wear,
                        PowerOnHours: hours,
                        ReadErrorsTotal: readErrors,
                        WriteErrorsTotal: writeErrors));
                }
            }
        }
        catch (Exception ex)
        {
            return new ElevatedResponse
            {
                RequestId = request.Id,
                Success = false,
                ErrorMessage = $"Falha ao consultar MSFT_StorageReliabilityCounter: {ex.Message}",
            };
        }

        return new ElevatedResponse
        {
            RequestId = request.Id,
            Success = true,
            StorageReliability = list,
        };
    }

    private static ElevatedResponse HandleTerminateProcess(ElevatedRequest request)
    {
        var term = request.Terminate ?? throw new InvalidOperationException("Payload de encerramento de processo ausente.");

        if (CriticalProcesses.IsCritical(term.ProcessId, term.ExecutableName))
        {
            throw new SecurityException($"Processo '{term.ExecutableName}' (PID {term.ProcessId}) é crítico do sistema e não pode ser encerrado.");
        }

        try
        {
            using var proc = Process.GetProcessById(term.ProcessId);

            if (term.ExpectedStartTime.HasValue)
            {
                try
                {
                    var actualStart = proc.StartTime;
                    if (Math.Abs((actualStart - term.ExpectedStartTime.Value).TotalSeconds) > 2)
                    {
                        return new ElevatedResponse
                        {
                            RequestId = request.Id,
                            Success = false,
                            ErrorMessage = "PID reaproveitado por outro processo antes da confirmação.",
                            ProcessTerminated = false,
                        };
                    }
                }
                catch
                {
                    // Se não tiver permissão para ler StartTime, prossegue com o encerramento do PID solicitado
                }
            }

            proc.Kill(entireProcessTree: true);
            return new ElevatedResponse
            {
                RequestId = request.Id,
                Success = true,
                ProcessTerminated = true,
            };
        }
        catch (ArgumentException)
        {
            return new ElevatedResponse
            {
                RequestId = request.Id,
                Success = true,
                ProcessTerminated = false,
                ErrorMessage = "Processo já encerrado.",
            };
        }
    }

    private static int? TryConvertInt(object? val)
    {
        if (val is null)
        {
            return null;
        }

        if (int.TryParse(val.ToString(), out var i))
        {
            return i;
        }

        return null;
    }

    private static long? TryConvertLong(object? val)
    {
        if (val is null)
        {
            return null;
        }

        if (long.TryParse(val.ToString(), out var l))
        {
            return l;
        }

        return null;
    }

    private static async Task<string> ReadStreamCappedAsync(StreamReader reader, int maxChars)
    {
        var sb = new StringBuilder();
        var buffer = new char[4096];
        while (sb.Length < maxChars)
        {
            var toRead = Math.Min(buffer.Length, maxChars - sb.Length);
            var read = await reader.ReadAsync(buffer, 0, toRead).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            sb.Append(buffer, 0, read);
        }

        return sb.ToString();
    }
}

