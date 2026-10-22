using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 3D 오버레이 다리(GIS → map-3d) 순수 함수 — WPF · GMap · 심볼 모델 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DOverlayTests).
                  ① 3D 전용 DTO 레코드 만들기(모델 값을 복사해 받는다 — 모델을 그대로 직렬화하지 않는다)
                  ② DTO → JSON ③ 직전에 보낸 JSON 과 비교해 바뀐 필드만 고르기(patch)
                  ④ overlay.snapshot · patch · add · remove 메시지 만들기.
                  ⑤ 규약 v2(2026-10-17): 아이콘 · 라벨 스타일 · FOV · 선 스타일 · 오버레이 이미지 DTO, overlay.icon · overlay.imageData(내용 SHA-1 키),
                    track.upsert · track.remove, PNG 긴 변 맞추기(4096 px).
   Note         : 상태 이름은 enum 이름 그대로(EnumCompositeEventStatus · EnumOperationState · EnumDoorState), 색은 "#RRGGBB".
                  visible = 모델 Visible && 마커 레이어 켬. minZoom = 모델 Zoom(실효줌) 그대로 — 3D 가 자기 줌과 비교한다.
                  모델에서 값을 읽어 이 함수에 넘기는 일은 Services/Map3D/Map3DOverlayBridge 가 한다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>점 · 선 상태(3D 전용). 없는 값은 null.</summary>
public sealed record Map3DOverlayState(string? Composite, string? Operation, string? Door);

/// <summary>3D 로 보내는 점 심볼 — Kind = "pids" | "symbol" | "geometric" | "military" | "infra".</summary>
public sealed record Map3DOverlayPoint(
    string Kind, int Id, string Title, double Lat, double Lon, double AltM, double Bearing,
    string? DeviceType, string? Model, Map3DOverlayState State, bool ShowLabel, bool Visible, double MinZoom)
{
    /// <summary>2D 마커 시각을 그린 PNG 아이콘(overlay.icon 키 · 화면 크기 DIP). 그리기 실패면 null(3D 가 원 표식).</summary>
    public Map3DOverlayIcon? Icon { get; init; }
    /// <summary>라벨 스타일. null 이면 label 은 show 만 보낸다.</summary>
    public Map3DOverlayLabelStyle? Label { get; init; }
    /// <summary>카메라 시야 부채꼴(카메라 장비만).</summary>
    public Map3DOverlayFov? Fov { get; init; }
    /// <summary>모양 표시(ShowShape) — 꺼지면 3D 는 받침 · 기둥 · 바닥 점을 빼고 이름표만(2026-10-22).</summary>
    public bool ShowShape { get; init; } = true;
    /// <summary>도형 투명도(0~1, 도형 심볼만) — 아이콘 PNG 는 바깥 투명도 없이 굽고 3D 가 이 값을 건다. 없으면 null.</summary>
    public double? Opacity { get; init; }
}

/// <summary>점 아이콘 — Key = overlay.icon 키, WPx × HPx = 2D 화면 크기(DIP).</summary>
public sealed record Map3DOverlayIcon(string Key, double WPx, double HPx);

/// <summary>라벨 스타일 — Color "#RRGGBB", Bg "#AARRGGBB"(투명이면 "transparent", 형식이 틀리면 null = 3D 기본 칩).</summary>
public sealed record Map3DOverlayLabelStyle(string? Text, double Size, string Color, string? Bg, bool Bold)
{
    /// <summary>글꼴 이름(비면 null = 3D 기본) · 기울임 · 최대 폭(DIP, 넘치면 말줄임 — 2D LabelAdorner.EffectiveMaxWidth 값).</summary>
    public string? Font { get; init; }
    public bool Italic { get; init; }
    public double? MaxWidth { get; init; }
    /// <summary>점 심볼 — 2D 이름표를 옮긴 만큼(DIP, 2D 와 같은 기준: 모양 아래 기본 자리 + 이 값).</summary>
    public (double X, double Y)? Offset { get; init; }
    /// <summary>선 · 구역 — 2D 이름표 자리 비율(U · V, 점 상자 반폭 · 반높이 기준)과 그 자리의 지리 좌표.</summary>
    public (double U, double V)? Anchor { get; init; }
    public (double Lon, double Lat)? At { get; init; }
}

