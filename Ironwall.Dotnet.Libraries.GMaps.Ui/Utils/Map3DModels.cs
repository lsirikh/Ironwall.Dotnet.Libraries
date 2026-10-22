using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 넣은 모델(외부 3D 모델 주입 — Blender · Fusion 360 · SketchUp 등에서 만든 GLB · OBJ · FBX, 엔진 8cacc6e models.py) 순수 함수 —
                  WPF · GMap 무의존(헤드리스 시험: tests/GMaps.Ui.Tests/Map3DModelsTests).
                  ① 엔진 항목 해석(GET …/models · POST · PUT 응답) ② 자리 고치기 본문(PUT …/models/{uid}) ③ GIS → 3D 메시지(models.reload · model.select)
                  ④ 파일 고르기 거르개 · 쉬운 말.
   Note         : uid 는 엔진이 만든다(12자리 16진). alt_m = null 이면 그 자리 지형에 붙는다. 방향 = 북 0° · 시계, 크기 = 배(0.01~100).
                  3D 손잡이(edit.start target kind "model")가 옮기기 · 돌리기 · 크기를 고치고 edit.commit 에 scale 을 싣는다(Map3DEdit).
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>넣은 모델 하나 — 자리(경위도 · 고도 null = 지형) · 방향 · 크기 · 이름, 엔진이 잰 삼각형 수 · 원래 형식(glb · obj · fbx).</summary>
public sealed record Map3DModelItem(string Uid, string? Name, double Lon, double Lat, double? AltM, double HeadingDeg, double Scale)
{
    public int? Tris { get; init; }
    public string? Source { get; init; }

    /// <summary>판 목록 한 줄 — "이름 · 1.5배 · 90°".</summary>
    public string Text => $"{(string.IsNullOrWhiteSpace(Name) ? "모델" : Name)} · {Map3DModels.Num(Scale)}배 · {Map3DModels.Num(HeadingDeg)}°";
}

/// <summary>넣은 모델의 되돌리기 상태 — 자리 · 이름에 더해, 지운 모델을 되살릴 때 다시 올릴 파일(Data, 없으면 null).</summary>
public sealed record Map3DModelState(Map3DModelItem Item, string FileName, byte[]? Data);

public static class Map3DModels
{
    /// <summary>엔진이 받는 확장자 · 크기 상한(엔진 models.MAX_BYTES 와 같게 — 넘으면 올리기 전에 거른다).</summary>
    public static readonly IReadOnlyList<string> Extensions = new[] { ".glb", ".obj", ".fbx" };
    public const long MaxBytes = 50L * 1024 * 1024;
    public const string FileFilter = "3D 모델 (*.glb;*.obj;*.fbx)|*.glb;*.obj;*.fbx|모든 파일 (*.*)|*.*";

    /// <summary>되돌리기 기록 설명 앞머리 — 꾸미기 판의 Ctrl+Z 가 같이 되돌리게 꾸미기(Map3DDecoration.UndoPrefix)로 시작한다.</summary>
    public const string UndoPrefix = Map3DDecoration.UndoPrefix + " · 모델";

    public const string WrongFileText = "GLB · OBJ · FBX 파일만 넣을 수 있습니다";
    public const string TooBigText = "파일이 너무 큽니다(50 MB 까지)";
    public const string OldEngineText = "엔진이 옛 판이라 모델 넣기를 쓸 수 없습니다";
    public const string PlaceHintText = "3D 에서 손잡이로 자리 · 방향 · 크기를 맞추세요";

    /// <summary>올리기 전 검사 — 문제 없으면 null, 있으면 쉬운 말.</summary>
    public static string? CheckFile(string path, long length)
        => !Extensions.Contains(Path.GetExtension(path).ToLowerInvariant()) ? WrongFileText : length > MaxBytes ? TooBigText : length <= 0 ? "빈 파일입니다" : null;

    /// <summary>엔진 항목 — uid · 경위도가 틀리면 null.</summary>
    public static Map3DModelItem? ParseItem(JObject? o)
    {
        if (o == null || Str(o["uid"]) is not { } uid || !Map3DUserBuilding.IsUid(uid) || Num(o["lon"]) is not { } lon || Num(o["lat"]) is not { } lat
            || Math.Abs(lon) > 180 || Math.Abs(lat) > 85) return null;
        return new Map3DModelItem(uid, Str(o["name"]), lon, lat, Num(o["alt_m"]), Num(o["heading_deg"]) ?? 0, Num(o["scale"]) is double s && s > 0 ? s : 1)
               {
                   Tris = o["tris"]?.Type == JTokenType.Integer ? o.Value<int>("tris") : null,
                   Source = Str(o["source"]),
               };
    }

    /// <summary>GET …/models 응답 {"items":[…]} — 형식이 틀린 항목은 뺀다.</summary>
    public static IReadOnlyList<Map3DModelItem> ParseList(string json)
    {
        var list = new List<Map3DModelItem>();
        if (JObject.Parse(json)["items"] is JArray items)
            foreach (var o in items.OfType<JObject>())
                if (ParseItem(o) is { } it) list.Add(it);
        return list;
    }

    /// <summary>PUT …/models/{uid} 본문 — 자리 · 고도(null = 지형) · 방향 · 크기 · 이름 모두(엔진은 모르는 키를 거절한다 — 이 키들만).</summary>
    public static JObject PlaceBody(Map3DModelItem m)
        => new()
        {
            ["lon"] = Math.Round(m.Lon, 7), ["lat"] = Math.Round(m.Lat, 7),
            ["alt_m"] = m.AltM is double a ? a : JValue.CreateNull(),
            ["heading_deg"] = Math.Round(m.HeadingDeg, 3), ["scale"] = Math.Round(m.Scale, 3),
            ["name"] = string.IsNullOrWhiteSpace(m.Name) ? JValue.CreateNull() : m.Name,
        };

    /// <summary>3D 가 넣은 모델을 다시 받아 그리게.</summary>
    public static string ReloadMessageJson() => new JObject { ["type"] = "models.reload" }.ToString(Formatting.None);

    /// <summary>3D 강조(null = 지움).</summary>
    public static string SelectMessageJson(string? uid)
        => new JObject { ["type"] = "model.select", ["uid"] = uid is null ? JValue.CreateNull() : uid }.ToString(Formatting.None);

    /// <summary>"넣은 모델 3개"(0 이면 "넣은 모델 없음").</summary>
    public static string CountText(int count) => count > 0 ? $"넣은 모델 {count}개" : "넣은 모델 없음";

    /// <summary>값 글(소수 둘째 자리까지, 끝 0 없이).</summary>
    public static string Num(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    private static string? Str(JToken? t) => t?.Type == JTokenType.String && t.Value<string>() is { Length: > 0 } s ? s : null;

    private static double? Num(JToken? t)
        => t?.Type is JTokenType.Float or JTokenType.Integer && double.IsFinite(t.Value<double>()) ? t.Value<double>() : null;
}
