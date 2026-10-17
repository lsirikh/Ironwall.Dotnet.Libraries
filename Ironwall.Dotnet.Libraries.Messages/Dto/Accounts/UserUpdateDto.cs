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
/// <para><b>비우기(명시적 null)</b>: 서버는 <b>보낸 키만</b> 반영하고, 보낸 값이 null 이면 그 칸을 비운다
/// (<c>users.py update_user</c> 의 <c>model_fields_set</c> — role · is_active 는 null 이면 무시). 기본은 종전대로 null 을 싣지 않으며,
/// 비울 키만 <see cref="ClearFields"/> 에 서버 필드 이름으로 넣으면 <c>"키": null</c> 로 실린다.
/// 종전(전부 Ignore)에는 콘솔에서 이메일 · 전화를 지워도 키가 빠져 "적용했습니다" 뒤 서버 값이 그대로였다(라이브 실측 2026-09-24).</para>
/// </summary>
public class UserUpdateDto
{
    [JsonProperty("name")] public string? Name { get; set; }
    [JsonProperty("email")] public string? Email { get; set; }
    [JsonProperty("department")] public string? Department { get; set; }
    [JsonProperty("position")] public string? Position { get; set; }
    [JsonProperty("employee_number")] public string? EmployeeNumber { get; set; }
    [JsonProperty("photo_url")] public string? PhotoUrl { get; set; }
    [JsonProperty("phone")] public string? Phone { get; set; }
    [JsonProperty("role")] public string? Role { get; set; }
    [JsonProperty("group_id")] public int? GroupId { get; set; }
    [JsonProperty("is_active")] public bool? IsActive { get; set; }

    /// <summary>값이 null 이어도 <c>"키": null</c> 로 실을 서버 필드 이름(비우기). 비어 있으면 null 은 전부 생략한다.</summary>
    [JsonIgnore] public HashSet<string> ClearFields { get; } = new(StringComparer.Ordinal);

    // Newtonsoft 는 ShouldSerialize{속성}() 을 먼저 묻는다 — null 이면 싣지 않되, 비우라고 한 키만 null 로 싣는다.
    public bool ShouldSerializeName() => Name is not null || ClearFields.Contains("name");
    public bool ShouldSerializeEmail() => Email is not null || ClearFields.Contains("email");
    public bool ShouldSerializeDepartment() => Department is not null || ClearFields.Contains("department");
    public bool ShouldSerializePosition() => Position is not null || ClearFields.Contains("position");
    public bool ShouldSerializeEmployeeNumber() => EmployeeNumber is not null || ClearFields.Contains("employee_number");
    public bool ShouldSerializePhotoUrl() => PhotoUrl is not null || ClearFields.Contains("photo_url");
    public bool ShouldSerializePhone() => Phone is not null || ClearFields.Contains("phone");
    public bool ShouldSerializeRole() => Role is not null;          // 서버: role null 은 무시 — 비우기 대상 아님
    public bool ShouldSerializeGroupId() => GroupId is not null || ClearFields.Contains("group_id");
    public bool ShouldSerializeIsActive() => IsActive is not null;  // 서버: is_active null 은 무시 — 비우기 대상 아님
}
