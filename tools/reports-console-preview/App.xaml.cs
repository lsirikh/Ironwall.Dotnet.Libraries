using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;
using Ironwall.Dotnet.Libraries.Reports.Ui.Tests;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// ↓ 조치보고 문구 관리 콘솔 미리보기(--action-report-templates) 전용. 보고서 콘솔과 같은 진짜 뷰 + 진짜 뷰모델 +
//   가짜 API 패턴을 그대로 따른다(§Build/§RunSnapshotsAsync 참고) — DI 변경 없이 이 파일 하나만 늘린다.

namespace ReportsConsolePreview;

/// <summary>
/// 보고서 콘솔 미리보기 — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 데이터 위에 띄운다(호스트 앱 · 서버 없음).
/// <c>--snapshot &lt;폴더&gt;</c> 면 입력 없이 여러 상태를 PNG 로 떠 놓고 끝낸다. <c>--dark</c> 면 다크로 띄운다.
/// </summary>
/// <remarks>
/// ★ 미리보기(WebView2)는 <b>오프스크린에서 렌더되지 않는다</b>. 그래서 캡처에서는 자리표시자로 내려간다 —
/// 런타임이 없을 때의 실제 동작과 같은 길이므로, 그 자체가 확인할 거리다(FR-18 · FR-23).
/// </remarks>
public partial class App : Application
{
    private const string DarkTokens = "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml";

    private ReportConsoleViewModel _viewModel = null!;
    private ReportConsoleView _view = null!;
    private Window _window = null!;

    private ActionReportTemplateConsoleViewModel _artViewModel = null!;
    private ActionReportTemplateConsoleView _artView = null!;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var snapshotAt = System.Array.IndexOf(e.Args, "--snapshot");
        var directory = snapshotAt >= 0 && snapshotAt + 1 < e.Args.Length ? e.Args[snapshotAt + 1] : null;

        try
        {
            if (e.Args.Contains("--dark")) ApplyDark();

            // 조치보고 문구 관리 콘솔은 보고서 콘솔과는 별개의 독립 콘솔이라(각자 자기 ConsoleShell)
            // 창을 따로 띄운다 — DI·본 콘솔 코드는 건드리지 않는다.
            if (e.Args.Contains("--action-report-templates"))
            {
                await RunActionReportTemplatesFlowAsync(e.Args, directory);
                if (directory is not null) Shutdown();
                return;
            }

            _viewModel = Build(e.Args.Contains("--readonly"), e.Args.Contains("--empty"));
            _view = new ReportConsoleView { DataContext = _viewModel };
            _window = new Window
            {
                Title = "보고서 콘솔 미리보기",
                // ★ 1320 이면 바깥 여백(12×2) + 창 테두리를 빼고 셸이 1256 밖에 안 돼 늘 서랍(<1280)으로 찍혔다 —
                //   도킹(세 칸이 나란한 기본 배치)을 한 번도 안 보여 주던 값이다. 1360 이면 셸이 1296 으로 도킹이다.
                Width = 1360,
                Height = 820,
                Background = (Brush)FindResource("SurfaceBrush"),
                Content = new Border { Margin = new Thickness(12), Child = _view },
            };
            PreviewTools.Shared.OffscreenStage.ApplySurface(e.Args, _view, _window);
            PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

            await ((IActivate)_viewModel).ActivateAsync();

            if (directory is null) return;

            Directory.CreateDirectory(directory);
            await RunSnapshotsAsync(directory, e.Args.Contains("--dark") ? "dark" : "light", e.Args.Contains("--empty"));
        }
        catch (System.Exception ex)
        {
            if (directory is null) MessageBox.Show(ex.ToString());
            else File.WriteAllText(Path.Combine(directory, "snapshot-error.txt"), ex.ToString());
        }