/// <summary>카메라 시야 부채꼴 — BearingDeg 는 북 0° 시계 방향(지도 방위와 무관한 월드각).</summary>
public sealed record Map3DOverlayFov(bool Show, double RangeM, double AngleDeg, double BearingDeg, string Color, double Opacity);

/// <summary>구역 선의 3D 철망 설정.</summary>
public sealed record Map3DOverlayFence(double HeightM, double PostSpacingM, bool Render3D);

/// <summary>3D 로 보내는 선 — Kind = "pidsGroup" | "line" | "polygon". 점은 (위도, 경도).</summary>
public sealed record Map3DOverlayLine(
    string Kind, int Id, string Title, IReadOnlyList<(double Lat, double Lon)> Points, bool Closed,
    string Stroke, double StrokeW, Map3DOverlayFence? Fence, string? Composite, bool Visible, double MinZoom)
{
    /// <summary>일반 선 · 다각형 스타일. null 이면 보내지 않는다(구역 선).</summary>
    public Map3DOverlayLineStyle? Style { get; init; }
    /// <summary>구역 선 위 탐지 센서 구간 — 선 길이 비율 [From, To](0~1). 모르면 null. 구역 선(pidsGroup)만 state.activeSection 으로 보낸다.</summary>
    public (double From, double To)? ActiveSection { get; init; }
    /// <summary>이름표(2026-10-22 — 점과 같은 label). null 이면 보내지 않는다(옛 호출). ShowLabel = 2D ShowTitle.</summary>
    public Map3DOverlayLabelStyle? Label { get; init; }
    public bool ShowLabel { get; init; }
}

/// <summary>일반 선 · 다각형 스타일 — Fill "#RRGGBB"(채움 없음이면 null), Pattern "Solid" | "Dash" | "Dot".</summary>
public sealed record Map3DOverlayLineStyle(string? Fill, double FillOpacity, string Pattern, bool Arrow, double Opacity);

/// <summary>3D 로 보내는 오버레이 이미지(보안 가림 포함). 경계는 경도(Left · Right) · 위도(Top · Bottom), ImageKey = overlay.imageData 키.</summary>
public sealed record Map3DOverlayImage(
    int Id, string Title, double Left, double Top, double Right, double Bottom, double Rotation, double Opacity,
    int ZOrder, bool Visible, double MinZoom, bool Mask, string ImageKey)
{
    /// <summary>이름표(2026-10-22, 오버레이 이미지 마커). null 이면 보내지 않는다(지도 CustomImages 는 이름표가 없다).</summary>
    public Map3DOverlayLabelStyle? Label { get; init; }
    public bool ShowLabel { get; init; }
}

public static class Map3DOverlay
{
    public const string KindPids = "pids";
    public const string KindSymbol = "symbol";
    public const string KindGeometric = "geometric";
    public const string KindMilitary = "military";
    public const string KindInfra = "infra";
    public const string KindPidsGroup = "pidsGroup";
    public const string KindLine = "line";
    public const string KindPolygon = "polygon";
    public const string KindImage = "image";
    public const string KindTrack = "track";

    /// <summary>상태 변경을 모아 보내는 간격(ms).</summary>
    public const int CoalesceMs = 50;

    /// <summary>오버레이 이미지 PNG 의 긴 변 상한(px).</summary>
    public const int MaxImageSide = 4096;

    /// <summary>디스크 캐시에 넣는 오버레이 이미지 인코딩의 긴 변 상한(px).</summary>
    public const int CachedImageSide = 2048;

    /// <summary>투명 픽셀이 없는 오버레이 이미지의 JPEG 품질.</summary>
    public const int JpegQuality = 85;

