using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 3D 화면 위 GIS 창 자리 순수 함수 — WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DScreenTests).
                  ① 지켜볼 목록(overlay.watch) 만들기 · 화면 좌표(overlay.screen) 해석
                  ② 3D 화면 좌표(CSS px) → 지도 칸(outer, 팝업 캔버스) DIP 좌표
                  ③ 카메라 팝업 오프셋 — 2D 규칙(팝업 좌상단 = 위경도 앵커)을 3D 화면으로 옮기기 · 드래그한 3D 오프셋을 2D 로 되돌리기
   Note         : ★ 3D 화면 좌표는 map-3d 가 투영한 CSS px(= pick 의 screen 과 같은 기준)이다. WebView2 의 CSS px = DIP / ZoomFactor 라
                    지도 칸 DIP = 호스트 원점(지도 칸 기준) + CSS px × ZoomFactor. 모니터 DPI 는 WPF DIP · CSS px 양쪽이 이미 흡수한다.
                  ★ 2D 에서 팝업 좌상단은 위경도 앵커(AnchorGeo)에 붙어 줌 · 회전을 따라간다. 3D 에서는 앵커 지점이 기울어진 땅 위라
                    화면 밖(지평선 너머)으로 갈 수 있어, 마커가 3D 에 보이는 자리 + 2D 화면 오프셋(앵커 − 마커)을 쓴다.
                    오프셋은 2D 와 3D 의 줌 차(2^Δz)만큼 키우고 방위 차만큼 돌린다(지도는 방위만큼 반시계로 돈다 — Map3DMath 머리말).
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>3D 가 지켜볼 항목 — 마커(kind · id)가 3D 에 있으면 그 머리, 없으면 위경도(+ 높이) 지점.</summary>
public sealed record Map3DWatchItem(string Key, string? Kind, int? Id, double Lat, double Lon, double AltM);

/// <summary>3D 가 보낸 화면 좌표(CSS px). 카메라 뒤면 X · Y = null. Visible = 화면 안인가.</summary>
public readonly record struct Map3DScreenPoint(string Key, double? X, double? Y, bool Visible);

/// <summary>overlay.screen 이 함께 실어 온 그 프레임의 3D 메르카토르 줌 · 방위(view) — 마커 화면 자리와 같은 프레임 값.</summary>
public readonly record struct Map3DScreenView(double Zoom, double Bearing);

public static class Map3DScreen
{
    /// <summary>카메라 팝업의 지켜볼 key(카메라 id 당 하나).</summary>
    public static string CameraPopupKey(int cameraId) => $"camera-popup:{cameraId}";

    /// <summary>GIS → 3D 지켜볼 목록(통째로 바꾸기). 빈 목록이면 3D 는 보내기를 멈춘다.</summary>
    public static string WatchJson(IEnumerable<Map3DWatchItem> items)
        => new JObject
        {
            ["type"] = "overlay.watch",
            ["items"] = new JArray(items.Select(i =>
            {
                var o = new JObject { ["key"] = i.Key, ["lat"] = i.Lat, ["lon"] = i.Lon, ["altM"] = i.AltM };
                if (i.Kind != null && i.Id != null) { o["kind"] = i.Kind; o["id"] = i.Id.Value; }
                return o;
            })),
        }.ToString(Formatting.None);

    /// <summary>overlay.screen 의 items 해석 — key 가 없는 항목은 버린다. x · y 는 둘 다 수일 때만(아니면 null).</summary>
    public static IReadOnlyList<Map3DScreenPoint> ParseScreens(JObject o)
    {
        var list = new List<Map3DScreenPoint>();
        if (o["items"] is not JArray items) return list;
        foreach (var t in items.OfType<JObject>())
        {
            string? key = t["key"]?.Type == JTokenType.String ? t.Value<string>("key") : null;
            if (string.IsNullOrEmpty(key)) continue;
            double? x = Finite(t["x"]), y = Finite(t["y"]);
            if (x == null || y == null) { x = null; y = null; }
            list.Add(new Map3DScreenPoint(key!, x, y, t["visible"]?.Type == JTokenType.Boolean && t.Value<bool>("visible")));
        }
        return list;
    }

