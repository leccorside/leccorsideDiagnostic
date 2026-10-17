using LeccorWindowsHealthCenter.Application.Settings;
using LeccorWindowsHealthCenter.Shared;
using LeccorWindowsHealthCenter.Shared.Security;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace LeccorWindowsHealthCenter.Infrastructure.Logging;

/// <summary>
/// Logging estruturado (seção 42): JSON compacto em %ProgramData%\LeccorWindowsHealthCenter\Logs,
/// rotação diária e por tamanho, retenção configurável e redação de segredos em todas as propriedades.
/// A aplicação usa apenas <c>Microsoft.Extensions.Logging.ILogger&lt;T&gt;</c>; Serilog é só o provider.
/// </summary>
public static class LoggingSetup
{
    public const string FileNamePattern = "lwhc-.log";

    public static Logger CreateLogger(AppPaths paths, LoggingFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);

        var level = Enum.TryParse<LogEventLevel>(options.MinimumLevel, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;

        return new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.With(new RedactionEnricher())
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(paths.Logs, FileNamePattern),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: Math.Max(1024 * 1024, options.FileSizeLimitBytes),
                retainedFileCountLimit: Math.Max(1, options.RetainedFileCount),
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(2))
            .CreateLogger();
    }
}

/// <summary>Mascara propriedades com nomes sensíveis e segredos embutidos em strings (seção 42).</summary>
public sealed class RedactionEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        foreach (var (name, value) in logEvent.Properties.ToList())
        {
            if (SensitiveDataRedactor.IsSensitiveKey(name))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(SensitiveDataRedactor.Mask)));
            }
            else if (value is ScalarValue { Value: string text })
            {
                var redacted = SensitiveDataRedactor.Redact(text);
                if (!ReferenceEquals(redacted, text) && redacted != text)
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(name, new ScalarValue(redacted)));
                }
            }
        }
    }
}
