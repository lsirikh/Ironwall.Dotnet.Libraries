using System;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.CameraPopup;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : 카메라 팝업 격리 적대 검토 후속(survfix M2 · M3) 헤드리스 시험
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// M3 — 팝업 VM 이 MapViewModel 로 보내는 요청(이벤트)은 팝업 VM 이 스스로 감싸 기록한다: 구독자(지도 VM)가 던져도
/// 팝업 조작 경로 밖으로 새지 않고, 다른 구독자는 그대로 불린다. (GIS 전역 처리기가 "팝업 출처" 로 뭉개지 않는다.)
/// M2 — [다시 시작]은 건강하지 않은 어떤 호스트 상태에서도 보인다.
/// </summary>
public class CameraPopupHandlerFaultTests
{
    private static CameraStreamPopupViewModel NewVm(ICameraPopupHost? host = null)
        => new(7, "정문", new PointLatLng(37, 127), host, null, (_, _) => System.Threading.Tasks.Task.CompletedTask);

    [Fact]
    public void should_contain_map_handler_exception_and_notify_other_subscribers_when_select_requested()
    {
        // Arrange — 첫 구독자(지도 VM 대역)가 던진다
        var vm = NewVm();
        int reached = 0;
        vm.SelectRequested += (_, _) => throw new InvalidOperationException("MapViewModel bug");
        vm.SelectRequested += (_, _) => reached++;

        // Act
        var ex = Record.Exception(() => vm.RaiseSelectRequested());

        // Assert
        Assert.Null(ex);
        Assert.Equal(1, reached);
    }

    [Fact]
    public void should_contain_map_handler_exceptions_when_every_popup_request_is_raised()
    {
        // Arrange — 모든 요청 이벤트에 던지는 구독자를 단다
        var vm = NewVm();
        var boom = new InvalidOperationException("MapViewModel bug");
        vm.CloseRequested += (_, _) => throw boom;
        vm.DragCompleted += (_, _) => throw boom;
        vm.PtzDragRequested += (_, _) => throw boom;
        vm.PtzZoomRequested += (_, _) => throw boom;
        vm.ZoomHoldRequested += (_, _) => throw boom;
        vm.FocusHoldRequested += (_, _) => throw boom;
        vm.FocusStopRequested += (_, _) => throw boom;
        vm.PtzNudgeRequested += (_, _) => throw boom;
        vm.PtzStopRequested += (_, _) => throw boom;
        vm.PresetsReloadRequested += (_, _) => throw boom;
        vm.PresetGotoRequested += (_, _) => throw boom;
        vm.PresetSaveRequested += (_, _) => throw boom;
        vm.PresetDeleteRequested += (_, _) => throw boom;
        vm.PresetHomeSetRequested += (_, _) => throw boom;
        vm.PresetHomeGotoRequested += (_, _) => throw boom;
        vm.OptionsReloadRequested += (_, _) => throw boom;
        vm.IrCutFilterRequested += (_, _) => throw boom;
        vm.AutoFocusRequested += (_, _) => throw boom;
        var preset = new OnvifPresetDisplayModel(7, "1", "Preset_1");

        // Act
        var actions = new List<Action>
        {
            () => vm.CloseCommand.Execute(null),
            () => vm.RaiseDragCompleted(),
            () => vm.RaisePtzDrag(10, 5, 100, 80),
            () => vm.RaisePtzZoom(1),
            () => vm.RaiseZoomHold(-1),
            () => vm.RaiseFocusHold(1),
            () => vm.RaiseFocusStop(),
            () => vm.RaisePadPress(1, 0),
            () => vm.NudgeCommand.Execute("UL"),
            () => vm.RaisePtzStop(),
            () => vm.StopCommand.Execute(null),
            () => vm.SelectTabCommand.Execute("1"),
            () => vm.SelectTabCommand.Execute("2"),
            () => vm.GotoPresetCommand.Execute(preset),
            () => vm.DeletePresetCommand.Execute(preset),
            () => vm.SetHomeCommand.Execute(null),
            () => vm.GotoHomeCommand.Execute(null),
            () => { vm.NewPresetName = "p"; vm.ConfirmSavePresetCommand.Execute(null); },
            () => vm.SetIrCutFilterCommand.Execute("ON"),
            () => vm.SetAutoFocusCommand.Execute("true"),
        };

        // Assert — 어느 경로도 던지지 않는다
        for (int i = 0; i < actions.Count; i++)
            Assert.True(Record.Exception(actions[i]) is null, $"action #{i} leaked the MapViewModel exception");
    }

    [Theory]
    [InlineData(CameraPopupHostState.Restarting, true)]
    [InlineData(CameraPopupHostState.Starting, true)]
    [InlineData(CameraPopupHostState.NotStarted, true)]
    [InlineData(CameraPopupHostState.Suspended, true)]
    [InlineData(CameraPopupHostState.Unavailable, true)]
    [InlineData(CameraPopupHostState.Running, false)]
    [InlineData(CameraPopupHostState.Disposed, false)]
    public void should_offer_restart_in_any_unhealthy_host_state_when_host_registered(CameraPopupHostState state, bool expected)
    {
        // Arrange
        var host = new OverlayTestHost();
        var vm = NewVm(host);
        vm.StartVideo();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        host.ChangeState(state);

        // Assert
        Assert.Equal(expected, vm.CanRestartHost);
        if (expected) Assert.Contains(nameof(CameraStreamPopupViewModel.CanRestartHost), changed);
    }
}