    /// <summary>overlay.screen 의 view(그 프레임 줌 · 방위) 해석 — 없거나(옛 3D) 수가 아니면 null.</summary>
    public static Map3DScreenView? ParseScreenView(JObject o)
        => o["view"] is JObject v && Finite(v["zoom"]) is double z && Finite(v["bearing"]) is double b ? new Map3DScreenView(z, b) : null;

    /// <summary>
    /// 상자 오프셋을 키우고 돌릴 3D 줌 · 방위 — 마커 화면 자리와 같은 프레임 값(overlay.screen 의 view)이 먼저,
    /// 없으면(옛 3D) 마지막 camera 메시지, 그것도 없으면(펼친 직후) 2D 와 같다고 본다.
    /// ★ camera 메시지는 150 ms 에 한 번이라 그 방위로 돌리면 관성 회전 중 상자가 마커 둘레에서 계단처럼 떨렸다(2026-10-19).
    /// </summary>
    public static (double Zoom, double Bearing) OffsetView3D(Map3DScreenView? frame, Map3DCamera? lastCamera, double zoom2D, double bearing2D)
        => frame is { } f ? (f.Zoom, f.Bearing) : lastCamera is { } c ? (c.Zoom, c.Bearing) : (zoom2D, bearing2D);

    /// <summary>
    /// 2D 지도 통지(3D 카메라를 2D 에 맞추며 생기는 팬 · 줌 · 회전)로 상자를 다시 놓을지 — 같은 프레임 값(view)을 받는 동안은
    /// overlay.screen 한 출처만 놓는다(한 프레임에 한 번 · 2D 정수 반올림이 끼어들지 않게). 옛 3D(view 없음)면 예전처럼 놓는다.
    /// </summary>
    public static bool PlaceOn2DChange(Map3DScreenView? frame) => frame == null;

    /// <summary>3D 화면 CSS px → 지도 칸(outer) DIP. originX · originY = 3D 호스트 왼쪽 위의 지도 칸 좌표. ZoomFactor 가 수가 아니거나 0 이하면 1.</summary>
    public static (double X, double Y) ToOuter(double cssX, double cssY, double zoomFactor, double originX, double originY)
    {
        double k = double.IsFinite(zoomFactor) && zoomFactor > 0 ? zoomFactor : 1d;
        return (originX + cssX * k, originY + cssY * k);
    }

    /// <summary>
    /// 2D 화면 오프셋(팝업 좌상단 − 마커, 2D 줌 · 방위에서) → 3D 화면 오프셋. 줌은 메르카토르 줌(3D 와 같은 축척),
    /// 방위는 화면 위쪽이 가리키는 방위(북 0, 시계). 수가 아닌 값은 차이 0 으로 본다.
    /// </summary>
    public static (double X, double Y) Offset2DTo3D(double dx, double dy, double zoom2D, double zoom3D, double bearing2D, double bearing3D)
        => Transform(dx, dy, Diff(zoom3D, zoom2D), Diff(bearing3D, bearing2D));

    /// <summary><see cref="Offset2DTo3D"/> 의 역 — 3D 에서 끌어다 놓은 오프셋을 2D 화면 오프셋으로(앵커 저장용).</summary>
    public static (double X, double Y) Offset3DTo2D(double dx, double dy, double zoom2D, double zoom3D, double bearing2D, double bearing3D)
        => Transform(dx, dy, -Diff(zoom3D, zoom2D), -Diff(bearing3D, bearing2D));

