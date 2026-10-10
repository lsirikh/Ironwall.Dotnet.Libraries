using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Help;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 앞 작업이 "헤디드 남김" 으로 둔 ACC 항목 중 창 없이 확인할 수 있는 부분 —
               ACC-016(손잡이 → 칩 끌어 놓기 = 칩 클릭) · ACC-032(계정 "?" — 열림 · 한 번에 하나).
   Notes     : 실제 마우스 캡처 끌기 · Esc 취소(CaptureDragBehavior) · F1 포커스 탐색 · Esc 로 말풍선 닫기는
               창(마우스 캡처 · 키보드 포커스 · 보이는 트리)이 있어야 해 여기서 보지 않는다(헤디드 몫).
               이 프로세스는 Application.Current == null 을 전제로 하므로(ConsoleHarness) Application 을 만들지 않는다.
****************************************************************************/
public class AccountHeadedLeftoverScenarioTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static IEnumerable<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel> TwoUsers() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영"),
        ConsoleFixtures.User(2, "op2", "이감시"),
    };

    private static UserGroupDto Group(int id, string name) => new() { Id = id, Name = name, IsActive = true, Permissions = new PermissionsDto() };

    private static async Task<TestAccountConsole> OpenWithBothUsersSelectedAsync()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(TwoUsers(), new[] { Group(7, "야간조") });
        await console.ActivateForTestAsync();
        console.OnUsersSelected(console.AccountManagerPanelViewModel.ViewModelProvider.Cast<object>().ToList());
        Assert.Equal(2, console.SelectedRows.Count);
        Assert.Single(console.GroupChips);
        return console;
    }

    private static List<(string, string, string)> Drafts(TestAccountConsole console)
        => console.DraftTray.Entries.Select(e => (e.TargetKey, e.CallKind, e.Description)).ToList();

    // ── ACC-016: 손잡이 → 칩 끌어 놓기 — 칩 클릭과 같다 ─────────────────────────────
    [Trait("Scenario", "ACC-016")]
    [Fact]
    public async Task should_queue_the_same_drafts_and_say_the_same_line_when_users_are_dropped_on_a_chip_as_when_the_chip_is_clicked()
    {
        var clicked = await OpenWithBothUsersSelectedAsync();
        var dropped = await OpenWithBothUsersSelectedAsync();

        // 칩 클릭(키보드 · 자동화 폴백)
        clicked.AssignSelectionToGroup(clicked.GroupChips[0]);

        // 끌어 놓기 — 커널 캡처 드래그가 놓는 순간 부르는 것과 같은 두 호출(CanDrop → Drop).
        // 판정 · 처리는 출발 목록(Source)을 보지 않는다 — 창 없이 null 로 둔다.
        var payload = new DragPayload(null!, dropped.SelectedRows.Cast<object>().ToList(), "op1");
        var target = new DropTarget(UserGroupDrop.ZoneKey, dropped.GroupChips[0], -1);
        Assert.True(dropped.GroupDrop.CanDrop(payload, target));
        dropped.GroupDrop.Drop(payload, target);

        Assert.Equal(2, clicked.DraftTray.Count);
        Assert.Equal(Drafts(clicked), Drafts(dropped));
        Assert.Equal(clicked.StatusText, dropped.StatusText);
        Assert.Contains("2명을 ‘야간조’ 그룹으로 옮길 준비가 됐습니다", dropped.StatusText);
        Assert.True(dropped.ShowDraftTray);
    }

    [Trait("Scenario", "ACC-016")]
    [Fact]
    public async Task should_queue_nothing_when_the_users_are_released_over_a_zone_that_is_not_a_group_chip()
    {
        var console = await OpenWithBothUsersSelectedAsync();
        var payload = new DragPayload(null!, console.SelectedRows.Cast<object>().ToList(), "op1");

        // 바깥(칩이 아닌 곳) — 커널은 CanDrop 이 거절한 자리에서는 Drop 을 부르지 않는다
        var outside = new DropTarget("unit", null, -1);
        Assert.False(console.GroupDrop.CanDrop(payload, outside));
        console.GroupDrop.Drop(payload, outside);                     // 불려도 칩이 아니면 아무것도 담지 않는다

        Assert.False(console.DraftTray.HasEntries);
        Assert.False(console.ShowDraftTray);
    }

    // ── ACC-032: 계정 "?" — 콘솔 · 권한 · 부여 · 정책 ×2 ─────────────────────────
    private static readonly string[] AccountHelpKeys =
    {
        "Accounts.Console", "Accounts.Permissions", "Accounts.Grants", "Accounts.SessionSetup.Policy", "Accounts.SessionSetup.Concurrency",
    };

    [Trait("Scenario", "ACC-032")]
    [Fact]
    public void should_register_a_callout_for_every_question_mark_key_on_the_account_views()
    {
        _ = AccountsHelp.Entries;   // 모듈 초기화(설명 목록 등록)가 돌았는지 확실히 한다
        var views = Directory.GetFiles(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views"), "*.xaml", SearchOption.AllDirectories);
        var keys = views.SelectMany(v => Regex.Matches(File.ReadAllText(v), "HelpKey=\"([^\"]+)\"").Select(m => m.Groups[1].Value))
                        .Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.Equal(AccountHelpKeys.OrderBy(k => k, StringComparer.Ordinal), keys);   // 콘솔 · 권한 · 부여 · 정책 ×2
        foreach (var key in keys)
        {
            var entry = HelpCatalog.Find(key);
            Assert.True(entry is not null, $"'{key}' 의 설명이 목록에 없다 — \"?\" 가 꺼진다");
            Assert.False(string.IsNullOrWhiteSpace(entry!.Title));
            Assert.NotEmpty(entry.Sections);
        }
    }

    [Trait("Scenario", "ACC-032")]
    [Fact]
    public void should_keep_only_one_account_callout_open_when_the_question_marks_are_opened_one_after_another()
    {
        _ = AccountsHelp.Entries;
        var (afterEach, closedAtEnd) = OnSta(() =>
        {
            var tips = AccountHelpKeys.Select(k => new HelpTip { HelpKey = k }).ToList();
            var open = new List<string>();
            foreach (var tip in tips)
            {
                Assert.True(tip.IsAvailable, $"'{tip.HelpKey}' \"?\" 가 꺼져 있다");
                tip.Open();
                open.Add(string.Join(",", tips.Where(t => t.IsChecked == true).Select(t => t.HelpKey)) + "|" + HelpTip.Current?.HelpKey);
            }
            HelpTip.CloseCurrent();
            return (open, tips.All(t => t.IsChecked != true) && HelpTip.Current is null);
        });

        // 열 때마다 방금 연 것 하나만 열려 있다
        Assert.Equal(AccountHelpKeys.Select(k => $"{k}|{k}"), afterEach);
        Assert.True(closedAtEnd);
    }

    /// <summary>HWND 없는 입력 원천 — 창을 띄우지 않고 "?" 가 Esc · F1 을 들을 뿌리를 준다(Reports.Ui ReportLeftoverScenarioTests 와 같은 방법).</summary>
    private sealed class HeadlessSource : System.Windows.PresentationSource
    {
        private System.Windows.Media.Visual? _root;
        public override System.Windows.Media.Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static bool PressEscape(HeadlessSource source, System.Windows.UIElement target)
    {
        var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, System.Windows.Input.Key.Escape)
        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    [Trait("Scenario", "ACC-032")]
    [Fact]
    public void should_close_only_the_open_account_callout_with_escape_and_let_escape_pass_when_none_is_open()
    {
        _ = AccountsHelp.Entries;
        var (handledWhileOpen, closed, handledWhenClosed) = OnSta(() =>
        {
            var root = new System.Windows.Controls.StackPanel();
            var tips = AccountHelpKeys.Select(k => new HelpTip { HelpKey = k }).ToList();
            foreach (var tip in tips) root.Children.Add(tip);
            var source = new HeadlessSource { RootVisual = root };
            try
            {
                tips[3].Open();                                               // 세션 정책 "?"
                var first = PressEscape(source, tips[3]);
                var shut = tips[3].IsChecked != true && HelpTip.Current is null;
                var second = PressEscape(source, tips[3]);
                return (first, shut, second);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(handledWhileOpen);     // 열린 말풍선을 닫고 Esc 를 먹는다
        Assert.True(closed);
        Assert.False(handledWhenClosed);   // 닫혀 있으면 Esc 는 지나간다(콘솔 닫기 · 선택 해제 몫)
    }

    [Trait("Scenario", "ACC-032")]
    [Fact]
    public void should_open_the_nearest_account_question_mark_when_help_is_asked_from_a_session_policy_field_or_outside_it()
    {
        _ = AccountsHelp.Entries;
        var (fromField, fromOutside, policyClosed) = OnSta(() =>
        {
            // 콘솔 셸 뿌리(F1 처리) → 콘솔 "?"(Accounts.Console) → 세션 정책 절 "?"(Accounts.SessionSetup.Policy) + 잠금 횟수 칸
            var root = new System.Windows.Controls.Border();
            HelpTip.SetHandlesF1(root, true);
            var console = new System.Windows.Controls.StackPanel();
            root.Child = console;
            var consoleTip = new HelpTip { HelpKey = "Accounts.Console", Scope = console };
            var policy = new System.Windows.Controls.StackPanel();
            var policyTip = new HelpTip { HelpKey = "Accounts.SessionSetup.Policy", Scope = policy };
            var lockout = new System.Windows.Controls.TextBox();
            policy.Children.Add(policyTip);
            policy.Children.Add(lockout);
            var search = new System.Windows.Controls.TextBox();                // 절 밖(툴바 검색 자리)
            console.Children.Add(consoleTip);
            console.Children.Add(search);
            console.Children.Add(policy);

            var source = new HeadlessSource { RootVisual = root };
            try
            {
                // 렌더 펌프가 없으니 "?" 가 제 범위에 이름을 거는 Loaded 를 직접 울린다
                consoleTip.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent, consoleTip));
                policyTip.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent, policyTip));

                System.Windows.Input.ApplicationCommands.Help.Execute(null, lockout);
                var a = HelpTip.Current?.HelpKey;
                System.Windows.Input.ApplicationCommands.Help.Execute(null, search);
                var b = HelpTip.Current?.HelpKey;
                return (a, b, policyTip.IsChecked != true);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal("Accounts.SessionSetup.Policy", fromField);   // 칸에서 F1 = 그 절의 "?"
        Assert.Equal("Accounts.Console", fromOutside);             // 절 밖이면 콘솔 머리 "?"
        Assert.True(policyClosed);                                  // 한 번에 하나
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
}
