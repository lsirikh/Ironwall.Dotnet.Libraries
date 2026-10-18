using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 3D 전환 안내 · 3D 영역 표시 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DGuideTests).
                  ① 전환 안내 패널 문구 · 최소 표시 시간 · 로그인 뒤 안내(R5)
                  ② 영역 크기 · 2 km 판정 · 크기 글(R6)
                  ③ 영역 테두리 표시 줌 · "3D 가능" 칩 · 영역 가운데 · 툴팁 글(R4)
                  ④ 창을 열 때 2D 로 돌아올지(R7 · 2026-10-18 WPF 안 3D 면 3D 유지)
                  ⑤ 펼침 시점(rendered · 1.5 s 시간 초과) · 진입 한 번의 전환 시간 로그 줄
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>3D 펼침 판정 — 기다림 · rendered 받음 · rendered 기다림 시간 초과.</summary>
public enum Map3DRevealDecision { Wait, Rendered, TimedOut }

public static class Map3DGuide
{
    // ── ① 전환 안내 ──

    /// <summary>2D → 3D 안내 제목.</summary>
    public const string EnterTitle = "3D 지도로 전환합니다";
    /// <summary>3D → 2D 안내 제목.</summary>
    public const string LeaveTitle = "2D 지도로 돌아갑니다";
    /// <summary>2D → 3D 안내 최소 표시 시간(이미 만든 3D 화면을 다시 펼칠 때도 이만큼은 보인다).</summary>
    public static readonly TimeSpan MinEnterShow = TimeSpan.FromSeconds(0.6);
    /// <summary>미리 불러와 이미 그려진(rendered) 3D 화면을 펼칠 때의 최소 안내 시간.</summary>
    public static readonly TimeSpan MinReuseShow = TimeSpan.FromSeconds(0.4);
    /// <summary>ready 뒤 rendered 를 기다리는 최대 시간 — 넘으면 그냥 펼친다.</summary>
    public static readonly TimeSpan RenderedTimeout = TimeSpan.FromSeconds(1.5);
    /// <summary>3D → 2D 안내 표시 시간(짧게).</summary>
    public static readonly TimeSpan LeaveShow = TimeSpan.FromSeconds(0.5);
    /// <summary>로그인 화면 뒤에서 3D 로 들어간 뒤, 로그인 직후 한 번 보이는 안내 시간.</summary>
    public static readonly TimeSpan AfterLoginShow = TimeSpan.FromSeconds(1.5);

    /// <summary>안내 패널 단계 글. stage = "reuse"(만들어 둔 화면) · "page" · "scene" · "model" · "ready" · "login", 모르면 "page".</summary>
    public static string StageText(string? stage) => stage switch
    {
        "reuse" => "만들어 둔 3D 화면을 펼치는 중…",
        "scene" => "3D 장면 정보를 읽는 중…",
        "model" => "3D 장면을 받는 중…",
        "ready" => "3D 화면 준비됨",
        "rendering" => "3D 화면을 그리는 중…",
        "login" => "지금 3D 지도를 보고 있습니다 (줌 16.5 밑 · ESC = 2D)",
        _ => "3D 화면을 여는 중…",
    };

    /// <summary>안내 패널 단계 글 — "login" 글의 이탈 줌을 설정(Map3DModel.ExitZoom)에서 받는다.</summary>
    public static string StageText(string? stage, double exitZoom)
        => stage == "login" ? $"지금 3D 지도를 보고 있습니다 (줌 {Zoom(exitZoom)} 밑 · ESC = 2D)" : StageText(stage);

    /// <summary>줌 글 — 17 · 16.5.</summary>
    public static string Zoom(double zoom) => zoom.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>글 뒤에 " n %"(percent 가 없으면 글 그대로).</summary>
    public static string WithPercent(string text, double? percent)
        => percent is double p && double.IsFinite(p) ? $"{text} {Math.Clamp(p, 0, 100).ToString("0", CultureInfo.InvariantCulture)} %" : text;

    /// <summary>최소 표시 시간까지 남은 시간(음수 없음).</summary>
    public static TimeSpan Remaining(TimeSpan elapsed, TimeSpan minimum)
        => elapsed >= minimum ? TimeSpan.Zero : minimum - elapsed;

    /// <summary>시간으로 채우는 막대(만들어 둔 화면 펼치기) — 0~100. 최소 시간이 0 이하면 100.</summary>
    public static double TimedPercent(TimeSpan elapsed, TimeSpan minimum)
        => minimum <= TimeSpan.Zero ? 100d : Math.Clamp(elapsed.TotalMilliseconds / minimum.TotalMilliseconds * 100d, 0d, 100d);

