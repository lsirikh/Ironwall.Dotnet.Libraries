using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>센서 모델의 입체 모양(sensor-mount-preset FR-18) — 종류 4가지가 아니라 모델 7종으로 그린다.</summary>
public enum SensorShape
{
    /// <summary>펜스센서 — 메쉬에 붙는 작은 상자.</summary>
    [EnumMember(Value = "fence")] Fence = 0,
    /// <summary>복합센서 — 브래킷 상자.</summary>
    [EnumMember(Value = "multi")] Multi = 1,
    /// <summary>스마트센서 — 후드 상자.</summary>
    [EnumMember(Value = "smart")] Smart = 2,
    /// <summary>스마트센서2 — 후드 상자(넓은 후드).</summary>
    [EnumMember(Value = "smart2")] Smart2 = 3,
    /// <summary>PIR센서 — 둥근 렌즈 몸.</summary>
    [EnumMember(Value = "pir")] Pir = 4,
    /// <summary>광케이블 — 펜스를 따라 가는 줄.</summary>
    [EnumMember(Value = "optical")] Optical = 5,
    /// <summary>지진동센서 — 땅속 막대.</summary>
    [EnumMember(Value = "underground")] Underground = 6,
}

/// <summary>붙는 점의 종류(모델이 허용하는 갈래 — 사용자 스케치 1 · 2).</summary>
public enum SensorAnchorKind
{
    [EnumMember(Value = "post")] Post = 0,
    [EnumMember(Value = "rail")] Rail = 1,
    [EnumMember(Value = "mesh")] Mesh = 2,
    /// <summary>윤형 철조망 코일 — 철조망이 있는 칸에서만.</summary>
    [EnumMember(Value = "razor")] Razor = 3,
    [EnumMember(Value = "ground")] Ground = 4,
    [EnumMember(Value = "wall")] Wall = 5,
}

/// <summary>위치 프리셋이 붙는 곳(storyboard 데이터 표 <c>anchor</c>).</summary>
public enum SensorMountAnchor
{
    [EnumMember(Value = "post_top")] PostTop = 0,
    [EnumMember(Value = "post_rail")] PostRail = 1,
    [EnumMember(Value = "post_mid")] PostMid = 2,
    [EnumMember(Value = "post_low")] PostLow = 3,
    [EnumMember(Value = "rail")] Rail = 4,
    [EnumMember(Value = "mesh")] Mesh = 5,
    /// <summary>윤형 코일 위 · 가운데.</summary>
    [EnumMember(Value = "razor")] Razor = 6,
    /// <summary>윤형 코일의 기둥 쪽 끝 고리 자체(스케치 2 빨간 큰 타원).</summary>
    [EnumMember(Value = "razor_loop")] RazorLoop = 7,
    [EnumMember(Value = "wall_top")] WallTop = 8,
    [EnumMember(Value = "wall_face")] WallFace = 9,
    [EnumMember(Value = "ground")] Ground = 10,
}

/// <summary>속성 값이 어디에 쓰이나 — 그림 · 알림이면 결선맵이 읽고, 참고는 표시만.</summary>
public enum SensorAttributeUse
{
    [EnumMember(Value = "render")] Render = 0,
    [EnumMember(Value = "alert")] Alert = 1,
    [EnumMember(Value = "reference")] Reference = 2,
}

/// <summary>실제 치수(mm) — 가로 W · 높이 H · 길이(깊이) L.</summary>
public sealed record SensorSizeMm(
    [property: JsonProperty("w")] double W,
    [property: JsonProperty("h")] double H,
    [property: JsonProperty("l")] double L)
{
    public override string ToString() => $"{W:0}×{H:0}×{L:0}";
}

/// <summary>
/// 센서 모델 = 회사 제품 한 종(sensor-mount-preset §2). 장비 종류(<see cref="EnumDeviceType"/>)와 1:1 이 아니다 — 같은 종류도 판이 다를 수 있다.
/// </summary>
public sealed record SensorModel
{
    [JsonProperty("id")] public int Id { get; init; }
    /// <summary>모델 고유 글자(내보내기/가져오기 맞춤 열쇠).</summary>
    [JsonProperty("code")] public string Code { get; init; } = string.Empty;
    [JsonProperty("name")] public string Name { get; init; } = string.Empty;
    [JsonProperty("device_type")] public EnumDeviceType DeviceType { get; init; }
    [JsonProperty("shape")] public SensorShape Shape { get; init; }
    [JsonProperty("size_mm")] public SensorSizeMm SizeMm { get; init; } = new(100, 100, 60);
    [JsonProperty("manufacturer")] public string? Manufacturer { get; init; }
    /// <summary>붙을 수 있는 점 종류.</summary>
    [JsonProperty("allowed_kinds")] public IReadOnlyList<SensorAnchorKind> AllowedKinds { get; init; } = Array.Empty<SensorAnchorKind>();
    /// <summary>회사 기본 모델(지울 수 없음 · 숨기기만).</summary>
    [JsonProperty("is_builtin")] public bool IsBuiltin { get; init; }
    [JsonProperty("is_hidden")] public bool IsHidden { get; init; }
    [JsonProperty("updated_at", NullValueHandling = NullValueHandling.Ignore)] public DateTime? UpdatedAt { get; init; }

    /// <summary>이 종류의 붙는 점에 붙을 수 있는가(허용 목록이 비면 모두 허용).</summary>
    public bool Allows(SensorAnchorKind kind) => AllowedKinds.Count == 0 || AllowedKinds.Contains(kind);

    /// <summary>모양 이름(쉬운 말).</summary>
    public static string ShapeText(SensorShape shape) => shape switch
    {
        SensorShape.Fence => "메쉬 상자",
        SensorShape.Multi => "브래킷 상자",
        SensorShape.Smart => "후드 상자",
        SensorShape.Smart2 => "넓은 후드 상자",
        SensorShape.Pir => "둥근 렌즈",
        SensorShape.Optical => "케이블",
        _ => "땅속 막대",
    };

    /// <summary>목록 표시 이름.</summary>
    public override string ToString() => Name;

    public bool Equals(SensorModel? other)
        => other is not null && Id == other.Id && Code == other.Code && Name == other.Name && DeviceType == other.DeviceType && Shape == other.Shape
           && SizeMm == other.SizeMm && Manufacturer == other.Manufacturer && AllowedKinds.SequenceEqual(other.AllowedKinds)
           && IsBuiltin == other.IsBuiltin && IsHidden == other.IsHidden;

    public override int GetHashCode() => HashCode.Combine(Id, Code, Name, DeviceType, Shape);
}
