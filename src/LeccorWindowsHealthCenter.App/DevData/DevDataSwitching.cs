#if DEBUG
using LeccorWindowsHealthCenter.Application.SystemInfo;
using LeccorWindowsHealthCenter.Infrastructure.SystemInfo;
using LeccorWindowsHealthCenter.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeccorWindowsHealthCenter.App.DevData;

public enum DevDataMode
{
    Real,
    Worst,
    Empty,
    One,
    Many,
}

/// <summary>Modo atual dos dados de desenvolvimento. Inicial vem de <c>--DevData:Mode=worst</c> (persiste via argumento).</summary>
public sealed class DevDataSwitch
{
    public DevDataSwitch(IConfiguration configuration)
    {
        Mode = Enum.TryParse<DevDataMode>(configuration["DevData:Mode"], ignoreCase: true, out var mode) ? mode : DevDataMode.Real;
    }

    public DevDataMode Mode { get; private set; }

    public event EventHandler? Changed;

    public void Set(DevDataMode mode)
    {
        Mode = mode;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Decoradores na fronteira de dados (regra 2 do break-ui: muda-se o dado, nunca o componente).
/// Em <see cref="DevDataMode.Real"/> repassam para as fontes reais.
/// </summary>
internal sealed class SwitchingCounterFactory(PdhPerformanceCounterSessionFactory real, DevDataSwitch sw) : IPerformanceCounterSessionFactory
{
    public IPerformanceCounterSession Create() => new Session(real.Create(), sw);

    private sealed class Session(IPerformanceCounterSession inner, DevDataSwitch sw) : IPerformanceCounterSession
    {
        public PerformanceReading Read() => sw.Mode == DevDataMode.Real ? inner.Read() : DevDataFixtures.Reading(sw.Mode);

        public void Dispose() => inner.Dispose();
    }
}

internal sealed class SwitchingProcesses(ProcessSnapshotProvider real, DevDataSwitch sw) : IProcessSnapshotProvider
{
    public ProcessSnapshot Capture(bool includePaths = false) =>
        sw.Mode == DevDataMode.Real ? real.Capture(includePaths) : DevDataFixtures.Processes(sw.Mode, Environment.ProcessorCount);

    public string? TryGetExecutablePath(int pid) => sw.Mode == DevDataMode.Real ? real.TryGetExecutablePath(pid) : null;
}

internal sealed class SwitchingHistory(MetricsHistoryService real, DevDataSwitch sw) : IMetricsHistory
{
    public TimeSpan Retention => real.Retention;

    public IReadOnlyList<SystemHistorySample> SystemSamples() => sw.Mode == DevDataMode.Real ? real.SystemSamples() : DevDataFixtures.History(sw.Mode);

    public IReadOnlyList<ProcessMemorySample> ProcessMemorySamples() => sw.Mode == DevDataMode.Real ? real.ProcessMemorySamples() : [];
}

internal sealed class SwitchingInventory(WmiHardwareInventoryProvider real, DevDataSwitch sw) : IHardwareInventoryProvider
{
    public Task<HardwareInventory> GetAsync(CancellationToken cancellationToken, bool forceRefresh = false) =>
        sw.Mode == DevDataMode.Real ? real.GetAsync(cancellationToken, forceRefresh) : Task.FromResult(DevDataFixtures.Inventory(sw.Mode));
}

public static class DevDataRegistration
{
    /// <summary>Registra os decoradores por cima das fontes reais (as últimas registrações vencem no DI).</summary>
    public static void AddDevDataSwitching(this IServiceCollection services)
    {
        services.AddSingleton<DevDataSwitch>();
        services.AddSingleton<PdhPerformanceCounterSessionFactory>();
        services.AddSingleton<ProcessSnapshotProvider>();
        services.AddSingleton<WmiHardwareInventoryProvider>();
        services.AddSingleton<IPerformanceCounterSessionFactory>(sp => new SwitchingCounterFactory(sp.GetRequiredService<PdhPerformanceCounterSessionFactory>(), sp.GetRequiredService<DevDataSwitch>()));
        services.AddSingleton<IProcessSnapshotProvider>(sp => new SwitchingProcesses(sp.GetRequiredService<ProcessSnapshotProvider>(), sp.GetRequiredService<DevDataSwitch>()));
        services.AddSingleton<IMetricsHistory>(sp => new SwitchingHistory(sp.GetRequiredService<MetricsHistoryService>(), sp.GetRequiredService<DevDataSwitch>()));
        services.AddSingleton<IHardwareInventoryProvider>(sp => new SwitchingInventory(sp.GetRequiredService<WmiHardwareInventoryProvider>(), sp.GetRequiredService<DevDataSwitch>()));
    }
}
#endif
