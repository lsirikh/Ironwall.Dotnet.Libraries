using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// B3 계정 셀프서비스 창(window-design-inventory-analysis.md #8 · #9 · #10 · #19 ~ #22)을 실제로 띄워 본다.
/// </summary>
/// <remarks>
/// <para>지키는 것: ① 커널 다이얼로그 틀(ConsoleDialogFrame)을 쓴다 ② 바꾸기 전의 x:Name(Caliburn 바인딩 지시자) · AutomationId 가
/// 하나도 빠지지 않고 <b>같은 형(型)</b>이다 ③ 그 이름들이 UIA 기본 보기에 AutomationId 로 나온다(호스트 UiTests 가 그걸로 찾는다)
/// ④ 버튼 · ESC 가 뷰모델까지 닿는다 — 틀의 자기 속성(FooterActions)에 앉힌 버튼은 Caliburn 이름 관례가 닿지 않아 Message.Attach 로 이었다.</para>
/// <para>기대 목록은 바꾸기 전(HEAD 7bf00228) XAML 에서 뽑았다 — CardContents 만 형이 바뀐다(md:Card/Border → ConsoleDialogFrame: 바인딩 대상 아님).</para>
/// </remarks>
public class SelfServiceViewTests
{
    /// <summary>바꾸기 전의 x:Name → 형. <c>CardContents</c> 는 틀 자신이 된다.</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, Type Type)[]> Names = new Dictionary<string, (string, Type)[]>
    {
        ["Login"] = new[] { ("DialogHost", typeof(DialogHost)), ("CardContents", typeof(ConsoleDialogFrame)), ("TextBoxID", typeof(TextBox)), ("TextBoxPW", typeof(PasswordBox)), ("EyePeek", typeof(Button)), ("ClickOk", typeof(Button)), ("ClickCancel", typeof(Button)), ("ClickRegister", typeof(Button)) },
        ["Logout"] = new[] { ("DialogHost", typeof(DialogHost)), ("CardContents", typeof(ConsoleDialogFrame)), ("ClickOk", typeof(Button)), ("ClickCancel", typeof(Button)) },
        ["MyPage"] = new[] { ("DialogHost", typeof(DialogHost)), ("CardContents", typeof(ConsoleDialogFrame)), ("ClickCancel", typeof(Button)), ("ClickClearPicture", typeof(Button)), ("ClickAddPicture", typeof(Button)), ("EditorPassword", typeof(PasswordBox)), ("ClickPasswordReset", typeof(Button)), ("EditorMyPageName", typeof(TextBox)), ("EditorMyPageEmployeeNumber", typeof(TextBox)), ("ClickDeleteAccount", typeof(Button)), ("ClickResetAccount", typeof(Button)), ("ClickApplyAccount", typeof(Button)) },
        ["Editor"] = new[] { ("CardContents", typeof(ConsoleDialogFrame)), ("Username", typeof(TextBox)), ("Password", typeof(PasswordBox)), ("ClickResetPassword", typeof(Button)), ("LevelComboBox", typeof(ComboBox)), ("EditorEmployeeNumber", typeof(TextBox)), ("EditorPhone", typeof(TextBox)), ("EditorEmail", typeof(TextBox)), ("ClickAddPicture", typeof(Button)), ("ClickDeletePicture", typeof(Button)), ("EditorPosition", typeof(TextBox)), ("EditorDepartment", typeof(TextBox)), ("UsedComboBox", typeof(ComboBox)), ("ClickOk", typeof(Button)), ("ClickCancel", typeof(Button)) },
        ["ResetPass"] = new[] { ("DialogHost", typeof(DialogHost)), ("CardContents", typeof(ConsoleDialogFrame)), ("InputPassword", typeof(PasswordBox)), ("NewPassword", typeof(PasswordBox)), ("NewPasswordConfirm", typeof(PasswordBox)), ("ClickOk", typeof(Button)), ("ClickCancel", typeof(Button)) },
        ["DeleteAccount"] = new[] { ("CardContents", typeof(ConsoleDialogFrame)), ("InputPassword", typeof(PasswordBox)), ("ClickOk", typeof(Button)), ("ClickCancel", typeof(Button)) },
        ["Register"] = new[] { ("DialogHost", typeof(DialogHost)), ("CardContents", typeof(ConsoleDialogFrame)), ("Username", typeof(TextBox)), ("RegisterPass", typeof(PasswordBox)), ("PasswordConfirm", typeof(PasswordBox)), ("ClickAddPicture", typeof(Button)), ("ClickCancel", typeof(Button)), ("ClickOk", typeof(Button)) },
    };

