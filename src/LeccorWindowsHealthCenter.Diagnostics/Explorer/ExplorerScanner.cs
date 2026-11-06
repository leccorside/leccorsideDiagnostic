using System.Globalization;
using LeccorWindowsHealthCenter.Application.Explorer;
using LeccorWindowsHealthCenter.Application.Scanning;
using LeccorWindowsHealthCenter.Domain;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Diagnostics.Explorer;

public sealed class ExplorerScanner : DiagnosticScannerBase
{
    public const string ScannerId = "explorer";

    private readonly IExplorerProvider _provider;

    public ExplorerScanner(IExplorerProvider provider, ILogger<ExplorerScanner> logger)
        : base(logger)
    {
        _provider = provider;
    }

    public override string Id => ScannerId;

    public override string Name => "Windows Explorer";

    public override ScanModule Module => ScanModule.Explorer;

    public override bool IncludedInQuickScan => true;

    public override int Order => 80;

    protected override async Task CollectAsync(ScanCollector collector, CancellationToken cancellationToken)
    {
        var extensions = await _provider.GetShellExtensionsAsync(cancellationToken).ConfigureAwait(false);
        var cache = await _provider.GetCacheStatusAsync(cancellationToken).ConfigureAwait(false);

        var missingCount = extensions.Count(e => !e.FileExists);

        collector.AddMetric("explorer.shellex.total", extensions.Count, "extensões");
        collector.AddMetric("explorer.shellex.missing", missingCount, "extensões");
        collector.AddMetric("explorer.cache.icon_bytes", cache.IconCacheSizeBytes, "bytes");
        collector.AddMetric("explorer.cache.thumb_bytes", cache.ThumbnailCacheSizeBytes, "bytes");

        foreach (var ext in extensions)
        {
            var attributes = new Dictionary<string, string>
            {
                [ExplorerNames.AttrClsid] = ext.Clsid,
                [ExplorerNames.AttrName] = ext.Name,
                [ExplorerNames.AttrType] = ext.Type.ToString(),
                [ExplorerNames.AttrDllPath] = ext.DllPath,
                [ExplorerNames.AttrExists] = ext.FileExists.ToString(),
                [ExplorerNames.AttrWindows] = ext.IsWindowsOwned.ToString(),
                [ExplorerNames.AttrBlocked] = ext.IsBlocked.ToString(),
            };

            if (!string.IsNullOrWhiteSpace(ext.Publisher))
            {
                attributes[ExplorerNames.AttrPublisher] = ext.Publisher;
            }

            if (ext.IsSigned.HasValue)
            {
                attributes[ExplorerNames.AttrSigned] = ext.IsSigned.Value.ToString();
            }

            if (ext.IsRevoked)
            {
                attributes[ExplorerNames.AttrRevoked] = "True";
            }

            collector.AddObservation(
                ExplorerNames.ObservationShellExtension,
                ext.Name,
                attributes);
        }

        collector.AddObservation(
            ExplorerNames.ObservationCache,
            "ExplorerCache",
            new Dictionary<string, string>
            {
                ["iconCacheBytes"] = cache.IconCacheSizeBytes.ToString(CultureInfo.InvariantCulture),
                ["thumbnailCacheBytes"] = cache.ThumbnailCacheSizeBytes.ToString(CultureInfo.InvariantCulture),
                ["filesCount"] = cache.CacheFilesCount.ToString(CultureInfo.InvariantCulture),
            });
    }
}

