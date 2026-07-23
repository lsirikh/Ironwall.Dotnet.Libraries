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

    /// <summary>GET /api/auth/me/permissions — 유효권한 스냅샷(modules/device_groups/valid_until/server_time).
    /// grant 만료로 stale 된 권한 재평가용(FR-GS-01/02). 기본구현=미지원(테스트 스텁 무수정).</summary>
    Task<ApiResponse<PermissionsSnapshotDto>> GetMyPermissionsAsync(CancellationToken ct = default)
        => throw new NotImplementedException();

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
    /// <summary>POST /api/users/{id}/unlock — 계정 잠금 해제(ADMIN, users:control). 응답 {success:true}. default=미구현(하위호환).</summary>
    Task<ApiResponse<object>> UnlockUserAsync(int id, CancellationToken ct = default)
        => throw new NotImplementedException();

    // ── 본인 프로필 (FR-17) ──
    /// <summary>GET /api/users/me — 본인 프로필(envelope, A-4). permissions 미포함.</summary>
    Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default);
    /// <summary>PUT /api/users/me — 본인 프로필 수정. ⚠ photo_url 미반영(C-5).</summary>
    Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default);
    /// <summary>POST /api/users/me/photo — 본인 프로필 사진 업로드(multipart). 서버가 photo_url(절대 URL) 갱신 후 사용자 반환.</summary>
    Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default);
    /// <summary>DELETE /api/users/me/photo — 본인 프로필 사진 삭제(idempotent). 서버가 photo_url=null(default 아바타) 갱신 후 사용자 반환. 기본구현=미지원(테스트 스텁 무수정). — MyPage_SelfPhoto_Delete_Fix</summary>
    Task<ApiResponse<AuthUserDto>> DeleteMyPhotoAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>POST /api/users/{id}/photo — 관리자: 대상 계정 사진 업로드(multipart, users:edit+base-ADMIN). 서버가 대상 photo_url 갱신 후 대상 user 반환. 기본구현=미지원(테스트 스텁 무수정). — Admin_Photo_Upload</summary>
    Task<ApiResponse<AuthUserDto>> UploadUserPhotoAsync(int userId, string filePath, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>DELETE /api/users/{id}/photo — 관리자: 대상 계정 사진 삭제(idempotent). photo_url=null→default 아바타. 기본구현=미지원. — Admin_Photo_Upload</summary>
    Task<ApiResponse<AuthUserDto>> DeleteUserPhotoAsync(int userId, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>PUT /api/users/me/password — {current_password,new_password(min6)}. 서버 세션무효화 없음(F07-01).</summary>
    Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    // ── 관리 화면용 조회 (FR-19, 후속 GOP-05 UI) ──
    /// <summary>GET /api/user-groups — 그룹 목록(권한 nested {modules,device_groups}).</summary>
    Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default);
    /// <summary>POST /api/user-groups/{id}/permissions — 그룹 권한(모듈×동작) 수정(ADMIN 전용, PRD-GOP-01 IMPL-06/서버 v5.0).
    /// 일반 PUT 은 권한상승 방지로 permissions 차단 → 이 전용 경로로만 변경. 기본구현=미지원(테스트 스텁 무수정 목적).</summary>
    Task<ApiResponse<UserGroupDto>> UpdateGroupPermissionsAsync(int groupId, PermissionsDto permissions, CancellationToken ct = default)
        => throw new NotImplementedException();

    // ── 권한 그룹 CRUD (GOP_Permission_Group_Management). 기본구현=미지원(테스트 스텁 무수정) ──
    /// <summary>POST /api/user-groups — 권한 그룹 생성(ADMIN). name 필수, 중복명 409. (FR-01)</summary>
    Task<ApiResponse<UserGroupDto>> CreateUserGroupAsync(UserGroupCreateDto dto, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>PUT /api/user-groups/{id} — 그룹 메타(name/description/is_active) 수정(ADMIN). permissions 는 전용 경로. (FR-02)</summary>
    Task<ApiResponse<UserGroupDto>> UpdateUserGroupAsync(int groupId, UserGroupUpdateDto dto, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>DELETE /api/user-groups/{id} — 그룹 삭제(ADMIN). 소속 계정 group_id→NULL. (FR-03)</summary>
    Task<ApiResponse<object>> DeleteUserGroupAsync(int groupId, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>GET /api/user-groups/{id}/users — 그룹 소속 계정 목록(ADMIN). (FR-04)</summary>
    Task<ApiListResponse<AuthUserDto>> GetUserGroupUsersAsync(int groupId, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>PUT /api/users/{id} — 계정 상시 그룹 배정/해제(ADMIN). group_id 항상 전송(null=해제). (FR-07)</summary>
    Task<ApiResponse<AuthUserDto>> AssignUserGroupAsync(int userId, int? groupId, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>GET /api/user-sessions — 세션 목록(ADMIN, page/limit(max 100) + is_active 필터, created_at DESC).
    /// 날짜필터 없음. 응답 pagination:{page,limit,total,total_pages}.</summary>
    Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default);
    /// <summary>DELETE /api/user-sessions/{session_id} — 세션 강제 로그아웃(ADMIN).
    /// 기본구현=미지원(테스트 스텁 무수정 목적) — 실제 구현은 AccountApiService.</summary>
    Task<ApiResponse<object>> ForceLogoutSessionAsync(int sessionId, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>DELETE /api/user-sessions/user/{user_id} — 해당 사용자의 활성 세션 전체 강제 로그아웃(ADMIN, users:control).
    /// 응답 {success, data:&lt;종료 수&gt;}. 마지막 활성 ADMIN 전원잠금이면 409(가드 존중 → 오류 메시지 표시).
    /// 기본구현=미지원(테스트 스텁 무수정 목적) — 실제 구현은 AccountApiService.</summary>
    Task<ApiResponse<object>> ForceLogoutAllUserSessionsAsync(int userId, CancellationToken ct = default)
        => throw new NotImplementedException();

    // ── Grant Scheduling (T4/FR-GS-06) — 서버 grants API. 기본구현=미지원(테스트 스텁 무수정) ──
    /// <summary>GET /api/users/{userId}/grants — 사용자 권한그룹 부여 목록(ADMIN). status=ACTIVE/PENDING/EXPIRED/REVOKED.</summary>
    Task<ApiListResponse<GrantDto>> GetUserGrantsAsync(int userId, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>POST /api/users/{userId}/grants — 한시 권한그룹 부여(ADMIN). valid_until 생략=상시. 경계위반(과거·until≤from) 422.</summary>
    Task<ApiResponse<GrantDto>> CreateGrantAsync(int userId, GrantCreateDto dto, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>DELETE /api/grants/{grantId} — 부여 회수(soft, ADMIN).</summary>
    Task<ApiResponse<object>> DeleteGrantAsync(int grantId, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>GET /api/grants — 전체 부여 목록(ADMIN, REQ_Server_Grants_ListAll). page/size/user_id/group_id/status/active_only 필터.
    /// 계정별 N-순회 대체(단일 호출). v5.4 응답에 user_login_id/user_name 비정규화 포함.</summary>
    Task<ApiListResponse<GrantDto>> GetAllGrantsAsync(int page = 1, int size = 20, int? userId = null, int? groupId = null, string? status = null, bool activeOnly = false, CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>GET /api/audit-logs — 감사 로그(ADMIN, page/limit + startDate/endDate 날짜범위 필터, ISO yyyy-MM-ddTHH:mm:ss). action_type/resource_type=str(tolerant).</summary>
    Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default);

    // ── 세션 설정 (GOP_Session_Settings_Admin FR-SS-C1) ──
    /// <summary>GET /api/settings/session — 세션/인증 정책(ADMIN). 서버 API 미배포 시 404 → 클라 graceful.
    /// 기본구현=미지원(테스트 스텁 무수정) — 실제 구현은 AccountApiService.</summary>
    Task<ApiResponse<SessionSettingsDto>> GetSessionSettingsAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
    /// <summary>PUT /api/settings/session — 편집 부분집합(만료/refresh/잠금/세션사용) 변경(ADMIN). 기본구현=미지원.</summary>
    Task<ApiResponse<SessionSettingsDto>> UpdateSessionSettingsAsync(SessionSettingsDto dto, CancellationToken ct = default)
        => throw new NotImplementedException();
}
