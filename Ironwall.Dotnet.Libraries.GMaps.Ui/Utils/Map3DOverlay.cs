using System;
using System.Collections.Generic;
using System.Linq;
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
   Note         : 상태 이름은 enum 이름 그대로(EnumCompositeEventStatus · EnumOperationState · EnumDoorState), 색은 "#RRGGBB".
                  visible = 모델 Visible && 마커 레이어 켬. minZoom = 모델 Zoom(실효줌) 그대로 — 3D 가 자기 줌과 비교한다.
                  모델에서 값을 읽어 이 함수에 넘기는 일은 Services/Map3D/Map3DOverlayBridge 가 한다.
   Created On   : 2026-10-17 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>점 · 선 상태(3D 전용). 없는 값은 null.</summary>
public sealed record Map3DOverlayState(string? Composite, string? Operation, string? Door);

/// <summary>3D 로 보내는 점 심볼 — Kind = "pids" | "symbol".</summary>
public sealed record Map3DOverlayPoint(
    string Kind, int Id, string Title, double Lat, double Lon, double AltM, double Bearing,
    string? DeviceType, string? Model, Map3DOverlayState State, bool ShowLabel, bool Visible, double MinZoom);

/// <summary>구역 선의 3D 철망 설정.</summary>
public sealed record Map3DOverlayFence(double HeightM, double PostSpacingM, bool Render3D);

/// <summary>3D 로 보내는 선 — Kind = "pidsGroup" | "line". 점은 (위도, 경도).</summary>
public sealed record Map3DOverlayLine(
    string Kind, int Id, string Title, IReadOnlyList<(double Lat, double Lon)> Points, bool Closed,
    string Stroke, double StrokeW, Map3DOverlayFence? Fence, string? Composite, bool Visible, double MinZoom);

public static class Map3DOverlay
{
    public const string KindPids = "pids";
    public const string KindSymbol = "symbol";
    public const string KindPidsGroup = "pidsGroup";
    public const string KindLine = "line";

    /// <summary>상태 변경을 모아 보내는 간격(ms).</summary>
    public const int CoalesceMs = 50;

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

    // ── JSON ──

    public static JObject ToJson(Map3DOverlayPoint p) => new()
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
        ["label"] = new JObject { ["show"] = p.ShowLabel },
        ["visible"] = p.Visible,
        ["minZoom"] = p.MinZoom,
    };

    public static JObject ToJson(Map3DOverlayLine l) => new()
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
        => new JObject
        {
            ["type"] = "overlay.snapshot",
            ["rev"] = rev,
            ["points"] = new JArray(points),
            ["lines"] = new JArray(lines),
        }.ToString(Formatting.None);

    public static string PatchJson(long rev, string kind, int id, JObject set)
        => new JObject { ["type"] = "overlay.patch", ["rev"] = rev, ["kind"] = kind, ["id"] = id, ["set"] = set }.ToString(Formatting.None);

    public static string AddJson(long rev, JObject item)
        => new JObject { ["type"] = "overlay.add", ["rev"] = rev, ["item"] = item }.ToString(Formatting.None);

    public static string RemoveJson(long rev, string kind, int id)
        => new JObject { ["type"] = "overlay.remove", ["rev"] = rev, ["kind"] = kind, ["id"] = id }.ToString(Formatting.None);

    /// <summary>선 종류인가(스냅샷에서 lines 로 간다).</summary>
    public static bool IsLineKind(string? kind) => kind is KindPidsGroup or KindLine;

    private static double Finite(double v) => double.IsFinite(v) ? v : 0d;

    /// <summary>null 은 JSON null 형으로(Newtonsoft 의 string 암묵 변환은 String 형 null 을 만든다).</summary>
    private static JToken Str(string? v) => v == null ? JValue.CreateNull() : new JValue(v);
}
