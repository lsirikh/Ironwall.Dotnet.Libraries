using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Microsoft.Xaml.Behaviors;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 공용 "?"(<see cref="HelpTip"/>) — 열고 닫기 · Esc · 한 번에 하나 · 바깥 클릭 · 없는 키 · F1 · "키보드로" 자동 묶음 · 커널 칸 · 토큰 재해석
/// (help-callout PRD FR-01 · 02 · 04 · 05 · 06 · NFR-01).
/// </summary>
/// <remarks>
/// <para>실제 창(화면 밖)에 띄운다. 대부분의 시험은 창 자원에 <b>말풍선 창(Popup) 없는 템플릿</b>을 암시 스타일로 넣어
/// 열림 판정만 본다 — 시험 중 화면에 팝업 창이 깜빡이지 않게(다른 세션의 헤디드 회차가 같은 데스크톱을 쓴다).
/// 기본 템플릿(진짜 팝업)은 몸 AutomationId · 이름을 보는 한 시험에서만 연다.</para>
/// <para>설명 목록은 프로세스 전역이라 시험마다 다른 키(<c>Test.Tip.*</c>)를 쓴다.</para>
/// </remarks>
[Collection(WpfFocusCollection.Name)]
public class HelpTipTests
{
    private const string NoPopupStyle =
        "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:c='clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils' TargetType='c:HelpTip'>" +
        "<Setter Property='Template'><Setter.Value><ControlTemplate TargetType='c:HelpTip'><Border Width='20' Height='20' Background='Transparent'/></ControlTemplate></Setter.Value></Setter>" +
        "</Style>";

    private static void Register(params string[] keys)
        => HelpCatalog.Register(keys.Select(k => HelpEntry.Create(k, $"제목 {k}", "항목 하나")));

    #region - Open · close -
    [Fact]
    public void should_open_on_click_and_close_on_a_second_click_when_the_key_is_registered()
    {
        Sta(() =>
        {
        Register("Test.Tip.Click");
        var r = OnWindow(new HelpTip { HelpKey = "Test.Tip.Click" }, (window, tip) =>
        {
            var toggle = (IToggleProvider)UIElementAutomationPeer.CreatePeerForElement(tip).GetPattern(PatternInterface.Toggle);
            toggle.Toggle();
            var opened = (tip.IsChecked, HelpTip.Current == tip);
            toggle.Toggle();
            return (opened, Closed: tip.IsChecked == false, CurrentCleared: HelpTip.Current is null);
        });

        Assert.Equal((true, true), r.opened);
        Assert.True(r.Closed);
        Assert.True(r.CurrentCleared);
        });
    }

    [Theory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void should_open_from_the_keyboard_when_enter_or_space_is_pressed(Key key)
    {
        Sta(() =>
        {
        Register("Test.Tip.Keys");
        var opened = OnWindow(new HelpTip { HelpKey = "Test.Tip.Keys" }, (window, tip) =>
        {
            var source = PresentationSource.FromVisual(window)!;
            tip.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyDownEvent });
            tip.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.KeyUpEvent });
            return tip.IsChecked == true;
        }, focusTip: true);

