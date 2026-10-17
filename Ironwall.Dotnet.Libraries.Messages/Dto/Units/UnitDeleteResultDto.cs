using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Units;
/****************************************************************************
   Purpose      : 부대 삭제 응답 DTO — GOP API 8.0 §11-A (DELETE /api/units/{unit_id})
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <c>DELETE /api/units/{unit_id}</c> 의 응답 <c>data</c> — <c>{"id": unit_id}</c> 하나뿐이다
/// (배포 스웨거는 <c>ApiSingleResponse[dict]</c> 로 느슨하게 선언한다).
/// </summary>
/// <remarks>
/// <para>⚠ <b>매달린 것이 있으면 409</b> 다 — 자식 부대·장비·장비 그룹·서버·이벤트·조치·억제 일정·시스템 이벤트
/// 8종을 세고, 0 이 아닌 종류만 <c>error.details.counts</c> 에 담아 준다.
/// 서버 정책은 <b>퇴역 = 삭제가 아니라 <c>is_enable=false</c></b> 다.</para>
/// </remarks>
public class UnitDeleteResultDto
{
    /// <summary>삭제된 부대 id.</summary>
    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }
}
