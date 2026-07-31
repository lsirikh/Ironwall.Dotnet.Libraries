using Xunit;
using Moq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 장애 카드 flip 뒷장 제어기/센서 표시 로직 회귀 테스트.
/// - 표시 분류는 <b>장비 타입 기준(사유 무관)</b> — reason은 장비 타입을 신뢰성 있게 나타내지 못함
///   (실측: FAULT_CABLE_CUTTING이 Fence 센서에 실려 옴).
/// - ControllerDeviceNumber는 controller_id(FK)로 DeviceProvider에서 번호를 재해석(중첩 nav 미하이드레이션 대비).
/// - ConvertDeviceFromDto 폴백(장비 Provider 미스)에서 controller_id로 컨트롤러 연결.
/// </summary>
[Collection("IoC-Dependent")]
public class MalfunctionCardDisplayTests : IDisposable
{
    private readonly DeviceProvider _deviceProvider = new();

    public MalfunctionCardDisplayTests()
    {
        var mockSetup = new Mock<IEventSetupModel>();
        mockSetup.Setup(s => s.TimeDiscardSec).Returns(30);
        mockSetup.Setup(s => s.IsAutoEventDiscard).Returns(false);
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return null!;
            if (type == typeof(EventSetupModel)) return new EventSetupModel(mockSetup.Object);
            if (type == typeof(DeviceProvider)) return _deviceProvider;
            return null!;
        };
        IoC.GetAllInstances = type => System.Linq.Enumerable.Empty<object>();
        IoC.BuildUp = obj => { };
    }

    public void Dispose()
    {
        IoC.GetInstance = (type, key) => throw new InvalidOperationException("IoC is not initialized.");
        IoC.GetAllInstances = type => throw new InvalidOperationException("IoC is not initialized.");
        IoC.BuildUp = obj => throw new InvalidOperationException("IoC is not initialized.");
    }

    private static MalfunctionEventCardViewModel BuildCard(EnumFaultType reason, IBaseDeviceModel device)
        => new(new MalfunctionEventModel
        {
            Reason = reason,
            Device = device,
            MessageType = EnumEventType.Fault,
            DateTime = DateTime.Now
        });

    private static SensorDeviceModel Sensor(int number, int controllerId, int controllerNumber)
        => new()
        {
            Id = 1802,
            DeviceNumber = number,
            DeviceType = EnumDeviceType.Fence,
            Controller = new ControllerDeviceModel { Id = controllerId, DeviceNumber = controllerNumber }
        };

    // ─────────────── 장비 타입 기준 분류 (사유 무관) ───────────────

    /// <summary>
    /// 센서 장비: 어떤 사유든 센서 자기 번호(센서 칸) + 연결 제어기 번호(제어기 칸).
    /// 실측 버그 재현: FAULT_CABLE_CUTTING이 Fence 센서(번호 2, 상위 제어기 1351 번호 1)에 실려도 정상 배정.
    /// </summary>
    [Theory]
    [InlineData(EnumFaultType.FAULT_FENCE)]
    [InlineData(EnumFaultType.FAULT_MULTI)]
    [InlineData(EnumFaultType.FAULT_ETC)]
    [InlineData(EnumFaultType.FAULT_CABLE_CUTTING)]
    [InlineData(EnumFaultType.FAULT_CONTROLLER)]
    public void should_show_sensor_number_and_linked_controller_when_device_is_sensor(EnumFaultType reason)
    {
        var card = BuildCard(reason, Sensor(number: 2, controllerId: 1351, controllerNumber: 1));
        Assert.Equal(2, card.SensorDisplay);      // 센서 자기 번호
        Assert.Equal(1, card.ControllerDisplay);  // 연결된 제어기 번호
    }

    /// <summary>제어기 장비: 어떤 사유든 제어기 자기 번호(제어기 칸) + 센서 칸 null.</summary>
    [Theory]
    [InlineData(EnumFaultType.FAULT_CONTROLLER)]
    [InlineData(EnumFaultType.FAULT_CABLE_CUTTING)]
    [InlineData(EnumFaultType.FAULT_ETC)]
    public void should_show_controller_own_number_and_null_sensor_when_device_is_controller(EnumFaultType reason)
    {
        var controller = new ControllerDeviceModel { Id = 1351, DeviceNumber = 1, DeviceType = EnumDeviceType.Controller };
        var card = BuildCard(reason, controller);
        Assert.Equal(1, card.ControllerDisplay);  // 제어기 자기 번호
        Assert.Null(card.SensorDisplay);
    }

    // ─────────────── ControllerDeviceNumber: controller_id → Provider 재해석 ───────────────

    [Fact]
    public void should_resolve_controller_number_from_provider_when_nested_controller_unhydrated()
    {
        // Provider엔 정식 컨트롤러(번호=1) 존재
        _deviceProvider.Add(new ControllerDeviceModel { Id = 1351, DeviceNumber = 1, DeviceType = EnumDeviceType.Controller });
        // 카드 센서의 중첩 Controller는 FK(id)만 있고 번호=0 (폴백/미하이드레이션)
        var card = BuildCard(EnumFaultType.FAULT_CABLE_CUTTING, Sensor(number: 2, controllerId: 1351, controllerNumber: 0));
        Assert.Equal(1, card.ControllerDisplay);  // Provider에서 재해석 → 1
        Assert.Equal(2, card.SensorDisplay);
    }

    [Fact]
    public void should_use_nested_controller_number_directly_when_hydrated_even_without_provider()
    {
        // Provider 비어있어도 중첩 nav에 번호가 있으면 그대로 사용(빠른 경로)
        var card = BuildCard(EnumFaultType.FAULT_FENCE, Sensor(number: 2, controllerId: 1351, controllerNumber: 7));
        Assert.Equal(7, card.ControllerDisplay);
    }

    [Fact]
    public void should_return_null_controller_but_keep_sensor_when_no_controller_link_and_provider_miss()
    {
        // controller_id 없음(0) + Provider 미스 → 제어기 복구 불가(정직하게 공란). 센서 번호는 유지.
        var card = BuildCard(EnumFaultType.FAULT_FENCE, Sensor(number: 2, controllerId: 0, controllerNumber: 0));
        Assert.Null(card.ControllerDisplay);
        Assert.Equal(2, card.SensorDisplay);
    }

    // ─────────────── DTO 변환 폴백(장비 Provider 미스)에서 controller_id 연결 ───────────────

    [Fact]
    public void should_link_controller_from_controller_id_when_device_not_in_provider()
    {
        // Provider엔 컨트롤러만 있고 해당 센서(Id=1802)는 없음 → ConvertDeviceFromDto 폴백 경로
        _deviceProvider.Add(new ControllerDeviceModel { Id = 1351, DeviceNumber = 1, DeviceType = EnumDeviceType.Controller });

        // 실측 메시지 형태: device.controller_id=1351(FLAT), detail=null
        var dto = new MalfunctionEventDto
        {
            Id = 52027,
            CreatedAt = "2026-07-31T21:48:41.814465+09:00",
            TypeEvent = "Fault",
            ActionReported = "False",
            Reason = "FAULT_CABLE_CUTTING",
            Detail = null,
            Device = new BaseDeviceDto { Id = 1802, TypeDevice = "Fence", NumberDevice = 2, ControllerId = 1351 }
        };

        var model = dto.ToMalfunctionEventModel(_deviceProvider);
        var sensor = model.Device as ISensorDeviceModel;

        Assert.NotNull(sensor);
        Assert.Equal(2, sensor!.DeviceNumber);            // 센서 자기 번호(number_device)
        Assert.Equal(1351, sensor.Controller?.Id);         // controller_id 보존
        Assert.Equal(1, sensor.Controller?.DeviceNumber);  // Provider 정식 인스턴스로 번호 복구
        // detail=null → 4개 값 0,0,0,0
        Assert.Equal(0, model.FirstStart);
        Assert.Equal(0, model.FirstEnd);
        Assert.Equal(0, model.SecondStart);
        Assert.Equal(0, model.SecondEnd);
    }
}
