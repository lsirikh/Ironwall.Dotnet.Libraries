using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 카메라 팝업 설정 모델 · 이관 · 격자 · 배치 순수 함수 (camera-popup-modes T-03)
   Created By   : Claude (T-03)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>격자 규칙(PRD FR-10) — 카메라 수별 고를 수 있는 격자와 스냅.</summary>
public class CameraPopupGridLayoutsTests
{
    [Theory]
    [InlineData(1, "1x1")]
    [InlineData(2, "2x1,1x2")]
    [InlineData(3, "3x1,1x3")]
    [InlineData(4, "2x2,4x1,1x4")]
    [InlineData(5, "3x2,2x3")]
    [InlineData(6, "3x2,2x3,6x1,1x6")]
    public void should_offer_exact_layouts_from_prd_table_when_count_given(int count, string expected)
        => Assert.Equal(expected, string.Join(",", CameraPopupGridLayouts.Allowed(count).Select(l => l.Key)));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 6)]
    [InlineData(99, 6)]
    public void should_clamp_camera_count_when_out_of_range(int count, int expected)
        => Assert.Equal(expected, CameraPopupGridLayouts.ClampCount(count));

    [Fact]
    public void should_keep_layout_when_still_allowed_after_count_change()
        => Assert.Equal(new CameraPopupGridLayout(2, 3), CameraPopupGridLayouts.Snap(5, new CameraPopupGridLayout(2, 3)));

    [Fact]
    public void should_snap_to_default_when_layout_not_allowed_for_new_count()
        => Assert.Equal(new CameraPopupGridLayout(3, 2), CameraPopupGridLayouts.Snap(5, new CameraPopupGridLayout(6, 1)));

    [Fact]
    public void should_snap_to_default_when_no_current_layout()
        => Assert.Equal(new CameraPopupGridLayout(2, 2), CameraPopupGridLayouts.Snap(4, null));

    [Fact]
    public void should_leave_one_empty_cell_when_five_cameras_use_three_by_two()
    {
        var layout = CameraPopupGridLayouts.Default(5);
        Assert.Equal(6, layout.Capacity);
        Assert.True(layout.Capacity - 5 == 1);
    }

    [Theory]
    [InlineData("3x2", 3, 2)]
    [InlineData("3X2", 3, 2)]
    [InlineData(" 2×3 ", 2, 3)]
    public void should_parse_layout_when_text_is_columns_by_rows(string text, int columns, int rows)
    {
        Assert.True(CameraPopupGridLayout.TryParse(text, out var layout));
        Assert.Equal(new CameraPopupGridLayout(columns, rows), layout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("0x2")]
    [InlineData("ax2")]
    [InlineData("3x2x1")]
    public void should_reject_layout_when_text_is_malformed(string? text)
        => Assert.False(CameraPopupGridLayout.TryParse(text, out _));
}

/// <summary>저장 모양 ↔ 설정 값 · 옛 키 이관(PRD FR-01 · FR-18).</summary>
public class CameraPopupSettingsCodecTests
{
    private static CameraPopupSettings Resolve(CameraPopupSettingsRecord? record, bool used = true,
                                               EnumCameraPopupRtspSource source = EnumCameraPopupRtspSource.Url,
                                               bool autoDiscard = true, int timeout = 15)
        => CameraPopupSettingsCodec.Resolve(record, used, source, autoDiscard, timeout);

    [Fact]
    public void should_migrate_to_self_when_legacy_popup_used_is_true()
        => Assert.Equal(CameraPopupMode.Self, Resolve(null, used: true).Mode);

    [Fact]
    public void should_migrate_to_none_when_legacy_popup_used_is_false()
        => Assert.Equal(CameraPopupMode.None, Resolve(null, used: false).Mode);

    [Fact]
    public void should_migrate_to_rtsp_url_provider_when_legacy_source_is_url()
        => Assert.Equal(VideoProviderKind.RtspUrl, Resolve(null, source: EnumCameraPopupRtspSource.Url).Provider);

    [Fact]
    public void should_migrate_to_onvif_provider_when_legacy_source_is_onvif()
        => Assert.Equal(VideoProviderKind.Onvif, Resolve(null, source: EnumCameraPopupRtspSource.Onvif).Provider);

