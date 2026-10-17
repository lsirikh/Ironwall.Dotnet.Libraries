using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// PUT /api/users/me 요청 본문 (본인 프로필 수정, C-5: 6필드, extra=forbid).
/// ⚠ photo_url 은 서버 미반영 버그(C-5, v4.7 핫픽스 전). null 필드는 미전송. — GOP-00 PRD FR-17
/// </summary>
public class UserSelfUpdateDto
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] public string? Name { get; set; }
    [JsonProperty("email", NullValueHandling = NullValueHandling.Ignore)] public string? Email { get; set; }
    [JsonProperty("department", NullValueHandling = NullValueHandling.Ignore)] public string? Department { get; set; }
    [JsonProperty("position", NullValueHandling = NullValueHandling.Ignore)] public string? Position { get; set; }
    [JsonProperty("phone", NullValueHandling = NullValueHandling.Ignore)] public string? Phone { get; set; }
    [JsonProperty("photo_url", NullValueHandling = NullValueHandling.Ignore)] public string? PhotoUrl { get; set; }
}
