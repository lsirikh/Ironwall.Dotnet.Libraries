using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Monitoring.Models.Fences.Presets;

/// <summary>
/// 회사 기본 7종과 그 기본 프리셋(sensor-mount-preset D4 · FR-13) — 삭제 불가 · 숨기기만. 저장소가 처음 열 때 심는다.
/// 값은 회사 제품 소개서(제어기 · 펜스센서 · 복합센서)에서 옮겼고, 소개서에 없는 모델의 값은 편집 가능한 시작값이다(쓰임 = 참고가 아닌 값은 결선맵이 읽는다).
/// </summary>
public static class SensorPresetCatalog
{
    public const int FENCE = 1, MULTI = 2, UNDERGROUND = 3, SMART = 4, SMART2 = 5, PIR = 6, OPTICAL = 7;

    /// <summary>기본 샘플 — 칸 폭 3.0 m · 펜스 2.4 m(결선맵 기준과 같다).</summary>
    public static FenceSampleBay DefaultBay { get; } = new(3.0, 2.4);

    public static IReadOnlyList<SensorModel> Models { get; } = new[]
    {
        Model(FENCE, "FENCE_MEMS", "펜스센서 MEMS", EnumDeviceType.Fence, SensorShape.Fence, new(115, 59, 31), SensorAnchorKind.Mesh, SensorAnchorKind.Rail, SensorAnchorKind.Razor),
        Model(MULTI, "MULTI_PIR", "복합센서 PIR", EnumDeviceType.Multi, SensorShape.Multi, new(91, 108, 70), SensorAnchorKind.Post, SensorAnchorKind.Rail, SensorAnchorKind.Razor),
        Model(UNDERGROUND, "SEISMIC", "지진동센서", EnumDeviceType.Underground, SensorShape.Underground, new(40, 300, 40), SensorAnchorKind.Ground),
        Model(SMART, "SMART", "스마트센서", EnumDeviceType.SmartSensor, SensorShape.Smart, new(120, 160, 90), SensorAnchorKind.Post, SensorAnchorKind.Rail, SensorAnchorKind.Razor),
        Model(SMART2, "SMART2", "스마트센서2", EnumDeviceType.SmartSensor2, SensorShape.Smart2, new(150, 170, 100), SensorAnchorKind.Post, SensorAnchorKind.Rail, SensorAnchorKind.Razor),
        Model(PIR, "PIR", "PIR센서", EnumDeviceType.PIR, SensorShape.Pir, new(70, 70, 60), SensorAnchorKind.Post, SensorAnchorKind.Rail, SensorAnchorKind.Wall),
        Model(OPTICAL, "OPTICAL", "광케이블", EnumDeviceType.OpticalCable, SensorShape.Optical, new(8, 8, 1000), SensorAnchorKind.Mesh, SensorAnchorKind.Rail, SensorAnchorKind.Razor, SensorAnchorKind.Ground),
    };

    /// <summary>기본 위치 프리셋(모델마다 ★기본 하나).</summary>
    public static IReadOnlyList<SensorMountPreset> MountPresets { get; } = BuildMountPresets();

    /// <summary>기본 속성 프리셋(모델마다 ★기본 하나).</summary>
    public static IReadOnlyList<SensorAttributePreset> AttributePresets { get; } = new[]
    {
        Attr(FENCE, "표준 (탐지 반경 3 m)", R("detect_range_m", "탐지 반경", "3", "m"), A("spacing_m", "권장 설치 간격", "2.5", "m"), Ref("size", "크기 W×H×L", "115×59×31", "mm")),
        Attr(MULTI, "표준 (탐지 20 m)", R("detect_range_m", "탐지 거리", "20", "m"), R("detect_angle_deg", "탐지 각", "90", "°"), A("spacing_m", "권장 설치 간격", "20", "m"),
             Ref("wavelength", "열감지 파장", "8~14", "μm"), Ref("temp", "동작 온도", "-40 ~ 85", "℃"), Ref("size", "크기 W×H×L", "91×108×70", "mm")),
        Attr(UNDERGROUND, "표준 (탐지 반경 15 m)", R("detect_range_m", "탐지 반경", "15", "m"), A("spacing_m", "권장 설치 간격", "20~30", "m")),
        Attr(SMART, "표준", R("detect_range_m", "탐지 거리", "20", "m"), R("detect_angle_deg", "탐지 각", "90", "°"), A("spacing_m", "권장 설치 간격", "20", "m")),
        Attr(SMART2, "표준", R("detect_range_m", "탐지 거리", "20", "m"), R("detect_angle_deg", "탐지 각", "90", "°"), A("spacing_m", "권장 설치 간격", "20", "m")),
        Attr(PIR, "표준", R("detect_range_m", "탐지 거리", "12", "m"), R("detect_angle_deg", "탐지 각", "90", "°"), A("spacing_m", "권장 설치 간격", "12", "m")),
        Attr(OPTICAL, "표준", Ref("length", "구간 길이", "1000", "m")),
    };