    /// <summary>3D 에서 카메라 상자 왼쪽 위 = 마커 3D 화면 자리 + 2D 오프셋(앵커 − 마커, 2D 화면)을 3D 줌 · 방위로 옮긴 것. 좌표는 모두 지도 칸(outer).</summary>
    public static (double X, double Y) PopupTopLeft3D((double X, double Y) marker3D, (double X, double Y) marker2D, (double X, double Y) anchor2D,
                                                      double zoom2D, double zoom3D, double bearing2D, double bearing3D)
    {
        var (ox, oy) = Offset2DTo3D(anchor2D.X - marker2D.X, anchor2D.Y - marker2D.Y, zoom2D, zoom3D, bearing2D, bearing3D);
        return (marker3D.X + ox, marker3D.Y + oy);
    }

    /// <summary>
    /// 2D 화면 오프셋(앵커 − 마커, 2D 줌 · 방위에서) → 줌 0 · 방위 0 기준 오프셋. <see cref="PopupTopLeft3DFromBase"/> 와 이으면 <see cref="PopupTopLeft3D"/> 와 같다.
    /// ★ 3D 동안 상자마다 한 번 셈해 둔다 — 2D 지도는 camera 메시지(150 ms)마다 따라 움직이고 2D 화면 좌표는 정수라, 매번 2D 에서 다시 재면
    ///   오프셋이 그때마다 ±1 px(× 3D 줌 배율) 튀어 상자가 잘게 떨렸다(2026-10-19 재현 하네스 Map3DPopupJitterTests).
    /// </summary>
    public static (double X, double Y) BaseOffset((double X, double Y) marker2D, (double X, double Y) anchor2D, double zoom2D, double bearing2D)
        => Offset2DTo3D(anchor2D.X - marker2D.X, anchor2D.Y - marker2D.Y, zoom2D, 0d, bearing2D, 0d);

    /// <summary>카메라 상자 왼쪽 위 = 마커 3D 화면 자리 + 줌 0 · 방위 0 기준 오프셋(<see cref="BaseOffset"/>)을 3D 줌 · 방위로 옮긴 것.</summary>
    public static (double X, double Y) PopupTopLeft3DFromBase((double X, double Y) marker3D, (double X, double Y) baseOffset, double zoom3D, double bearing3D)
    {
        var (ox, oy) = Offset2DTo3D(baseOffset.X, baseOffset.Y, 0d, zoom3D, 0d, bearing3D);
        return (marker3D.X + ox, marker3D.Y + oy);
    }

    /// <summary><see cref="PopupTopLeft3D"/> 의 역 — 3D 에서 끌어다 놓은 상자 왼쪽 위 → 2D 화면의 앵커 자리(위경도로 바꿔 저장한다).</summary>
    public static (double X, double Y) DraggedAnchor2D((double X, double Y) topLeft3D, (double X, double Y) marker3D, (double X, double Y) marker2D,
                                                       double zoom2D, double zoom3D, double bearing2D, double bearing3D)
    {
        var (dx, dy) = Offset3DTo2D(topLeft3D.X - marker3D.X, topLeft3D.Y - marker3D.Y, zoom2D, zoom3D, bearing2D, bearing3D);
        return (marker2D.X + dx, marker2D.Y + dy);
    }

    /// <summary>크기 2^dz 배 · 방위가 db 만큼 늘었을 때 화면 벡터 회전(y 아래 화면에서 반시계 db — 회전각 −db).</summary>
    private static (double X, double Y) Transform(double dx, double dy, double dz, double db)
    {
        double s = Math.Pow(2d, dz), r = -db * Math.PI / 180d, c = Math.Cos(r), n = Math.Sin(r);
        return ((dx * c - dy * n) * s, (dx * n + dy * c) * s);
    }

    private static double Diff(double a, double b) => double.IsFinite(a) && double.IsFinite(b) ? a - b : 0d;

    private static double? Finite(JToken? t)
        => t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) && double.IsFinite(t.Value<double>())
            ? t.Value<double>() : null;
}
