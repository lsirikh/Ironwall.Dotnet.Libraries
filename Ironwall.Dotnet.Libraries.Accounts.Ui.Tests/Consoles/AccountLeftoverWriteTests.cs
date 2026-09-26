using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 계정 창 잔여 결함 — 라이브 왕복(tools/live-api-roundtrip, accounts-left AL1~AL4)의 헤드리스 짝.
/// 서버에 무엇이 실리는지는 라이브 하네스와 Accounts.Api 시험이, 여기서는 창이 <b>무엇을 넘기고 무엇을 말하는지</b>를 본다.
/// </summary>
public class AccountLeftoverWriteTests
{
    // ── AL1: 편집 다이얼로그 [확인] 은 바뀐 칸만 넘긴다 ─────────────────────

    private static (EditorDialogViewModel Editor, FakeDirectoryGateway Gateway, CapturingEventAggregator Events) OpenEditor(AccountModel target)
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway();
        gateway.Accounts.Add(target);
        var editor = new EditorDialogViewModel(events, log, new AccountViewModel(events, log, new AccountModel()),
            gateway, new FakeSessionConfig(), new FakeProfileImageService(), new FakeProfileGateway());
        editor.BeginEdit(target);
        return (editor, gateway, events);
    }

    private static AccountModel Target() => new()
    {
        Id = 5, Username = "op5", Name = "김운영", Role = EnumUserRole.USER, Level = EnumLevelType.USER,
        Used = EnumUsedType.USED, Department = "경비과", EMail = "op5@example.com", Phone = "010-0000-0005",
    };

    [Fact]
    public async Task should_pass_only_department_when_the_editor_changes_only_the_department()
    {
        var (editor, gateway, _) = OpenEditor(Target());

        editor.ViewModel.Department = "상황실";
        await editor.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None);

        var fields = Assert.Single(gateway.UpdatedFields);
        Assert.Equal(new[] { "department" }, fields);
    }

    [Fact]
    public async Task should_pass_is_active_and_cleared_fields_when_status_and_contacts_are_changed()
    {
        var (editor, gateway, _) = OpenEditor(Target());

        editor.ViewModel.Used = EnumUsedType.NOT_USED;
        editor.ViewModel.EMail = "";      // 화면 입력 칸을 비우면 빈 글
        editor.ViewModel.Phone = "";
        await editor.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None);

        var fields = Assert.Single(gateway.UpdatedFields);
        Assert.Equal(new[] { "email", "is_active", "phone" }, fields.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task should_not_write_when_nothing_changed_in_the_editor()
    {
        var (editor, gateway, events) = OpenEditor(Target());

        await editor.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None);

        Assert.Equal(0, gateway.UpdateCallCount);
        Assert.Empty(gateway.UpdatedFields);
        Assert.Equal(EditorDialogViewModel.NothingChangedText, events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    [Fact]
    public async Task should_pass_role_when_the_editor_promotes_the_account_to_admin()
    {
        var (editor, gateway, _) = OpenEditor(Target());

        editor.ViewModel.Role = EnumUserRole.ADMIN;
        await editor.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { "role" }, Assert.Single(gateway.UpdatedFields));
    }

    [Fact]
    public async Task should_compare_against_the_newly_opened_account_when_the_editor_is_reopened_for_another_row()
    {
        var (editor, gateway, _) = OpenEditor(Target());
        var other = Target();
        other.Id = 6; other.Username = "op6"; other.Department = "상황실";

        editor.BeginEdit(other);
        editor.ViewModel.Position = "반장";
        await editor.HandleAsync(new CallEditAccountAdminProcessMessageModel(), CancellationToken.None);

        Assert.Equal(new[] { "position" }, Assert.Single(gateway.UpdatedFields));
    }

    // ── AL2: 세션 정책 범위 검사 = 서버 SessionSettingsUpdate 규칙 ──────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(21)]
    [InlineData(-1)]
    public void should_reject_the_lockout_threshold_when_it_is_outside_zero_or_three_to_twenty(int threshold)
    {
        var message = AccountSetupPanelViewModel.ValidatePolicy(24, 7, threshold, 30, "allow", 0, 0);

        Assert.NotNull(message);
        Assert.Contains("3~20", message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(20)]
    public void should_accept_the_lockout_threshold_when_it_is_zero_or_three_to_twenty(int threshold)
    {
        Assert.Null(AccountSetupPanelViewModel.ValidatePolicy(24, 7, threshold, 30, "allow", 0, 0));
    }

    [Theory]
    [InlineData(0, 7, 5, 30, "allow", 0, 0, "1~168")]
    [InlineData(169, 7, 5, 30, "allow", 0, 0, "1~168")]
    [InlineData(24, 0, 5, 30, "allow", 0, 0, "1~90")]
    [InlineData(24, 91, 5, 30, "allow", 0, 0, "1~90")]
    [InlineData(24, 7, 5, 1441, "allow", 0, 0, "1~1440")]
    [InlineData(24, 7, 5, -1, "allow", 0, 0, "1~1440")]
    [InlineData(24, 7, 5, 30, "single", 0, 0, "evict_all")]
    [InlineData(24, 7, 5, 30, "allow", 101, 0, "0~100")]
    [InlineData(24, 7, 5, 30, "allow", 0, 3651, "0~3650")]
    public void should_name_the_offending_range_when_a_session_policy_value_is_out_of_the_server_range(
        int timeout, int refresh, int threshold, int duration, string policy, int maxSessions, int retention, string expected)
    {
        var message = AccountSetupPanelViewModel.ValidatePolicy(timeout, refresh, threshold, duration, policy, maxSessions, retention);

        Assert.NotNull(message);
        Assert.Contains(expected, message);
    }

    [Fact]
    public void should_accept_the_server_boundaries_when_every_value_is_at_its_limit()
    {
        Assert.Null(AccountSetupPanelViewModel.ValidatePolicy(1, 1, 0, 0, "evict_all", 0, 0));
        Assert.Null(AccountSetupPanelViewModel.ValidatePolicy(168, 90, 20, 1440, "allow", 100, 3650));
    }

    // ── AL3: 서버 모드 등록은 빈 로컬 목록으로 ADMIN 을 추론하지 않는다 ─────

    private static (RegisterDialogViewModel Dialog, FakeDirectoryGateway Gateway) BuildRegister(bool canInferFirstAdmin)
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway { CanInferFirstAccountAdmin = canInferFirstAdmin };
        var dialog = new RegisterDialogViewModel(events, log, new RegisterViewModel(events, log, new AccountModel()),
            new AccountProvider(), gateway, new FakeProfileImageService());   // 빈 로컬 목록
        return (dialog, gateway);
    }

    [Fact]
    public async Task should_register_as_user_when_the_server_mode_local_list_is_empty()
    {
        var (dialog, gateway) = BuildRegister(canInferFirstAdmin: false);
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = "new1";
        dialog.Name = "신규";
        dialog.RegisterPass = "abcdefgh1";
        dialog.PasswordConfirm = "abcdefgh1";

        await dialog.ClickOk();

        var created = Assert.Single(gateway.Accounts);
        Assert.Equal(EnumLevelType.USER, created.Level);
    }

    [Fact]
    public async Task should_keep_the_first_account_admin_rule_when_the_directory_is_the_local_database()
    {
        var (dialog, gateway) = BuildRegister(canInferFirstAdmin: true);
        await Caliburn.Micro.ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = "first";
        dialog.Name = "최초";
        dialog.RegisterPass = "abcdefgh1";
        dialog.PasswordConfirm = "abcdefgh1";

        await dialog.ClickOk();

        Assert.Equal(EnumLevelType.ADMIN, Assert.Single(gateway.Accounts).Level);
    }

    // ── AL4: 마이페이지 사원번호 — 서버 모드는 본인이 바꿀 수 없다 ──────────

    /// <summary>서버 본인 수정 흉내 — 사원번호는 저장하지 않고 원래 값을 에코한다.</summary>
    private sealed class ServerLikeProfileGateway : IProfileGateway
    {
        private readonly AccountModel _stored;
        public ServerLikeProfileGateway(AccountModel stored) => _stored = stored;
        public bool CanSelfEditEmployeeNumber => false;
        public Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default)
        {
            var copy = new AccountModel(); copy.Update(_stored);
            return Task.FromResult<IAccountModel?>(copy);
        }
        public Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default)
        {
            var employeeNumber = _stored.EmployeeNumber;
            _stored.Update(acc);
            _stored.EmployeeNumber = employeeNumber;   // PUT /users/me 는 employee_number 를 받지 않는다
            var echo = new AccountModel(); echo.Update(_stored);
            return Task.FromResult<IAccountModel?>(echo);
        }
        public Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
        public Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> DeletePhotoAsync(CancellationToken ct = default) => Task.FromResult(false);
    }

    private static (MyPagePanelViewModel Page, CapturingEventAggregator Events) OpenMyPage(IProfileGateway gateway)
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var login = new LoginViewModel(events, log, new AccountModel { Id = 5, Username = "op5", Name = "김운영", EmployeeNumber = "EMP-1" });
        return (new MyPagePanelViewModel(events, log, login, gateway, new FakeProfileImageService()), events);
    }

    [Fact]
    public async Task should_tell_the_user_the_employee_number_was_not_saved_when_the_server_echo_keeps_the_old_value()
    {
        var (page, events) = OpenMyPage(new ServerLikeProfileGateway(Target().WithEmployeeNumber("EMP-1")));

        page.ViewModel.EmployeeNumber = "EMP-2";
        await page.HandleAsync(new CallEditProcessMessageModel(), CancellationToken.None);

        var done = events.OfType<OpenInfoPopupMessageModel>().Last().Explain;
        Assert.EndsWith(MyPagePanelViewModel.EmployeeNumberNotSavedNote, done);
        Assert.Equal("EMP-1", page.ViewModel.EmployeeNumber);   // 화면은 서버 값으로 되돌아간다
    }

    [Fact]
    public async Task should_not_add_the_employee_number_note_when_the_employee_number_was_not_touched()
    {
        var (page, events) = OpenMyPage(new ServerLikeProfileGateway(Target().WithEmployeeNumber("EMP-1")));

        page.ViewModel.Department = "상황실";
        await page.HandleAsync(new CallEditProcessMessageModel(), CancellationToken.None);

        Assert.DoesNotContain("사원번호", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    [Fact]
    public void should_make_the_employee_number_read_only_when_the_profile_gateway_cannot_self_edit_it()
    {
        var (serverPage, _) = OpenMyPage(new ServerLikeProfileGateway(Target()));
        var (dbPage, _) = OpenMyPage(new FakeProfileGateway());   // 기본구현 = 편집 가능(DB 모드)

        Assert.True(serverPage.IsEmployeeNumberReadOnly);
        Assert.False(dbPage.IsEmployeeNumberReadOnly);
    }
}

internal static class AccountModelTestExtensions
{
    public static AccountModel WithEmployeeNumber(this AccountModel model, string employeeNumber)
    {
        model.EmployeeNumber = employeeNumber;
        return model;
    }
}
