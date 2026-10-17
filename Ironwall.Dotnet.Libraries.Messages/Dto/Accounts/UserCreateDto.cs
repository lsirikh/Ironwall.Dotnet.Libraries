using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>POST /api/users 요청 본문 (관리자 계정 생성, §9.6). — GOP-00 PRD FR-19</summary>
public class UserCreateDto
{
    [JsonProperty("login_id")] public string LoginId { get; set; } = string.Empty;
    [JsonProperty("password")] public string Password { get; set; } = string.Empty;
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("email")] public string? Email { get; set; }
    [JsonProperty("department")] public string? Department { get; set; }
    [JsonProperty("position")] public string? Position { get; set; }
    [JsonProperty("employee_number")] public string? EmployeeNumber { get; set; }
    [JsonProperty("phone")] public string? Phone { get; set; }
    [JsonProperty("photo_url", NullValueHandling = NullValueHandling.Ignore)] public string? PhotoUrl { get; set; }
    [JsonProperty("role")] public string? Role { get; set; }
    [JsonProperty("group_id", NullValueHandling = NullValueHandling.Ignore)] public int? GroupId { get; set; }
}
