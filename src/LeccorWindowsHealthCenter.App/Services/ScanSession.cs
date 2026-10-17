using LeccorWindowsHealthCenter.Domain;

namespace LeccorWindowsHealthCenter.App.Services;

/// <summary>Último resultado de varredura compartilhado entre telas (dashboard, varredura, relatórios).</summary>
public sealed class ScanSession
{
    public ScanReport? LastReport { get; private set; }

    public event EventHandler? ReportChanged;

    public void Publish(ScanReport report)
    {
        LastReport = report;
        ReportChanged?.Invoke(this, EventArgs.Empty);
    }
}
