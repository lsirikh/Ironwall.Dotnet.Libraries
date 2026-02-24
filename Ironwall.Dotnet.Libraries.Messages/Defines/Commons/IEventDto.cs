using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

namespace Ironwall.Dotnet.Libraries.Messages.Defines.Commons;

/// <summary>
/// Event DTO 공통 인터페이스 (최소 속성)
/// <para>ConnectionEventDto 포함 모든 이벤트가 구현</para>
/// </summary>
public interface IEventDto
{
    int Id { get; set; }
    string TypeEvent { get; set; }
    string? CreatedAt { get; set; }
    string? UpdatedAt { get; set; }
}

/// <summary>
/// 조치보고 가능한 Event DTO 인터페이스
/// <para>DetectionEventDto, MalfunctionEventDto가 구현 (ConnectionEventDto 제외)</para>
/// </summary>
public interface IActionReportableEventDto : IEventDto
{
    string? ActionReported { get; set; }
}

/// <summary>
/// Device를 포함하는 Event DTO 인터페이스
/// <para>DetectionEventDto, MalfunctionEventDto, ConnectionEventDto가 구현</para>
/// </summary>
public interface IDeviceEventDto : IEventDto
{
    BaseDeviceDto? Device { get; set; }
    string? DeviceDescription { get; set; }
}
