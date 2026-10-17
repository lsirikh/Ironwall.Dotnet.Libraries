using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

/// <summary>
/// 사용자 그룹 DTO (§9.4, GET /api/user-groups). 실서버 확인 필드.
/// user_count 는 목록에서 null(상세 호출 시 채워짐, B-5). — GOP-00 PRD FR-19
/// </summary>
public class UserGroupDto
{
    [JsonProperty("id")] public int Id { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("permissions")] public PermissionsDto? Permissions { get; set; }
    [JsonProperty("is_active")] public bool IsActive { get; set; }
    [JsonProperty("user_count")] public int? UserCount { get; set; }
    [JsonProperty("created_at")] public string? CreatedAt { get; set; }
    [JsonProperty("updated_at")] public string? UpdatedAt { get; set; }

    /// <summary>
    /// 콤보 항목 · 화면 읽기 프로그램 이름 — <c>DisplayMemberPath</c> 는 그리는 글자만 바꾸고 UIA 이름은 이 값을 쓴다.
    /// 계정 콘솔 [부여] 레일의 [그룹] 콤보(<c>DisplayMemberPath="Name"</c>)가 항목을 전부 타입 이름으로 읽혔다. 비면 "그룹 #id".
    /// </summary>
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? $"그룹 #{Id}" : Name;
}
