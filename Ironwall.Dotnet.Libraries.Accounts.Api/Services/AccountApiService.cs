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

    public async Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default)
    {
        try
        {
            var res = await _api.PostRequestAsync($"users/{id}/reset-password", new ResetPasswordRequestDto { NewPassword = newPassword }).ConfigureAwait(false);
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
