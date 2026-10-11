using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Help;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.3(ACC) · §3.5(AUX) 중 W7 몫의 계정 쪽 —
               ACC-034(가입 사진) · AUX-023(말풍선 규칙: Esc · 바깥 · 하나씩 · F1) · AUX-025(한 번도 안 연 키: 글 · Esc).
   Notes     : 창을 띄우지 않는다(Window.Show · ShowDialog · 화면 밖 창 금지). 이 어셈블리는 Application.Current == null 을
               전제로 하므로(ConsoleHarness) Application 을 만들지 않는다. 입력 원천이 필요하면 HWND 없는 가짜 PresentationSource 를 쓴다.
               말풍선 창(Popup)은 HWND 를 만들므로 열지 않는다 — "?" 에는 팝업 없는 템플릿(말풍선 몸 PART_Callout 만)을 물린다.
               Win32 파일 고르기 창(OpenFileDialog)은 띄우지 않는다 — 고른 뒤의 같은 경로(SetPictureAsync)를 직접 탄다.
****************************************************************************/
public class ShellAccountsLeftoverScenarioTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    #region - ACC-034: 가입 사진 -
    private static (RegisterDialogViewModel Dialog, FakeDirectoryGateway Gateway, string Dir, CapturingEventAggregator Events) BuildRegister()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway();
        var provider = new AccountProvider();
        provider.Add(ConsoleFixtures.User(1, "admin", "관리자", EnumUserRole.ADMIN));   // 첫 등록자(=ADMIN) 분기를 피한다

        var dir = Path.Combine(Path.GetTempPath(), "acc_shell_w7_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var dialog = new RegisterDialogViewModel(events, log, new RegisterViewModel(events, log, new AccountModel()),
            provider, gateway, new ProfileImageService(log, Path.Combine(dir, "Profile")));
        dialog.Name = "신규";
        dialog.RegisterPass = "abcdefgh1";
        dialog.PasswordConfirm = "abcdefgh1";
        return (dialog, gateway, dir, events);
    }

    private static string WriteFile(string dir, string name, byte[] bytes)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

    // [사진 추가] 의 Win32 창은 띄우지 않는다. 창이 돌려준 파일 이름이 가는 곳(SetPictureAsync)이 같은 경로임은 아래 소스 계약이 지킨다.
    [Trait("Scenario", "ACC-034")]
    [Fact]
    public void should_hand_the_file_chosen_in_the_add_picture_window_straight_to_the_same_picture_path_and_do_nothing_when_cancelled()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "ViewModels", "Dialogs", "RegisterDialogViewModel.cs"));
        var body = Regex.Match(source, @"public async Task ClickAddPicture\(\)\s*\{(?<b>.*?)\n    \}", RegexOptions.Singleline);

        Assert.True(body.Success, "RegisterDialogViewModel.ClickAddPicture 를 찾지 못했다");
        var b = body.Groups["b"].Value;
        Assert.Matches(@"if \(dlg\.ShowDialog\(\) != true\) return;", b);           // 취소 = 아무것도 하지 않는다
        Assert.Matches(@"await SetPictureAsync\(dlg\.FileName\);", b);               // 고른 파일 = 같은 경로
        Assert.Contains("*.png", b);                                                 // 사진 형식 거름
        Assert.Equal(1, Regex.Matches(b, "SetPictureAsync").Count);
    }

    [Trait("Scenario", "ACC-034")]
    [Fact]
    public async Task should_show_the_chosen_picture_as_a_copy_in_the_profile_folder_and_upload_that_copy_once_the_account_is_created()
    {
        var (dialog, gateway, dir, events) = BuildRegister();
        try
        {
            var picked = WriteFile(dir, "운영자 사진.png", PngBytes);
            gateway.CreateResult = acc => new AccountModel { Id = 9, Username = acc.Username, Image = "https://h/api/users/photo/default.png" };

            await dialog.SetPictureAsync(picked);                                   // 파일 창이 돌려준 이름

            var profile = Path.Combine(dir, "Profile");
            var copy = Assert.Single(Directory.GetFiles(profile));
            Assert.Equal(Path.GetFileName(copy), dialog.ViewModel.Image);          // 미리보기 = Profile 폴더 사본의 이름
            Assert.Equal(".png", Path.GetExtension(copy));
            Assert.Equal(PngBytes, File.ReadAllBytes(copy));                       // 그대로 복사됐다
            Assert.Empty(events.OfType<OpenInfoPopupMessageModel>());              // 거절 안내 없음
            Assert.True(File.Exists(picked));                                      // 원본은 건드리지 않는다

            dialog.Username = "newop1";
            await dialog.ClickOk();

            var upload = Assert.Single(gateway.PhotoUploads);                      // 계정이 생긴 뒤 그 사본을 올린다
            Assert.Equal((9, copy), (upload.UserId, upload.Path));
            Assert.Equal("https://h/api/users/photo/9_new.png", gateway.Accounts.Single().Image);
            Assert.DoesNotContain(RegisterDialogViewModel.PhotoNotAppliedNote, events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Trait("Scenario", "ACC-034")]
    [Theory]
    [InlineData("메모.txt", false)]          // 사진 형식이 아니다
    [InlineData("큰사진.png", true)]          // 5MB 넘는다
    public async Task should_keep_the_previous_picture_and_say_why_when_the_chosen_file_cannot_be_a_photo(string name, bool tooLarge)
    {
        var (dialog, gateway, dir, events) = BuildRegister();
        try
        {
            await dialog.SetPictureAsync(WriteFile(dir, "first.png", PngBytes));
            var before = dialog.ViewModel.Image;
            var copiesBefore = Directory.GetFiles(Path.Combine(dir, "Profile")).Length;

            var bad = tooLarge ? WriteFile(dir, name, new byte[ProfileImageHelper.MaxBytes + 1]) : WriteFile(dir, name, new byte[] { 1, 2, 3 });
            await dialog.SetPictureAsync(bad);

            Assert.Equal(before, dialog.ViewModel.Image);                          // 앞의 사진 그대로
            Assert.Equal(copiesBefore, Directory.GetFiles(Path.Combine(dir, "Profile")).Length);   // 복사하지 않았다
            var popup = Assert.Single(events.OfType<OpenInfoPopupMessageModel>());
            Assert.Equal(("사진", ProfileImageHelper.RejectedText), (popup.Title, popup.Explain));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
    #endregion

    #region - 창 없는 "?" 도우미 -
    private static readonly string[] AccountKeys = AccountsHelp.Entries.Select(e => e.Key).ToArray();

    /// <summary>HWND 없는 입력 원천 — 창을 띄우지 않고 "?" 가 바깥 클릭 · Esc 를 들을 뿌리를 준다.</summary>
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>말풍선 몸(PART_Callout · 그 안 PART_Body)은 있고 말풍선 창(Popup)은 없는 템플릿 — 열어도 HWND 가 생기지 않는다.</summary>
    private const string CalloutOnlyTemplate =
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
        "xmlns:c='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils' TargetType='c:HelpTip'>" +
        "<Grid><Border Width='20' Height='20' Background='Transparent'/>" +
        "<c:HelpCallout x:Name='PART_Callout'><c:HelpCallout.Template><ControlTemplate TargetType='c:HelpCallout'>" +
        "<StackPanel x:Name='PART_Body'/></ControlTemplate></c:HelpCallout.Template></c:HelpCallout></Grid></ControlTemplate>";

    private static HelpTip CalloutTip(string key)
    {
        var tip = new HelpTip { HelpKey = key, Template = (ControlTemplate)XamlReader.Parse(CalloutOnlyTemplate) };
        tip.ApplyTemplate();
        tip.Callout!.ApplyTemplate();
        return tip;
    }

    private static bool PressEscape(HeadlessSource source, UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static void PressMouse(UIElement element)
        => element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });

    private static void RaiseLoaded(FrameworkElement element) => element.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, element));

    /// <summary>말풍선 몸에 실제로 그려진 글(한글 줄바꿈 표지 U+2060 제거).</summary>
    private static string[] BodyLines(HelpCallout callout)
    {
        var body = (StackPanel)callout.Template.FindName("PART_Body", callout);
        // 글 조각(Run) + 단축키 칩(InlineUIContainer → Border → TextBlock)
        static string Piece(Inline inline) => inline switch
        {
            Run run => run.Text,
            InlineUIContainer { Child: Border { Child: TextBlock chip } } => chip.Text,
            _ => string.Empty,
        };
        static string Text(TextBlock t) => KoreanWordWrap.Strip(string.Concat(t.Inlines.Select(Piece)));
        return body.Children.OfType<FrameworkElement>()
            .SelectMany(e => e is TextBlock t ? new[] { t } : e is Grid g ? g.Children.OfType<TextBlock>().ToArray() : Array.Empty<TextBlock>())
            .Select(Text).Where(s => s.Length > 0).ToArray();
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { HelpTip.CloseCurrent(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
    #endregion

    #region - AUX-023: 말풍선 규칙(계정) -
    // Esc = 열린 말풍선만 닫고 먹는다(닫혀 있으면 지나감) · 바깥 클릭 = 닫힘("?" 자신은 아님) · 한 번에 하나 · F1 = 가장 가까운 범위의 "?".
    [Trait("Scenario", "AUX-023")]
    [Fact]
    public void should_follow_the_callout_rules_escape_outside_click_one_at_a_time_and_f1_on_the_account_question_marks()
    {
        var r = OnSta(() =>
        {
            // 콘솔 셸 뿌리(F1) → 콘솔 머리 "?" → 절 둘(권한 그룹 · 세션 정책)과 절 밖 검색 칸 · 빈 바탕
            var root = new Border();
            HelpTip.SetHandlesF1(root, true);
            var console = new StackPanel();
            root.Child = console;
            var consoleTip = new HelpTip { HelpKey = "Accounts.Console", Scope = console };
            var groups = new StackPanel();
            var groupsTip = new HelpTip { HelpKey = "Accounts.Permissions", Scope = groups };
            var groupField = new TextBox();
            groups.Children.Add(groupsTip);
            groups.Children.Add(groupField);
            var policy = new StackPanel();
            var policyTip = new HelpTip { HelpKey = "Accounts.SessionSetup.Policy", Scope = policy };
            var lockout = new TextBox();
            policy.Children.Add(policyTip);
            policy.Children.Add(lockout);
            var search = new TextBox();
            var blank = new Border { Width = 200, Height = 40, Background = Brushes.Transparent };
            console.Children.Add(consoleTip);
            console.Children.Add(search);
            console.Children.Add(groups);
            console.Children.Add(policy);
            console.Children.Add(blank);
            var tips = new[] { consoleTip, groupsTip, policyTip };

            var source = new HeadlessSource { RootVisual = root };
            try
            {
                foreach (var tip in tips) RaiseLoaded(tip);                         // 렌더 펌프가 없으니 범위 등록(Loaded)을 직접 울린다

                // 하나씩
                var oneAtATime = new List<string>();
                foreach (var tip in tips)
                {
                    tip.Open();
                    oneAtATime.Add(string.Join(",", tips.Where(t => t.IsChecked == true).Select(t => t.HelpKey)));
                }

                // 바깥 클릭: "?" 자신을 눌러도 닫히지 않고(토글이 맡는다) 바탕을 누르면 닫힌다
                PressMouse(policyTip);
                var afterSelf = policyTip.IsChecked == true;
                PressMouse(blank);
                var afterOutside = policyTip.IsChecked != true && HelpTip.Current is null;

                // 말풍선 안을 누르면 닫히지 않는다 — 다시 열고 말풍선 몸 대신 "?" 의 자식 없음 → 절 안 다른 칸은 '바깥'
                groupsTip.Open();
                PressMouse(groupField);
                var fieldInSectionIsOutside = groupsTip.IsChecked != true;

                // Esc
                consoleTip.Open();
                var escOpen = PressEscape(source, consoleTip);
                var escClosedIt = consoleTip.IsChecked != true && HelpTip.Current is null;
                var escPasses = !PressEscape(source, consoleTip);

                // F1
                ApplicationCommands.Help.Execute(null, lockout);
                var f1Field = HelpTip.Current?.HelpKey;
                ApplicationCommands.Help.Execute(null, groupField);
                var f1Group = HelpTip.Current?.HelpKey;
                var policyClosedByF1 = policyTip.IsChecked != true;
                ApplicationCommands.Help.Execute(null, search);
                var f1Outside = HelpTip.Current?.HelpKey;
                var escAfterF1 = PressEscape(source, search) && HelpTip.Current is null;

                return (oneAtATime, afterSelf, afterOutside, fieldInSectionIsOutside, escOpen, escClosedIt, escPasses,
                        f1Field, f1Group, policyClosedByF1, f1Outside, escAfterF1);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal(new[] { "Accounts.Console", "Accounts.Permissions", "Accounts.SessionSetup.Policy" }, r.oneAtATime);   // 열 때마다 방금 연 것 하나
        Assert.True(r.afterSelf);
        Assert.True(r.afterOutside);
        Assert.True(r.fieldInSectionIsOutside);
        Assert.True(r.escOpen);
        Assert.True(r.escClosedIt);
        Assert.True(r.escPasses);                                                    // 닫혀 있으면 Esc 는 콘솔 몫으로 지나간다
        Assert.Equal("Accounts.SessionSetup.Policy", r.f1Field);
        Assert.Equal("Accounts.Permissions", r.f1Group);
        Assert.True(r.policyClosedByF1);                                             // F1 로 연 것도 한 번에 하나
        Assert.Equal("Accounts.Console", r.f1Outside);                               // 절 밖이면 콘솔 머리 "?"
        Assert.True(r.escAfterF1);
    }
    #endregion

    #region - AUX-025: 한 번도 안 연 키 — 글 · Esc -
    public static IEnumerable<object[]> AccountKeyRows() => AccountsHelp.Entries.Select(e => new object[] { e.Key });

    [Trait("Scenario", "AUX-025")]
    [Theory]
    [MemberData(nameof(AccountKeyRows))]
    public void should_draw_the_whole_callout_text_with_its_body_id_and_close_it_with_escape_for_every_account_key(string key)
    {
        var entry = HelpCatalog.Find(key);
        Assert.NotNull(entry);
        var expected = new[] { entry!.Title }
            .Concat(entry.Sections.SelectMany(s => (string.IsNullOrWhiteSpace(s.Heading) ? Array.Empty<string>() : new[] { s.Heading! }).Concat(s.Items)))
            .Select(HelpText.Plain).ToArray();

        var r = OnSta(() =>
        {
            var tip = CalloutTip(key);
            var root = new StackPanel { Children = { tip } };
            var source = new HeadlessSource { RootVisual = root };
            try
            {
                Assert.True(tip.IsAvailable, $"'{key}' \"?\" 가 꺼져 있다(설명 목록에 없음)");
                tip.Open();
                var callout = tip.Callout!;
                var id = AutomationProperties.GetAutomationId(callout);
                var lines = BodyLines(callout);
                var name = UIElementAutomationPeer.CreatePeerForElement(callout).GetName();
                callout.Measure(new Size(HelpCallout.MaxCardWidth, double.PositiveInfinity));
                var height = callout.DesiredSize.Height;
                var esc = PressEscape(source, tip);
                return (Open: true, id, lines, name, height, esc, Closed: tip.IsChecked != true && HelpTip.Current is null);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal($"Help.{key}.Body", r.id);                                    // 헤디드 시험이 찾는 몸 id
        Assert.Equal("? " + expected[0], r.lines[0]);                               // 첫 줄 = "?" + 제목
        Assert.Equal(expected.Skip(1), r.lines.Skip(1));                            // 절 머리 · 항목이 빠짐없이 차례로
        Assert.Contains(entry.Title, r.name);                                       // UIA 이름 = 평문(제목 · 항목)
        Assert.All(entry.Sections.SelectMany(s => s.Items), item => Assert.Contains(HelpText.Plain(item), KoreanWordWrap.Strip(r.name)));
        Assert.True(r.height > 0, "말풍선 몸이 높이를 갖지 않는다 — 글이 그려지지 않았다");
        Assert.True(r.esc);                                                          // Esc 는 말풍선만 닫고 먹는다
        Assert.True(r.Closed);
    }
    #endregion
}
