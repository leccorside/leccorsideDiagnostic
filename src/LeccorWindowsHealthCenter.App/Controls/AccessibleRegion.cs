using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LeccorWindowsHealthCenter.App.Controls;

/// <summary>
/// Bloco de conteúdo que recebe Tab e aparece ao leitor de tela como um grupo com nome
/// (<c>AutomationProperties.Name</c>). Um <see cref="Border"/> não gera elemento de automação: um alerta de "disco com
/// falha" dentro dele nunca seria lido por quem navega pelo teclado.
/// </summary>
public sealed class AccessibleRegion : ContentControl
{
    static AccessibleRegion()
    {
        FocusableProperty.OverrideMetadata(typeof(AccessibleRegion), new FrameworkPropertyMetadata(true));
        IsTabStopProperty.OverrideMetadata(typeof(AccessibleRegion), new FrameworkPropertyMetadata(true));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RegionAutomationPeer(this);

    private sealed class RegionAutomationPeer(AccessibleRegion owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(AccessibleRegion);

        protected override bool IsControlElementCore() => true;

        protected override bool IsContentElementCore() => true;

        protected override bool IsKeyboardFocusableCore() => true;
    }
}
