using System.Diagnostics;
using LeccorWindowsHealthCenter.Application.Explorer;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Explorer;

public sealed class FolderBenchmarkService : IFolderBenchmarkService
{
    private readonly ILogger<FolderBenchmarkService> _logger;

    public FolderBenchmarkService(ILogger<FolderBenchmarkService> logger)
    {
        _logger = logger;
    }

    public async Task<FolderBenchmarkResult> BenchmarkFolderAsync(string folderPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"A pasta '{folderPath}' não foi encontrada.");
        }

        var fullPath = Path.GetFullPath(folderPath);
        var totalSw = Stopwatch.StartNew();

        return await Task.Run(() =>
        {
            var enumSw = Stopwatch.StartNew();
            var fileList = new List<string>();
            var dirCount = 0;

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.Exists(entry))
                    {
                        fileList.Add(entry);
                    }
                    else if (Directory.Exists(entry))
                    {
                        dirCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha durante enumeração de pasta para benchmark: {Path}", fullPath);
            }
            enumSw.Stop();

            var metaSw = Stopwatch.StartNew();
            long totalBytes = 0;
            var filesWithAttr = 0;

            foreach (var file in fileList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    totalBytes += fi.Length;
                    _ = fi.Attributes;
                    _ = fi.LastWriteTimeUtc;
                    filesWithAttr++;
                }
                catch
                {
                    // Arquivo inacessível ou em uso exclusivo
                }
            }
            metaSw.Stop();
            totalSw.Stop();

            var bottlenecks = new List<string>();

            if (fileList.Count >= 3000)
            {
                bottlenecks.Add($"Quantidade excessiva de arquivos na mesma pasta ({fileList.Count:N0} arquivos). O Windows Explorer tem lentidão conhecida para indexar e ordenar listas acima de 3.000 itens.");
            }

            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal) || IsNetworkDrive(fullPath))
            {
                bottlenecks.Add("Pasta localizada em unidade de rede (compartilhamento SMB). O tempo de resposta depende da latência e banda da conexão de rede.");
            }

            if (IsCloudSyncFolder(fullPath))
            {
                bottlenecks.Add("Pasta integrada a provedor de nuvem (ex.: OneDrive, Google Drive, Dropbox). A consulta a arquivos sob demanda pode causar pausas na listagem.");
            }

            if (metaSw.ElapsedMilliseconds > 1500 && fileList.Count > 0)
            {
                var avgPerFileMs = metaSw.Elapsed.TotalMilliseconds / fileList.Count;
                if (avgPerFileMs > 1.5)
                {
                    bottlenecks.Add($"Leitura lenta de metadados ({avgPerFileMs:0.1} ms por arquivo). Isso geralmente indica intervenção profunda de filtro antivírus em tempo real ou latência de disco.");
                }
            }

            return new FolderBenchmarkResult
            {
                FolderPath = fullPath,
                EnumerationDuration = enumSw.Elapsed,
                MetadataReadDuration = metaSw.Elapsed,
                TotalDuration = totalSw.Elapsed,
                FileCount = fileList.Count,
                DirectoryCount = dirCount,
                TotalSizeBytes = totalBytes,
                IdentifiedBottlenecks = bottlenecks,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsNetworkDrive(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                return drive.DriveType == DriveType.Network;
            }
        }
        catch
        {
        }
        return false;
    }

    private static bool IsCloudSyncFolder(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.Contains("onedrive") || lower.Contains("dropbox") || lower.Contains("google drive") || lower.Contains("nextcloud");
    }
}