    /// <summary>라벨 배경 알파(2D LabelAdorner.ChipAlpha 와 같은 0xCD).</summary>
    private const string LabelChipAlpha = "CD";

    /// <summary>점 심볼 DTO. pids 가 아니면(일반 심볼) 장비 종류 · 모델 · composite · door 는 비운다.</summary>
    public static Map3DOverlayPoint Point(
        string kind, int id, string? title, double lat, double lon, double altM, double bearing,
        EnumDeviceType? deviceType, string? modelKey,
        EnumCompositeEventStatus? composite, EnumOperationState operation, EnumDoorState? door,
        bool showTitle, bool modelVisible, bool layerEnabled, double minZoom)
        => new(kind, id, title ?? string.Empty, lat, lon, Finite(altM), Finite(bearing),
               deviceType?.ToString(), modelKey,
               new Map3DOverlayState(composite?.ToString(), operation.ToString(), door?.ToString()),
               showTitle, modelVisible && layerEnabled, Finite(minZoom));

    /// <summary>선 DTO. 철망 설정이 비어 있으면 기본값(FenceDefaults)을 넣는다 — 구역 선(pidsGroup)만 철망이 있다.</summary>
    public static Map3DOverlayLine Line(
        string kind, int id, string? title, IEnumerable<(double Lat, double Lon)>? points, bool closed,
        string strokeHex, double strokeThickness,
        bool hasFence, double? fenceHeightM, double? postSpacingM, bool render3D,
        EnumCompositeEventStatus? composite, bool modelVisible, bool layerEnabled, double minZoom)
        => new(kind, id, title ?? string.Empty, points?.ToList() ?? new List<(double, double)>(), closed,
               strokeHex, Finite(strokeThickness),
               hasFence ? new Map3DOverlayFence(fenceHeightM ?? FenceDefaults.FenceHeightM, postSpacingM ?? FenceDefaults.PostSpacingM, render3D) : null,
               composite?.ToString(), modelVisible && layerEnabled, Finite(minZoom));

    /// <summary>라벨 스타일. 배경은 2D 와 같이 알파 0xCD 칩 — 투명(알파 0 인 "#AARRGGBB")이면 null.</summary>
    public static Map3DOverlayLabelStyle Label(string? text, double size, string colorHex, string? bgHex, bool bold)
        => new(text, Finite(size), colorHex, ChipBackground(bgHex), bold);

    /// <summary>카메라 시야 부채꼴. 투명도는 0~1 로 자른다.</summary>
    public static Map3DOverlayFov Fov(bool show, double rangeM, double angleDeg, double bearingDeg, string colorHex, double opacity)
        => new(show, Finite(rangeM), Finite(angleDeg), Finite(bearingDeg), colorHex, Math.Clamp(Finite(opacity), 0d, 1d));

    /// <summary>일반 선 · 다각형 스타일. 2D 는 선 투명도(LineOpacity)를 선 · 채움 모두에 건다. 채움이 투명이면 null.</summary>
    public static Map3DOverlayLineStyle LineStyle(string? fillHex, double lineOpacity, EnumLinePattern pattern, bool arrow)
    {
        double opacity = Math.Clamp(Finite(lineOpacity), 0d, 1d);
        string? fill = fillHex is { Length: 7 } ? fillHex : null;   // "#00000000"(투명) · 빈 값은 채움 없음
        return new(fill, opacity, PatternName(pattern), arrow, opacity);
    }

    /// <summary>2D 선 무늬 → 3D 무늬("Solid" | "Dash" | "Dot"). 점-선은 Dash.</summary>
    public static string PatternName(EnumLinePattern pattern) => pattern switch
    {
        EnumLinePattern.Dashed or EnumLinePattern.DashDot => "Dash",
        EnumLinePattern.Dotted => "Dot",
        _ => "Solid",
    };

