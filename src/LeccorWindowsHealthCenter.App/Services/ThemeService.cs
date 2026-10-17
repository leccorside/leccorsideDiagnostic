using System.Windows;
using LeccorWindowsHealthCenter.Application.Settings;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.App.Services;

/// <summary>Alterna entre os dicionários de tema claro/escuro (seção 43) e segue o tema do Windows.</summary>
public sealed class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public AppTheme Current { get; private set; } = AppTheme.System;

    public bool IsDark { get; private set; }

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme theme)
    {
        Current = theme;
        IsDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => SystemUsesDarkTheme(),
        };

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var uri = new Uri(IsDark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var existing = dictionaries.FirstOrDefault(d => d.Source?.OriginalString is "Themes/Dark.xaml" or "Themes/Light.xaml");
        var replacement = new ResourceDictionary { Source = uri };
        if (existing is null)
        {
            dictionaries.Insert(0, replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool SystemUsesDarkTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }
}
