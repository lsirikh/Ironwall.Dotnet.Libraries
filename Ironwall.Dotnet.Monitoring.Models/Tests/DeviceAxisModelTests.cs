using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// FR-03 — 장비 모델의 v7.0+ 표현 축(접속·형상·관측·의도) 수용.
/// 축은 <see cref="IBaseDeviceModel.Axes"/> 한 묶음이다 — 재조회 복사에서 필드를 빠뜨릴 수 없게(ISSUE-17).
/// </summary>
public class DeviceAxisModelTests
{
    private static DeviceAxesModel SampleAxes() => new()
    {
        Connection = new ConnectionAxisModel
        {
            Type = "CONTROLLER_CONTACT",
            ParentDeviceId = 12,
            Channel = 3,
            Protocol = "ONVIF",
        },
        HardwareSpec = new HardwareSpecModel
        {
            Manufacturer = "Sensorway",
            Components =
            {
                new ComponentDefinitionModel { Key = "main_door_motor", Type = "DOOR_ACTUATOR", Channel = 1 },
                new ComponentDefinitionModel { Key = "door_sw", Type = "DOOR_SENSOR" },
            },
        },
        DeviceStatus = new DeviceStatusModel
        {
            Components =
            {
                ["main_door_motor"] = new ComponentStatusModel { State = "RUNNING", Health = "OK", ObservedAt = "2026-09-19T08:41:12.574065+09:00" },
                ["door_sw"] = new ComponentStatusModel { State = "OPEN", Health = "OK" },
            },
        },
        DeviceConfig = new DeviceConfigModel(),
        Meta = new ResponseMeta("full", new[] { "connection", "hardware_spec", "components", "device_status" }),
    };

    [Fact]
    public void should_expose_discriminator_type_axis_unit_and_axes_on_base_device_model()
    {
        IBaseDeviceModel model = new CameraDeviceModel
        {
            CategoryDevice = EnumDeviceCategory.Camera,
            TypeAxisCode = "SPEED_DOME",
            UnitId = 3,
            Axes = SampleAxes(),
        };

        Assert.Equal(EnumDeviceCategory.Camera, model.CategoryDevice);
        Assert.Equal("SPEED_DOME", model.TypeAxisCode);
        Assert.Equal(3, model.UnitId);
        Assert.Equal("CONTROLLER_CONTACT", model.Axes!.Connection!.Type);
        Assert.Equal(2, model.Axes.HardwareSpec!.Components.Count);
    }

    [Fact]
    public void should_default_to_no_axes_when_model_is_new()
    {
        var model = new SensorDeviceModel();

        Assert.Null(model.Axes);
        Assert.Null(model.TypeAxisCode);
        Assert.Null(model.UnitId);
        Assert.Equal(EnumDeviceCategory.None, model.CategoryDevice);
    }

    [Fact]
    public void should_not_leak_axis_properties_into_legacy_json_when_serializing_model()
    {
        // 장비 모델은 NATS·조치보고 본문으로도 직렬화된다 — 새 속성이 그 와이어에 새면 안 된다.
        var model = new CameraDeviceModel
        {
            Id = 7,
            CategoryDevice = EnumDeviceCategory.Camera,
            TypeAxisCode = "SPEED_DOME",
            UnitId = 3,
            Axes = SampleAxes(),
        };
        var baseline = JsonConvert.SerializeObject(new CameraDeviceModel { Id = 7 });

        var json = JsonConvert.SerializeObject(model);

        Assert.Equal(baseline, json);
    }

    [Fact]
    public void should_carry_axis_properties_when_copy_constructing()
    {
        var source = new BaseDeviceModel
        {
            Id = 9,
            CategoryDevice = EnumDeviceCategory.Gate,
            TypeAxisCode = "Sliding",
            UnitId = 2,
            Axes = SampleAxes(),
        };

        var copy = new BaseDeviceModel(source);

        Assert.Equal(EnumDeviceCategory.Gate, copy.CategoryDevice);
        Assert.Equal("Sliding", copy.TypeAxisCode);
        Assert.Equal(2, copy.UnitId);
        Assert.Same(source.Axes, copy.Axes);
    }

    [Theory]
    [InlineData("DOOR_ACTUATOR", "RUNNING")]
    [InlineData("DOOR_SENSOR", "OPEN")]
    [InlineData("door_actuator", "RUNNING")]   // 어휘 비교는 대소문자 무시
    public void should_find_component_status_by_type_when_key_is_arbitrary(string componentType, string expectedState)
    {
        // 부품 key 는 장비마다 자유 — 코드에 key 를 박지 않고 type 으로 찾는다.
        var axes = SampleAxes();

        var status = axes.FindStatusByType(componentType);

        Assert.Equal(expectedState, status?.State);
    }

    [Fact]
    public void should_return_null_status_when_component_type_is_absent_or_unobserved()
    {
        var axes = SampleAxes();
        axes.HardwareSpec!.Components.Add(new ComponentDefinitionModel { Key = "fan1", Type = "FAN" });   // 선언만 있고 관측 없음

        Assert.Null(axes.FindStatusByType("HEATER"));
        Assert.Null(axes.FindStatusByType("FAN"));
        Assert.Null(new DeviceAxesModel().FindStatusByType("DOOR_ACTUATOR"));
    }

    [Theory]
    [InlineData("connection", true)]
    [InlineData("HARDWARE_SPEC", true)]
    [InlineData("device_config", false)]   // 응답에 실리지 않은 절 — "빈 값"이 아니라 "미수신"
    public void should_report_section_received_only_when_listed_in_meta_sections(string section, bool expected)
    {
        var axes = SampleAxes();

        Assert.Equal(expected, axes.IsSectionReceived(section));
    }

    [Fact]
    public void should_report_nothing_received_when_meta_is_missing()
    {
        // 6.3 응답에는 meta.sections 가 없다 — 어떤 절도 "받았다"고 말하지 않는다.
        var axes = new DeviceAxesModel { Connection = new ConnectionAxisModel() };

        Assert.False(axes.IsSectionReceived("connection"));
        Assert.Empty(new ResponseMeta(null, null).Sections);
    }
}
