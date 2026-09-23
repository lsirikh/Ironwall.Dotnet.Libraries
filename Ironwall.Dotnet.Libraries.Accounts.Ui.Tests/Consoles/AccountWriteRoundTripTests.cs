using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using System.IO;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 계정 창의 쓰기 경로 — 라이브 왕복(tools/live-api-roundtrip, accounts-vm A2 · A4 · A5 · A6)에서 드러난 결함의 헤드리스 짝.
/// 서버에 무엇이 실리는지는 Accounts.Api / Messages 시험이, 여기서는 창이 <b>무엇을 넘기고 무엇을 보이는지</b>를 본다.
/// </summary>
public class AccountWriteRoundTripTests
{
    private static IEnumerable<AccountModel> Users() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영", department: "경비과", position: "주임"),
        ConsoleFixtures.User(2, "op2", "이감시", department: "상황실", position: "반장"),
    };

    private static async Task<(TestAccountConsole Console, FakeAccountApi Api, FakeDirectoryGateway Gateway)> OpenAsync()
    {
        var (console, api, gateway, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        return (console, api, gateway);
    }

    private static AccountViewModel SelectOne(TestAccountConsole console, int index)
    {
        var row = console.AccountManagerPanelViewModel.ViewModelProvider[index];
        console.OnUsersSelected(new List<object> { row });
        return row;
    }

    // ── A2: [적용] 은 손댄 칸의 서버 필드 이름만 넘긴다 ───────────────────

    [Fact]
    public async Task should_pass_only_the_touched_api_fields_when_applying()
    {
        var (console, _, gateway) = await OpenAsync();
        SelectOne(console, 0);

        console.Form.Fields.Single(f => f.Key == "department").Text = "경비1과";
        console.Form.Fields.Single(f => f.Key == "used").Text = "미사용";
        await console.ApplyAsync();

        var fields = Assert.Single(gateway.UpdatedFields);
        Assert.Equal(new[] { "department", "is_active" }, fields.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task should_not_pass_role_when_only_a_personal_field_is_touched()
    {
        var (console, _, gateway) = await OpenAsync();
        SelectOne(console, 0);

        console.Form.Fields.Single(f => f.Key == "department").Text = "";   // 비우기
        await console.ApplyAsync();

        var fields = Assert.Single(gateway.UpdatedFields);
        Assert.Equal(new[] { "department" }, fields);
        Assert.DoesNotContain("role", fields);
    }

    [Fact]
    public async Task should_write_not_used_to_the_row_when_the_status_is_applied()
    {
        var (console, _, _) = await OpenAsync();
        var row = SelectOne(console, 0);

        console.Form.Fields.Single(f => f.Key == "used").Text = "미사용";
        await console.ApplyAsync();

        Assert.Equal(EnumUsedType.NOT_USED, row.Used);
    }

    // ── A5: 최근 로그인은 서버 last_login_at 이 정본 ──────────────────────

    [Fact]
    public async Task should_show_the_server_last_login_when_the_session_page_has_no_row_for_the_user()
    {
        var (console, api, _) = await OpenAsync();
        api.Users.Single(u => u.Id == 1).LastLoginAt = "2026-09-24T01:02:03.456+09:00";
        await console.LoadGroupsAsync(CancellationToken.None);
        SelectOne(console, 0);

        var expected = DateTimeOffset.Parse("2026-09-24T01:02:03.456+09:00").ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        Assert.Equal(expected, console.LastLoginText);
    }

    [Fact]
    public async Task should_say_no_record_when_the_server_has_no_last_login()
    {
        var (console, _, _) = await OpenAsync();
        SelectOne(console, 1);

        Assert.Equal("기록 없음", console.LastLoginText);
    }

    // ── A4: 등록 다이얼로그가 고른 사진 ────────────────────────────────

    private static (RegisterDialogViewModel Dialog, FakeDirectoryGateway Gateway, string Picture, string Dir, CapturingEventAggregator Events) BuildRegister()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway();
        var provider = new AccountProvider();
        provider.Add(ConsoleFixtures.User(1, "admin", "관리자", EnumUserRole.ADMIN));   // 첫 등록자(=ADMIN) 분기를 피한다

        var dir = Path.Combine(Path.GetTempPath(), "acc_ui_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var picture = Path.Combine(dir, "pick.png");
        File.WriteAllBytes(picture, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        var dialog = new RegisterDialogViewModel(events, log, new RegisterViewModel(events, log, new AccountModel()),
            provider, gateway, new ProfileImageService(log, Path.Combine(dir, "Profile")));
        dialog.Name = "신규";
        dialog.RegisterPass = "abcdefgh1";
        dialog.PasswordConfirm = "abcdefgh1";
        return (dialog, gateway, picture, dir, events);
    }

    [Fact]
    public async Task should_upload_the_chosen_photo_to_the_created_account_when_the_server_does_not_keep_file_names()
    {
        var (dialog, gateway, picture, dir, events) = BuildRegister();
        try
        {
            gateway.CreateResult = acc => new AccountModel { Id = 9, Username = acc.Username, Image = "https://h/api/users/photo/default.png" };
            await dialog.SetPictureAsync(picture);

            await dialog.ClickOk();

            var upload = Assert.Single(gateway.PhotoUploads);
            Assert.Equal(9, upload.UserId);
            Assert.True(File.Exists(upload.Path));
            Assert.Equal("https://h/api/users/photo/9_new.png", gateway.Accounts.Single().Image);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task should_not_upload_when_the_created_account_already_keeps_the_local_file_name()
    {
        var (dialog, gateway, picture, dir, events) = BuildRegister();
        try
        {
            await dialog.SetPictureAsync(picture);   // DB 모드: 게이트웨이가 입력 모델을 그대로 돌려준다(파일 이름 보존)

            await dialog.ClickOk();

            Assert.Empty(gateway.PhotoUploads);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task should_tell_the_user_when_the_account_is_created_but_the_photo_upload_fails()
    {
        var (dialog, gateway, picture, dir, events) = BuildRegister();
        try
        {
            gateway.CreateResult = acc => new AccountModel { Id = 9, Username = acc.Username, Image = "https://h/api/users/photo/default.png" };
            gateway.PhotoUploadResult = null;
            await dialog.SetPictureAsync(picture);

            await dialog.ClickOk();

            Assert.Single(gateway.PhotoUploads);
            Assert.Single(gateway.Accounts);   // 계정은 만들어졌다
            var done = events.OfType<OpenInfoPopupMessageModel>().Last().Explain;
            Assert.Contains("계정 등록을 성공", done);
            Assert.EndsWith(RegisterDialogViewModel.PhotoNotAppliedNote, done);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── A6: 세션 설정 403 안내는 서버 판정(setup_system)을 말한다 ────────────

    [Fact]
    public void should_name_the_setup_system_permission_when_session_setup_is_forbidden()
    {
        Assert.Contains("setup_system", AccountSetupPanelViewModel.ForbiddenSaveText);
        Assert.Contains("setup_system", AccountSetupPanelViewModel.ForbiddenLoadText);
        Assert.DoesNotContain("ADMIN 전용", AccountSetupPanelViewModel.ForbiddenSaveText);
    }
}
