using MaterialDesignThemes.Wpf;
using SkiaSharp;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/// <summary>
/// IMPL-21 (FR-13) — SkiaSharp 차트 theme-aware 색/타입페이스 단일 공급자.
/// LiveChartsCore 페인트는 WPF DynamicResource 토큰에 도달하지 못하므로(리소스 미도달),
/// 차트 텍스트/타입페이스를 여기서 <see cref="BaseTheme"/>에 따라 직접 공급하고
/// 테마 전환 시(IThemeService.ThemeChanged) 차트를 rebuild/recolor 한다.
/// <para>V-07: 하드코딩 <c>SKColor</c> white·<c>SKTypeface.FromFamilyName</c>는 이 클래스로 일원화.</para>
/// </summary>
public static class ChartThemeProvider
{
    /// <summary>
    /// 축/범례/툴팁 텍스트 색 — 배경과 대비(theme-aware).
    /// Light=어두운 글자(#1C1B1F, TextPrimary Light), Dark=밝은 글자(#EDF1F6, TextPrimary Dark).
    /// </summary>
    public static SKColor TextColor(BaseTheme theme)
        => theme == BaseTheme.Dark
            ? new SKColor(0xED, 0xF1, 0xF6)
            : new SKColor(0x1C, 0x1B, 0x1F);

    /// <summary>
    /// 채도 높은 시리즈색 위에 얹는 고정 라벨/스트로크 색 — 양 테마 흰색(세그먼트 위라 테마 무관).
    /// </summary>
    public static SKColor OnSeriesFixed { get; } = new(255, 255, 255);

    /// <summary>
    /// 한글 차트 텍스트 타입페이스. 현행 동작 보존(Malgun Gothic) — 디자인 폰트(Noto) 통일은 EXT-07.
    /// FromFamilyName 호출을 이 한 곳으로 모아 V-07 grep 기준을 만족시킨다.
    /// </summary>
    public static SKTypeface KoreanTypeface()
        => SKTypeface.FromFamilyName("Malgun Gothic");

    /// <summary>theme-aware 텍스트 SolidColorPaint(한글 타입페이스 포함) 생성 헬퍼.</summary>
    public static LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint TextPaint(BaseTheme theme, float strokeWidth = 2)
        => new(TextColor(theme), strokeWidth) { SKTypeface = KoreanTypeface() };
}