    /// <summary>
    /// 펼칠 때인가 — ready 전이면 기다림, rendered(오버레이 적용 + 한 프레임)를 받았으면 펼침,
    /// ready 뒤 <see cref="RenderedTimeout"/> 가 지나도 안 왔으면 시간 초과로 펼침(페이지가 rendered 를 모르는 판).
    /// </summary>
    public static Map3DRevealDecision DecideReveal(bool ready, bool rendered, TimeSpan sinceReady)
        => !ready ? Map3DRevealDecision.Wait
           : rendered ? Map3DRevealDecision.Rendered
           : sinceReady >= RenderedTimeout ? Map3DRevealDecision.TimedOut
           : Map3DRevealDecision.Wait;

    /// <summary>
    /// 진입 한 번의 전환 시간 로그 줄 — 진입(enterAt)부터 각 표시 시각까지 차례로 나눈 구간(초). 진입 전에 끝난 단계
    /// (미리 데우기 · 미리 불러오기)는 0, 표시 시각이 없으면 "-"(그 구간은 다음 구간에 들어간다).
    /// </summary>
    public static string TransitionTimingText(DateTime enterAt, DateTime? webViewAt, DateTime? pageAt, DateTime? snapshotAt,
                                              DateTime? renderedAt, DateTime revealAt, bool prewarmed, bool preloaded)
    {
        var prev = enterAt;
        string Segment(DateTime? mark)
        {
            if (mark is not DateTime m) return "-";
            var at = m < enterAt ? enterAt : m;
            var span = at > prev ? at - prev : TimeSpan.Zero;
            if (at > prev) prev = at;
            return Seconds(span);
        }
        string webview = Segment(webViewAt), page = Segment(pageAt), snapshot = Segment(snapshotAt), rendered = Segment(renderedAt), reveal = Segment(revealAt);
        string total = Seconds(revealAt > enterAt ? revealAt - enterAt : TimeSpan.Zero);
        return $"[Map3D] 전환 시간 total={total} webview={webview} page={page} snapshot={snapshot} rendered={rendered} reveal={reveal} "
               + $"(prewarmed={(prewarmed ? "true" : "false")}, preloaded={(preloaded ? "true" : "false")})";
    }

    private static string Seconds(TimeSpan span) => span.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";

    /// <summary>로그인 화면 뒤인가 — 토큰 저장소가 "로그인 안 됨"이라고 할 때만 true(저장소가 없으면 = 로그인 게이트 없음 = false).</summary>
    public static bool IsBehindLogin(bool? isAuthenticated) => isAuthenticated == false;

    /// <summary>로그인 직후 3D 안내를 한 번 보일까 — 로그인 화면 뒤에서 3D 를 펼쳤고 지금도 3D 가 보일 때.</summary>
    public static bool ShouldGuideAfterLogin(bool revealedBehindLogin, bool isShown) => revealedBehindLogin && isShown;

    // ── ② 영역 크기 ──

    /// <summary>3D 로 만들 수 있는 영역 한 변 최대(m) — map-3d 서버 제한과 같다.</summary>
    public const double MaxAreaSideM = 2000d;
    private const double EarthRadiusM = 6371008.8;

    /// <summary>bbox [minLon, minLat, maxLon, maxLat] → (가로 m, 세로 m). 가로는 가운데 위도 기준. 형식이 틀리면 (0, 0).</summary>
    public static (double WidthM, double HeightM) AreaSizeMeters(IReadOnlyList<double> bbox)
    {
        if (bbox.Count != 4) return (0d, 0d);
        double rad = Math.PI / 180d;
        double midLat = (bbox[1] + bbox[3]) / 2d * rad;
        double w = Math.Abs(bbox[2] - bbox[0]) * rad * EarthRadiusM * Math.Cos(midLat);
        double h = Math.Abs(bbox[3] - bbox[1]) * rad * EarthRadiusM;
        return (w, h);
    }

    /// <summary>한 변이라도 2 km 를 넘는가.</summary>
    public static bool IsAreaTooLarge(double widthM, double heightM) => IsAreaTooLarge(widthM, heightM, MaxAreaSideM);

    /// <summary>한 변이라도 설정의 최대 크기(Map3DModel.MaxAreaSideM)를 넘는가.</summary>
    public static bool IsAreaTooLarge(double widthM, double heightM, double maxSideM) => widthM > maxSideM || heightM > maxSideM;