    /// <summary>오버레이 이미지 DTO. 회전은 [0, 360), 투명도는 0~1 로 자른다. visible = 모델 표시 && 레이어 켬.</summary>
    public static Map3DOverlayImage Image(
        int id, string? title, double left, double top, double right, double bottom, double rotation, double opacity,
        int zOrder, bool modelVisible, bool layerEnabled, double minZoom, bool mask, string imageKey)
    {
        double r = Finite(rotation) % 360d;
        return new(id, title ?? string.Empty, Finite(left), Finite(top), Finite(right), Finite(bottom),
                   r < 0 ? r + 360d : r, Math.Clamp(Finite(opacity), 0d, 1d), zOrder, modelVisible && layerEnabled, Finite(minZoom), mask, imageKey);
    }

    // ── JSON ──

    public static JObject ToJson(Map3DOverlayPoint p)
    {
        var o = new JObject
        {
            ["kind"] = p.Kind,
            ["id"] = p.Id,
            ["title"] = p.Title,
            ["lat"] = p.Lat,
            ["lon"] = p.Lon,
            ["altM"] = p.AltM,
            ["bearing"] = p.Bearing,
            ["deviceType"] = Str(p.DeviceType),
            ["model"] = Str(p.Model),
            ["state"] = new JObject
            {
                ["composite"] = Str(p.State.Composite),
                ["operation"] = Str(p.State.Operation),
                ["door"] = Str(p.State.Door),
            },
            ["label"] = LabelJson(p.ShowLabel, p.Label),
            ["visible"] = p.Visible,
            ["minZoom"] = p.MinZoom,
            // Diff 는 사라진 키를 보내지 못한다 — 켜짐(true)도 늘 싣는다(옛 뷰어는 모르는 키를 무시)
            ["showShape"] = p.ShowShape,
        };
        if (p.Opacity is double op) o["opacity"] = op;
        if (p.Icon is { } i) o["icon"] = new JObject { ["key"] = i.Key, ["wPx"] = i.WPx, ["hPx"] = i.HPx };
        if (p.Fov is { } f)
            o["fov"] = new JObject
            {
                ["show"] = f.Show, ["rangeM"] = f.RangeM, ["angleDeg"] = f.AngleDeg, ["bearingDeg"] = f.BearingDeg,
                ["color"] = f.Color, ["opacity"] = f.Opacity,
            };
        return o;
    }

    public static JObject ToJson(Map3DOverlayLine l)
    {
        var o = new JObject
        {
            ["kind"] = l.Kind,
            ["id"] = l.Id,
            ["title"] = l.Title,
            ["pts"] = new JArray(l.Points.Select(pt => new JArray(pt.Lat, pt.Lon))),
            ["closed"] = l.Closed,
            ["stroke"] = l.Stroke,
            ["strokeW"] = l.StrokeW,
            ["fence"] = l.Fence is { } f
                ? new JObject { ["heightM"] = f.HeightM, ["postSpacingM"] = f.PostSpacingM, ["render3d"] = f.Render3D }
                : JValue.CreateNull(),
            ["state"] = new JObject { ["composite"] = Str(l.Composite) },
            ["visible"] = l.Visible,
            ["minZoom"] = l.MinZoom,
        };
        if (l.Kind == KindPidsGroup)   // 옛 뷰어는 모르는 키를 무시한다
            o["state"]!["activeSection"] = l.ActiveSection is { } a ? new JArray(a.From, a.To) : JValue.CreateNull();
        if (l.Style is { } s)
        {
            o["fill"] = Str(s.Fill);
            o["fillOpacity"] = s.FillOpacity;
            o["pattern"] = s.Pattern;
            o["arrow"] = s.Arrow;
            o["opacity"] = s.Opacity;
        }
        if (l.Label != null) o["label"] = LabelJson(l.ShowLabel, l.Label);   // 없으면 옛 뷰어가 이름을 늘 보였다(ShowTitle 무시)
        return o;
    }

