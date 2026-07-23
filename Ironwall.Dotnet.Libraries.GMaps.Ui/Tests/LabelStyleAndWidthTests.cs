using System;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 라벨 스타일(FR-05~07)·폭 WYSIWYG(FR-13) 회귀 테스트 — 2차 검증 시뮬 W그룹 이식 + NFR-01 기본값 삼위일치.
/// Overlay_Title_ZoomStyle 스테이지 3.
/// </summary>
public class LabelStyleAndWidthTests
{
    // ── NFR-01: 기본값 = 종전 하드코딩과 시각 동일 (삼위일치 P2-06) ──

    [Fact(DisplayName = "기본값 삼위일치: SymbolModel/ImageModel 스타일 기본 = 종전 하드코딩 ARGB·200px")]
    public void should_keep_legacy_visual_defaults_when_style_fields_untouched()
    {
        int legacyFg = unchecked((int)0xF0F0F4F8), legacyBg = unchecked((int)0xCD1C1E22);
        var s = new SymbolModel();
        Assert.Equal(legacyFg, s.TitleColor);
        Assert.Equal(legacyBg, s.TitleBackground);
        Assert.Equal(string.Empty, s.TitleFontFamily);
        Assert.False(s.TitleBold);
        Assert.False(s.TitleItalic);
        Assert.Equal(200.0, s.TitleMaxWidth, 9);

        var im = new ImageModel();
        Assert.Equal(legacyFg, im.TitleColor);
        Assert.Equal(legacyBg, im.TitleBackground);
        Assert.Equal(200.0, im.TitleMaxWidth, 9);
        Assert.Equal(11.0, im.TitleSize, 9);   // 종전 인메모리 기본 승계(FR-10)
        Assert.False(im.ShowTitle);
    }

    [Fact(DisplayName = "이미지 마커 위임: TitleSize/ShowTitle/U·V/스타일이 모델에 영속되고 INPC 발화 (P0-01 해소)")]
    public void should_delegate_title_fields_to_model_when_image_marker_set()
    {
        var model = new ImageModel { Id = 7, Title = "IMG" };
        var marker = new GMapImageMarker(null, model);
        string? lastProp = null;
        marker.PropertyChanged += (_, e) => lastProp = e.PropertyName;

        marker.TitleSize = 18.5;
        Assert.Equal(18.5, model.TitleSize, 9);
        Assert.Equal(nameof(marker.TitleSize), lastProp);

        marker.ShowTitle = true;
        Assert.True(model.ShowTitle);

        marker.LabelOffsetU = 0.75;
        Assert.Equal(0.75, model.LabelOffsetU, 9);
        Assert.Equal(nameof(marker.LabelOffsetU), lastProp);

        marker.LabelOffsetV = -1.25;
        Assert.Equal(-1.25, model.LabelOffsetV, 9);

        marker.TitleColor = unchecked((int)0xFF00AAFF);
        Assert.Equal(unchecked((int)0xFF00AAFF), model.TitleColor);
        Assert.Equal(nameof(marker.TitleColor), lastProp);

        marker.TitleMaxWidth = 320;
        Assert.Equal(320, model.TitleMaxWidth, 9);
    }

    [Fact(DisplayName = "ImageModel 복사 생성자: 타이틀 부속+스타일 전 필드 보존")]
    public void should_copy_all_title_fields_when_image_model_cloned()
    {
        var src = new ImageModel
        {
            TitleSize = 22, ShowTitle = true, LabelOffsetU = 0.4, LabelOffsetV = -0.6,
            TitleColor = 123456, TitleBackground = -99, TitleFontFamily = "Malgun Gothic",
            TitleBold = true, TitleItalic = true, TitleMaxWidth = 555
        };
        var dst = new ImageModel(src);
        Assert.Equal(22, dst.TitleSize, 9);
        Assert.True(dst.ShowTitle);
        Assert.Equal(0.4, dst.LabelOffsetU, 9);
        Assert.Equal(-0.6, dst.LabelOffsetV, 9);
        Assert.Equal(123456, dst.TitleColor);
        Assert.Equal(-99, dst.TitleBackground);
        Assert.Equal("Malgun Gothic", dst.TitleFontFamily);
        Assert.True(dst.TitleBold);
        Assert.True(dst.TitleItalic);
        Assert.Equal(555, dst.TitleMaxWidth, 9);
    }

    // ── FR-13: 폭 클램프/스트립/edge-pinned 수식 (2차 검증 W그룹 이식) ──

    [Theory(DisplayName = "폭 클램프: 0/미설정=200(레거시), 40~800 범위 강제")]
    [InlineData(0, 200)]      // 레거시/미설정 → 기본
    [InlineData(-5, 200)]
    [InlineData(25, 40)]      // 하한
    [InlineData(200, 200)]
    [InlineData(1200, 800)]   // 상한
    public void should_clamp_effective_max_width_when_out_of_range(double input, double expected)
    {
        Assert.Equal(expected, LabelAdorner.EffectiveMaxWidth(input), 9);
    }

