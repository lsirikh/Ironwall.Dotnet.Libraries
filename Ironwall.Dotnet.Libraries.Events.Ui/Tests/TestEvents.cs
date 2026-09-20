using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 콘솔 테스트가 쓰는 최소 이벤트 모델 — 서버 없이 세운다.
/// </summary>
internal static class TestEvents
{
    public static IBaseDeviceModel Device(int id = 101, string name = "정문 1구간")
        => new SensorDeviceModel { Id = id, DeviceNumber = id, DeviceName = name, DeviceType = EnumDeviceType.Fence };

    public static IDetectionEventModel Detection(int id, bool reported = false, EnumDetectionType result = EnumDetectionType.NONE)
        => new DetectionEventModel
        {
            Id = id,
            MessageType = EnumEventType.Intrusion,
            Status = reported ? EnumTrueFalse.True : EnumTrueFalse.False,
            Result = result,
            DateTime = new DateTime(2026, 9, 20, 12, 0, 0),
            Device = Device(),
        };

    public static IMalfunctionEventModel Malfunction(int id, bool reported = false)
        => new MalfunctionEventModel
        {
            Id = id,
            MessageType = EnumEventType.Fault,
            Status = reported ? EnumTrueFalse.True : EnumTrueFalse.False,
            Reason = EnumFaultType.FAULT_FENCE,
            DateTime = new DateTime(2026, 9, 20, 12, 0, 0),
            Device = Device(),
        };
}
