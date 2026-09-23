using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// D-31 후속 — 함체 히터·팬 부품 key 의 읽기 시점 캐시(<c>ToEnclosureDeviceModel</c>)와
/// 쓰기 시점 힌트 전달(<c>ToEnclosureDeviceDto</c>)을 고정한다.
/// </summary>
/// <remarks>
/// 이 캐시가 없으면 패널이 선언된 히터·팬을 가진 함체를 편집해도 저장 본문에 override 가 아예 안 실려
/// (선언을 모르니 <see cref="EnclosureDeviceDto.SetEnabledIfPresent"/> 가 조용히 스킵) 조작이 사라진다.
/// 동시에, 이 캐시는 <b>본문에 <c>hardware_spec</c> 을 절대 되싣지 않는다</b> — PATCH 도
/// <c>hardware_spec.components</c> 를 통째 교체하기 때문이다.
/// </remarks>
public class EnclosureComponentKeyMappingTests
{
    [Fact]
    public void should_capture_declared_component_keys_when_reading_a_dto_with_hardware_spec()
    {
        var dto = new EnclosureDeviceDto
        {
            NumberDevice = 1,
            NameDevice = "함체_01",
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "heater_1", Type = ComponentTypeNames.Heater },
                    new() { Key = "fan_1", Type = ComponentTypeNames.Fan },
                },
            },
        };

        var model = dto.ToEnclosureDeviceModel();

        Assert.Equal("heater_1", model.HeaterComponentKey);
        Assert.Equal("fan_1", model.FanComponentKey);
    }

    [Fact]
    public void should_leave_component_keys_null_when_hardware_spec_is_absent()
    {
        var dto = new EnclosureDeviceDto { NumberDevice = 1, NameDevice = "함체_01" };

        var model = dto.ToEnclosureDeviceModel();

        Assert.Null(model.HeaterComponentKey);
        Assert.Null(model.FanComponentKey);
    }

    [Fact]
    public void should_leave_component_keys_null_when_declared_components_do_not_include_heater_or_fan()
    {
        var dto = new EnclosureDeviceDto
        {
            NumberDevice = 1,
            NameDevice = "함체_01",
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "door", Type = ComponentTypeNames.DoorSensor },
                },
            },
        };

        var model = dto.ToEnclosureDeviceModel();

        Assert.Null(model.HeaterComponentKey);
        Assert.Null(model.FanComponentKey);
    }

    [Fact]
    public void should_carry_cached_keys_into_the_write_dto_as_lookup_only_hints_without_hardware_spec()
    {
        var model = new EnclosureDeviceModel
        {
            DeviceNumber = 1,
            DeviceName = "함체_01",
            HeaterEnabled = true,
            HeaterComponentKey = "heater_1",
            FanComponentKey = "fan_1",
        };

        var dto = model.ToEnclosureDeviceDto();
        dto.UseAxisWrite = true;

        Assert.Equal("heater_1", dto.HeaterComponentKeyHint);
        Assert.Equal("fan_1", dto.FanComponentKeyHint);
        // 쓰기 채널은 절대 건드리지 않는다 — 되실으면 PATCH 가 components 를 통째 교체한다.
        Assert.Null(dto.HardwareSpec);

        var raw = JsonConvert.SerializeObject(dto);
        var body = JObject.Parse(raw);

        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater_1.enabled"));
        Assert.Null(body["hardware_spec"]);
        Assert.DoesNotContain("hardware_spec", raw);
    }

    [Fact]
    public void should_round_trip_declared_keys_through_model_and_back_to_a_new_dto()
    {
        var original = new EnclosureDeviceDto
        {
            NumberDevice = 1,
            NameDevice = "함체_01",
            HardwareSpec = new HardwareSpecDto
            {
                Components = new List<ComponentDefinitionDto>
                {
                    new() { Key = "heater_9", Type = ComponentTypeNames.Heater },
                },
            },
        };

        var model = original.ToEnclosureDeviceModel();
        model.HeaterEnabled = true;
        var editDto = model.ToEnclosureDeviceDto();
        editDto.UseAxisWrite = true;

        var body = JObject.Parse(JsonConvert.SerializeObject(editDto));

        Assert.Equal(true, (bool?)body.SelectToken("device_config.component_overrides.heater_9.enabled"));
        Assert.Null(body["hardware_spec"]);
    }
}
