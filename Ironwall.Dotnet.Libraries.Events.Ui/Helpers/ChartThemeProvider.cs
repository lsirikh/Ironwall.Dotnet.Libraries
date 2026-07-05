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
            ? new SKColor(0xE6, 0xED, 0xF3)   // tactical Dark textPrimary
            : new SKColor(0x13, 0x20, 0x2C);  // tactical Light textPrimary

    /// <summary>
    /// 범례(legend) 라벨 전용 회색 계열 텍스트 색 — 축/툴팁의 고대비 본문색(<see cref="TextColor"/>)과 분리.
    /// 시리즈 색 점 옆 라벨을 부드럽게(사용자 요청: 회색 계열). Light=슬레이트 #64748B, Dark=밝은 슬레이트 #94A3B8(양 테마 가독).
    /// </summary>
    public static SKColor LegendTextColor(BaseTheme theme)
        => theme == BaseTheme.Dark
            ? new SKColor(0x94, 0xA3, 0xB8)   // slate-400 (dark bg 가독)
            : new SKColor(0x64, 0x74, 0x8B);  // slate-500 (light bg 가독)

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