    /// <summary>바꾸기 전의 명시 AutomationId.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Ids = new Dictionary<string, string[]>
    {
        ["Login"] = new[] { "Accounts.Login.Panel", "Accounts.Login.IdTextBox", "Accounts.Login.PasswordBox", "Accounts.Login.PasswordPlainTextBox", "Accounts.Login.RevealPasswordButton", "Accounts.Login.SaveIdCheckBox", "Accounts.Login.SubmitButton", "Accounts.Login.CancelButton", "Accounts.Login.RegisterButton", "Accounts.Login.ResultText" },
        ["Logout"] = Array.Empty<string>(),
        ["MyPage"] = Array.Empty<string>(),
        ["Editor"] = Array.Empty<string>(),
        ["ResetPass"] = Array.Empty<string>(),
        ["DeleteAccount"] = Array.Empty<string>(),
        ["Register"] = new[] { "Accounts.Register.NameInput" },
    };

    /// <summary>창마다 틀의 식별자 가운데 토막(Dialog.{Key}.Root …) · 제목.</summary>
    public static readonly IReadOnlyDictionary<string, (string Key, string Title)> Frames = new Dictionary<string, (string, string)>
    {
        ["Login"] = ("Accounts.Login", "로그인"),
        ["Logout"] = ("Accounts.Logout", "로그아웃"),
        ["MyPage"] = ("Accounts.MyPage", "내 정보"),
        ["Editor"] = ("Accounts.Editor", "사용자 변경"),
        ["ResetPass"] = ("Accounts.ResetPassword", "비밀번호 변경"),
        ["DeleteAccount"] = ("Accounts.DeleteAccount", "계정 삭제"),
        ["Register"] = ("Accounts.Register", "사용자 추가"),
    };

