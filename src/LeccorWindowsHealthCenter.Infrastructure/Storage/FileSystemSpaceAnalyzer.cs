using System.Diagnostics;
using System.IO.Enumeration;
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeccorWindowsHealthCenter.Infrastructure.Storage;

/// <summary>
/// Análise de espaço somente leitura (seção 15). Regras que evitam números errados:
/// <list type="bullet">
/// <item>Junções e links simbólicos de pasta não são seguidos (evita laços e contar a mesma pasta duas vezes).</item>
/// <item>Arquivos do OneDrive que só existem na nuvem não contam como espaço usado (somados à parte).</item>
/// <item>Pasta sem permissão é contada, não ignorada em silêncio: a UI avisa que o total pode ser maior.</item>
/// </list>
/// </summary>
public sealed class FileSystemSpaceAnalyzer : ISpaceAnalyzer
{
    private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    private const FileAttributes CloudOnly = FileAttributes.Offline | RecallOnDataAccess;

    private static readonly EnumerationOptions Enumeration = new()
    {
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    private readonly SpaceAnalysisOptions _options;
    private readonly ILogger<FileSystemSpaceAnalyzer> _logger;
    private readonly TimeProvider _timeProvider;

    public FileSystemSpaceAnalyzer(IOptions<SpaceAnalysisOptions> options, ILogger<FileSystemSpaceAnalyzer> logger, TimeProvider timeProvider)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<SpaceAnalysis> AnalyzeAsync(string root, bool solidState, IProgress<SpaceProgress>? progress, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(root);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"A pasta '{fullPath}' não existe.");
        }

        var started = _timeProvider.GetTimestamp();
        var concurrency = solidState ? _options.MaxConcurrency : _options.RotationalMaxConcurrency;
        using var walk = new Walk(_options, concurrency, progress, _timeProvider.GetUtcNow() - _options.OldFileAge);
        var node = await Task.Run(() => walk.VisitAsync(fullPath, DisplayName(fullPath), 0, cancellationToken), cancellationToken).ConfigureAwait(false);
        var duration = _timeProvider.GetElapsedTime(started);

        _logger.LogInformation("Análise de espaço de {Root}: {Bytes} bytes em {Files} arquivos, {Folders} pastas, {Inaccessible} sem acesso, {DurationMs} ms, {Concurrency} leitores",
            fullPath, node.SizeBytes, node.FileCount, walk.Folders, node.InaccessibleCount, (long)duration.TotalMilliseconds, concurrency);

