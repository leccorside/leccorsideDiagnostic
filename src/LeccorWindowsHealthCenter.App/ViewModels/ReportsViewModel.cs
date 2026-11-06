using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed partial class ReportsViewModel : ObservableObject
{
    private readonly ScanSession _session;
    private readonly IScanRepository _repository;
    private readonly Dictionary<string, IReportExporter> _exporters;

    public ReportsViewModel(ScanSession session, IScanRepository repository, IEnumerable<IReportExporter> exporters)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        ArgumentNullException.ThrowIfNull(exporters);

        _exporters = exporters.ToDictionary(e => e.Format, StringComparer.OrdinalIgnoreCase);

        Formats =
        [
            new("HTML", "HTML — Visual Profissional (autocontido, navegadores e impressão)", ".html"),
            new("PDF", "PDF — Documento Portátil (padronizado PDF 1.4)", ".pdf"),
            new("CSV", "CSV — Dados Tabulares para Excel (UTF-8 com BOM)", ".csv"),
            new("JSON", "JSON — Dados Estruturados Completos", ".json"),
        ];
        _selectedFormat = Formats[0];

        _session.ReportChanged += (_, _) => _ = RefreshReportsAsync();
        _ = RefreshReportsAsync();
    }

    public IReadOnlyList<ExporterOptionViewModel> Formats { get; }

    [ObservableProperty]
    private ExporterOptionViewModel _selectedFormat;

    [ObservableProperty]
    private ObservableCollection<ReportOptionViewModel> _availableReports = [];

    [ObservableProperty]
    private ReportOptionViewModel? _selectedReport;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isExporting;

    public bool HasReport => SelectedReport is not null;

    partial void OnSelectedReportChanged(ReportOptionViewModel? value)
    {
        OnPropertyChanged(nameof(HasReport));
    }

    [RelayCommand]
    public async Task RefreshReportsAsync()
    {
        var list = new List<ReportOptionViewModel>();

        // Se houver relatório em memória na sessão atual
        if (_session.LastReport is not null)
        {
            list.Add(new ReportOptionViewModel(
                _session.LastReport.Id,
                $"Última Varredura (Sessão Atual) — {_session.LastReport.Mode}",
                _session.LastReport.CompletedAt,
                _session.LastReport.Score.Overall,
                _session.LastReport.Score.Rating,
                _session.LastReport.Problems.Count,
                _session.LastReport.Recommendations.Count,
                _session.LastReport));
        }

        try
        {
            var recents = await _repository.GetRecentAsync(10, CancellationToken.None).ConfigureAwait(true);
            foreach (var r in recents)
            {
                if (_session.LastReport?.Id == r.Id)
                {
                    continue;
                }

                list.Add(new ReportOptionViewModel(
                    r.Id,
                    $"Varredura {r.Mode} de {r.CompletedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)}",
                    r.CompletedAt,
                    r.Score,
                    r.Rating,
                    r.ProblemCount,
                    r.RecommendationCount,
                    null));
            }
        }
        catch
        {
            // Histórico offline / sem banco
        }

        AvailableReports = new ObservableCollection<ReportOptionViewModel>(list);
        if (SelectedReport is null && AvailableReports.Count > 0)
        {
            SelectedReport = AvailableReports[0];
        }
    }

    [RelayCommand]
    public async Task ExportToFileAsync()
    {
        if (SelectedReport is null || !_exporters.TryGetValue(SelectedFormat.Format, out var exporter))
        {
            return;
        }

        var report = await GetReportAsync(SelectedReport).ConfigureAwait(true);
        if (report is null)
        {
            StatusMessage = "Não foi possível carregar os dados da varredura selecionada.";
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = $"{SelectedFormat.Format} (*{SelectedFormat.Extension})|*{SelectedFormat.Extension}|Todos os Arquivos (*.*)|*.*",
            FileName = $"Laudo_Saude_{Environment.MachineName}_{report.CompletedAt:yyyyMMdd_HHmm}{SelectedFormat.Extension}"
        };

        if (sfd.ShowDialog() == true)
        {
            IsExporting = true;
            StatusMessage = "Exportando relatório...";
            try
            {
                await using var stream = File.Create(sfd.FileName);
                await exporter.ExportAsync(report, stream, CancellationToken.None).ConfigureAwait(true);
                StatusMessage = $"Relatório exportado com sucesso para:\n{sfd.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao exportar relatório: {ex.Message}";
            }
            finally
            {
                IsExporting = false;
            }
        }
    }

    [RelayCommand]
    public async Task OpenImmediatelyAsync()
    {
        if (SelectedReport is null || !_exporters.TryGetValue(SelectedFormat.Format, out var exporter))
        {
            return;
        }

        var report = await GetReportAsync(SelectedReport).ConfigureAwait(true);
        if (report is null)
        {
            StatusMessage = "Não foi possível carregar os dados da varredura selecionada.";
            return;
        }

        IsExporting = true;
        StatusMessage = "Gerando visualização temporária...";
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"Laudo_{report.Id:N}{SelectedFormat.Extension}");
            await using (var stream = File.Create(tempFile))
            {
                await exporter.ExportAsync(report, stream, CancellationToken.None).ConfigureAwait(true);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = tempFile,
                UseShellExecute = true
            });

            StatusMessage = $"Arquivo aberto no visualizador padrão do Windows.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao abrir visualização: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task<ScanReport?> GetReportAsync(ReportOptionViewModel item)
    {
        if (item.LoadedReport is not null)
        {
            return item.LoadedReport;
        }

        try
        {
            var findings = await _repository.GetFindingsAsync(item.Id, CancellationToken.None).ConfigureAwait(true);
            var score = new HealthScore
            {
                Overall = item.Score,
                Rating = item.Rating,
                Categories = new Dictionary<ScoreCategory, int>()
            };

            var dummyResult = new DiagnosticResult
            {
                ScannerId = "historical",
                ScannerName = "Histórico",
                Module = ScanModule.Hardware,
                Status = ScannerRunStatus.Completed,
                Findings = findings
            };

            return new ScanReport
            {
                Id = item.Id,
                Mode = ScanMode.Full,
                StartedAt = item.CompletedAt.AddSeconds(-30),
                CompletedAt = item.CompletedAt,
                WasCancelled = false,
                Results = [dummyResult],
                Score = score,
                MachineName = Environment.MachineName
            };
        }
        catch
        {
            return null;
        }
    }
}

public sealed record ExporterOptionViewModel(string Format, string DisplayName, string Extension);

public sealed record ReportOptionViewModel(
    Guid Id,
    string DisplayTitle,
    DateTimeOffset CompletedAt,
    int? Score,
    HealthRating Rating,
    int ProblemsCount,
    int RecommendationsCount,
    ScanReport? LoadedReport)
{
    public string ScoreDisplay => Score.HasValue ? $"{Score.Value}/100" : "—";
}

