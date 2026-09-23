using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// GOP <see cref="AuthUserDto"/> → 공유 <see cref="AccountModel"/> 매퍼 (FR-14).
/// <para>서버가 주지 않는 필드(Birth/Address/Company)는 기본값. Image←photo_url(URL). <c>Level</c>은 RoleMappingHelper 로 채워
/// 기존 AdminLevelAllowConverter 바인딩 호환을 유지한다(§2.4.5). Role/GroupId/IsLocked 등 GOP 전용값은
/// AuthResult/PermissionService 가 별도 보관(상태 VM 중복 방지).</para>
/// </summary>
public static class AccountDtoMapper
{
    /// <param name="d">서버 사용자.</param>
    /// <param name="apiBaseUrl">접속한 API 주소 — 주면 상대 <c>photo_url</c> 을 화면이 그릴 수 있는 절대 URL 로 만든다(<see cref="ServerPhotoUrl.ToDisplay"/>).</param>
    public static AccountModel ToAccountModel(AuthUserDto d, string? apiBaseUrl = null) => new()
    {
        Id = d.Id,
        Username = d.LoginId,
        Name = d.Name ?? string.Empty,
        EMail = d.Email,
        Department = d.Department,
        Position = d.Position,
        EmployeeNumber = d.EmployeeNumber,
        Phone = d.Phone,
        Level = RoleMappingHelper.ToLevel(RoleMappingHelper.ParseRole(d.Role)),
        Role  = RoleMappingHelper.ParseRole(d.Role),     // GOP 5단계 전체 보존(구분 표시/편집)
        Image = ServerPhotoUrl.ToDisplay(d.PhotoUrl, apiBaseUrl),   // 서버 상대 경로 → 접속 주소 기준 절대 URL(표시용)
        Used = d.IsActive ? EnumUsedType.USED : EnumUsedType.NOT_USED,
        IsLocked = d.IsLocked,                           // 잠금 상태 표시·해제 게이팅용(계정 목록)
        LockReason = d.LockReason,                       // 잠금 사유(툴팁)
    };

    /// <summary>AccountModel → POST /users 본문. role 은 Level(2단계)→5단계 매핑(ToRole).</summary>
    public static UserCreateDto ToUserCreateDto(IAccountModel m) => new()
    {
        LoginId = m.Username,
        Password = m.Password,
        Name = m.Name,
        Email = m.EMail,
        Department = m.Department,
        Position = m.Position,
        EmployeeNumber = m.EmployeeNumber,
        Phone = m.Phone,
        Role = ToServerRole(m),
        PhotoUrl = ToServerPhotoUrl(m.Image),
    };

    /// <summary>
    /// AccountModel → PUT /users/{id} 본문(부분수정). null 필드는 DTO에서 미전송.
    /// <para>⚠ <c>login_id</c> 는 <b>싣지 않는다</b> — 서버 <c>AccountUserUpdate</c> 에 그 키가 없고
    /// 배포 8.0.1 은 <c>extra="forbid"</c> 라 422 <c>UNKNOWN_FIELD</c> 였다(계정 편집 저장이 항상 실패).
    /// 로그인 ID 변경 엔드포인트는 서버에 없다.</para>
    /// </summary>
    public static UserUpdateDto ToUserUpdateDto(IAccountModel m) => new()
    {
        Name = m.Name,
        Email = m.EMail,
        Department = m.Department,
        Position = m.Position,
        EmployeeNumber = m.EmployeeNumber,
        Phone = m.Phone,
        Role = ToServerRole(m),
        PhotoUrl = ToServerPhotoUrl(m.Image),
    };

    /// <summary>콘솔 상세 칸의 서버 필드 중 비울 수 있는 것 — 서버가 null 을 "해제" 로 받는다(name · role · is_active 는 아님).</summary>
    public static IReadOnlyCollection<string> ClearableFields { get; } = new[] { "email", "department", "position", "employee_number", "phone" };

