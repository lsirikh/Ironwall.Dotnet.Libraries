using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 완성도 수정 패스(2026-09-27) — 감사 문서 계정 절 A-1 … A-52 의 회귀 시험.
/// 레일별 상세 열림 · 표시 사전 · 변경 전후 표 · 저장 막대 · 빈 상태 · 운영자 문구(금지어).
/// </summary>
public class AccountCompletenessFixTests
{
    // ── A-29 · A-30 · A-31: 상세 칸은 레일마다 연다 ───────────────────────────────
    [Fact]
    public async Task should_open_the_detail_when_a_session_is_selected_on_the_sessions_rail()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Sessions);

        Assert.False(console.IsDetailRequested);

        console.SelectedSession = new UserSessionDto { Id = 7, UserId = 2, LoginId = "op1", IsActive = true };

        Assert.True(console.IsDetailRequested);
    }

    [Fact]
    public async Task should_always_open_the_detail_when_the_grants_rail_is_shown()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(AccountConsoleKeys.Grants);

        Assert.True(console.IsDetailRequested);   // 새 부여 폼이 상세의 본문이다
        Assert.True(console.ShowAddButton);
        Assert.Equal("새 부여", console.AddText);
    }

    [Fact]
    public async Task should_open_the_detail_when_an_audit_record_is_selected()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Audit);

        Assert.False(console.IsDetailRequested);

        console.SelectedAuditLog = new AuditLogDto { Id = 1, ActionType = "USER_UPDATED", ActionStatus = "SUCCESS", ResourceType = "USER" };

        Assert.True(console.IsDetailRequested);
        Assert.Equal("사용자 정보 변경", console.DetailTitle);
    }

    [Fact]
    public async Task should_not_request_the_detail_when_the_session_setup_rail_is_shown()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(AccountConsoleKeys.SessionSetup);

        Assert.False(console.IsDetailRequested);
        Assert.False(console.ShowAddButton);
        Assert.False(console.ShowDeleteButton);
    }

    // ── A-9 · A-10: 그룹을 고르기 전 [+ 새 그룹] ──────────────────────────────────
    [Fact]
    public async Task should_open_the_detail_with_the_new_group_form_when_no_group_is_selected()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(groups: new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        Assert.False(console.IsDetailRequested);

        console.Add();

        Assert.True(console.PermissionMatrixPanelViewModel.IsGroupFormOpen);
        Assert.True(console.IsDetailRequested);
        Assert.Equal("새 권한 그룹", console.DetailTitle);
        Assert.Equal("만들기", console.PermissionMatrixPanelViewModel.FormSaveText);
    }

    [Fact]
    public async Task should_title_the_form_as_an_edit_when_rename_is_opened_for_a_group()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(groups: new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.Single();

        console.PermissionMatrixPanelViewModel.OnClickRenameGroup();

        Assert.Equal("그룹 이름/설명 수정", console.PermissionMatrixPanelViewModel.FormTitle);
        Assert.Equal("저장", console.PermissionMatrixPanelViewModel.FormSaveText);
    }

    // ── A-7 · X3: 빈 상태 ─────────────────────────────────────────────────────────
    [Fact]
    public async Task should_show_the_no_group_state_instead_of_an_empty_matrix_when_no_group_is_selected()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(groups: new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);

        Assert.True(console.ShowNoGroupState);
        Assert.False(console.ShowMatrixGrid);
        Assert.Equal("권한 그룹을 고르세요", console.NoGroupTitle);

        console.Matrix.SelectedGroup = console.Matrix.Groups.Single();

        Assert.False(console.ShowNoGroupState);
        Assert.True(console.ShowMatrixGrid);
    }

    [Fact]
    public async Task should_describe_the_empty_list_when_a_rail_has_no_rows()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();

        await console.SelectRailAsync(AccountConsoleKeys.Audit);

        Assert.True(console.ShowListEmpty);
        Assert.Equal("이 기간의 감사 기록이 없습니다", console.ListEmptyTitle);
        Assert.False(string.IsNullOrWhiteSpace(console.ListEmptyHint));
    }

    // ── A-11: 행 [전체] ─────────────────────────────────────────────────────────
    [Fact]
    public async Task should_turn_on_every_applicable_cell_of_the_row_when_the_row_button_is_pressed()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(groups: new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.Single();
        var row = console.Matrix.Modules.First(m => m.ModuleKey == "devices");

        Assert.True(console.Matrix.ToggleRow(row));

        Assert.True(row.View && row.Edit && row.Delete && row.Control);
        Assert.True(console.Matrix.IsDirty);
    }

    // ── A-18: 구성원 추가 — 기존 배정 경로 그대로 ───────────────────────────────────
    [Fact]
    public async Task should_assign_the_chosen_account_to_the_group_when_add_member_is_pressed()
    {
        var users = new[] { ConsoleFixtures.User(1, "admin", "관리"), ConsoleFixtures.User(2, "op1", "운영") };
        var (console, api, _, _, _) = ConsoleFixtures.Build(users, new[] { Group(10, "관제") });
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.Single();
        console.Matrix.ShowMembers = true;
        await Task.Yield();
        var target = console.PermissionMatrixPanelViewModel.AddableAccounts.First(a => a.LoginId == "op1");
        console.PermissionMatrixPanelViewModel.SelectedAddAccount = target;
        Assert.True(console.CanAddMember);

        await console.AddMemberAsync();

        Assert.Contains((2, (int?)10), api.AssignCalls);
    }

    // ── A-21 · A-32 · A-34 · A-48: 표시 사전 ─────────────────────────────────────
    [Theory]
    [InlineData("USER_CREATED", "사용자 등록")]
    [InlineData("PERMISSION_CHANGED", "권한 변경")]
    [InlineData("SESSION_FORCED_LOGOUT", "강제 로그아웃")]
    [InlineData("SOMETHING_NEW", "알 수 없음")]
    [InlineData(null, "")]
    public void should_translate_audit_actions_when_the_code_is_known_or_fall_back_when_unknown(string? raw, string expected)
        => Assert.Equal(expected, AccountDisplay.AuditAction(raw));

    [Fact]
    public void should_cover_the_whole_server_vocabulary_when_the_audit_dictionaries_are_built()
    {
        // 루프백 시험 서버 OpenAPI 8.0.2 의 닫힌 집합(2026-09-27 확인).
        var actions = new[]
        {
            "USER_CREATED", "USER_UPDATED", "USER_DELETED", "USER_LOCKED", "USER_UNLOCKED", "USER_ACTIVATED", "USER_DEACTIVATED",
            "USER_PHOTO_CHANGED", "USER_PHOTO_DELETED", "PASSWORD_CHANGED", "PASSWORD_RESET", "ROLE_CHANGED", "GROUP_ASSIGNED",
            "GROUP_CREATED", "GROUP_UPDATED", "GROUP_DELETED", "PERMISSION_CHANGED", "GRANT_CREATED", "GRANT_REVOKED",
            "GRANT_EXPIRED", "SESSION_CREATED", "SESSION_TERMINATED", "SESSION_FORCED_LOGOUT",
        };
        Assert.Equal(actions.OrderBy(x => x), AccountDisplay.AuditActions.Keys.OrderBy(x => x));
        Assert.Equal(new[] { "PASSWORD", "USER", "USER_GROUP", "USER_SESSION" }, AccountDisplay.AuditResources.Keys.OrderBy(x => x));
        Assert.Equal(new[] { "FAILURE", "SUCCESS" }, AccountDisplay.AuditStatuses.Keys.OrderBy(x => x));
        Assert.Equal(new[] { "ACTIVE", "EXPIRED", "PENDING", "REVOKED" }, AccountDisplay.GrantStatuses.Keys.OrderBy(x => x));
        Assert.All(AccountDisplay.AuditActions.Values.Concat(AccountDisplay.GrantStatuses.Values),
                   text => Assert.Matches("^[가-힣 ·()]+$", text));
    }

    [Theory]
    [InlineData(EnumUserRole.ADMIN, "관리자")]
    [InlineData(EnumUserRole.USER, "사용자")]
    [InlineData(EnumUserRole.UNDEFINED, "알 수 없음")]
    public void should_show_roles_in_korean_when_the_role_is_displayed(EnumUserRole role, string expected)
        => Assert.Equal(expected, AccountDisplay.Role(role));

    [Fact]
    public void should_offer_korean_role_choices_and_write_back_the_enum_when_the_role_field_is_edited()
    {
        Assert.Equal(new[] { "관리자", "사용자" }, AccountFieldCatalog.RoleOptions);
        Assert.True(AccountDisplay.TryParseRole("관리자", out var admin));
        Assert.Equal(EnumUserRole.ADMIN, admin);
        Assert.True(AccountDisplay.TryParseRole("USER", out var user));   // 옛 표기도 받아 준다
        Assert.Equal(EnumUserRole.USER, user);
        Assert.False(AccountDisplay.TryParseRole("모름", out _));
    }

    [Fact]
    public void should_say_used_or_not_used_in_korean_when_the_state_is_displayed()
    {
        Assert.Equal("사용", AccountDisplay.Used(EnumUsedType.USED));
        Assert.Equal("미사용", AccountDisplay.Used(EnumUsedType.NOT_USED));
    }

    [Fact]
    public void should_flag_only_active_grants_ending_within_a_day_when_expiry_is_checked()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0);

        Assert.True(AccountDisplay.IsExpiringSoon("ACTIVE", now.AddHours(6), now));
        Assert.False(AccountDisplay.IsExpiringSoon("ACTIVE", now.AddDays(3), now));
        Assert.False(AccountDisplay.IsExpiringSoon("ACTIVE", null, now));             // 상시
        Assert.False(AccountDisplay.IsExpiringSoon("REVOKED", now.AddHours(6), now));
        Assert.False(AccountDisplay.IsExpiringSoon("ACTIVE", now.AddHours(-1), now)); // 이미 지났다
    }

    [Fact]
    public void should_render_server_time_in_seconds_when_an_iso_value_is_shown()
    {
        var text = AccountDisplay.Time("2026-09-27T01:05:56.647956+09:00");

        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", text);
        Assert.DoesNotContain("T", text);
    }

    // ── A-31: 변경 전후 표 ─────────────────────────────────────────────────────────
    [Fact]
    public void should_list_only_changed_fields_with_korean_names_when_changes_have_before_and_after()
    {
        // 서버 실기록 모양(2026-09-27 루프백 확인).
        var changes = JObject.Parse("""{"after":{"is_active":false,"department":"경비1과","name":"같음"},"before":{"is_active":true,"department":null,"name":"같음"}}""");

        var rows = AccountDisplay.Changes(changes);

        Assert.Equal(2, rows.Count);
        var active = rows.Single(r => r.Field == "상태");
        Assert.Equal("사용", active.Before);
        Assert.Equal("미사용", active.After);
        var department = rows.Single(r => r.Field == "부서");
        Assert.Equal("(없음)", department.Before);
        Assert.Equal("경비1과", department.After);
    }

    [Fact]
    public void should_flatten_permission_changes_into_module_and_verb_rows_when_a_group_permission_changed()
    {
        var changes = JObject.Parse("""
            {"before":{"permissions":{"modules":{"devices":{"view":true,"edit":false},"events":{"view":false,"edit":false}}}},
             "after":{"permissions":{"modules":{"devices":{"view":true,"edit":true},"events":{"view":false,"edit":false}}}}}
            """);

        var rows = AccountDisplay.Changes(changes);

        var row = Assert.Single(rows);
        Assert.Equal("장비 · 편집", row.Field);
        Assert.Equal("꺼짐", row.Before);
        Assert.Equal("켜짐", row.After);
    }

    [Fact]
    public async Task should_describe_the_selected_audit_record_in_korean_when_the_detail_is_shown()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Audit);

        console.SelectedAuditLog = new AuditLogDto
        {
            Id = 3, ActionType = "USER_UPDATED", ActionStatus = "FAILURE", ResourceType = "USER", ResourceName = "홍길동 (op1)",
            ActorLoginId = "admin", ActorName = "관리자", CreatedAt = "2026-09-27T01:05:56+09:00",
            Changes = JObject.Parse("""{"before":{"role":"USER"},"after":{"role":"ADMIN"}}"""),
        };

        Assert.Equal("실패", console.AuditStatusText);
        Assert.Equal("사용자 · 홍길동 (op1)", console.AuditResourceText);
        Assert.Equal("관리자(admin)", console.AuditActorText);
        var change = Assert.Single(console.AuditChanges);
        Assert.Equal(("구분", "사용자", "관리자"), (change.Field, change.Before, change.After));
    }

    // ── A-45: 저장은 바뀐 것이 있을 때만 ────────────────────────────────────────────
    [Fact]
    public async Task should_enable_save_only_when_a_policy_value_differs_from_the_loaded_one()
    {
        var setup = new AccountSetupPanelViewModel(new CapturingEventAggregator(), new SilentLog());
        await setup.ClickReload();                 // 서버가 없으면 기본값을 "불러온 값" 으로 삼는다
        setup.ServerSettingsAvailable = true;

        Assert.False(setup.CanClickSave);
        Assert.Equal("변경 없음", setup.ChangeSummaryText);

        setup.TimeoutHours += 1;

        Assert.True(setup.CanClickSave);
        Assert.Equal("변경 1건 · 저장하지 않음", setup.ChangeSummaryText);

        setup.TimeoutHours -= 1;

        Assert.False(setup.CanClickSave);
    }

    [Fact]
    public void should_fold_the_bottom_reload_button_when_the_setup_form_lives_in_the_console()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();

        Assert.True(console.AccountSetupPanelViewModel.IsHostedInConsole);
        Assert.False(console.AccountSetupPanelViewModel.ShowReloadButton);
    }

    // ── A-24 · A-28 · A-3: 상태 띠 문구 ─────────────────────────────────────────────
    [Fact]
    public void should_report_the_apply_result_in_plain_words_when_the_tray_is_applied()
    {
        var line = UserGroupDrop.AppliedLine(new Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.DraftApplySummary(3, 1, 0, 0, false));

        Assert.Equal("적용했습니다(3명). 1명은 옮기지 못했습니다 — [적용]을 다시 누르세요.", line);
        Assert.Equal("옮기려던 것을 취소했습니다.", UserGroupDrop.RevertedLine);
    }

    // ── 문구 규칙(원장 D-2026-09-26-648420): 운영자 화면에 개발 용어 · 영문 코드 · 평서형이 없다 ────────
    private static readonly string[] ForbiddenWords =
    {
        "Draft", "호출", "전체 교체", "PATCH", "PUT ", "422", "스키마", "판본", "서버 v", "appsettings", "폴백",
        "ADMIN", "USED", "(allow)", "evict_all", "revoked_at", "client_id", "setup_system", "실집행", "게이팅",
        "Refresh", "(예약)", "다음 판", "이번 판", "키 그대로",
    };

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    public static IEnumerable<object[]> OperatorXamlFiles()
    {
        var ui = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui");
        foreach (var relative in new[]
                 {
                     @"Views\Panels\AccountConsolePanelView.xaml", @"Views\Panels\AccountSetupPanelView.xaml",
                     @"Views\Panels\MyPagePanelView.xaml", @"Consoles\Forms\AccountDetailFormView.xaml",
                     @"Views\Dialogs\EditorDialogView.xaml", @"Views\Dialogs\RegisterDialogView.xaml",
                     @"Views\Dialogs\ResetPassDialogView.xaml", @"Views\Dialogs\DeleteAccountDialogView.xaml",
                 })
            yield return new object[] { Path.Combine(ui, relative) };
    }

    [Theory]
    [MemberData(nameof(OperatorXamlFiles))]
    public void should_keep_developer_words_out_of_visible_text_when_the_accounts_views_are_declared(string path)
    {
        var xaml = File.ReadAllText(path);
        xaml = Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);   // 주석은 화면에 안 나온다

        var visible = Regex.Matches(xaml, "\\b(?:Text|ToolTip|Content|Header|Title|Hint|Note|md:HintAssist\\.Hint)=\"([^\"{][^\"]*)\"")
                           .Select(m => m.Groups[1].Value)
                           .ToList();

        foreach (var text in visible)
        {
            foreach (var word in ForbiddenWords) Assert.DoesNotContain(word, text);
            // 평서형("없다", "만든다") 대신 존댓말 — 명사로 끝나는 라벨은 해당하지 않는다.
            Assert.False(Regex.IsMatch(text, "[^니]다\\.?$"), $"평서형 문장: '{text}'");
        }
    }

    [Fact]
    public void should_keep_developer_words_out_of_operator_messages_when_the_view_models_speak()
    {
        var messages = new[]
        {
            PermissionMatrixPanelViewModel.UnresolvedServerNote, PermissionMatrixPanelViewModel.SaveRejectedText,
            PermissionMatrixPanelViewModel.RemoveUnsupportedText, PermissionMatrixPanelViewModel.UnknownModuleName,
            AccountSetupPanelViewModel.UnavailableText, AccountSetupPanelViewModel.ForbiddenSaveText,
            AccountSetupPanelViewModel.ForbiddenLoadText, AccountFieldCatalog.GroupNote, UserGroupDrop.RevertedLine,
            UserGroupDrop.DropLine(UserGroupDrop.Plan(7, "야간조", Array.Empty<Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.AccountViewModel>()), 2),
            AccountConsolePanelViewModel.NoChangesText,
        };
        foreach (var message in messages)
            foreach (var word in ForbiddenWords) Assert.DoesNotContain(word, message);

        // 검증 문구도 화면 이름으로 — 종전 "refresh 토큰 유효기간" · "'단일(evict_all)'"(A-41).
        var refresh = AccountSetupPanelViewModel.ValidatePolicy(24, 0, 5, 30, "allow", 0, 0)!;
        Assert.DoesNotContain("refresh", refresh, StringComparison.OrdinalIgnoreCase);
        var policy = AccountSetupPanelViewModel.ValidatePolicy(24, 7, 5, 30, "single", 0, 0)!;
        Assert.DoesNotContain("evict_all", policy);
    }

    private static UserGroupDto Group(int id, string name)
        => new() { Id = id, Name = name, IsActive = true, Permissions = new PermissionsDto() };
}
