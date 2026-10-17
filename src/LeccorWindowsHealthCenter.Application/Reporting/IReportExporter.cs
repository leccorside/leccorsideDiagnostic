using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.Application.Reporting;

/// <summary>Exportador de relatórios (seção 54). Um por formato: JSON, HTML, CSV, PDF.</summary>
public interface IReportExporter
{
    string Format { get; }

    string FileExtension { get; }

    Task ExportAsync(ScanReport report, Stream destination, CancellationToken cancellationToken);
}