    [Fact]
    public void should_prefer_new_keys_over_legacy_when_both_present()
    {
        var s = Resolve(new CameraPopupSettingsRecord { Mode = "Broker", Provider = "Onvif" }, used: false,
                        source: EnumCameraPopupRtspSource.Url);
        Assert.Equal(CameraPopupMode.Broker, s.Mode);
        Assert.Equal(VideoProviderKind.Onvif, s.Provider);
    }

    [Theory]
    [InlineData("Brokr")]
    [InlineData("7")]
    [InlineData("")]
    [InlineData(null)]
    public void should_fall_back_to_legacy_mode_when_new_mode_is_unreadable(string? mode)
        => Assert.Equal(CameraPopupMode.None, Resolve(new CameraPopupSettingsRecord { Mode = mode }, used: false).Mode);

    [Fact]
    public void should_accept_old_url_spelling_when_parsing_provider()
        => Assert.Equal(VideoProviderKind.RtspUrl, Resolve(new CameraPopupSettingsRecord { Provider = "url" }, source: EnumCameraPopupRtspSource.Onvif).Provider);

    [Fact]
    public void should_fall_back_to_onvif_when_external_vms_is_stored()
        => Assert.Equal(VideoProviderKind.Onvif, Resolve(new CameraPopupSettingsRecord { Provider = "ExternalVms" }).Provider);

    [Fact]
    public void should_use_code_defaults_when_record_missing()
    {
        var s = Resolve(null);
        Assert.True(s.EventWindowOnDetection);
        Assert.False(s.EventWindowOnMalfunction);
        Assert.Equal(6, s.CamerasPerWindow);
        Assert.Equal(new CameraPopupGridLayout(3, 2), s.GridLayout);
        Assert.Equal(10, s.MaxOpenWindows);
        Assert.Equal(24, s.CascadeStepPx);
        Assert.Equal(960, s.WindowWidth);
        Assert.Equal(600, s.WindowHeight);
        Assert.True(s.AlwaysOnTop);
        Assert.True(s.CloseOnActionReport);
        Assert.True(s.CloseByTimer);
        Assert.Equal(60, s.CloseTimerSeconds);
        Assert.True(s.ReturnHomePresetOnClose);
        Assert.Equal(CameraPopupOnOccupied.Replace, s.BrokerOnOccupied);
        Assert.Equal(0, s.BrokerCell);
        Assert.Equal(5, s.BrokerResponseTimeoutSeconds);
        Assert.Equal(string.Empty, s.TargetMonitorId);
    }

    [Fact]
    public void should_carry_double_click_auto_close_from_existing_keys()
    {
        var s = Resolve(null, autoDiscard: false, timeout: 42);
        Assert.False(s.DoubleClickAutoClose);
        Assert.Equal(42, s.DoubleClickAutoCloseSeconds);
    }

    [Fact]
    public void should_clamp_values_when_stored_out_of_range()
    {
        var s = Resolve(new CameraPopupSettingsRecord
        {
            CamerasPerWindow = 9,
            GridLayout = "1x1",
            MaxOpenWindows = 50,
            CascadeStepPx = -5,
            CloseTimerSeconds = 0,
            BrokerCell = 12,
            BrokerResponseTimeoutSeconds = 0,
            WindowWidth = 10,
        });
        Assert.Equal(6, s.CamerasPerWindow);
        Assert.Equal(new CameraPopupGridLayout(3, 2), s.GridLayout);   // 1x1 은 6대에서 못 고른다 → 기본
        Assert.Equal(10, s.MaxOpenWindows);
        Assert.Equal(0, s.CascadeStepPx);
        Assert.Equal(CameraPopupSettings.MinCloseTimerSeconds, s.CloseTimerSeconds);
        Assert.Equal(9, s.BrokerCell);
        Assert.Equal(1, s.BrokerResponseTimeoutSeconds);
        Assert.Equal(CameraPopupSettings.MinWindowWidth, s.WindowWidth);
    }

