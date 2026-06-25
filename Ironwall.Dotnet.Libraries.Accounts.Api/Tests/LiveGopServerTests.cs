using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Modules;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 실제 GOP 서버 상대 E2E 통합 테스트 (§2.3 확정계약 실측 검증). 모의 아님.
/// <para><b>opt-in</b>: 아래 환경변수가 모두 설정됐을 때만 실행, 미설정 시 no-op(일반/CI 실행 안전).
/// 자격증명은 env 로만 주입 — 코드/로그에 남기지 않는다(security.md).</para>
/// <para>실행: (PowerShell)
/// <code>
/// $env:GOP_BASE_URL="https://&lt;host:port&gt;/api/"   # API 베이스(끝에 / , /api/ 포함)
/// $env:GOP_LOGIN_ID="&lt;test_id&gt;"
/// $env:GOP_PASSWORD="&lt;test_pw&gt;"
/// dotnet test --filter LiveGopServerTests
/// </code></para>
/// </summary>
public class LiveGopServerTests
{
    private readonly ITestOutputHelper _out;
    public LiveGopServerTests(ITestOutputHelper output) => _out = output;

    private static string? BaseUrl => Environment.GetEnvironmentVariable("GOP_BASE_URL");
    private static string? LoginId => Environment.GetEnvironmentVariable("GOP_LOGIN_ID");
    private static string? Password => Environment.GetEnvironmentVariable("GOP_PASSWORD");

    private static bool Enabled =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(LoginId) &&
        !string.IsNullOrWhiteSpace(Password);

    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        var setup = new ApiSetupModel { Url = BaseUrl!, Timeout = 15 };
        builder.RegisterModule(new AccountApiModule(null, setup, "Account"));
        return builder.Build();
    }

    [Fact]
    public async Task should_login_and_fetch_profile_against_real_server()
    {
        if (!Enabled)
        {
            _out.WriteLine("GOP_BASE_URL/LOGIN_ID/PASSWORD 미설정 — 라이브 E2E 건너뜀(no-op).");
            return;
        }

        using var c = BuildContainer();

        // 실제 HttpClient 파이프라인 생성(BearerAuthHandler 포함)
        c.ResolveNamed<IApiService>("Account").Initialize();

        var gateway = c.Resolve<IAuthGateway>();

        // 1) 로그인 → 서버 인증 + 토큰 보관(R1)
        var outcome = await gateway.AuthenticateAsync(LoginId!, Password!);
        Assert.True(outcome.Success, $"로그인 실패: {outcome.ErrorCode} {outcome.Message}");
        var auth = outcome.Result!;
        Assert.False(string.IsNullOrEmpty(auth.Token));
        _out.WriteLine($"[로그인 OK] role={auth.Role}, perms={auth.Permissions.Count}, account={auth.Account.Username}");

        // 2) 보관된 토큰으로 본인 프로필 조회 → BearerAuthHandler 자동 첨부 검증
        var profile = await ((IProfileGateway)gateway).GetProfileAsync(auth.Account.Id);
        Assert.NotNull(profile);
        _out.WriteLine($"[프로필 OK] {profile!.Username} / {profile.EMail}");

        // 3) 계정 목록 조회(read-only) → 디렉터리 계약 + 리스트 봉투(B-8) 실측 검증
        var accounts = await ((IUserDirectoryGateway)gateway).GetAllAccountsAsync();
        Assert.NotNull(accounts);
        _out.WriteLine($"[목록 OK] {accounts!.Count} accounts: {string.Join(", ", accounts.ConvertAll(a => a.Username))}");

        // 3.5) 감사 로그(read-only) — audit-logs 500 수정 후 .NET 경로 실검증
        var api = c.Resolve<IAccountApiService>();
        var audits = await api.GetAuditLogsAsync(1, 5);
        Assert.True(audits.Success, $"audit 조회 실패: {audits.Error?.Code} {audits.Message}");
        _out.WriteLine($"[감사 OK] {audits.Data!.Count}건, 첫 action={(audits.Data.Count > 0 ? audits.Data[0].ActionType : "-")}");

        // 4) refresh — 보관된 refresh_token으로 갱신 (A-3: 토큰만 반환)
        var store = c.Resolve<ITokenStorageService>();
        var oldAccess = store.AccessToken;
        var refreshed = await api.RefreshAsync(store.RefreshToken!);
        Assert.True(refreshed.Success, $"refresh 실패: {refreshed.Error?.Code} {refreshed.Message}");
        Assert.False(string.IsNullOrEmpty(refreshed.Data!.AccessToken));
        _out.WriteLine($"[refresh OK] 새 access 발급, 이전과 다름={refreshed.Data.AccessToken != oldAccess}");

        // 5) logout — best-effort (access_token 서버 블랙리스트 없음, §2.3.3)
        var logout = await api.LogoutAsync();
        _out.WriteLine($"[logout] success={logout.Success}, code={logout.Error?.Code}");
    }
}
