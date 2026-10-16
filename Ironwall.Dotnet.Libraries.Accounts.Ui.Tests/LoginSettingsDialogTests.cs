using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Models;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Help;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Modules;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Moq;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/// <summary>
/// 로그인 설정 창(로그인 화면 톱니 · SSO 연동 켜기 · 끄기 — 사용자 2026-10-16). 창을 띄우지 않는다 — 뷰모델과 XAML 글만 본다.
/// </summary>
public class LoginSettingsDialogTests
{
    /// <summary>설정 창구 가짜 — 쓴 값을 기록한다.</summary>
    private sealed class FakePort : ILoginSettingsPort
    {
        public bool Stored { get; set; } = true;
        public List<bool> Writes { get; } = new();
        public int Restarts { get; private set; }
        public bool CanRestart { get; set; }
        public Exception? WriteFailure { get; set; }

        public bool ReadIsSsoEnabled() => Stored;
        public void WriteIsSsoEnabled(bool isSsoEnabled)
        {
            if (WriteFailure is not null) throw WriteFailure;
            Writes.Add(isSsoEnabled);
            Stored = isSsoEnabled;
        }
        public void Restart() => Restarts++;
    }

    private static LoginPanelViewModel Panel(ILoginSettingsPort? port)
    {
        var events = new EventAggregator();
        var log = new Mock<ILogService>().Object;
        return new LoginPanelViewModel(events, log, new LoginViewModel(events, log, new AccountModel()), new AccountProvider(),
                                       Mock.Of<IAuthGateway>(), loginSettings: port);
    }

    // ── 톱니 보이기 ──────────────────────────────────────────────────

    [Fact]
    public void should_hide_the_settings_gear_and_open_nothing_when_no_port_is_registered()
    {
        var panel = Panel(null);

        panel.ClickLoginSettings();

        Assert.False(panel.IsLoginSettingsAvailable);
        Assert.Null(panel.LoginSettings);
    }

    [Fact]
    public void should_show_the_gear_and_open_the_dialog_with_the_stored_value_when_a_port_is_registered()
    {
        var panel = Panel(new FakePort { Stored = true });

        panel.ClickLoginSettings();

        Assert.True(panel.IsLoginSettingsAvailable);
        var dialog = Assert.IsType<LoginSettingsDialogViewModel>(panel.LoginSettings);
        Assert.True(dialog.IsSsoEnabled);
        Assert.Equal("SSO 연동 꺼짐", dialog.SsoStatus);   // 조정자가 없다 = 지금 실행 중인 구성은 SSO 를 쓰지 않는다
    }

    [Fact]
    public void should_open_even_when_login_is_forced()
    {
        // 시작 로그인 게이팅(취소 불가)에서도 연다 — SSO 를 꺼야 아이디 · 비밀번호로 들어갈 수 있다
        var panel = Panel(new FakePort());
        panel.IsForced = true;

        panel.ClickLoginSettings();

        Assert.NotNull(panel.LoginSettings);
    }

    [Fact]
    public void should_show_the_read_failure_on_the_login_screen_instead_of_opening()
    {
        var port = new Mock<ILoginSettingsPort>();
        port.Setup(p => p.ReadIsSsoEnabled()).Throws(new IOException("잠김"));
        var panel = Panel(port.Object);

        panel.ClickLoginSettings();

        Assert.Null(panel.LoginSettings);
        Assert.True(panel.IsLoginFailed);
        Assert.Equal("로그인 설정을 읽지 못했습니다.", panel.Result);
    }

    // ── 저장 · 취소 ──────────────────────────────────────────────────

