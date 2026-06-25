using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// GOP REST Auth API 래퍼 (DeviceApiService 패턴: IApiService 주입 + ToApiResponseAsync).
/// <para>ApiAccountGateway 의 내부 협력자 — VM 에 직접 주입되지 않는다(§2.4.1).</para>
/// User/UserGroup/UserSession/AuditLogs CRUD 는 IMPL-11 에서 확장. — GOP-00 PRD FR-7
/// </summary>
public interface IAccountApiService
{
    /// <summary>POST /api/auth/login — data.access_token + data.user(권한 포함).</summary>
    Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default);

    /// <summary>POST /api/auth/refresh — 토큰만(user/permissions 없음, A-3). BearerAuthHandler 가 401 시 호출.</summary>
    Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>POST /api/auth/logout — best-effort(access_token 서버 블랙리스트 없음, §2.3.3).</summary>
    Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default);

    /// <summary>GET /api/auth/me — 평면(envelope 없음, A-4) 응답. 실패 시 null. permissions 미포함.</summary>
    Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default);

    // ── User CRUD (FR-19) ──
    /// <summary>GET /api/users — 계정 목록(pagination meta 없을 수 있음, B-8).</summary>
    Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default);
    /// <summary>POST /api/users — 계정 생성. 중복 login_id 면 400(B-4 우회).</summary>
    Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default);
    /// <summary>PUT /api/users/{id} — 부분수정(PATCH 미구현).</summary>
    Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default);
    /// <summary>DELETE /api/users/{id} — 본문/비번게이트 없음(§2.4.4).</summary>
    Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default);
    /// <summary>POST /api/users/{id}/reset-password — {new_password}, 응답 {success:true}(C-3).</summary>
    Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default);

    // ── 본인 프로필 (FR-17) ──
    /// <summary>GET /api/users/me — 본인 프로필(envelope, A-4). permissions 미포함.</summary>
    Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default);
    /// <summary>PUT /api/users/me — 본인 프로필 수정. ⚠ photo_url 미반영(C-5).</summary>
    Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default);
    /// <summary>PUT /api/users/me/password — {current_password,new_password(min6)}. 서버 세션무효화 없음(F07-01).</summary>
    Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    // ── 관리 화면용 조회 (FR-19, 후속 GOP-05 UI) ──
    /// <summary>GET /api/user-groups — 그룹 목록(권한 nested {modules,device_groups}).</summary>
    Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default);
    /// <summary>GET /api/user-sessions — 세션 목록(ADMIN).</summary>
    Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(CancellationToken ct = default);
}
