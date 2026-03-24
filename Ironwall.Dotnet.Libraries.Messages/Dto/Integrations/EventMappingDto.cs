using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;

/****************************************************************************
   Purpose      : Event Mapping DTO
   Created By   : GHLee
   Created On   : 11/12/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// Event Mapping DTO - 이벤트 매핑 설정
/// <para>GOP API의 이벤트 매핑 연동을 위한 데이터 전송 객체</para>
/// </summary>
public class EventMappingDto : BaseDto
{
    /// <summary>
    /// 데이터베이스 ID (자동 생성)
    /// </summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }

    /// <summary>
    /// 이벤트 이름
    /// </summary>
    [JsonProperty("name_event", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? NameEvent { get; set; }

    /// <summary>
    /// 장비그룹 ID (FK → DeviceGroup)
    /// </summary>
    [JsonProperty("device_group_id", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public int? DeviceGroupId { get; set; }

    /// <summary>
    /// 이벤트 매핑 카테고리
    /// </summary>
    [JsonProperty("category_event_mapping", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public string? CategoryEventMapping { get; set; }

    /// <summary>
    /// 설명
    /// </summary>
    [JsonProperty("description", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// 활성화 상태
    /// </summary>
    [JsonProperty("status", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public bool? Status { get; set; }

    /// <summary>
    /// 연동 카메라 목록
    /// </summary>
    [JsonProperty("cameras", Order = 7)]
    public List<EventMappingCameraDto>? Cameras { get; set; }

    /// <summary>
    /// 연동 스피커 목록
    /// </summary>
    [JsonProperty("speakers", Order = 8)]
    public List<EventMappingSpeakerDto>? Speakers { get; set; }

    /// <summary>
    /// 연동 경광등 목록
    /// </summary>
    [JsonProperty("lamps", Order = 9)]
    public List<EventMappingLampDto>? Lamps { get; set; }
}
