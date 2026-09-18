using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// PUT /api/users/{id} 요청 본문 (관리자 부분수정, <b>10 Optional 필드</b>). null 필드는 미전송.
/// PATCH 는 서버 미구현(PUT-only). — GOP-00 PRD FR-19
/// <para>⚠ <b>login_id 는 이 본문에 없다</b>(서버 <c>AccountUserUpdate</c> = name·email·department·position·
/// employee_number·photo_url·phone·role·group_id·is_active 10키, <c>additionalProperties:false</c>/<c>extra="forbid"</c>).
/// 배포 8.0.1 은 모르는 키를 422 <c>UNKNOWN_FIELD</c> 로 거부하고, 운영 6.3.2 는 무시한다 —
/// 어느 판본에서도 보낼 이유가 없으므로 <b>필드를 두지 않는다</b>. 로그인 ID 변경 경로는 서버에 존재하지 않는다
/// (계정 편집 다이얼로그의 아이디 입력도 읽기 전용). 근거: 배포 Swagger 8.0.1/6.3.2 · 명세 §9.3.1.</para>
/// </summary>
public class UserUpdateDto
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string? Name { get; set; }
    [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)] public string? Email { get; set; }
    [JsonProperty("department", NullValueHandling = NullValueHandling.Ignore)] public string? Department { get; set; }
    [JsonProperty("position", NullValueHandling = NullValueHandling.Ignore)] public string? Position { get; set; }
    [JsonProperty("employee_number", NullValueHandling = NullValueHandling.Ignore)] public string? EmployeeNumber { get; set; }
    [JsonProperty("photo_url", NullValueHandling = NullValueHandling.Ignore)] public string? PhotoUrl { get; set; }
    [JsonProperty("phone", NullValueHandling = NullValueHandling.Ignore)] public string? Phone { get; set; }
    [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)] public string? Role { get; set; }
    [JsonProperty("group_id", NullValueHandling = NullValueHandling.Ignore)] public int? GroupId { get; set; }
    [JsonProperty("is_active", NullValueHandling = NullValueHandling.Ignore)] public bool? IsActive { get; set; }
}
