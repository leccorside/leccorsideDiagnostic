using System.IO;
using LeccorWindowsHealthCenter.App;
using LeccorWindowsHealthCenter.Application.Commands;
using LeccorWindowsHealthCenter.Application.Persistence;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Application.Reporting;
using LeccorWindowsHealthCenter.Application.Rules;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Infrastructure.Persistence;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.SystemTests;

/// <summary>
/// Sobe o host real do aplicativo (mesma composição do .exe) com diretório de dados isolado
/// e executa o fluxo VARREDURA → DIAGNÓSTICO → CLASSIFICAÇÃO → HISTÓRICO de ponta a ponta.
/// </summary>
public sealed class HostCompositionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lwhc-system-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private IHost BuildHost(Action<IServiceCollection>? overrides = null) =>
        HealthCenterHost.Build([], new AppPaths(_root, isPortable: true), overrides);

    [Fact]
    public void Container_resolves_all_core_services()
    {
        using var host = BuildHost();
        var services = host.Services;

        Assert.NotNull(services.GetRequiredService<IScanOrchestrator>());
        Assert.NotNull(services.GetRequiredService<IRuleEngine>());
        Assert.NotNull(services.GetRequiredService<IRepairEngine>());
        Assert.NotNull(services.GetRequiredService<ICommandExecutor>());
        Assert.NotNull(services.GetRequiredService<IScanRepository>());
        Assert.NotNull(services.GetRequiredService<IRepairHistoryRepository>());
        Assert.NotNull(services.GetRequiredService<IBackupStore>());
        Assert.NotNull(services.GetRequiredService<ILiveMetricsProvider>());
        Assert.NotEmpty(services.GetServices<IReportExporter>());
    }

    [Fact]
    public async Task Scan_without_scanners_reports_no_data_instead_of_a_perfect_score()
    {
        using var host = BuildHost();
        await host.Services.GetRequiredService<MigrationRunner>().MigrateAsync(CancellationToken.None);

        var report = await host.Services.GetRequiredService<IScanOrchestrator>().RunAsync(ScanPlan.Full, null, CancellationToken.None);

        Assert.Null(report.Score.Overall);
        Assert.Equal(HealthRating.Unknown, report.Score.Rating);
        var saved = await host.Services.GetRequiredService<IScanRepository>().GetRecentAsync(5, CancellationToken.None);
        Assert.Single(saved);
    }

    [Fact]
    public async Task End_to_end_scan_with_scanner_and_rule_persists_findings_and_logs()
    {
        using (var host = BuildHost(services =>
               {
                   services.AddSingleton<IDiagnosticScanner, SyntheticCpuScanner>();
                   services.AddSingleton<IRule, SustainedCpuRule>();
               }))
        {
            await host.Services.GetRequiredService<MigrationRunner>().MigrateAsync(CancellationToken.None);

            var report = await host.Services.GetRequiredService<IScanOrchestrator>().RunAsync(ScanPlan.Quick, null, CancellationToken.None);

            var problem = Assert.Single(report.Problems);
            Assert.Equal("cpu.sustained-high", problem.RuleId);
            Assert.NotEmpty(problem.Evidence);
            Assert.NotNull(report.Score.Overall);
            Assert.True(report.Score.Overall < 100);

            var findings = await host.Services.GetRequiredService<IScanRepository>().GetFindingsAsync(report.Id, CancellationToken.None);
            Assert.Equal(problem.Title, Assert.Single(findings).Title);
        }

        // Host descartado → logger Serilog descarregado em disco.
        var log = Assert.Single(Directory.GetFiles(Path.Combine(_root, "Logs"), "lwhc-*.log"));
        Assert.Contains("synthetic-cpu", File.ReadAllText(log), StringComparison.Ordinal);
    }

    private sealed class SyntheticCpuScanner : DiagnosticScannerBase
    {
        public SyntheticCpuScanner(ILogger<SyntheticCpuScanner> logger)
            : base(logger)
        {
        }

        public override string Id => "synthetic-cpu";

        public override string Name => "CPU (sintética)";

        public override ScanModule Module => ScanModule.Performance;

        protected override Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
        {
            var start = DateTimeOffset.UtcNow.AddMinutes(-5);
            for (var i = 0; i < 60; i++)
            {
                collector.AddMetric("cpu.total", 97, "%", timestamp: start.AddSeconds(i * 5));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SustainedCpuRule : IRule
    {
        public string Id => "cpu.sustained-high";

        public string Name => "CPU alta sustentada";

        public IReadOnlyList<string> RequiredScanners { get; } = ["synthetic-cpu"];

        public IReadOnlyList<RuleParameter> Parameters { get; } =
        [
            new("Threshold", 90, "%", "Acima de 90% sobra pouca margem para tarefas interativas."),
            new("MinimumSustainedSeconds", 120, "s", "Picos curtos são normais."),
        ];

        public IEnumerable<Finding> Evaluate(RuleContext context)
        {
            var series = context.Series("cpu.total");
            var sustained = series.LongestRunAtOrAbove(context.Parameter("Threshold"));
            if (sustained.TotalSeconds < context.Parameter("MinimumSustainedSeconds"))
            {
                yield break;
            }

            yield return new Finding
            {
                RuleId = Id,
                ScannerId = "synthetic-cpu",
                Category = FindingCategory.Cpu,
                Title = "CPU acima de 90% por período prolongado",
                Description = $"A CPU ficou acima de 90% por {sustained.TotalMinutes:0.#} minutos.",
                Severity = Severity.Medium,
                Confidence = Confidence.Confirmed,
                Evidence = [new Evidence { Source = EvidenceSource.PerformanceCounter, Description = "Média", Value = series.Mean, Unit = "%", SampleCount = series.Count }],
                Impact = "Lentidão em tarefas interativas.",
                Recommendation = "Identificar o processo responsável.",
            };
        }
    }
}
