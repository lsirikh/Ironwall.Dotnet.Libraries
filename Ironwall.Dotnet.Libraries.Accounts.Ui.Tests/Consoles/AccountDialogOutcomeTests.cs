using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 완성도 P2 — 계정 다이얼로그가 서버 결과와 다른 말을 하지 않는다.
/// <list type="bullet">
/// <item>SC-ACC-071: 편집 창 [비밀번호 초기화] 는 서버가 성공했을 때만 "변경되었습니다" 를 띄운다.</item>
/// <item>SC-ACC-081: 서버 모드 내 정보 [계정 삭제] 는 비밀번호를 확인할 수 없어 막는다(서버 삭제를 부르지 않는다).</item>
/// </list>
/// 모든 시험은 가짜 게이트웨이만 쓴다 — 실제 계정을 지우거나 비밀번호를 바꾸지 않는다.
/// </summary>
public class AccountDialogOutcomeTests
{
    #region - 가짜 -
    /// <summary>비밀번호 초기화 · 삭제 결과를 시험이 정하는 디렉터리 게이트웨이.</summary>
    private sealed class ScriptedDirectoryGateway : IUserDirectoryGateway
    {
        public bool ResetSucceeds { get; set; } = true;
        public bool ResetThrows { get; set; }
        public int ResetCallCount { get; private set; }
        public int RemoveCallCount { get; private set; }

        public Task<IAccountModel?> ResetAccountPasswordAsync(IAccountModel acc, string newPassword, CancellationToken ct = default)
        {
            ResetCallCount++;
            if (ResetThrows) throw new System.Net.Http.HttpRequestException("connection refused (raw)");
            return Task.FromResult(ResetSucceeds ? acc : null);
        }

        public Task<bool> RemoveAccountAsync(IAccountModel acc, string currentPassword, CancellationToken ct = default)
        {
            RemoveCallCount++;
            return Task.FromResult(true);
        }

        public Task<List<IAccountModel>?> GetAllAccountsAsync(CancellationToken ct = default) => Task.FromResult<List<IAccountModel>?>(new List<IAccountModel>());
        public Task<IAccountModel?> CreateAccountAsync(IAccountModel acc, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
        public Task<IAccountModel?> UpdateAccountAsync(IAccountModel acc, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
        public Task<bool> IsUsernameTakenAsync(string username, CancellationToken ct = default) => Task.FromResult(false);
    }

    /// <summary>본인 삭제 가능 여부만 바꾸는 프로필 게이트웨이(서버 모드 = false).</summary>
    private sealed class ModeProfileGateway : IProfileGateway
    {
        public ModeProfileGateway(bool canSelfDelete) => CanSelfDeleteAccount = canSelfDelete;
        public bool CanSelfDeleteAccount { get; }
        public Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(null);
        public Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
        public Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default) => Task.FromResult<IAccountModel?>(acc);
        public Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> DeletePhotoAsync(CancellationToken ct = default) => Task.FromResult(false);
    }
    #endregion

    private static AccountModel Target() => new()
    {
        Id = 5, Username = "op5", Name = "김운영", Role = EnumUserRole.USER, Level = EnumLevelType.USER, Used = EnumUsedType.USED,
    };

    private static (EditorDialogViewModel Editor, ScriptedDirectoryGateway Gateway, CapturingEventAggregator Events, SilentLog Log) OpenEditor()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new ScriptedDirectoryGateway();
        var editor = new EditorDialogViewModel(events, log, new AccountViewModel(events, log, new AccountModel()),
            gateway, new FakeSessionConfig(), new FakeProfileImageService(), new FakeProfileGateway());
        editor.BeginEdit(Target());
        return (editor, gateway, events, log);
    }

