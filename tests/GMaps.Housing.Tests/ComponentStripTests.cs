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
/// 지도 부품 이름표(L2) — component-display-unify FR-04 · FR-08 · NFR-01/02, 2026-10-01 사용자 결정 "1안 이상 있을 때만 이름표".
/// 이름표 글 · 가장 급한 부품 · 8 글자 줄임 · 정상/미상 = 표시 없음 · 호버/선택 문장 · LOD 48px · 밀도 30개 · 6.3 무회귀 · 라이트/다크 렌더.
/// </summary>
public class ComponentStripTests
{
    private static ComponentSnapshot Snapshot(params (string Key, string Type, bool? InService, string? State, string? Health)[] parts)
        => ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(true,
            parts.Select(p => (p.Key, p.Type, (string?)null, p.InService, p.State, p.Health, (string?)null, p.Health != null || p.State != null)).ToArray()));

    private static ComponentSnapshot Labeled(params (string Key, string Label, string? Health)[] parts)
        => ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(true,
            parts.Select(p => (p.Key, "HEATER", (string?)p.Label, (bool?)null, (string?)"ON", p.Health, (string?)null, p.Health != null)).ToArray()));

    // ── 이름표 글(순수) ──

    [Fact]
    public void should_name_most_urgent_part_with_health_word_and_count_rest_when_faults_and_degradation_mix()
    {
        var strip = ComponentStripRules.Build(Labeled(("a", "히터", "OK"), ("b", "적외선 LED", "DEGRADED"), ("c", "레이더", "FAULT"), ("d", "팬", "FAULT")));

        Assert.Equal(ComponentPlateSeverity.Fault, strip.Severity);
        Assert.Equal("레이더 고장 +2", strip.IssueText);                     // 고장 먼저(선언 순서) · 나머지 고장 1 + 저하 1
        Assert.Equal("레이더 고장 +2", ComponentStripRules.PlateText(strip));
        Assert.True(strip.HasFault);
    }

    [Fact]
    public void should_write_degraded_plate_without_count_when_only_one_part_is_degraded()
    {
        var strip = ComponentStripRules.Build(Labeled(("a", "히터", "OK"), ("b", "적외선 LED", "DEGRADED")));

        Assert.Equal(ComponentPlateSeverity.Degraded, strip.Severity);
        Assert.Equal("적외선 LED 저하", strip.IssueText);
        Assert.False(strip.HasFault);
    }

    [Theory]
    [InlineData("레이더", "레이더")]
    [InlineData("PTZ 구동부", "PTZ 구동부")]                 // 7 글자 — 그대로
    [InlineData("12345678", "12345678")]                     // 정확히 8 글자 — 그대로
    [InlineData("네트워크 인터페이스", "네트워크 인터페…")]      // 10 글자 → 앞 8 + "…"
    [InlineData("열화상 카메라 모듈 A", "열화상 카메라…")]       // 줄인 끝의 공백은 뺀다
    public void should_truncate_component_name_at_eight_characters_when_name_is_long(string name, string expected)
        => Assert.Equal(expected, ComponentStripRules.TruncateName(name));

    [Fact]
    public void should_keep_full_name_for_tooltip_when_plate_name_is_truncated()
    {
        var strip = ComponentStripRules.Build(Labeled(("n", "네트워크 인터페이스", "FAULT")));
        Assert.Equal("네트워크 인터페… 고장", strip.IssueText);
        Assert.Equal("네트워크 인터페이스", strip.LeadFullName);
    }

    [Theory]
    [InlineData("OK", "OK", "부품 2 · 이상 없음")]
    [InlineData("OK", null, "부품 2 · 이상 없음")]          // 정상 + 미상 — 이상 없음
    [InlineData(null, null, "부품 2 · 미상")]              // 전부 미상
    public void should_have_no_issue_but_offer_summary_sentence_when_parts_are_normal_or_unknown(string? first, string? second, string idle)
    {
        var strip = ComponentStripRules.Build(Labeled(("a", "히터", first), ("b", "팬", second)));

        Assert.False(strip.HasIssue);
        Assert.Equal(string.Empty, strip.IssueText);
        Assert.Equal(idle, strip.IdleText);
        Assert.Equal(idle, ComponentStripRules.PlateText(strip));
        Assert.False(ComponentStripRules.ShowsStrip(strip, 64, crowded: false, emphasized: false));   // 평소 — 표시 없음
        Assert.True(ComponentStripRules.ShowsStrip(strip, 64, crowded: false, emphasized: true));     // 호버 · 선택 — 문장
    }

    [Fact]
    public void should_ignore_out_of_service_faults_when_choosing_lead()
    {
        var strip = ComponentStripRules.Build(Snapshot(("spare", "HEATER", false, "OFF", "FAULT"), ("fan", "FAN", null, "ON", "OK")));
        Assert.False(strip.HasIssue);
        Assert.False(strip.HasFault);
    }

    [Fact]
    public void should_draw_no_plate_when_axes_or_status_missing()
    {
        Assert.True(ComponentStripRules.Build(ComponentSnapshot.None).IsEmpty);                                   // 6.3
        Assert.True(ComponentStripRules.Build(ComponentSnapshot.Build(ComponentHealthSummaryTests.Axes(false,
            ("h", "HEATER", null, null, null, null, null, false)))).IsEmpty);                                     // 관측 미수신
        Assert.True(ComponentHealthSummary.None.Strip.IsEmpty);
        Assert.False(ComponentStripRules.ShowsStrip(ComponentStrip.Empty, 96, crowded: false, emphasized: true));  // 호버여도 그릴 것 없음
    }

    // ── LOD · 밀도(순수) ──

    [Theory]
    // 화면 px, 밀집, 강조(선택 · 호버), 고장 있음 → 그림
    [InlineData(47.9, false, false, true, false)]    // 48px 미만 — 작게 보이면 아예 안 그린다
    [InlineData(48, false, false, true, true)]       // 고장 — 그림
    [InlineData(48, false, false, false, false)]     // 정상 — 평소엔 표시 없음
    [InlineData(48, false, true, false, true)]       // 정상 + 호버 · 선택 — 요약 문장
    [InlineData(64, true, false, false, false)]      // 밀집 — 평범한 아이콘은 숨김
    [InlineData(64, true, true, false, true)]        // 밀집이어도 선택 · 호버는 그림
    [InlineData(64, true, false, true, true)]        // 밀집이어도 고장은 그림
    [InlineData(40, true, true, true, false)]        // 작으면 강조 · 고장이어도 안 그림(LOD 우선)
    public void should_gate_plate_by_size_density_and_issue_when_map_is_shown(double px, bool crowded, bool emphasized, bool faulty, bool expected)
    {
        var strip = ComponentStripRules.Build(Snapshot(("a", "HEATER", null, "ON", faulty ? "FAULT" : "OK")));
        Assert.Equal(expected, ComponentStripRules.ShowsStrip(strip, px, crowded, emphasized));
    }

    [Fact]
    public void should_hide_degraded_only_plate_when_crowded_because_prd_exempts_only_faults()
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
    public void should_not_qualify_for_density_when_plate_is_empty_or_small()
    {
        var strip = ComponentStripRules.Build(Snapshot(("a", "HEATER", null, "ON", "OK")));
        Assert.True(ComponentStripRules.Qualifies(strip, 48));                 // 밀도는 "부품 표가 있고 크게 보이는" 아이콘을 센다(규칙 그대로)
        Assert.False(ComponentStripRules.Qualifies(strip, 47));
        Assert.False(ComponentStripRules.Qualifies(ComponentStrip.Empty, 96));
    }

    // ── 렌더(오프스크린) ──

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_draw_plate_below_icon_only_when_large_and_wrong_in_light_and_dark(bool dark) => HousingTests.Sta(() =>
    {
        var (small, smallOverlay) = Render(dark, scale: 1.0, crowded: false);
        var (large, largeOverlay) = Render(dark, scale: 1.5, crowded: false);
        var (healthy, healthyOverlay) = Render(dark, scale: 1.5, crowded: false, healthy: true);
        var (selected, selectedOverlay) = Render(dark, scale: 1.5, crowded: false, healthy: true, selected: true);
        var (crowdedBitmap, crowdedOverlay) = Render(dark, scale: 1.5, crowded: true, healthy: true);

        Assert.False(smallOverlay.IsStripDrawn);                               // 40px — 이름표 없음
        Assert.True(largeOverlay.IsStripDrawn);                                // 60px + 저하 — 이름표
        Assert.Equal("적외선 LED 저하", largeOverlay.DrawnPlateText);
        Assert.False(healthyOverlay.IsStripDrawn);                             // 정상 — 표시 없음
        Assert.Equal("부품 5 · 이상 없음", selectedOverlay.DrawnPlateText);    // 선택 — 요약 문장
        Assert.False(crowdedOverlay.IsStripDrawn);                             // 밀집 + 정상 — 숨김

        // 모양 검증 — 아이콘 아래 띠에만 픽셀이 생긴다.
        var band = new Int32Rect(0, Pad + Size + 5, Canvas, 20);   // 배지 걸침(아래 4px) 바로 밑부터
        Assert.Equal(0, Ink(small, band));
        Assert.True(Ink(large, band) > 60, "이름표 픽셀 없음");
        Assert.Equal(0, Ink(healthy, band));
        Assert.True(Ink(selected, band) > 60, "선택 요약 문장 픽셀 없음");
        Assert.Equal(0, Ink(crowdedBitmap, band));
    });

    [Fact]
    public void should_keep_health_word_and_count_inside_width_cap_when_name_is_long_at_64px() => HousingTests.Sta(() =>
    {
        var (_, overlay) = Render(dark: false, scale: 1.6, crowded: false, longName: true);   // 40 × 1.6 = 64px

        Assert.Equal("네트워크 인터페… 고장 +1", overlay.DrawnPlateText);                       // 8 글자 줄임 + 건강 단어 + 나머지 수
        Assert.InRange(overlay.LastPlateWidth, 1, ComponentStatusOverlay.MaxPlateWidth);         // 폭 상한 안 — 넘치면 이름만 더 줄인다
    });

    // ── helpers ──

    private const int Canvas = 160, Pad = 20, Size = 40;

    private static (RenderTargetBitmap Bitmap, ComponentStatusOverlay Overlay) Render(bool dark, double scale, bool crowded, bool healthy = false, bool selected = false, bool longName = false)
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
                ("ir", "IR_LED", longName ? "네트워크 인터페이스 모듈" : "적외선 LED", null, "ON", longName ? "FAULT" : healthy ? "OK" : "DEGRADED", healthy ? null : "OVER_CURRENT", true),
                ("wiper", "WIPER", null, null, "OFF", longName ? "FAULT" : "OK", null, true),
                ("fan", "FAN", null, false, "OFF", "OK", null, true)),
        };
        if (crowded) marker.ComponentStripCrowded = true;   // 지도가 심볼에 내려 주는 값 → 컨트롤 → 부품 층
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        control.Width = Size; control.Height = Size;
        control.MarkerScreenScale = scale;
        if (selected) control.IsSelected = true;
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
