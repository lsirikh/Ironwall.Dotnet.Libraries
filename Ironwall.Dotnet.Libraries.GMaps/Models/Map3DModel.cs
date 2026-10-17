using System;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 지도 3D 바꿔 끼우기(map-3d POC) 설정 — AppSettings.Map3D.
                  3D 게이트 켜기(기본 켜짐) + map-3d 서버 주소(기본 http://127.0.0.1:8765).
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

    /// <summary>오버레이 이미지 기본 굽기 방식(마지막 고른 값) — "tiles"(기본, 위성 타일만) | "composite"(그림 합성) | "extract"(합성 + 건물 추출).</summary>
    public string OverlayMode { get; set; } = "tiles";

    /// <summary>주소가 http/https 절대 주소가 아니면 기본값으로 되돌리고, 끝 '/' 를 뗀다. 굽기 방식이 셋 중 하나가 아니면 tiles. 주소가 바뀌었으면 true.</summary>
    public bool Normalize()
    {
        string mode = OverlayMode?.Trim().ToLowerInvariant() ?? "";
        OverlayMode = mode is "composite" or "extract" ? mode : "tiles";
        string before = ServerUrl;
        if (!Uri.TryCreate(ServerUrl?.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            ServerUrl = DefaultServerUrl;
        else
            ServerUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return !string.Equals(before, ServerUrl, StringComparison.Ordinal);
    }

    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture, "Map3D[enabled={0}, server={1}, composition={2}, overlay={3}]", IsEnabled, ServerUrl, UseCompositionHost, OverlayMode);
}
