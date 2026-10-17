using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 지도 3D 바꿔 끼우기(map-3d POC) 설정 — AppSettings.Map3D.
                  3D 게이트 켜기(기본 켜짐) + map-3d 서버 주소(기본 http://127.0.0.1:8765).
                  전환 줌 · 영역 표시 줌 · 영역 최대 크기 · 안내 시간(2026-10-17, 메인 앱 설정 「3D 지도」 절에서 고친다).
                  로드: MapSettingsHelper.LoadMap3D(키 부재 = 기본값). MapTiltModel 과 같은 방식(기본값 + Normalize).
   Created On   : 2026-10-16 · Sensorway Co., Ltd.
 ****************************************************************************/
public class Map3DModel
{
    /// <summary>map-3d 서버 기본 주소(`python -m map3d serve` 기본값).</summary>
    public const string DefaultServerUrl = "http://127.0.0.1:8765";

    /// <summary>3D 게이트 켜기 — 꺼지면 구운 영역이 있어도 2D 만 쓴다.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>map-3d 서버 주소(http/https 절대 주소, 끝 '/' 없음).</summary>
    public string ServerUrl { get; set; } = DefaultServerUrl;

    /// <summary>3D 화면을 WPF 안에서 그린다(WebView2CompositionControl, 기본 켜짐) — 상세 창 · 카메라 상자 · 조립 카드가 3D 위에 보인다.
    /// 끄면 네이티브 창(WebView2) — 더 빠르지만 같은 자리의 WPF 를 덮는다(공역). RDP · 저사양에서 느리면 끈다.</summary>
    public bool UseCompositionHost { get; set; } = true;

    /// <summary>오버레이 이미지 기본 만들기 방식(마지막 고른 값) — "tiles"(위성 타일만) | "photo"(기본, 오버레이를 사진으로 · 건물 유지) |
    /// "mask"(오버레이로 가리기, 보안) | "extract"(가리기 + 건물 찾기). 옛 값 "composite" 는 mask 로 읽는다(2026-10-18).</summary>
    public string OverlayMode { get; set; } = "photo";

    // ── 전환 · 영역 값(설정 화면 「3D 지도」, 기본값 = 2026-10-17 까지의 고정값) ──

    public const double MinZoom = 10d, MaxZoom = 20.5d;
    public const double MinAreaSideM = 200d, MaxAreaSideLimitM = 5000d;
    public const double MaxGuideSeconds = 3d;

    /// <summary>2D → 3D 로 바뀌는 실효줌.</summary>
    public double EnterZoom { get; set; } = 17.0;
    /// <summary>3D → 2D 로 돌아가는 줌(EnterZoom 보다 작아야 한다 — 그 사이가 히스테리시스).</summary>
    public double ExitZoom { get; set; } = 16.5;
    /// <summary>영역 안이면 3D 장면을 미리 불러오기 시작하는 실효줌(ExitZoom 이하).</summary>
    public double PreloadZoom { get; set; } = 15.0;
    /// <summary>2D 지도에 3D 영역 테두리 · 배지를 그리는 최소 실효줌.</summary>
    public double AreaShowZoom { get; set; } = 13.0;
    /// <summary>3D 로 만들 수 있는 영역 한 변 최대(m).</summary>
    public double MaxAreaSideM { get; set; } = 2000d;
    /// <summary>2D → 3D 전환 안내 최소 표시 시간(초).</summary>
    public double GuideMinSeconds { get; set; } = 0.6;

    /// <summary>
    /// 주소가 http/https 절대 주소가 아니면 기본값으로 되돌리고, 끝 '/' 를 뗀다. 굽기 방식은 옛 "composite" → mask, 넷 중 하나가 아니면 tiles.
    /// 줌은 10~20.5 · 영역 200~5000 m · 안내 0~3 s 로 자르고(수가 아니면 기본값), 이탈 줌 &lt; 진입 줌(아니면 진입 − 0.5) · 미리 준비 ≤ 이탈.
    /// 주소가 바뀌었으면 true(다른 값 고침은 반환값에 넣지 않는다).
    /// </summary>
    public bool Normalize()
    {
        var def = new Map3DModel();
        EnterZoom = Clamp(EnterZoom, MinZoom, MaxZoom, def.EnterZoom);
        ExitZoom = Clamp(ExitZoom, MinZoom, MaxZoom, def.ExitZoom);
        PreloadZoom = Clamp(PreloadZoom, MinZoom, MaxZoom, def.PreloadZoom);
        AreaShowZoom = Clamp(AreaShowZoom, MinZoom, MaxZoom, def.AreaShowZoom);
        MaxAreaSideM = Clamp(MaxAreaSideM, MinAreaSideM, MaxAreaSideLimitM, def.MaxAreaSideM);
        GuideMinSeconds = Clamp(GuideMinSeconds, 0d, MaxGuideSeconds, def.GuideMinSeconds);
        if (ExitZoom >= EnterZoom) ExitZoom = EnterZoom - 0.5;
        if (ExitZoom < MinZoom) { ExitZoom = MinZoom; EnterZoom = MinZoom + 0.5; }
        if (PreloadZoom > ExitZoom) PreloadZoom = ExitZoom;

        string mode = OverlayMode?.Trim().ToLowerInvariant() ?? "";
        OverlayMode = mode == "composite" ? "mask" : mode is "photo" or "mask" or "extract" ? mode : "tiles";
        string before = ServerUrl;
        if (!Uri.TryCreate(ServerUrl?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            ServerUrl = DefaultServerUrl;
        else
            ServerUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return !string.Equals(before, ServerUrl, StringComparison.Ordinal);
    }

    private static double Clamp(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "Map3D[enabled={0}, server={1}, composition={2}, overlay={3}, enter={4}, exit={5}, preload={6}, area={7}, maxSide={8}, guide={9}]",
            IsEnabled, ServerUrl, UseCompositionHost, OverlayMode, EnterZoom, ExitZoom, PreloadZoom, AreaShowZoom, MaxAreaSideM, GuideMinSeconds);
}