    /// <summary>
    /// AccountModel → PUT /users/{id} 본문 — <b>손댄 칸만</b> 싣는다(<paramref name="changedFields"/> = 서버 필드 이름).
    /// </summary>
    /// <remarks>
    /// <para>전체 모델을 싣는 <see cref="ToUserUpdateDto(IAccountModel)"/> 로 콘솔 [적용] 을 보내면 세 가지가 조용히 틀렸다(라이브 실측 2026-09-24):
    /// ① 상태(<c>is_active</c>)는 아예 실리지 않아 "적용했습니다" 뒤에도 서버 값이 그대로였고,
    /// ② 비운 칸은 null → 생략이라 이메일 · 전화를 지울 수 없었고,
    /// ③ 바꾸지 않은 <c>role</c> 이 늘 실려 <c>users:edit</c> 만 가진 비-ADMIN 편집자는 부서 하나 고쳐도 403
    ///    ("Only ADMIN role can change role or group assignment") 이었다.</para>
    /// <para>비운 칸은 <see cref="UserUpdateDto.ClearFields"/> 로 <c>"키": null</c> 을 싣는다 — 서버는 보낸 키만 반영하고 null 은 해제다.</para>
    /// </remarks>
    public static UserUpdateDto ToUserUpdateDto(IAccountModel m, IEnumerable<string> changedFields)
    {
        var dto = new UserUpdateDto();
        foreach (var field in (changedFields ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal))
        {
            switch (field)
            {
                case "name":            dto.Name = NullIfBlank(m.Name); break;   // 필수 칸 — 비우기는 폼이 막는다(빈 값이면 싣지 않음)
                case "email":           dto.Email = NullIfBlank(m.EMail); break;
                case "department":      dto.Department = NullIfBlank(m.Department); break;
                case "position":        dto.Position = NullIfBlank(m.Position); break;
                case "employee_number": dto.EmployeeNumber = NullIfBlank(m.EmployeeNumber); break;
                case "phone":           dto.Phone = NullIfBlank(m.Phone); break;
                case "role":            dto.Role = ToServerRole(m); break;
                case "is_active":       dto.IsActive = m.Used == EnumUsedType.USED; break;
                case "photo_url":       dto.PhotoUrl = ToServerPhotoUrl(m.Image); break;
                default: continue;      // group_id 등 이 본문이 다루지 않는 축 — 전용 경로가 있다
            }
            if (ClearableFields.Contains(field) && IsBlankValue(dto, field)) dto.ClearFields.Add(field);
        }
        return dto;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool IsBlankValue(UserUpdateDto dto, string field) => field switch
    {
        "email" => dto.Email is null,
        "department" => dto.Department is null,
        "position" => dto.Position is null,
        "employee_number" => dto.EmployeeNumber is null,
        "phone" => dto.Phone is null,
        _ => false,
    };

    /// <summary>AccountModel → PUT /users/me 본문(본인 6필드). Image→photo_url.</summary>
    public static UserSelfUpdateDto ToUserSelfUpdateDto(IAccountModel m) => new()
    {
        Name = m.Name,
        Email = m.EMail,
        Department = m.Department,
        Position = m.Position,
        Phone = m.Phone,
        PhotoUrl = ToServerPhotoUrl(m.Image),
    };

    /// <summary>
    /// 서버 photo_url 허용값은 <c>http://</c>·<c>https://</c>·<b><c>/api/users/photo/</c></b>(서버 자체 서빙 경로) 뿐이다
    /// (validator: "photo_url must start with http://, https://, or /api/users/photo/").
    /// <para>종전 허용목록의 <c>/static/profiles/</c> 는 <b>실존하지 않는 경로</b>로 v6.3 에서 폐기됐다 —
    /// 그 값을 보내면 422 이고, 반대로 서버가 채워 주는 <c>/api/users/photo/default.png</c> 를 되돌려 보낼 때는
    /// 허용목록에 없어 <b>null 로 떨어져 영구 미전송</b>됐다. 로컬 파일 경로는 여전히 미전송(사진 영속은 업로드 엔드포인트 담당).</para>
    /// <para>화면용으로 절대 URL 로 만든 서버 사진 주소는 <b>상대 경로로 되돌려</b> 보낸다 — 서버 8.0 이 없앤
    /// "DB 에 호스트가 박히는" 오염을 클라가 되살리지 않게(<see cref="ServerPhotoUrl.ToServer"/>).</para>
    /// </summary>
    private static string? ToServerPhotoUrl(string? image) => ServerPhotoUrl.ToServer(image);

    /// <summary>
    /// 전송용 role 정규화 — 서버 <c>EnumUserRole</c> 는 <b>ADMIN·USER 2종</b>뿐이다(레거시 5단계는 서버에서 삭제됨).
    /// <para>응답에 예상 밖 role 문자열이 오면 <c>RoleMappingHelper.ParseRole</c> 폴백이 <c>GUEST</c> 로 오염시키고,
    /// 그 값을 그대로 되돌려 보내면 다음 저장이 422 가 된다. 그래서 <b>전송 직전</b>에 ADMIN 이 아니면 USER 로 고정한다.</para>
    /// </summary>
    private static string ToServerRole(IAccountModel m)
    {
        var role = m.Role != EnumUserRole.UNDEFINED ? m.Role : RoleMappingHelper.ToRole(m.Level);
        return role == EnumUserRole.ADMIN ? "ADMIN" : "USER";
    }
}
