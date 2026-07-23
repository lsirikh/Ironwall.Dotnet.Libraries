using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// FR-7/14/21 ApiAccountGateway — <b>로그인이 실제로 API를 타는지</b> 검증.
/// login(서버) → 토큰 보관(R1) → AuthResult(계정/역할/권한 평탄화) 전 경로.
/// </summary>
public class ApiAccountGatewayTests
{
    [Fact]
    public async Task should_login_via_api_then_store_tokens_and_return_authresult()
    {
        var store = new TokenStorageService();
        var gw = new ApiAccountGateway(new StubAuthApi(loginOk: true), store, new PermissionService());

        var outcome = await gw.AuthenticateAsync("admin", "pw");

        Assert.True(outcome.Success);
        var result = outcome.Result!;
        Assert.Equal("acc", result.Token);                  // 서버 access_token
        Assert.Equal("ADMIN", result.Role);                 // 서버 role
        Assert.Contains("events:r", result.Permissions);    // flat 권한 평탄화
        Assert.Contains("events:w", result.Permissions);
        Assert.Equal("admin", result.Account.Username);     // DTO→AccountModel 매핑
        Assert.Equal(EnumLevelType.ADMIN, result.Account.Level);

        // R1: 토큰을 게이트웨이가 직접 보관(access+refresh)
        Assert.Equal("acc", store.AccessToken);
        Assert.Equal("ref", store.RefreshToken);
        Assert.True(store.IsAuthenticated);
    }

    [Fact]
    public async Task should_return_null_and_not_store_when_login_fails()
    {
        var store = new TokenStorageService();
        var gw = new ApiAccountGateway(new StubAuthApi(loginOk: false), store, new PermissionService());

        var outcome = await gw.AuthenticateAsync("admin", "bad");

        Assert.False(outcome.Success);
        Assert.Equal("UNAUTHORIZED", outcome.ErrorCode);
        Assert.False(store.IsAuthenticated);
    }

    private sealed class StubAuthApi : IAccountApiService
    {
        private readonly bool _ok;
        public StubAuthApi(bool loginOk) => _ok = loginOk;

        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default)
        {
            if (!_ok)
                return Task.FromResult(ApiResponse<LoginResponseDataDto>.CreateError("UNAUTHORIZED", "bad credentials"));

            var data = new LoginResponseDataDto
            {
                AccessToken = "acc",
                RefreshToken = "ref",
                TokenType = "bearer",
                User = new AuthUserDto
                {
                    Id = 1,
                    LoginId = "admin",
                    Role = "ADMIN",
                    IsActive = true,
                    Permissions = JObject.Parse(@"{""events"":""rw""}")
                }
            };
            return Task.FromResult(ApiResponse<LoginResponseDataDto>.CreateSuccess(data));
        }

        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, string? startDate = null, string? endDate = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
