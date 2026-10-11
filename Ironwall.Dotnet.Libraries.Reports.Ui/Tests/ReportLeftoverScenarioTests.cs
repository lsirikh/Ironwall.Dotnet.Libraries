using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Ironwall.Dotnet.Libraries.Reports.Ui.Help;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Microsoft.Xaml.Behaviors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.4(RPT) 중 앞 작업이 헤디드로 남긴 줄(RPT-009 · 010 · 017 · 019 · 020 · 024 · 028)과
               범위 밖으로 둔 RPT-025 를 다시 보고, 창 없이 단언할 수 있는 부분만 시험한다.
   Notes     : 창을 띄우지 않는다(Window.Show · ShowDialog · 화면 밖 창 금지). WPF 요소가 필요한 시험은 STA 스레드에서
               요소만 만들고, 입력 원천이 필요하면 HWND 없는 가짜 PresentationSource(<see cref="HeadlessSource"/>)를 쓴다.
               실제 별도 창 · WebView2 줌 · 실마우스 끌기 · Esc 키로 끌기 취소 · 실렌더 레이아웃은 헤디드 몫이다.
****************************************************************************/
[Collection("ReportsCaliburnIoC")]
public class ReportLeftoverScenarioTests : IDisposable
{
    private readonly ReportIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string ViewText(string fileName)
        => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Reports.Ui", "Views", "Panels", fileName));

    #region - 세우기 -
    private static async Task<(ReportConsoleViewModel Console, FakeReportApiService Api)> OpenReportsAsync()
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeReportApiService();
        api.Generations.Add(ReportSeed.Generation(3, "9월 정기 보고서"));
        api.Generations.Add(ReportSeed.Generation(2, "장애 요약", "GENERATING", 40));
        api.Templates.Add(ReportSeed.Template(11, "주간 요약", "a", "b", "c"));
        api.Components.AddRange(ReportSeed.Catalog("a", "b", "c", "d"));

        var console = new ReportConsoleViewModel(
            events, log, new FakePermissionService(),
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
        console.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        await ((IActivate)console).ActivateAsync();
        return (console, api);
    }

    /// <summary>템플릿 레일에서 '주간 요약'(구성 a · b · c, 카탈로그 a~d)을 고른 상태.</summary>
    private static async Task<(ReportConsoleViewModel Console, FakeReportApiService Api)> OpenTemplateAsync()
    {
        var (console, api) = await OpenReportsAsync();
        await console.SelectRailAsync(ReportConsoleRails.Template);
        console.OnRowSelected(console.TemplateViewModel.Rows.First(t => t.Id == 11));
        return (console, api);
    }

    private static async Task<(ActionReportTemplateConsoleViewModel Console, FakeActionReportTemplateApiService Api)> OpenPhrasesAsync()
    {
        var events = new EventAggregator();
        var api = new FakeActionReportTemplateApiService();
        api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
        api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
        api.Templates.Add(ActionReportTemplateSeed.Template(3, "오경보", 2));
        var console = new ActionReportTemplateConsoleViewModel(events, new FakeLogService(), new FakePermissionService(), api);
        await ((IActivate)console).ActivateAsync();
        return (console, api);
    }
    #endregion

