namespace Ironwall.Dotnet.Libraries.Messages.Defines.Commons;

/// <summary>
/// Event DTO 공통 인터페이스
/// <para>DetectionEventDto와 MalfunctionEventDto의 공통 속성 정의</para>
/// </summary>
public interface IEventDto
{
    /// <summary>
    /// 데이터베이스 ID
    /// </summary>
    int Id { get; set; }

    /// <summary>
    /// 이벤트 그룹
    /// </summary>
    string GroupEvent { get; set; }

    /// <summary>
    /// 이벤트 타입
    /// </summary>
    string TypeEvent { get; set; }

    /// <summary>
    /// Controller ID
    /// </summary>
    int Controller { get; set; }

    /// <summary>
    /// Sensor ID
    /// </summary>
    int Sensor { get; set; }

    /// <summary>
    /// 디바이스 타입
    /// </summary>
    string TypeDevice { get; set; }

    /// <summary>
    /// 시퀀스 번호
    /// </summary>
    int Sequence { get; set; }

    /// <summary>
    /// 조치 여부
    /// </summary>
    string ActionReported { get; set; }

    /// <summary>
    /// 생성일시
    /// </summary>
    string? CreatedAt { get; set; }

    /// <summary>
    /// 수정일시
    /// </summary>
    string? UpdatedAt { get; set; }
}
