using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;   // FakeGopServer / RecordingEventAggregator / RecordingLog (동일 어셈블리 internal)
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Sessions;

/****************************************************************************
   Purpose   : UserSessionPanelViewModel is_active 필터 + 무한스크롤 페이지네이션 검증.
   대상      : Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels.UserSessionPanelViewModel
   서버계약  : FakeGopServer.GetUserSessionsAsync(is_active 필터·id desc·page/limit 슬라이스).
   비고      : 헤드리스(Application.Current=null → DispatcherService.Invoke 인라인) 결정론. AuditLogPanelTests 미러.
****************************************************************************/

/// <summary>IsAuthenticated=true 고정 가짜 토큰 저장소(불러오기 실패 팝업 경로 무관 — 로드 성공만 검증).</summary>
internal sealed class FakeTokenStore : ITokenStorageService
{
    public string? AccessToken => "acc";
    public string? RefreshToken => "ref";
    public DateTime? AccessExpiresAtUtc => null;
    public DateTime? RefreshExpiresAtUtc => null;
    public bool IsAuthenticated => true;
    public string? Jti => null;
    public string? UserId { get; set; }
    public string? SessionId { get; set; }   // 서버 sid 클레임 시뮬레이션(현재세션 정확 대조 테스트용)
    public int Generation => 0;
    public void SetTokens(string accessToken, string? refreshToken = null, string? sessionId = null) { }
    public bool SetTokensIfGeneration(int expectedGeneration, string accessToken, string? refreshToken = null, string? sessionId = null) => true;
    public bool IsAccessTokenExpiring(TimeSpan threshold) => false;
    public void Clear() { }
    public event System.Action? TokensRenewed;
}

/// <summary>protected OnActivateAsync/OnDeactivateAsync 를 테스트에 노출(초기 로드·teardown 가드 경로 검증).</summary>
internal sealed class TestUserSessionPanel : UserSessionPanelViewModel
{
    public TestUserSessionPanel(IEventAggregator ea, ILogService log, IAccountApiService api, ITokenStorageService store)
        : base(ea, log, api, store) { }
    public Task ActivateForTestAsync() => OnActivateAsync(CancellationToken.None);
    public Task DeactivateForTestAsync() => OnDeactivateAsync(true, CancellationToken.None);
}

public class UserSessionPanelTests
{
    private static FakeGopServer NewServerWithSessions(int active, int inactive)
    {
        var s = new FakeGopServer();
        int id = 0;
        for (int i = 0; i < active; i++)
            s.Sessions.Add(new UserSessionDto { Id = ++id, UserId = id, LoginId = $"user{id}", Role = "OPERATOR", IsActive = true });
        for (int i = 0; i < inactive; i++)
            s.Sessions.Add(new UserSessionDto { Id = ++id, UserId = id, LoginId = $"user{id}", Role = "OPERATOR", IsActive = false });
        return s;
    }

    private static TestUserSessionPanel NewVm(FakeGopServer s)
        => new(new RecordingEventAggregator(), new RecordingLog(), s, new FakeTokenStore());

    [Fact]
    public async Task should_append_next_page_when_load_more_invoked()
    {
        // Arrange — 250 활성 세션(3페이지: 100/100/50)
        var s = NewServerWithSessions(active: 250, inactive: 0);
        var vm = NewVm(s);
        await vm.ActivateForTestAsync();   // page 1(100)
        var afterFirst = vm.Items.Count;

        // Act
        await vm.LoadNextPageAsync();       // page 2 append

        // Assert
        Assert.Equal(100, afterFirst);
        Assert.Equal(200, vm.Items.Count);
        Assert.True(vm.HasMorePages);       // 아직 3페이지 남음
    }

    [Fact]
    public async Task should_stop_loading_when_last_page_reached()
    {
        // Arrange — 150 활성 세션(2페이지: 100/50)
        var s = NewServerWithSessions(active: 150, inactive: 0);
        var vm = NewVm(s);
        await vm.ActivateForTestAsync();    // page 1(100)

        // Act
        await vm.LoadNextPageAsync();       // page 2(50) → 150

        // Assert
        Assert.Equal(150, vm.Items.Count);
        Assert.False(vm.HasMorePages);
    }

