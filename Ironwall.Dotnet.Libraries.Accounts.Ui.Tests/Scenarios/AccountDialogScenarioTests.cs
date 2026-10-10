using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.3(ACC) 중 가입 · 비밀번호 바꾸기 · 내 정보 항목을 창 없이 확인한다.
   Notes     : 가짜 게이트웨이만 쓴다 — 실제 계정을 만들거나 비밀번호를 바꾸지 않는다.
               Win32 파일창(사진 추가) · 실제 Esc 키 · 화면 포커스는 헤디드 몫이다.
****************************************************************************/
public class AccountDialogScenarioTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string MyPageViewPath()
        => Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", "MyPagePanelView.xaml");

    #region - 가짜 -
    /// <summary>프로필을 기억하는 가짜 프로필 게이트웨이 — 결과를 시험이 정한다.</summary>
    private sealed class StoredProfileGateway : IProfileGateway
    {
        public AccountModel Stored { get; } = new()
        {
            Id = 5, Username = "op5", Name = "김운영", Phone = "010-0000-0005", EMail = "op5@example.com",
            Position = "대원", Department = "경비과", EmployeeNumber = "EMP-5",
        };
        public bool ChangeSucceeds { get; set; } = true;
        public bool DeletePhotoSucceeds { get; set; } = true;
        public int UpdateCallCount { get; private set; }
        public int ChangePasswordCallCount { get; private set; }

        public Task<IAccountModel?> GetProfileAsync(int accountId, CancellationToken ct = default)
        {
            var copy = new AccountModel(); copy.Update(Stored);
            return Task.FromResult<IAccountModel?>(copy);
        }

        public Task<IAccountModel?> UpdateProfileAsync(IAccountModel acc, CancellationToken ct = default)
        {
            UpdateCallCount++;
            Stored.Update(acc);
            var echo = new AccountModel(); echo.Update(Stored);
            return Task.FromResult<IAccountModel?>(echo);
        }

        public Task<IAccountModel?> ChangePasswordAsync(IAccountModel acc, string currentPassword, string newPassword, CancellationToken ct = default)
        {
            ChangePasswordCallCount++;
            return Task.FromResult(ChangeSucceeds ? acc : null);
        }

        public Task<string?> UploadPhotoAsync(string filePath, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<bool> DeletePhotoAsync(CancellationToken ct = default) => Task.FromResult(DeletePhotoSucceeds);
    }

    private static (MyPagePanelViewModel Page, StoredProfileGateway Gateway, CapturingEventAggregator Events) OpenMyPage()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new StoredProfileGateway();
        var login = new LoginViewModel(events, log, new AccountModel { Id = 5, Username = "op5", Image = "photo.png" });
        return (new MyPagePanelViewModel(events, log, login, gateway, new FakeProfileImageService()), gateway, events);
    }

    private static (RegisterDialogViewModel Dialog, FakeDirectoryGateway Gateway, CapturingEventAggregator Events) OpenRegister()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway { CanInferFirstAccountAdmin = false };
        gateway.Accounts.Add(ConsoleFixtures.User(1, "taken1", "기존"));
        var dialog = new RegisterDialogViewModel(events, log, new RegisterViewModel(events, log, new AccountModel()),
            new AccountProvider(), gateway, new FakeProfileImageService());
        return (dialog, gateway, events);
    }

    private static (ResetPassDialogViewModel Dialog, StoredProfileGateway Gateway, CapturingEventAggregator Events) OpenResetPassword()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new StoredProfileGateway();
        var login = new LoginViewModel(events, log, new AccountModel { Id = 5, Username = "op5" });
        return (new ResetPassDialogViewModel(events, log, login, gateway), gateway, events);
    }
    #endregion

    // ── ACC-033: 가입 대화상자 — ID 확인 · 중복 · 비밀번호 규칙 · 취소 ─────────────
    [Trait("Scenario", "ACC-033")]
    [Fact]
    public async Task should_refuse_a_taken_id_and_accept_a_free_one_when_the_register_id_is_checked()
    {
        var (dialog, _, events) = OpenRegister();
        await ScreenExtensions.TryActivateAsync(dialog);

        dialog.Username = "taken1";
        Assert.Equal("taken1 아이디가 존재합니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Equal(string.Empty, dialog.Username);                  // 쓰인 아이디는 비운다

        dialog.Username = "Fresh01";
        Assert.Equal("사용 가능한 아이디입니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Equal("fresh01", dialog.Username);                      // 소문자로 맞춘다
    }

    [Trait("Scenario", "ACC-033")]
    [Theory]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(20, true)]
    public async Task should_follow_the_password_length_rule_when_the_register_ok_button_is_evaluated(int length, bool expected)
    {
        var (dialog, _, _) = OpenRegister();
        await ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = "fresh01";
        dialog.Name = "신규";
        dialog.RegisterPass = new string('a', length);
        dialog.PasswordConfirm = new string('a', length);

        Assert.Equal(expected, dialog.CanClickOk);
    }

    /// <summary>
    /// 가입 창 안내는 「비밀번호 8~20자」(RegisterDialogView.xaml Message · StringLengthValidationRule Max=20)인데
    /// 뷰모델이 하한만 보면 21자에도 [확인] 이 켜진다 — ACC-041 과 같은 결함.
    /// </summary>
    [Trait("Scenario", "ACC-033")]
    [Fact]
    public async Task should_disable_register_ok_when_the_password_is_21_characters()
    {
        var (dialog, _, _) = OpenRegister();
        await ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = "fresh01";
        dialog.Name = "신규";
        dialog.RegisterPass = new string('a', 21);
        dialog.PasswordConfirm = new string('a', 21);

        Assert.False(dialog.CanClickOk, "21자 비밀번호에 가입 [확인] 이 켜졌다 — 안내는 8~20자");
    }

    [Trait("Scenario", "ACC-033")]
    [Fact]
    public async Task should_close_without_creating_when_register_is_cancelled()
    {
        var (dialog, gateway, events) = OpenRegister();
        await ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = "fresh01";

        await dialog.ClickCancel();

        Assert.Single(events.OfType<CloseDialogMessageModel>());
        Assert.Single(gateway.Accounts);                               // 기존 1명 그대로
    }

    // ── ACC-037: 내 정보 열기 — 칸 채움 · 비밀번호 칸 꺼짐 ─────────────────────────
    [Trait("Scenario", "ACC-037")]
    [Fact]
    public async Task should_fill_my_fields_and_keep_the_password_box_disabled_when_my_page_opens()
    {
        var (page, _, _) = OpenMyPage();

        await ScreenExtensions.TryActivateAsync(page);

        Assert.Equal("김운영", page.ViewModel.Name);
        Assert.Equal("경비과", page.ViewModel.Department);
        Assert.Equal("EMP-5", page.ViewModel.EmployeeNumber);

        var doc = XDocument.Load(MyPageViewPath());
        var password = doc.Descendants().Single(e => e.Name.LocalName == "PasswordBox"
                                                  && (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "EditorPassword");
        Assert.Equal("False", (string?)password.Attribute("IsEnabled"));
    }

    // ── ACC-038: 내 정보 이름 없는 칸 — 전화 · 메일 · 직급 · 부서 ──────────────────
    [Trait("Scenario", "ACC-038")]
    [Theory]
    [InlineData("ViewModel.Phone")]
    [InlineData("ViewModel.EMail")]
    [InlineData("ViewModel.Position")]
    [InlineData("ViewModel.Department")]
    public void should_give_each_editable_my_page_field_an_automation_id(string bindingPath)
    {
        var doc = XDocument.Load(MyPageViewPath());
        var box = doc.Descendants().Single(e => e.Name.LocalName == "TextBox"
                                             && ((string?)e.Attribute("Text"))?.Contains($"Binding {bindingPath},") == true);

        var id = (string?)box.Attribute("AutomationProperties.AutomationId");
        Assert.False(string.IsNullOrWhiteSpace(id), $"{bindingPath} 칸에 자동화 id 가 없다 — 헤디드가 트리 순서로만 찾는다");
        Assert.Null(box.Attribute("IsReadOnly"));                      // 편집 가능
        Assert.Null(box.Attribute("IsEnabled"));
    }

    [Trait("Scenario", "ACC-038")]
    [Fact]
    public async Task should_send_the_edited_contact_fields_when_my_page_is_applied()
    {
        var (page, gateway, _) = OpenMyPage();
        await ScreenExtensions.TryActivateAsync(page);

        page.ViewModel.Phone = "010-1111-2222";
        page.ViewModel.EMail = "new@example.com";
        page.ViewModel.Position = "반장";
        page.ViewModel.Department = "상황실";
        await page.HandleAsync(new CallEditProcessMessageModel(), CancellationToken.None);

        Assert.Equal(1, gateway.UpdateCallCount);
        Assert.Equal(("010-1111-2222", "new@example.com", "반장", "상황실"),
                     (gateway.Stored.Phone, gateway.Stored.EMail, gateway.Stored.Position, gateway.Stored.Department));
    }

    // ── ACC-039: 내 정보 적용 / 초기화 ─────────────────────────────────────────
    [Trait("Scenario", "ACC-039")]
    [Fact]
    public async Task should_confirm_then_report_success_when_my_page_apply_is_pressed()
    {
        var (page, _, events) = OpenMyPage();
        await ScreenExtensions.TryActivateAsync(page);
        page.ViewModel.Position = "반장";

        await page.ClickApplyAccount();
        var confirm = events.OfType<OpenConfirmPopupMessageModel>().Last();
        Assert.Equal("사용자 정보를 변경하시겠습니까?", confirm.Explain);
        Assert.IsType<CallEditProcessMessageModel>(confirm.MessageModel);

        await page.HandleAsync(new CallEditProcessMessageModel(), CancellationToken.None);

        Assert.Equal("사용자 정보 변경이 정상적으로 완료되었습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Equal("반장", page.ViewModel.Position);
    }

    [Trait("Scenario", "ACC-039")]
    [Fact]
    public async Task should_bring_back_the_saved_values_when_my_page_reset_is_confirmed()
    {
        var (page, _, events) = OpenMyPage();
        await ScreenExtensions.TryActivateAsync(page);
        page.ViewModel.Position = "적다 만 직급";

        await page.ClickResetAccount();
        Assert.IsType<CallResetProcessMessageModel>(events.OfType<OpenConfirmPopupMessageModel>().Last().MessageModel);

        await page.HandleAsync(new CallResetProcessMessageModel(), CancellationToken.None);

        Assert.Equal("대원", page.ViewModel.Position);
        Assert.Equal("변경 전 정보를 정상적으로 불러왔습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    // ── ACC-040: 비밀번호 바꾸기 — 틀린 현재 · 성공 · 취소 ─────────────────────────
    [Trait("Scenario", "ACC-040")]
    [Fact]
    public async Task should_say_the_current_password_is_wrong_and_keep_the_dialog_open_when_the_gateway_refuses()
    {
        var (dialog, gateway, events) = OpenResetPassword();
        gateway.ChangeSucceeds = false;
        dialog.InputPassword = "wrong-pass";
        dialog.NewPassword = "newpass123";
        dialog.NewPasswordConfirm = "newpass123";

        await dialog.ClickOk();

        Assert.Equal("현재 비밀번호가 틀렸습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Empty(events.OfType<CloseDialogMessageModel>());
        Assert.Null(dialog.InputPassword);                             // 틀린 현재 비밀번호는 지운다
    }

    [Trait("Scenario", "ACC-040")]
    [Fact]
    public async Task should_report_success_and_close_when_the_password_is_changed()
    {
        var (dialog, gateway, events) = OpenResetPassword();
        dialog.InputPassword = "current-pass";
        dialog.NewPassword = "newpass123";
        dialog.NewPasswordConfirm = "newpass123";

        await dialog.ClickOk();

        Assert.Equal(1, gateway.ChangePasswordCallCount);
        Assert.Equal("비밀번호가 변경되었습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Single(events.OfType<CloseDialogMessageModel>());
    }

    [Trait("Scenario", "ACC-040")]
    [Fact]
    public async Task should_close_without_changing_when_the_password_dialog_is_cancelled()
    {
        var (dialog, gateway, events) = OpenResetPassword();
        dialog.InputPassword = "current-pass";

        await dialog.ClickCancel();

        Assert.Equal(0, gateway.ChangePasswordCallCount);
        Assert.Single(events.OfType<CloseDialogMessageModel>());
    }

    // ── ACC-042: 사진 지우기 ───────────────────────────────────────────────────
    [Trait("Scenario", "ACC-042")]
    [Fact]
    public async Task should_confirm_then_clear_the_photo_when_my_photo_is_deleted()
    {
        var (page, _, events) = OpenMyPage();

        await page.ClickClearPicture();
        Assert.IsType<CallDeletePhotoProcessMessageModel>(events.OfType<OpenConfirmPopupMessageModel>().Last().MessageModel);
        Assert.Equal("photo.png", page.ViewModel.Image);              // 묻기만 했다

        await page.HandleAsync(new CallDeletePhotoProcessMessageModel(), CancellationToken.None);

        Assert.Null(page.ViewModel.Image);
        Assert.Equal("프로필 사진을 삭제했습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
    }

    // ── ACC-043: 내 정보 닫기 ─────────────────────────────────────────────────
    [Trait("Scenario", "ACC-043")]
    [Fact]
    public async Task should_ask_the_host_to_close_the_panel_when_my_page_close_is_pressed()
    {
        var (page, _, events) = OpenMyPage();

        await page.ClickCancel();

        Assert.Single(events.OfType<ClosePanelMessageModel>());
    }
}