    /// <summary>
    /// label 객체 — show · text · size · color · bg · bold · font · italic · maxWidth(스타일이 있을 때),
    /// 점은 offset{x,y}, 선 · 구역은 anchor{u,v} 와 그 자리 lon · lat(있을 때).
    /// </summary>
    private static JObject LabelJson(bool show, Map3DOverlayLabelStyle? l)
    {
        var o = new JObject { ["show"] = show };
        if (l == null) return o;
        o["text"] = Str(l.Text); o["size"] = l.Size; o["color"] = l.Color; o["bg"] = Str(l.Bg); o["bold"] = l.Bold;
        o["font"] = Str(l.Font); o["italic"] = l.Italic;
        o["maxWidth"] = l.MaxWidth is double w ? w : JValue.CreateNull();
        if (l.Offset is { } off) o["offset"] = new JObject { ["x"] = off.X, ["y"] = off.Y };
        if (l.Anchor is { } a) o["anchor"] = new JObject { ["u"] = a.U, ["v"] = a.V };
        if (l.At is { } at) { o["lon"] = at.Lon; o["lat"] = at.Lat; }
        return o;
    }

    /// <summary>
    /// 선 · 구역 이름표의 지리 자리 — 2D LabelAdorner 와 같은 규칙: 기준점(마커 Position) + (U · 반폭, 아래로 반높이 + V · 반높이),
    /// 반폭 · 반높이 = 점 상자의 절반(경도 · 위도). 2D 의 아이콘 간격 8 px(줌마다 거리가 바뀜)은 넣지 않는다. 점이 없으면 null.
    /// </summary>
    public static (double Lon, double Lat)? LineLabelAt(IReadOnlyList<(double Lat, double Lon)> points, double posLat, double posLon, double u, double v)
    {
        if (points.Count == 0) return null;
        double hw = (points.Max(p => p.Lon) - points.Min(p => p.Lon)) / 2d, hh = (points.Max(p => p.Lat) - points.Min(p => p.Lat)) / 2d;
        return (Finite(posLon + Finite(u) * hw), Finite(posLat - (1d + Finite(v)) * hh));   // 화면 아래 = 남쪽
    }

    public static JObject ToJson(Map3DOverlayImage i)
    {
        var o = ImageJson(i);
        if (i.Label != null) o["label"] = LabelJson(i.ShowLabel, i.Label);
        return o;
    }

    private static JObject ImageJson(Map3DOverlayImage i) => new()
    {
        ["kind"] = KindImage,
        ["id"] = i.Id,
        ["title"] = i.Title,
        ["bounds"] = new JObject { ["left"] = i.Left, ["top"] = i.Top, ["right"] = i.Right, ["bottom"] = i.Bottom },
        ["rotation"] = i.Rotation,
        ["opacity"] = i.Opacity,
        ["zOrder"] = i.ZOrder,
        ["visible"] = i.Visible,
        ["minZoom"] = i.MinZoom,
        ["mask"] = i.Mask,
        ["imageKey"] = i.ImageKey,
    };

    /// <summary>
    /// 아이콘을 다시 그려야 하는지 가르는 시각 서명 — 항목 JSON 에서 위치 · 방위 · 표시 규칙 · 아이콘(그리기 결과)을 뺀 것 + 마커 스타일 + 화면 크기.
    /// 같으면 직전 아이콘을 그대로 쓴다(상태 · 크기 · 색이 바뀌면 달라진다).
    /// </summary>
    public static string IconSignature(JObject item, string style, double wPx, double hPx)
    {
        var copy = (JObject)item.DeepClone();
        // opacity = 3D 가 아이콘에 거는 값(아이콘은 바깥 투명도 없이 굽는다) — 바뀌어도 다시 그리지 않는다
        foreach (var name in new[] { "lat", "lon", "altM", "bearing", "visible", "minZoom", "icon", "fov", "label", "title", "opacity" }) copy.Remove(name);
        return $"{copy.ToString(Formatting.None)}|{style}|{wPx:0.##}x{hPx:0.##}";
    }