    [Fact]
    public async Task should_reload_page_one_when_active_filter_toggled()
    {
        // Arrange — 250 활성, 2페이지까지 로드(200)
        var s = NewServerWithSessions(active: 250, inactive: 0);
        var vm = NewVm(s);
        await vm.ActivateForTestAsync();
        await vm.LoadNextPageAsync();
        Assert.Equal(200, vm.Items.Count);

        // Act — 필터 토글(기본 활성만 true → false 전체) → setter가 첫 페이지부터 재조회
        vm.IsActiveOnly = false;

        // Assert — 첫 페이지로 리셋(100), 아직 더 있음
        Assert.Equal(100, vm.Items.Count);
        Assert.True(vm.HasMorePages);
    }

    [Fact]
    public async Task should_send_is_active_when_active_only()
    {
        // Arrange — 활성 3 + 비활성 2(활성만 필터 시 3건만). 기본이 활성만(true)이라 재조회만 해도 활성 3건(서버 회신).
        var s = NewServerWithSessions(active: 3, inactive: 2);
        var vm = NewVm(s);

        // Act — 기본(활성만) 상태에서 조회
        await vm.OnClickReloadButton();

        // Assert — is_active=true 전송 + 활성 3건만 로드
        Assert.True(s.LastSessionIsActive);
        Assert.Equal(3, vm.Items.Count);
        Assert.All(vm.Items, x => Assert.True(x.IsActive));
    }

    [Fact]
    public async Task should_send_null_is_active_when_active_only_disabled()
    {
        // Arrange — 활성 3 + 비활성 2(전체 5건)
        var s = NewServerWithSessions(active: 3, inactive: 2);
        var vm = NewVm(s);

        // Act — 전체 보기
        vm.IsActiveOnly = false;
        await vm.OnClickReloadButton();

        // Assert — is_active 미전송(null) + 5건 전체 로드
        Assert.Null(s.LastSessionIsActive);
        Assert.Equal(5, vm.Items.Count);
    }

    [Fact]
    public async Task should_mark_only_matching_session_as_current_when_sid_provided()
    {
        // Arrange — 활성 세션 3개(id 1,2,3). 내 sid=2 (서버 sid 클레임 ↔ 행 id 정확 대조, 서버 회신 2026-08-03)
        var s = NewServerWithSessions(active: 3, inactive: 0);
        var store = new FakeTokenStore { SessionId = "2" };
        var vm = new TestUserSessionPanel(new RecordingEventAggregator(), new RecordingLog(), s, store);

        // Act
        await vm.ActivateForTestAsync();

        // Assert — 정확히 1개(id=2)만 현재세션(login_id 근사로 인한 과다표시 없음)
        Assert.Equal(1, vm.Items.Count(x => x.IsCurrentSession));
        Assert.True(vm.Items.Single(x => x.Id == 2).IsCurrentSession);
    }

    [Fact]
    public async Task should_fallback_to_account_approx_when_sid_absent()
    {
        // Arrange — sid 미제공(구서버). 내 계정=user2 → 그 계정 활성 세션만 근사 현재표시
        var s = NewServerWithSessions(active: 3, inactive: 0);   // login_id: user1,user2,user3
        var store = new FakeTokenStore { UserId = "user2" };     // SessionId=null → 폴백 경로
        var vm = new TestUserSessionPanel(new RecordingEventAggregator(), new RecordingLog(), s, store);

        // Act
        await vm.ActivateForTestAsync();

        // Assert — 폴백: 내 계정(user2) 세션만 현재표시
        Assert.True(vm.Items.Single(x => x.LoginId == "user2").IsCurrentSession);
        Assert.False(vm.Items.Single(x => x.LoginId == "user1").IsCurrentSession);
    }
}