    [Fact]
    public void should_write_false_and_announce_restart_when_sso_is_turned_off_and_saved()
    {
        // Arrange
        var port = new FakePort { Stored = true };
        var panel = Panel(port);
        panel.ClickLoginSettings();
        var dialog = panel.LoginSettings!;

        // Act
        dialog.IsSsoEnabled = false;
        dialog.Primary();

        // Assert — 창구에 false 하나 · 재시작 안내 · 재시작 길이 없으니 [닫기]만
        Assert.Equal(new[] { false }, port.Writes);
        Assert.True(dialog.IsSaved);
        Assert.False(dialog.IsEditable);
        Assert.Contains("다시 시작하면 적용", dialog.Message);
        Assert.Equal(DialogMessageSeverity.Info, dialog.MessageSeverity);
        Assert.Equal(string.Empty, dialog.PrimaryText);
        Assert.Equal("닫기", dialog.SecondaryText);
        Assert.Same(dialog, panel.LoginSettings);   // 안내를 읽을 때까지 창이 남는다

        dialog.Secondary();
        Assert.Null(panel.LoginSettings);
        Assert.Equal(0, port.Restarts);
    }

    [Fact]
    public void should_offer_restart_now_or_later_and_restart_once_when_the_app_can_restart()
    {
        var port = new FakePort { Stored = true, CanRestart = true };
        var panel = Panel(port);
        panel.ClickLoginSettings();
        var dialog = panel.LoginSettings!;

        dialog.IsSsoEnabled = false;
        dialog.Primary();
        Assert.Equal("지금 다시 시작", dialog.PrimaryText);
        Assert.Equal("나중에", dialog.SecondaryText);
        Assert.True(dialog.IsPrimaryEnabled);

        dialog.Primary();

        Assert.Equal(1, port.Restarts);
        Assert.Equal(new[] { false }, port.Writes);   // 다시 시작이 한 번 더 쓰지 않는다
    }

    [Fact]
    public void should_close_without_restarting_when_later_is_chosen()
    {
        var port = new FakePort { Stored = false, CanRestart = true };
        var panel = Panel(port);
        panel.ClickLoginSettings();
        var dialog = panel.LoginSettings!;

        dialog.IsSsoEnabled = true;
        dialog.Primary();
        dialog.Secondary();

        Assert.Equal(new[] { true }, port.Writes);
        Assert.Equal(0, port.Restarts);
        Assert.Null(panel.LoginSettings);
    }

    [Fact]
    public void should_write_nothing_when_the_dialog_is_cancelled_after_a_change()
    {
        var port = new FakePort { Stored = true };
        var panel = Panel(port);
        panel.ClickLoginSettings();
        var dialog = panel.LoginSettings!;

        dialog.IsSsoEnabled = false;
        dialog.Secondary();

        Assert.Empty(port.Writes);
        Assert.True(port.Stored);
        Assert.Null(panel.LoginSettings);
    }

    [Fact]
    public void should_keep_save_disabled_and_write_nothing_when_nothing_changed()
    {
        var port = new FakePort { Stored = true };
        var dialog = new LoginSettingsDialogViewModel(port, "SSO 에이전트 없음", () => { });

        Assert.False(dialog.IsPrimaryEnabled);
        dialog.Primary();

        Assert.Empty(port.Writes);
        Assert.False(dialog.IsSaved);

        dialog.IsSsoEnabled = false;
        Assert.True(dialog.IsPrimaryEnabled);
        dialog.IsSsoEnabled = true;
        Assert.False(dialog.IsPrimaryEnabled);
    }

