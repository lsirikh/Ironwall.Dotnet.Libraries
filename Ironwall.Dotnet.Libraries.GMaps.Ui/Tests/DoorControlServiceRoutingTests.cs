using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 문 개폐 명령의 라우팅 고정(device-console-v8 FR-01).
/// <para><b>특성화 테스트다</b> — 시나리오 분석의 ISSUE-20("미지 타입이 함체 명령으로 나간다")은 코드 확인 결과
/// 성립하지 않았다: <c>ResolveTopic</c> 이 문 없는 타입에 <c>null</c> 을 돌려 발행 자체를 취소하고,
/// <c>ResolveCommandName</c> 의 기본값(함체)은 그 뒤에만 도달한다. 이 동작이 깨지지 않게 못 박는다.</para>
/// <para>v7.0+ 통문은 <c>type_device</c> 가 없어 옛 매핑에선 <c>DeviceType=NONE</c> 이었고 개폐가 거부됐다 —
/// 그 복원은 매핑 계층 몫이다(<c>DeviceTypeResolver</c>, Devices.Ui <c>AxisMappingTests</c>).</para>
/// </summary>
public class DoorControlServiceRoutingTests
{
    [Fact]
    public void should_route_gate_to_gate_door_topic_and_command()
    {
        Assert.Equal(DoorControlService.GateTopic, DoorControlService.ResolveTopic(EnumDeviceType.Gate));
        Assert.Equal(DoorControlService.GateDoorSetCommand, DoorControlService.ResolveCommandName(EnumDeviceType.Gate));
        Assert.Equal("gate", DoorControlService.ResolveCategory(EnumDeviceType.Gate));
    }

    [Fact]
    public void should_route_enclosure_to_enclosure_door_topic_and_command()
    {
        Assert.Equal(DoorControlService.EnclosureTopic, DoorControlService.ResolveTopic(EnumDeviceType.Enclosure));
        Assert.Equal(DoorControlService.EnclosureDoorSetCommand, DoorControlService.ResolveCommandName(EnumDeviceType.Enclosure));
        Assert.Equal("enclosure", DoorControlService.ResolveCategory(EnumDeviceType.Enclosure));
    }

    [Fact]
    public void should_refuse_to_route_every_device_type_that_has_no_door()
    {
        foreach (EnumDeviceType type in Enum.GetValues<EnumDeviceType>())
        {
            if (type is EnumDeviceType.Gate or EnumDeviceType.Enclosure) continue;

            // topic 이 null 이면 SendAsync 가 발행 전에 취소한다 — 미지·무문 장비에 함체 명령이 나가지 않는다.
            Assert.Null(DoorControlService.ResolveTopic(type));
        }
    }

    [Theory]
    [InlineData("open", "OPEN")]
    [InlineData(" Close ", "CLOSE")]
    [InlineData("TOGGLE", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void should_accept_only_open_and_close_commands(string? command, string? expected)
    {
        Assert.Equal(expected, DoorControlService.NormalizeCommand(command));
    }
}
