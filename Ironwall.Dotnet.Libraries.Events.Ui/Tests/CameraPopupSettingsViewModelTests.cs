using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Settings.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 설정 콘솔 "카메라 팝업 연동" 블록 뷰모델 — 헤드리스 (camera-popup-modes T-03)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 모드별 보이는 칸(FR-02) · 격자 스냅(FR-10) · 초안/저장/되돌리기 · 첫 창 끌기(데드존 · Esc) · 키보드 대체 · 모니터 폴백.
/// 설정 파일은 건드리지 않는다 — 가짜 창구가 받는다.
/// </summary>
public class CameraPopupSettingsViewModelTests
{
    private sealed class FakePort : ICameraPopupSettingsPort
    {
        public CameraPopupSettings Stored = new CameraPopupSettings().Normalize();
        public int SaveCount;
        public bool Throw;
        public bool Ignore;

        public CameraPopupSettings LoadCameraPopup() => Stored;

        public void SaveCameraPopup(CameraPopupSettings settings)
        {
            SaveCount++;
            if (Throw) throw new System.IO.IOException("disk full");
            if (!Ignore) Stored = settings;
        }

        public string CameraPopupClientId => "gis-7f3a9c2e41b6";
    }

    private sealed class FakeMonitors : IDisplayMonitorProvider
    {
        public List<DisplayMonitorInfo> List = new()
        {
            new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true),
            new(@"\\.\DISPLAY2", new PixelRect(1920, 0, 2560, 1440), new PixelRect(1920, 0, 2560, 1400), false),
        };

