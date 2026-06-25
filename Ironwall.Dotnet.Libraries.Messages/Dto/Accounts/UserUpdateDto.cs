using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// PUT /api/users/{id} 요청 본문 (관리자 부분수정, C-1: 11 Optional 필드). null 필드는 미전송.
/// PATCH 는 서버 미구현(PUT-only). — GOP-00 PRD FR-19
/// </summary>
public class UserUpdateDto
{
    [JsonProperty("login_id", NullValueHandling = NullValueHandling.Ignore)] public string? LoginId { get; set; }
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