    /// <summary>최대 크기 글 — 2000 → "2 km", 1500 → "1.5 km", 800 → "800 m".</summary>
    public static string LimitText(double maxSideM)
        => maxSideM >= 1000d
            ? (maxSideM / 1000d).ToString("0.##", CultureInfo.InvariantCulture) + " km"
            : maxSideM.ToString("0", CultureInfo.InvariantCulture) + " m";

    /// <summary>길이 글 — 1 km 이상 "1.25 km", 밑으로 "850 m".</summary>
    public static string FormatLength(double meters)
        => meters >= 1000d
            ? (meters / 1000d).ToString("0.00", CultureInfo.InvariantCulture) + " km"
            : meters.ToString("0", CultureInfo.InvariantCulture) + " m";

    /// <summary>"가로 × 세로" 글.</summary>
    public static string FormatAreaSize(double widthM, double heightM) => $"{FormatLength(widthM)} × {FormatLength(heightM)}";

    /// <summary>그리는 중 크기 표시 — (글, 경고). 2 km 를 넘으면 글 뒤에 " — 2 km 초과" 와 경고.</summary>
    public static (string Text, bool IsWarning) AreaDrawLabel(IReadOnlyList<double> bbox) => AreaDrawLabel(bbox, MaxAreaSideM);

    /// <summary>그리는 중 크기 표시 — 최대 크기를 설정에서 받는다.</summary>
    public static (string Text, bool IsWarning) AreaDrawLabel(IReadOnlyList<double> bbox, double maxSideM)
    {
        var (w, h) = AreaSizeMeters(bbox);
        bool warn = IsAreaTooLarge(w, h, maxSideM);
        return (FormatAreaSize(w, h) + (warn ? $" — {LimitText(maxSideM)} 초과" : ""), warn);
    }

    /// <summary>놓았을 때 너무 크면 배너에 띄우는 이유.</summary>
    public static string AreaTooLargeText(double widthM, double heightM) => AreaTooLargeText(widthM, heightM, MaxAreaSideM);

    /// <summary>놓았을 때 너무 크면 배너에 띄우는 이유 — 최대 크기를 설정에서 받는다.</summary>
    public static string AreaTooLargeText(double widthM, double heightM, double maxSideM)
        => $"영역이 너무 큽니다({FormatAreaSize(widthM, heightM)}) — 한 변 {LimitText(maxSideM)} 이하로 다시 그리세요 (ESC = 취소)";

    // ── ③ 영역 표시 ──

    /// <summary>2D 지도에 3D 영역 테두리 · 배지를 그리는 최소 실효줌.</summary>
    public const double AreaOutlineMinZoom = 13d;
    /// <summary>「이 영역 3D 로 보기」가 옮겨 가는 실효줌.</summary>
    public const double AreaViewZoom = 17.5d;
    /// <summary>줌 컨트롤 옆 칩 글.</summary>
    public const string ChipText = "3D 가능 — 줌 17 이상";

    /// <summary>영역 테두리 · 배지를 보일까(실효줌 ≥ 13). NaN 은 false.</summary>
    public static bool ShouldShowAreaOutlines(double effectiveZoom) => ShouldShowAreaOutlines(effectiveZoom, AreaOutlineMinZoom);

    /// <summary>영역 테두리 · 배지를 보일까 — 최소 줌을 설정(Map3DModel.AreaShowZoom)에서 받는다.</summary>
    public static bool ShouldShowAreaOutlines(double effectiveZoom, double minZoom) => !double.IsNaN(effectiveZoom) && effectiveZoom >= minZoom;

    /// <summary>"3D 가능 — 줌 17 이상" 칩 — 3D 가 꺼져 있고 화면 가운데가 3D 영역 안이며 실효줌 &lt; 17.</summary>
    public static bool ShouldShowChip(double effectiveZoom, bool centerInsideArea, bool is3DOff)
        => ShouldShowChip(effectiveZoom, centerInsideArea, is3DOff, Map3DMath.EnterZoom);

    /// <summary>"3D 가능" 칩 판정 — 진입 줌을 설정에서 받는다.</summary>
    public static bool ShouldShowChip(double effectiveZoom, bool centerInsideArea, bool is3DOff, double enterZoom)
        => is3DOff && centerInsideArea && double.IsFinite(effectiveZoom) && effectiveZoom < enterZoom;

    /// <summary>칩 글 — 진입 줌을 설정에서 받는다("3D 가능 — 줌 17 이상").</summary>
    public static string ChipTextFor(double enterZoom) => $"3D 가능 — 줌 {Zoom(enterZoom)} 이상";