    #region - STA · 끌기 경로 도우미 -
    /// <summary>본문을 STA 스레드에서 돌린다(WPF 요소는 만들기만 하고 창은 띄우지 않는다). 가짜 API 는 모두 동기로 끝난다.</summary>
    private static void OnSta(Func<Task> body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { body().GetAwaiter().GetResult(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw new Exception($"STA 본문 실패: {failure.Message}", failure);
    }

    private const double RowHeight = 28;
    private static readonly Rect[] RowRects = Enumerable.Range(0, 8).Select(i => new Rect(0, i * RowHeight, 320, RowHeight)).ToArray();

    /// <summary>
    /// 마우스를 <paramref name="row"/> 번째 행의 위쪽 반(또는 아래쪽 반)에서 놓았을 때의 삽입 자리 — 커널 끌기
    /// (<c>CaptureDragBehavior</c>)가 행 실측 rect 로 재는 것과 같은 순수 함수(<see cref="DragMath.InsertionIndex"/>)다.
    /// </summary>
    private static int MouseInsertionAt(int row, bool upperHalf)
        => DragMath.InsertionIndex(RowRects, row * RowHeight + (upperHalf ? 4 : RowHeight - 4));

    /// <summary>Alt+↑(−1) · Alt+↓(+1)이 고른 행을 옮길 때 마우스로 같은 결과를 내는 놓기 자리.</summary>
    private static int MouseInsertionLikeAltArrow(int index, int direction)
        => direction < 0 ? MouseInsertionAt(index - 1, upperHalf: true) : MouseInsertionAt(index + 1, upperHalf: false);

    /// <summary>
    /// 마우스를 놓았다 — 커널 <c>CaptureDragBehavior.FinishDrag</c> 와 같은 순서: 받을 수 있을 때만 Drop, 아니면 거절(목록은 그대로).
    /// </summary>
    private static bool ReleaseMouse(IDragDropHandler handler, object item, DropTarget target)
    {
        var payload = new DragPayload(null!, new List<object> { item }, item.ToString() ?? string.Empty);
        var canDrop = handler.CanDrop(payload, target);
        if (canDrop) handler.Drop(payload, target);
        return canDrop;
    }

    /// <summary>
    /// 진짜 키보드 폴백(<see cref="ReorderKeyboardBehavior"/>)을 목록(창에 올리지 않은 ListBox)에 붙이고 Alt+↑/↓ 한 번을 누른 것과 같은
    /// <see cref="ReorderKeyboardBehavior.MoveSelection"/> 을 부른다. 끝나면 목록을 떼어 다른 스레드의 변경이 이 목록에 닿지 않게 한다.
    /// </summary>
    private static void PressAltArrow(IDragDropHandler handler, string zoneKey, IEnumerable items, object selected, int direction)
    {
        var list = new ListBox { ItemsSource = items };
        DropZone.SetKey(list, zoneKey);
        var keyboard = new ReorderKeyboardBehavior { Handler = handler };
        var behaviors = Interaction.GetBehaviors(list);
        behaviors.Add(keyboard);
        list.SelectedItem = selected;

        Assert.True(keyboard.MoveSelection(direction), "Alt+화살표 경로가 행을 옮기지 않았다");

        behaviors.Remove(keyboard);
        list.ItemsSource = null;
    }

    private static string[] ComponentOrder(ReportConsoleViewModel console)
        => console.EditViewModel.Board.Items.Select(i => i.Id).ToArray();

    private static string[] SentComponents(FakeReportApiService api)
        => api.LastUpdate?.Components?.Select(c => $"{c.Id}:{c.Order}").ToArray() ?? Array.Empty<string>();

    private static string[] SentReorder(FakeActionReportTemplateApiService api)
        => api.ReorderCalls.Single().Select(r => $"{r.Id}:{r.DisplayOrder}").ToArray();
    #endregion

    // ── RPT-009: 큰 창 — 열기 요청 · 「큰 창에서 보고 있습니다」 · 닫으면 되돌아옴 ─────────────────────
    // 부분: 별도 최상위 창(Reports.LargePreview.Window) 자체는 뷰 코드비하인드가 만든다 — 실창은 헤디드.
    [Trait("Scenario", "RPT-009")]
    [Fact]
    public async Task should_ask_for_the_large_window_with_the_html_and_say_the_preview_is_shown_there_until_it_closes()
    {
        var (console, _) = await OpenReportsAsync();
        console.OnRowSelected(console.ListViewModel.Rows.First(r => r.Id == 3));
        var preview = console.PreviewViewModel;
        Assert.True(preview.IsSurfaceLive);
        Assert.True(preview.CanOpenLargeView);
        ReportPreviewViewModel? asked = null;
        console.LargePreviewRequested += p => asked = p;

        console.OpenLargePreview();

        Assert.Same(preview, asked);                                         // 창 여는 쪽(뷰)에 미리보기를 넘긴다
        Assert.Equal("<html><body>미리보기</body></html>", asked!.Html);       // 큰 창이 그릴 HTML 이 실려 있다
        Assert.Equal("9월 정기 보고서", asked.Row!.Title);                     // 창 제목 재료
        Assert.False(preview.IsSurfaceLive);                                 // 상세 칸은 살아 있는 미리보기를 내린다
        Assert.Equal("큰 창에서 보고 있습니다", preview.SurfaceReason);
        Assert.Equal("그 창을 닫으면 미리보기가 이 자리로 돌아옵니다", preview.SurfaceHint);
        Assert.False(preview.CanOpenLargeView);                              // 열려 있는 동안 [크게 보기] 는 꺼진다

        console.OnLargePreviewClosed();                                      // 큰 창 ✕

        Assert.True(preview.IsSurfaceLive);
        Assert.True(preview.CanOpenLargeView);
    }

    // ── RPT-017: 템플릿 구성 끌어 순서 = Alt+화살표와 같음 ─────────────────────────────────────────
    // 부분: 마우스 경로는 커널과 같은 삽입 자리 계산(DragMath) + 같은 담당(Drop)까지. 실마우스 좌표 · Esc 키는 헤디드.
    [Trait("Scenario", "RPT-017")]
    [Theory]
    [InlineData(1, -1)]   // b 를 위로
    [InlineData(0, +1)]   // a 를 아래로
    [InlineData(2, -1)]   // c 를 위로
    public void should_reorder_template_components_by_mouse_exactly_like_alt_arrow(int index, int direction)
    {
        OnSta(async () =>
        {
            // Alt+↑/↓ — 진짜 ReorderKeyboardBehavior
            var (byKey, byKeyApi) = await OpenTemplateAsync();
            var original = ComponentOrder(byKey);
            PressAltArrow(byKey.EditViewModel.Drop, TemplateComponentDropHandler.ZoneKey, byKey.EditViewModel.Board.Items,
                          byKey.EditViewModel.Board.Items[index], direction);

            // 마우스로 끌어 같은 자리에 놓기
            var (byMouse, byMouseApi) = await OpenTemplateAsync();
            var target = new DropTarget(TemplateComponentDropHandler.ZoneKey, null, MouseInsertionLikeAltArrow(index, direction));
            Assert.True(ReleaseMouse(byMouse.EditViewModel.Drop, byMouse.EditViewModel.Board.Items[index], target));

            Assert.NotEqual(original, ComponentOrder(byKey));
            Assert.Equal(ComponentOrder(byKey), ComponentOrder(byMouse));
            Assert.True(byKey.Detail.IsDirty);                               // 서버는 아직 부르지 않는다 — 미적용
            Assert.True(byMouse.Detail.IsDirty);
            Assert.Null(byMouseApi.LastUpdate);

            await byKey.ApplyAsync();
            await byMouse.ApplyAsync();

            Assert.NotEmpty(SentComponents(byKeyApi));
            Assert.Equal(SentComponents(byKeyApi), SentComponents(byMouseApi)); // [적용] PATCH 도 한 글자 다르지 않다
        });
    }

    // 부분: "놓기 취소"의 라이브러리 몫 — 다른 드롭존 · 제자리에 놓으면 순서 · 미적용 상태 그대로. Esc 키 자체는 커널(헤디드).
    [Trait("Scenario", "RPT-017")]
    [Fact]
    public void should_keep_the_component_order_and_stay_clean_when_the_drag_is_released_outside_or_back_in_place()
    {
        OnSta(async () =>
        {
            var (console, api) = await OpenTemplateAsync();
            var original = ComponentOrder(console);
            var item = console.EditViewModel.Board.Items[1];

            var outside = ReleaseMouse(console.EditViewModel.Drop, item, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, 0));
            var notReorder = ReleaseMouse(console.EditViewModel.Drop, item, new DropTarget(TemplateComponentDropHandler.ZoneKey, null, -1));
            ReleaseMouse(console.EditViewModel.Drop, item, new DropTarget(TemplateComponentDropHandler.ZoneKey, null, MouseInsertionAt(1, upperHalf: true)));   // 제자리(위 반)
            ReleaseMouse(console.EditViewModel.Drop, item, new DropTarget(TemplateComponentDropHandler.ZoneKey, null, MouseInsertionAt(1, upperHalf: false)));  // 제자리(아래 반)

            Assert.False(outside);
            Assert.False(notReorder);
            Assert.Equal(original, ComponentOrder(console));
            Assert.False(console.Detail.IsDirty);

            await console.ApplyAsync();
            Assert.Null(api.LastUpdate?.Components);                         // 보낼 순서가 없다
        });
    }

    // ── RPT-024: 조치보고 문구 끌기 = Alt+화살표 = ▲▼ ─────────────────────────────────────────────
    // 부분: 마우스 경로는 커널과 같은 삽입 자리 계산 + 같은 담당까지. 실마우스 좌표 · Esc 키는 헤디드.
    [Trait("Scenario", "RPT-024")]
    [Theory]
    [InlineData(2, -1)]
    [InlineData(0, +1)]
    [InlineData(1, -1)]
    public void should_reorder_phrases_by_mouse_exactly_like_alt_arrow_and_the_arrow_buttons(int index, int direction)
    {
        OnSta(async () =>
        {
            var (byKey, byKeyApi) = await OpenPhrasesAsync();
            var original = byKey.Board.OrderedIds.ToArray();
            PressAltArrow(byKey.Drop, ActionReportTemplateDropHandler.ZoneKey, byKey.Items, byKey.Items[index], direction);

            var (byMouse, byMouseApi) = await OpenPhrasesAsync();
            var target = new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, MouseInsertionLikeAltArrow(index, direction));
            Assert.True(ReleaseMouse(byMouse.Drop, byMouse.Items[index], target));

            var (byButton, byButtonApi) = await OpenPhrasesAsync();
            byButton.MoveSelected(byButton.Items[index], direction);

            Assert.NotEqual(original, byKey.Board.OrderedIds.ToArray());
            Assert.Equal(byKey.Board.OrderedIds.ToArray(), byMouse.Board.OrderedIds.ToArray());
            Assert.Equal(byKey.Board.OrderedIds.ToArray(), byButton.Board.OrderedIds.ToArray());
            Assert.Equal(SentReorder(byKeyApi), SentReorder(byMouseApi));   // 즉시 저장 — 단일 reorder 호출 · 같은 본문
            Assert.Equal(SentReorder(byKeyApi), SentReorder(byButtonApi));
            Assert.True(byMouse.CanUndoReorder);                             // 되돌리기도 같은 조건으로 켜진다
            Assert.True(byKey.CanUndoReorder);
        });
    }

