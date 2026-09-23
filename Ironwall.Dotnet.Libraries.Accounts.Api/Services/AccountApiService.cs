using Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;
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

    /// <inheritdoc/>
    public string? ServerBaseUrl => _api.Url;

    public async Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default)
    {
        try
        {
            // client_id: 커넥션 식별값(§9.2.2). allow 정책에서 세션 주체 구분 + self-replace 축.
            // 패턴 위반값은 서버가 무시(로그인 차단 없음)하고, 키 자체를 생략해도 로그인은 성공한다 → 무회귀.
            var res = await _api.PostRequestAsync("auth/login",
                                    new LoginRequestDto { LoginId = loginId, Password = password, ClientId = ClientIdentity.Current })
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

    // GET /auth/me/permissions — 유효권한 스냅샷(FR-GS-01/02). 표준 envelope {success,data:{modules,device_groups,valid_until,server_time}}.
    public async Task<ApiResponse<PermissionsSnapshotDto>> GetMyPermissionsAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetRequestAsync("auth/me/permissions").ConfigureAwait(false);
            return await res.ToApiResponseAsync<PermissionsSnapshotDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<PermissionsSnapshotDto>.CreateError("INTERNAL_ERROR", ex.Message); }
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

    /// <summary>
    /// GET /api/users 전량 조회 — <c>page</c> 를 올려가며 <b>서버가 더 줄 게 없을 때까지</b> 순회한다(§9.3.2).
    /// <para>서버 <c>limit</c> 상한이 100(초과 지정은 422)이라 단일 호출은 101번째 계정부터 <b>조용히 잘렸다</b>.
    /// 운영 6.3.2 봉투에는 <c>pagination</c> 이 없어 "더 있다"를 알 방법조차 없었으므로, 판본과 무관하게 성립하는
    /// 종료조건을 쓴다 — ① 빈 페이지 ② 반환 수 &lt; limit ③ (<c>pagination</c> 이 있으면) <c>page &gt;= total_pages</c>.
    /// 그래서 8.0.1(pagination 있음)·6.3.2(없음) 양쪽에서 같은 코드로 끝까지 가져온다.</para>
    /// </summary>
    public async Task<ApiListResponse<AuthUserDto>> GetAllUsersAsync(CancellationToken ct = default)
        => await GetAllPagesAsync<AuthUserDto>("users", "limit", ct).ConfigureAwait(false);

    /// <summary>
    /// GET /api/user-groups 전량 조회 — 그룹 목록도 <c>page</c>/<c>limit</c>(기본·상한 100)을 받는다.
    /// 종전 호출은 파라미터를 아예 보내지 않아 그룹이 100개를 넘으면 잘렸다(무증상). 종료조건은 users 와 동일.
    /// </summary>
    public async Task<ApiListResponse<UserGroupDto>> GetAllUserGroupsAsync(CancellationToken ct = default)
        => await GetAllPagesAsync<UserGroupDto>("user-groups", "limit", ct).ConfigureAwait(false);

    /// <summary>서버 페이지 상한(users·user-groups·user-sessions·audit-logs 공통 <c>le=100</c>).</summary>
    private const int MAX_PAGE_SIZE = 100;
    /// <summary>순회 안전 상한(= 최대 10,000행). 서버가 같은 페이지를 반복 반환하는 병리적 상황에서 무한루프 방지.</summary>
    private const int MAX_PAGES = 100;

    /// <summary>page 를 1 부터 올려가며 목록을 이어붙인다. 첫 페이지가 실패하면 그 실패 응답을 그대로 반환(호출부 swap-on-success 유지).</summary>
    private async Task<ApiListResponse<T>> GetAllPagesAsync<T>(string path, string limitKey, CancellationToken ct)
    {
        try
        {
            var all = new List<T>();
            ApiListResponse<T>? last = null;
            for (var page = 1; page <= MAX_PAGES; page++)
            {
                ct.ThrowIfCancellationRequested();
                var query = new Dictionary<string, string> { ["page"] = page.ToString(), [limitKey] = MAX_PAGE_SIZE.ToString() };
                var http = await _api.GetRequestAsync(path, query).ConfigureAwait(false);
                var res = await http.ToApiListResponseAsync<T>().ConfigureAwait(false);

                if (!res.Success || res.Data is null)
                    return page == 1 ? res : Finish(all, last);   // 중간 페이지 실패: 여기까지를 성공으로 넘기되 last 로 표시
                last = res;
                all.AddRange(res.Data);

                if (res.Data.Count < MAX_PAGE_SIZE) break;                                  // 마지막 페이지(부분 채움)
                if (res.Pagination is { } p && p.TotalPages > 0 && page >= p.TotalPages) break;  // pagination 제공 서버(8.0+)
            }
            return Finish(all, last);
        }
        catch (OperationCanceledException) { return ApiListResponse<T>.CreateError("CANCELED", "요청이 취소되었습니다."); }
        catch (Exception ex) { return ApiListResponse<T>.CreateError("INTERNAL_ERROR", ex.Message); }

        static ApiListResponse<T> Finish(List<T> all, ApiListResponse<T>? last)
        {
            // pagination 은 '마지막 페이지' 기준이라 그대로 싣으면 전량 결과를 잘못 설명한다 → 합본 기준으로 재작성.
            var merged = ApiListResponse<T>.CreateSuccess(all,
                new PaginationDto { Page = 1, Limit = all.Count, Total = all.Count, TotalPages = 1 },
                last?.Message);
            merged.Total = all.Count;   // 봉투가 total 을 안 주는 판본에서도 호출부가 전체 건수를 읽을 수 있게 채운다
            merged.StatusCode = last?.StatusCode ?? 200;
            return merged;
        }
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

    public async Task<ApiResponse<object>> ForceLogoutAllUserSessionsAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync($"user-sessions/user/{userId}").ConfigureAwait(false);
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
            // ⚠ status 는 '비어있지 않을 때만' 보낸다 — 서버 어휘가 닫혀 있어(ACTIVE/EXPIRED/PENDING/REVOKED)
            //   빈 문자열을 "전체"로 여겨 `?status=` 로 조립하면 422 가 된다(라이브 실측: status=bogus → 422).
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

    // DELETE /users/me/photo — 본인 사진 삭제(idempotent). 서버가 photo_url=null(default 아바타) 후 사용자 반환. (MyPage_SelfPhoto_Delete_Fix)
    public async Task<ApiResponse<AuthUserDto>> DeleteMyPhotoAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync("users/me/photo").ConfigureAwait(false);
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

    // ── 관리자: 대상 계정 사진 (Admin_Photo_Upload — 서버 v6.3 POST/DELETE /users/{id}/photo) ──
    public async Task<ApiResponse<AuthUserDto>> UploadUserPhotoAsync(int userId, string filePath, CancellationToken ct = default)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GuessImageMime(filePath));
            content.Add(fileContent, "file", System.IO.Path.GetFileName(filePath));
            // ⚠ 대상 계정 {id} (=본인 /me 아님). 서버가 base-ADMIN 상승가드 + actor≠target 감사 기록.
            var res = await _api.PostFormDataRequestAsync($"users/{userId}/photo", content).ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiResponse<AuthUserDto>> DeleteUserPhotoAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.DeleteRequestAsync($"users/{userId}/photo").ConfigureAwait(false);
            return await res.ToApiResponseAsync<AuthUserDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiResponse<AuthUserDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

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

    public async Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default)
    {
        try
        {
            var q = $"user-sessions?page={page}&limit={limit}";
            if (isActive.HasValue) q += $"&is_active={(isActive.Value ? "true" : "false")}";
            var res = await _api.GetRequestAsync(q).ConfigureAwait(false);
            return await res.ToApiListResponseAsync<UserSessionDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<UserSessionDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }

    public async Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default)
    {
        try
        {
            var q = $"audit-logs?page={page}&limit={limit}";
            if (!string.IsNullOrEmpty(startDate)) q += $"&start_date={Uri.EscapeDataString(startDate)}";
            if (!string.IsNullOrEmpty(endDate))   q += $"&end_date={Uri.EscapeDataString(endDate)}";
            var res = await _api.GetRequestAsync(q).ConfigureAwait(false);
            return await res.ToApiListResponseAsync<AuditLogDto>().ConfigureAwait(false);
        }
        catch (Exception ex) { return ApiListResponse<AuditLogDto>.CreateError("INTERNAL_ERROR", ex.Message); }
    }
}