    [Fact]
    public void should_stay_in_edit_and_say_why_when_the_write_fails()
    {
        var port = new FakePort { Stored = true, WriteFailure = new UnauthorizedAccessException("읽기 전용") };
        var closed = 0;
        var dialog = new LoginSettingsDialogViewModel(port, "SSO 에이전트 없음", () => closed++);

        dialog.IsSsoEnabled = false;
        dialog.Primary();

        Assert.False(dialog.IsSaved);
        Assert.True(dialog.IsEditable);
        Assert.Equal(DialogMessageSeverity.Critical, dialog.MessageSeverity);
        Assert.Contains("저장하지 못했습니다", dialog.Message);
        Assert.Equal("저장", dialog.PrimaryText);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void should_close_the_open_dialog_when_the_login_panel_is_deactivated()
    {
        var panel = Panel(new FakePort());
        ((IActivate)panel).ActivateAsync().GetAwaiter().GetResult();
        panel.ClickLoginSettings();
        Assert.NotNull(panel.LoginSettings);

        ((IDeactivate)panel).DeactivateAsync(close: true).GetAwaiter().GetResult();

        Assert.Null(panel.LoginSettings);
    }

    [Theory]
    [InlineData(false, false, "SSO 연동 꺼짐")]
    [InlineData(false, true, "SSO 연동 꺼짐")]
    [InlineData(true, true, "SSO 에이전트 연결됨")]
    [InlineData(true, false, "SSO 에이전트 없음")]
    public void should_describe_the_current_sso_state_in_one_line(bool ssoOn, bool agentPresent, string expected)
        => Assert.Equal(expected, LoginPanelViewModel.SsoStatusText(ssoOn, agentPresent));

    // ── DI ──────────────────────────────────────────────────────────

    private sealed class StubSetup : IAccountSetupModel, IMariaDbSetupModel
    {
        public bool IsSession { get; set; }
        public int SessionExpiration { get; set; } = 60;
        public string IpDbServer { get; set; } = "127.0.0.1";
        public int PortDbServer { get; set; } = 3306;
        public string DbDatabase { get; set; } = "test";
        public string UidDbServer { get; set; } = "test";
        public string PasswordDbServer { get; set; } = "test";
    }

    private sealed class StubApiSetup : Ironwall.Dotnet.Libraries.Api.Models.IApiSetupModel
    {
        public string Url { get; set; } = "https://127.0.0.1:8000/api";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int Timeout { get; set; } = 10;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_show_the_gear_only_when_the_host_registers_the_port(bool registered)
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance<IEventAggregator>(new EventAggregator());
        builder.RegisterInstance(new Mock<ILogService>().Object).As<ILogService>();
        builder.RegisterType<AccountModel>().AsImplementedInterfaces().SingleInstance();
        var setup = new StubSetup();
        builder.RegisterModule(new AccountUiModule(setup, setup, null, 10, useDbAuth: false, apiSetup: new StubApiSetup()));
        if (registered) builder.RegisterInstance<ILoginSettingsPort>(new FakePort());
        using var c = builder.Build();

        Assert.Equal(registered, c.Resolve<LoginPanelViewModel>().IsLoginSettingsAvailable);
    }

    // ── XAML 계약(파일만 읽는다) ────────────────────────────────────

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Cal = "http://caliburnmicro.com";

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static string ViewPath(string relative) => Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", relative);

    [Fact]
    public void should_put_a_collapsed_by_default_gear_in_the_login_header_and_an_overlay_for_the_dialog()
    {
        var doc = XDocument.Load(ViewPath(@"Panels\LoginPanelView.xaml"));

        // 톱니 — 머리 오른쪽(HeaderExtra) · 기본 숨김 · 창구가 있을 때만 보임 · Message.Attach · x:Name 없음
        var header = Assert.Single(doc.Descendants().Where(e => e.Name.LocalName == "ConsoleDialogFrame.HeaderExtra"));
        var gear = Assert.Single(header.Descendants().Where(e => e.Name.LocalName == "Button"));
        Assert.Equal("Accounts.Login.SettingsButton", (string?)gear.Attribute("AutomationProperties.AutomationId"));
        Assert.Equal("ClickLoginSettings", (string?)gear.Attribute(Cal + "Message.Attach"));
        Assert.Equal("로그인 설정", (string?)gear.Attribute("ToolTip"));
        Assert.Null(gear.Attribute(X + "Name"));
        var style = Assert.Single(gear.Descendants().Where(e => e.Name.LocalName == "Style"));
        Assert.Contains(style.Elements().Where(e => e.Name.LocalName == "Setter"),
            s => (string?)s.Attribute("Property") == "Visibility" && (string?)s.Attribute("Value") == "Collapsed");
        Assert.Contains(style.Descendants().Where(e => e.Name.LocalName == "DataTrigger"),
            t => (string?)t.Attribute("Binding") == "{Binding IsLoginSettingsAvailable}" && (string?)t.Attribute("Value") == "True");

        // 덮개 — LoginSettings 가 있을 때만 암시적 템플릿이 창을 그린다
        Assert.Contains(doc.Descendants().Where(e => e.Name.LocalName == "ContentControl"),
            e => (string?)e.Attribute("Content") == "{Binding LoginSettings}");
        Assert.Contains(doc.Descendants().Where(e => e.Name.LocalName == "DataTemplate"),
            e => (string?)e.Attribute("DataType") == "{x:Type vmd:LoginSettingsDialogViewModel}"
                 && e.Elements().Any(v => v.Name.LocalName == "LoginSettingsDialogView"));
    }

    [Fact]
    public void should_use_the_kernel_frame_with_a_registered_help_key_and_automation_ids_only()
    {
        var doc = XDocument.Load(ViewPath(@"Dialogs\LoginSettingsDialogView.xaml"));

        var frame = Assert.Single(doc.Descendants().Where(e => e.Name.LocalName == "ConsoleDialogFrame"));
        Assert.Equal("Accounts.LoginSettings", (string?)frame.Attribute("DialogKey"));
        Assert.Equal("로그인 설정", (string?)frame.Attribute("Title"));
        Assert.Equal("OnPrimaryInvoked", (string?)frame.Attribute("PrimaryInvoked"));
        Assert.Equal("OnSecondaryInvoked", (string?)frame.Attribute("SecondaryInvoked"));
        var helpKey = (string?)frame.Attribute("HelpKey");
        Assert.Contains(AccountsHelp.Entries, e => e.Key == helpKey);

        // 켜기 · 끄기는 커널 스위치 · 식별자는 AutomationId 만
        var toggle = Assert.Single(doc.Descendants().Where(e => e.Name.LocalName == "ToggleButton"));
        Assert.Equal("{StaticResource Console.ToggleSwitch}", (string?)toggle.Attribute("Style"));
        Assert.Equal("{Binding IsSsoEnabled, Mode=TwoWay}", (string?)toggle.Attribute("IsChecked"));
        Assert.Equal("Accounts.LoginSettings.SsoEnabledToggle", (string?)toggle.Attribute("AutomationProperties.AutomationId"));
        Assert.DoesNotContain(doc.Descendants(), e => e.Attribute(X + "Name") is not null);
    }

    [Fact]
    public void should_paint_only_with_tokens_present_in_both_light_and_dark_themes()
    {
        var text = File.ReadAllText(ViewPath(@"Dialogs\LoginSettingsDialogView.xaml"));
        var doc = XDocument.Parse(text);
        var themes = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes");
        var light = File.ReadAllText(Path.Combine(themes, "Tokens.Light.xaml"));
        var dark = File.ReadAllText(Path.Combine(themes, "Tokens.Dark.xaml"));

        // 하드코딩 색 없음
        Assert.DoesNotContain(doc.Descendants().SelectMany(e => e.Attributes()), a => Regex.IsMatch(a.Value, "^#[0-9A-Fa-f]{3,8}$"));

        // 붓(…Brush)은 라이트 · 다크 둘 다에 있다
        var brushes = Regex.Matches(text, @"DynamicResource\s+(\w+Brush)\}").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(brushes);
        foreach (var key in brushes)
        {
            Assert.Contains($"x:Key=\"{key}\"", light);
            Assert.Contains($"x:Key=\"{key}\"", dark);
        }
    }

    [Theory]
    [InlineData(@"Ironwall.Dotnet.Libraries.Accounts.Ui\Views\Dialogs\LoginSettingsDialogView.xaml")]
    [InlineData(@"Ironwall.Dotnet.Libraries.Accounts.Ui\Views\Dialogs\LoginSettingsDialogView.xaml.cs")]
    [InlineData(@"Ironwall.Dotnet.Libraries.Accounts.Ui\ViewModels\Dialogs\LoginSettingsDialogViewModel.cs")]
    [InlineData(@"Ironwall.Dotnet.Libraries.Accounts.Ui\Services\ILoginSettingsPort.cs")]
    public void should_save_new_korean_sources_with_a_utf8_bom(string relative)
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepoRoot(), relative));
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, $"{relative} 에 BOM 이 없다");
    }
}
