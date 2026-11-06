using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeccorWindowsHealthCenter.App.Services;
using LeccorWindowsHealthCenter.Application.Storage;
using LeccorWindowsHealthCenter.Shared;
using Microsoft.Extensions.Logging;

namespace LeccorWindowsHealthCenter.App.ViewModels;

public sealed class CleanItemRowViewModel
{
    public CleanItemRowViewModel(CleanItem item)
    {
        Item = item;
    }

    public CleanItem Item { get; }

    public string Title => Item.Title;

    public string Description => Item.Description;

    public CleanCategory Category => Item.Category;

    public string CategoryText => Item.Category switch
    {
        CleanCategory.Safe => "Seguro",
        CleanCategory.Review => "Revisar",
        CleanCategory.Advanced => "Avançado",
        _ => "Outro",
    };

    public string SizeText => Fmt.Bytes(Item.TotalSizeBytes);

    public string FileCountText => Item.FileCount > 0 ? $"{Item.FileCount:N0} arquivos" : "Nenhum arquivo";

    public string TechnicalJustification => Item.TechnicalJustification;

    public string PathsText => string.Join(", ", Item.TargetPaths);

    public bool HasData => Item.TotalSizeBytes > 0 || Item.FileCount > 0;
}

public sealed partial class SafeCleanViewModel : ObservableObject, IPageLifecycle
{
    private readonly ISafeCleanProvider _safeCleanProvider;
    private readonly ILogger<SafeCleanViewModel> _logger;

    public SafeCleanViewModel(ISafeCleanProvider safeCleanProvider, ILogger<SafeCleanViewModel> logger)
    {
        _safeCleanProvider = safeCleanProvider;
        _logger = logger;
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _totalCleanableText = "0 B";

    [ObservableProperty]
    private string _safeCleanableText = "0 B";

    [ObservableProperty]
    private string _reviewCleanableText = "0 B";

    [ObservableProperty]
    private string _advancedCleanableText = "0 B";

    [ObservableProperty]
    private int _totalFilesCount;

    [ObservableProperty]
    private string _selectedCategoryTab = "Seguro";

    public ObservableCollection<CleanItemRowViewModel> SafeItems { get; } = [];

    public ObservableCollection<CleanItemRowViewModel> ReviewItems { get; } = [];

    public ObservableCollection<CleanItemRowViewModel> AdvancedItems { get; } = [];

    public ObservableCollection<CleanItemRowViewModel> CurrentCategoryItems { get; } = [];

    public IReadOnlyList<string> CategoryTabs { get; } = ["Seguro", "Revisar", "Avançado"];

    public void Activate()
    {
        if (SafeItems.Count == 0 && ReviewItems.Count == 0 && AdvancedItems.Count == 0)
        {
            _ = AnalyzeCommand.ExecuteAsync(null);
        }
    }

    public void Deactivate()
    {
    }

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        try
        {
            IsLoading = true;

            var result = await _safeCleanProvider.AnalyzeCleanableLocationsAsync(CancellationToken.None);

            TotalCleanableText = Fmt.Bytes(result.TotalCleanableBytes);
            SafeCleanableText = Fmt.Bytes(result.SafeCleanableBytes);
            ReviewCleanableText = Fmt.Bytes(result.ReviewCleanableBytes);
            AdvancedCleanableText = Fmt.Bytes(result.AdvancedCleanableBytes);
            TotalFilesCount = result.TotalFilesCount;

            SafeItems.Clear();
            var safeSum = result.Categories.FirstOrDefault(c => c.Category == CleanCategory.Safe);
            if (safeSum != null)
            {
                foreach (var it in safeSum.Items)
                {
                    SafeItems.Add(new CleanItemRowViewModel(it));
                }
            }

            ReviewItems.Clear();
            var revSum = result.Categories.FirstOrDefault(c => c.Category == CleanCategory.Review);
            if (revSum != null)
            {
                foreach (var it in revSum.Items)
                {
                    ReviewItems.Add(new CleanItemRowViewModel(it));
                }
            }

            AdvancedItems.Clear();
            var advSum = result.Categories.FirstOrDefault(c => c.Category == CleanCategory.Advanced);
            if (advSum != null)
            {
                foreach (var it in advSum.Items)
                {
                    AdvancedItems.Add(new CleanItemRowViewModel(it));
                }
            }

            UpdateCurrentCategoryView();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar locais para limpeza segura");
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedCategoryTabChanged(string value) => UpdateCurrentCategoryView();

    private void UpdateCurrentCategoryView()
    {
        CurrentCategoryItems.Clear();
        var source = SelectedCategoryTab switch
        {
            "Revisar" => ReviewItems,
            "Avançado" => AdvancedItems,
            _ => SafeItems,
        };

        foreach (var item in source)
        {
            CurrentCategoryItems.Add(item);
        }
    }
}
