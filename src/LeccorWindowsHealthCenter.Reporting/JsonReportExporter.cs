using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Reporting;

/// <summary>Exporta o relatório completo (findings, evidências, métricas, score) em JSON.</summary>
public sealed class JsonReportExporter : IReportExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Format => "JSON";

    public string FileExtension => ".json";

    public Task ExportAsync(ScanReport report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.SerializeAsync(destination, report, Options, cancellationToken);
    }
}