    /// <summary>「이 영역 3D 로 보기」가 옮겨 갈 실효줌 — 17.5, 진입 줌이 그보다 높으면 진입 줌.</summary>
    public static double AreaViewZoomFor(double enterZoom) => Math.Max(AreaViewZoom, enterZoom);

    // ── 영역마다 3D 줌(2026-10-18) ──

    /// <summary>3D 만들기 패널 · 「3D 줌 바꾸기」에서 고를 수 있는 3D 로 바뀌는 줌 — 지도 디지털 줌과 같은 0.5 단위.</summary>
    public const double EnterZoomChoiceMin = 15d, EnterZoomChoiceMax = 19d;

    /// <summary>줌 선택지 15 · 15.5 · … · 19.</summary>
    public static IReadOnlyList<double> EnterZoomChoices { get; } = BuildEnterZoomChoices();

    private static double[] BuildEnterZoomChoices()
    {
        var list = new List<double>();
        for (double z = EnterZoomChoiceMin; z <= EnterZoomChoiceMax; z += 0.5) list.Add(z);
        return list.ToArray();
    }

    /// <summary>처음 골라 둘 줌 — 세팅 · 영역 값을 가장 가까운 선택지(0.5 단위, 15~19)로. 수가 아니면 17.</summary>
    public static double EnterZoomChoiceFor(double zoom)
        => double.IsFinite(zoom) ? Math.Clamp(Math.Round(zoom * 2d, MidpointRounding.AwayFromZero) / 2d, EnterZoomChoiceMin, EnterZoomChoiceMax)
                                 : Map3DMath.EnterZoom;

    /// <summary>배지 툴팁 · 목록 툴팁 — 이름 · 만든 날짜 · 건물 수 · 그 영역의 3D 줌.</summary>
    public static string AreaToolTip(string title, DateTime? created, int? buildings, double enterZoom)
        => $"{AreaToolTip(title, created, buildings)}\n3D 로 바뀌는 줌: {Zoom(enterZoom)} 이상";

    /// <summary>지도 메뉴 「3D 영역 목록」 한 줄 — "이름 · 줌 17 이상".</summary>
    public static string AreaMenuHeader(string title, double enterZoom) => $"{title} · 줌 {Zoom(enterZoom)} 이상";

    /// <summary>bbox 가운데(위도, 경도).</summary>
    public static (double Lat, double Lon) AreaCenter(IReadOnlyList<double> bbox)
        => bbox.Count == 4 ? ((bbox[1] + bbox[3]) / 2d, (bbox[0] + bbox[2]) / 2d) : (0d, 0d);

    /// <summary>영역 이름 — 서버 이름이 있으면 그것, 없으면 id.</summary>
    public static string AreaTitle(string id, string? name) => string.IsNullOrWhiteSpace(name) ? id : name!;

    /// <summary>배지 툴팁 · 목록 툴팁 — 이름 · 만든 날짜 · 건물 수(모르면 "-").</summary>
    public static string AreaToolTip(string title, DateTime? created, int? buildings)
        => $"{title}\n만든 날짜: {(created is DateTime c ? c.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "-")}\n건물: {(buildings is int b ? $"{b}개" : "-")}";

    // ── ④ 창 열기 ──

    /// <summary>
    /// 3D 위에서 창(카메라 상자 · 조립 카드 · 상세 창)을 열 때 2D 로 돌아올까. 상세 창(symbol-detail)은 WPF 안 3D(composition)면
    /// 3D 위에 그대로 보이므로 돌아오지 않는다. 카메라 상자 · 카드는 2D 심볼 화면 위치에 붙으므로 늘 돌아온다.
    /// </summary>
    public static bool ShouldLeave3DForPanel(string cause, string hostMode)
        => !(cause == "symbol-detail" && hostMode == "composition");

    /// <summary>
    /// 3D 위에서 창(카메라 상자 · 조립 카드 · 상세 창)을 열 때 2D 로 돌아올까(2026-10-18 — 3D 화면 자리 따라가기).
    /// WPF 안 3D(composition)면 창이 3D 위에 그대로 보이고, 카메라 상자 · 카드는 3D 가 알려 주는 마커 화면 자리(overlay.screen)를
    /// 따라가므로 돌아오지 않는다. 네이티브 창 3D(hwnd)는 WPF 창을 덮으므로(공역) 늘 돌아온다.
    /// <see cref="ShouldLeave3DForPanel"/> 은 화면 자리를 받기 전의 옛 규칙이다.
    /// </summary>
    public static bool ShouldLeave3DForWindow(string hostMode) => hostMode != "composition";
}