    [Fact(DisplayName = "edge-pinned 리사이즈: 드래그 가장자리=커서 추종·반대편 고정 (W그룹 — 유일 성립 모델)")]
    public void should_pin_opposite_edge_when_width_handle_dragged()
    {
        // 수식 사양: newW = clamp(w0+dw), centerShift = ±actualDw/2 (우측 드래그=+).
        foreach (var (w0, d) in new[] { (80.0, 40.0), (200.0, 100.0), (200.0, -60.0), (400.0, 100.0) })
        {
            double newW = Math.Clamp(w0 + d, LabelAdorner.WIDTH_MIN, LabelAdorner.WIDTH_MAX);
            double actualDw = newW - w0;
            double centerShift = actualDw / 2d;   // 우측 가장자리 드래그
            // 검증 불변식: 우측 가장자리 = (중심+시프트) + newW/2 == 원래 우측 + actualDw (커서 추종)
            double rightBefore = w0 / 2d, rightAfter = centerShift + newW / 2d;
            Assert.Equal(rightBefore + actualDw, rightAfter, 9);
            // 반대편(좌측) 고정: (중심+시프트) - newW/2 == 원래 좌측
            double leftBefore = -w0 / 2d, leftAfter = centerShift - newW / 2d;
            Assert.Equal(leftBefore, leftAfter, 9);
        }
    }

    [Fact(DisplayName = "이미지 폭 조절 U 보상: ΔU = Δ폭/2 ÷ hw — 렌더 왕복 무손실")]
    public void should_compensate_image_offset_u_when_width_resized()
    {
        double hw = 320, u0 = 0.5, w0 = 200, d = 100;
        double centerShift = d / 2d;
        double u1 = (u0 * hw + centerShift) / hw;
        Assert.Equal(u0 + d / 2d / hw, u1, 9);
        // 같은 줌 재렌더 px = 원래 px + 시프트 (정확)
        Assert.Equal(u0 * hw + centerShift, u1 * hw, 9);
    }

    // ── FR-05/07 헬퍼 ──

    [Fact(DisplayName = "BrushFromArgb: 0=투명, 그 외 ARGB 채널 정확 분해")]
    public void should_build_brush_from_packed_argb()
    {
        Assert.Same(System.Windows.Media.Brushes.Transparent, LabelAdorner.BrushFromArgb(0, System.Windows.Media.Brushes.Red));
        var b = (System.Windows.Media.SolidColorBrush)LabelAdorner.BrushFromArgb(unchecked((int)0xCD1C1E22), System.Windows.Media.Brushes.Red);
        Assert.Equal(0xCD, b.Color.A);
        Assert.Equal(0x1C, b.Color.R);
        Assert.Equal(0x1E, b.Color.G);
        Assert.Equal(0x22, b.Color.B);
        Assert.True(b.IsFrozen);
    }

    [Fact(DisplayName = "ResolveTypeface: 빈값=Segoe UI, Bold/Italic 반영, 비정상 폰트명 폴백")]
    public void should_resolve_typeface_with_fallback_when_family_invalid()
    {
        var def = LabelAdorner.ResolveTypeface(null, false, false);
        Assert.Equal("Segoe UI", def.FontFamily.Source);
        var bi = LabelAdorner.ResolveTypeface("Malgun Gothic", true, true);
        Assert.Equal(System.Windows.FontWeights.Bold, bi.Weight);
        Assert.Equal(System.Windows.FontStyles.Italic, bi.Style);
        var weird = LabelAdorner.ResolveTypeface("definitely-not-a-font-@@", false, false);
        Assert.NotNull(weird);   // 예외 없이 생성(WPF 폴백) 또는 Segoe UI 폴백
    }

    [Fact(DisplayName = "ArgbHexConverter: int↔hex 왕복 무손실, 형식 오류=DoNothing (V-07)")]
    public void should_round_trip_argb_hex_when_converting()
    {
        var conv = new ArgbHexConverter();
        int argb = unchecked((int)0xF0F0F4F8);
        var hex = (string)conv.Convert(argb, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture)!;
        Assert.Equal("#F0F0F4F8", hex);
        var back = conv.ConvertBack(hex, typeof(int?), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(argb, back);
        // RGB 6자리 입력 → 불투명 처리
        Assert.Equal(unchecked((int)0xFF112233), conv.ConvertBack("#112233", typeof(int?), null, System.Globalization.CultureInfo.InvariantCulture));
        // 형식 오류 → DoNothing(기존값 유지)
        Assert.Equal(System.Windows.Data.Binding.DoNothing, conv.ConvertBack("zzz", typeof(int?), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