    /// <summary>바이트 내용 키 — SHA-1 소문자 16진 앞 16자(같은 그림은 같은 키 → 한 번만 보낸다).</summary>
    public static string HashKey(byte[] bytes)
        => Convert.ToHexString(SHA1.HashData(bytes)).Substring(0, 16).ToLowerInvariant();

    /// <summary>PNG 바이트 → data URL.</summary>
    public static string PngDataUrl(byte[] png) => "data:image/png;base64," + Convert.ToBase64String(png);

    /// <summary>이미지 바이트 → data URL(mime = "image/png" | "image/jpeg").</summary>
    public static string DataUrl(byte[] bytes, string mime) => $"data:{mime};base64," + Convert.ToBase64String(bytes);

    /// <summary>
    /// 오버레이 이미지 디스크 캐시 키 — 원본 파일 경로(대소문자 무시) + 크기 + 수정 시각 + 인코딩 조건(긴 변 · 품질).
    /// 파일이 바뀌면(크기 · 시각) 키가 달라져 다시 인코딩한다. 파일 이름에 쓸 수 있는 16진 16자.
    /// </summary>
    public static string ImageCacheKey(string path, long length, DateTime lastWriteUtc)
        => HashKey(Encoding.UTF8.GetBytes($"{path.ToLowerInvariant()}|{length}|{lastWriteUtc.Ticks}|{CachedImageSide}|{JpegQuality}"));

    /// <summary>BGRA32 픽셀에 덜 불투명한(알파 &lt; 255) 픽셀이 하나라도 있는가.</summary>
    public static bool HasTransparentPixel(ReadOnlySpan<byte> bgra)
    {
        for (int i = 3; i < bgra.Length; i += 4)
            if (bgra[i] != 255) return true;
        return false;
    }

    /// <summary>캐시 인코딩 형식 — 투명이 있으면 PNG, 없으면 JPEG(같은 그림이 훨씬 작다).</summary>
    public static string ImageMime(bool hasTransparency) => hasTransparency ? "image/png" : "image/jpeg";

    /// <summary>긴 변이 max 이하가 되게 비율을 지켜 줄인 크기(이미 작으면 그대로, 짧은 변은 최소 1).</summary>
    public static (int Width, int Height) FitLongSide(int width, int height, int max = MaxImageSide)
    {
        int longSide = Math.Max(width, height);
        if (longSide <= max || longSide <= 0) return (width, height);
        double s = (double)max / longSide;
        return width >= height
            ? (max, Math.Max(1, (int)Math.Round(height * s)))
            : (Math.Max(1, (int)Math.Round(width * s)), max);
    }

    /// <summary>3D 에 "배경 없음"을 알리는 값 — null 은 3D 가 기본 칩(어두운 배경)으로 바꿔 그렸다(2026-10-22).</summary>
    public const string TransparentBg = "transparent";

    /// <summary>"#RRGGBB" → 알파 0xCD 칩 배경 "#CDRRGGBB". "#AARRGGBB" 의 알파가 0(투명)이면 "transparent", 형식이 다르면 null.</summary>
    public static string? ChipBackground(string? hex)
    {
        if (hex is { Length: 7 } && hex[0] == '#') return $"#{LabelChipAlpha}{hex.Substring(1)}";
        if (hex is { Length: 9 } && hex[0] == '#')
            return hex.StartsWith("#00", StringComparison.Ordinal) ? TransparentBg : $"#{LabelChipAlpha}{hex.Substring(3)}";
        return null;
    }

    /// <summary>항목 키 — 종류마다 Id 공간이 다를 수 있어 종류 + Id 로 가른다.</summary>
    public static string Key(string kind, int id) => $"{kind}:{id}";