        return new SpaceAnalysis
        {
            Root = node,
            LargestFiles = walk.LargestFiles(),
            OldFiles = walk.OldFiles(),
            Duration = duration,
            CapturedAt = _timeProvider.GetUtcNow(),
        };
    }

    public async Task<IReadOnlyList<LocationMeasure>> MeasureLocationsAsync(IReadOnlyList<StorageLocation> locations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locations);
        var results = new LocationMeasure[locations.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, locations.Count),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency), CancellationToken = cancellationToken },
            (index, ct) =>
            {
                results[index] = Measure(locations[index], ct);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);
        return results;
    }

    /// <summary>Expande variáveis de ambiente e "*" (um nível, ex.: perfis do navegador). Só caminhos existentes.</summary>
    internal static IReadOnlyList<string> Resolve(string pattern)
    {
        var expanded = Environment.ExpandEnvironmentVariables(pattern);
        if (expanded.Contains('%', StringComparison.Ordinal))
        {
            return []; // Variável inexistente nesta máquina.
        }

        IEnumerable<string> current = [string.Empty];
        foreach (var segment in expanded.Split(Path.DirectorySeparatorChar))
        {
            current = segment == "*"
                ? current.SelectMany(SafeSubdirectories)
                : current.Select(c => c.Length == 0 ? segment + Path.DirectorySeparatorChar : Path.Combine(c, segment));
        }

        return current.Select(Path.TrimEndingDirectorySeparator)
            .Where(p => Directory.Exists(p) || File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static LocationMeasure Measure(StorageLocation location, CancellationToken cancellationToken)
    {
        if (location.Id == StorageLocations.RecycleBinId)
        {
            var info = new NativeMethods.ShQueryRbInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ShQueryRbInfo>() };
            var found = NativeMethods.ShQueryRecycleBin(null, ref info) == 0;
            return new LocationMeasure { Location = location, Found = found, SizeBytes = found ? info.SizeBytes : 0, FileCount = found ? info.NumItems : 0 };
        }

        var paths = location.PathPatterns.SelectMany(Resolve).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        long bytes = 0, files = 0;
        var inaccessible = 0;
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                bytes += new FileInfo(path).Length;
                files++;
                continue;
            }

            var (b, f, i) = Sum(path, cancellationToken);
            bytes += b;
            files += f;
            inaccessible += i;
        }

        return new LocationMeasure { Location = location, Found = paths.Count > 0, SizeBytes = bytes, FileCount = files, InaccessibleCount = inaccessible, ResolvedPaths = paths };
    }

    private static (long Bytes, long Files, int Inaccessible) Sum(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long bytes = 0, files = 0;
        var inaccessible = 0;
        var subdirectories = new List<string>();
        try
        {
            var entries = new FileSystemEnumerable<(string? Directory, long Length, FileAttributes Attributes)>(
                path,
                (ref FileSystemEntry e) => (e.IsDirectory ? e.ToFullPath() : null, e.Length, e.Attributes),
                Enumeration);
            foreach (var (directory, length, attributes) in entries)
            {
                if (directory is not null)
                {
                    if ((attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        subdirectories.Add(directory);
                    }
                }
                else if ((attributes & CloudOnly) == 0)
                {
                    bytes += length;
                    files++;
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return (0, 0, 1);
        }

        foreach (var sub in subdirectories)
        {
            var (b, f, i) = Sum(sub, cancellationToken);
            bytes += b;
            files += f;
            inaccessible += i;
        }

        return (bytes, files, inaccessible);
    }

    private static string DisplayName(string fullPath) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath)) is { Length: > 0 } name ? name : fullPath;

    private static IEnumerable<string> SafeSubdirectories(string parent)
    {
        try
        {
            return parent.Length == 0 ? [] : Directory.GetDirectories(parent);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private readonly record struct Entry(string? Name, bool IsDirectory, long Length, FileAttributes Attributes, DateTimeOffset LastWrite);

    /// <summary>Estado de uma análise: contadores, top-N de arquivos e vagas de paralelismo.</summary>
    private sealed class Walk : IDisposable
    {
        private const int MaxInaccessibleShown = 10;
        private static readonly long ProgressIntervalTicks = Stopwatch.Frequency / 5;

        private readonly SpaceAnalysisOptions _options;
        private readonly IProgress<SpaceProgress>? _progress;
        private readonly DateTimeOffset _oldCutoff;
        private readonly SemaphoreSlim _slots;
        private readonly Lock _filesLock = new();
        private readonly PriorityQueue<FileEntry, long> _largest = new();
        private readonly PriorityQueue<FileEntry, long> _old = new();
        private long _largestThreshold;
        private long _files;
        private long _bytes;
        private long _folders;
        private long _lastProgress;

        public Walk(SpaceAnalysisOptions options, int concurrency, IProgress<SpaceProgress>? progress, DateTimeOffset oldCutoff)
        {
            _options = options;
            _progress = progress;
            _oldCutoff = oldCutoff;

            // A thread atual já é uma leitora; as vagas são as leituras extras em paralelo.
            _slots = new SemaphoreSlim(Math.Max(0, concurrency - 1));
        }

        public long Folders => Interlocked.Read(ref _folders);

        public void Dispose() => _slots.Dispose();

        public async Task<FolderNode> VisitAsync(string path, string name, int depth, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _folders);

            long filesBytes = 0, fileCount = 0, cloudBytes = 0;
            var subdirectories = new List<(string Path, string Name)>();
            var nameThreshold = Math.Min(Volatile.Read(ref _largestThreshold), _options.OldFileMinimumBytes);
            try
            {
                var entries = new FileSystemEnumerable<Entry>(
                    path,
                    (ref FileSystemEntry e) => new Entry(
                        e.IsDirectory || e.Length >= nameThreshold ? e.FileName.ToString() : null,
                        e.IsDirectory,
                        e.Length,
                        e.Attributes,
                        e.LastWriteTimeUtc),
                    Enumeration);

                foreach (var entry in entries)
                {
                    if (entry.IsDirectory)
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            subdirectories.Add((Path.Combine(path, entry.Name!), entry.Name!));
                        }

                        continue;
                    }

                    if ((entry.Attributes & CloudOnly) != 0)
                    {
                        cloudBytes += entry.Length;
                        continue;
                    }

                    filesBytes += entry.Length;
                    fileCount++;
                    if (entry.Name is not null)
                    {
                        Track(new FileEntry(Path.Combine(path, entry.Name), entry.Length, entry.LastWrite));
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return new FolderNode { Name = name, Path = path, Inaccessible = true, InaccessibleCount = 1 };
            }

            Interlocked.Add(ref _files, fileCount);
            Interlocked.Add(ref _bytes, filesBytes);
            Report(path);

            var children = new List<FolderNode>(subdirectories.Count);
            var spawned = new List<Task<FolderNode>>();
            foreach (var (subPath, subName) in subdirectories)
            {
                // Vaga livre em qualquer profundidade: os leitores ficam ocupados até o fim (limitar a profundidade
                // deixava um leitor sozinho em subárvores grandes como Users\...\AppData).
                if (_slots.Wait(0, cancellationToken))
                {
                    spawned.Add(Task.Run(async () =>
                    {
                        try
                        {
                            return await VisitAsync(subPath, subName, depth + 1, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            _slots.Release();
                        }
                    }, cancellationToken));
                }
                else
                {
                    children.Add(await VisitAsync(subPath, subName, depth + 1, cancellationToken).ConfigureAwait(false));
                }
            }

            children.AddRange(await Task.WhenAll(spawned).ConfigureAwait(false));
            return Build(name, path, filesBytes, fileCount, cloudBytes, children);
        }

        public List<FileEntry> LargestFiles()
        {
            lock (_filesLock)
            {
                return _largest.UnorderedItems.Select(i => i.Element).OrderByDescending(f => f.SizeBytes).ToList();
            }
        }

        public List<FileEntry> OldFiles()
        {
            lock (_filesLock)
            {
                return _old.UnorderedItems.Select(i => i.Element).OrderByDescending(f => f.SizeBytes).ToList();
            }
        }

        private FolderNode Build(string name, string path, long filesBytes, long fileCount, long cloudBytes, List<FolderNode> children)
        {
            children.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            var kept = children.Where(c => c.SizeBytes >= _options.MinFolderBytes).Take(_options.MaxChildrenPerFolder).ToList();

            // Pasta sem permissão tem 0 B e sumiria na poda: a legenda diria "N pastas sem permissão" sem mostrar quais.
            kept.AddRange(children.Where(c => c.Inaccessible && !kept.Contains(c)).Take(MaxInaccessibleShown));
            var hidden = children.Count - kept.Count;

            return new FolderNode
            {
                Name = name,
                Path = path,
                FilesBytes = filesBytes,
                SizeBytes = filesBytes + children.Sum(c => c.SizeBytes),
                FileCount = fileCount + children.Sum(c => c.FileCount),
                Children = kept,
                HiddenChildrenCount = hidden,
                HiddenChildrenBytes = children.Sum(c => c.SizeBytes) - kept.Sum(c => c.SizeBytes),
                InaccessibleCount = children.Sum(c => c.InaccessibleCount),
                CloudOnlyBytes = cloudBytes + children.Sum(c => c.CloudOnlyBytes),
            };
        }

        private void Track(FileEntry file)
        {
            lock (_filesLock)
            {
                Push(_largest, file, _options.TopFiles);
                if (_largest.Count >= _options.TopFiles && _largest.TryPeek(out _, out var smallest))
                {
                    Volatile.Write(ref _largestThreshold, smallest);
                }

                // Antes de 1980 não é "antigo", é carimbo inválido (zerado/corrompido): fica fora da lista.
                if (file.SizeBytes >= _options.OldFileMinimumBytes && file.LastWrite < _oldCutoff && file.LastWrite.Year >= 1980)
                {
                    Push(_old, file, _options.TopFiles);
                }
            }
        }

        private static void Push(PriorityQueue<FileEntry, long> heap, FileEntry file, int limit)
        {
            if (heap.Count < limit)
            {
                heap.Enqueue(file, file.SizeBytes);
            }
            else if (heap.TryPeek(out _, out var smallest) && file.SizeBytes > smallest)
            {
                heap.EnqueueDequeue(file, file.SizeBytes);
            }
        }

        private void Report(string currentFolder)
        {
            if (_progress is null)
            {
                return;
            }

            var now = Stopwatch.GetTimestamp();
            var last = Interlocked.Read(ref _lastProgress);
            if (now - last >= ProgressIntervalTicks && Interlocked.CompareExchange(ref _lastProgress, now, last) == last)
            {
                _progress.Report(new SpaceProgress(Interlocked.Read(ref _files), Interlocked.Read(ref _bytes), Interlocked.Read(ref _folders), currentFolder));
            }
        }
    }
}
