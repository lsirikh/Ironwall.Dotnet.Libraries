using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.5(AUX) 중 W7 몫의 커널 쪽 —
               AUX-002(WINDY 닫기: [닫기] · ✕ · Esc) · AUX-003(WINDY "?": 첫 Esc 말풍선만 · 둘째 Esc 창 닫기) ·
               AUX-024(말풍선 몸 rect ⊂ 셸) · AUX-026(섹션 없는 F1 → 머리 "?") · AUX-028(카드 키보드 이동 · 클램프).
   Notes     : 창을 띄우지 않는다(Window.Show · 화면 밖 창 금지). 실제 커널 템플릿(Themes/Generic.xaml)을 부모 없는 요소에 물리고
               Measure/Arrange 만 한다(ConsoleShellTests 와 같은 방식). 입력 원천은 HWND 없는 가짜 PresentationSource.
               "?" 의 말풍선 창(Popup)은 HWND 를 만들므로, 뿌리 자원에 '말풍선 몸만 있고 Popup 은 없는' HelpTip 암시 스타일을 둔다.
               WINDY 창 자체(호스트 WindyPanelView)는 이 저장소에 없다 — 같은 틀 설정(DialogKey=Shell.Windy · HelpKey=Host.Windy.Mode ·
               Size=Small · 버튼 줄에 [닫기]=ClickCancel)으로 커널 거동을 보고, 호스트의 ✕ · Esc → [닫기] 넘김 배선은
               ShellHostEntryPointContractTests 가 호스트 소스로 지킨다.