    #region - 편집 창 비밀번호 초기화 (SC-ACC-071) -
    [Fact]
    public async Task should_show_success_and_close_when_the_server_resets_the_password()
    {
        var (editor, gateway, events, _) = OpenEditor();

        await editor.HandleAsync(new CallResetPasswordAdminProcessMessageModel(), CancellationToken.None);

        Assert.Equal(1, gateway.ResetCallCount);
        Assert.Equal(EditorDialogViewModel.ResetPasswordDoneText, events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Single(events.OfType<CloseDialogMessageModel>());
    }

    [Fact]
    public async Task should_not_claim_success_when_the_server_fails_to_reset_the_password()
    {
        var (editor, gateway, events, _) = OpenEditor();
        gateway.ResetSucceeds = false;

        await editor.HandleAsync(new CallResetPasswordAdminProcessMessageModel(), CancellationToken.None);

        var popups = events.OfType<OpenInfoPopupMessageModel>().Select(p => p.Explain).ToList();
        Assert.DoesNotContain(EditorDialogViewModel.ResetPasswordDoneText, popups);
        Assert.Equal(EditorDialogViewModel.ResetPasswordFailedText, popups.Last());
        Assert.Empty(events.OfType<CloseDialogMessageModel>());          // 창은 열어 둔다 — 다시 누를 수 있게
        Assert.Empty(events.OfType<RefreshAccountsMessageModel>());      // 바뀐 것이 없으니 목록도 다시 읽지 않는다
    }

    [Fact]
    public async Task should_show_a_fixed_sentence_and_log_the_raw_error_when_the_reset_call_throws()
    {
        var (editor, gateway, events, log) = OpenEditor();
        gateway.ResetThrows = true;

        await editor.HandleAsync(new CallResetPasswordAdminProcessMessageModel(), CancellationToken.None);

        var shown = events.OfType<OpenInfoPopupMessageModel>().Last().Explain;
        Assert.Equal(EditorDialogViewModel.ResetPasswordFailedText, shown);
        Assert.DoesNotContain("connection refused", shown);               // 원문은 팝업에 싣지 않는다
        Assert.Contains(log.Errors, e => e.Contains("connection refused"));   // 원문은 로그로
        Assert.Empty(events.OfType<CloseDialogMessageModel>());
    }
    #endregion

    #region - 내 정보 계정 삭제 (SC-ACC-081) -
    private static (DeleteAccountDialogViewModel Dialog, ScriptedDirectoryGateway Gateway, CapturingEventAggregator Events) OpenDeleteDialog(bool serverMode)
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new ScriptedDirectoryGateway();
        var login = new LoginViewModel(events, log, Target());
        var dialog = new DeleteAccountDialogViewModel(events, log, login, new AccountProvider(), gateway, new ModeProfileGateway(canSelfDelete: !serverMode));
        dialog.InputPassword = "whatever-typed";
        return (dialog, gateway, events);
    }

    [Fact]
    public async Task should_not_call_the_server_delete_when_self_delete_is_confirmed_in_server_mode()
    {
        var (dialog, gateway, events) = OpenDeleteDialog(serverMode: true);

        await dialog.ClickOk();

        Assert.Equal(0, gateway.RemoveCallCount);                         // 비밀번호를 확인하지 못한 채 지우지 않는다
        Assert.Equal(DeleteAccountDialogViewModel.SelfDeleteUnavailableText, events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Empty(events.OfType<CloseAllWindowsMessageModel>());       // 로그아웃 · 창 닫기도 없다
    }

    [Fact]
    public async Task should_let_the_gateway_verify_and_delete_when_self_delete_is_confirmed_in_db_mode()
    {
        var (dialog, gateway, _) = OpenDeleteDialog(serverMode: false);

        await dialog.ClickOk();

        Assert.Equal(1, gateway.RemoveCallCount);                         // DB 모드는 게이트웨이가 로컬 해시로 확인한다
    }

    private static MyPagePanelViewModel OpenMyPage(bool serverMode, CapturingEventAggregator events)
    {
        var log = new SilentLog();
        var login = new LoginViewModel(events, log, Target());
        return new MyPagePanelViewModel(events, log, login, new ModeProfileGateway(canSelfDelete: !serverMode), new FakeProfileImageService());
    }

    [Fact]
    public async Task should_disable_delete_account_with_a_reason_when_in_server_mode()
    {
        var events = new CapturingEventAggregator();
        var page = OpenMyPage(serverMode: true, events);

        Assert.False(page.CanClickDeleteAccount);
        Assert.Equal(DeleteAccountDialogViewModel.SelfDeleteUnavailableText, page.DeleteAccountBlockedReason);

        await page.ClickDeleteAccount();                                   // 키 · 다른 경로로 불려도 창을 열지 않는다
        Assert.Empty(events.OfType<OpenDeleteAccountDialogMessageModel>());
    }

    [Fact]
    public async Task should_open_the_delete_dialog_when_in_db_mode()
    {
        var events = new CapturingEventAggregator();
        var page = OpenMyPage(serverMode: false, events);

        Assert.True(page.CanClickDeleteAccount);
        Assert.Null(page.DeleteAccountBlockedReason);

        await page.ClickDeleteAccount();
        Assert.Single(events.OfType<OpenDeleteAccountDialogMessageModel>());
    }
    #endregion
}