    // 부분: 다른 드롭존 · 순서 드롭 아님 · 제자리에 놓으면 순서 그대로 · 서버 호출 없음. Esc 키 자체는 커널(헤디드).
    [Trait("Scenario", "RPT-024")]
    [Fact]
    public void should_keep_the_phrase_order_and_send_nothing_when_the_drag_is_released_outside_or_back_in_place()
    {
        OnSta(async () =>
        {
            var (console, api) = await OpenPhrasesAsync();
            var original = console.Board.OrderedIds.ToArray();
            var item = console.Items[1];

            var outside = ReleaseMouse(console.Drop, item, new DropTarget(TemplateComponentDropHandler.ZoneKey, null, 0));
            var notReorder = ReleaseMouse(console.Drop, item, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, -1));
            ReleaseMouse(console.Drop, item, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, MouseInsertionAt(1, upperHalf: true)));
            ReleaseMouse(console.Drop, item, new DropTarget(ActionReportTemplateDropHandler.ZoneKey, null, MouseInsertionAt(1, upperHalf: false)));

            Assert.False(outside);
            Assert.False(notReorder);
            Assert.Equal(original, console.Board.OrderedIds.ToArray());
            Assert.Empty(api.ReorderCalls);
            Assert.False(console.CanUndoReorder);
        });
    }

    // ── RPT-019 · RPT-028: "?" 말풍선 ───────────────────────────────────────────────────────────
    // 정적: 뷰마다 키를 콘솔 셸 · 절에 걸었고, 그 키가 설명 목록에 등록돼 있다(버튼 id = Help.{키}).
    [Trait("Scenario", "RPT-019")]
    [Theory]
    [InlineData("ReportConsoleView.xaml", "ConsoleShell", "Reports.Console", "보고서")]
    [InlineData("ReportCreateDetailView.xaml", "ConsoleSection", "Reports.Create.Severity", "심각도")]
    [InlineData("ReportTemplateDetailView.xaml", "ConsoleSection", "Reports.Template.Components", "구성 요소")]
    [InlineData("ReportGenerationDetailView.xaml", "ConsoleSection", "Reports.Generation.Actions", "생성 취소 · 삭제")]
    public void should_hang_a_registered_question_mark_on_each_reports_area(string view, string host, string key, string title)
        => AssertQuestionMarkHung(view, host, key, title);

    [Trait("Scenario", "RPT-028")]
    [Fact]
    public void should_hang_the_registered_phrase_question_mark_on_the_phrase_console_header()
    {
        AssertQuestionMarkHung("ActionReportTemplateConsoleView.xaml", "ConsoleShell", "Reports.Phrase", "조치보고 문구");
        var entry = HelpCatalog.Find("Reports.Phrase")!;
        Assert.Contains(entry.Sections, s => s.Heading == "순서 바꾸기");
        Assert.Contains(entry.Sections, s => s.Heading == "다른 곳에서 바뀌었을 때");
    }

    private static void AssertQuestionMarkHung(string view, string host, string key, string title)
    {
        var xaml = ViewText(view);
        Assert.Matches(new Regex($"<c:{host}\\b[^>]*HelpKey=\"{Regex.Escape(key)}\"", RegexOptions.Singleline), xaml);

        Assert.Contains(ReportsHelp.Entries, e => e.Key == key);
        var entry = HelpCatalog.Find(key);                                    // 모듈 초기화로 등록됐다
        Assert.NotNull(entry);
        Assert.Equal(title, entry!.Title);
        Assert.NotEmpty(entry.Sections.SelectMany(s => s.Items));
        Assert.DoesNotContain(entry.ToPlainText().Split('\n'), line => string.IsNullOrWhiteSpace(line));
    }

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

    private static bool PressEscape(HeadlessSource source, UIElement target)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    // 부분: 말풍선은 한 번에 하나 · 열린 것만 Esc 로 닫힌다(창 없는 입력 원천). 말풍선 그림 · 실제 Esc 키 입력은 헤디드.
    [Trait("Scenario", "RPT-019,RPT-028")]
    [Fact]
    public void should_open_one_reports_callout_at_a_time_and_close_only_the_open_one_with_escape()
    {
        OnSta(() =>
        {
            var keys = new[] { "Reports.Console", "Reports.Create.Severity", "Reports.Template.Components", "Reports.Generation.Actions", "Reports.Phrase" };
            var tips = keys.Select(k => new HelpTip { HelpKey = k }).ToList();
            var root = new StackPanel();
            foreach (var tip in tips) root.Children.Add(tip);
            var source = new HeadlessSource { RootVisual = root };
            try
            {
                Assert.All(tips, t => Assert.True(t.IsAvailable, $"{t.HelpKey} 를 열 수 없다"));

                tips[0].Open();
                Assert.Same(tips[0], HelpTip.Current);
                Assert.Equal("보고서", HelpTip.Current!.Entry!.Title);

                foreach (var tip in tips.Skip(1))                             // 심각도 · 구성 · 행동 · 문구 차례로
                {
                    var previous = HelpTip.Current!;
                    tip.Open();
                    Assert.Same(tip, HelpTip.Current);
                    Assert.NotEqual(true, previous.IsChecked);                // 앞의 것은 닫혔다 — 한 번에 하나
                }
                Assert.Equal("조치보고 문구", HelpTip.Current!.Entry!.Title);

                Assert.True(PressEscape(source, tips[^1]));                  // 열린 말풍선만 닫고 Esc 를 먹는다
                Assert.NotEqual(true, tips[^1].IsChecked);
                Assert.Null(HelpTip.Current);
                Assert.False(PressEscape(source, tips[^1]));                 // 닫혀 있으면 Esc 는 지나간다(창 · 편집 취소 몫)
            }
            finally { source.RootVisual = null!; }
            return Task.CompletedTask;
        });
    }

    // 부분: F1 = 포커스에서 가장 가까운 범위의 "?"(커널 HandlesF1 명령 경로). 실제 F1 키 제스처 · 실제 뷰 트리의 범위 배선은 헤디드.
    [Trait("Scenario", "RPT-019")]
    [Fact]
    public void should_open_the_nearest_reports_question_mark_when_help_is_asked_from_inside_it()
    {
        OnSta(() =>
        {
            var root = new Border();
            HelpTip.SetHandlesF1(root, true);                                  // 콘솔 셸 뿌리처럼
            var console = new StackPanel();
            root.Child = console;

            var consoleTip = new HelpTip { HelpKey = "Reports.Console", Scope = console };
            var severity = new StackPanel();
            var severityTip = new HelpTip { HelpKey = "Reports.Create.Severity", Scope = severity };
            var severityChip = new Button { Content = "주의" };
            severity.Children.Add(severityTip);
            severity.Children.Add(severityChip);
            var titleBox = new TextBox();                                      // 심각도 절 밖(콘솔 안)
            console.Children.Add(consoleTip);
            console.Children.Add(titleBox);
            console.Children.Add(severity);

            var source = new HeadlessSource { RootVisual = root };
            try
            {
                // 화면에 붙은 "?" 는 Loaded 에서 제 범위에 이름을 건다 — 렌더 펌프가 없으니 Loaded 를 직접 울린다.
                consoleTip.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, consoleTip));
                severityTip.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, severityTip));

                Assert.True(ApplicationCommands.Help.CanExecute(null, severityChip));
                ApplicationCommands.Help.Execute(null, severityChip);
                Assert.Same(severityTip, HelpTip.Current);                    // 가장 가까운 범위 = 심각도 절

                ApplicationCommands.Help.Execute(null, titleBox);
                Assert.Same(consoleTip, HelpTip.Current);                     // 절 밖이면 콘솔 머리 "?"
                Assert.NotEqual(true, severityTip.IsChecked);
            }
            finally { source.RootVisual = null!; }
            return Task.CompletedTask;
        });
    }

    // ── RPT-020(표지 없음 — 부분): 두 테마 토큰 계약 ────────────────────────────────────────────
    // 테마는 런타임에 Tokens.Light/Dark 를 갈아 끼운다 → 색은 DynamicResource 여야 따라가고, 쓰는 키는 두 사전 모두에 있어야 한다.
    // "레이아웃 위반 없음"(LayoutAudit) 자체는 실렌더라 이 시험이 단언하지 않는다 — 그래서 표지를 달지 않는다.
    [Fact]
    public void should_draw_reports_views_with_theme_brushes_that_exist_in_both_themes_and_follow_a_theme_switch()
    {
        var themes = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes");
        HashSet<string> Keys(string file) => Regex.Matches(File.ReadAllText(Path.Combine(themes, file)), "x:Key=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var shared = Keys("Tokens.Shared.xaml");
        var light = Keys("Tokens.Light.xaml");
        var dark = Keys("Tokens.Dark.xaml");

        var views = Directory.GetFiles(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Reports.Ui", "Views", "Panels"), "*.xaml");
        var sources = views.Select(v => (Name: Path.GetFileName(v), Text: File.ReadAllText(v)))
            .Concat(new[] { (Name: "ReportConsoleView.xaml.cs", Text: ViewText("ReportConsoleView.xaml.cs")) })   // 코드로 만드는 칩 셀 템플릿
            .ToList();
        var problems = new List<string>();

        foreach (var (name, text) in sources)
        {
            foreach (Match m in Regex.Matches(text, "\\{StaticResource\\s+([A-Za-z.]+Brush)\\}"))
                problems.Add($"{name}: 색 토큰 {m.Groups[1].Value} 을 StaticResource 로 씀(테마 전환을 안 따라간다)");

            foreach (Match m in Regex.Matches(text, "\\{DynamicResource\\s+([A-Za-z.]+Brush)\\}"))
            {
                var key = m.Groups[1].Value;
                if (shared.Contains(key)) continue;
                if (!light.Contains(key)) problems.Add($"{name}: {key} 가 라이트 토큰에 없다");
                if (!dark.Contains(key)) problems.Add($"{name}: {key} 가 다크 토큰에 없다");
            }

            if (!name.EndsWith(".xaml", StringComparison.Ordinal)) continue;
            var literals = Regex.Matches(text, "\\s(Background|Foreground|BorderBrush|Stroke|Fill)=\"([^\"{][^\"]*)\"")
                .Cast<Match>()
                .Concat(Regex.Matches(text, "<Setter\\s+Property=\"(Background|Foreground|BorderBrush|Stroke|Fill)\"\\s+Value=\"([^\"{][^\"]*)\"").Cast<Match>());
            foreach (var m in literals)
            {
                var value = m.Groups[2].Value;
                if (value is "Transparent" or "True" or "False") continue;   // 투명 · Fill 이라는 이름의 bool 속성
                // R20 — 미리보기 종이: 보고서 HTML 이 흰 종이라 칸도 테마와 무관하게 흰색(ReportPreviewViewModel 주석).
                if (value == "White" && m.Groups[1].Value == "Background" && name is "ReportPreviewView.xaml" or "ReportLargePreviewWindow.xaml") continue;
                problems.Add($"{name}: {m.Groups[1].Value}=\"{value}\" — 테마 토큰이 아닌 고정 색");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // ── RPT-025(표지 없음 — 보고서 쪽 몫): 문구 등록 → 이벤트 트레이가 읽을 서버 순서 ─────────────────────
    // 트레이는 서버 목록을 display_order 순으로 읽고(Events.Ui ActionReportPhraseSource) SYNC_ACTION_REPORT_TEMPLATE →
    // ActionReportTemplatesChangedMessage(호스트 라우터)로 다시 읽는다. 문구 콘솔은 따로 알림을 내지 않는다.
    // 트레이 반영은 Events.Ui/Tests/ReportPhraseTrayScenarioTests 가 단언한다.
    [Fact]
    public async Task should_store_a_registered_phrase_at_the_end_of_the_server_order_that_the_event_tray_reads()
    {
        var (console, api) = await OpenPhrasesAsync();
        await console.AddAsync();
        console.DraftContent = "차량 통제";

        await console.ApplyAsync();

        var created = Assert.Single(api.CreateCalls);
        Assert.Equal(("차량 통제", 3), (created.Content, created.DisplayOrder));     // 지금 맨 끝(0·1·2) 다음
        var served = await api.GetTemplatesAsync();
        Assert.Equal(new[] { "야생동물출현", "강풍/폭우", "오경보", "차량 통제" }, served.Data!.Select(t => t.Content).ToArray());
    }
}