    [Fact]
    public void should_round_trip_when_written_then_read()
    {
        var original = new CameraPopupSettings
        {
            Mode = CameraPopupMode.Broker,
            Provider = VideoProviderKind.RtspUrl,
            CamerasPerWindow = 4,
            GridLayout = new CameraPopupGridLayout(1, 4),
            MaxOpenWindows = 3,
            TargetMonitorId = @"\\.\DISPLAY2@2560x1440",
            FirstWindowX = 100,
            FirstWindowY = 50,
            CascadeStepPx = 30,
            CloseByTimer = false,
            BrokerCell = 3,
            BrokerOnOccupied = CameraPopupOnOccupied.Reject,
            DoubleClickAutoClose = false,
            DoubleClickAutoCloseSeconds = 33,
        }.Normalize();

        var record = CameraPopupSettingsCodec.ToRecord(original);
        var back = CameraPopupSettingsCodec.Resolve(record, legacyIsCameraPopupUsed: true, EnumCameraPopupRtspSource.Onvif,
                                                   autoDiscard: false, timeoutSeconds: 33);

        Assert.Equal(original, back);
        Assert.Equal("REJECT", record.BrokerOnOccupied);
        Assert.Equal("1x4", record.GridLayout);
        Assert.Equal("Broker", record.Mode);
    }

    [Theory]
    [InlineData(CameraPopupMode.Self, true)]
    [InlineData(CameraPopupMode.Broker, true)]
    [InlineData(CameraPopupMode.None, false)]
    public void should_write_legacy_popup_used_when_mode_saved(CameraPopupMode mode, bool expected)
        => Assert.Equal(expected, CameraPopupSettingsCodec.LegacyIsCameraPopupUsed(new CameraPopupSettings { Mode = mode }));

    [Theory]
    [InlineData(VideoProviderKind.Onvif, EnumCameraPopupRtspSource.Onvif)]
    [InlineData(VideoProviderKind.RtspUrl, EnumCameraPopupRtspSource.Url)]
    public void should_write_legacy_rtsp_source_when_provider_saved(VideoProviderKind provider, EnumCameraPopupRtspSource expected)
        => Assert.Equal(expected, CameraPopupSettingsCodec.LegacyRtspSource(new CameraPopupSettings { Provider = provider }));

    [Fact]
    public void should_open_event_windows_only_in_self_mode()
    {
        Assert.True(new CameraPopupSettings { Mode = CameraPopupMode.Self }.OpensEventWindows);
        Assert.False(new CameraPopupSettings { Mode = CameraPopupMode.Broker }.OpensEventWindows);
        Assert.False(new CameraPopupSettings { Mode = CameraPopupMode.None }.OpensEventWindows);
        Assert.False(new CameraPopupSettings { EventWindowOnDetection = false, EventWindowOnMalfunction = false }.OpensEventWindows);
    }
}

/// <summary>배치 — 모니터 찾기 · 계단식 · 화면 끝 되돌이 · 화면 밖 당기기(PRD FR-12).</summary>
public class CameraPopupPlacementTests
{
    private static readonly PixelRect Work = new(0, 0, 1920, 1040);

    [Fact]
    public void should_step_right_down_by_cascade_step_when_room_left()
    {
        var rects = CameraPopupPlacement.Cascade(Work, 24, 24, 960, 600, 24, 3);
        Assert.Equal(new PixelRect(24, 24, 960, 600), rects[0]);
        Assert.Equal(new PixelRect(48, 48, 960, 600), rects[1]);
        Assert.Equal(new PixelRect(72, 72, 960, 600), rects[2]);
    }

    [Fact]
    public void should_wrap_to_first_position_when_next_window_crosses_edge()
    {
        // 세로 여유 1040 − (24+600) = 416 → 416/24 = 17 → 한 바퀴 18개. 19번째(index 18)는 첫 자리.
        var first = CameraPopupPlacement.FirstWindow(Work, 24, 24, 960, 600);
        Assert.Equal(18, CameraPopupPlacement.CascadePeriod(Work, first, 24));
        Assert.Equal(first, CameraPopupPlacement.CascadeAt(Work, 24, 24, 960, 600, 24, 18));
        var last = CameraPopupPlacement.CascadeAt(Work, 24, 24, 960, 600, 24, 17);
        Assert.True(Work.Contains(last));
    }

    [Fact]
    public void should_keep_every_cascade_window_on_screen_when_many_windows()
    {
        foreach (var r in CameraPopupPlacement.Cascade(Work, 900, 400, 960, 600, 40, 30))
            Assert.True(Work.Contains(r));
    }

    [Fact]
    public void should_stack_on_same_spot_when_step_is_zero()
    {
        var rects = CameraPopupPlacement.Cascade(Work, 10, 10, 400, 300, 0, 3);
        Assert.All(rects, r => Assert.Equal(rects[0], r));
    }

