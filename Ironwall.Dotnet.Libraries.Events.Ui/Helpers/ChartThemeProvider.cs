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
    /// 채도 높은 시리즈색 위에 얹는 고정 스트로크 색 — 양 테마 흰색(세그먼트 구분선 용도 유지).
    /// </summary>
    public static SKColor OnSeriesFixed { get; } = new(255, 255, 255);

    /// <summary>
    /// 시리즈 세그먼트 위 데이터라벨 잉크색(#0C1117) — 흰 라벨은 노랑/적/녹 세그먼트에서 대비 미달
    /// (시뮬 SIM-B153~157: CR 1.50/2.89/2.12). 잉크는 전 시리즈 최저 CR 4.06(보라) — SIM-B158~162.
    /// </summary>
    public static SKColor OnSeriesInk { get; } = new(0x0C, 0x11, 0x17);

    /// <summary>
    /// 툴팁 배경색(theme-aware) — 미지정 시 라이브러리 기본 밝은 박스가 다크 텍스트색과 충돌해
    /// CR=1.04(시뮬 SIM-R009, 사용자 실기 보고 재현). Dark=#243240(SurfaceHover 톤) / Light=#E9EEF4.
    /// </summary>
    public static SKColor TooltipBackgroundColor(BaseTheme theme)
        => theme == BaseTheme.Dark ? new SKColor(0x24, 0x32, 0x40) : new SKColor(0xE9, 0xEE, 0xF4);

    /// <summary>툴팁 배경 페인트 — 3개 차트 표면(막대/파이/라인) 공용, 테마 전환 시 재할당.</summary>
    public static LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint TooltipBackgroundPaint(BaseTheme theme)
        => new(TooltipBackgroundColor(theme));

    /// <summary>툴팁 텍스트 페인트 — 고대비 본문색 + 한글 타입페이스.</summary>
    public static LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint TooltipTextPaint(BaseTheme theme)
        => new() { Color = TextColor(theme), SKTypeface = KoreanTypeface() };

    private static SKTypeface? _koreanTypeface;

    /// <summary>
    /// 한글 차트 텍스트 타입페이스 — 디자인 폰트(Noto) 우선 폴백 체인(시뮬 SIM-F001~004):
    /// Noto Sans KR → Noto Sans CJK KR → Malgun Gothic. 스키아는 WPF 임베디드 폰트(NotoSansCJKkR)를
    /// 못 보므로 설치 폰트를 매칭하고, 부재 시 현행(Malgun)을 보존한다. FromFamilyName/매칭은 이 한 곳뿐(V-07).
    /// </summary>
    public static SKTypeface KoreanTypeface()
    {
        if (_koreanTypeface != null) return _koreanTypeface;
        var fm = SKFontManager.Default;
        _koreanTypeface = fm.MatchFamily("Noto Sans KR")
                       ?? fm.MatchFamily("Noto Sans CJK KR")
                       ?? SKTypeface.FromFamilyName("Malgun Gothic");
        return _koreanTypeface;
    }

    /// <summary>theme-aware 텍스트 SolidColorPaint(한글 타입페이스 포함) 생성 헬퍼.</summary>
    public static LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint TextPaint(BaseTheme theme, float strokeWidth = 2)
        => new(TextColor(theme), strokeWidth) { SKTypeface = KoreanTypeface() };
}