    public static IEnumerable<object[]> Views() => Names.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Views))]
    public void should_render_inside_the_kernel_dialog_frame_when_the_view_is_loaded(string name) => AppHost.Run(() =>
    {
        // Arrange · Act
        var hosted = SelfService.Host(name);
        try
        {
            // Assert — 틀이 뷰의 카드 자리(CardContents)이고 템플릿이 붙었다
            var frame = Assert.IsType<ConsoleDialogFrame>(hosted.View.FindName("CardContents"));
            Assert.True(frame.IsLoaded, $"{name}: 틀이 로드되지 않았다");
            Assert.Equal(Frames[name].Title, frame.Title);
            Assert.Equal(Frames[name].Key, frame.DialogKey);
            Assert.Equal(DialogIds.Root(Frames[name].Key), AutomationProperties.GetAutomationId(frame));
            Assert.True(frame.ActualWidth > 0 && frame.ActualHeight > 0, $"{name}: 틀 크기 0");

            // 옛 지문이 남아 있지 않다 — md:Card · ModernDialogHeader 스타일
            var visuals = SelfService.Visuals(hosted.View).ToList();
            Assert.DoesNotContain(visuals, v => v is Card c && c.TemplatedParent is null);
            var header = Application.Current.TryFindResource("ModernDialogHeader");
            if (header is Style legacy)
                Assert.DoesNotContain(visuals, v => v is FrameworkElement fe && ReferenceEquals(fe.Style, legacy));
        }
        finally { hosted.Window.Close(); }
    });

    [Theory]
    [MemberData(nameof(Views))]
    public void should_keep_every_previous_xname_with_its_type_when_the_view_is_redesigned(string name) => AppHost.Run(() =>
    {
        var hosted = SelfService.Host(name);
        try
        {
            foreach (var (xname, type) in Names[name])
            {
                var element = hosted.View.FindName(xname);
                Assert.True(element is not null, $"{name}: x:Name '{xname}' 이 사라졌다");
                Assert.IsAssignableFrom(type, element);
                if (type != typeof(ConsoleDialogFrame) && type != typeof(DialogHost))
                    Assert.Equal(type, element!.GetType());   // Caliburn 관례는 이름과 형으로 묶는다(PasswordBox 는 PasswordBox 그대로)
            }
        }
        finally { hosted.Window.Close(); }
    });

    [Theory]
    [MemberData(nameof(Views))]
    public void should_keep_every_previous_automation_id_in_the_visual_tree_when_the_view_is_redesigned(string name) => AppHost.Run(() =>
    {
        var hosted = SelfService.Host(name);
        try
        {
            var present = SelfService.Visuals(hosted.Window)
                .Select(AutomationProperties.GetAutomationId)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);
            present.Add(AutomationProperties.GetAutomationId(hosted.View));

            foreach (var id in Ids[name])
                Assert.True(present.Contains(id), $"{name}: AutomationId '{id}' 가 사라졌다");
        }
        finally { hosted.Window.Close(); }
    });

    [Theory]
    [MemberData(nameof(Views))]
    public void should_expose_the_previous_names_as_uia_automation_ids_when_the_elements_are_visible(string name) => AppHost.Run(() =>
    {
        // 호스트 UiTests 는 ByIdIn(dialog, "ClickOk") 처럼 x:Name 폴백 id 로 찾는다 — UIA 기본 보기에 그 id 가 있어야 한다.
        var hosted = SelfService.Host(name);
        try
        {
            var uia = SelfService.ControlPeers(hosted.Window).Select(p => p.GetAutomationId()).ToHashSet(StringComparer.Ordinal);

            foreach (var (xname, _) in Names[name].Where(n => n.Type != typeof(ConsoleDialogFrame) && n.Type != typeof(DialogHost)))
            {
                var element = (UIElement)hosted.View.FindName(xname)!;
                if (!element.IsVisible) continue;   // 접힌 것(눈 버튼을 누를 때만 보이는 평문 칸 등)은 UIA 에도 없다 — 원래 그렇다
                var expected = AutomationProperties.GetAutomationId(element) is { Length: > 0 } explicitId ? explicitId : xname;
                Assert.True(uia.Contains(expected), $"{name}: '{expected}' 가 UIA 기본 보기에 없다");
            }
            foreach (var id in Ids[name])
            {
                var element = SelfService.Visuals(hosted.Window).OfType<UIElement>().FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == id)
                              ?? (AutomationProperties.GetAutomationId(hosted.View) == id ? hosted.View : null);
                if (element is { IsVisible: true })
                    Assert.True(uia.Contains(id), $"{name}: '{id}' 가 UIA 기본 보기에 없다");
            }

            // 틀 제목은 자동화가 글로 읽는다(ConsoleDialogText peer)
            Assert.Contains(SelfService.ControlPeers(hosted.Window), p => p.GetAutomationControlType() == AutomationControlType.Text && SelfService.Name(p) == Frames[name].Title);
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_expose_field_labels_as_uia_text_beside_their_edit_when_my_page_is_open() => AppHost.Run(() =>
    {
        // UI 시험 EditBesideLabel(mp, "직급") — 글(Text) 라벨을 찾고 같은 줄의 Edit 을 고른다. 라벨이 커널 템플릿 안 TextBlock 이면 사라진다.
        var hosted = SelfService.Host("MyPage");
        try
        {
            var texts = SelfService.ControlPeers(hosted.Window).Where(p => p.GetAutomationControlType() == AutomationControlType.Text).ToList();
            foreach (var label in new[] { "아이디", "비밀번호", "성명", "사번", "전화", "이메일", "직급", "부서" })
                Assert.Contains(texts, p => SelfService.Name(p) == label);

            var position = texts.First(p => SelfService.Name(p) == "직급");
            var edits = SelfService.ControlPeers(hosted.Window).Where(p => p.GetAutomationControlType() == AutomationControlType.Edit).ToList();
            var labelRect = position.GetBoundingRectangle();
            var labelCenter = labelRect.Top + labelRect.Height / 2;
            Assert.Contains(edits, e =>
            {
                var r = e.GetBoundingRectangle();
                return r.Width > 0 && Math.Abs(r.Top + r.Height / 2 - labelCenter) <= 14 && r.Left >= labelRect.Left;
            });

            // 로그인한 본인의 아이디가 글로 보인다(T-MY015)
            Assert.Contains(texts, p => SelfService.Name(p) == SelfService.Sample().Username);
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_expose_the_logout_question_as_uia_text_when_logout_is_open() => AppHost.Run(() =>
    {
        var hosted = SelfService.Host("Logout");
        try
        {
            Assert.Contains(SelfService.ControlPeers(hosted.Window),
                p => p.GetAutomationControlType() == AutomationControlType.Text && SelfService.Name(p).Contains("정말로 로그아웃", StringComparison.Ordinal));
        }
        finally { hosted.Window.Close(); }
    });

    // ── Caliburn 연결 · 키 ───────────────────────────────────────────

    private static void Click(object? button) => ((ButtonBase)button!).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void PressEscape(FrameworkElement target)
    {
        var source = PresentationSource.FromVisual(target)!;
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        AppHost.Pump();
    }

    [Theory]
    [InlineData("Login", nameof(ClosePanelMessageModel))]
    [InlineData("Logout", nameof(ClosePanelMessageModel))]
    [InlineData("MyPage", nameof(ClosePanelMessageModel))]
    [InlineData("Editor", nameof(CloseDialogMessageModel))]
    [InlineData("ResetPass", nameof(CloseDialogMessageModel))]
    [InlineData("DeleteAccount", nameof(CloseDialogMessageModel))]
    [InlineData("Register", nameof(CloseDialogMessageModel))]
    public void should_run_the_view_model_cancel_exactly_once_when_the_footer_cancel_button_is_clicked(string name, string expected) => AppHost.Run(() =>
    {
        // Arrange
        var hosted = SelfService.Host(name);
        try
        {
            // Act — 틀 버튼 줄에 앉힌 x:Name 버튼(FooterActions · Message.Attach)
            Click(hosted.View.FindName("ClickCancel"));
            AppHost.Pump();

            // Assert — 한 번만(관례 + Message.Attach 가 겹쳐 두 번 불리지 않는다)
            Assert.Equal(1, hosted.Messages.Seen.Count(s => s == expected));
        }
        finally { hosted.Window.Close(); }
    });

    [Theory]
    [InlineData("Login", nameof(ClosePanelMessageModel))]
    [InlineData("Logout", nameof(ClosePanelMessageModel))]
    [InlineData("MyPage", nameof(ClosePanelMessageModel))]
    [InlineData("Editor", nameof(CloseDialogMessageModel))]
    [InlineData("ResetPass", nameof(CloseDialogMessageModel))]
    [InlineData("DeleteAccount", nameof(CloseDialogMessageModel))]
    [InlineData("Register", nameof(CloseDialogMessageModel))]
    public void should_run_the_view_model_cancel_once_when_escape_is_pressed_in_the_frame(string name, string expected) => AppHost.Run(() =>
    {
        var hosted = SelfService.Host(name);
        try
        {
            PressEscape((FrameworkElement)hosted.View.FindName("CardContents")!);
            Assert.Equal(1, hosted.Messages.Seen.Count(s => s == expected));
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_not_close_and_should_hide_the_header_close_when_login_is_forced() => AppHost.Run(() =>
    {
        // Arrange — 기동 시 로그인 게이팅(IsForced) — 취소(닫기) 불가
        var hosted = SelfService.Host("Login");
        try
        {
            ((LoginPanelViewModel)hosted.ViewModel).IsForced = true;
            AppHost.Pump();
            var frame = (ConsoleDialogFrame)hosted.View.FindName("CardContents")!;

            // Act
            PressEscape(frame);

            // Assert
            Assert.False(((Button)hosted.View.FindName("ClickCancel")!).IsEnabled);
            Assert.False(frame.ShowClose);
            Assert.Equal(0, hosted.Messages.Count<ClosePanelMessageModel>());
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_make_only_the_login_button_the_default_when_login_is_shown() => AppHost.Run(() =>
    {
        // 옛 창은 [확인] · [취소] 가 둘 다 IsDefault=True 라 Enter 가 비결정적이었다(ui-automation.md) — 이제 [확인] 하나.
        var hosted = SelfService.Host("Login");
        try
        {
            Assert.True(((Button)hosted.View.FindName("ClickOk")!).IsDefault);
            Assert.False(((Button)hosted.View.FindName("ClickCancel")!).IsDefault);
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_reach_the_view_model_from_both_body_and_footer_buttons_when_my_page_is_bound() => AppHost.Run(() =>
    {
        // 몸통(ConsoleField 안 — Caliburn 이름 관례)과 버튼 줄(FooterActions — Message.Attach) 두 길이 모두 닿는다.
        var hosted = SelfService.Host("MyPage");
        try
        {
            Click(hosted.View.FindName("ClickPasswordReset"));
            Click(hosted.View.FindName("ClickApplyAccount"));
            AppHost.Pump();

            Assert.Equal(1, hosted.Messages.Count<OpenResetPasswordDialogMessageModel>());
            Assert.Equal(1, hosted.Messages.Count<OpenConfirmPopupMessageModel>());
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_disable_self_delete_with_the_reason_tooltip_when_the_server_cannot_confirm_the_password() => AppHost.Run(() =>
    {
        // 0e314a6e — 서버 모드는 [계정 삭제] 가 꺼지고 툴팁이 까닭을 말한다. 틀 버튼 줄 왼쪽(FooterExtra)으로 옮겨도 그대로.
        var hosted = SelfService.Host("MyPage", canSelfDelete: false);
        try
        {
            var delete = (Button)hosted.View.FindName("ClickDeleteAccount")!;
            Assert.False(delete.IsEnabled);
            Assert.Equal(DeleteAccountDialogViewModel.SelfDeleteUnavailableText, delete.ToolTip);
            Assert.True(ToolTipService.GetShowOnDisabled(delete));

            Click(delete);
            AppHost.Pump();
            Assert.Equal(0, hosted.Messages.Count<OpenDeleteAccountDialogMessageModel>());
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_open_the_delete_dialog_once_when_self_delete_is_allowed() => AppHost.Run(() =>
    {
        var hosted = SelfService.Host("MyPage", canSelfDelete: true);
        try
        {
            var delete = (Button)hosted.View.FindName("ClickDeleteAccount")!;
            Assert.True(delete.IsEnabled);
            Click(delete);
            AppHost.Pump();
            Assert.Equal(1, hosted.Messages.Count<OpenDeleteAccountDialogMessageModel>());
        }
        finally { hosted.Window.Close(); }
    });

    [Fact]
    public void should_keep_the_confirm_disabled_when_the_reset_password_fields_are_empty() => AppHost.Run(() =>
    {
        // 가드(CanClickOk)가 Message.Attach 로 옮긴 버튼에도 걸린다.
        var hosted = SelfService.Host("ResetPass");
        try
        {
            Assert.False(((Button)hosted.View.FindName("ClickOk")!).IsEnabled);
            var vm = (ResetPassDialogViewModel)hosted.ViewModel;
            vm.InputPassword = "Current1!";
            vm.NewPassword = "NewPass12";
            vm.NewPasswordConfirm = "NewPass12";
            AppHost.Pump();
            Assert.True(((Button)hosted.View.FindName("ClickOk")!).IsEnabled);
        }
        finally { hosted.Window.Close(); }
    });
}
