using System.ComponentModel;
using System.Windows.Data;
using LeccorWindowsHealthCenter.App.ViewModels;
using LeccorWindowsHealthCenter.Application.Startup;
using LeccorWindowsHealthCenter.Application.SystemInfo;
using Xunit;

namespace LeccorWindowsHealthCenter.SystemTests.ViewModels;

public sealed class PacoteCUiPerformanceTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 12)]
    [InlineData(3, 36)]
    [InlineData(6, 72)]
    [InlineData(8, 84)]
    [InlineData(10, 96)]
    [InlineData(16, 132)]
    [InlineData(20, 132)] // Limitado a 132 px no 16º nível
    public void ProcessItem_Indent_CalculatesProgressiveIndentUpTo16Levels(int depth, double expectedLeftThickness)
    {
        var details = new ProcessDetails
        {
            Pid = 1234,
            Name = "test.exe",
            ParentPid = 0,
            StartTime = DateTimeOffset.UtcNow,
        };

        var item = new ProcessItem(details)
        {
            Depth = depth,
        };

        Assert.Equal(expectedLeftThickness, item.Indent.Left);
        Assert.Equal(0, item.Indent.Top);
        Assert.Equal(0, item.Indent.Right);
        Assert.Equal(0, item.Indent.Bottom);
    }

    [Theory]
    [InlineData(StartupSourceKind.RegistryRun, "Aplicativos", 0)]
    [InlineData(StartupSourceKind.RegistryRunOnce, "Aplicativos", 0)]
    [InlineData(StartupSourceKind.StartupFolder, "Aplicativos", 0)]
    [InlineData(StartupSourceKind.ScheduledTask, "Tarefas agendadas", 1)]
    [InlineData(StartupSourceKind.Service, "Serviços automáticos", 2)]
    public void StartupRow_GroupName_And_GroupOrder_ClassifiesCorrectly(StartupSourceKind kind, string expectedGroup, int expectedOrder)
    {
        var entry = new StartupEntry
        {
            Id = "test-item",
            Name = "TestItem",
            Source = kind,
            Location = "Run",
            Command = @"C:\app.exe --start",
            ExecutablePath = @"C:\app.exe",
            ExecutableExists = true,
            Enabled = true,
        };

        var row = new StartupRow(entry, StartupImpactLevel.Low);

        Assert.Equal(expectedGroup, row.GroupName);
        Assert.Equal(expectedOrder, row.GroupOrder);
    }

    [Fact]
    public void StartupRows_CanBeGroupedAndSortedInCollectionView()
    {
        var entries = new List<StartupRow>
        {
            new(new StartupEntry { Id = "s1", Name = "Service1", Source = StartupSourceKind.Service, Location = "Services", Command = "srv.exe", ExecutablePath = @"C:\srv.exe", ExecutableExists = true, Enabled = true }, StartupImpactLevel.Medium),
            new(new StartupEntry { Id = "a1", Name = "App1", Source = StartupSourceKind.RegistryRun, Location = "Run", Command = "app.exe", ExecutablePath = @"C:\app.exe", ExecutableExists = true, Enabled = true }, StartupImpactLevel.High),
            new(new StartupEntry { Id = "t1", Name = "Task1", Source = StartupSourceKind.ScheduledTask, Location = "Tasks", Command = "task.exe", ExecutablePath = @"C:\task.exe", ExecutableExists = true, Enabled = true }, StartupImpactLevel.Low),
        };

        var view = CollectionViewSource.GetDefaultView(entries);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(StartupRow.GroupName)));

        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(nameof(StartupRow.GroupOrder), ListSortDirection.Ascending));
        view.SortDescriptions.Add(new SortDescription(nameof(StartupRow.Impact), ListSortDirection.Descending));

        Assert.NotNull(view.Groups);
        Assert.Equal(3, view.Groups.Count);

        // Primeiro grupo deve ser "Aplicativos"
        var firstGroup = (CollectionViewGroup)view.Groups[0];
        Assert.Equal("Aplicativos", firstGroup.Name);

        // Segundo grupo deve ser "Tarefas agendadas"
        var secondGroup = (CollectionViewGroup)view.Groups[1];
        Assert.Equal("Tarefas agendadas", secondGroup.Name);

        // Terceiro grupo deve ser "Serviços automáticos"
        var thirdGroup = (CollectionViewGroup)view.Groups[2];
        Assert.Equal("Serviços automáticos", thirdGroup.Name);
    }
}
