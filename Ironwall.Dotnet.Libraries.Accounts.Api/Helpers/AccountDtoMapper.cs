using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// GOP <see cref="AuthUserDto"/> → 공유 <see cref="AccountModel"/> 매퍼 (FR-14).
/// <para>서버가 주지 않는 필드(Birth/Address/Image/Company)는 기본값. <c>Level</c>은 RoleMappingHelper 로 채워
/// 기존 AdminLevelAllowConverter 바인딩 호환을 유지한다(§2.4.5). Role/GroupId/IsLocked 등 GOP 전용값은
/// AuthResult/PermissionService 가 별도 보관(상태 VM 중복 방지).</para>
/// </summary>
public static class AccountDtoMapper
{
    public static AccountModel ToAccountModel(AuthUserDto d) => new()
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
        Used = d.IsActive ? EnumUsedType.USED : EnumUsedType.NOT_USED,
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
        Role = (m.Role != EnumUserRole.UNDEFINED ? m.Role : RoleMappingHelper.ToRole(m.Level)).ToString(),
    };

    /// <summary>AccountModel → PUT /users/{id} 본문(부분수정). null 필드는 DTO에서 미전송.</summary>
    public static UserUpdateDto ToUserUpdateDto(IAccountModel m) => new()
    {
        LoginId = m.Username,
        Name = m.Name,
        Email = m.EMail,
        Department = m.Department,
        Position = m.Position,
        EmployeeNumber = m.EmployeeNumber,
        Phone = m.Phone,
        Role = (m.Role != EnumUserRole.UNDEFINED ? m.Role : RoleMappingHelper.ToRole(m.Level)).ToString(),
    };

    /// <summary>AccountModel → PUT /users/me 본문(본인 6필드). Image→photo_url.</summary>
    public static UserSelfUpdateDto ToUserSelfUpdateDto(IAccountModel m) => new()
    {
        Name = m.Name,
        Email = m.EMail,
        Department = m.Department,
        Position = m.Position,
        Phone = m.Phone,
        PhotoUrl = m.Image,
    };
}