****************************************************************************/
[Collection(WpfApplicationCollection.Name)]
public class ShellKernelScenarioTests
{
    private static readonly object EnsureApplicationGate = new();

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    #region - 세우기 -
    private static ResourceDictionary Kernel()
    {
        lock (EnsureApplicationGate)
        {
            if (Application.Current == null) _ = new Application();
        }
        return (ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Utils;component/Themes/Generic.xaml", UriKind.Relative));
    }

    private static ResourceDictionary Tokens(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    /// <summary>"?" 암시 스타일 — 말풍선 몸(PART_Callout · PART_Body)은 있고 말풍선 창(Popup)은 없다. 열어도 HWND 가 생기지 않는다.</summary>
    private const string CalloutOnlyHelpTipStyle =
        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " +
        "xmlns:c='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils' TargetType='c:HelpTip'>" +
        "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='c:HelpTip'>" +
        "<Grid><Border Width='20' Height='20' Background='Transparent'/>" +
        "<c:HelpCallout x:Name='PART_Callout' Visibility='Collapsed'><c:HelpCallout.Template><ControlTemplate TargetType='c:HelpCallout'>" +
        "<StackPanel x:Name='PART_Body'/></ControlTemplate></c:HelpCallout.Template></c:HelpCallout></Grid>" +
        "</ControlTemplate></Setter.Value></Setter></Style>";

    /// <summary>커널 사전 + 라이트 토큰 + 팝업 없는 "?" 를 자원으로 가진 뿌리.</summary>
    private static Grid Root(ResourceDictionary kernel)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(kernel);
        root.Resources.MergedDictionaries.Add(Tokens("Tokens.Light.xaml"));
        root.Resources.Add(typeof(HelpTip), XamlReader.Parse(CalloutOnlyHelpTipStyle));
        return root;
    }

    private static void Layout(FrameworkElement root, double width, double height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    /// <summary>렌더 펌프 없이도 "?" 가 제 범위에 이름을 걸도록 Loaded 를 울린다(이미 걸렸으면 같은 값).</summary>
    private static void RaiseLoaded(DependencyObject root)
    {
        foreach (var tip in Descendants<HelpTip>(root)) tip.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, tip));
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

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

    private static bool Press(HeadlessSource source, UIElement target, Key key, RoutedEvent? routed = null)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = routed ?? Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

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
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
    #endregion

    #region - WINDY 모양 카드 -
    private const string WindyHelpKey = "Host.Windy.Mode";

    /// <summary>호스트 HostHelp 의 강풍모드 "?" 와 같은 키 — 이 프로세스에는 호스트가 없어 같은 키로 하나 등록한다(이미 있으면 그대로).</summary>
    private static void EnsureWindyHelp()
    {
        if (HelpCatalog.Find(WindyHelpKey) is null)
            HelpCatalog.Register(new[] { HelpEntry.Create(WindyHelpKey, "강풍모드", "고르는 즉시 적용됩니다.", "[닫기] · ✕ · Esc 로 닫습니다.") });
    }

    private sealed record WindyRig(Grid Root, ConsoleDialogFrame Frame, Button ClickCancel, RadioButton Mode, HeadlessSource Source)
    {
        public int Secondary { get; set; }
        public int Cancels { get; set; }
    }

    /// <summary>
    /// 호스트 WindyPanelView 와 같은 틀 설정. 호스트 코드 숨김(OnSecondaryInvoked → PopupActionForwarder.Invoke(ClickCancel))처럼
    /// 틀의 취소(✕ · Esc)를 [닫기] 의 Click 으로 넘기고, [닫기] Click(= cal:Message.Attach ClickCancel)을 센다.
    /// </summary>
    private static WindyRig Windy(double width = 1280, double height = 800)
    {
        EnsureWindyHelp();
        var kernel = Kernel();
        var root = Root(kernel);
        var clickCancel = new Button { Name = "ClickCancel", Content = "닫기", Height = 34, MinWidth = 92 };
        var mode = new RadioButton { Content = "강풍 모드", GroupName = "GroupAction" };
        var frame = new ConsoleDialogFrame
        {
            DialogKey = "Shell.Windy",
            HelpKey = WindyHelpKey,
            PrimaryText = "",
            SecondaryText = "",
            Size = DialogSize.Small,
            Title = "강풍모드 설정",
            FooterActions = clickCancel,
            Content = new StackPanel { Children = { new RadioButton { Content = "일반 모드", GroupName = "GroupAction" }, mode } },
        };
        root.Children.Add(frame);
        var source = new HeadlessSource { RootVisual = root };
        var rig = new WindyRig(root, frame, clickCancel, mode, source);
        frame.SecondaryInvoked += (_, _) => { rig.Secondary++; Click(clickCancel); };
        clickCancel.Click += (_, _) => rig.Cancels++;
        Layout(root, width, height);
        Pump();
        RaiseLoaded(root);
        return rig;
    }
    #endregion

    // ── AUX-002: WINDY 닫기 — [닫기] · ✕ · Esc 가 모두 같은 닫기 길로 ─────────────────────────────────
    [Trait("Scenario", "AUX-002")]
    [Fact]
    public void should_close_the_windy_card_through_the_same_cancel_path_from_the_close_button_the_header_x_and_escape()
    {
        var r = OnSta(() =>
        {
            var rig = Windy();
            try
            {
                var x = (Button)rig.Frame.Template.FindName(ConsoleDialogFrame.PartClose, rig.Frame);
                var primary = (Button)rig.Frame.Template.FindName(ConsoleDialogFrame.PartPrimary, rig.Frame);
                var secondary = (Button)rig.Frame.Template.FindName(ConsoleDialogFrame.PartSecondary, rig.Frame);
                var ids = (Root: AutomationProperties.GetAutomationId(rig.Frame), Close: AutomationProperties.GetAutomationId(x));
                var shown = (X: x.Visibility, Primary: primary.Visibility, Secondary: secondary.Visibility,
                             ClickCancelInCard: rig.ClickCancel.IsVisible || rig.ClickCancel.ActualWidth > 0);

                Click(rig.ClickCancel);                                       // [닫기]
                var afterButton = (rig.Secondary, rig.Cancels);
                Click(x);                                                     // 머리 ✕
                var afterX = (rig.Secondary, rig.Cancels);
                var escHandled = Press(rig.Source, rig.Mode, Key.Escape);    // Esc(말풍선 닫힘 상태)
                var afterEsc = (rig.Secondary, rig.Cancels);
                return (ids, shown, afterButton, afterX, escHandled, afterEsc);
            }
            finally { rig.Source.RootVisual = null!; }
        });

        Assert.Equal(("Dialog.Shell.Windy.Root", "Dialog.Shell.Windy.Close"), r.ids);   // 헤디드 시험이 찾는 ✕ id
        Assert.Equal(Visibility.Visible, r.shown.X);
        Assert.Equal(Visibility.Collapsed, r.shown.Primary);         // 고르는 즉시 적용 — [저장] 없음
        Assert.Equal(Visibility.Collapsed, r.shown.Secondary);       // 틀의 [취소] 대신 창의 [닫기]
        Assert.True(r.shown.ClickCancelInCard);
        Assert.Equal((0, 1), r.afterButton);                         // [닫기] = 뷰모델 ClickCancel 한 번
        Assert.Equal((1, 2), r.afterX);                              // ✕ = 틀 취소 → [닫기] 로 넘김(두 번 닫히지 않는다)
        Assert.True(r.escHandled);
        Assert.Equal((2, 3), r.afterEsc);                            // Esc = 같은 길
    }

    // ── AUX-003: WINDY "?" — 첫 Esc 는 말풍선만, 둘째 Esc 는 창 ──────────────────────────────────────
    [Trait("Scenario", "AUX-003")]
    [Fact]
    public void should_close_only_the_windy_callout_on_the_first_escape_and_close_the_card_on_the_second()
    {
        var r = OnSta(() =>
        {
            var rig = Windy();
            try
            {
                var tip = Descendants<HelpTip>(rig.Frame).Single();
                var scopeIsFrame = ReferenceEquals(tip.Scope, rig.Frame);

                ApplicationCommands.Help.Execute(null, rig.Mode);            // 카드 안에서 F1 → 머리 "?"
                var openedByF1 = tip.IsChecked == true;
                var bodyId = AutomationProperties.GetAutomationId(tip.Callout!);

                var first = Press(rig.Source, rig.Mode, Key.Escape);
                var afterFirst = (Callout: tip.IsChecked == true, rig.Secondary, rig.Cancels);
                var second = Press(rig.Source, rig.Mode, Key.Escape);
                var afterSecond = (Callout: tip.IsChecked == true, rig.Secondary, rig.Cancels);

                tip.Open();                                                   // 클릭으로 열어도 같다
                var third = Press(rig.Source, rig.Mode, Key.Escape);
                var afterThird = (Callout: tip.IsChecked == true, rig.Secondary, rig.Cancels);
                return (scopeIsFrame, openedByF1, bodyId, first, afterFirst, second, afterSecond, third, afterThird);
            }
            finally { rig.Source.RootVisual = null!; }
        });

        Assert.True(r.scopeIsFrame);
        Assert.True(r.openedByF1);
        Assert.Equal($"Help.{WindyHelpKey}.Body", r.bodyId);        // Help.Host.Windy.Mode.Body
        Assert.True(r.first);
        Assert.Equal((false, 0, 0), r.afterFirst);                  // 첫 Esc — 말풍선만 닫혔고 창은 그대로
        Assert.True(r.second);
        Assert.Equal((false, 1, 1), r.afterSecond);                 // 둘째 Esc — 창 닫기([닫기] 길)
        Assert.True(r.third);
        Assert.Equal((false, 1, 1), r.afterThird);
    }

    // ── AUX-028: 카드 키보드 이동 · 클램프 ─────────────────────────────────────────────────────────
    [Trait("Scenario", "AUX-028")]
    [Fact]
    public void should_move_the_card_one_step_per_arrow_from_the_move_handle_and_clamp_it_inside_the_host()
    {
        const double hostW = 900, hostH = 600;
        var r = OnSta(() =>
        {
            var rig = Windy(hostW, hostH);
            try
            {
                var frame = rig.Frame;
                var move = (Thumb)frame.Template.FindName(ConsoleDialogFrame.PartMove, frame);
                var card = (FrameworkElement)frame.Template.FindName(ConsoleDialogFrame.PartCard, frame);
                Rect CardRect() => card.TransformToAncestor(frame).TransformBounds(new Rect(card.RenderSize));
                var id = AutomationProperties.GetAutomationId(move);
                var size = new Size(card.ActualWidth, card.ActualHeight);
                var start = CardRect();

                // 손잡이에 키보드 초점이 없으면 화살표는 카드를 옮기지도, 먹지도 않는다(라디오 · 목록의 화살표 몫).
                var arrowOnModeHandled = Press(rig.Source, rig.Mode, Key.Right);
                var arrowOnHandleUnfocusedHandled = Press(rig.Source, move, Key.Right);
                var afterNoFocus = frame.Offset;
                var reachable = (move.Focusable, move.IsTabStop, move.IsVisible || move.ActualWidth > 0);
                var closeId = AutomationProperties.GetAutomationId((Button)frame.Template.FindName(ConsoleDialogFrame.PartClose, frame));
                return (id, closeId, reachable, size, start, arrowOnModeHandled, arrowOnHandleUnfocusedHandled, afterNoFocus, Secondary: rig.Secondary);
            }
            finally { rig.Source.RootVisual = null!; }
        });

        // ① 실제 템플릿 — 손잡이 id · Tab 으로 닿음 · 처음엔 가운데 · 초점 없으면 무반응
        var hostSize = new Size(hostW, hostH);
        Assert.Equal("Dialog.Shell.Windy.Move", r.id);
        Assert.Equal("Dialog.Shell.Windy.Close", r.closeId);
        Assert.Equal((true, true, true), r.reachable);
        Assert.True(r.size.Width > 0 && r.size.Height > 0, "카드가 배치되지 않았다");
        var center = DialogPlacementRules.Origin(new Vector(), r.size, hostSize);                 // 카드 rect = 가운데 자리 + 비킴(0)
        Assert.Equal(center.X, r.start.X, 0.5);                                                  // UseLayoutRounding — 반 픽셀은 반올림된다
        Assert.Equal(center.Y, r.start.Y, 0.5);
        Assert.Equal(r.size, r.start.Size);
        Assert.False(r.arrowOnModeHandled);
        Assert.False(r.arrowOnHandleUnfocusedHandled);
        Assert.Equal(new Vector(), r.afterNoFocus);
        Assert.Equal(0, r.Secondary);

        // ② 화살표 판정 — 틀이 초점 받은 손잡이의 화살표마다 부르는 그 함수(아래 ③ 계약)를 실제 카드 · 틀 크기로 되풀이한다
        var step = DialogPlacementRules.KeyboardStep;
        var coarse = DialogPlacementRules.KeyboardCoarseStep;
        var max = DialogPlacementRules.MaxOffset(r.size, hostSize);
        var host = new Rect(hostSize);
        Vector Arrow(Vector o, int dx, int dy, bool big = false) => DialogPlacementRules.KeyboardMove(o, dx, dy, big, r.size, hostSize);
        Rect CardAt(Vector o) => new(DialogPlacementRules.Origin(o, r.size, hostSize), r.size);

        var offset = Arrow(new Vector(), +1, 0);
        Assert.Equal(new Vector(step, 0), offset);                               // → 한 걸음
        offset = Arrow(offset, 0, +1);
        Assert.Equal(new Vector(step, step), offset);                            // ↓ 한 걸음
        Assert.Equal(new Vector(step + coarse, step), Arrow(offset, +1, 0, big: true));   // Ctrl+→ 큰 걸음
        Assert.True(max.X > step && max.Y > step, $"틀 {hostSize} 이 카드 {r.size} 보다 넉넉해야 옮길 수 있다");

        var rects = new List<Rect>();
        for (var i = 0; i < 400; i++) { offset = Arrow(Arrow(offset, +1, 0), 0, +1); rects.Add(CardAt(offset)); }
        Assert.Equal(max, offset);                                               // 오른쪽 아래 끝에서 멈춘다(더 눌러도 그대로)
        Assert.Equal(host.Right, CardAt(offset).Right, 3);
        Assert.Equal(host.Bottom, CardAt(offset).Bottom, 3);
        for (var i = 0; i < 800; i++) { offset = Arrow(Arrow(offset, -1, 0, big: i % 3 == 0), 0, -1); rects.Add(CardAt(offset)); }
        Assert.Equal(-max, offset);                                              // 왼쪽 위 끝에서 멈춘다
        Assert.Equal(host.Left, CardAt(offset).Left, 3);
        Assert.Equal(host.Top, CardAt(offset).Top, 3);
        Assert.All(rects, rect => Assert.True(Contains(host, rect), $"카드 {rect} 가 틀 {host} 밖으로 나갔다"));   // ✕ · 버튼 줄이 늘 안에

        // ③ 배선 — 화살표는 손잡이에 초점이 있을 때만 그 함수로 Offset(카드 RenderTransform)을 바꾸고 먹는다
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Utils", "Console", "Dialogs", "ConsoleDialogFrame.cs"));
        Assert.Contains("if (TryKeyboardMove(e)) return;", source);
        Assert.Contains("if (!CanMove || _move?.IsKeyboardFocused != true) return false;", source);
        Assert.Contains("Offset = DialogPlacementRules.KeyboardMove(Offset, dirX, dirY, coarse, CardSize, HostSize);", source);
        Assert.Contains("_card.RenderTransform = _shift;", source);
        foreach (var arrow in new[] { "Key.Left => (-1, 0)", "Key.Right => (1, 0)", "Key.Up => (0, -1)", "Key.Down => (0, 1)" })
            Assert.Contains(arrow, source);
    }

    // ── AUX-026: 섹션 없는 F1 → 머리 "?" ────────────────────────────────────────────────────────
    [Trait("Scenario", "AUX-026")]
    [Fact]
    public void should_open_the_console_header_question_mark_when_f1_is_pressed_where_no_section_owns_the_focus()
    {
        const string consoleKey = "Shell.W7.Aux026.Console";
        const string sectionKey = "Shell.W7.Aux026.Section";
        HelpCatalog.Register(new[] { HelpEntry.Create(consoleKey, "콘솔", "머리 설명"), HelpEntry.Create(sectionKey, "절", "절 설명") });

        var r = OnSta(() =>
        {
            var kernel = Kernel();
            var root = Root(kernel);
            var headButton = new Button { Content = "새로 고침" };                   // 머리 끼움 칸(HeaderContent)
            var listField = new TextBox();                                          // 목록 자리 — 어느 절에도 속하지 않는다
            var sectionField = new TextBox();
            var section = new ConsoleSection { Header = "절", HelpKey = sectionKey, Content = sectionField };
            var shell = new ConsoleShell
            {
                ConsoleKey = "W7", Title = "콘솔", HelpKey = consoleKey, HeaderContent = headButton,
                List = new StackPanel { Children = { listField, section } },
            };
            root.Children.Add(shell);
            var source = new HeadlessSource { RootVisual = root };
            try
            {
                Layout(root, 1280, 800);
                Pump();
                RaiseLoaded(root);
                var headTip = Descendants<HelpTip>(shell).Single(t => t.HelpKey == consoleKey);
                var sectionTip = Descendants<HelpTip>(shell).Single(t => t.HelpKey == sectionKey);

                // 진짜 F1 키 — 머리 칸에서 올라온 KeyDown(F1) → 명령 관리자가 F1 제스처로 셸의 Help 바인딩을 찾아 보낸다.
                // (키보드 초점 자체는 HWND 가 있어야 옮겨진다 — 여기서는 초점 요소가 받을 KeyDown 을 그 요소에서 올린다)
                var keyHandled = Press(source, headButton, Key.F1, Keyboard.KeyDownEvent);
                var byKey = HelpTip.Current?.HelpKey;
                var bodyId = HelpTip.Current?.Callout is { } c ? AutomationProperties.GetAutomationId(c) : null;
                HelpTip.CloseCurrent();

                ApplicationCommands.Help.Execute(null, listField);                 // 절 밖 목록 자리
                var fromList = HelpTip.Current?.HelpKey;
                ApplicationCommands.Help.Execute(null, sectionField);              // 절 안 — 그 절
                var fromSection = HelpTip.Current?.HelpKey;
                var headClosed = headTip.IsChecked != true;
                ApplicationCommands.Help.Execute(null, headButton);                // 다시 머리
                var fromHead = HelpTip.Current?.HelpKey;
                return (handles: HelpTip.GetHandlesF1(shell), headScope: ReferenceEquals(headTip.Scope, shell),
                        keyHandled, byKey, bodyId, fromList, fromSection, headClosed, fromHead, sectionClosed: sectionTip.IsChecked != true);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.True(r.handles);                                    // 셸은 F1 을 기본으로 받는다
        Assert.True(r.headScope);                                  // 머리 "?" 의 범위 = 셸 전체
        Assert.True(r.keyHandled, "F1 키가 셸의 도움말 명령으로 번역되지 않았다");
        Assert.Equal(consoleKey, r.byKey);                         // 머리 포커스 → F1 → 머리 "?"
        Assert.Equal($"Help.{consoleKey}.Body", r.bodyId);
        Assert.Equal(consoleKey, r.fromList);
        Assert.Equal(sectionKey, r.fromSection);
        Assert.True(r.headClosed);                                 // 한 번에 하나
        Assert.Equal(consoleKey, r.fromHead);
        Assert.True(r.sectionClosed);
    }

    // ── AUX-024: 말풍선 몸 rect ⊂ 셸 ───────────────────────────────────────────────────────────
    // "?" 가 셸 어디에 있든(가장자리 · 구석 포함) 말풍선이 셸 안에 통째로 들어갈 수 있으면 몸(꼬리 자리 뺀 카드)은 셸 안에 놓인다.
    // HelpTip 은 배치를 이 순수 함수 하나로 정한다(PopupFitTests.help_tip_places_its_callout_window_first 가 배선을 지킨다).
    public static IEnumerable<object[]> ShellRects() => new[]
    {
        new object[] { "최대화 1920x1032", new Rect(0, 0, 1920, 1032) },
        new object[] { "복원 1600x900", new Rect(160, 66, 1600, 900) },
        new object[] { "좁힘 1280x800", new Rect(40, 40, 1280, 800) },
        new object[] { "오른쪽 붙임 1280x800", new Rect(640, 232, 1280, 800) },
    };

    [Trait("Scenario", "AUX-024")]
    [Theory]
    [MemberData(nameof(ShellRects))]
    public void should_place_the_callout_body_inside_the_shell_wherever_the_question_mark_sits(string label, Rect shell)
    {
        var work = new Rect(0, 0, 1920, 1032);
        var width = HelpCallout.MaxCardWidth + 2 * HelpCalloutPlacement.TailMargin;
        var misses = new List<string>();
        var checkedCount = 0;
        foreach (var height in new[] { 120d, 200d, 280d, 360d, 400d })
        {
            var callout = new Size(width, height + 2 * HelpCalloutPlacement.TailMargin);
            for (var y = shell.Top + 4; y + 20 <= shell.Bottom - 4; y += 12)
                for (var x = shell.Left + 4; x + 20 <= shell.Right - 4; x += 12)
                {
                    var target = new Rect(x, y, 20, 20);
                    var placed = HelpCalloutPlacement.Resolve(target, callout, shell, work);
                    var outer = new Rect(placed.Position, callout);
                    var body = Rect.Inflate(outer, -HelpCalloutPlacement.TailMargin, -HelpCalloutPlacement.TailMargin);
                    checkedCount++;
                    if (!Contains(shell, body)) misses.Add($"h={height} \"?\"=({x:0},{y:0}) → 몸 {body} ({placed.Side})");
                    if (body.IntersectsWith(target)) misses.Add($"h={height} \"?\"=({x:0},{y:0}) 몸이 \"?\" 를 덮는다 {body}");
                }
        }

        Assert.True(checkedCount > 1000, $"{label}: 표본이 너무 적다({checkedCount})");
        Assert.True(misses.Count == 0, $"{label}: {misses.Count}/{checkedCount} 건 — {string.Join(" ; ", misses.Take(8))}");
    }

    private static bool Contains(Rect outer, Rect inner)
        => inner.Left >= outer.Left - 0.01 && inner.Top >= outer.Top - 0.01 && inner.Right <= outer.Right + 0.01 && inner.Bottom <= outer.Bottom + 0.01;
}
