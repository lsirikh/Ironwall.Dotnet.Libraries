using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>FR-19 ApiAccountGateway 계정관리(IUserDirectoryGateway) — API 호출 + DTO↔Model 매핑 검증.</summary>
public class ApiAccountGatewayCrudTests
{
    private static ApiAccountGateway Gw() => new(new CrudStubApi(), new TokenStorageService(), new PermissionService());

    [Fact]
    public async Task should_list_accounts_via_api()
    {
        var list = await Gw().GetAllAccountsAsync();

        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Equal("a", list[0].Username);
        Assert.Equal(EnumLevelType.ADMIN, list[0].Level);   // ADMIN role → ADMIN level
        Assert.Equal(EnumLevelType.USER, list[1].Level);    // VIEWER → USER level
    }

    [Fact]
    public async Task should_create_account_via_api()
    {
        var created = await Gw().CreateAccountAsync(
            new AccountModel { Username = "newuser", Password = "pw", Level = EnumLevelType.ADMIN });

        Assert.NotNull(created);
        Assert.Equal(10, created!.Id);             // 서버 발급 PK
        Assert.Equal("newuser", created.Username);
    }

    [Fact]
    public async Task should_return_true_when_remove_account_via_api()
    {
        var ok = await Gw().RemoveAccountAsync(new AccountModel { Id = 5 }, currentPassword: "any");

        Assert.True(ok);
    }

    [Fact]
    public async Task should_optimistic_false_for_username_taken()
        => Assert.False(await Gw().IsUsernameTakenAsync("whoever"));   // B-4 v4.10 — 낙관 false

    [Fact]
    public async Task should_echo_model_when_reset_password_ok()
    {
        var acc = new AccountModel { Id = 7, Username = "u" };

        var result = await Gw().ResetAccountPasswordAsync(acc, "newPw1");

        Assert.Same(acc, result);   // 서버 {success:true} → 입력 모델 에코
    }

    private sealed class CrudStubApi : IAccountApiService
    {
        public Task<ApiListResponse<AuthUserDto>> GetUsersAsync(int page = 1, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(ApiListResponse<AuthUserDto>.CreateSuccess(new List<AuthUserDto>
            {
                new() { Id = 1, LoginId = "a", Role = "ADMIN",  IsActive = true },
                new() { Id = 2, LoginId = "b", Role = "VIEWER", IsActive = true },
            }));

        public Task<ApiResponse<AuthUserDto>> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(
                new AuthUserDto { Id = 10, LoginId = dto.LoginId, Role = dto.Role, IsActive = true }));

        public Task<ApiResponse<AuthUserDto>> UpdateUserAsync(int id, UserUpdateDto dto, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<AuthUserDto>.CreateSuccess(
                new AuthUserDto { Id = id, LoginId = dto.LoginId ?? "x", Role = dto.Role, IsActive = true }));

        public Task<ApiResponse<object>> DeleteUserAsync(int id, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<object>.CreateSuccess(new object()));

        public Task<ApiResponse<object>> ResetUserPasswordAsync(int id, string newPassword, CancellationToken ct = default)
            => Task.FromResult(ApiResponse<object>.CreateSuccess(new object()));

        // 로그인 경로는 본 테스트 미사용
        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<AuditLogDto>> GetAuditLogsAsync(int page = 1, int limit = 20, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthUserDto?> GetMeAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> GetMyProfileAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UpdateMyProfileAsync(UserSelfUpdateDto dto, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<AuthUserDto>> UploadMyPhotoAsync(string filePath, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<object>> ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
