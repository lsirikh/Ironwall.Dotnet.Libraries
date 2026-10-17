using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 서버 모니터 <b>실제 뷰</b> — 미적용 변경으로 레일 전환이 막히면 화면의 레일도 '전체'로 돌아와야 한다.
/// </summary>
public class ServerConsoleRailViewTests
{
    [Fact]
    public void should_put_the_rail_selection_back_to_all_when_the_switch_is_refused_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            console.OnRowsSelected(new List<object> { console.Rows[0] });
            console.BeginEdit();
            console.NameText = "고친 이름";
            AppHost.Pump();
            Assert.True(console.Detail.IsDirty);

            var rail = RailProbe.Rail(view, "Console.Servers.Rail");
            Assert.Equal(ServerTypeCatalog.AllKey, RailProbe.SelectedKey(rail));

            RailProbe.Select(rail, ServerTypeCatalog.NvrKey);
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(ServerTypeCatalog.AllKey, console.SelectedRail?.Key);
            Assert.Equal("고친 이름", console.NameText);
            Assert.True(console.Detail.IsDirty, "막힌 뒤에도 고친 칸은 남아야 한다");
            RailProbe.AssertShows(rail, ServerTypeCatalog.AllKey, refused: ServerTypeCatalog.NvrKey);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_move_the_rail_when_there_are_no_unapplied_changes_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            var rail = RailProbe.Rail(view, "Console.Servers.Rail");

            RailProbe.Select(rail, ServerTypeCatalog.NvrKey);
            Assert.True(RailProbe.ItemSelected(rail, ServerTypeCatalog.NvrKey), "누른 즉시 새 레일이 선택으로 보여야 한다 · " + RailProbe.State(rail));
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(ServerTypeCatalog.NvrKey, console.SelectedRail?.Key);
            RailProbe.AssertShows(rail, ServerTypeCatalog.NvrKey, refused: ServerTypeCatalog.AllKey);
        }
        finally { window.Close(); }
    });

    private static (ServerMonitorView View, System.Windows.Window Window, ServerMonitorViewModel Console) HostConsole()
    {
        var events = new EventAggregator();
        RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);

        var servers = new List<ServerAxisView> { Entry(1, "프록시 1", EnumServerType.PROXY), Entry(2, "NVR 1", EnumServerType.NVR_API) };
        var service = new Mock<IServerConsoleService>(MockBehavior.Loose) { DefaultValue = DefaultValue.Empty };
        service.SetupGet(s => s.Contract).Returns(EnumServerContract.V8_0);
        service.SetupGet(s => s.IsUnitEra).Returns(true);
        service.SetupGet(s => s.IsAxisEra).Returns(true);
        service.Setup(s => s.LoadAsync(It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ServerLoadResult(servers, Array.Empty<ServerUnitOption>(), Array.Empty<ServerCategoryOption>(), false, null));
        service.Setup(s => s.GetAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((int id, CancellationToken _) => servers.FirstOrDefault(s => s.Id == id));
        service.Setup(s => s.MetricHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((IReadOnlyList<ServerMetricDto>?)Array.Empty<ServerMetricDto>());
        service.Setup(s => s.GetDeviceServerMapAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new Dictionary<int, int?>());

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);
        clock.SetupGet(c => c.Now).Returns(Now.ToLocalTime());

        var console = new ServerMonitorViewModel(events, new MockLogService(), service.Object, new DeviceProvider(), clock.Object,
            new Lazy<IServerConsoleDialogs>(() => new Mock<IServerConsoleDialogs>().Object));
        RailProbe.Wait(((IActivate)console).ActivateAsync());

        var view = new ServerMonitorView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }

    private static readonly DateTime Now = new(2026, 9, 20, 0, 5, 0, DateTimeKind.Utc);

    /// <summary>7.0+ 축 응답 모양의 서버 한 대(ServerMonitorViewModelTests.Entry 와 같은 모양).</summary>
    private static ServerAxisView Entry(int id, string name, EnumServerType type) => new()
    {
        Id = id,
        TypeServer = type.ToString(),
        Name = name,
        IsEnable = true,
        UnitId = 4,
        Status = "NORMAL",
        HasStatusKey = true,
        StatusObservedAt = "2026-09-20T00:03:30+00:00",
        HasStatusObservedAtKey = true,
        IpAddress = $"10.0.0.{id}",
        Port = 8000 + id,
        Hostname = $"host-{id}",
        UserName = "admin",
        HasConnectionSection = true,
        HasConfigSection = true,
        CreatedAt = "2026-01-01T00:00:00+00:00",
        UpdatedAt = "2026-09-20T00:03:30+00:00",
    };
}
