using System.Windows;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Monitors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose      : 모니터 식별 카드 판정 — 배치 · DPI · 확장 스타일 · 시간 (헤드리스)
   Created By   : Claude (monitor-identify)
   Created On   : 2026-10-01
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class MonitorIdentifyMathTests
{
    // ══════ 배치 ══════

    [Fact]
    public void should_center_window_on_monitor_when_dpi_is_100_percent()
    {
        var rect = MonitorIdentifyMath.CenteredWindow(new Int32Rect(1920, 0, 1920, 1080), pixelsPerDiu: 1.0);

        Assert.Equal(440, rect.Width);
        Assert.Equal(300, rect.Height);
        Assert.Equal(1920 + (1920 - 440) / 2, rect.X);
        Assert.Equal((1080 - 300) / 2, rect.Y);
    }

    [Fact]
    public void should_center_on_negative_coordinates_when_monitor_is_left_of_primary()
    {
        var rect = MonitorIdentifyMath.CenteredWindow(new Int32Rect(-2560, -200, 2560, 1440), pixelsPerDiu: 1.0);

        Assert.Equal(-2560 + (2560 - 440) / 2, rect.X);
        Assert.Equal(-200 + (1440 - 300) / 2, rect.Y);
    }

    [Fact]
    public void should_scale_window_by_pixels_per_diu_when_dpi_is_150_percent()
    {
        var rect = MonitorIdentifyMath.CenteredWindow(new Int32Rect(0, 0, 3840, 2160), pixelsPerDiu: 1.5);

        Assert.Equal(660, rect.Width);
        Assert.Equal(450, rect.Height);
        Assert.Equal((3840 - 660) / 2, rect.X);
    }

    [Fact]
    public void should_not_exceed_monitor_when_window_is_larger_than_monitor()
    {
        var monitor = new Int32Rect(100, 50, 400, 240);
        var rect = MonitorIdentifyMath.CenteredWindow(monitor, pixelsPerDiu: 2.0);

        Assert.Equal(monitor, rect);
    }

    [Fact]
    public void should_return_empty_when_monitor_is_empty()
    {
        Assert.True(MonitorIdentifyMath.CenteredWindow(Int32Rect.Empty, 1.0).IsEmpty);
        Assert.True(MonitorIdentifyMath.CenteredWindow(new Int32Rect(0, 0, 0, 1080), 1.0).IsEmpty);
    }

    [Fact]
    public void should_fall_back_to_one_pixel_per_diu_when_scale_is_invalid()
    {
        var rect = MonitorIdentifyMath.CenteredWindow(new Int32Rect(0, 0, 1920, 1080), double.NaN);
        Assert.Equal(440, rect.Width);
    }

    // ══════ 좌표계 · DPI ══════

    [Theory]
    [InlineData(MonitorIdentifyDpiMode.System, 144, 96, 1.5)]       // 시스템 인지(WPF 기본) — 시스템 DPI 로 그린다
    [InlineData(MonitorIdentifyDpiMode.PerMonitor, 96, 144, 1.5)]   // 모니터별 인지 — 그 모니터 DPI
    [InlineData(MonitorIdentifyDpiMode.PerMonitor, 144, 96, 1.0)]
    [InlineData(MonitorIdentifyDpiMode.Unaware, 144, 192, 1.0)]     // 비인지 — 모두 96 으로 가상화
    public void should_pick_pixels_per_diu_from_the_thread_dpi_mode_when_placing(MonitorIdentifyDpiMode mode, int systemDpi, int monitorDpi, double expected)
    {
        Assert.Equal(expected, MonitorIdentifyMath.PixelsPerDiu(mode, systemDpi, monitorDpi), 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-96)]
    [InlineData(100000)]
    public void should_use_96_when_dpi_is_invalid(int dpi)
    {
        Assert.Equal(96, MonitorIdentifyMath.SafeDpi(dpi));
        Assert.Equal(1.0, MonitorIdentifyMath.PixelsPerDiu(MonitorIdentifyDpiMode.PerMonitor, 96, dpi), 6);
    }

    [Fact]
    public void should_use_bounds_seen_in_thread_space_when_device_name_matches()
    {
        // 목록은 물리 픽셀(150% 모니터 2560 폭), 시스템 인지 스레드에서는 같은 모니터가 논리 1707 폭으로 보인다.
        var seen = new[]
        {
            (@"\\.\DISPLAY1", new Int32Rect(0, 0, 1920, 1080)),
            (@"\\.\DISPLAY2", new Int32Rect(1920, 0, 1707, 960)),
        };
        var picked = MonitorIdentifyMath.PickBounds(seen, @"\\.\display2", new Int32Rect(1920, 0, 2560, 1440));

        Assert.Equal(new Int32Rect(1920, 0, 1707, 960), picked);
    }

    [Fact]
    public void should_fall_back_to_physical_bounds_when_device_is_not_seen()
    {
        var fallback = new Int32Rect(1920, 0, 2560, 1440);
        var seen = new[] { (@"\\.\DISPLAY1", new Int32Rect(0, 0, 1920, 1080)) };

        Assert.Equal(fallback, MonitorIdentifyMath.PickBounds(seen, @"\\.\DISPLAY7", fallback));
        Assert.Equal(fallback, MonitorIdentifyMath.PickBounds(seen, string.Empty, fallback));
    }

    // ══════ 창 스타일 · 접근성 ══════

    [Fact]
    public void should_add_click_through_bits_and_keep_others_when_ex_style_is_computed()
    {
        const long topmost = 0x00000008;
        var style = MonitorIdentifyMath.ClickThroughExStyle(topmost | MonitorIdentifyMath.WsExAppWindow);

        Assert.True(MonitorIdentifyMath.IsClickThrough(style));
        Assert.NotEqual(0, style & MonitorIdentifyMath.WsExLayered);
        Assert.NotEqual(0, style & topmost);                              // 다른 비트는 그대로
        Assert.Equal(0, style & MonitorIdentifyMath.WsExAppWindow);      // 작업 표시줄 강제 표시는 끈다
    }

    [Fact]
    public void should_not_be_click_through_when_transparent_bit_is_missing()
    {
        Assert.False(MonitorIdentifyMath.IsClickThrough(MonitorIdentifyMath.WsExNoActivate | MonitorIdentifyMath.WsExToolWindow));
    }

    [Fact]
    public void should_format_automation_id_with_monitor_number_when_prefix_is_given()
    {
        Assert.Equal("CameraPopup.MonitorIdentify.2", MonitorIdentifyMath.AutomationId("CameraPopup.MonitorIdentify", 2));
    }

    // ══════ 시간 · 움직임 줄이기 ══════

    [Fact]
    public void should_finish_within_duration_when_animation_is_on()
    {
        Assert.True(MonitorIdentifyMath.ShouldAnimate(clientAreaAnimation: true));
        Assert.Equal(MonitorIdentifyMath.Duration, MonitorIdentifyMath.DismissAfter(true) + MonitorIdentifyMath.FadeOut);
        Assert.Equal(TimeSpan.FromSeconds(3), MonitorIdentifyMath.Duration);
    }

    [Fact]
    public void should_skip_fades_and_close_at_duration_when_client_area_animation_is_off()
    {
        Assert.False(MonitorIdentifyMath.ShouldAnimate(clientAreaAnimation: false));
        Assert.Equal(MonitorIdentifyMath.Duration, MonitorIdentifyMath.DismissAfter(false));
    }
}
