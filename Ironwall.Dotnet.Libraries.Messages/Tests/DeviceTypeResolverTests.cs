using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// FR-02 — 판별자(<c>category_device</c>) + 옛 종류(<c>type_device</c>) → <see cref="EnumDeviceType"/> 해석의 단일 정본.
/// Events.Ui 와 Devices.Ui 가 같은 함수를 쓴다(두 벌 금지, device-console-v8 ISSUE-27).
/// </summary>
public class DeviceTypeResolverTests
{
    // ── ① 6.3 전문: type_device 가 읽히면 그것이 이긴다 (AD-1 — 6.3 결과 불변) ──

    [Fact]
    public void should_resolve_every_legacy_type_device_name_when_parseable()
    {
        foreach (EnumDeviceType type in Enum.GetValues<EnumDeviceType>())
        {
            if (type == EnumDeviceType.NONE) continue;

            var resolved = DeviceTypeResolver.Resolve(type.ToString(), categoryDevice: null);

            Assert.Equal(type, resolved);
        }
    }

    [Theory]
    [InlineData("ipcamera", EnumDeviceType.IpCamera)]
    [InlineData("FENCE", EnumDeviceType.Fence)]
    [InlineData(" Gate ", EnumDeviceType.Gate)]
    public void should_ignore_case_and_padding_when_parsing_type_device(string text, EnumDeviceType expected)
    {
        Assert.Equal(expected, DeviceTypeResolver.Resolve(text, categoryDevice: null));
    }

    [Fact]
    public void should_prefer_type_device_over_category_when_both_present()
    {
        // 종류가 카테고리보다 좁다 — 전문이 있으면 전문이 이긴다.
        var resolved = DeviceTypeResolver.Resolve("SmartSensor", "sensor");

        Assert.Equal(EnumDeviceType.SmartSensor, resolved);
    }

    // ── ② 7.0+ 참조: type_device 가 없거나 못 읽으면 카테고리로 복원 ──

    [Theory]
    [InlineData("controller", EnumDeviceType.Controller)]
    [InlineData("camera", EnumDeviceType.IpCamera)]
    [InlineData("speaker", EnumDeviceType.IpSpeaker)]
    [InlineData("enclosure", EnumDeviceType.Enclosure)]
    [InlineData("lamp", EnumDeviceType.Lamp)]
    [InlineData("gate", EnumDeviceType.Gate)]
    [InlineData("GATE", EnumDeviceType.Gate)]
    public void should_resolve_from_category_device_when_type_device_missing(string category, EnumDeviceType expected)
    {
        Assert.Equal(expected, DeviceTypeResolver.Resolve(typeDevice: null, category));
    }

    [Theory]
    [InlineData("SPEED_DOME", "camera", EnumDeviceType.IpCamera)]   // 종류축 값이 type_device 자리에 새어 들어와도
    [InlineData("NONE", "lamp", EnumDeviceType.Lamp)]               // NONE 은 미복원 취급
    [InlineData("", "gate", EnumDeviceType.Gate)]
    public void should_fall_back_to_category_when_type_device_is_unreadable(string typeDevice, string category, EnumDeviceType expected)
    {
        Assert.Equal(expected, DeviceTypeResolver.Resolve(typeDevice, category));
    }

    [Theory]
    [InlineData("sensor")]      // 의도적 미매핑 — Fence·Multi·PIR… 중 하나를 고르면 틀린 종류를 단정하게 된다
    [InlineData("etc")]
    [InlineData("hovercraft")]  // 미지 어휘
    [InlineData("")]
    [InlineData(null)]
    public void should_return_null_when_category_cannot_pin_a_single_type(string? category)
    {
        Assert.Null(DeviceTypeResolver.Resolve(typeDevice: null, category));
    }

    [Fact]
    public void should_resolve_from_dto_when_given_base_device_dto()
    {
        var dto = new GateDeviceDto { TypeDevice = null, CategoryDevice = "gate" };

        Assert.Equal(EnumDeviceType.Gate, DeviceTypeResolver.Resolve(dto));
    }

    // ── ③ 역방향: 6.3 응답에는 category_device 가 없다 — 옛 종류에서 판별자를 유도한다 (FR-04 키) ──

    [Theory]
    [InlineData(EnumDeviceType.Controller, EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceType.IoController, EnumDeviceCategory.Sensor)]   // DeviceModelConverter 와 같은 분류(센서 계열)
    [InlineData(EnumDeviceType.IpCamera, EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceType.IpSpeaker, EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceType.Enclosure, EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceType.Lamp, EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceType.Gate, EnumDeviceCategory.Gate)]
    [InlineData(EnumDeviceType.Fence, EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceType.Multi, EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceType.SmartMultisensor2, EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceType.Radar, EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceType.Cable, EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceType.Fence_Group, EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceType.NONE, EnumDeviceCategory.None)]
    public void should_derive_category_from_legacy_type(EnumDeviceType type, EnumDeviceCategory expected)
    {
        Assert.Equal(expected, DeviceTypeResolver.CategoryOf(type));
    }

    [Theory]
    [InlineData("camera", null, EnumDeviceCategory.Camera)]          // 7.0+: 판별자가 있으면 그것이 정본
    [InlineData(null, "IpCamera", EnumDeviceCategory.Camera)]        // 6.3: 옛 종류에서 유도
    [InlineData("lamp", "IpCamera", EnumDeviceCategory.Lamp)]        // 둘 다 있으면 판별자가 이긴다(경로가 정본)
    [InlineData(null, null, EnumDeviceCategory.None)]
    [InlineData("hovercraft", "Fence", EnumDeviceCategory.Sensor)]   // 미지 판별자 → 옛 종류로
    public void should_resolve_category_with_discriminator_first(string? category, string? typeDevice, EnumDeviceCategory expected)
    {
        Assert.Equal(expected, DeviceTypeResolver.ResolveCategory(category, typeDevice));
    }
}
