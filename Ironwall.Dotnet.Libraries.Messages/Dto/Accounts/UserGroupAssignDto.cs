using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 계정 → 그룹 상시 배정/해제 요청 DTO (PUT /api/users/{id}, ADMIN). group_id 를 <b>항상</b> 직렬화(null=그룹 해제).
/// ⚠ <see cref="UserUpdateDto"/> 는 group_id 에 NullValueHandling.Ignore → null 미전송(해제 불가)이라 배정 전용 DTO 사용. — FR-07
/// </summary>
public class UserGroupAssignDto
{
    [JsonProperty("group_id")] public int? GroupId { get; set; }
}
