using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-17 ApiAccountGateway 본인 프로필(IProfileGateway) — /users/me 연동 검증.</summary>
public class ApiAccountGatewayProfileTests
{
    private static ApiAccountGateway Gw() => new(new ProfileStubApi(), new TokenStorageService());

    [Fact]
    public async Task should_get_profile_via_api()
    {
        var me = await Gw().GetProfileAsync(accountId: 7);

        Assert.NotNull(me);
        Assert.Equal("me", me!.Username);
        Assert.Equal("나", me.Name);
    }

    [Fact]
    public async Task should_update_profile_via_api()
    {
        var updated = await Gw().UpdateProfileAsync(new AccountModel { Id = 7, Username = "me", Name = "수정됨" });

        Assert.NotNull(updated);
        Assert.Equal("me", updated!.Username);
    }

    [Fact]
    public async Task should_echo_model_when_change_password_ok()
    {
        var acc = new AccountModel { Id = 7, Username = "me" };

        var result = await Gw().ChangePasswordAsync(acc, "oldPw", "newPw1");

        Assert.Same(acc, result);   // 서버 {success:true} → 입력 모델 에코
    }

    private sealed class ProfileStubApi : IAccountApiService
    {
        private static AuthUserDto Me => new() { Id = 7, LoginId = "me", Name = "나", Role = "OPERATOR", IsActive = true };

        public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default)
            => Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(Me));

        public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(Me));

        public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<object>.CreateSuccess(new object()));

        // 그 외는 본 테스트 미사용
        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
