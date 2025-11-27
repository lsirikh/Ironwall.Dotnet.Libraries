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
    [JsonProperty("name_event", Order = 2)]
    public string NameEvent { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트 그룹
    /// </summary>
    [JsonProperty("group_event", Order = 3)]
    public string GroupEvent { get; set; } = string.Empty;

    /// <summary>
    /// 이벤트 카테고리
    /// </summary>
    [JsonProperty("category_event", Order = 4)]
    public string CategoryEvent { get; set; } = string.Empty;

    /// <summary>
    /// 설명
    /// </summary>
    [JsonProperty("description", Order = 5)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 활성화 상태
    /// </summary>
    [JsonProperty("status", Order = 6)]
    public bool Status { get; set; }
}