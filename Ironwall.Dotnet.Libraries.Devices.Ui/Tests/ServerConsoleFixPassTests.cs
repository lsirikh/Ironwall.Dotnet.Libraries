using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 콘솔 — GIS 실창 육안 검토(2026-09-27 2회차) 결함 회귀
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 실창 캡처 049~053 에서 나온 서버 콘솔 결함(#1 · #3 · #7 · #8 · #10 · #11)이 다시 생기지 않는지 본다.
/// </summary>
public class ServerConsoleFixPassTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 0, 5, 0, DateTimeKind.Utc);

    private static ServerAxisView Entry(int id, string name, EnumServerType type)
        => new()
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
            HasConnectionSection = true,
            HasConfigSection = true,
            CreatedAt = "2026-01-01T00:00:00+00:00",
            UpdatedAt = "2026-09-20T00:03:30+00:00",
        };

    private static (ServerMonitorViewModel Vm, FakeServerConsoleService Service) Build()
    {
        var service = new FakeServerConsoleService();
        var vm = new ServerMonitorViewModel(new EventAggregator(), new MockLogService(), service, new DeviceProvider(),
            new FixedClock(Now), new Lazy<IServerConsoleDialogs>(() => new FakeServerDialogs()));
        return (vm, service);
    }

    private static Task ActivateAsync(ServerMonitorViewModel vm) => ((IActivate)vm).ActivateAsync();

    #region - #8 [추가] 가 늘 꺼진 회색 -
    [Fact]
    public async Task should_announce_add_as_enabled_when_the_list_has_finished_loading()
    {
        // Arrange — 적재 도중(_isBusy)에 알린 CanAdd 는 거짓이다. 바쁨이 풀린 뒤 다시 알려야 단추가 켜진다.
        var (vm, service) = Build();
        service.Categories.Add(new ServerCategoryOption(1, "방송", EnumServerType.SPEAKER_API, "SPEAKER_API"));
        var announced = new List<bool>();
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ServerMonitorViewModel.CanAdd)) announced.Add(vm.CanAdd); };

        // Act
        await ActivateAsync(vm);

        // Assert — 마지막으로 알린 값이 켜짐이어야 바인딩된 단추가 채운 청록(Primary)으로 선다.
        Assert.NotEmpty(announced);
        Assert.True(announced[^1]);
    }
    #endregion

    #region - #7 적용 뒤 선택 · 문구 -
    [Fact]
    public async Task should_keep_the_applied_row_selected_with_its_detail_when_settings_are_applied()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(1, "프록시-1", EnumServerType.PROXY));
        service.Servers.Add(Entry(2, "프록시-2", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[1] });
        vm.BeginEdit();
        vm.PortText = "9000";

        // Act
        await vm.ApplyAsync(CancellationToken.None);

        // Assert — 그리드만 되살리는 것이 아니라 뷰모델도 그 행을 쥔다(예전: 행은 강조, 상세는 "선택한 항목 없음").
        Assert.Equal(2, vm.SelectedRow?.Id);
        Assert.True(vm.HasDetail);
        Assert.Equal("프록시-2", vm.Detail.Title);
    }

    [Fact]
    public async Task should_say_the_saved_line_only_once_when_settings_are_applied()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(1, "프록시-1", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        vm.BeginEdit();
        vm.PortText = "9000";

        // Act
        await vm.ApplyAsync(CancellationToken.None);

        // Assert — 상세 바닥 막대에만 뜬다. 상태 띠에도 쓰면 같은 말이 두 번 보였다.
        Assert.Contains("저장했습니다", vm.Detail.FooterText);
        Assert.Equal(string.Empty, vm.StatusText);
    }
    #endregion

    #region - #10 · #11 상세 머리 · 빈 상태 · 모드 콤보 -
    [Fact]
    public async Task should_show_the_server_type_instead_of_the_internal_id_when_one_server_is_selected()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(44, "LRT-UI-SRV", EnumServerType.PROXY));
        await ActivateAsync(vm);

        // Act
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });

        // Assert
        Assert.Equal($"서버 · {vm.Rows[0].TypeText}", vm.Detail.Kind);
        Assert.DoesNotContain("44", vm.Detail.Kind);
    }

    [Fact]
    public async Task should_offer_a_guide_line_when_nothing_is_selected()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));

        // Act
        await ActivateAsync(vm);

        // Assert
        Assert.False(vm.HasDetail);
        Assert.Contains("고르면", vm.DetailEmptyHint);
        Assert.Contains("[추가]", vm.DetailEmptyHint);
    }

    [Fact]
    public async Task should_hint_the_mode_combo_when_editing_starts()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        await ActivateAsync(vm);
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });
        var readHint = vm.ModeHint;

        // Act
        vm.BeginEdit();

        // Assert — 빈 콤보가 "고장인지 미설정인지" 말한다.
        Assert.False(string.IsNullOrWhiteSpace(readHint));
        Assert.NotEqual(readHint, vm.ModeHint);
    }
    #endregion

    #region - #1 지표 띠가 목록을 굶긴다 -
    [Fact]
    public async Task should_collapse_the_metric_band_to_one_line_when_the_server_sent_no_metric()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        service.LatestMetric = null;
        await ActivateAsync(vm);

        // Act
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });

        // Assert — "보고 없음" 네 칸 대신 한 줄.
        Assert.True(vm.IsMetricBandVisible);
        Assert.False(vm.HasMetricValues);
        Assert.False(string.IsNullOrWhiteSpace(vm.MetricEmptyText));
    }

    [Fact]
    public async Task should_draw_metric_cells_when_the_server_sent_any_metric()
    {
        // Arrange
        var (vm, service) = Build();
        service.Servers.Add(Entry(1, "a", EnumServerType.PROXY));
        service.LatestMetric = new ServerMetricDto { CpuUsage = 12 };
        await ActivateAsync(vm);

        // Act
        vm.OnRowsSelected(new List<object> { vm.Rows[0] });

        // Assert
        Assert.True(vm.HasMetricValues);
    }

    [Fact]
    public void should_reserve_the_column_header_and_five_rows_when_the_list_height_floor_is_read()
    {
        // Act
        var floor = ServerMonitorLayout.MinListHeight;

        // Assert
        Assert.True(floor >= ServerMonitorLayout.ColumnHeaderHeight + (5 * ServerMonitorLayout.RowHeight));
    }

    [Theory]
    [InlineData(732, 4)]   // 1280 도킹 — 목록 756 − 띠 여백 24. 예전 문턱(760)에서는 2칸 두 줄(약 170px)이었다.
    [InlineData(640, 4)]
    [InlineData(639, 2)]
    public void should_keep_four_metric_cells_in_one_row_when_the_console_is_docked(double bandWidth, int expected)
    {
        // Act
        var columns = MetricBandColumnsConverter.ColumnsFor(bandWidth);

        // Assert
        Assert.Equal(expected, columns);
    }
    #endregion

    #region - #3 주소 열 -
    [Fact]
    public void should_give_the_address_column_room_for_a_full_ipv4_address_with_port_when_columns_are_declared()
    {
        // Arrange — "192.168.100.100:8100"(20자) · Consolas 12.5 ≈ 6.9/자 + 칸 여백 24.
        var address = ServerColumnCatalog.All.Single(c => c.Key == "address");

        // Assert
        Assert.True(address.Width >= 164, $"주소 열 {address.Width}");
    }

    [Theory]
    [InlineData("type")]
    [InlineData("last_change")]
    [InlineData("unit")]
    public void should_fit_visible_columns_with_a_scrollbar_when_the_list_is_exactly_at_a_collapse_threshold(string key)
    {
        // Arrange — 문턱 폭 그 자체에서 남는 열 + 세로 스크롤 막대(10)가 들어가야 한다.
        var listWidth = ServerMonitorView.CollapseBelowFor(key);
        const double handle = 26, starFloor = 140, scrollBar = 10;

        // Act
        var sum = ServerColumnCatalog.For(isUnitEra: true)
            .Where(c => c.IsDefault && !ConsoleColumns.ShouldCollapse(ServerMonitorView.CollapseBelowFor(c.Key), listWidth))
            .Sum(c => c.Width <= 0 ? starFloor : c.Width) + handle + scrollBar;

        // Assert
        Assert.True(sum <= listWidth, $"{key}: 목록 {listWidth} 에 {sum} 필요");
    }
    #endregion
}
