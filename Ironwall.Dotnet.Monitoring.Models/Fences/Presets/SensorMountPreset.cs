using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 위치 프리셋 — 담장 한 칸 기준 설치 자리(sensor-mount-preset §5.1 <c>SensorMountPresets</c>).
/// </summary>
public sealed record SensorMountPreset
{
    [JsonProperty("id")] public int Id { get; init; }
    [JsonProperty("model_id")] public int ModelId { get; init; }
    [JsonProperty("name")] public string Name { get; init; } = string.Empty;
    /// <summary>모델당 하나 — 새 센서는 이 자리로 선다.</summary>
    [JsonProperty("is_default")] public bool IsDefault { get; init; }
    [JsonProperty("anchor")] public SensorMountAnchor Anchor { get; init; }
    /// <summary>칸 안 가로 몫(0 = 기둥 A · 1 = 기둥 B) — 다른 칸 폭엔 비례.</summary>
    [JsonProperty("along_ratio")] public double AlongRatio { get; init; } = 0.5;
    /// <summary>높이(m · 지중은 음수).</summary>
    [JsonProperty("height_m")] public double HeightM { get; init; }
    /// <summary>펜스에서 떨어진 거리(m) — 안 + / 밖 −.</summary>
    [JsonProperty("depth_m")] public double DepthM { get; init; }
    /// <summary>바라보는 방향(°) — 0 = 펜스 따라 보기 · +90 = 바깥쪽 정면 · −90 = 안쪽 정면.</summary>
    [JsonProperty("yaw_deg")] public double YawDeg { get; init; } = 90;
    /// <summary>위아래 각도(°) — 아래로 −.</summary>
    [JsonProperty("tilt_deg")] public double TiltDeg { get; init; }
    /// <summary>붙은 붙는 점 이름(<see cref="SensorSnapPoints"/>) — 자유 위치면 <c>null</c>.</summary>
    [JsonProperty("snap_point")] public string? SnapPoint { get; init; }
    [JsonProperty("bracket_m")] public double BracketM { get; init; }
    /// <summary>정한 샘플의 칸 폭(m).</summary>
    [JsonProperty("panel_width_m")] public double PanelWidthM { get; init; } = 3.0;
    /// <summary>정한 샘플의 펜스 높이(m).</summary>
    [JsonProperty("fence_height_m")] public double FenceHeightM { get; init; } = 2.4;
    /// <summary>정한 샘플에 철조망이 있었나.</summary>
    [JsonProperty("sample_has_razor")] public bool SampleHasRazor { get; init; }
    [JsonProperty("note", NullValueHandling = NullValueHandling.Ignore)] public string? Note { get; init; }
    [JsonProperty("updated_at", NullValueHandling = NullValueHandling.Ignore)] public DateTime? UpdatedAt { get; init; }

    /// <summary>자리 값만 같은가(이름 · id · 시각 제외) — 편집기의 "바뀜" 셈.</summary>
    public bool SamePlace(SensorMountPreset other)
        => other is not null && Anchor == other.Anchor && Near(AlongRatio, other.AlongRatio, 1e-4) && Near(HeightM, other.HeightM) && Near(DepthM, other.DepthM)
           && Near(YawDeg, other.YawDeg) && Near(TiltDeg, other.TiltDeg) && SnapPoint == other.SnapPoint && Near(BracketM, other.BracketM);

    internal static bool Near(double a, double b, double eps = 0.005) => Math.Abs(a - b) < eps;
}

/// <summary>
/// 그 자리에서만 따로 고친 값 — 있는 칸만 프리셋 값을 덮는다(레이아웃 JSON <c>mount_override</c>).
/// </summary>
public sealed record SensorMountOverride
{
    [JsonProperty("along_ratio", NullValueHandling = NullValueHandling.Ignore)] public double? AlongRatio { get; init; }
    [JsonProperty("height_m", NullValueHandling = NullValueHandling.Ignore)] public double? HeightM { get; init; }
    [JsonProperty("depth_m", NullValueHandling = NullValueHandling.Ignore)] public double? DepthM { get; init; }
    [JsonProperty("yaw_deg", NullValueHandling = NullValueHandling.Ignore)] public double? YawDeg { get; init; }
    [JsonProperty("tilt_deg", NullValueHandling = NullValueHandling.Ignore)] public double? TiltDeg { get; init; }
    [JsonProperty("bracket_m", NullValueHandling = NullValueHandling.Ignore)] public double? BracketM { get; init; }