    /// <summary>장비 종류의 기본 모델(종류가 같은 회사 모델 · 스마트 복합 · 모름은 스마트).</summary>
    public static SensorModel DefaultModelFor(EnumDeviceType type, IReadOnlyList<SensorModel>? models = null)
    {
        var list = models ?? Models;
        var target = type switch
        {
            EnumDeviceType.Fence or EnumDeviceType.Fence_Group => FENCE,
            EnumDeviceType.Multi => MULTI,
            EnumDeviceType.Underground => UNDERGROUND,
            EnumDeviceType.SmartSensor2 or EnumDeviceType.SmartMultisensor2 => SMART2,
            EnumDeviceType.PIR => PIR,
            EnumDeviceType.OpticalCable or EnumDeviceType.Cable => OPTICAL,
            _ => SMART,
        };
        return list.FirstOrDefault(m => m.DeviceType == type && m.IsBuiltin)
               ?? list.FirstOrDefault(m => m.Id == target)
               ?? Models.First(m => m.Id == target);
    }

    #region - builders -
    private static SensorModel Model(int id, string code, string name, EnumDeviceType type, SensorShape shape, SensorSizeMm size, params SensorAnchorKind[] kinds)
        => new() { Id = id, Code = code, Name = name, DeviceType = type, Shape = shape, SizeMm = size, Manufacturer = "아이언월", AllowedKinds = kinds, IsBuiltin = true };

    private static IReadOnlyList<SensorMountPreset> BuildMountPresets()
    {
        var bay = DefaultBay;
        var list = new List<SensorMountPreset>();
        var next = 1;
        void Add(int model, string point, bool isDefault, double yaw = 90, double tilt = 0, double bracket = 0)
        {
            var snap = SensorSnapPoints.Find(bay with { HasRazor = true }, point) ?? throw new InvalidOperationException(point);
            var preset = SensorMountResolver.SnapTo(new SensorMountPreset { ModelId = model, IsDefault = isDefault, YawDeg = yaw, TiltDeg = tilt, BracketM = bracket },
                                                    snap, bay with { HasRazor = snap.Kind == SensorAnchorKind.Razor });
            list.Add(preset with { Id = next++, Name = SensorMountResolver.SuggestName(preset, bay with { HasRazor = snap.Kind == SensorAnchorKind.Razor }) });
        }
        Add(FENCE, "mesh.mid", true, yaw: 0);
        Add(FENCE, "mesh.low", false, yaw: 0);
        Add(FENCE, "razor.mid", false, yaw: 0);
        Add(MULTI, "post_b.mid", true, bracket: 0.3);
        Add(MULTI, "post_b.top", false, tilt: -10);
        Add(MULTI, "razor.loop_b", false);
        Add(UNDERGROUND, "ground.in", true, yaw: 0);
        Add(UNDERGROUND, "ground.out", false, yaw: 0);
        Add(SMART, "post_b.top", true);
        Add(SMART2, "post_b.top", true);
        Add(PIR, "post_b.mid", true, bracket: 0.2);
        Add(OPTICAL, "mesh.mid", true, yaw: 0);
        Add(OPTICAL, "razor.top.50", false, yaw: 0);
        return list;
    }

    private static int _attrId;
    private static SensorAttributePreset Attr(int model, string name, params SensorAttributeValue[] values)
        => new() { Id = ++_attrId, ModelId = model, Name = name, IsDefault = true, Values = values };

    private static SensorAttributeValue R(string key, string label, string value, string unit) => new(key, label, value, unit, SensorAttributeUse.Render);
    private static SensorAttributeValue A(string key, string label, string value, string unit) => new(key, label, value, unit, SensorAttributeUse.Alert);
    private static SensorAttributeValue Ref(string key, string label, string value, string unit) => new(key, label, value, unit, SensorAttributeUse.Reference);
    #endregion
}
