using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : 등록 센서 정보 오버레이 VM 단위 테스트 (pidsgroup-rightclick TEST-04 — FR-04/05)
   Created By   : GHLee
   Created On   : 2026-08-06
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 순수 VM 검증 — 상태 배지 집계·빈 상태·컨텍스트 교체(재우클릭)·커맨드/이벤트 배선.
/// DeviceProvider 역필터(멤버십)는 MapViewModel private 조립부 소관이라 런타임 검증(TEST-08)에서 커버.
/// </summary>
public class SensorInfoPanelViewModelTests
{
    private static SensorInfoRowModel Row(int id, EnumCompositeEventStatus state) => new()
    {
        DeviceId = id,
        DeviceNumber = id,
        DeviceName = $"FN-{id:D4}",
        State = state,
    };

    [Fact]
    public void should_count_status_badges_by_live_state_when_loaded()
    {
        var vm = new SensorInfoPanelViewModel();

        vm.Load(3, "3구역", new[]
        {
            Row(1, EnumCompositeEventStatus.Normal),
            Row(2, EnumCompositeEventStatus.Normal),
            Row(3, EnumCompositeEventStatus.Detecting),
            Row(4, EnumCompositeEventStatus.FaultedDetecting),   // 탐지 우선 집계
            Row(5, EnumCompositeEventStatus.Faulted),
            Row(6, EnumCompositeEventStatus.Connection),          // 장애 계열 집계
            Row(7, EnumCompositeEventStatus.Blackout),
        });

        Assert.Equal(7, vm.SensorCount);
        Assert.True(vm.HasSensors);
        Assert.Equal(2, vm.NormalCount);
        Assert.Equal(2, vm.DetectCount);
        Assert.Equal(2, vm.FaultCount);
        Assert.Equal(1, vm.BlackoutCount);
        Assert.Equal("3구역", vm.GroupName);
        Assert.Equal(3, vm.GroupId);
    }

    [Fact]
    public void should_show_empty_state_when_no_members()
    {
        var vm = new SensorInfoPanelViewModel();

        vm.Load(4, "4구역", new List<SensorInfoRowModel>());

        Assert.False(vm.HasSensors);
        Assert.Equal(0, vm.SensorCount);
        Assert.Equal(0, vm.NormalCount + vm.DetectCount + vm.FaultCount + vm.BlackoutCount);
    }

    [Fact]
    public void should_replace_context_when_reloaded_with_other_group()
    {
        var vm = new SensorInfoPanelViewModel();
        vm.Load(1, "1구역", new[] { Row(1, EnumCompositeEventStatus.Normal), Row(2, EnumCompositeEventStatus.Faulted), Row(3, EnumCompositeEventStatus.Normal) });

        vm.Load(2, "2구역", new[] { Row(9, EnumCompositeEventStatus.Detecting) });

        // 누적 금지 — 재우클릭 = 완전 교체(FR-03 생명주기 계약)
        Assert.Equal(2, vm.GroupId);
        Assert.Equal("2구역", vm.GroupName);
        Assert.Equal(1, vm.SensorCount);
        Assert.Equal(1, vm.DetectCount);
        Assert.Equal(0, vm.NormalCount);
        Assert.Equal(0, vm.FaultCount);
    }

    [Theory]
    [InlineData(EnumCompositeEventStatus.Normal, "정상")]
    [InlineData(EnumCompositeEventStatus.Detecting, "탐지")]
    [InlineData(EnumCompositeEventStatus.FaultedDetecting, "탐지+장애")]
    [InlineData(EnumCompositeEventStatus.Faulted, "장애")]
    [InlineData(EnumCompositeEventStatus.Connection, "연결")]
    [InlineData(EnumCompositeEventStatus.Blackout, "통신두절")]
    public void should_map_state_text_when_composite_status(EnumCompositeEventStatus state, string expected)
    {
        Assert.Equal(expected, Row(1, state).StateText);
    }

    [Fact]
    public void should_clear_selection_when_context_reloaded()
    {
        var vm = new SensorInfoPanelViewModel();
        vm.Load(1, "1구역", new[] { Row(1, EnumCompositeEventStatus.Normal) });
        vm.SelectedRow = vm.Rows[0];

        vm.Load(2, "2구역", new[] { Row(2, EnumCompositeEventStatus.Normal) });

        // 재우클릭(컨텍스트 교체) 시 이전 그룹의 선택 잔존 금지
        Assert.Null(vm.SelectedRow);
    }

    [Fact]
    public void should_raise_close_requested_when_close_command_executed()
    {
        var vm = new SensorInfoPanelViewModel();
        var raised = false;
        vm.CloseRequested += () => raised = true;

        vm.CloseCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void should_pass_row_when_sensor_history_command_executed()
    {
        var vm = new SensorInfoPanelViewModel();
        SensorInfoRowModel? received = null;
        vm.SensorHistoryRequested += r => received = r;
        var row = Row(12, EnumCompositeEventStatus.Normal);

        vm.SensorHistoryCommand.Execute(row);
        vm.SensorHistoryCommand.Execute("not-a-row");   // 타입 가드 — 무시돼야 함

        Assert.NotNull(received);
        Assert.Equal(12, received!.DeviceId);
    }

    [Fact]
    public void should_pass_row_when_locate_command_executed()
    {
        var vm = new SensorInfoPanelViewModel();
        SensorInfoRowModel? received = null;
        vm.LocateRequested += r => received = r;

        vm.LocateCommand.Execute(Row(7, EnumCompositeEventStatus.Normal));

        Assert.Equal(7, received!.DeviceId);
    }
}
