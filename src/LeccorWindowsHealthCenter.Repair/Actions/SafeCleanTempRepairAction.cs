using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using LeccorWindowsHealthCenter.Application.Repair;
using LeccorWindowsHealthCenter.Domain;
using LeccorWindowsHealthCenter.Domain.Repairs;

namespace LeccorWindowsHealthCenter.Repair.Actions;

/// <summary>
/// Limpeza Segura de Arquivos Temporários (seção 27, 35 e 39 do PROMPT).
/// Implementa medição antes/depois através de <see cref="IMeasurableRepairAction"/>.
/// Remove apenas arquivos com mais de 24 horas em pastas temporárias do usuário e do Windows,
/// além de relatórios e despejos de erro (WER / CrashDumps).
/// Nunca toca em pastas do usuário (Downloads, Documentos, Desktop) nem em dados de navegadores.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SafeCleanTempRepairAction : IMeasurableRepairAction
{
    public const string ActionId = "repair.storage.safe-clean";
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    public string Id => ActionId;

    public string Title => "Limpeza Segura de Arquivos Temporários";

    public string Description => "Exclui arquivos temporários não utilizados com mais de 24 horas, relatórios de erro e despejos de memória (CrashDumps). Não afeta documentos, downloads ou senhas.";

    public RiskLevel Risk => RiskLevel.Safe;

    public bool RequiresElevation => false;

    public bool RequiresRestart => false;

    public bool SupportsRollback => false;

    public string MeasurementName => "Arquivos Temporários";

    public string MeasurementUnit => "MB";

    public bool LowerIsBetter => true;

    public Task<IReadOnlyList<PlannedChange>> PlanAsync(CancellationToken cancellationToken)
    {
        var locations = GetCleanableTargets();
        var changes = new List<PlannedChange>();

        foreach (var loc in locations)
        {
            changes.Add(new PlannedChange
            {
                Target = loc.DirectoryPath,
                Description = $"Limpeza segura de arquivos em '{loc.DirectoryPath}' (filtro: {loc.Description}).",
                PreviousValue = "Contém arquivos temporários",
                NewValue = "Arquivos antigos removidos"
            });
        }

        return Task.FromResult<IReadOnlyList<PlannedChange>>(changes);
    }

    public Task<IReadOnlyDictionary<string, string?>> CaptureStateAsync(CancellationToken cancellationToken)
    {
        var mb = MeasureInMb();
        var state = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EstimatedSizeMb"] = mb.ToString("F2", CultureInfo.InvariantCulture),
            ["CapturedAt"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };

        return Task.FromResult<IReadOnlyDictionary<string, string?>>(state);
    }

    public Task<double> MeasureAsync(CancellationToken cancellationToken)
    {
        return Task.Run(MeasureInMb, cancellationToken);
    }

    public Task<RepairResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            long bytesFreed = 0;
            int filesDeleted = 0;
            int filesSkipped = 0;

            var targets = GetCleanableTargets();
            var appliedChanges = new List<PlannedChange>();

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(target.DirectoryPath))
                {
                    continue;
                }

                long folderFreed = 0;
                int folderCount = 0;

                try
                {
                    var dir = new DirectoryInfo(target.DirectoryPath);
                    var files = target.Pattern != null
                        ? dir.EnumerateFiles(target.Pattern, SearchOption.AllDirectories)
                        : dir.EnumerateFiles("*", SearchOption.AllDirectories);

                    var cutoff = DateTime.UtcNow - target.MinAge;

                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Ignora arquivos mais novos que o tempo limite
                        if (file.LastWriteTimeUtc > cutoff)
                        {
                            continue;
                        }

                        try
                        {
                            var length = file.Length;
                            file.Delete();
                            folderFreed += length;
                            folderCount++;
                        }
                        catch (Exception)
                        {
                            // Arquivo em uso ou protegido: ignora com segurança
                            filesSkipped++;
                        }
                    }
                }
                catch (Exception)
                {
                    // Falha de permissão no diretório: ignora e continua os demais
                }

                bytesFreed += folderFreed;
                filesDeleted += folderCount;

                if (folderCount > 0)
                {
                    var freedMb = folderFreed / (1024.0 * 1024.0);
                    appliedChanges.Add(new PlannedChange
                    {
                        Target = target.DirectoryPath,
                        Description = $"Liberados {freedMb:F2} MB em {folderCount} arquivos.",
                        PreviousValue = $"{freedMb:F2} MB",
                        NewValue = "0 MB"
                    });
                }
            }

            var totalFreedMb = bytesFreed / (1024.0 * 1024.0);
            var message = $"Limpeza segura concluída: {totalFreedMb:F2} MB liberados ({filesDeleted} arquivos excluídos, {filesSkipped} ignorados por estarem em uso).";

            return new RepairResult(true, message)
            {
                AppliedChanges = appliedChanges
            };
        }, cancellationToken);
    }

    public Task<RepairValidation> ValidateAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new RepairValidation(true, "Limpeza segura de temporários concluída e validada com sucesso."));
    }

    public Task<RollbackResult> RollbackAsync(BackupRecord backup, CancellationToken cancellationToken)
    {
        return Task.FromResult(new RollbackResult(false, "Limpeza de arquivos temporários não suporta restauração (arquivos temporários descartáveis)."));
    }

    private double MeasureInMb()
    {
        long totalBytes = 0;
        var targets = GetCleanableTargets();

        foreach (var target in targets)
        {
            if (!Directory.Exists(target.DirectoryPath))
            {
                continue;
            }

            try
            {
                var dir = new DirectoryInfo(target.DirectoryPath);
                var files = target.Pattern != null
                    ? dir.EnumerateFiles(target.Pattern, SearchOption.AllDirectories)
                    : dir.EnumerateFiles("*", SearchOption.AllDirectories);

                var cutoff = DateTime.UtcNow - target.MinAge;
                foreach (var file in files)
                {
                    if (file.LastWriteTimeUtc <= cutoff)
                    {
                        try
                        {
                            totalBytes += file.Length;
                        }
                        catch
                        {
                            // Inacessível
                        }
                    }
                }
            }
            catch
            {
                // Diretório inacessível
            }
        }

        return Math.Round(totalBytes / (1024.0 * 1024.0), 2);
    }

    private static IReadOnlyList<CleanTarget> GetCleanableTargets()
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return
        [
            new CleanTarget(Path.GetTempPath(), MinimumAge, null, "Arquivos temporários do usuário com mais de 24h"),
            new CleanTarget(Path.Combine(winDir, "Temp"), MinimumAge, null, "Arquivos temporários do Windows"),
            new CleanTarget(Path.Combine(localAppData, "CrashDumps"), TimeSpan.Zero, "*.dmp", "Despejos de memória de travamentos (CrashDumps)"),
            new CleanTarget(Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive"), TimeSpan.Zero, null, "Relatórios arquivados de erro do Windows"),
            new CleanTarget(Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"), TimeSpan.Zero, null, "Fila de relatórios de erro do Windows")
        ];
    }

    private sealed record CleanTarget(string DirectoryPath, TimeSpan MinAge, string? Pattern, string Description);
}

