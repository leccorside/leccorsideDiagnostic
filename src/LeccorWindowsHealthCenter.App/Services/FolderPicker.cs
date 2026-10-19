using System.Windows;
using Microsoft.Win32;

namespace LeccorWindowsHealthCenter.App.Services;

/// <summary>Seletor de pasta do Windows (OpenFolderDialog). Interface para os ViewModels não abrirem janelas direto.</summary>
public interface IFolderPicker
{
    /// <summary>Caminho escolhido, ou null se o usuário cancelou.</summary>
    string? PickFolder(string title);

    /// <summary>Copia texto para a área de transferência; falso quando outro programa a está segurando.</summary>
    bool CopyToClipboard(string text);
}

public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true ? dialog.FolderName : null;
    }

    public bool CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}