    /// <summary>고친 칸이 하나도 없는가.</summary>
    [JsonIgnore]
    public bool IsEmpty => AlongRatio is null && HeightM is null && DepthM is null && YawDeg is null && TiltDeg is null && BracketM is null;

    /// <summary>고친 칸 수(칩 "바뀜 n").</summary>
    [JsonIgnore]
    public int Count => new object?[] { AlongRatio, HeightM, DepthM, YawDeg, TiltDeg, BracketM }.Count(v => v is not null);

    /// <summary>빈 것은 <c>null</c> 로(저장 문서에 빈 칸을 싣지 않는다).</summary>
    public static SensorMountOverride? OrNull(SensorMountOverride? value) => value is null || value.IsEmpty ? null : value;
}

/// <summary>속성 값 한 줄 — 키 · 표시 이름 · 값(글자) · 단위 · 쓰임.</summary>
public sealed record SensorAttributeValue(
    [property: JsonProperty("key")] string Key,
    [property: JsonProperty("label")] string Label,
    [property: JsonProperty("value")] string Value,
    [property: JsonProperty("unit")] string Unit,
    [property: JsonProperty("use")] SensorAttributeUse Use)
{
    /// <summary>숫자로 읽기(범위 "8~14" · "20~30" 이면 앞 값).</summary>
    public double? Number
    {
        get
        {
            var text = (Value ?? string.Empty).Split('~')[0].Trim();
            return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        }
    }
}

/// <summary>속성 프리셋 — 모델마다 항목이 달라 키-값(sensor-mount-preset §5.1 <c>SensorAttributePresets</c>).</summary>
public sealed record SensorAttributePreset
{
    /// <summary>결선맵 탐지 범위 그림이 읽는 키.</summary>
    public const string KEY_RANGE = "detect_range_m";
    /// <summary>탐지 각.</summary>
    public const string KEY_ANGLE = "detect_angle_deg";
    /// <summary>결선맵 "권장 간격 넘음" 알림이 읽는 키.</summary>
    public const string KEY_SPACING = "spacing_m";

    [JsonProperty("id")] public int Id { get; init; }
    [JsonProperty("model_id")] public int ModelId { get; init; }
    [JsonProperty("name")] public string Name { get; init; } = string.Empty;
    [JsonProperty("is_default")] public bool IsDefault { get; init; }
    [JsonProperty("values")] public IReadOnlyList<SensorAttributeValue> Values { get; init; } = Array.Empty<SensorAttributeValue>();
    [JsonProperty("updated_at", NullValueHandling = NullValueHandling.Ignore)] public DateTime? UpdatedAt { get; init; }

    /// <summary>키의 숫자 값(그림 · 알림 쓰임만 — 참고 값은 결선맵이 읽지 않는다).</summary>
    public double? NumberOf(string key)
        => Values.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase) && v.Use != SensorAttributeUse.Reference)?.Number;

    /// <summary>탐지 거리(m).</summary>
    [JsonIgnore] public double? RangeM => NumberOf(KEY_RANGE);
    /// <summary>권장 설치 간격(m).</summary>
    [JsonIgnore] public double? SpacingM => NumberOf(KEY_SPACING);
}

/// <summary>프리셋 갈래.</summary>
public enum PresetKind
{
    [EnumMember(Value = "model")] Model = 0,
    [EnumMember(Value = "mount")] Mount = 1,
    [EnumMember(Value = "attribute")] Attribute = 2,
}

/// <summary>지울 대상 — 갈래 + id.</summary>
public readonly record struct PresetKey(PresetKind Kind, int Id);
