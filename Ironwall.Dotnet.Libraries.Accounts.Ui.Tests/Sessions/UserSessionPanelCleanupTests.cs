using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;   // FakeGopServer / RecordingEventAggregator / RecordingLog (동일 어셈블리 internal)
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;   // OpenConfirmPopupMessageModel (VM이 발행하는 정본)
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Sessions;

/****************************************************************************
   Purpose   : 세션 패널 정리(GOP_SessionPanel_Cleanup) 검증 — FR-1 표시 컨버터 · FR-2 사용자 전체 세션 종료 · FR-3 기본 전체+자동갱신.
   대상      : UserSessionPanelViewModel(TryAutoRefresh / OnClickForceLogoutAllUserSessions / HandleAsync) · IsoDateStringConverter.
   서버계약  : FakeGopServer.ForceLogoutAllUserSessionsAsync(userId 캡처+활성 비활성화) · GetUserSessionsAsync(is_active null=전체).
   비고      : 헤드리스(Application.Current=null → DispatcherService.Invoke 인라인) 결정론. UserSessionPanelTests 하네스 미러(sleep 없음).
****************************************************************************/
public class UserSessionPanelCleanupTests
{
    // ── 하네스: UserSessionPanelTests 와 동일 방식(활성/비활성 세션 시드) ──
    private static FakeGopServer NewServerWithSessions(int active, int inactive, int userId = 0)
    {
        var s = new FakeGopServer();
        int id = 0;
        for (int i = 0; i < active; i++)
            s.Sessions.Add(new UserSessionDto { Id = ++id, UserId = userId == 0 ? id : userId, LoginId = $"user{id}", Role = "OPERATOR", IsActive = true });
        for (int i = 0; i < inactive; i++)
            s.Sessions.Add(new UserSessionDto { Id = ++id, UserId = userId == 0 ? id : userId, LoginId = $"user{id}", Role = "OPERATOR", IsActive = false });
        return s;
    }

    private static (TestUserSessionPanel vm, RecordingEventAggregator ea) NewVm(FakeGopServer s)
    {
        var ea = new RecordingEventAggregator();
        var vm = new TestUserSessionPanel(ea, new RecordingLog(), s, new FakeTokenStore());
        return (vm, ea);
    }

    /// <summary>Confirm 팝업에 실린 전체종료 트리거를 꺼내 HandleAsync 로 전달(실제 ConfirmPopupDialog Yes 경로 모사).</summary>
    private static CallForceLogoutAllUserSessionsMessageModel ExtractAllTrigger(RecordingEventAggregator ea)
    {
        var confirm = ea.OfType<OpenConfirmPopupMessageModel>().Last();
        return Assert.IsType<CallForceLogoutAllUserSessionsMessageModel>(confirm.MessageModel);
    }

    // ─────────────────────────── FR-2: 사용자 전체 세션 종료 ───────────────────────────

    [Fact]
    public async Task should_call_delete_user_sessions_when_force_logout_all()
    {
        // Arrange — userId=7 사용자의 활성 세션 2 + 다른 사용자 활성 1
        var s = NewServerWithSessions(active: 2, inactive: 0, userId: 7);
        s.Sessions.Add(new UserSessionDto { Id = 99, UserId = 8, LoginId = "other", Role = "OPERATOR", IsActive = true });
        var (vm, ea) = NewVm(s);
        await vm.ActivateForTestAsync();
        var target = new UserSessionDto { Id = 1, UserId = 7, LoginId = "user1", IsActive = true };

        // Act — 클릭(Confirm 발행) → Yes(HandleAsync)
        await vm.OnClickForceLogoutAllUserSessions(target);
        var trigger = ExtractAllTrigger(ea);
        await vm.HandleAsync(trigger, CancellationToken.None);

        // Assert — DELETE /user-sessions/user/{id} 도달 + 캡처된 userId=7
        Assert.Equal(1, s.ForceLogoutAllCallCount);
        Assert.Equal(7, s.LastForceLogoutAllUserId);
    }

    [Fact]
    public async Task should_reload_after_force_logout_all()
    {
        // Arrange — 전체 보기(false). userId=7 활성 2 + userId=8 활성 1 = 3건 로드
        var s = NewServerWithSessions(active: 2, inactive: 0, userId: 7);
        s.Sessions.Add(new UserSessionDto { Id = 99, UserId = 8, LoginId = "other", Role = "OPERATOR", IsActive = true });
        var (vm, ea) = NewVm(s);
        await vm.ActivateForTestAsync();
        Assert.Equal(3, vm.Items.Count);
        var callsBefore = s.SessionCallCount;
        var target = new UserSessionDto { Id = 1, UserId = 7, IsActive = true };

        // Act — 전체종료(userId=7의 2건 비활성화) → 성공 시 page1 재조회
        await vm.OnClickForceLogoutAllUserSessions(target);
        await vm.HandleAsync(ExtractAllTrigger(ea), CancellationToken.None);

        // Assert — 재조회 발생(GetUserSessions 재호출). 전체(false) 조회라 3건 유지되나
        //          userId=7 은 비활성, 활성으로 남은 건 userId=8 1건뿐(서버 부수효과 반영).
        Assert.True(s.SessionCallCount > callsBefore);
        Assert.Equal(3, vm.Items.Count);
        var stillActive = Assert.Single(vm.Items, x => x.IsActive);
        Assert.Equal(8, stillActive.UserId);
    }

