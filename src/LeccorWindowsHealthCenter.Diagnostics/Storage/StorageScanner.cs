using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;
using static LeccorWindowsHealthCenter.Diagnostics.Storage.StorageNames;

namespace LeccorWindowsHealthCenter.Diagnostics.Storage;

/// <summary>
/// Armazenamento (seção 15) na varredura: mede só os locais conhecidos de temporários e caches — rápido e sem
/// percorrer o disco inteiro. A árvore completa de pastas é sob demanda, na tela Armazenamento. Nada é apagado.
/// </summary>
public sealed class StorageScanner : DiagnosticScannerBase
{
    public const string ScannerId = "storage";

    private readonly ISpaceAnalyzer _analyzer;

    public StorageScanner(ISpaceAnalyzer analyzer, ILogger<StorageScanner> logger)
        : base(logger)
    {
        _analyzer = analyzer;
    }

    public override string Id => ScannerId;

    public override string Name => "Armazenamento";

    public override ScanModule Module => ScanModule.Storage;

    public override int Order => 60;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var measures = await _analyzer.MeasureLocationsAsync(StorageLocations.All, cancellationToken).ConfigureAwait(false);
        foreach (var measure in measures)
        {
            collector.AddObservation(ObservationLocation, measure.Location.Id, new Dictionary<string, string>
            {
                ["id"] = measure.Location.Id,
                ["title"] = measure.Location.Title,
                ["kind"] = measure.Location.Kind.ToString(),
                ["found"] = Invariant(measure.Found),
                ["sizeBytes"] = Invariant(measure.SizeBytes),
                ["files"] = Invariant(measure.FileCount),
                ["inaccessible"] = Invariant(measure.InaccessibleCount),
                ["paths"] = string.Join("; ", measure.ResolvedPaths),
            });
        }

        // A lixeira é decisão do usuário: fica fora do total de temporários e caches.
        collector.AddMetric(TempCacheTotal, measures.Where(m => m.Location.Kind != StorageLocationKind.RecycleBin).Sum(m => m.SizeBytes), "bytes");
    }
}