    [Fact]
    public void should_clamp_first_position_when_off_screen()
    {
        var c = CameraPopupPlacement.ClampRelative(Work, 5000, -20, 960, 600);
        Assert.Equal(1920 - 960, c.X);
        Assert.Equal(0, c.Y);
        Assert.True(c.Clamped);
    }

    [Fact]
    public void should_shrink_window_to_work_area_when_window_larger_than_monitor()
    {
        var small = new PixelRect(0, 0, 800, 560);
        var c = CameraPopupPlacement.ClampRelative(small, 0, 0, 960, 600);
        Assert.Equal(800, c.Width);
        Assert.Equal(560, c.Height);
        Assert.True(c.Clamped);
    }

    [Fact]
    public void should_offset_by_work_area_origin_when_monitor_not_at_zero()
    {
        var right = new PixelRect(1920, 0, 2560, 1400);
        Assert.Equal(new PixelRect(1920 + 24, 24, 960, 600), CameraPopupPlacement.FirstWindow(right, 24, 24, 960, 600));
    }

    private static readonly DisplayMonitorInfo Primary = new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true);
    private static readonly DisplayMonitorInfo Second = new(@"\\.\DISPLAY2", new PixelRect(1920, 0, 2560, 1440), new PixelRect(1920, 0, 2560, 1400), false);

    [Fact]
    public void should_find_monitor_when_device_and_resolution_match()
    {
        var r = CameraPopupPlacement.ResolveMonitor(new[] { Primary, Second }, Second.Id);
        Assert.Same(Second, r.Monitor);
        Assert.Equal(MonitorMatch.Exact, r.Match);
        Assert.False(r.NeedsNotice);
    }

    [Fact]
    public void should_use_same_device_and_notify_when_resolution_changed()
    {
        var r = CameraPopupPlacement.ResolveMonitor(new[] { Primary, Second }, @"\\.\DISPLAY2@1920x1080");
        Assert.Same(Second, r.Monitor);
        Assert.Equal(MonitorMatch.ResolutionChanged, r.Match);
        Assert.True(r.NeedsNotice);
    }

    [Fact]
    public void should_fall_back_to_primary_and_notify_when_target_monitor_missing()
    {
        var r = CameraPopupPlacement.ResolveMonitor(new[] { Second, Primary }, @"\\.\DISPLAY3@1920x1080");
        Assert.Same(Primary, r.Monitor);
        Assert.Equal(MonitorMatch.FallbackPrimary, r.Match);
        Assert.True(r.NeedsNotice);
    }

    [Fact]
    public void should_use_primary_without_notice_when_no_target_saved()
    {
        var r = CameraPopupPlacement.ResolveMonitor(new[] { Second, Primary }, "");
        Assert.Same(Primary, r.Monitor);
        Assert.Equal(MonitorMatch.DefaultPrimary, r.Match);
        Assert.False(r.NeedsNotice);
    }

    [Fact]
    public void should_report_no_monitors_when_list_empty()
        => Assert.Equal(MonitorMatch.NoMonitors, CameraPopupPlacement.ResolveMonitor(Array.Empty<DisplayMonitorInfo>(), "x").Match);

    [Fact]
    public void should_format_and_parse_monitor_id_when_round_tripped()
    {
        var id = CameraPopupMonitorId.Format(@"\\.\DISPLAY2", 2560, 1440);
        Assert.Equal(@"\\.\DISPLAY2@2560x1440", id);
        Assert.True(CameraPopupMonitorId.TryParse(id, out var device, out var w, out var h));
        Assert.Equal((@"\\.\DISPLAY2", 2560, 1440), (device, w, h));
        Assert.Equal(2, CameraPopupMonitorId.DisplayNumber(device));
    }

    [Fact]
    public void should_describe_monitor_with_number_resolution_scale_and_primary()
    {
        var m = Primary with { Dpi = 144 };
        Assert.Equal("모니터 1 · 1920×1080 · 150% · 주", m.Describe(1));
    }

    [Fact]
    public void should_not_throw_when_enumerating_real_monitors()
    {
        var list = new Win32DisplayMonitorProvider().GetMonitors();
        Assert.NotNull(list);
        Assert.All(list, m => Assert.False(m.Bounds.IsEmpty));
    }
}