        Assert.True(opened);
        });
    }

    [Fact]
    public void should_close_and_consume_escape_only_while_a_callout_is_open()
    {
        Sta(() =>
        {
        Register("Test.Tip.Esc");
        var r = OnWindow(new HelpTip { HelpKey = "Test.Tip.Esc" }, (window, tip) =>
        {
            var closedEsc = Escape(window);           // 닫혀 있으면 지나간다
            tip.Open();
            var openEsc = Escape(window);
            return (closedEsc, openEsc, StillOpen: tip.IsChecked == true);
        });

        Assert.False(r.closedEsc);                    // 다른 Esc 동작(선택 해제 · 대화창 닫기)을 막지 않는다
        Assert.True(r.openEsc);
        Assert.False(r.StillOpen);
        });
    }

    [Fact]
    public void should_keep_only_one_callout_open_when_another_question_mark_opens()
    {
        Sta(() =>
        {
        Register("Test.Tip.One.A", "Test.Tip.One.B");
        var a = new HelpTip { HelpKey = "Test.Tip.One.A" };
        var b = new HelpTip { HelpKey = "Test.Tip.One.B" };
        var r = OnWindow(new StackPanel { Children = { a, b } }, (window, _) =>
        {
            a.Open();
            b.Open();
            return (A: a.IsChecked, B: b.IsChecked, Current: HelpTip.Current);
        });

        Assert.Equal(false, r.A);
        Assert.Equal(true, r.B);
        Assert.Same(b, r.Current);
        });
    }

    [Fact]
    public void should_close_on_an_outside_click_but_not_when_the_question_mark_itself_is_pressed()
    {
        Sta(() =>
        {
        Register("Test.Tip.Outside");
        var tip = new HelpTip { HelpKey = "Test.Tip.Outside" };
        var elsewhere = new Border { Width = 100, Height = 40, Background = Brushes.Transparent };
        var r = OnWindow(new StackPanel { Children = { tip, elsewhere } }, (window, _) =>
        {
            tip.Open();
            PreviewDown(tip);                       // "?" 자신 — 닫기는 토글이 맡는다
            var afterSelf = tip.IsChecked;
            PreviewDown(elsewhere);
            return (afterSelf, afterOutside: tip.IsChecked);
        });

        Assert.Equal(true, r.afterSelf);
        Assert.Equal(false, r.afterOutside);
        });
    }

    [Fact]
    public void should_disable_the_question_mark_without_crashing_when_the_key_is_missing_and_enable_it_once_registered()
    {
        Sta(() =>
        {
        var r = OnWindow(new HelpTip { HelpKey = "Test.Tip.Late" }, (window, tip) =>
        {
            var before = (tip.IsEnabled, tip.IsAvailable, tip.Visibility);
            tip.Open();
            var openedWhileMissing = tip.IsChecked == true;
            Register("Test.Tip.Late");                // 늦게 들어온 설명 목록(모듈 초기화 순서) — 다시 찾는다
            Pump();
            return (before, openedWhileMissing, After: tip.IsEnabled);
        });

        Assert.Equal((false, false, Visibility.Visible), r.before);
        Assert.False(r.openedWhileMissing);
        Assert.True(r.After);
        });
    }

    [Fact]
    public void should_hide_the_question_mark_when_no_key_is_given()
    {
        Sta(() =>
        {
        var visibility = OnWindow(new HelpTip(), (_, tip) => tip.Visibility, useDefaultTemplate: true);

        Assert.Equal(Visibility.Collapsed, visibility);
        });
    }
    #endregion

    #region - Automation -
    [Fact]
    public void should_expose_a_button_named_help_dot_key_and_let_a_local_alias_win()
    {
        Sta(() =>
        {
        Register("Test.Tip.Id");
        var r = OnWindow(new StackPanel
        {
            Children =
            {
                new HelpTip { HelpKey = "Test.Tip.Id" },
                AliasTip("Test.Tip.Id", "Legacy.Help"),
            },
        }, (window, root) =>
        {
            var tips = ((StackPanel)root).Children.OfType<HelpTip>().ToList();
            var peer = UIElementAutomationPeer.CreatePeerForElement(tips[0]);
            return (Id: AutomationProperties.GetAutomationId(tips[0]), Alias: AutomationProperties.GetAutomationId(tips[1]),
                Type: peer.GetAutomationControlType(), Name: peer.GetName());
        }, useDefaultTemplate: true);

        Assert.Equal("Help.Test.Tip.Id", r.Id);
        Assert.Equal("Legacy.Help", r.Alias);
        Assert.Equal(AutomationControlType.Button, r.Type);   // peer 있는 형(ToggleButton)
        Assert.Equal("도움말 · 제목 Test.Tip.Id", r.Name);
        });
    }

    [Fact]
    public void should_open_a_real_callout_whose_body_carries_help_key_body_and_reads_as_plain_text()
    {
        Sta(() =>
        {
        HelpCatalog.Register(new[] { HelpEntry.Create("Test.Tip.Body", "펜스 보기", "{Shift}+끌기 = 망 선택") });
        var r = OnWindow(new StackPanel { Children = { new HelpTip { HelpKey = "Test.Tip.Body" }, AliasTip("Test.Tip.Body", null, "Legacy.HelpText") } }, (window, root) =>
        {
            var tips = ((StackPanel)root).Children.OfType<HelpTip>().ToList();
            tips[0].Open();
            Pump();
            var popup = tips[0].CalloutPopup!;
            var body = tips[0].Callout!;
            var name = UIElementAutomationPeer.CreatePeerForElement(body).GetName();
            var first = (popup.IsOpen, Id: AutomationProperties.GetAutomationId(body), name, Width: body.ActualWidth);
            tips[1].Open();                                         // 하나만 — 앞의 것이 닫힌다
            Pump();
            return (first, FirstClosed: !popup.IsOpen, AliasId: AutomationProperties.GetAutomationId(tips[1].Callout!));
        }, useDefaultTemplate: true);

        Assert.True(r.first.IsOpen);
        Assert.Equal("Help.Test.Tip.Body.Body", r.first.Id);
        Assert.Contains("Shift+끌기", r.first.name);
        Assert.True(r.first.Width <= HelpCallout.MaxCardWidth + 2 * HelpCalloutPlacement.TailMargin);
        Assert.True(r.FirstClosed);
        Assert.Equal("Legacy.HelpText", r.AliasId);
        });
    }
    #endregion

    #region - F1 -
    [Fact]
    public void should_open_the_focused_sections_question_mark_when_f1_is_pressed()
    {
        Sta(() =>
        {
        Register("Test.Tip.F1.Window", "Test.Tip.F1.A", "Test.Tip.F1.B");
        var inA = new TextBox();
        var inB = new TextBox();
        var outside = new TextBox();
        var sectionA = Section("Test.Tip.F1.A", inA);
        var sectionB = Section("Test.Tip.F1.B", inB);
        var root = new StackPanel();
        var windowTip = new HelpTip { HelpKey = "Test.Tip.F1.Window", Scope = root };
        root.Children.Add(windowTip);
        root.Children.Add(sectionA);
        root.Children.Add(sectionB);
        root.Children.Add(outside);
        HelpTip.SetHandlesF1(root, true);

        var r = OnWindow(root, (window, _) =>
        {
            ApplicationCommands.Help.Execute(null, inB);
            var b = Opened(sectionB);
            ApplicationCommands.Help.Execute(null, outside);       // 섹션 밖 → 창 전체 "?"
            var w = windowTip.IsChecked == true;
            var source = PresentationSource.FromVisual(window)!;
            inA.Focus();
            inA.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.F1) { RoutedEvent = Keyboard.KeyDownEvent });
            return (b, w, A: Opened(sectionA), WindowClosed: windowTip.IsChecked == false);
        });

        Assert.True(r.b);
        Assert.True(r.w);
        Assert.True(r.A);                // 진짜 F1 키(기본 제스처) — 한 번에 하나라 창 "?" 는 닫혔다
        Assert.True(r.WindowClosed);
        });
    }

    [Fact]
    public void should_skip_a_section_whose_key_is_missing_and_open_the_next_scope_up_when_f1_is_pressed()
    {
        Sta(() =>
        {
        Register("Test.Tip.F1Skip.Window");
        var inner = new TextBox();
        var root = new StackPanel();
        var windowTip = new HelpTip { HelpKey = "Test.Tip.F1Skip.Window", Scope = root };
        root.Children.Add(windowTip);
        root.Children.Add(Section("Test.Tip.F1Skip.Missing", inner));
        HelpTip.SetHandlesF1(root, true);

        var opened = OnWindow(root, (_, _) =>
        {
            ApplicationCommands.Help.Execute(null, inner);
            return windowTip.IsChecked == true;
        });

        Assert.True(opened);
        });
    }
    #endregion

    #region - Keyboard fallbacks (FR-06) -
    [Fact]
    public void should_add_drag_surface_keyboard_fallbacks_under_the_section_as_a_keyboard_group()
    {
        Sta(() =>
        {
        Register("Test.Tip.Fallback.Outer", "Test.Tip.Fallback.Inner");
        var list = DragList("{Alt}+{↑}/{↓} 순서");
        var inner = Section("Test.Tip.Fallback.Inner", DragList("안쪽 목록 — 제 섹션 몫"));
        var outer = Section("Test.Tip.Fallback.Outer", new StackPanel { Children = { list, DragList("{Alt}+{↑}/{↓} 순서"), inner } });

        var r = OnWindow(outer, (_, _) =>
        {
            var tip = TipOf(outer);
            tip.Open();
            return tip.Callout?.Entry ?? HelpKeyboardFallbacks.AppendTo(tip.Entry!, outer);
        });

        var keyboard = Assert.Single(r.Sections, s => s.Heading == "키보드로");
        Assert.Equal(new[] { "{Alt}+{↑}/{↓} 순서" }, keyboard.Items);       // 한 번만 · 안쪽 섹션 것은 빠진다
        });
    }

    [Fact]
    public void should_collect_nothing_when_the_drag_surface_is_hidden()
    {
        Sta(() =>
        {
        var hidden = DragList("숨은 목록");
        hidden.Visibility = Visibility.Collapsed;
        var panel = new StackPanel { Children = { hidden } };

        var found = OnWindow(panel, (_, _) => HelpKeyboardFallbacks.Collect(panel));

        Assert.Empty(found);
        });
    }
    #endregion

    #region - Kernel slots (FR-04 · FR-05) -
    [Fact]
    public void should_put_a_question_mark_right_of_the_section_title_only_when_the_section_has_a_key()
    {
        Sta(() =>
        {
        Register("Test.Tip.Slot.Section");
        var withKey = new ConsoleSection { Header = "값 편집", HelpKey = "Test.Tip.Slot.Section", Content = new TextBlock() };
        var withoutKey = new ConsoleSection { Header = "값 편집", Content = new TextBlock() };
        var r = OnWindow(new StackPanel { Children = { withKey, withoutKey } }, (_, _) =>
        {
            var tip = TipOf(withKey);
            var header = Descendants<ContentPresenter>(withKey).First(p => p.ContentSource == "Header");
            return (Key: tip.HelpKey, Scope: tip.Scope, tip.IsEnabled, RightOfTitle: tip.TranslatePoint(new Point(), withKey).X >= header.TranslatePoint(new Point(header.ActualWidth, 0), withKey).X,
                Hidden: TipOf(withoutKey).Visibility);
        }, useDefaultTemplate: true);

        Assert.Equal("Test.Tip.Slot.Section", r.Key);
        Assert.IsType<ConsoleSection>(r.Scope);
        Assert.True(r.IsEnabled);
        Assert.True(r.RightOfTitle);
        Assert.Equal(Visibility.Collapsed, r.Hidden);
        });
    }

    [Fact]
    public void should_put_a_question_mark_in_the_toolbar_action_group_and_in_the_shell_header_when_keys_are_given()
    {
        Sta(() =>
        {
        Register("Test.Tip.Slot.Toolbar", "Test.Tip.Slot.Shell");
        var toolbar = new ConsoleToolbar { ConsoleKey = "T", HelpKey = "Test.Tip.Slot.Toolbar", Width = 900 };
        var shell = new ConsoleShell { ConsoleKey = "S", Title = "콘솔", HelpKey = "Test.Tip.Slot.Shell", Width = 1200, Height = 400, Toolbar = toolbar };

        var r = OnWindow(shell, (_, _) =>
        {
            var tips = Descendants<HelpTip>(shell).ToList();
            return (Keys: tips.Select(t => t.HelpKey).OrderBy(k => k).ToArray(),
                ToolbarScope: tips.Single(t => t.HelpKey == "Test.Tip.Slot.Toolbar").Scope,
                ShellScope: tips.Single(t => t.HelpKey == "Test.Tip.Slot.Shell").Scope,
                ShellF1: HelpTip.GetHandlesF1(shell),
                ToolbarTipVisible: tips.Single(t => t.HelpKey == "Test.Tip.Slot.Toolbar").IsVisible);
        }, useDefaultTemplate: true, width: 1200, height: 400);

        Assert.Equal(new[] { "Test.Tip.Slot.Shell", "Test.Tip.Slot.Toolbar" }, r.Keys);
        Assert.IsType<ConsoleToolbar>(r.ToolbarScope);
        Assert.IsType<ConsoleShell>(r.ShellScope);
        Assert.True(r.ShellF1);            // 콘솔 셸은 F1 을 기본으로 받는다
        Assert.True(r.ToolbarTipVisible);
        });
    }

    [Fact]
    public void should_keep_the_lock_reason_on_screen_and_still_show_a_legacy_note()
    {
        Sta(() =>
        {
        var field = new ConsoleField { Header = "번호", Content = new TextBlock(), LockReason = "생성 시 확정", Note = "옛 주석" };
        var bare = new ConsoleField { Header = "이름", Content = new TextBlock() };
        var r = OnWindow(new StackPanel { Children = { field, bare } }, (_, _) =>
        {
            string[] Visible(DependencyObject root) => Descendants<TextBlock>(root).Where(t => t.IsVisible && t.Text.Length > 0).Select(t => KoreanWordWrap.Strip(t.Text)).ToArray();   // 다른 시험이 전역 한글 줄바꿈을 설치해 둘 수 있다
            return (Field: Visible(field), Bare: Visible(bare));
        }, useDefaultTemplate: true);

        Assert.Contains("생성 시 확정", r.Field);
        Assert.Contains("옛 주석", r.Field);
        Assert.True(Array.IndexOf(r.Field, "생성 시 확정") < Array.IndexOf(r.Field, "옛 주석"));   // 사유가 위
        Assert.DoesNotContain(r.Bare, t => t == "생성 시 확정");
        });
    }

    [Theory]
    [InlineData(ConsoleHintKind.Action, true)]
    [InlineData(ConsoleHintKind.Failure, true)]
    [InlineData(ConsoleHintKind.Permission, true)]
    [InlineData(ConsoleHintKind.Search, true)]
    [InlineData(ConsoleHintKind.Usage, false)]
    public void should_show_the_empty_state_hint_inline_unless_it_is_usage(ConsoleHintKind kind, bool visible)
    {
        Sta(() =>
        {
        var empty = new ConsoleEmptyState { Title = "없음", Hint = "힌트 글", HintKind = kind, Width = 400, Height = 200 };

        var shown = OnWindow(empty, (_, _) => Descendants<TextBlock>(empty).Single(t => KoreanWordWrap.Strip(t.Text) == "힌트 글").IsVisible, useDefaultTemplate: true);

        Assert.Equal(visible, shown);
        });
    }

    [Fact]
    public void should_default_the_empty_state_hint_to_an_inline_action()
        => Assert.Equal(ConsoleHintKind.Action, (ConsoleHintKind)ConsoleEmptyState.HintKindProperty.DefaultMetadata.DefaultValue);

    [Fact]
    public void should_put_a_question_mark_in_the_dialog_header_and_let_escape_close_only_the_callout_while_it_is_open()
    {
        Sta(() =>
        {
        Register("Test.Tip.Slot.Dialog");
        var frame = new ConsoleDialogFrame { Title = "대화창", HelpKey = "Test.Tip.Slot.Dialog", Content = new TextBox() };
        var cancels = 0;
        frame.SecondaryInvoked += (_, _) => cancels++;

        var r = OnWindow(frame, (window, _) =>
        {
            var tip = TipOf(frame);
            tip.Open();
            EscapeAt(window, frame);                          // 열려 있으면 말풍선만 닫힌다 — 대화창 취소가 아니다
            var afterOpenEsc = (tip.IsChecked, cancels);
            EscapeAt(window, frame);                          // 닫혀 있으면 대화창의 Esc(취소) 그대로
            return (Scope: tip.Scope, afterOpenEsc, cancels);
        }, useDefaultTemplate: false);

        Assert.IsType<ConsoleDialogFrame>(r.Scope);
        Assert.Equal((false, 0), r.afterOpenEsc);
        Assert.Equal(1, r.cancels);
        });
    }

    [Fact]
    public void should_open_the_dialog_question_mark_when_f1_is_pressed_inside_a_dialog_frame()
    {
        Sta(() =>
        {
        Register("Test.Tip.F1.Dialog");
        var box = new TextBox();
        var frame = new ConsoleDialogFrame { Title = "대화창", HelpKey = "Test.Tip.F1.Dialog", Content = box };
        var r = OnWindow(frame, (window, _) =>
        {
            ApplicationCommands.Help.Execute(null, box);       // r24: 대화창 틀이 F1 을 받지 않아 아무 일도 없었다
            return (Handles: HelpTip.GetHandlesF1(frame), Opened: TipOf(frame).IsChecked == true);
        });
        Assert.True(r.Handles);
        Assert.True(r.Opened);
        });
    }

    [Fact]
    public void should_handle_f1_at_the_window_root_when_console_chrome_is_applied()
    {
        Sta(() =>
        {
            var window = new Window();
            ConsoleWindowChrome.Apply(window);                  // r24: 결선 · 조립기 창(UserControl 뿌리)이 F1 을 받을 뿌리가 없었다
            Assert.True(HelpTip.GetHandlesF1(window));
            window.Close();
        });
    }
    #endregion

    #region - Theme -
    [Fact]
    public void should_repaint_the_question_mark_and_the_callout_when_the_theme_tokens_change()
    {
        Sta(() =>
        {
        Register("Test.Tip.Theme");
        var tip = new HelpTip { HelpKey = "Test.Tip.Theme" };
        var callout = new HelpCallout { Entry = HelpEntry.Create("Test.Tip.Theme.Body", "제목", "{Ctrl} 여러 줄") };
        var root = new StackPanel { Children = { tip, callout } };
        root.Resources["PrimaryBrush"] = new SolidColorBrush(Colors.Red);
        root.Resources["SurfaceAltBrush"] = new SolidColorBrush(Colors.Red);
        root.Resources["SurfaceSunkenBrush"] = new SolidColorBrush(Colors.Red);

        var r = OnWindow(root, (_, _) =>
        {
            Color Ring() => ((SolidColorBrush)Descendants<System.Windows.Shapes.Ellipse>(tip).Single(e => e.StrokeThickness > 1 && e.Visibility == Visibility.Visible).Stroke).Color;
            Color Chip() => ((SolidColorBrush)Descendants<Border>(callout).Single(b => b.Child is TextBlock { Text: "Ctrl" }).Background).Color;
            var before = (Ring(), ((SolidColorBrush)callout.Background).Color, Chip());
            root.Resources["PrimaryBrush"] = new SolidColorBrush(Colors.Blue);         // 테마 교체 = 사전 교체
            root.Resources["SurfaceAltBrush"] = new SolidColorBrush(Colors.Blue);
            root.Resources["SurfaceSunkenBrush"] = new SolidColorBrush(Colors.Blue);
            Pump();
            return (before, after: (Ring(), ((SolidColorBrush)callout.Background).Color, Chip()));
        }, useDefaultTemplate: true);

        Assert.Equal((Colors.Red, Colors.Red, Colors.Red), r.before);
        Assert.Equal((Colors.Blue, Colors.Blue, Colors.Blue), r.after);   // 한 번 풀어 캐싱하지 않는다
        });
    }
    #endregion

    #region - Helpers -
    private static HelpTip AliasTip(string key, string? id, string? bodyId = null)
    {
        var tip = new HelpTip { HelpKey = key };
        if (id is not null) AutomationProperties.SetAutomationId(tip, id);
        if (bodyId is not null) tip.BodyAutomationId = bodyId;
        return tip;
    }

    private static ConsoleSection Section(string key, object content) => new() { Header = key, HelpKey = key, Content = content };

    private static ListBox DragList(string fallback)
    {
        var list = new ListBox { ItemsSource = new[] { "a" }, Height = 30 };
        Interaction.GetBehaviors(list).Add(new CaptureDragBehavior { KeyboardFallback = fallback });
        return list;
    }

    private static HelpTip TipOf(DependencyObject root) => Descendants<HelpTip>(root).First();

    private static bool Opened(ConsoleSection section) => TipOf(section).IsChecked == true;

    private static bool Escape(Window window)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        window.RaiseEvent(args);
        return args.Handled;
    }

    private static void EscapeAt(Window window, UIElement target)
        => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });

    private static void PreviewDown(UIElement element)
        => element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent });

    private static T OnWindow<T, TRoot>(TRoot root, Func<Window, TRoot, T> body, bool useDefaultTemplate = false, bool focusTip = false, double width = 700, double height = 400)
        where TRoot : UIElement
    {
            var window = new Window
            {
                Content = root,
                Width = width, Height = height, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000,
            };
            if (!useDefaultTemplate) window.Resources.Add(typeof(HelpTip), XamlReader.Parse(NoPopupStyle));
            window.Show();
            try
            {
                window.Activate();
                window.UpdateLayout();
                Pump();
                if (focusTip && root is HelpTip tip) tip.Focus();
                return body(window, root);
            }
            finally
            {
                HelpTip.CloseCurrent();
                window.Close();
            }
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deep in Descendants<T>(child)) yield return deep;
        }
    }

    /// <summary>시험 몸 전체를 STA 스레드에서 — WPF 요소는 만든 스레드에서만 쓴다. 단언 실패는 안쪽 예외로 올라온다.</summary>
    private static void Sta(Action body)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) throw new InvalidOperationException("STA body failed", error);
        return result;
    }
    #endregion
}
