using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
/****************************************************************************
   Purpose      : 처음 실행 때 심어 주는 본보기 프리셋 다섯(설계 정본 AS 의 예시 그대로).
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 설계 정본(<c>device-component-assembly-preset-storyboard.html</c>)이 보여 주는 <b>다섯</b>을 그대로 심는다.
/// </summary>
/// <remarks>
/// <para><b>Id 가 고정 글자</b>인 이유: 다시 심어도 같은 줄이라 늘어나지 않는다. Guid 로 만들면 파일이
/// 한 번 깨질 때마다 "표준 옥외 함체" 가 하나씩 늘어난다.</para>
/// <para><see cref="DevicePreset.UpdatedAt"/> 도 <b>고정 시각</b>이다 — 씨앗은 심은 때가 아니라 만든 때를 갖는다.
/// 그래야 두 PC 의 씨앗이 같은 글자가 된다.</para>
/// <para>씨앗은 <b>읽을 때마다 새로 만든다</b>. 안에 든 <see cref="JObject"/> 는 고칠 수 있는 물건이라
/// 한 벌을 돌려 쓰면 한 곳의 편집이 모두에게 번진다.</para>
/// </remarks>
public static class DevicePresetSeeds
{
    /// <summary>씨앗이 만들어진 때(고정).</summary>
    public static DateTimeOffset SeededAt { get; } =
        new(2026, 9, 19, 0, 0, 0, TimeSpan.FromHours(9));

    public const string EnclosureStandardId = "seed-enclosure-standard";
    public const string ControllerIo16Id = "seed-controller-io16";
    public const string GateSlidingId = "seed-gate-sliding";
    public const string CameraPtzColdId = "seed-camera-ptz-cold";
    public const string SensorFenceId = "seed-sensor-fence";

    /// <summary>본보기 다섯.</summary>
    public static IReadOnlyList<DevicePreset> All => new[]
    {
        EnclosureStandard(), ControllerIo16(), GateSliding(), CameraPtzCold(), SensorFence(),
    };

    /// <summary>표준 옥외 함체 — 도어 · 온습도 · 히터 · 팬 · UPS.</summary>
    private static DevicePreset EnclosureStandard() => new()
    {
        Id = EnclosureStandardId,
        Name = "표준 옥외 함체",
        Category = EnumDeviceCategory.Enclosure,
        TypeAxisCode = "Outdoor",
        Description = "도어·온습도·히터·팬·UPS",
        UpdatedAt = SeededAt,
        IsSeed = true,
        Components = new List<ComponentDefinitionModel>
        {
            Component("DOOR_SENSOR", "door", "전면 도어"),
            Component("TEMPERATURE_SENSOR", "temp", "내부 상단"),
            Component("HUMIDITY_SENSOR", "humid", "내부 상단"),
            Component("HEATER", "heater", "하단"),
            Component("FAN", "fan", "상단 배기"),
            Component("UPS", "ups", "하단 트레이"),
            Component("NETWORK_INTERFACE", "nic", null),
        },
        Thresholds = JObject.Parse("""
            { "temperature": { "high": 45, "low": -10 }, "humidity": { "high": 85, "low": null } }
            """),
        ComponentOverrides = JObject.Parse("""
            { "heater": { "enabled": true }, "fan": { "enabled": true } }
            """),
    };

    /// <summary>IO 제어기 16채널 — 접점 16 + NIC. key 는 <c>ci_01</c>‥<c>ci_16</c>, 채널도 같이 붙는다.</summary>
    private static DevicePreset ControllerIo16()
    {
        var components = new List<ComponentDefinitionModel>();
        for (var i = 1; i <= 16; i++)
        {
            var index = i.ToString("00", CultureInfo.InvariantCulture);
            components.Add(Component("CONTACT_INPUT", $"ci_{index}", $"단자 {i}", i));
        }
        components.Add(Component("NETWORK_INTERFACE", "nic", null));

        return new DevicePreset
        {
            Id = ControllerIo16Id,
            Name = "IO 제어기 16채널",
            Category = EnumDeviceCategory.Controller,
            TypeAxisCode = "IoController",
            Description = "접점 16 + NIC",
            UpdatedAt = SeededAt,
            IsSeed = true,
            Components = components,
        };
    }

    /// <summary>슬라이딩 통문 — 구동부 · 도어센서 · 리미트 2. 문 부품의 key 가 함체와 다르다(<c>actuator</c>).</summary>
    private static DevicePreset GateSliding() => new()
    {
        Id = GateSlidingId,
        Name = "슬라이딩 통문",
        Category = EnumDeviceCategory.Gate,
        TypeAxisCode = "Sliding",
        Description = "구동부·도어센서·리미트 2",
        UpdatedAt = SeededAt,
        IsSeed = true,
        Components = new List<ComponentDefinitionModel>
        {
            Component("DOOR_ACTUATOR", "actuator", "문틀 상부", 2),
            Component("DOOR_SENSOR", "door", "문틀 측면"),
            Component("LIMIT_SWITCH", "limit_open", "열림단", 3),
            Component("LIMIT_SWITCH", "limit_close", "닫힘단", 4),
            Component("NETWORK_INTERFACE", "nic", null),
        },
    };

    /// <summary>PTZ 카메라 (한랭지) — PTZ · IR · 와이퍼 · 히터.</summary>
    private static DevicePreset CameraPtzCold() => new()
    {
        Id = CameraPtzColdId,
        Name = "PTZ 카메라 (한랭지)",
        Category = EnumDeviceCategory.Camera,
        TypeAxisCode = "PTZ",
        Description = "PTZ·IR·와이퍼·히터",
        UpdatedAt = SeededAt,
        IsSeed = true,
        Components = new List<ComponentDefinitionModel>
        {
            Component("PTZ_UNIT", "ptz", null),
            Component("IR_LED", "ir", "전면"),
            Component("WIPER", "wiper", "전면"),
            Component("HEATER", "heater", "하우징"),
            Component("NETWORK_INTERFACE", "nic", null),
        },
        Modes = JObject.Parse("""
            { "camera_mode": "AUTO", "day_night_mode": "AUTO", "is_record": true }
            """),
        ComponentOverrides = JObject.Parse("""
            { "heater": { "enabled": true } }
            """),
    };

    /// <summary>펜스 진동 센서 — 진동 감지부 + NIC.</summary>
    private static DevicePreset SensorFence() => new()
    {
        Id = SensorFenceId,
        Name = "펜스 진동 센서",
        Category = EnumDeviceCategory.Sensor,
        TypeAxisCode = "Fence",
        Description = "진동 감지부 + NIC",
        UpdatedAt = SeededAt,
        IsSeed = true,
        Components = new List<ComponentDefinitionModel>
        {
            Component("VIBRATION_SENSOR", "vib", "펜스 상단"),
            Component("NETWORK_INTERFACE", "nic", null),
        },
    };

    private static ComponentDefinitionModel Component(string type, string key, string? position, int? channel = null)
        => new()
        {
            Key = key,
            Type = type,
            Position = string.IsNullOrEmpty(position) ? null : position,
            Channel = channel,
        };
}
