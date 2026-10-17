namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface IGateDeviceViewModel : IDeviceViewModel
{
    /// <summary>문 위치 — 구동부 부품의 관측 상태(<c>OPEN</c>·<c>CLOSED</c>·<c>RUNNING</c>). 읽기 전용.</summary>
    string DoorPosition { get; }

    /// <summary>결선 방식(<c>connection.type</c>). 읽기 전용.</summary>
    string? ConnectionType { get; }

    /// <summary>상위 장비 id(<c>connection.parent_device_id</c>). 읽기 전용.</summary>
    int? ParentDeviceId { get; }

    /// <summary>상위 장비의 채널(<c>connection.channel</c>). 읽기 전용.</summary>
    int? Channel { get; }
}
