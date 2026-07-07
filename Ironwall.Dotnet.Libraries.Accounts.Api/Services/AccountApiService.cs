using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// <see cref="IAccountApiService"/> 구현. 401 응답은 항상 envelope(§2.3.1)이므로 raw {detail} 경로 없음 — ToApiResponseAsync 가 처리.
/// </summary>
public class AccountApiService : IAccountApiService
{
    private readonly IApiService _api;
    private readonly ILogService? _log;

    public AccountApiService(IApiService api, ILogService? log = null)
    {
        _api = api;
        _log = log;
    }

    public async Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync("auth/login", new LoginRequestDto { LoginId = loginId, Password = password })
                                .ConfigureAwait(false);
            var parsed = await res.ToApiResponseAsync<LoginResponseDataDto>().ConfigureAwait(false);

            // 성공인데 토큰이 비면 API 버전/계약 불일치 — 명시적 에러로 승격
            if (parsed.Success && string.IsNullOrEmpty(parsed.Data?.AccessToken))
                return ApiResponse<LoginResponseDataDto>.CreateError(
                    "INVALID_TOKEN_RESPONSE", "서버 응답에 토큰이 없습니다 — API 버전 확인 필요");

            return parsed;
        }
        catch (Exception ex)
        {
            return ApiResponse<LoginResponseDataDto>.CreateError("INTERNAL_ERROR", ex.Message);
        }
    }

    public async Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync("auth/refresh", new RefreshTokenRequestDto { RefreshToken = refreshToken })
                                .ConfigureAwait(false);
            return await res.ToApiResponseAsync<TokenDataDto>().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ApiResponse<TokenDataDto>.CreateError("INTERNAL_ERROR", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync("auth/logout", new { }).ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message);
        }
    }

    public async Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync("auth/me").ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;

            var content = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<AuthUserDto>(content, ApiMessageHelper.JsonSettings);
        }
        catch (Exception ex)
        {
            _log?.Error($"[AccountApiService] GetMe 실패: {ex.Message}");
            return null;
        }
    }

    public async Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default)
    {
        try
        {
            var query = new Dictionary<string, string> { ["page"] = page.ToString(), ["limit"] = limit.ToString() };
            var res = await _api.GetRequestAsync("users", query).ConfigureAwait(false);
            return await res.ToApiListResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    // ── 세션 설정 (GOP_Session_Settings_Admin FR-SS-C1) — 서버 API 미배포 시 404 → res.Success=false (클라 graceful) ──
    public async Task<ApiResponse<SessionSettingsDto>> GetSessionSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync("settings/session").ConfigureAwait(false);
            return await res.ToApiResponseAsync<SessionSettingsDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<SessionSettingsDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<SessionSettingsDto>> UpdateSessionSettingsAsync(SessionSettingsDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PutRequestAsync("settings/session", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<SessionSettingsDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<SessionSettingsDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync("users", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PutRequestAsync($"users/{id}", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync($"users/{id}").ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<object>> ForceLogoutSessionAsync(int sessionId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync($"user-sessions/{sessionId}").ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    // ── Grant Scheduling (T4/FR-GS-06) — 서버 grants API 소비 ──
    public async Task<ApiListResponse<GrantDto>> GetUserGrantsAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync($"users/{userId}/grants").ConfigureAwait(false);
            return await res.ToApiListResponseAsync<GrantDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<GrantDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<GrantDto>> CreateGrantAsync(int userId, GrantCreateDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync($"users/{userId}/grants", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<GrantDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<GrantDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<object>> DeleteGrantAsync(int grantId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync($"grants/{grantId}").ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    // GET /grants — 전체 부여(REQ_Server_Grants_ListAll). 계정별 N-순회 대체.
    public async Task<ApiListResponse<GrantDto>> GetAllGrantsAsync(int page = 1, int size = 20, int? userId = null, int? groupId = null, string? status = null, bool activeOnly = false, CancellationToken ct = default)
    {
        try
        {
            var query = new Dictionary<string, string> { ["page"] = page.ToString(), ["size"] = size.ToString() };
            if (userId.HasValue) query["user_id"] = userId.Value.ToString();
            if (groupId.HasValue) query["group_id"] = groupId.Value.ToString();
            if (!string.IsNullOrEmpty(status)) query["status"] = status;
            if (activeOnly) query["active_only"] = "true";
            var res = await _api.GetRequestAsync("grants", query).ConfigureAwait(false);
            return await res.ToApiListResponseAsync<GrantDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<GrantDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync($"users/{id}/reset-password", new ResetPasswordRequestDto { NewPassword = newPassword }).ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    // POST /users/{id}/unlock — 계정 잠금 해제(ADMIN). 서버 권한: users:control(ADMIN 대상은 base-ADMIN). 실패=403/네트워크.
    public async Task<ApiResponse<object>> UnlockUserAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync($"users/{id}/unlock", new { }).ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync("users/me").ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PutRequestAsync("users/me", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessImageMime(filePath));
            content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
            var res = await _api.PostFormDataRequestAsync("users/me/photo", content).ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    private static string GuessImageMime(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };

    public async Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        try
        {
            var body = new PasswordChangeRequestDto { CurrentPassword = currentPassword, NewPassword = newPassword };
            var res = await _api.PutRequestAsync("users/me/password", body).ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync("user-groups").ConfigureAwait(false);
            return await res.ToApiListResponseAsync<UserGroupDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<UserGroupDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<UserGroupDto>> UpdateGroupPermissionsAsync(int groupId, PermissionsDto permissions, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync($"user-groups/{groupId}/permissions", permissions).ConfigureAwait(false);
            return await res.ToApiResponseAsync<UserGroupDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<UserGroupDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    // ── 권한 그룹 CRUD (GOP_Permission_Group_Management) — 서버 user-groups API 소비 ──
    public async Task<ApiResponse<UserGroupDto>> CreateUserGroupAsync(UserGroupCreateDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync("user-groups", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<UserGroupDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<UserGroupDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<UserGroupDto>> UpdateUserGroupAsync(int groupId, UserGroupUpdateDto dto, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PutRequestAsync($"user-groups/{groupId}", dto).ConfigureAwait(false);
            return await res.ToApiResponseAsync<UserGroupDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<UserGroupDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<object>> DeleteUserGroupAsync(int groupId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync($"user-groups/{groupId}").ConfigureAwait(false);
            return await res.ToApiResponseAsync<object>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<object>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiListResponse<AuthUserDto>> GetUserGroupUsersAsync(int groupId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync($"user-groups/{groupId}/users").ConfigureAwait(false);
            return await res.ToApiListResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> AssignUserGroupAsync(int userId, int? groupId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PutRequestAsync($"users/{userId}", new UserGroupAssignDto { GroupId = groupId }).ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync("user-sessions").ConfigureAwait(false);
            return await res.ToApiListResponseAsync<UserSessionDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<UserSessionDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync($"audit-logs?page={page}&limit={limit}").ConfigureAwait(false);
            return await res.ToApiListResponseAsync<AuditLogDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<AuditLogDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }
}