        if (directory is not null) Shutdown();
    }

    #region - Composition -
    private static ReportConsoleViewModel Build(bool readOnly, bool empty = false)
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeReportApiService();
        var permission = new FakePermissionService { Edit = !readOnly };

        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute.BeginOnUIThread 가 제자리에서 돌아 교차 스레드로 화면을 만진다.
        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? events : null!;
        IoC.GetAllInstances = _ => System.Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();

        if (!empty) PreviewData.Fill(api);

        return new ReportConsoleViewModel(
            events, log, permission,
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
    }
    #endregion

    #region - 조치보고 문구 관리 콘솔 미리보기 -
    private async Task RunActionReportTemplatesFlowAsync(string[] args, string? directory)
    {
        _artViewModel = BuildActionReportTemplates(args.Contains("--readonly"), args.Contains("--empty"));
        _artView = new ActionReportTemplateConsoleView { DataContext = _artViewModel };
        _window = new Window
        {
            Title = "조치보고 문구 관리 콘솔 미리보기",
            Width = 1200,
            Height = 760,
            Background = (Brush)FindResource("SurfaceBrush"),
            Content = new Border { Margin = new Thickness(12), Child = _artView },
        };
        PreviewTools.Shared.OffscreenStage.ApplySurface(args, _artView, _window);
        PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

        await ((IActivate)_artViewModel).ActivateAsync();

        if (directory is null) return;
        Directory.CreateDirectory(directory);
        await RunActionReportTemplateSnapshotsAsync(directory, args.Contains("--dark") ? "dark" : "light", args.Contains("--empty"));
    }

    private static ActionReportTemplateConsoleViewModel BuildActionReportTemplates(bool readOnly, bool empty)
    {
        var log = new FakeLogService();
        var events = new EventAggregator();
        var api = new FakeActionReportTemplateApiService { FailList = empty };
        var permission = new FakePermissionService { Edit = !readOnly };

        IoC.GetInstance = (type, _) => type == typeof(IEventAggregator) ? events : null!;
        IoC.GetAllInstances = _ => System.Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new XamlPlatformProvider();

        if (!empty)
        {
            api.Templates.Add(ActionReportTemplateSeed.Template(1, "야생동물출현", 0));
            api.Templates.Add(ActionReportTemplateSeed.Template(2, "강풍/폭우", 1));
            api.Templates.Add(ActionReportTemplateSeed.Template(3, "울타리 점검/작업", 2));
            api.Templates.Add(ActionReportTemplateSeed.Template(4, "침입발생 특경출동조치", 3));
            api.Templates.Add(ActionReportTemplateSeed.Template(5, "오경보", 4));
        }

        return new ActionReportTemplateConsoleViewModel(events, log, permission, api);
    }

    /// <summary>
    /// 목록 · 손잡이 · ▲▼ 폴백 단추 · 선택 상세(수정 폼) · 등록 폼 · 빈 상태를 찍는다.
    /// ★ 삽입선(AdornerLayer)은 실제 마우스 캡처 드래그 중에만 뜨는 순간 시각효과라 VM 레벨 시뮬로는
    /// 재현할 수 없다 — 여기서는 "찍지 못했다"로 정직하게 남긴다(찍은 걸 검증됐다고 적지 않는다).
    /// </summary>
    private async Task RunActionReportTemplateSnapshotsAsync(string directory, string theme, bool empty)
    {
        await Settle();
        Save(directory, empty ? $"{theme}-art-00-empty-state" : $"{theme}-art-01-list-none");
        if (empty) return;

        _artViewModel.OnRowSelected(_artViewModel.Items.First(i => i.Id == 3));
        await Settle();
        Save(directory, $"{theme}-art-02-list-selected");

        await _artViewModel.AddAsync();
        await Settle();
        Save(directory, $"{theme}-art-03-create-empty");

        _artViewModel.DraftContent = "차량 통제";
        await Settle();
        Save(directory, $"{theme}-art-04-create-filled");
        _artViewModel.Revert();

        // 드래그 · Alt+↑/↓ · ▲ 단추가 공유하는 같은 커밋 경로의 결과 상태(되돌리기 단추가 켜진다).
        _artViewModel.MoveSelected(_artViewModel.Items.First(i => i.Id == 5), -1);
        await Settle();
        Save(directory, $"{theme}-art-05-reordered-undo-available");

        // 좁은 폭 — 상세가 서랍으로 겹친 모습과 닫힌 모습. --surface 면 콘솔 폭 자체를 줄인다.
        PreviewTools.Shared.OffscreenStage.SetWidth(_window, _artView, 900);
        _artViewModel.OnRowSelected(_artViewModel.Items.First(i => i.Id == 3));
        await Settle();
        Save(directory, $"{theme}-art-06-narrow-900-selected");
        _artViewModel.OnRowSelected(null);
        await Settle();
        Save(directory, $"{theme}-art-07-narrow-900-none");
    }
    #endregion

    #region - Snapshots -
    private async Task RunSnapshotsAsync(string directory, string theme, bool empty = false)
    {
        // WebView2 는 오프스크린에서 그려지지 않는다 — 런타임이 없을 때와 같은 길로 내려 자리표시자를 찍는다.
        _viewModel.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(false);
        _viewModel.PreviewViewModel.ProbeRuntime();

        await Settle();
        if (empty)
        {
            // 빈 상태(커널 ConsoleEmptyState) — 생성 이력 · 템플릿 두 목록.
            Save(directory, $"{theme}-00-empty-history");
            await _viewModel.SelectRailAsync(ReportConsoleRails.Template);
            await Settle();
            Save(directory, $"{theme}-00-empty-templates");
            return;
        }
        Save(directory, $"{theme}-01-list-none");

        _viewModel.OnRowSelected(_viewModel.ListViewModel.Rows.First(r => r.Id == 104));
        await Settle();
        Save(directory, $"{theme}-02-list-selected");
        WriteSurfaceDecision(directory, $"{theme}-02-list-selected");

        // 직접 지정 기간 + 템플릿 기반 — 메타의 기간 범위(R18) · 템플릿 이름(R17).
        _viewModel.OnRowSelected(_viewModel.ListViewModel.Rows.First(r => r.Id == 101));
        await Settle();
        Save(directory, $"{theme}-02b-list-custom-period");

        _viewModel.OnRowSelected(_viewModel.ListViewModel.Rows.First(r => r.Id == 103));
        await Settle();
        Save(directory, $"{theme}-03-list-running");

        _viewModel.OnRowSelected(_viewModel.ListViewModel.Rows.First(r => r.Id == 102));
        await Settle();
        Save(directory, $"{theme}-04-list-failed");

        await _viewModel.ListViewModel.ToggleStatusAsync(_viewModel.ListViewModel.StatusChips.First(c => c.Value == "COMPLETED"));
        await Settle();
        Save(directory, $"{theme}-05-list-filtered");
        await _viewModel.ListViewModel.ToggleStatusAsync(_viewModel.ListViewModel.StatusChips.First(c => c.Value == "COMPLETED"));

        await _viewModel.SelectRailAsync(ReportConsoleRails.Create);
        await Settle();
        Save(directory, $"{theme}-06-create-empty");

        _viewModel.CreateViewModel.Title = "9월 정기 보고서";
        _viewModel.CreateViewModel.IsTemplateBased = true;
        _viewModel.CreateViewModel.Severities[2].IsSelected = true;
        await Settle();
        Save(directory, $"{theme}-07-create-filled");

        // 직접 지정 — 시작 · 종료 날짜 칸이 값 칸 안에 들어오는지(R23: 종료일 칸이 잘렸다).
        _viewModel.CreateViewModel.IsCustomRange = true;
        await Settle();
        Save(directory, $"{theme}-07b-create-custom-range");
        _viewModel.Revert();

        await _viewModel.SelectRailAsync(ReportConsoleRails.Template);
        await Settle();
        Save(directory, $"{theme}-08-template-none");

        _viewModel.OnRowSelected(_viewModel.TemplateViewModel.Rows.First(t => t.Id == 201));
        await Settle();
        Save(directory, $"{theme}-09-template-selected");

        // 구성 순서를 끌어 옮긴 상태(드래그와 같은 담당을 부른다) — 바닥 막대가 경고색으로 바뀐다.
        _viewModel.EditViewModel.Board.Move(new[] { 1 }, 0);
        _viewModel.EditViewModel.Name += " (개정)";
        await Settle();
        Save(directory, $"{theme}-10-template-dirty");

        // 미적용 변경이 있는 채로 다른 줄을 누르면 막힌다 — 바닥 막대가 까닭을 말한다.
        _viewModel.OnRowSelected(_viewModel.TemplateViewModel.Rows.First(t => t.Id == 202));
        await Settle();
        Save(directory, $"{theme}-11-template-blocked");
        _viewModel.Revert();

        await _viewModel.AddAsync();
        await Settle();
        Save(directory, $"{theme}-12-template-create");
        _viewModel.Revert();

        // 좁은 폭 — 서랍(960~1279) · 접힘(<960). 미리보기는 자리표시자로 내려간다(공역).
        await _viewModel.SelectRailAsync(ReportConsoleRails.List);
        _viewModel.OnRowSelected(_viewModel.ListViewModel.Rows.First(r => r.Id == 104));
        await Settle();

        // 좁은 폭에서는 공역 때문에 어차피 자리표시자다 — 폭을 먼저 줄여 서랍으로 바뀐 <b>뒤에</b> 런타임을 되살려
        // "창이 좁아…" 문구 그대로를 찍는다. 순서를 바꾸면 잠깐 도킹으로 판정돼 WebView2 를 만들려다
        // 실패하고(오프스크린) 런타임 문구로 되돌아간다.
        PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, 1150);
        await Settle();
        _viewModel.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        _viewModel.PreviewViewModel.ProbeRuntime();
        await Settle();
        Save(directory, $"{theme}-13-drawer-1150");
        WriteSurfaceDecision(directory, $"{theme}-13-drawer-1150");

        PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, 900);
        await Settle();
        _viewModel.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        _viewModel.PreviewViewModel.ProbeRuntime();
        await Settle();
        Save(directory, $"{theme}-14-compact-900");

        // 선택을 놓으면 서랍이 닫힌다 — 그래야 좁은 폭에서 툴바(상태 칩)와 목록이 실제로 어떻게 보이는지 안다.
        _viewModel.OnRowSelected(null);
        await Settle();
        Save(directory, $"{theme}-15-compact-900-nodrawer");

        PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, 1150);
        await Settle();
        Save(directory, $"{theme}-16-drawer-1150-nodrawer");
    }

    /// <summary>
    /// R1 — 미리보기 칸의 판정을 글로 남긴다. WebView2 는 화면 밖에서 그려지지 않으므로 캡처로는 "살아 있는 미리보기" 를
    /// 볼 수 없다 — 대신 커널 폭 판정(도킹 · 서랍)과 그 폭에서 런타임이 있을 때 내려질 판정(Live · 자리표시자)을 적는다.
    /// </summary>
    private void WriteSurfaceDecision(string directory, string frame)
    {
        var shell = FindShell(_view);
        var mode = _viewModel.LayoutMode;
        var ifRuntime = ReportPreviewSurfaceRules.Resolve(mode, _viewModel.PreviewViewModel.IsLargeViewOpen,
            _viewModel.PreviewViewModel.IsOverlayOpen, isRuntimeReady: true, _viewModel.PreviewViewModel.Content);
        File.AppendAllText(Path.Combine(directory, "preview-surface.txt"),
            $"{frame}\tshellWidth={shell?.ActualWidth:0}\tlayout={mode}\tcontent={_viewModel.PreviewViewModel.Content}" +
            $"\tifRuntime={(ifRuntime.IsLive ? "Live" : "Placeholder: " + ifRuntime.Reason + " / " + ifRuntime.Hint)}" +
            $"\tcanOpenLarge={ReportPreviewSurfaceRules.CanOpenLargeView(true, _viewModel.PreviewViewModel.Content)}{System.Environment.NewLine}");
    }

    private static Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleShell? FindShell(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Ironwall.Dotnet.Libraries.Utils.Consoles.ConsoleShell shell) return shell;
            if (FindShell(child) is { } found) return found;
        }
        return null;
    }

    private void ApplyDark()
    {
        if (Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == DarkTokens)) return;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri(DarkTokens) });
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Dark;
        // 호스트(ThemeService.SyncMaterialDesignAndMahApps)는 MD 색만이 아니라 MahApps 크롬도 같이 바꾼다
        // (ThemeManager.Current.ChangeTheme(app, "Dark.Cyan")). 여기선 그 한 줄만 그대로 거울처럼 부른다 —
        // 안 부르면 이 콘솔이 쓰는 MahApps 스타일 컨트롤이 다크에서도 라이트 크롬으로 남는다.
        ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this, "Dark.Cyan");
    }

    private static Task Settle() => Task.Delay(420);

    /// <summary>
    /// D-05(2026-09-23, device/accounts 콘솔에서 먼저 잡음) — <c>VisualBrush(content){Stretch=None}</c> 로
    /// 간접 합성하던 옛 방식은 창을 막 띄운 <b>첫 캡처</b>에서 자식의 최근 레이아웃 변경분을 브러시가 못
    /// 따라가는 경우가 있었다(실측: 제품 바인딩·레이아웃은 처음부터 맞았는데 그 방식으로 뜬 PNG 에서만
    /// 일부 내용이 통째로 빠졌다). <c>RenderTargetBitmap.Render(element)</c> 로 직접 찍으면 그대로 나온다 —
    /// 그래서 지금은 배경 사각형을 그린 뒤 <c>content</c> 를 VisualBrush 없이 직접 Render 한다.
    /// (바깥 여백 Border 가 아니라 <c>.Child</c> 를 찍는 이유는 그대로다 — Margin 이 Border 자신의 오프셋으로
    /// 실려 그림이 오른쪽·아래로 잘리기 때문이다.)
    /// ⚠ <c>.Child</c> 로 옮겨도 안심할 수 없다(device 콘솔의 다이얼로그 갤러리에서 실측) — 자식이
    /// <c>HorizontalAlignment/VerticalAlignment=Center</c> 라 Border 를 꽉 채우지 않으면, 그 자식도 제
    /// 부모(Border) 안에서 가운데로 밀린 만큼 제 오프셋을 갖는다 — 곧이곧대로 Render 하면 이번엔 자식 자신이
    /// 잘린다. 그래서 <see cref="SaveVisual"/> 로 그 오프셋만큼 캔버스를 더 크게 잡아 찍은 뒤 자식의 사각만
    /// 오려낸다 — Stretch 로 꽉 채우는 화면(오프셋 0)이든 가운데 정렬(오프셋 ≠0)이든 같은 경로로 항상 옳다.
    /// </summary>
    private void Save(string directory, string name)
    {
        var content = (FrameworkElement)((Border)_window.Content).Child;
        SaveVisual(Path.Combine(directory, name + ".png"), content, _window.Background);
        PreviewTools.Shared.ClipAudit.Frame(directory, name, content);
    }

    /// <summary>
    /// 어떤 요소든, 제 부모 안에서의 배치 위치(오프셋)에 상관없이 정확히 찍는다. 요소의 부모 기준 오프셋만큼
    /// 캔버스를 더 크게 잡아 직접 Render 한 뒤, 요소 자신의 사각만 오려낸다(VisualBrush 없이).
    /// </summary>
    private static void SaveVisual(string path, FrameworkElement element, Brush background)
    {
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var parent = VisualTreeHelper.GetParent(element) as Visual;
        var offset = parent != null ? element.TransformToAncestor(parent).Transform(new Point(0, 0)) : new Point(0, 0);

        var dpi = VisualTreeHelper.GetDpi(element);
        var canvasWidth = offset.X + width;
        var canvasHeight = offset.Y + height;
        var pixelWidth = System.Math.Max(1, (int)System.Math.Ceiling(canvasWidth * dpi.DpiScaleX));
        var pixelHeight = System.Math.Max(1, (int)System.Math.Ceiling(canvasHeight * dpi.DpiScaleY));

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

        var backdrop = new DrawingVisual();
        using (var dc = backdrop.RenderOpen())
            dc.DrawRectangle(background, null, new Rect(0, 0, canvasWidth, canvasHeight));
        bitmap.Render(backdrop);
        bitmap.Render(element);

        var cropX = System.Math.Max(0, (int)System.Math.Round(offset.X * dpi.DpiScaleX));
        var cropY = System.Math.Max(0, (int)System.Math.Round(offset.Y * dpi.DpiScaleY));
        var cropW = System.Math.Max(1, System.Math.Min((int)System.Math.Ceiling(width * dpi.DpiScaleX), pixelWidth - cropX));
        var cropH = System.Math.Max(1, System.Math.Min((int)System.Math.Ceiling(height * dpi.DpiScaleY), pixelHeight - cropY));

        BitmapSource final = cropX == 0 && cropY == 0 && cropW == pixelWidth && cropH == pixelHeight
            ? bitmap
            : new CroppedBitmap(bitmap, new Int32Rect(cropX, cropY, cropW, cropH));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
    #endregion
}