    // ─────────────────────────── FR-3: 기본 전체 표시 ───────────────────────────

    [Fact]
    public async Task should_default_to_all_sessions_when_activated()
    {
        // Arrange — 활성 3 + 비활성 2. 기본 IsActiveOnly=false 여야 전체(5) + is_active 미전송
        var s = NewServerWithSessions(active: 3, inactive: 2);
        var (vm, _) = NewVm(s);

        // Act
        await vm.ActivateForTestAsync();

        // Assert — 기본 전체 조망(is_active null → 5건 전체)
        Assert.False(vm.IsActiveOnly);
        Assert.Null(s.LastSessionIsActive);
        Assert.Equal(5, vm.Items.Count);
    }

    // ─────────────────────────── FR-3: 자동 갱신 가드 ───────────────────────────

    [Fact]
    public async Task should_skip_auto_refresh_when_scrolled_past_first_page()
    {
        // Arrange — 250 활성(3페이지). page1 로드 후 page2 로 스크롤(=현재 페이지 2)
        var s = NewServerWithSessions(active: 250, inactive: 0);
        var (vm, _) = NewVm(s);
        await vm.ActivateForTestAsync();   // page 1
        await vm.LoadNextPageAsync();       // page 2 → _currentPage=2
        Assert.Equal(200, vm.Items.Count);
        var callsBefore = s.SessionCallCount;

        // Act — 자동 갱신 시도(2페이지+ 스크롤 상태 → skip 되어야 함)
        await vm.TryAutoRefresh();

        // Assert — 서버 재호출 없음(무한스크롤 비방해) + 목록 유지
        Assert.Equal(callsBefore, s.SessionCallCount);
        Assert.Equal(200, vm.Items.Count);
    }

    [Fact]
    public async Task should_auto_refresh_when_on_first_page()
    {
        // Arrange — 50 활성(1페이지). 활성화 후 page1 상태(_currentPage=1)
        var s = NewServerWithSessions(active: 50, inactive: 0);
        var (vm, _) = NewVm(s);
        await vm.ActivateForTestAsync();   // page 1
        var callsBefore = s.SessionCallCount;

        // Act — page1 에서 자동 갱신 → 재조회 발생해야 함
        await vm.TryAutoRefresh();

        // Assert — 서버 재조회 1회 발생 + 목록 유지(50)
        Assert.Equal(callsBefore + 1, s.SessionCallCount);
        Assert.Equal(50, vm.Items.Count);
    }

    [Fact]
    public async Task should_not_auto_refresh_after_deactivate()
    {
        // Arrange — 50 활성(1페이지). 활성화 후 OnDeactivate(=_isTearingDown=true, 타이머 폐기)
        var s = NewServerWithSessions(active: 50, inactive: 0);
        var (vm, _) = NewVm(s);
        await vm.ActivateForTestAsync();
        await vm.DeactivateForTestAsync();   // teardown — 이후 늦은 tick 은 가드로 단락되어야 함
        var callsBefore = s.SessionCallCount;

        // Act — teardown 이후 자동 갱신 시도(늦은 타이머 tick 모사)
        await vm.TryAutoRefresh();

        // Assert — _isTearingDown 가드가 단락 → 서버 재호출 없음(teardown 후 Items 변이·재조회 금지)
        Assert.Equal(callsBefore, s.SessionCallCount);
    }

    // ─────────────────────────── FR-1: ISO 날짜 컨버터 ───────────────────────────

    [Fact]
    public void should_format_iso_string_when_valid()
    {
        // Arrange
        var conv = new IsoDateStringConverter();

        // Act — ISO(+09:00 오프셋 포함) → 로컬 "yyyy-MM-dd HH:mm"
        var result = conv.Convert("2026-07-22T14:30:00", null!, null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("2026-07-22 14:30", result);
    }

    [Fact]
    public void should_return_raw_when_unparseable()
    {
        // Arrange
        var conv = new IsoDateStringConverter();

        // Act — 파싱 불가 문자열은 원문 폴백(크래시 금지)
        var result = conv.Convert("not-a-date", null!, null, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("not-a-date", result);
    }
}
