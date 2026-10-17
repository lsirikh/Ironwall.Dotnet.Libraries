using Ironwall.Dotnet.Libraries.Accounts.Ui.Views;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/// <summary>
/// B3 계정 셀프서비스 창 7종(로그인 · 로그아웃 · 내 정보 · 사용자 추가 · 사용자 변경 · 비밀번호 변경 · 계정 삭제)의 XAML 계약.
/// </summary>
/// <remarks>
/// <para>이 어셈블리는 Application 을 세우지 않는다(다른 시험이 "Application.Current == null" 에 기댄다) — 뷰를 실제로 띄워 보는 시험은
/// <c>tests/Accounts.Ui.ViewTests</c> 에 있다. 여기서는 파일만 읽어 바꾸기 전 목록(HEAD 7bf00228)과 대조한다.</para>
/// <para>지키는 것: x:Name(Caliburn 바인딩 지시자 · 호스트 UiTests 앵커)과 AutomationId 를 하나도 잃지 않는다 · 커널 틀을 쓴다 ·
/// 옛 지문(md:Card · ModernDialogHeader · Theme=Dark 고정 · 하드코딩 색 · 호스트 전용 스타일 키)이 없다.</para>
/// </remarks>
public class SelfServiceViewContractTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Cal = "http://caliburnmicro.com";

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static XDocument Load(string relative)
        => XDocument.Load(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", relative));

    /// <summary>파일 → (바꾸기 전 x:Name → 요소 이름). CardContents 만 ConsoleDialogFrame 이 된다(바인딩 대상 아님).</summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string Element)[]> Names = new Dictionary<string, (string, string)[]>
    {
        [@"Panels\LoginPanelView.xaml"] = new[] { ("DialogHost", "DialogHost"), ("CardContents", "ConsoleDialogFrame"), ("TextBoxID", "TextBox"), ("TextBoxPW", "PasswordBox"), ("EyePeek", "Button"), ("ClickOk", "Button"), ("ClickCancel", "Button"), ("ClickRegister", "Button") },
        [@"Panels\LogoutPanelView.xaml"] = new[] { ("DialogHost", "DialogHost"), ("CardContents", "ConsoleDialogFrame"), ("ClickOk", "Button"), ("ClickCancel", "Button") },
        [@"Panels\MyPagePanelView.xaml"] = new[] { ("DialogHost", "DialogHost"), ("CardContents", "ConsoleDialogFrame"), ("ClickCancel", "Button"), ("ClickClearPicture", "Button"), ("ClickAddPicture", "Button"), ("EditorPassword", "PasswordBox"), ("ClickPasswordReset", "Button"), ("EditorMyPageName", "TextBox"), ("EditorMyPageEmployeeNumber", "TextBox"), ("ClickDeleteAccount", "Button"), ("ClickResetAccount", "Button"), ("ClickApplyAccount", "Button") },
        [@"Dialogs\EditorDialogView.xaml"] = new[] { ("CardContents", "ConsoleDialogFrame"), ("Username", "TextBox"), ("Password", "PasswordBox"), ("ClickResetPassword", "Button"), ("LevelComboBox", "ComboBox"), ("EditorEmployeeNumber", "TextBox"), ("EditorPhone", "TextBox"), ("EditorEmail", "TextBox"), ("ClickAddPicture", "Button"), ("ClickDeletePicture", "Button"), ("EditorPosition", "TextBox"), ("EditorDepartment", "TextBox"), ("UsedComboBox", "ComboBox"), ("ClickOk", "Button"), ("ClickCancel", "Button") },
        [@"Dialogs\ResetPassDialogView.xaml"] = new[] { ("DialogHost", "DialogHost"), ("CardContents", "ConsoleDialogFrame"), ("InputPassword", "PasswordBox"), ("NewPassword", "PasswordBox"), ("NewPasswordConfirm", "PasswordBox"), ("ClickOk", "Button"), ("ClickCancel", "Button") },
        [@"Dialogs\DeleteAccountDialogView.xaml"] = new[] { ("CardContents", "ConsoleDialogFrame"), ("InputPassword", "PasswordBox"), ("ClickOk", "Button"), ("ClickCancel", "Button") },
        [@"Dialogs\RegisterDialogView.xaml"] = new[] { ("DialogHost", "DialogHost"), ("CardContents", "ConsoleDialogFrame"), ("Username", "TextBox"), ("RegisterPass", "PasswordBox"), ("PasswordConfirm", "PasswordBox"), ("ClickAddPicture", "Button"), ("ClickCancel", "Button"), ("ClickOk", "Button") },
    };

    public static readonly IReadOnlyDictionary<string, string[]> Ids = new Dictionary<string, string[]>
    {
        [@"Panels\LoginPanelView.xaml"] = new[] { "Accounts.Login.Panel", "Accounts.Login.IdTextBox", "Accounts.Login.PasswordBox", "Accounts.Login.PasswordPlainTextBox", "Accounts.Login.RevealPasswordButton", "Accounts.Login.SaveIdCheckBox", "Accounts.Login.SubmitButton", "Accounts.Login.CancelButton", "Accounts.Login.RegisterButton", "Accounts.Login.ResultText" },
        [@"Dialogs\RegisterDialogView.xaml"] = new[] { "Accounts.Register.NameInput" },
    };

    public static IEnumerable<object[]> Files() => Names.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Files))]
    public void should_keep_every_previous_xname_on_the_same_element_type_when_the_view_is_redesigned(string file)
    {
        // Arrange
        var names = Load(file).Descendants()
            .Where(e => e.Attribute(X + "Name") is not null)
            .GroupBy(e => (string)e.Attribute(X + "Name")!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Assert — 하나도 잃지 않고, 한 번만, 같은 형
        foreach (var (name, element) in Names[file])
        {
            Assert.True(names.ContainsKey(name), $"{file}: x:Name '{name}' 이 사라졌다");
            Assert.Single(names[name]);
            Assert.Equal(element, names[name][0].Name.LocalName);
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void should_keep_every_previous_automation_id_when_the_view_is_redesigned(string file)
    {
        var ids = Load(file).Descendants()
            .Select(e => (string?)e.Attribute("AutomationProperties.AutomationId"))
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var id in Ids.TryGetValue(file, out var expected) ? expected : Array.Empty<string>())
            Assert.True(ids.Contains(id), $"{file}: AutomationId '{id}' 가 사라졌다");
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void should_use_the_kernel_dialog_frame_without_legacy_fingerprints_when_the_view_is_redesigned(string file)
    {
        // Arrange
        var doc = Load(file);
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", file));

        // Assert — 커널 틀이 카드 자리(CardContents)다
        var frame = Assert.Single(doc.Descendants().Where(e => e.Name.LocalName == "ConsoleDialogFrame"));
        Assert.Equal("CardContents", (string?)frame.Attribute(X + "Name"));
        Assert.False(string.IsNullOrWhiteSpace((string?)frame.Attribute("DialogKey")), $"{file}: DialogKey 가 없다");

        // 옛 지문(주석은 빼고 — 주석이 옛 모양을 설명한다)
        text = Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName == "Card");
        Assert.DoesNotContain("ModernDialogHeader", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ThemeAssist.Theme", text, StringComparison.Ordinal);

        // 호스트 앱 전용 스타일 키에 기대지 않는다(미리보기 · 헤드리스에서 뷰가 뜨지 않던 까닭)
        foreach (var hostKey in new[] { "TextBoxInput", "PasswordBoxInput", "TextBlockTitle", "TextBlockContent", "ButtonIcon", "ButtonTextBlock", "SubDialogIcon", "SubDialogText", "ComboBoxContent", "ComboBoxItemContent", "SeparatorVerticalStyle", "ModernPanelCloseButton", "WidthIconSmall", "HeightIconSmall" })
            Assert.False(Regex.IsMatch(text, @"StaticResource\s+" + Regex.Escape(hostKey) + @"\}"), $"{file}: 호스트 전용 키 '{hostKey}' 를 찾는다");

        // 색은 토큰으로만 — d: 디자인 속성 밖의 #RRGGBB 금지
        var literals = doc.Descendants().SelectMany(e => e.Attributes())
            .Where(a => a.Name.NamespaceName != "http://schemas.microsoft.com/expression/blend/2008")
            .Where(a => Regex.IsMatch(a.Value, "^#[0-9A-Fa-f]{3,8}$"))
            .Select(a => $"{a.Parent!.Name.LocalName}.{a.Name.LocalName}={a.Value}")
            .ToList();
        Assert.True(literals.Count == 0, $"{file}: 하드코딩 색 {string.Join(", ", literals)}");
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void should_wire_frame_footer_buttons_with_message_attach_when_they_sit_in_frame_properties(string file)
    {
        // 틀의 자기 속성(FooterActions · FooterExtra) 안은 Caliburn 이름 관례가 뷰를 붙일 때 닿지 않는다(ConsoleShell.HeaderContent 와 같은 까닭)
        var doc = Load(file);
        var footerButtons = doc.Descendants()
            .Where(e => e.Name.LocalName is "ConsoleDialogFrame.FooterActions" or "ConsoleDialogFrame.FooterExtra")
            .SelectMany(e => e.Descendants())
            .Where(e => e.Attribute(X + "Name") is not null)
            .ToList();

        Assert.NotEmpty(footerButtons);
        foreach (var button in footerButtons)
            Assert.Equal((string?)button.Attribute(X + "Name"), (string?)button.Attribute(Cal + "Message.Attach"));

        // 틀의 취소(ESC · ✕)는 코드 숨김이 [취소] 로 넘긴다 — 틀 버튼은 비운다(이름이 없는 버튼이 둘 생기지 않게)
        var frame = doc.Descendants().Single(e => e.Name.LocalName == "ConsoleDialogFrame");
        Assert.Equal("OnSecondaryInvoked", (string?)frame.Attribute("SecondaryInvoked"));
        Assert.Equal(string.Empty, (string?)frame.Attribute("PrimaryText"));
        Assert.Equal(string.Empty, (string?)frame.Attribute("SecondaryText"));
    }

    [Fact]
    public void should_make_only_the_submit_button_default_when_login_is_redesigned()
    {
        // ui-automation.md — 옛 로그인은 [확인] · [취소] 가 둘 다 IsDefault=True 라 Enter 가 비결정적이었다.
        var defaults = Load(@"Panels\LoginPanelView.xaml").Descendants()
            .Where(e => (string?)e.Attribute("IsDefault") == "True")
            .Select(e => (string?)e.Attribute(X + "Name"))
            .ToList();

        Assert.Equal(new[] { "ClickOk" }, defaults);
    }

    [Fact]
    public void should_not_open_the_file_picker_on_enter_when_register_is_redesigned()
    {
        // 옛 사용자 추가 창은 [사진 추가] 가 IsDefault=True 라 Enter 가 Win32 파일 창을 열었다.
        var add = Load(@"Dialogs\RegisterDialogView.xaml").Descendants().Single(e => (string?)e.Attribute(X + "Name") == "ClickAddPicture");
        Assert.Null(add.Attribute("IsDefault"));
    }

    [Fact]
    public void should_use_tokens_only_when_the_shared_form_styles_are_read()
    {
        var path = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "AccountFormStyles.xaml");
        var text = File.ReadAllText(path);
        var doc = XDocument.Parse(text);

        // 색 리터럴 없음 · 바깥 키를 StaticResource 로 찾지 않음(어디서 병합해도 읽힌다)
        Assert.DoesNotContain(doc.Descendants().SelectMany(e => e.Attributes()), a => Regex.IsMatch(a.Value, "^#[0-9A-Fa-f]{3,8}$"));
        var ownKeys = doc.Root!.Elements().Select(e => (string?)e.Attribute(X + "Key")).Where(k => k is not null).Select(k => k!).ToHashSet(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(text, @"StaticResource\s+([A-Za-z.]+)\}"))
            Assert.Contains(m.Groups[1].Value, ownKeys);

        // UTF-8 BOM (encoding-i18n.md)
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "AccountFormStyles.xaml 에 BOM 이 없다");
    }

    // ── DialogCancelRoute ────────────────────────────────────────────

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() => { try { result = body(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }

    [Fact]
    public void should_click_the_cancel_button_once_when_the_frame_cancel_is_routed()
    {
        var (routed, clicks) = OnSta(() =>
        {
            var count = 0;
            var cancel = new Button();
            cancel.Click += (_, _) => count++;
            return (DialogCancelRoute.Invoke(cancel), count);
        });

        Assert.True(routed);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void should_not_click_the_cancel_button_when_it_is_disabled()
    {
        // 강제 로그인(CanClickCancel=false) — ESC 가 로그인 창을 닫으면 안 된다
        var (routed, clicks) = OnSta(() =>
        {
            var count = 0;
            var cancel = new Button { IsEnabled = false };
            cancel.Click += (_, _) => count++;
            return (DialogCancelRoute.Invoke(cancel), count);
        });

        Assert.False(routed);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void should_not_click_the_cancel_button_when_it_is_collapsed_or_missing()
    {
        var (collapsed, missing) = OnSta(() =>
        {
            var hidden = new Button { Visibility = Visibility.Collapsed };
            return (DialogCancelRoute.Invoke(hidden), DialogCancelRoute.Invoke(null));
        });

        Assert.False(collapsed);
        Assert.False(missing);
    }
}
