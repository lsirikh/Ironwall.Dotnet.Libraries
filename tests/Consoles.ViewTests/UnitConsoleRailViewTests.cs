using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 부대 콘솔 <b>실제 뷰</b> — 미적용 변경으로 레일 전환이 막히면 화면의 레일도 '편제 트리'로 돌아와야 한다.
/// </summary>
public class UnitConsoleRailViewTests
{
    [Fact]
    public void should_put_the_rail_selection_back_to_the_tree_when_the_switch_is_refused_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            RailProbe.Wait(console.SelectRowAsync(console.Rows.First(r => r.Id == 6)));
            console.Form.Name = "6중대 (개편)";
            AppHost.Pump();
            Assert.True(console.Detail.IsDirty);

            var rail = RailProbe.Rail(view, "Console.Units.Rail");
            Assert.Equal(UnitConsoleViewModel.RAIL_TREE, RailProbe.SelectedKey(rail));

            // v1.7 — 「미배치 장비」 칸은 없다(Q-1 ⓐ). 남은 다른 칸(부대 관계도)으로 같은 관문을 시험한다.
            RailProbe.Select(rail, UnitConsoleViewModel.RAIL_ADJACENCY);
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(UnitConsoleViewModel.RAIL_TREE, console.SelectedRail.Key);
            Assert.Equal("6중대 (개편)", console.Form.Name);
            Assert.True(console.Detail.IsDirty, "막힌 뒤에도 고친 칸은 남아야 한다");
            RailProbe.AssertShows(rail, UnitConsoleViewModel.RAIL_TREE, refused: UnitConsoleViewModel.RAIL_ADJACENCY);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_move_the_rail_when_there_are_no_unapplied_changes_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            var rail = RailProbe.Rail(view, "Console.Units.Rail");

            RailProbe.Select(rail, UnitConsoleViewModel.RAIL_ADJACENCY);
            Assert.True(RailProbe.ItemSelected(rail, UnitConsoleViewModel.RAIL_ADJACENCY), "누른 즉시 새 레일이 선택으로 보여야 한다 · " + RailProbe.State(rail));
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(UnitConsoleViewModel.RAIL_ADJACENCY, console.SelectedRail.Key);
            RailProbe.AssertShows(rail, UnitConsoleViewModel.RAIL_ADJACENCY, refused: UnitConsoleViewModel.RAIL_TREE);
        }
        finally { window.Close(); }
    });

    private static (UnitConsoleView View, System.Windows.Window Window, UnitConsoleViewModel Console) HostConsole()
    {
        var events = new EventAggregator();
        RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);

        var graph = Sample();
        var units = new Mock<IUnitGraphApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        units.SetupGet(u => u.IsAvailable).Returns(true);
        units.Setup(u => u.GetGraphAsync(It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => ApiResponse<UnitGraphDto>.CreateSuccess(graph));
        units.Setup(u => u.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((int id, CancellationToken _) =>
             {
                 var node = graph.Nodes.First(n => n.Id == id);
                 return ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
                 {
                     Id = id, Code = node.Code, Name = node.Name, EchelonRaw = node.EchelonRaw, ParentId = node.ParentId,
                     IsEnable = true, AdjacentUnitIds = new List<int>(),
                 });
             });
        var devices = new Mock<IUnitDeviceApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        devices.SetupGet(d => d.IsAvailable).Returns(true);
        devices.Setup(d => d.LoadAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new UnitDeviceLoadResult(Array.Empty<UnitDeviceItem>(), Array.Empty<string>()));

        var console = new UnitConsoleViewModel(units.Object, devices.Object, log: null, myUnitCode: () => "c06",
                                               canEdit: () => true, canDelete: () => true, events: events);
        RailProbe.Wait(((IActivate)console).ActivateAsync());

        var view = new UnitConsoleView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }

    private static UnitListDto Node(int id, string code, string echelon, int? parentId = null)
        => new() { Id = id, Code = code, Name = code, EchelonRaw = echelon, ParentId = parentId };

    /// <summary>사단1 → 연대2 → 대대3 → 중대5 · 중대6 (UnitConsoleViewModelTests 와 같은 편제의 줄인 모양).</summary>
    private static UnitGraphDto Sample() => new()
    {
        Nodes = new List<UnitListDto>
        {
            Node(1, "d01", "Division"),
            Node(2, "r01", "Regiment", 1),
            Node(3, "b01", "Battalion", 2),
            Node(5, "c05", "Company", 3),
            Node(6, "c06", "Company", 3),
        },
        Edges = new UnitGraphEdgesDto
        {
            Hierarchy = new List<List<int>> { new() { 1, 2 }, new() { 2, 3 }, new() { 3, 5 }, new() { 3, 6 } },
            Adjacency = new List<List<int>>(),
        },
    };
}
