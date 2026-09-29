using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DeviceConsolePreview;

/****************************************************************************
   Purpose      : 부대 관계도 미리보기 — 스냅숏 한 묶음(TEST-36) · 성능 벤치(TEST-37)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 끌기 · 줌을 캔버스의 내부 입력 입구로 흉내 낸다(실제 마우스 없음 — 화면 밖 창). 입구가 internal 이라 반사로 부른다(도구 전용).
/// </summary>
internal static class CanvasDriver
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Press(UnitMapCanvas canvas, Point at, UnitMapNode? node)
        => Call(canvas, "OnPointerPressed", at, node, false);

    public static void Move(UnitMapCanvas canvas, Point at) => Call(canvas, "OnPointerMoved", at);

    public static void Release(UnitMapCanvas canvas, Point at) => Call(canvas, "OnPointerReleased", at);

    public static void Cancel(UnitMapCanvas canvas) => Call(canvas, "FinishGesture", false);

    public static T? Field<T>(UnitMapCanvas canvas, string name) where T : class
        => typeof(UnitMapCanvas).GetField(name, Any)?.GetValue(canvas) as T;

    private static void Call(object target, string name, params object?[] args)
    {
        var method = target.GetType().GetMethods(Any).First(m => m.Name == name && m.GetParameters().Length == args.Length);
        method.Invoke(target, args);
    }
}

internal static class UnitsMapPreviewRuns
{
    #region - 스냅숏 한 묶음(TEST-36) -
    /// <summary>라이트 · 다크 각각 새 미리보기를 한 번 띄워 상태를 차례로 찍는다 — 한 번의 실행 묶음(렌더 1회).</summary>
    public static async Task SnapshotAsync(App app, UnitsMapPreviewOptions options, string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var theme in new[] { "light", "dark" })
        {
            if (theme == "dark") app.ApplyDarkForPreview();
            var preview = await UnitsMapPreview.CreateAsync(app, options with { Mode = UnitsMapLayoutMode.Supported, Visible = false });
            try { await SweepAsync(preview, theme, folder); }
            finally { preview.Window.Close(); }
        }
    }

    private static async Task SweepAsync(UnitsMapPreview preview, string theme, string folder)
    {
        var client = preview.Main;
        var canvas = client.Canvas;
        var vm = client.ViewModel;
        var tree = client.Console.Tree;
        int Id(string name) => tree.Ordered.First(n => n.Name == name).Id;
        Point Screen(string name) => canvas.View.WorldToScreen(canvas.Scene.Positions[Id(name)]);
        UnitMapNode Node(string name) => canvas.Nodes.First(n => n.UnitId == Id(name));
        async Task Shot(string name, double dpiScale = 1.0)
        {
            await Settle(vm);
            Save(folder, $"umap-{theme}-{name}", client.Pane, canvas, preview.Window, dpiScale);
        }

        client.Console.TrySelect(Id("7중대"));
        await Settle(vm);

        ((IUnitMapSurface)canvas).Fit();
        await Shot("01-L0-fit");

        canvas.SetView(0.5, canvas.Scene.Positions[Id("7중대")]);
        await Shot("02-L1-50");

        canvas.SetView(1.0, canvas.Scene.Positions[Id("7중대")]);
        await Shot("03-L2-100");
        await Shot("03-L2-100-dpi125", 1.25);
        await Shot("03-L2-100-dpi150", 1.5);

        // 끄는 중 — 상위 후보 · 인접 후보 · 현 상위(막힘) · 빈 곳(위치).
        canvas.SetView(0.5, canvas.Scene.Positions[Id("7중대")]);
        await Settle(vm);
        var start = Screen("7중대");
        CanvasDriver.Press(canvas, start, Node("7중대"));
        CanvasDriver.Move(canvas, start + new Vector(20, 0));
        CanvasDriver.Move(canvas, Screen("3대대"));
        await Shot("04-drag-parent");
        CanvasDriver.Move(canvas, Screen("9중대"));
        await Shot("05-drag-adjoin");
        CanvasDriver.Move(canvas, Screen("2대대"));
        await Shot("06-drag-current-parent-blocked");
        var empty = start + new Vector(0, 140);
        CanvasDriver.Move(canvas, empty);
        await Shot("07-drag-position");

        // 상위 후보에 놓기 → 확인 오버레이(서버 0).
        CanvasDriver.Move(canvas, Screen("3대대"));
        CanvasDriver.Release(canvas, Screen("3대대"));
        await Shot("08-confirm");
        vm.CancelConfirm();
        await Settle(vm);

        // 위치 놓기 → 되돌리기 막대(공유).
        Drag(canvas, Screen("7중대"), Screen("7중대") + new Vector(-60, 70), Node("7중대"));
        await Shot("09-undo-bar");

        // 충돌 — 그 사이 다른 운영자가 같은 부대를 옮김(알림 없이) → 412 → 충돌 막대.
        preview.Server.SimulateOtherWrite(Id("7중대"), 10, 10, publish: false);
        Drag(canvas, Screen("7중대"), Screen("7중대") + new Vector(40, 0), Node("7중대"));
        await Shot("10-conflict-bar");

        // 쓰기 실패 → 오류 막대(왼쪽 세로 막대) · 배치를 다시 읽는다.
        preview.Server.Mode = UnitsMapLayoutMode.Failing;
        Drag(canvas, Screen("8중대"), Screen("8중대") + new Vector(40, 40), Node("8중대"));
        await Shot("11-error-bar-read-failed");

        // 배치 문구 4종(FR-11) — 공유 · 판 불일치 · 읽기 실패(위) · 미지원(세션 전용).
        preview.Server.Mode = UnitsMapLayoutMode.Supported;
        vm.DismissBar();
        await vm.RetryLayoutAsync();
        await Shot("12-status-shared");
        preview.Server.SetLayoutVersion(2);
        await vm.RetryLayoutAsync();
        await Shot("13-status-version-mismatch");
        preview.Server.SetLayoutVersion(UnitMapLayout.LayoutVersion);
        preview.Server.Mode = UnitsMapLayoutMode.Unsupported;
        await vm.RetryLayoutAsync();
        await Shot("14-status-session-only");
    }

    private static void Drag(UnitMapCanvas canvas, Point from, Point to, UnitMapNode node)
    {
        CanvasDriver.Press(canvas, from, node);
        CanvasDriver.Move(canvas, from + (to - from) / 2);
        CanvasDriver.Move(canvas, to);
        CanvasDriver.Release(canvas, to);
    }

    private static async Task Settle(UnitMapViewModel vm)
    {
        await vm.WhenIdleAsync();
        await Task.Delay(350);
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
    }

    /// <summary>칸(툴바 + 캔버스)을 PNG 로, 측정용 곁 파일(노드 사각형 · 상태 · 토큰 색)을 JSON 으로.</summary>
    private static void Save(string folder, string name, FrameworkElement pane, UnitMapCanvas canvas, Window window, double dpiScale)
    {
        var width = (int)Math.Ceiling(pane.ActualWidth * dpiScale);
        var height = (int)Math.Ceiling(pane.ActualHeight * dpiScale);
        var bitmap = new RenderTargetBitmap(width, height, 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(0, 0, pane.ActualWidth, pane.ActualHeight));
            dc.DrawRectangle(new VisualBrush(pane) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                             null, new Rect(0, 0, pane.ActualWidth, pane.ActualHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(folder, name + ".png"))) encoder.Save(stream);

        if (dpiScale != 1.0) return;
        var paneRect = new Rect(0, 0, pane.ActualWidth, pane.ActualHeight);
        var nodes = canvas.Nodes
            .Select(n => (Node: n, Rect: new Rect(n.TranslatePoint(new Point(0, 0), pane), new Size(n.ActualWidth, n.ActualHeight))))
            .Where(x => x.Node.IsVisible && paneRect.Contains(x.Rect))
            .Select(x => new
            {
                id = x.Node.UnitId,
                name = x.Node.UnitName,
                level = x.Node.Level.ToString(),
                state = x.Node.DropState.ToString(),
                selected = x.Node.IsSelectedNode,
                suspended = x.Node.IsSuspended,
                rect = new[] { x.Rect.X, x.Rect.Y, x.Rect.Width, x.Rect.Height },
                frame = new[] { x.Rect.X + x.Node.Visuals.Frame.X, x.Rect.Y + x.Node.Visuals.Frame.Y, x.Node.Visuals.Frame.Width, x.Node.Visuals.Frame.Height },
            })
            .ToList();
        var ghost = CanvasDriver.Field<FrameworkElement>(canvas, "_ghost");
        var sidecar = new
        {
            name,
            scale = canvas.Scale,
            level = canvas.Level.ToString(),
            tokens = new[] { "PrimaryBrush", "SelectionBrush", "TextPrimaryBrush", "TextMutedBrush", "SurfaceAltBrush", "SurfaceBrush", "StatusCriticalBrush" }
                .ToDictionary(t => t, t => (canvas.TryFindResource(t) as SolidColorBrush)?.Color.ToString()),
            ghost = ghost is { IsVisible: true } g ? new[] { g.TranslatePoint(new Point(0, 0), pane).X, g.TranslatePoint(new Point(0, 0), pane).Y, g.ActualWidth, g.ActualHeight } : null,
            nodes,
        };
        File.WriteAllText(Path.Combine(folder, name + ".json"), JsonConvert.SerializeObject(sidecar, Formatting.Indented));
    }
    #endregion

    #region - 성능 벤치(TEST-37 · NFR-01~04) -
    /// <summary>200 부대 · 장비 2,000 — 10회 중앙값. 결과는 <c>bench.json</c> · <c>bench.md</c>.</summary>
    public static async Task BenchAsync(App app, UnitsMapPreviewOptions options, string folder)
    {
        Directory.CreateDirectory(folder);
        var results = new Dictionary<string, object>();

        // ① 레일 전환 → 첫 그림(뷰모델 + 캔버스 + 장면 + 레이아웃 · 렌더 한 번) — 배치 GET 을 기다리지 않는다(NFR-01).
        var firstDraw = new List<double>();
        for (var i = 0; i < 11; i++)
        {
            var sw = Stopwatch.StartNew();
            var preview = await UnitsMapPreview.CreateAsync(app, options with { Mode = UnitsMapLayoutMode.Supported, Visible = false, CallLog = null });
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            sw.Stop();
            if (i > 0) firstDraw.Add(sw.Elapsed.TotalMilliseconds);     // 첫 회는 JIT 워밍업
            if (i < 10) preview.Window.Close();
            else await MeasureOnAsync(app, preview, results);
        }
        results["firstDrawMs.median"] = Median(firstDraw);
        results["firstDrawMs.max"] = firstDraw.Max();

        // ② 자동 배치 계산(200 부대).
        var tree = Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model.UnitTreeBuilder.Build(UnitsMapPreviewData.Standard200());
        UnitMapLayout.Compute(tree);
        var layout = new List<double>();
        for (var i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            UnitMapLayout.Compute(tree);
            sw.Stop();
            layout.Add(sw.Elapsed.TotalMilliseconds);
        }
        results["autoLayoutMs.median"] = Median(layout);

        File.WriteAllText(Path.Combine(folder, "bench.json"), JsonConvert.SerializeObject(results, Formatting.Indented));
        File.WriteAllLines(Path.Combine(folder, "bench.md"),
            new[] { "| 항목 | 값 |", "|---|---|" }.Concat(results.Select(r => $"| {r.Key} | {r.Value} |")));
    }

    /// <summary>띄운 미리보기 하나로 팬 · 단계 · 테마 · 끌리는 층 빈도를 잰다.</summary>
    private static async Task MeasureOnAsync(App app, UnitsMapPreview preview, Dictionary<string, object> results)
    {
        var canvas = preview.Main.Canvas;
        var tree = preview.Main.Console.Tree;
        int Id(string name) => tree.Ordered.First(n => n.Name == name).Id;
        await Settle(preview.Main.ViewModel);

        // ①′ 레일 전환(창은 이미 떠 있다) — 새 뷰모델 + 캔버스 + 장면 → 첫 렌더. 첫 회는 워밍업.
        var rail = new List<double>();
        var phases = new Dictionary<string, List<double>>();
        for (var i = 0; i < 11; i++)
        {
            var ms = await preview.MeasureRailSwitchAsync();
            if (i == 0) continue;
            rail.Add(ms);
            foreach (var (key, value) in preview.LastRailSwitchPhases)
                (phases.TryGetValue(key, out var list) ? list : phases[key] = new List<double>()).Add(value);
        }
        results["railSwitchFirstDrawMs.median"] = Median(rail);
        results["railSwitchFirstDrawMs.max"] = Math.Round(rail.Max(), 1);
        foreach (var (key, values) in phases) results[$"railSwitch.{key}.median"] = Median(values);

        // ①″ 저장된 개인 뷰가 L1(50%) · L2(100%)였을 때의 첫 그림 — 단계 템플릿이 무거울수록 비싸다(전체 보기 = L0 가 가장 가볍다).
        foreach (var (label, scale) in new[] { ("L1", 0.5), ("L2", 1.0) })
        {
            var at = new List<double>();
            var layoutAt = new List<double>();
            for (var i = 0; i < 11; i++)
            {
                var ms = await preview.MeasureRailSwitchAsync(scale);
                if (i == 0) continue;
                at.Add(ms);
                layoutAt.Add(preview.LastRailSwitchPhases["layout"]);
            }
            results[$"railSwitchFirstDrawMs{label}.median"] = Median(at);
            results[$"railSwitchFirstDrawMs{label}.max"] = Math.Round(at.Max(), 1);
            results[$"railSwitch{label}.layout.median"] = Median(layoutAt);
            results[$"railSwitch{label}.templates"] = preview.LastRailSwitchPhases["templates"];
            results[$"railSwitch{label}.visualsPerNode"] = preview.LastRailSwitchPhases["visualsPerNode"];
        }
        var frame = ((DockPanel)preview.Main.Pane.Content).Children.OfType<Border>().Last();
        frame.Child = canvas;                                   // 원래 캔버스로 되돌린다
        await Settle(preview.Main.ViewModel);

        // ③ 팬 100회 — 정적 층 재그림 0(NFR-02).
        canvas.SetView(0.5, canvas.Scene.Positions[Id("7중대")]);
        await Settle(preview.Main.ViewModel);
        var before = canvas.RenderCount;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++) canvas.PanBy(i % 2 == 0 ? 7 : -5, 3);
        sw.Stop();
        results["pan100.staticRedraws"] = canvas.RenderCount - before;
        results["pan100.ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 2);

        // ④ 같은 단계 안 줌 = 템플릿 교체 0 · 단계 경계 넘기 = 노드마다 1(NFR-03).
        var templates = canvas.Nodes.Sum(n => n.TemplateApplyCount);
        canvas.SetView(0.6, canvas.Scene.Positions[Id("7중대")]);
        await Settle(preview.Main.ViewModel);
        results["zoomWithinL1.templateSwaps"] = canvas.Nodes.Sum(n => n.TemplateApplyCount) - templates;
        templates = canvas.Nodes.Sum(n => n.TemplateApplyCount);
        sw.Restart();
        canvas.SetView(0.9, canvas.Scene.Positions[Id("7중대")]);
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        sw.Stop();
        await Settle(preview.Main.ViewModel);
        results["crossL1toL2.templateSwaps"] = canvas.Nodes.Sum(n => n.TemplateApplyCount) - templates;
        results["crossL1toL2.nodes"] = canvas.Nodes.Count;
        results["crossL1toL2.ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 1);

        // ⑤ 테마 전환 — 선 층 다시 그림 1회(NFR-07).
        var lines = canvas.LineLayer.RenderCount;
        app.ApplyDarkForPreview();
        await Settle(preview.Main.ViewModel);
        results["themeSwap.lineRedraws"] = canvas.LineLayer.RenderCount - lines;

        // ⑥ 끄는 중 120Hz 포인터 1초(가짜 시계) — 끌리는 층 갱신 ≤ 30Hz · 정적 층 재그림 0(NFR-04).
        canvas.SetView(0.5, canvas.Scene.Positions[Id("7중대")]);
        await Settle(preview.Main.ViewModel);
        // ⑥′ 끌기 시작 — 데드존을 넘는 이동부터 첫 렌더까지(모든 노드에 대상 표시를 칠한다, FR-30). 첫 회는 워밍업.
        var realized = typeof(UnitMapNode).GetProperty("IsDropDecorRealized", BindingFlags.Instance | BindingFlags.NonPublic);
        if (realized is not null) results["dragStart.decorsRealizedBefore"] = canvas.Nodes.Count(n => (bool)realized.GetValue(n)!);
        var dragStarts = new List<double>();
        for (var i = 0; i < 6; i++)
        {
            var from = canvas.View.WorldToScreen(canvas.Scene.Positions[Id("7중대")]);
            CanvasDriver.Press(canvas, from, canvas.Nodes.First(n => n.UnitId == Id("7중대")));
            var watch = Stopwatch.StartNew();
            CanvasDriver.Move(canvas, from + new Vector(20, 0));
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            watch.Stop();
            CanvasDriver.Cancel(canvas);
            await Settle(preview.Main.ViewModel);
            if (i > 0) dragStarts.Add(watch.Elapsed.TotalMilliseconds);
            else results["dragStartColdMs"] = Math.Round(watch.Elapsed.TotalMilliseconds, 2);   // 첫 끌기(워밍업 · 지연 요소 첫 실체화 포함)
        }
        results["dragStartMs.median"] = Median(dragStarts);

        var clock = new StepClock();
        canvas.Clock = clock;
        var start = canvas.View.WorldToScreen(canvas.Scene.Positions[Id("7중대")]);
        CanvasDriver.Press(canvas, start, canvas.Nodes.First(n => n.UnitId == Id("7중대")));
        CanvasDriver.Move(canvas, start + new Vector(20, 0));
        var ghost = CanvasDriver.Field<FrameworkElement>(canvas, "_ghost")!;
        var staticBefore = canvas.RenderCount;
        var updates = 0;
        var lastLeft = Canvas.GetLeft(ghost);
        for (var i = 1; i <= 120; i++)
        {
            clock.Advance(1000.0 / 120);
            CanvasDriver.Move(canvas, start + new Vector(20 + i * 0.5, (i % 7) * 0.25));
            var left = Canvas.GetLeft(ghost);
            if (left != lastLeft) { updates++; lastLeft = left; }
        }
        results["drag120Hz.dragLayerUpdatesPerSecond"] = updates;
        results["drag120Hz.staticRedraws"] = canvas.RenderCount - staticBefore;
        CanvasDriver.Cancel(canvas);

        // ⑦ 운영 조건 — 앱은 부대 콘솔(ConsoleShell)이 뜰 때 KoreanWordWrap(TextBlock 클래스 처리기)을 건다. 미리보기는 걸지 않아
        //    위 ①′ 는 글 비용을 덜 센다. 한 번 걸면 되돌릴 수 없으므로 맨 끝에서 같은 레일 전환을 다시 잰다(다크 테마 상태).
        Ironwall.Dotnet.Libraries.Utils.Consoles.KoreanWordWrap.Install();
        foreach (var (label, scale) in new (string, double?)[] { ("", null), ("L1", 0.5), ("L2", 1.0) })
        {
            var at = new List<double>();
            for (var i = 0; i < 11; i++)
            {
                var ms = await preview.MeasureRailSwitchAsync(scale);
                if (i > 0) at.Add(ms);
            }
            results[$"prodWordWrap.railSwitchFirstDrawMs{label}.median"] = Median(at);
        }
        preview.Window.Close();
    }

    private sealed class StepClock : Ironwall.Dotnet.Libraries.Base.Services.IClock
    {
        public DateTime UtcNow { get; private set; } = new(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Now => UtcNow.ToLocalTime();
        public void Advance(double ms) => UtcNow = UtcNow.AddMilliseconds(ms);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        var median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
        return Math.Round(median, 2);
    }
    #endregion
}