    /// <summary>
    /// 직전에 보낸 항목과 비교해 바뀐 필드만 담은 set(kind · id 제외). state 는 바뀐 하위 필드만 담는다.
    /// 바뀐 것이 없으면 null.
    /// </summary>
    public static JObject? Diff(JObject previous, JObject next)
    {
        var set = new JObject();
        foreach (var prop in next.Properties())
        {
            if (prop.Name is "kind" or "id") continue;
            var old = previous[prop.Name];
            if (prop.Name == "state" && old is JObject oldState && prop.Value is JObject newState)
            {
                var stateSet = new JObject();
                foreach (var s in newState.Properties())
                    if (!JToken.DeepEquals(oldState[s.Name], s.Value)) stateSet[s.Name] = s.Value.DeepClone();
                if (stateSet.Count > 0) set["state"] = stateSet;
                continue;
            }
            if (!JToken.DeepEquals(old, prop.Value)) set[prop.Name] = prop.Value.DeepClone();
        }
        return set.Count > 0 ? set : null;
    }

    // ── 메시지 ──

    public static string SnapshotJson(long rev, IEnumerable<JObject> points, IEnumerable<JObject> lines)
        => SnapshotJson(rev, points, lines, Array.Empty<JObject>());

    public static string SnapshotJson(long rev, IEnumerable<JObject> points, IEnumerable<JObject> lines, IEnumerable<JObject> images)
        => new JObject
        {
            ["type"] = "overlay.snapshot",
            ["rev"] = rev,
            ["points"] = new JArray(points),
            ["lines"] = new JArray(lines),
            ["images"] = new JArray(images),
        }.ToString(Formatting.None);

    /// <summary>아이콘 PNG(같은 key 는 한 번만 — 스냅샷 · add · patch 보다 먼저).</summary>
    public static string IconJson(string key, string dataUrl)
        => new JObject { ["type"] = "overlay.icon", ["key"] = key, ["dataUrl"] = dataUrl }.ToString(Formatting.None);

    /// <summary>오버레이 이미지 PNG 픽셀(같은 key 는 한 번만 — 그 이미지를 쓰는 항목보다 먼저).</summary>
    public static string ImageDataJson(string key, string dataUrl)
        => new JObject { ["type"] = "overlay.imageData", ["key"] = key, ["dataUrl"] = dataUrl }.ToString(Formatting.None);

    /// <summary>추적 타겟 추가 · 갱신(rev 없음). trail = 오래된 점 → 최신 점(위도, 경도).</summary>
    public static string TrackUpsertJson(string id, double lat, double lon, double bearing, string colorHex, string? label,
                                         IEnumerable<(double Lat, double Lon)> trail)
        => new JObject
        {
            ["type"] = "track.upsert",
            ["id"] = id,
            ["lat"] = lat,
            ["lon"] = lon,
            ["bearing"] = Finite(bearing),
            ["color"] = colorHex,
            ["label"] = label ?? string.Empty,
            ["trail"] = new JArray(trail.Select(pt => new JArray(pt.Lat, pt.Lon))),
        }.ToString(Formatting.None);

    public static string TrackRemoveJson(string id)
        => new JObject { ["type"] = "track.remove", ["id"] = id }.ToString(Formatting.None);

    public static string PatchJson(long rev, string kind, int id, JObject set)
        => new JObject { ["type"] = "overlay.patch", ["rev"] = rev, ["kind"] = kind, ["id"] = id, ["set"] = set }.ToString(Formatting.None);

    public static string AddJson(long rev, JObject item)
        => new JObject { ["type"] = "overlay.add", ["rev"] = rev, ["item"] = item }.ToString(Formatting.None);

    public static string RemoveJson(long rev, string kind, int id)
        => new JObject { ["type"] = "overlay.remove", ["rev"] = rev, ["kind"] = kind, ["id"] = id }.ToString(Formatting.None);

    /// <summary>선 종류인가(스냅샷에서 lines 로 간다).</summary>
    public static bool IsLineKind(string? kind) => kind is KindPidsGroup or KindLine or KindPolygon;

    private static double Finite(double v) => double.IsFinite(v) ? v : 0d;

    /// <summary>null 은 JSON null 형으로(Newtonsoft 의 string 암묵 변환은 String 형 null 을 만든다).</summary>
    private static JToken Str(string? v) => v == null ? JValue.CreateNull() : new JValue(v);
}
