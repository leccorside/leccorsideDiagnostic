using System.IO;
using LeccorWindowsHealthCenter.Application.Storage;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.Infrastructure.Storage;

public sealed class SafeCleanProvider : ISafeCleanProvider
{
    private readonly ILogger<SafeCleanProvider> _logger;

    public SafeCleanProvider(ILogger<SafeCleanProvider> logger)
    {
        _logger = logger;
    }

    public async Task<CleanAnalysisResult> AnalyzeCleanableLocationsAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var safeItems = new List<CleanItem>
            {
                MeasureLocation(
                    "clean.temp.windows",
                    "Temporários do Windows",
                    "Arquivos temporários criados por serviços do sistema operacional e instaladores.",
                    CleanCategory.Safe,
                    [Path.Combine(winDir, "Temp")],
                    "Arquivos temporários antigos podem ser removidos com total segurança para liberar espaço em disco.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.temp.user",
                    "Temporários de Usuário (> 24h)",
                    "Arquivos temporários gerados por programas do usuário que não foram excluídos após o fechamento.",
                    CleanCategory.Safe,
                    [Path.GetTempPath()],
                    "Arquivos com mais de 24 horas não estão mais em uso ativo por nenhum processo.",
                    filterOlderThan: TimeSpan.FromHours(24),
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.thumbnails",
                    "Cache de Miniaturas do Explorer",
                    "Miniaturas de imagens, vídeos e documentos geradas para exibição no Windows Explorer.",
                    CleanCategory.Safe,
                    [Path.Combine(localAppData, @"Microsoft\Windows\Explorer")],
                    "O cache é recriado dinamicamente pelo Windows quando você abre as pastas.",
                    filterOlderThan: null,
                    filePattern: "thumbcache_*.db",
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.crashdumps",
                    "Relatórios e Despejos de Erro (WER)",
                    "Relatórios de diagnóstico de travamentos e arquivos de Crash Dump de aplicativos.",
                    CleanCategory.Safe,
                    [Path.Combine(localAppData, "CrashDumps"), Path.Combine(programData, @"Microsoft\Windows\WER")],
                    "Relatórios históricos de erros não afetam o funcionamento do sistema.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.delivery",
                    "Cache de Otimização de Entrega",
                    "Arquivos de distribuição e compartilhamento local do Windows Update.",
                    CleanCategory.Safe,
                    [Path.Combine(winDir, @"SoftwareDistribution\DeliveryOptimization")],
                    "Arquivos de cache que podem ser baixados novamente sob demanda pelo Windows.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),
            };

            var reviewItems = new List<CleanItem>
            {
                MeasureLocation(
                    "clean.browser.edge",
                    "Cache do Microsoft Edge",
                    "Arquivos temporários da web, imagens e scripts salvos para acelerar a navegação.",
                    CleanCategory.Review,
                    [Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache")],
                    "Limpar o cache libera espaço, mas exigirá novo download de imagens em sites visitados.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.browser.chrome",
                    "Cache do Google Chrome",
                    "Arquivos temporários e páginas em cache do navegador Chrome.",
                    CleanCategory.Review,
                    [Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache")],
                    "Limpar o cache libera espaço, mas exigirá novo download de imagens em sites visitados.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.downloads",
                    "Downloads Antigos (> 30 dias)",
                    "Arquivos baixados da internet que não foram modificados há mais de 30 dias.",
                    CleanCategory.Review,
                    [Path.Combine(userProfile, "Downloads")],
                    "Requer revisão manual: podem conter instaladores, documentos ou fotos pessoais.",
                    filterOlderThan: TimeSpan.FromDays(30),
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.logs",
                    "Logs Residuais do Windows",
                    "Arquivos de registro de eventos de instalação e manutenção do sistema.",
                    CleanCategory.Review,
                    [Path.Combine(winDir, "Logs")],
                    "Logs antigos podem ser úteis para auditoria técnica recente.",
                    filterOlderThan: null,
                    filePattern: "*.log",
                    cancellationToken: cancellationToken),
            };

            var advancedItems = new List<CleanItem>
            {
                MeasureLocation(
                    "clean.memorydump",
                    "Despejo de Memória Completo (MEMORY.DMP)",
                    "Arquivo de despejo de tela azul (BSOD) gerado pelo kernel do Windows.",
                    CleanCategory.Advanced,
                    [Path.Combine(winDir, "MEMORY.DMP")],
                    "Despejos ocupam vários gigabytes da RAM física. Úteis apenas para depuração avançada com WinDbg.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),

                MeasureLocation(
                    "clean.winsxs",
                    "Backup de Componentes Substituídos (WinSxS)",
                    "Cópias de segurança de versões anteriores de arquivos do sistema mantidos após updates.",
                    CleanCategory.Advanced,
                    [Path.Combine(winDir, @"WinSxS\Backup")],
                    "Liberar versões substituídas impede a reversão de atualizações antigas do Windows.",
                    filterOlderThan: null,
                    filePattern: null,
                    cancellationToken: cancellationToken),
            };

            var safeSummary = new CleanCategorySummary
            {
                Category = CleanCategory.Safe,
                Title = "Seguro (Safe)",
                Description = "Arquivos temporários e caches comprovadamente descartáveis que não afetam suas aplicações nem seus documentos pessoais.",
                TotalSizeBytes = safeItems.Sum(i => i.TotalSizeBytes),
                TotalFilesCount = safeItems.Sum(i => i.FileCount),
                Items = safeItems,
            };

            var reviewSummary = new CleanCategorySummary
            {
                Category = CleanCategory.Review,
                Title = "Revisar (Review)",
                Description = "Caches de navegadores, pastas de downloads e arquivos que merecem conferência antes de qualquer decisão.",
                TotalSizeBytes = reviewItems.Sum(i => i.TotalSizeBytes),
                TotalFilesCount = reviewItems.Sum(i => i.FileCount),
                Items = reviewItems,
            };

            var advancedSummary = new CleanCategorySummary
            {
                Category = CleanCategory.Advanced,
                Title = "Avançado (Advanced)",
                Description = "Despejos de memória do kernel e repositórios de componentes do sistema. Exigem conhecimento técnico.",
                TotalSizeBytes = advancedItems.Sum(i => i.TotalSizeBytes),
                TotalFilesCount = advancedItems.Sum(i => i.FileCount),
                Items = advancedItems,
            };

            var summaries = new[] { safeSummary, reviewSummary, advancedSummary };

            return new CleanAnalysisResult
            {
                Categories = summaries,
                TotalCleanableBytes = summaries.Sum(s => s.TotalSizeBytes),
                SafeCleanableBytes = safeSummary.TotalSizeBytes,
                ReviewCleanableBytes = reviewSummary.TotalSizeBytes,
                AdvancedCleanableBytes = advancedSummary.TotalSizeBytes,
                TotalFilesCount = summaries.Sum(s => s.TotalFilesCount),
                AnalyzedAt = DateTimeOffset.UtcNow,
            };
        }, cancellationToken);
    }

    private CleanItem MeasureLocation(
        string id,
        string title,
        string description,
        CleanCategory category,
        IReadOnlyList<string> paths,
        string technicalJustification,
        TimeSpan? filterOlderThan = null,
        string? filePattern = null,
        CancellationToken cancellationToken = default)
    {
        long totalBytes = 0;
        var fileCount = 0;
        var inaccessible = 0;
        var cutoff = filterOlderThan.HasValue ? DateTimeOffset.UtcNow - filterOlderThan.Value : (DateTimeOffset?)null;

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var fi = new FileInfo(path);
                    totalBytes += fi.Length;
                    fileCount++;
                }
                catch
                {
                    inaccessible++;
                }
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            try
            {
                var pattern = filePattern ?? "*";
                var di = new DirectoryInfo(path);
                foreach (var fi in di.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (cutoff.HasValue && fi.LastWriteTimeUtc > cutoff.Value)
                        {
                            continue;
                        }

                        totalBytes += fi.Length;
                        fileCount++;
                    }
                    catch
                    {
                        inaccessible++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Acesso negado ou restrito ao mensurar local {Path}", path);
                inaccessible++;
            }
        }

        return new CleanItem
        {
            Id = id,
            Title = title,
            Description = description,
            Category = category,
            TargetPaths = paths,
            TotalSizeBytes = totalBytes,
            FileCount = fileCount,
            InaccessibleCount = inaccessible,
            TechnicalJustification = technicalJustification,
        };
    }
}
