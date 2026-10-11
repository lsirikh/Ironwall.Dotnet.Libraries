using Ironwall.Dotnet.Libraries.Reports.Ui.Help;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.5(AUX) 중 W7 몫의 보고서 · 조치보고 문구 쪽 —
               AUX-023(말풍선 규칙: Esc · 바깥 · 하나씩 · F1) · AUX-025(한 번도 안 연 키: 글 · Esc).
   Notes     : 창을 띄우지 않는다. 입력 원천은 HWND 없는 가짜 PresentationSource. 말풍선 창(Popup)은 HWND 를 만들므로
               "?" 에는 팝업 없는 템플릿(말풍선 몸 PART_Callout 만)을 물려 몸에 그려지는 글까지 본다.
****************************************************************************/
public class ShellReportsLeftoverScenarioTests
{
    #region - 창 없는 "?" 도우미 -
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

    private static string[] BodyLines(HelpCallout callout)
    {
        var body = (StackPanel)callout.Template.FindName("PART_Body", callout);
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

    // ── AUX-023: 말풍선 규칙(보고서 · 문구) ─────────────────────────────────────────────────────
    [Trait("Scenario", "AUX-023")]
    [Fact]
    public void should_follow_the_callout_rules_escape_outside_click_one_at_a_time_and_f1_on_the_report_and_phrase_question_marks()
    {
        _ = ReportsHelp.Entries;
        var r = OnSta(() =>
        {
            // 보고서 콘솔 셸(F1) → 머리 "?" + 심각도 절(칩) + 구성 절(목록) + 제목 칸(절 밖) / 문구 콘솔 셸(F1) → 머리 "?" + 문구 칸
            var root = new StackPanel();
            var reports = new Border();
            HelpTip.SetHandlesF1(reports, true);
            var reportsBody = new StackPanel();
            reports.Child = reportsBody;
            var reportsTip = new HelpTip { HelpKey = "Reports.Console", Scope = reportsBody };
            var severity = new StackPanel();
            var severityTip = new HelpTip { HelpKey = "Reports.Create.Severity", Scope = severity };
            var chip = new Button { Content = "주의" };
            severity.Children.Add(severityTip);
            severity.Children.Add(chip);
            var components = new StackPanel();
            var componentsTip = new HelpTip { HelpKey = "Reports.Template.Components", Scope = components };
            var componentList = new ListBox();
            components.Children.Add(componentsTip);
            components.Children.Add(componentList);
            var titleBox = new TextBox();
            reportsBody.Children.Add(reportsTip);
            reportsBody.Children.Add(titleBox);
            reportsBody.Children.Add(severity);
            reportsBody.Children.Add(components);

            var phrases = new Border();
            HelpTip.SetHandlesF1(phrases, true);
            var phrasesBody = new StackPanel();
            phrases.Child = phrasesBody;
            var phraseTip = new HelpTip { HelpKey = "Reports.Phrase", Scope = phrasesBody };
            var phraseBox = new TextBox();
            phrasesBody.Children.Add(phraseTip);
            phrasesBody.Children.Add(phraseBox);

            var blank = new Border { Width = 300, Height = 40, Background = Brushes.Transparent };
            root.Children.Add(reports);
            root.Children.Add(phrases);
            root.Children.Add(blank);
            var tips = new[] { reportsTip, severityTip, componentsTip, phraseTip };

            var source = new HeadlessSource { RootVisual = root };
            try
            {
                foreach (var tip in tips) RaiseLoaded(tip);

                var oneAtATime = new List<string>();
                foreach (var tip in tips)
                {
                    tip.Open();
                    oneAtATime.Add(string.Join(",", tips.Where(t => t.IsChecked == true).Select(t => t.HelpKey)));
                }

                PressMouse(phraseTip);                                            // "?" 자신 — 열린 채
                var afterSelf = phraseTip.IsChecked == true;
                PressMouse(blank);                                                // 바깥 — 닫힘
                var afterOutside = phraseTip.IsChecked != true && HelpTip.Current is null;
                componentsTip.Open();
                PressMouse(chip);                                                 // 다른 절의 칩도 바깥이다
                var afterOtherSection = componentsTip.IsChecked != true;

                severityTip.Open();
                var escOpen = PressEscape(source, severityTip);
                var escClosedIt = severityTip.IsChecked != true && HelpTip.Current is null;
                var escPasses = !PressEscape(source, severityTip);

                ApplicationCommands.Help.Execute(null, chip);
                var f1Chip = HelpTip.Current?.HelpKey;
                ApplicationCommands.Help.Execute(null, componentList);
                var f1List = HelpTip.Current?.HelpKey;
                ApplicationCommands.Help.Execute(null, titleBox);
                var f1Outside = HelpTip.Current?.HelpKey;
                ApplicationCommands.Help.Execute(null, phraseBox);
                var f1Phrase = HelpTip.Current?.HelpKey;
                var onlyPhraseOpen = tips.Count(t => t.IsChecked == true) == 1 && phraseTip.IsChecked == true;
                var escAfterF1 = PressEscape(source, phraseBox) && HelpTip.Current is null;

                return (oneAtATime, afterSelf, afterOutside, afterOtherSection, escOpen, escClosedIt, escPasses,
                        f1Chip, f1List, f1Outside, f1Phrase, onlyPhraseOpen, escAfterF1);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal(new[] { "Reports.Console", "Reports.Create.Severity", "Reports.Template.Components", "Reports.Phrase" }, r.oneAtATime);
        Assert.True(r.afterSelf);
        Assert.True(r.afterOutside);
        Assert.True(r.afterOtherSection);
        Assert.True(r.escOpen);
        Assert.True(r.escClosedIt);
        Assert.True(r.escPasses);
        Assert.Equal("Reports.Create.Severity", r.f1Chip);
        Assert.Equal("Reports.Template.Components", r.f1List);
        Assert.Equal("Reports.Console", r.f1Outside);
        Assert.Equal("Reports.Phrase", r.f1Phrase);                                 // 문구 콘솔 안에서는 그 머리 "?"
        Assert.True(r.onlyPhraseOpen);
        Assert.True(r.escAfterF1);
    }

    // ── AUX-025: 한 번도 안 연 키 — 글 · Esc ────────────────────────────────────────────────────
    public static IEnumerable<object[]> ReportKeyRows() => ReportsHelp.Entries.Select(e => new object[] { e.Key });

    [Trait("Scenario", "AUX-025")]
    [Theory]
    [MemberData(nameof(ReportKeyRows))]
    public void should_draw_the_whole_callout_text_with_its_body_id_and_close_it_with_escape_for_every_report_key(string key)
    {
        _ = ReportsHelp.Entries;
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
                return (id, lines, name, height, esc, Closed: tip.IsChecked != true && HelpTip.Current is null);
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Equal($"Help.{key}.Body", r.id);
        Assert.Equal("? " + expected[0], r.lines[0]);
        Assert.Equal(expected.Skip(1), r.lines.Skip(1));
        Assert.Contains(entry.Title, r.name);
        Assert.All(entry.Sections.SelectMany(s => s.Items), item => Assert.Contains(HelpText.Plain(item), KoreanWordWrap.Strip(r.name)));
        Assert.True(r.height > 0, "말풍선 몸이 높이를 갖지 않는다 — 글이 그려지지 않았다");
        Assert.True(r.esc);
        Assert.True(r.Closed);
    }
}
