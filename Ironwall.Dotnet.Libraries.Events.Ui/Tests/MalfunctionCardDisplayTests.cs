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
/// - D1: ControllerDeviceNumber가 controller_id(FK)로 DeviceProvider에서 번호를 재해석(중첩 nav 미하이드레이션 대비)
/// - D2: SensorDisplay 블랙리스트 — FAULT_ETC 등 기타 사유에서도 센서번호 표시
/// - D1/D3: ConvertDeviceFromDto 폴백(장비 Provider 미스)에서 controller_id로 컨트롤러 연결
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
            Id = 1,
            DeviceNumber = number,
            DeviceType = EnumDeviceType.Fence,
            Controller = new ControllerDeviceModel { Id = controllerId, DeviceNumber = controllerNumber }
        };

    // ─────────────── D2: SensorDisplay 블랙리스트 ───────────────

    [Theory]
    [InlineData(EnumFaultType.FAULT_FENCE, 5)]
    [InlineData(EnumFaultType.FAULT_MULTI, 5)]
    [InlineData(EnumFaultType.FAULT_ETC, 5)]              // 핵심 회귀: 화이트리스트였다면 null
    [InlineData(EnumFaultType.FAULT_CONTROLLER, null)]   // 순수 제어기 장애 — 센서 없음(의도)
    [InlineData(EnumFaultType.FAULT_CABLE_CUTTING, null)]
    public void should_show_sensor_number_for_non_controller_faults_when_reason_varies(EnumFaultType reason, int? expected)
    {
        var card = BuildCard(reason, Sensor(number: 5, controllerId: 10, controllerNumber: 3));
        Assert.Equal(expected, card.SensorDisplay);
    }

    // ─────────────── 제어기/센서 정상 표시 (하이드레이션 완료) ───────────────

    [Fact]
    public void should_show_linked_controller_number_when_sensor_fault_and_controller_hydrated()
    {
        var card = BuildCard(EnumFaultType.FAULT_FENCE, Sensor(number: 5, controllerId: 10, controllerNumber: 3));
        Assert.Equal(3, card.ControllerDisplay);
        Assert.Equal(5, card.SensorDisplay);
    }

    [Fact]
    public void should_show_device_own_number_when_controller_fault()
    {
        var controller = new ControllerDeviceModel { Id = 10, DeviceNumber = 3, DeviceType = EnumDeviceType.Controller };
        var card = BuildCard(EnumFaultType.FAULT_CONTROLLER, controller);
        Assert.Equal(3, card.ControllerDisplay);
        Assert.Null(card.SensorDisplay);
    }

    // ─────────────── D1: 미하이드레이션 시 Provider 폴백 ───────────────

    [Fact]
    public void should_resolve_controller_number_from_provider_when_nested_controller_unhydrated()
    {
        // Provider엔 정식 컨트롤러(번호=3) 존재
        _deviceProvider.Add(new ControllerDeviceModel { Id = 10, DeviceNumber = 3, DeviceType = EnumDeviceType.Controller });
        // 카드의 센서는 controller_id(FK)만 있고 번호=0 (폴백/미하이드레이션 상황)
        var card = BuildCard(EnumFaultType.FAULT_FENCE, Sensor(number: 5, controllerId: 10, controllerNumber: 0));
        // 수정 전이면 null(중첩 번호=0) → 수정 후 Provider에서 재해석 → 3
        Assert.Equal(3, card.ControllerDisplay);
    }

    [Fact]
    public void should_use_nested_controller_number_directly_when_hydrated_even_without_provider()
    {
        // Provider 비어있어도 중첩 nav에 번호가 있으면 그대로 사용(빠른 경로)
        var card = BuildCard(EnumFaultType.FAULT_FENCE, Sensor(number: 5, controllerId: 10, controllerNumber: 7));
        Assert.Equal(7, card.ControllerDisplay);
    }

    [Fact]
    public void should_return_null_controller_when_no_link_and_provider_miss()
    {
        // controller_id 없음(0) + Provider 미스 → 복구 불가(정직하게 공란). 센서 번호는 유지.
        var card = BuildCard(EnumFaultType.FAULT_FENCE, Sensor(number: 5, controllerId: 0, controllerNumber: 0));
        Assert.Null(card.ControllerDisplay);
        Assert.Equal(5, card.SensorDisplay);
    }

    // ─────────────── D1/D3: DTO 변환 폴백(장비 Provider 미스)에서 controller_id 연결 ───────────────

    [Fact]
    public void should_link_controller_from_controller_id_when_device_not_in_provider()
    {
        // Provider엔 컨트롤러만 있고 해당 센서(Id=999)는 없음 → ConvertDeviceFromDto 폴백 경로
        _deviceProvider.Add(new ControllerDeviceModel { Id = 10, DeviceNumber = 3, DeviceType = EnumDeviceType.Controller });

        var dto = new MalfunctionEventDto
        {
            Id = 1,
            CreatedAt = "2026-07-31T10:00:00+09:00",
            TypeEvent = "Fault",
            ActionReported = "False",
            Reason = "FAULT_FENCE",
            Device = new BaseDeviceDto { Id = 999, TypeDevice = "Fence", NumberDevice = 5, ControllerId = 10 }
        };

        var model = dto.ToMalfunctionEventModel(_deviceProvider);
        var sensor = model.Device as ISensorDeviceModel;

        Assert.NotNull(sensor);
        Assert.Equal(5, sensor!.DeviceNumber);          // 센서 자신의 번호(number_device)
        Assert.Equal(10, sensor.Controller?.Id);         // controller_id 보존
        Assert.Equal(3, sensor.Controller?.DeviceNumber); // Provider 정식 인스턴스로 번호 복구
    }
}
