using System.IO;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Converters;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.3(ACC) 중 계정 · 권한 콘솔 항목을 창 없이 확인한다.
               진짜 콘솔 뷰모델 위에 가짜 API · 게이트웨이(ConsoleFixtures)를 붙인다 — 서버 · DB 에 붙지 않는다.
   Notes     : 실제 마우스 끌기 · 키보드 포커스 · 별도 창은 여기서 보지 않는다(헤디드 몫).
****************************************************************************/
public class AccountConsoleScenarioTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string ConsoleView()
        => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", "AccountConsolePanelView.xaml"));

    private static IEnumerable<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel> TwoUsers() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영", department: "경비과", locked: true),
        ConsoleFixtures.User(2, "op2", "이감시", department: "상황실"),
    };

    private static UserGroupDto Group(int id, string name) => new() { Id = id, Name = name, IsActive = true, Permissions = new PermissionsDto() };

    // ── ACC-001: 열기 — 제목 「계정 · 권한」, 첫 화면은 사용자 레일 ─────────────────
    [Trait("Scenario", "ACC-001")]
    [Fact]
    public async Task should_open_on_the_users_rail_under_the_accounts_title_when_the_console_is_opened()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());

        await console.ActivateForTestAsync();

        Assert.Contains("Title=\"계정 · 권한\"", ConsoleView());
        Assert.True(console.IsUsersRail);
        Assert.Equal(AccountConsoleKeys.Users, console.SelectedRail!.Key);
        Assert.Equal(2, console.AccountManagerPanelViewModel.ViewModelProvider.Count);
    }

    // ── ACC-003: 레일 6 — 고를 때마다 내용이 바뀐다 ───────────────────────────────
    [Trait("Scenario", "ACC-003")]
    [Fact]
    public async Task should_switch_the_content_to_each_of_the_six_rails_when_each_rail_is_picked()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();
        var seen = new HashSet<string>();

        foreach (var key in console.RailEntries.Select(e => e.Key).ToList())
        {
            Assert.True(await console.SelectRailAsync(key));
            Assert.Equal(key, console.SelectedRail!.Key);

            var flags = new[] { console.IsUsersRail, console.IsPermissionsRail, console.IsSessionsRail,
                                console.IsGrantsRail, console.IsAuditRail, console.IsSessionSetupRail };
            Assert.Single(flags, f => f);                               // 한 레일만 켜진다
            seen.Add($"{console.DetailKind}|{console.AddText}|{console.ShowSearch}|{console.ShowAddButton}|{console.IsDetailRequested}");
        }

        Assert.Equal(6, console.RailEntries.Count);
        Assert.True(seen.Count >= 5, string.Join(" / ", seen));       // 레일마다 상세 · 툴바 모양이 다르다
    }

    // ── ACC-005: 레일 배지 · 바닥 ────────────────────────────────────────────────
    [Trait("Scenario", "ACC-005")]
    [Fact]
    public async Task should_count_users_and_locked_users_on_the_rail_badge_and_footer()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();

        var users = console.RailEntries.Single(e => e.Key == AccountConsoleKeys.Users);
        Assert.Equal(2, users.Count);
        Assert.Equal(1, users.BadCount);
        Assert.Equal("계정 2명 / 잠김 1명", console.RailFooterText);
    }

    // ── ACC-006: 레일별 툴바 — 맞는 곳에만 ────────────────────────────────────────
    [Trait("Scenario", "ACC-006")]
    [Theory]
    [InlineData(AccountConsoleKeys.Users, true, true, true, true)]
    [InlineData(AccountConsoleKeys.Permissions, true, true, false, false)]
    [InlineData(AccountConsoleKeys.Sessions, false, false, false, false)]
    [InlineData(AccountConsoleKeys.Grants, true, false, false, false)]
    [InlineData(AccountConsoleKeys.Audit, false, false, false, false)]
    [InlineData(AccountConsoleKeys.SessionSetup, false, false, false, false)]
    public async Task should_show_only_the_toolbar_buttons_that_mean_something_on_each_rail(
        string rail, bool add, bool delete, bool search, bool columns)
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(rail);

        Assert.Equal(add, console.ShowAddButton);
        Assert.Equal(delete, console.ShowDeleteButton);
        Assert.Equal(search, console.ShowSearch);
        Assert.Equal(columns, console.ShowColumnsButton);
        Assert.Equal(rail == AccountConsoleKeys.Users, console.ShowGroupChips);
    }

    // ── ACC-007: 검색 + 빈 상태 ─────────────────────────────────────────────────
    [Trait("Scenario", "ACC-007")]
    [Fact]
    public async Task should_say_no_user_matches_the_search_when_the_search_text_matches_nobody()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();

        console.SearchText = "없는사람";

        Assert.True(console.ShowListEmpty);
        Assert.Equal("검색에 맞는 사용자가 없습니다", console.ListEmptyTitle);
        Assert.Equal("검색어를 바꾸거나 지워 보세요.", console.ListEmptyHint);
        Assert.Equal("목록 0건(전체 2) · 선택 0", console.ListStatusText);
        Assert.All(console.AccountManagerPanelViewModel.ViewModelProvider, row => Assert.False(console.MatchesSearch(row)));
    }

    // ── ACC-010: 행 → 상세(칸 · 세션 · 잠금) ─────────────────────────────────────
    [Trait("Scenario", "ACC-010")]
    [Fact]
    public async Task should_fill_the_detail_with_fields_sessions_and_lock_state_when_a_row_is_picked()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();
        console.UserSessionPanelViewModel.Items.Add(new UserSessionDto { Id = 9, UserId = 1, LoginId = "op1", IsActive = true });
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op1");

        Assert.True(console.OnUsersSelected(new List<object> { row }));

        Assert.True(console.IsDetailRequested);
        Assert.Equal("김운영", console.Detail.SingleTitle);
        Assert.Equal("op1", console.Detail.SingleNumber);
        Assert.Equal("경비과", console.Form.Fields.Single(f => f.Key == "department").Text);
        Assert.Equal("1건", console.SessionSummaryText);
        Assert.Equal("잠김", console.LockStateText);
        Assert.Equal("로그인 5회 실패", console.LockReasonText);
    }

    // ── ACC-013: 세션 끊기 · 잠금 해제 ──────────────────────────────────────────
    [Trait("Scenario", "ACC-013")]
    [Fact]
    public async Task should_unlock_through_the_confirmation_and_ask_before_ending_sessions_when_detail_actions_are_used()
    {
        var (console, _, gateway, _, events) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();
        console.UserSessionPanelViewModel.Items.Add(new UserSessionDto { Id = 9, UserId = 1, LoginId = "op1", IsActive = true });
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op1");
        console.OnUsersSelected(new List<object> { row });

        // 세션 끊기 — 확인을 먼저 묻는다(바로 끊지 않는다)
        Assert.True(console.CanEndUserSessions);
        await console.EndUserSessionsAsync();
        var endConfirm = events.OfType<OpenConfirmPopupMessageModel>().Last();
        Assert.Contains("'op1' 사용자의 활성 세션을 모두 강제 로그아웃", endConfirm.Explain);
        Assert.IsType<CallForceLogoutAllUserSessionsMessageModel>(endConfirm.MessageModel);

        // 잠금 해제 — 확인 → [확인] → 게이트웨이 1회 → 해제 안내
        Assert.True(console.CanUnlockSelected);
        await console.UnlockSelectedAsync();
        var unlockConfirm = events.OfType<OpenConfirmPopupMessageModel>().Last();
        var call = Assert.IsType<CallUnlockAccountMessageModel>(unlockConfirm.MessageModel);
        Assert.Equal(0, gateway.UnlockCallCount);                    // 묻기만 했다

        await console.AccountManagerPanelViewModel.HandleAsync(call, CancellationToken.None);

        Assert.Equal(1, gateway.UnlockCallCount);
        Assert.Equal("'op1' 계정의 잠금을 해제했습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    // ── ACC-014: 삭제 + 취소 ───────────────────────────────────────────────────
    [Trait("Scenario", "ACC-014")]
    [Fact]
    public async Task should_keep_the_row_when_delete_is_cancelled_and_remove_it_when_delete_is_confirmed()
    {
        var (console, _, gateway, _, events) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        console.OnUsersSelected(new List<object> { row });

        await console.DeleteAsync();
        var confirm = events.OfType<OpenConfirmPopupMessageModel>().Last();
        Assert.Equal("선택한 1개 계정을 삭제하시겠습니까?", confirm.Explain);
        Assert.IsType<CallDeleteAccountAdminProcessMessageModel>(confirm.MessageModel);

        // [취소] — 확인 팝업은 우리 메시지를 내지 않는다 → 아무것도 지워지지 않는다
        Assert.Equal(2, gateway.Accounts.Count);

        // [확인]
        await console.AccountManagerPanelViewModel.HandleAsync(new CallDeleteAccountAdminProcessMessageModel(), CancellationToken.None);

        Assert.DoesNotContain(gateway.Accounts, a => a.Username == "op2");
        Assert.DoesNotContain(console.AccountManagerPanelViewModel.ViewModelProvider, r => r.Username == "op2");
        Assert.Equal("1개 계정을 삭제했습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    // ── ACC-017: 빈 상세 안내 — 레일마다 선택 없음 ────────────────────────────────
    [Trait("Scenario", "ACC-017")]
    [Theory]
    [InlineData(AccountConsoleKeys.Users, "선택한 항목 없음")]
    [InlineData(AccountConsoleKeys.Sessions, "선택한 세션 없음")]
    [InlineData(AccountConsoleKeys.Audit, "선택한 기록 없음")]
    public async Task should_say_nothing_is_selected_in_the_detail_when_a_rail_has_no_selection(string rail, string expected)
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(rail);

        Assert.Equal(expected, console.DetailTitle);
        Assert.False(console.IsDetailRequested);
    }

    [Trait("Scenario", "ACC-017")]
    [Fact]
    public async Task should_ask_to_pick_a_group_in_the_detail_when_the_permission_rail_has_no_selection()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers(), new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(AccountConsoleKeys.Permissions);

        Assert.True(console.ShowNoGroupState);
        Assert.Equal("권한 그룹을 고르세요", console.NoGroupTitle);
        Assert.Contains("그룹을 고르면", console.NoGroupHint);
    }

    // ── ACC-018: 권한 빈 상태 — 그룹이 하나도 없다 ────────────────────────────────
    [Trait("Scenario", "ACC-018")]
    [Fact]
    public async Task should_point_at_the_new_group_button_when_there_is_no_permission_group()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(AccountConsoleKeys.Permissions);

        Assert.True(console.ShowNoGroupState);
        Assert.False(console.ShowMatrixGrid);
        Assert.Equal("권한 그룹이 없습니다", console.NoGroupTitle);
        Assert.Contains("[+ 새 그룹]", console.NoGroupHint);
        Assert.Equal("새 그룹", console.AddText);
        Assert.True(console.CanAdd);
    }

    // ── ACC-021: 매트릭스 상태 · 그룹 요약(초안 수 · 모듈 · 인원) ─────────────────
    [Trait("Scenario", "ACC-021")]
    [Fact]
    public async Task should_report_draft_count_enabled_modules_and_member_count_when_a_cell_is_edited()
    {
        var permissions = new PermissionsDto();
        permissions.Modules["devices"] = new ModulePermissionDto { View = true };
        var group = new UserGroupDto { Id = 10, Name = "관제", IsActive = true, Permissions = permissions };
        var (console, api, _, _, _) = ConsoleFixtures.Build(TwoUsers(), new[] { group });
        foreach (var user in api.Users) user.GroupId = 10;          // 두 명 모두 이 그룹
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.Single();

        Assert.Equal(2, console.PermissionMatrixPanelViewModel.Groups.Single().UserCount);   // 인원
        Assert.StartsWith("켜진 모듈 ", console.MatrixStatusText);                            // 모듈
        Assert.Equal(AccountConsolePanelViewModel.NoChangesText, console.DetailFooter);

        console.Matrix.ToggleCell(0, 1);

        Assert.Equal(1, console.Matrix.DirtyCount);
        Assert.Equal("변경 1건 미적용", console.DetailFooter);                                // 초안 수
    }

    // ── ACC-022: 구성원 탭 — 빈 그룹 안내 ────────────────────────────────────────
    [Trait("Scenario", "ACC-022")]
    [Fact]
    public async Task should_show_the_empty_members_state_when_the_group_has_no_member()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers(), new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.Single();

        console.Matrix.ShowMembers = true;
        await Task.Yield();

        Assert.True(console.ShowMembersPane);
        Assert.Empty(console.PermissionMatrixPanelViewModel.Members);
        Assert.True(console.ShowNoMembersState);
    }

    // ── ACC-024: 세션 상세 — 칸 · 종료 사유 ──────────────────────────────────────
    [Trait("Scenario", "ACC-024")]
    [Fact]
    public async Task should_open_the_session_detail_with_its_fields_when_a_session_is_picked()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Sessions);

        console.SelectedSession = new UserSessionDto
        {
            Id = 7, UserId = 2, LoginId = "op2", IsActive = false, IpAddress = "10.0.0.7", LogoutReason = "FORCED",
        };

        Assert.True(console.IsDetailRequested);
        Assert.Equal("op2", console.DetailTitle);
        var view = ConsoleView();
        foreach (var path in new[] { "SelectedSession.Role", "SelectedSession.ClientId", "SelectedSession.IpAddress",
                                     "SelectedSession.UserAgent", "SelectedSession.CreatedAt", "SelectedSession.ExpiresAt",
                                     "SelectedSession.LogoutReason, Converter={StaticResource LogoutReason}" })
            Assert.Contains(path, view);
    }

    [Trait("Scenario", "ACC-024")]
    [Theory]
    [InlineData("FORCED", "강제 종료")]
    [InlineData("DUPLICATE", "중복 로그인 축출")]
    [InlineData("EXPIRED", "세션 만료")]
    [InlineData("SELF_LOGOUT", "본인 로그아웃")]
    [InlineData("PASSWORD_RESET", "비밀번호 초기화")]      // G06 — 8.0.15 서버 종료 사유
    [InlineData("ACCOUNT_LOCKED", "계정 잠금")]
    [InlineData("LOCKED", "계정 잠금")]
    [InlineData("ACCOUNT_DEACTIVATED", "계정 사용 중지")]
    [InlineData("ACCOUNT_DELETED", "계정 삭제")]
    [InlineData("MANUAL", "로그아웃")]
    [InlineData("SOMETHING_NEW", "알 수 없음")]
    [InlineData(null, "")]
    public void should_show_the_logout_reason_in_korean_when_the_session_detail_is_drawn(string? code, string expected)
        => Assert.Equal(expected, new LogoutReasonConverter().Convert(code, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));

    // ── ACC-025: 강제 로그아웃 — 자기 세션 포함 경고 ──────────────────────────────
    [Trait("Scenario", "ACC-025")]
    [Fact]
    public async Task should_warn_that_my_own_session_is_included_when_force_logout_all_targets_my_account()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(TwoUsers());
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Sessions);

        console.SelectedSession = new UserSessionDto { Id = 3, UserId = 2, LoginId = "op2", IsActive = true };
        Assert.Equal("이 사용자 전체 종료", console.ForceLogoutAllText);

        var mine = new UserSessionDto { Id = 1, UserId = 1, LoginId = "admin", IsActive = true };   // 가짜 권한의 로그인 = admin, 토큰 사용자 = 1
        console.SelectedSession = mine;
        Assert.Equal("이 사용자 전체 종료 (내 세션 포함)", console.ForceLogoutAllText);

        await console.UserSessionPanelViewModel.OnClickForceLogoutAllUserSessions(mine);
        Assert.Contains("본인도 즉시 로그아웃", events.OfType<OpenConfirmPopupMessageModel>().Last().Explain);
    }
}
