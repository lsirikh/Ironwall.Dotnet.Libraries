using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/****************************************************************************
   Purpose   : LogoutOnExitService(종료 시 강제 로그아웃) 검증 — logout-on-exit PRD.
   대상      : Ironwall.Dotnet.Libraries.Accounts.Api.Services.LogoutOnExitService
   계약      : StopAsync — 인증 시 best-effort 서버 로그아웃(IAccountApiService.LogoutAsync) 후 토큰 폐기.
               미인증 skip / 예외 삼킴 / 데드라인 초과 시 방치+로컬 폐기(WhenAny 실상한).
   비고      : 데드라인 주입으로 실대기 회피(sleep 없음). 이벤트/UI 미발화(IAuthGateway 미사용).
****************************************************************************/
public class LogoutOnExitServiceTests
{
    [Fact]
    public async Task should_call_logout_and_clear_when_authenticated()
    {
        // Arrange
        var api = new FakeLogoutApi();                 // 기본=성공 응답
        var store = new FakeTokenStore { Authenticated = true };
        var svc = new LogoutOnExitService(api, store);

        // Act
        await svc.StopAsync();

        // Assert — 서버 로그아웃 1회 + 로컬 토큰 폐기
        Assert.Equal(1, api.LogoutCalls);
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public async Task should_skip_logout_when_not_authenticated()
    {
        // Arrange — 미인증(이미 로그아웃/revoked/로그인 전)
        var api = new FakeLogoutApi();
        var store = new FakeTokenStore { Authenticated = false };
        var svc = new LogoutOnExitService(api, store);

        // Act
        await svc.StopAsync();

        // Assert — 서버 미호출 + 폐기 미수행(멱등 skip)
        Assert.Equal(0, api.LogoutCalls);
        Assert.Equal(0, store.ClearCalls);
    }

    [Fact]
    public async Task should_swallow_and_clear_when_logout_throws()
    {
        // Arrange — 서버 로그아웃이 실패(faulted) — best-effort라 삼키고 로컬 폐기는 수행
        var api = new FakeLogoutApi(() => Task.FromException<ApiResponse<object>>(new InvalidOperationException("boom")));
        var store = new FakeTokenStore { Authenticated = true };
        var svc = new LogoutOnExitService(api, store);

        // Act — 예외를 전파하면 안 됨(종료 흐름 보호)
        var ex = await Record.ExceptionAsync(() => svc.StopAsync());

        // Assert — 무예외 + 폐기 수행
        Assert.Null(ex);
        Assert.Equal(1, api.LogoutCalls);
        Assert.Equal(1, store.ClearCalls);
    }

    [Fact]
    public async Task should_bound_and_clear_when_logout_hangs()
    {
        // Arrange — 서버 로그아웃이 응답하지 않음(hang). 짧은 데드라인 주입으로 실대기 회피.
        var neverCompletes = new TaskCompletionSource<ApiResponse<object>>();
        var api = new FakeLogoutApi(() => neverCompletes.Task);
        var store = new FakeTokenStore { Authenticated = true };
        var svc = new LogoutOnExitService(api, store, log: null, deadline: TimeSpan.FromMilliseconds(50));

        // Act — 데드라인(50ms) 초과 시 호출 방치하고 반환해야 함(hang되면 이 테스트가 멈춤)
        await svc.StopAsync();

        // Assert — 호출은 시작됐고, 데드라인 후 로컬 폐기로 종료 진행
        Assert.Equal(1, api.LogoutCalls);
        Assert.Equal(1, store.ClearCalls);
    }

    // ─────────────────────────── 페이크 ───────────────────────────

    /// <summary>LogoutAsync 만 구성 가능(호출 카운트/동작). 나머지는 미사용(NotImplemented).</summary>
    private sealed class FakeLogoutApi : IAccountApiService
    {
        private readonly Func<Task<ApiResponse<object>>> _logout;
        public int LogoutCalls { get; private set; }

        public FakeLogoutApi(Func<Task<ApiResponse<object>>>? logout = null)
            => _logout = logout ?? (() => Task.FromResult(ApiResponse<object>.CreateSuccess(new object())));

        public Task<ApiResponse<object>> LogoutAsync(CancellationToken ct = default)
        {
            LogoutCalls++;
            return _logout();
        }

        // 미사용 멤버 — 종료 로그아웃 경로와 무관
        public Task<ApiResponse<LoginResponseDataDto>> LoginAsync(string loginId, string password, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiResponse<TokenDataDto>> RefreshAsync(string refreshToken, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserGroupDto>> GetUserGroupsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ApiListResponse<UserSessionDto>> GetUserSessionsAsync(int page = 1, int limit = 100, bool? isActive = null, CancellationToken ct = default) => throw new NotImplementedException();
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

    /// <summary>IsAuthenticated 토글 + Clear 카운트. Clear 시 미인증으로 전환(실제 Clear 시맨틱 근사).</summary>
    private sealed class FakeTokenStore : ITokenStorageService
    {
        public bool Authenticated { get; set; } = true;
        public int ClearCalls { get; private set; }

        public string? AccessToken => Authenticated ? "acc" : null;
        public string? RefreshToken => "ref";
        public DateTime? AccessExpiresAtUtc => null;
        public DateTime? RefreshExpiresAtUtc => null;
        public bool IsAuthenticated => Authenticated;
        public string? Jti => null;
        public string? UserId => null;
        public string? SessionId => null;
        public int Generation => 0;
        public event System.Action? TokensRenewed;

        public void SetTokens(string accessToken, string? refreshToken = null, string? sessionId = null) { _ = TokensRenewed; }
        public bool SetTokensIfGeneration(int expectedGeneration, string accessToken, string? refreshToken = null, string? sessionId = null) => true;
        public bool IsAccessTokenExpiring(TimeSpan threshold) => false;
        public void Clear() { ClearCalls++; Authenticated = false; }
    }
}