        public IReadOnlyList<DisplayMonitorInfo> GetMonitors() => List;
    }

    private static (CameraPopupSettingsViewModel Vm, FakePort Port, FakeMonitors Monitors) Make(CameraPopupSettings? stored = null)
    {
        var port = new FakePort();
        if (stored is not null) port.Stored = stored.Normalize();
        var monitors = new FakeMonitors();
        return (new CameraPopupSettingsViewModel(port, monitors), port, monitors);
    }

    // ══════ 모드별 보이는 칸(FR-02) ══════

    [Fact]
    public void should_show_self_groups_only_when_mode_is_self()
    {
        var (vm, _, _) = Make();
        Assert.True(vm.ShowSelfGroups);
        Assert.False(vm.ShowBrokerGroup);
        Assert.False(vm.ShowNoneNote);
    }

    [Fact]
    public void should_hide_provider_and_event_groups_when_broker_chosen()
    {
        var (vm, _, _) = Make();
        vm.ModeChoices.Single(c => (CameraPopupMode)c.Value == CameraPopupMode.Broker).IsSelected = true;

        Assert.Equal(CameraPopupMode.Broker, vm.Mode);
        Assert.False(vm.ShowSelfGroups);
        Assert.True(vm.ShowBrokerGroup);
        Assert.False(vm.ModeChoices.Single(c => (CameraPopupMode)c.Value == CameraPopupMode.Self).IsSelected);
    }

    [Fact]
    public void should_hide_all_popup_groups_when_none_chosen()
    {
        var (vm, _, _) = Make();
        vm.Mode = CameraPopupMode.None;
        Assert.False(vm.ShowSelfGroups);
        Assert.False(vm.ShowBrokerGroup);
        Assert.True(vm.ShowNoneNote);
        // 방식별 설명은 섹션 "?" 로 옮겼다(help-callout H-3) — 사용 안 함의 뜻이 말풍선에 있다
        Assert.Contains("카메라 상세",
            Ironwall.Dotnet.Libraries.Events.Ui.Help.EventsHelp.Entries.Single(e => e.Key == "Settings.CameraPopup.Mode").ToPlainText());
    }

    [Fact]
    public void should_not_select_external_vms_when_placeholder_picked()
    {
        var (vm, _, _) = Make();
        var vms = vm.ProviderChoices.Single(c => (VideoProviderKind)c.Value == VideoProviderKind.ExternalVms);
        Assert.False(vms.IsEnabled);

        vms.IsSelected = true;

        Assert.Equal(VideoProviderKind.Onvif, vm.Provider);
        Assert.False(vms.IsSelected);
        Assert.False(vm.IsVmsEditable);
        Assert.Equal(0, vm.DirtyCount);
    }

    [Fact]
    public void should_expose_client_id_read_only_when_port_supplies_it()
        => Assert.Equal("gis-7f3a9c2e41b6", Make().Vm.ClientId);

    // ══════ 창당 카메라 · 격자(FR-10) ══════

    [Fact]
    public void should_list_only_layouts_for_count_when_count_changes()
    {
        var (vm, _, _) = Make();
        vm.CameraCountChoices.Single(c => (int)c.Value == 4).IsSelected = true;

        Assert.Equal(4, vm.CamerasPerWindow);
        Assert.Equal(new[] { "2x2", "4x1", "1x4" }, vm.LayoutChoices.Select(c => ((CameraPopupGridLayout)c.Value).Key));
        Assert.Equal(new CameraPopupGridLayout(2, 2), vm.GridLayout);   // 3×2 는 4대에서 못 고른다 → 스냅
        Assert.True(vm.LayoutChoices[0].IsSelected);
    }

    [Fact]
    public void should_keep_layout_when_new_count_still_allows_it()
    {
        var (vm, _, _) = Make();
        vm.LayoutChoices.Single(c => ((CameraPopupGridLayout)c.Value).Key == "2x3").IsSelected = true;
        vm.CamerasPerWindow = 5;
        Assert.Equal(new CameraPopupGridLayout(2, 3), vm.GridLayout);
    }

    [Fact]
    public void should_mark_empty_cell_when_five_cameras_fill_three_by_two()
    {
        var (vm, _, _) = Make();
        vm.CamerasPerWindow = 5;
        var chip = vm.LayoutChoices.Single(c => ((CameraPopupGridLayout)c.Value).Key == "3x2");
        Assert.Equal(new[] { true, true, true, true, true, false }, chip.Cells);
    }

    [Fact]
    public void should_ignore_layout_when_not_allowed_for_count()
    {
        var (vm, _, _) = Make();
        vm.CamerasPerWindow = 2;
        vm.GridLayout = new CameraPopupGridLayout(3, 2);
        Assert.Equal(new CameraPopupGridLayout(2, 1), vm.GridLayout);
    }

    // ══════ 초안 · 저장 · 되돌리기 ══════

    [Fact]
    public void should_count_changed_fields_when_draft_edited()
    {
        var (vm, _, _) = Make();
        Assert.Equal(0, vm.DirtyCount);

        vm.CloseByTimer = false;
        vm.AlwaysOnTop = false;

        Assert.Equal(2, vm.DirtyCount);
        Assert.True(vm.Touched[CameraPopupSettingsViewModel.GroupClose]);
        Assert.True(vm.Touched[CameraPopupSettingsViewModel.GroupWindow]);
        Assert.False(vm.Touched[CameraPopupSettingsViewModel.GroupMode]);
    }

    [Fact]
    public void should_raise_draft_changed_when_field_edited()
    {
        var (vm, _, _) = Make();
        var raised = 0;
        vm.DraftChanged += (_, _) => raised++;
        vm.EventWindowOnMalfunction = true;
        Assert.True(raised > 0);
    }

    [Fact]
    public void should_save_through_port_and_clear_dirty_when_applied()
    {
        var (vm, port, _) = Make();
        vm.Mode = CameraPopupMode.Broker;
        vm.BrokerOnOccupied = CameraPopupOnOccupied.Reject;

        var result = vm.Apply();

        Assert.True(result.Applied);
        Assert.Equal(1, port.SaveCount);
        Assert.Equal(CameraPopupMode.Broker, port.Stored.Mode);
        Assert.Equal(CameraPopupOnOccupied.Reject, port.Stored.BrokerOnOccupied);
        Assert.Equal(0, vm.DirtyCount);
    }

    [Fact]
    public void should_not_call_port_when_nothing_changed()
    {
        var (vm, port, _) = Make();
        var result = vm.Apply();
        Assert.True(result.Skipped);
        Assert.Equal(0, port.SaveCount);
    }

    [Fact]
    public void should_keep_draft_and_report_reason_when_port_throws()
    {
        var (vm, port, _) = Make();
        port.Throw = true;
        vm.CloseByTimer = false;

        var result = vm.Apply();

        Assert.False(result.Applied);
        Assert.Equal(CameraPopupSettingsViewModel.SaveFailedReason, result.Reason);
        Assert.Contains("disk full", result.Detail);
        Assert.Equal(1, vm.DirtyCount);
    }

    [Fact]
    public void should_report_not_taken_when_reread_differs()
    {
        var (vm, port, _) = Make();
        port.Ignore = true;
        vm.CloseByTimer = false;

        var result = vm.Apply();

        Assert.False(result.Applied);
        Assert.Equal(CameraPopupSettingsViewModel.NotTakenReason, result.Reason);
        Assert.Equal(1, vm.DirtyCount);
    }

    [Fact]
    public void should_restore_saved_values_without_port_call_when_reverted()
    {
        var (vm, port, _) = Make();
        vm.Mode = CameraPopupMode.None;
        vm.CascadeStepText = "abc";

        vm.Revert();

        Assert.Equal(CameraPopupMode.Self, vm.Mode);
        Assert.Equal("24", vm.CascadeStepText);
        Assert.False(vm.HasError);
        Assert.Equal(0, vm.DirtyCount);
        Assert.Equal(0, port.SaveCount);
    }

    [Fact]
    public void should_keep_draft_when_reloaded_while_dirty()
    {
        var (vm, port, _) = Make();
        vm.Mode = CameraPopupMode.None;
        port.Stored = port.Stored with { CloseTimerSeconds = 90 };

        vm.Reload();

        Assert.Equal(CameraPopupMode.None, vm.Mode);          // 초안은 산다(절을 넘나들어도)
        Assert.Equal(90, vm.Saved.CloseTimerSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    [InlineData("abc")]
    [InlineData("")]
    public void should_flag_error_and_keep_draft_when_timer_text_invalid(string text)
    {
        var (vm, _, _) = Make();
        vm.CloseTimerSecondsText = text;

        Assert.True(vm.HasError);
        Assert.True(vm.HasCloseTimerSecondsError);
        Assert.Equal(60, vm.Draft.CloseTimerSeconds);
        Assert.False(vm.Apply().Applied);
    }

    [Fact]
    public void should_update_draft_when_timer_text_valid()
    {
        var (vm, _, _) = Make();
        vm.CloseTimerSecondsText = "120";
        Assert.False(vm.HasError);
        Assert.Equal(120, vm.Draft.CloseTimerSeconds);
        Assert.Equal(1, vm.DirtyCount);
    }

    // ══════ 첫 창 끌기(데드존 · Esc) · 키보드 대체 ══════

    [Fact]
    public void should_not_move_first_window_when_drag_within_deadzone()
    {
        var (vm, _, _) = Make();
        vm.PressFirstWindow();

        Assert.False(vm.DragFirstWindow(5, 5));   // √50 < 8
        Assert.False(vm.EndFirstWindowDrag(commit: true));

        Assert.Equal(24, vm.FirstWindowX);
        Assert.Equal(0, vm.DirtyCount);
    }

    [Fact]
    public void should_move_first_window_by_scaled_delta_when_dragged()
    {
        var (vm, _, _) = Make();
        var scale = vm.PreviewScale;              // 주 모니터 1920×1040 → 320/1920 = 1/6
        vm.PressFirstWindow();

        Assert.True(vm.DragFirstWindow(20, 10));
        Assert.True(vm.IsDraggingFirstWindow);
        Assert.True(vm.EndFirstWindowDrag(commit: true));

        Assert.Equal(24 + (int)Math.Round(20 / scale), vm.FirstWindowX);
        Assert.Equal(24 + (int)Math.Round(10 / scale), vm.FirstWindowY);
        Assert.False(vm.IsDraggingFirstWindow);
    }

    [Fact]
    public void should_restore_press_position_when_drag_cancelled()
    {
        var (vm, _, _) = Make();
        vm.PressFirstWindow();
        vm.DragFirstWindow(60, 40);
        Assert.NotEqual(24, vm.FirstWindowX);

        vm.EndFirstWindowDrag(commit: false);     // Esc · 캡처 잃음

        Assert.Equal(24, vm.FirstWindowX);
        Assert.Equal(24, vm.FirstWindowY);
        Assert.Equal(0, vm.DirtyCount);
    }

    [Fact]
    public void should_keep_first_window_inside_work_area_when_dragged_far()
    {
        var (vm, _, _) = Make();
        vm.PressFirstWindow();
        vm.DragFirstWindow(5000, 5000);
        vm.EndFirstWindowDrag(commit: true);

        Assert.Equal(1920 - 960, vm.FirstWindowX);
        Assert.Equal(1040 - 600, vm.FirstWindowY);
    }

    [Fact]
    public void should_move_ten_px_or_hundred_with_shift_when_arrow_keys_used()
    {
        var (vm, _, _) = Make();
        vm.NudgeFirstWindow(1, 0, big: false);
        Assert.Equal(34, vm.FirstWindowX);
        vm.NudgeFirstWindow(0, 1, big: true);
        Assert.Equal(124, vm.FirstWindowY);
        vm.NudgeFirstWindow(-1, -1, big: true);
        Assert.Equal(0, vm.FirstWindowX);         // 왼쪽 끝에서 멈춘다
        Assert.Equal(24, vm.FirstWindowY);
    }

    [Fact]
    public void should_show_first_window_and_two_ghosts_stepping_right_down()
    {
        var (vm, _, _) = Make();
        var ghosts = vm.GhostWindows;
        Assert.Equal(2, ghosts.Count);
        Assert.True(ghosts[0].Left > vm.FirstWindowPreview.Left);
        Assert.True(ghosts[0].Top > vm.FirstWindowPreview.Top);
        Assert.Equal(3, vm.FirstWindowPreview.Columns);
        Assert.Equal(2, vm.FirstWindowPreview.Rows);
    }

    // ══════ 모니터 ══════

    [Fact]
    public void should_select_primary_without_dirty_when_no_target_saved()
    {
        var (vm, _, _) = Make();
        Assert.Equal(@"\\.\DISPLAY1", vm.SelectedMonitor!.Monitor.DeviceName);
        Assert.Equal(0, vm.DirtyCount);
        Assert.False(vm.MonitorNeedsAttention);
    }

    [Fact]
    public void should_store_monitor_id_and_clamp_position_when_monitor_changed()
    {
        var (vm, _, _) = Make(new CameraPopupSettings { FirstWindowX = 2000 });   // 2560 폭에선 괜찮다
        vm.SelectedMonitor = vm.Monitors.Single(m => m.Monitor.DeviceName == @"\\.\DISPLAY2");
        Assert.Equal(@"\\.\DISPLAY2@2560x1440", vm.Draft.TargetMonitorId);
        Assert.Equal(2560 - 960, vm.FirstWindowX);

        vm.SelectedMonitor = vm.Monitors.Single(m => m.Monitor.DeviceName == @"\\.\DISPLAY1");
        Assert.Equal(1920 - 960, vm.FirstWindowX);
    }

    [Fact]
    public void should_warn_and_use_primary_when_saved_monitor_missing()
    {
        var (vm, _, _) = Make(new CameraPopupSettings { TargetMonitorId = @"\\.\DISPLAY9@1920x1080" });
        Assert.True(vm.MonitorNeedsAttention);
        Assert.Contains("주 모니터", vm.MonitorNote);
        Assert.Equal(@"\\.\DISPLAY1", vm.SelectedMonitor!.Monitor.DeviceName);
        Assert.Equal(0, vm.DirtyCount);           // 사람이 고르기 전에는 초안을 바꾸지 않는다
    }

    [Fact]
    public void should_note_no_monitors_when_enumeration_empty()
    {
        var (vm, _, monitors) = Make();
        monitors.List.Clear();
        vm.RefreshMonitors();
        Assert.False(vm.HasMonitors);
        Assert.Equal(CameraPopupSettingsViewModel.NoMonitorsNote, vm.MonitorNote);
        Assert.True(vm.PreviewWidth > 0);         // 가상 1920×1040 으로 미리보기는 그린다
    }

    [Fact]
    public void should_shrink_to_monitor_and_note_when_window_size_exceeds_monitor()
    {
        var (vm, _, monitors) = Make();
        monitors.List[0] = monitors.List[0] with { WorkArea = new PixelRect(0, 0, 1280, 680) };
        vm.RefreshMonitors();

        vm.SelectedSize = new CameraPopupSizeChoice(1920, 1080);

        Assert.Equal(1920, vm.Draft.WindowWidth);                     // 저장 값은 고른 그대로
        Assert.Contains("모니터 크기로", vm.PlacementStatusText);
        Assert.Equal(0, vm.FirstWindowX);
    }

    [Fact]
    public void should_include_custom_size_in_choices_when_stored_size_not_preset()
    {
        var (vm, _, _) = Make(new CameraPopupSettings { WindowWidth = 1000, WindowHeight = 700 });
        Assert.Equal(new CameraPopupSizeChoice(1000, 700), vm.SizeChoices[0]);
        Assert.Equal(new CameraPopupSizeChoice(1000, 700), vm.SelectedSize);
    }
}
