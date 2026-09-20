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

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var snapshotAt = System.Array.IndexOf(e.Args, "--snapshot");
        var directory = snapshotAt >= 0 && snapshotAt + 1 < e.Args.Length ? e.Args[snapshotAt + 1] : null;

        try
        {
            if (e.Args.Contains("--dark")) ApplyDark();

            _viewModel = Build(e.Args.Contains("--readonly"));
            _view = new ReportConsoleView { DataContext = _viewModel };
            _window = new Window
            {
                Title = "보고서 콘솔 미리보기",
                Width = 1320,
                Height = 820,
                Background = (Brush)FindResource("SurfaceBrush"),
                Content = new Border { Margin = new Thickness(12), Child = _view },
            };
            _window.Show();

            await ((IActivate)_viewModel).ActivateAsync();

            if (directory is null) return;

            Directory.CreateDirectory(directory);
            await RunSnapshotsAsync(directory, e.Args.Contains("--dark") ? "dark" : "light");
        }
        catch (System.Exception ex)
        {
            if (directory is null) MessageBox.Show(ex.ToString());
            else File.WriteAllText(Path.Combine(directory, "snapshot-error.txt"), ex.ToString());
        }

        if (directory is not null) Shutdown();
    }

    #region - Composition -
    private static ReportConsoleViewModel Build(bool readOnly)
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

        PreviewData.Fill(api);

        return new ReportConsoleViewModel(
            events, log, permission,
            new ReportListViewModel(events, log, api),
            new ReportCreateViewModel(events, log, api),
            new ReportTemplateViewModel(events, log, api),
            new ReportPreviewViewModel(events, log, api),
            new ReportTemplateEditViewModel(events, log, api));
    }
    #endregion

    #region - Snapshots -
    private async Task RunSnapshotsAsync(string directory, string theme)
    {
        // WebView2 는 오프스크린에서 그려지지 않는다 — 런타임이 없을 때와 같은 길로 내려 자리표시자를 찍는다.
        _viewModel.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(false);
        _viewModel.PreviewViewModel.ProbeRuntime();

        await Settle();
        Save(directory, $"{theme}-01-list-none");

        _viewModel.OnRowSelected(_viewModel.ListViewModel.Rows.First(r => r.Id == 104));
        await Settle();
        Save(directory, $"{theme}-02-list-selected");

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
        _window.Width = 1150;
        await Settle();
        _viewModel.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        _viewModel.PreviewViewModel.ProbeRuntime();
        await Settle();
        Save(directory, $"{theme}-13-drawer-1150");

        _window.Width = 900;
        await Settle();
        _viewModel.PreviewViewModel.RuntimeProbe = new FixedWebViewRuntimeProbe(true);
        _viewModel.PreviewViewModel.ProbeRuntime();
        await Settle();
        Save(directory, $"{theme}-14-compact-900");

        // 선택을 놓으면 서랍이 닫힌다 — 그래야 좁은 폭에서 툴바(상태 칩)와 목록이 실제로 어떻게 보이는지 안다.
        _viewModel.OnRowSelected(null);
        await Settle();
        Save(directory, $"{theme}-15-compact-900-nodrawer");

        _window.Width = 1150;
        await Settle();
        Save(directory, $"{theme}-16-drawer-1150-nodrawer");
    }

    private void ApplyDark()
    {
        if (Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == DarkTokens)) return;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new System.Uri(DarkTokens) });
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Dark;
    }

    private static Task Settle() => Task.Delay(420);

    private void Save(string directory, string name)
    {
        var content = (FrameworkElement)_window.Content;
        var width = (int)System.Math.Ceiling(content.ActualWidth);
        var height = (int)System.Math.Ceiling(content.ActualHeight);
        if (width <= 0 || height <= 0) return;

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(_window.Background, null, new Rect(0, 0, width, height));
            dc.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.None }, null, new Rect(0, 0, width, height));
        }
        bitmap.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(stream);
    }
    #endregion
}
