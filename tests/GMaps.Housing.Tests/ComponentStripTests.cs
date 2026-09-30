using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 부품 칸 줄(L2) — component-display-unify FR-04 · FR-08 · NFR-01/02.
/// 대표 4칸 + n · 칸 모양 · LOD 48px · 밀도 30개 · 6.3 무회귀(그리지 않음) · 라이트/다크 렌더.
/// </summary>
public class ComponentStripTests
{
    private static ComponentSnapshot Snapshot(params (string Key, string Type, bool? InService, string? State, string? Health)[] parts)
        => ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(true,
            parts.Select(p => (p.Key, p.Type, (string?)null, p.InService, p.State, p.Health, (string?)null, p.Health != null || p.State != null)).ToArray()));

    [Fact]
    public void should_take_first_four_of_card_order_and_count_rest_when_many_components()
    {
        var strip = ComponentStripRules.Build(Snapshot(
            ("ptz", "PTZ_UNIT", null, "IDLE", "OK"),
            ("tracker", "TRACKER", null, "ACTIVE", "OK"),
            ("ir", "IR_LED", null, "ON", "DEGRADED"),
            ("wiper", "WIPER", null, "OFF", null),
            ("heater", "HEATER", false, "OFF", "OK"),
            ("fan", "FAN", null, "ON", "FAULT")));

        // 카드 순서: 고장(fan) → 저하(ir) → 나머지 선언 순서(ptz · tracker · wiper) → 사용 안 함(heater)
        Assert.Equal(new[] { "fan", "ir", "ptz", "tracker" }, strip.Chips.Select(c => c.Key));
        Assert.Equal(2, strip.MoreCount);
        Assert.True(strip.HasFault);
        Assert.Equal(new[] { ComponentChipKind.Fault, ComponentChipKind.Degraded, ComponentChipKind.Normal, ComponentChipKind.Active },
            strip.Chips.Select(c => c.Kind));
    }

    [Theory]
    [InlineData(true, "ON", "OK", ComponentChipKind.Active)]
    [InlineData(true, "RUNNING", "OK", ComponentChipKind.Active)]
    [InlineData(true, "IDLE", "OK", ComponentChipKind.Normal)]
    [InlineData(true, "ON", "FAULT", ComponentChipKind.Fault)]           // 고장이 가동을 이긴다
    [InlineData(true, "OFF", "DEGRADED", ComponentChipKind.Degraded)]
    [InlineData(true, null, "UNKNOWN", ComponentChipKind.Unknown)]
    [InlineData(false, "ON", "FAULT", ComponentChipKind.OutOfService)]   // 사용 안 함 = 사선
    public void should_pick_chip_shape_when_state_and_health_combine(bool inService, string? state, string? health, ComponentChipKind expected)
    {
        var snapshot = Snapshot(("x", "HEATER", inService ? null : false, state, health));
        Assert.Equal(expected, ComponentStripRules.KindOf(snapshot.Rows[0]));
    }

    [Fact]
    public void should_draw_no_strip_when_axes_or_status_missing()
    {
        Assert.True(ComponentStripRules.Build(ComponentSnapshot.None).IsEmpty);                                   // 6.3
        Assert.True(ComponentStripRules.Build(ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(false,
            ("h", "HEATER", null, null, null, null, null, false)))).IsEmpty);                                     // 관측 미수신
        Assert.True(ComponentHealthSummary.None.Strip.IsEmpty);
    }

    [Theory]
    // 화면 px, 밀집, 강조(선택 · 호버), 고장 있음 → 그림
    [InlineData(47.9, false, false, false, false)]   // 48px 미만 — 작게 보이면 아예 안 그린다
    [InlineData(48, false, false, false, true)]
    [InlineData(64, true, false, false, false)]      // 밀집 — 평범한 아이콘은 숨김
    [InlineData(64, true, true, false, true)]        // 밀집이어도 선택 · 호버는 그림
    [InlineData(64, true, false, true, true)]        // 밀집이어도 고장은 그림
    [InlineData(40, true, true, true, false)]        // 작으면 강조 · 고장이어도 안 그림(LOD 우선)
    public void should_gate_strip_by_size_and_density_when_map_is_crowded(double px, bool crowded, bool emphasized, bool faulty, bool expected)
    {
        var strip = ComponentStripRules.Build(Snapshot(("a", "HEATER", null, "ON", faulty ? "FAULT" : "OK")));
        Assert.Equal(expected, ComponentStripRules.ShowsStrip(strip, px, crowded, emphasized));
    }

    [Fact]
    public void should_hide_degraded_only_strip_when_crowded_because_prd_exempts_only_faults()
    {
        var strip = ComponentStripRules.Build(Snapshot(("a", "HEATER", null, "ON", "DEGRADED")));
        Assert.False(strip.HasFault);
        Assert.False(ComponentStripRules.ShowsStrip(strip, 64, crowded: true, emphasized: false));
        Assert.True(ComponentStripRules.ShowsStrip(strip, 64, crowded: false, emphasized: false));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(31, true)]
    [InlineData(0, false)]
    public void should_call_crowded_only_above_thirty_when_counting_large_icons(int count, bool expected)
        => Assert.Equal(expected, ComponentStripRules.IsCrowded(count));

    [Fact]
    public void should_not_qualify_for_density_when_strip_is_empty_or_small()
    {
        var strip = ComponentStripRules.Build(Snapshot(("a", "HEATER", null, "ON", "OK")));
        Assert.True(ComponentStripRules.Qualifies(strip, 48));
        Assert.False(ComponentStripRules.Qualifies(strip, 47));
        Assert.False(ComponentStripRules.Qualifies(ComponentStrip.Empty, 96));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_draw_strip_below_icon_only_when_large_in_light_and_dark(bool dark) => HousingTests.Sta(() =>
    {
        var (small, smallOverlay) = Render(dark, scale: 1.0, crowded: false);
        var (large, largeOverlay) = Render(dark, scale: 1.5, crowded: false);
        var (crowdedBitmap, crowdedOverlay) = Render(dark, scale: 1.5, crowded: true, healthy: true);

        Assert.False(smallOverlay.IsStripDrawn);        // 40px — 칸 줄 없음
        Assert.True(largeOverlay.IsStripDrawn);         // 60px — 칸 줄
        Assert.False(crowdedOverlay.IsStripDrawn);      // 밀집 + 정상 아이콘 — 숨김

        // 모양 검증 — 아이콘 아래 띠에만 픽셀이 생긴다.
        var band = new Int32Rect(0, Pad + Size + 5, Canvas, 14);   // 배지 걸침(아래 4px) 바로 밑부터
        Assert.Equal(0, Ink(small, band));
        Assert.True(Ink(large, band) > 20, "칸 줄 픽셀 없음");
        Assert.Equal(0, Ink(crowdedBitmap, band));
    });

    // ── helpers ──

    private const int Canvas = 120, Pad = 20, Size = 40;

    private static (RenderTargetBitmap Bitmap, ComponentStatusOverlay Overlay) Render(bool dark, double scale, bool crowded, bool healthy = false)
    {
        var host = new Canvas { Width = Canvas, Height = Canvas, Background = Brushes.Transparent };
        host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative) });

        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = "cam", DeviceType = EnumDeviceType.IpCamera });
        marker.Width = Size; marker.Height = Size; marker.IsVisible = true;
        marker.LinkedDevice = new BaseDeviceModel
        {
            Id = 7, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED,
            Axes = ComponentHealthSummaryTests.Axes(true,
                ("ptz", "PTZ_UNIT", null, null, "IDLE", "OK", null, true),
                ("tracker", "TRACKER", null, null, "ACTIVE", "OK", null, true),
                ("ir", "IR_LED", null, null, "ON", healthy ? "OK" : "DEGRADED", healthy ? null : "OVER_CURRENT", true),
                ("wiper", "WIPER", null, null, "OFF", null, null, false),
                ("fan", "FAN", null, false, "OFF", "OK", null, true)),
        };
        if (crowded) marker.ComponentStripCrowded = true;   // 지도가 심볼에 내려 주는 값 → 컨트롤 → 부품 층
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        control.Width = Size; control.Height = Size;
        control.MarkerScreenScale = scale;
        System.Windows.Controls.Canvas.SetLeft(control, Pad); System.Windows.Controls.Canvas.SetTop(control, Pad);
        host.Children.Add(control);
        HousingTests.Layout(host, Canvas, Canvas);

        var overlay = FindOverlay(control);
        var bitmap = new RenderTargetBitmap(Canvas, Canvas, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        return (bitmap, overlay);
    }

    private static int Ink(BitmapSource bitmap, Int32Rect rect)
    {
        int stride = rect.Width * 4;
        var pixels = new byte[stride * rect.Height];
        bitmap.CopyPixels(rect, pixels, stride, 0);
        int ink = 0;
        for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 40) ink++;   // 투명 배경 위 알파
        return ink;
    }

    private static ComponentStatusOverlay FindOverlay(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ComponentStatusOverlay found) return found;
            try { return FindOverlay(child); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("ComponentStatusOverlay 없음");
    }
}
