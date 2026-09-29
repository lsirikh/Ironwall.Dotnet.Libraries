using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Theme.Services;
using LiveChartsCore.Kernel.Sketches;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LvcCartesianChart = LiveChartsCore.SkiaSharpView.WPF.CartesianChart;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;

/// <summary>
/// 개요(T3) — 추이 차트 위를 <b>좌우로 끌어 기간을 고른다</b>(정본 all-windows-drag-wireframe.html L299 · L427).
/// </summary>
/// <remarks>
/// <para><b>캡처 드래그</b>다 — OLE <c>DoDragDrop</c> 은 쓰지 않는다. 데드존 · 종료 순서 · ESC 판정은
/// <see cref="TrendDragStateMachine"/>(순수)이 쥐고, 뷰는 그 지시를 실행만 한다.</para>
/// <para>★ ESC 구독은 <b>창</b>(<c>Window.PreviewKeyDown</c>)에 건다. 누르는 대상이 포커스를 받지 않는
/// <c>Border</c> 라 <c>CaptureMouse</c> 로는 키보드 포커스가 오지 않고, UserControl 에 건 터널은
/// 그 경로를 지나가지 않아 <b>영원히 안 온다</b>(N-07 적대 검토 R3 — 커널 <c>CaptureDragBehavior</c> 와 같은 방식).</para>
/// <para>구독은 <b>누를 때 걸고</b> 종료 단계 ③에서 뗀다 — 끌지 않는 동안 창의 ESC 를 넘겨다보지 않는다.</para>
/// </remarks>
public partial class EventOverviewView : UserControl
{
    private readonly TrendDragStateMachine _drag = new();
    private FrameworkElement? _plot;
    private LvcCartesianChart? _chart;
    private Window? _keyHost;
    private IThemeService? _themeService;

    public EventOverviewView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
        Unloaded += (_, _) =>
        {
            FinishDrag(_drag.LostCapture());
            UnsubscribeTheme();
            if (_chart is not null)
            {
                _chart.UpdateFinished -= OnChartUpdateFinished;
                DetachChartData(_chart);
            }
        };
    }

    /// <summary>차트가 뷰모델 컬렉션을 관찰하는 속성들 — 셋 다 뷰모델의 <c>ObservableCollection</c> 에 묶여 있다.</summary>
    private static readonly (DependencyProperty Property, object Empty)[] ChartDataProperties =
    {
        (LvcCartesianChart.SeriesProperty, Array.Empty<LiveChartsCore.ISeries>()),
        (LvcCartesianChart.XAxesProperty, Array.Empty<ICartesianAxis>()),
        (LvcCartesianChart.YAxesProperty, Array.Empty<ICartesianAxis>()),
    };

    /// <summary>
    /// 창에서 떨어질 때 차트를 뷰모델 컬렉션에서 떼어 낸다.
    /// </summary>
    /// <remarks>
    /// LiveCharts2 차트는 Series · XAxes · YAxes 컬렉션의 CollectionChanged 에 <b>강한 참조</b>로 관찰자를 걸고 Unloaded 에도 풀지 않는다.
    /// 개요 뷰모델은 콘솔과 함께 오래 살아서, 이벤트 창을 닫았다 열 때마다 옛 차트 → 옛 개요 뷰 → 옛 콘솔 뷰 전체가
    /// 그 관찰자에 붙들려 수거되지 않았다(시험 EventConsoleViewLifetimeTests 로 경로 확인). 축 · 계열도 차트마다의 그림 상태
    /// (축 구분선 사전 · 계열 점 캐시)를 차트 엔진을 열쇠로 쥐고 있어, 차트가 붙어 있지 않으면 스스로 지우지 않는다.
    /// 그래서 ① 요소마다 이 차트의 그림 상태를 지우고(<c>RemoveFromUI</c> — 차트가 요소를 뺄 때 스스로 부르는 것과 같다)
    /// ② 바인딩을 잠시 떼고 빈 값을 넣어 관찰자를 푼다 — 바인딩을 남겨 두면 뷰모델이 축을 다시 만들며 알릴 때(새 창의 테마 적용)
    /// 떨어진 차트가 컬렉션을 다시 읽어 관찰자를 도로 건다(실측). 다시 붙을 때(<see cref="OnChartLoaded"/>) 떼어 둔 바인딩을 돌려놓는다.
    /// </remarks>
    private void DetachChartData(LvcCartesianChart chart)
    {
        LiveChartsCore.Chart? core = null;
        try { core = chart.CoreChart; }
        catch (Exception ex) when (IsCoreNotReady(ex)) { }

        foreach (var (property, empty) in ChartDataProperties)
        {
            if (core is not null && chart.GetValue(property) is System.Collections.IEnumerable current)
                foreach (var element in current.OfType<LiveChartsCore.Kernel.IChartElement>().ToList()) element.RemoveFromUI(core);
            if (System.Windows.Data.BindingOperations.GetBindingBase(chart, property) is { } binding)
            {
                _detachedChartBindings[property] = binding;
                System.Windows.Data.BindingOperations.ClearBinding(chart, property);
            }
            chart.SetValue(property, empty);
        }
    }

    private void ReattachChartData(LvcCartesianChart chart)
    {
        foreach (var (property, binding) in _detachedChartBindings) chart.SetBinding(property, binding);
        _detachedChartBindings.Clear();
    }

    /// <summary>떨어질 때 떼어 둔 차트 바인딩 — 다시 붙을 때 그대로 돌려놓는다.</summary>
    private readonly Dictionary<DependencyProperty, System.Windows.Data.BindingBase> _detachedChartBindings = new();

    private EventOverviewViewModel? Model => DataContext as EventOverviewViewModel;

    /// <summary>
    /// 테마 구독 — IThemeService 는 부트 순서 때문에 생성자 시점엔 없을 수 있다(IoC 미준비).
    /// 뷰가 Loaded 됐다는 건 셸이 떴다는 뜻이라 이 시점엔 항상 있다. 해제-후-구독으로 재로드 중복을 막는다
    /// (ChartThemeRefreshBehavior 와 같은 방식). 싱글턴 VM 이라도 구독은 뷰 수명에 묶어 안전하다.
    /// </summary>
    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        _themeService = TryResolveThemeService();
        if (_themeService is null) return;

        _themeService.ThemeChanged -= OnAppThemeChanged;
        _themeService.ThemeChanged += OnAppThemeChanged;
        Model?.ApplyTheme(_themeService.Current);
    }

    private void OnAppThemeChanged(object? sender, BaseTheme theme) => Model?.ApplyTheme(theme);

    private void UnsubscribeTheme()
    {
        if (_themeService is null) return;
        _themeService.ThemeChanged -= OnAppThemeChanged;
        _themeService = null;
    }

    private static IThemeService? TryResolveThemeService()
    {
        try { return IoC.Get<IThemeService>(); }
        catch { return null; }
    }

    /// <summary>
    /// 드래그 픽셀 수학(<see cref="EventOverviewViewModel.PlotLeft"/>/<c>PlotWidth</c>)을 차트가 <b>실제로
    /// 측정한</b> 그림 영역(<c>CoreChart.DrawMarginLocation</c>/<c>DrawMarginSize</c>)으로 맞춘다 — 더 이상
    /// 고정 여백을 추측해 <c>DrawMargin</c> 에 박지 않는다(그 여백을 쓰던 예전 판은 한글 글리프 높이·DPI·
    /// 폰트 스케일을 몰라 세 번째 실기 캡처까지 라벨이 잘리거나 겹쳤다). 방향을 뒤집는다: 라이브러리가
    /// 제 폰트 메트릭으로 스스로 여백을 계산하게 두고(<c>DrawMargin</c> 미지정 = Auto), 그 결과를 읽어
    /// 뷰모델에 되먹인다 — 드래그가 차트를 따라가지, 차트가 추측을 따라가지 않는다.
    /// </summary>
    /// <remarks>
    /// <para><see cref="LvcCartesianChart.UpdateFinished"/> 는 매 렌더 패스(크기 변화 · 테마 재색칠 · 데이터
    /// 갱신) 뒤에 돈다 — 리사이즈마다 따로 손볼 필요가 없다. 콜백이 UI 스레드가 아닐 수 있어(SkiaSharp 렌더
    /// 루프, <c>ChartThemeRefreshBehavior</c> 의 기존 선례와 같은 이유) <c>Dispatcher</c> 를 거친다.</para>
    /// <para><c>Loaded</c> 시점엔 아직 첫 측정 전일 수도, 재방문이라 이미 측정된 채일 수도 있다 — 구독을
    /// 걸고 나서 한 번 즉시 읽어 둘 중 어느 쪽이어도 맞는 값(또는 0, 다음 UpdateFinished 로 바로 갱신됨)을
    /// 받는다.</para>
    /// </remarks>
    private void OnChartLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not LvcCartesianChart chart) return;
        _chart = chart;
        ReattachChartData(chart);                         // 떨어질 때 비워 둔 컬렉션을 다시 읽는다(처음이면 같은 값이라 변화 없음)
        chart.UpdateFinished -= OnChartUpdateFinished;   // 해제-후-구독(재로드 중복 방지)
        chart.UpdateFinished += OnChartUpdateFinished;
        // 즉시 읽지 않고 한 박자 미룬다 — 이 Loaded 가 차트 자신의 Loaded(코어 생성)보다 먼저 올 수 있다.
        chart.Dispatcher.BeginInvoke(PushChartRect, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnChartUpdateFinished(IChartView chartView)
    {
        var chart = _chart;
        if (chart is null) return;
        chart.Dispatcher.BeginInvoke(PushChartRect);
    }

    private void PushChartRect()
    {
        if (_chart is null || Model is null) return;
        if (!TryReadDrawMargin(_chart, out var x, out var y, out var w, out var h)) return;   // 다음 UpdateFinished 가 다시 부른다
        Model.Resize(x, y, w, h);
    }

    /// <summary>
    /// 차트가 실제로 잰 그림 영역을 읽는다. 코어가 아직 없으면 false.
    /// <para>★ 이벤트 창을 닫았다 다시 열면 이 뷰의 Loaded 가 차트의 코어 생성보다 먼저 와서
    /// <c>CoreChart</c> 가 "Core not set yet." 을 던졌고, 처리되지 않은 예외로 <b>GIS 앱 전체가 종료</b>됐다
    /// (실창 로그 2026-09-26 22:44:14, PushChartRect ← OnChartLoaded). LiveCharts2 는 준비 여부를 묻는 공개
    /// 속성이 없어 그 한 가지 예외만 좁게 거른다.</para>
    /// </summary>
    internal static bool TryReadDrawMargin(LvcCartesianChart chart, out double x, out double y, out double w, out double h)
    {
        x = y = w = h = 0;
        try
        {
            var core = chart.CoreChart;
            x = core.DrawMarginLocation.X; y = core.DrawMarginLocation.Y;
            w = core.DrawMarginSize.Width; h = core.DrawMarginSize.Height;
            return true;
        }
        catch (Exception ex) when (IsCoreNotReady(ex))
        {
            return false;
        }
    }

    internal static bool IsCoreNotReady(Exception ex) => ex.Message.StartsWith("Core not set", StringComparison.Ordinal);

    private void OnTrendLoaded(object sender, RoutedEventArgs e)
    {
        // 배선 탐색은 Loaded 에서 — OnAttached 시점에는 부모 체인이 없다.
        // 마우스 좌표 기준점으로만 쓴다 — 플롯 크기는 더 이상 이 Border 에서 재지 않는다(위 OnChartLoaded).
        _plot = sender as FrameworkElement;
    }

    private void OnTrendPressed(object sender, MouseButtonEventArgs e)
    {
        if (_plot is null || Model is null) return;

        var at = e.GetPosition(_plot);
        if (!_drag.Press(at.X, at.Y)) return;

        _plot.CaptureMouse();
        Subscribe();
    }

    private void OnTrendMouseMove(object sender, MouseEventArgs e)
    {
        if (_plot is null || Model is null) return;

        var at = e.GetPosition(_plot);
        if (_drag.Move(at.X, at.Y)) Model.UpdateBand(_drag.PressX, at.X);
    }

    private void OnTrendReleased(object sender, MouseButtonEventArgs e)
    {
        if (_plot is null) return;
        FinishDrag(_drag.Release(), e.GetPosition(_plot).X);
    }

    private void OnTrendLostCapture(object sender, MouseEventArgs e) => FinishDrag(_drag.LostCapture());

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        var (handled, finish) = _drag.Escape();
        if (!handled) return;               // 끄는 중이 아닐 때는 소비하지 않는다

        FinishDrag(finish);
        e.Handled = true;
    }

    /// <summary>상태 기계가 내린 지시를 순서대로 실행한다 — ②시각 ③구독 ④캡처 ⑤커밋.</summary>
    private void FinishDrag(TrendDragFinish finish, double releaseX = 0)
    {
        if (finish is { ClearBand: false, ReleaseCapture: false, Unsubscribe: false, Commit: false }) return;

        if (finish.ClearBand) Model?.ClearBand();
        if (finish.Unsubscribe) Unsubscribe();
        if (finish.ReleaseCapture && _plot?.IsMouseCaptured == true) _plot.ReleaseMouseCapture();
        if (finish.Commit) Model?.CommitBand(_drag.PressX, releaseX);
    }

    private void Subscribe()
    {
        Unsubscribe();
        _keyHost = Window.GetWindow(this);
        if (_keyHost is not null) _keyHost.PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void Unsubscribe()
    {
        if (_keyHost is null) return;
        _keyHost.PreviewKeyDown -= OnWindowPreviewKeyDown;
        _keyHost = null;
    }

    private void OnControllerGroup(object sender, RoutedEventArgs e)
    {
        if (Model is not null) Model.DeviceGroup = OverviewDeviceGroup.Controller;
    }

    private void OnCameraGroup(object sender, RoutedEventArgs e)
    {
        if (Model is not null) Model.DeviceGroup = OverviewDeviceGroup.Camera;
    }

    private void OnFacilityGroup(object sender, RoutedEventArgs e)
    {
        if (Model is not null) Model.DeviceGroup = OverviewDeviceGroup.Facility;
    }

    private void OnBarClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: EventDeviceBarViewModel bar }) Model?.Drill(bar);
    }

    /// <summary>
    /// 아래에 더 있으면 본문 끝을 흐린다(<see cref="OverviewScrollFade"/>). 흐림은 <b>내용 칸에만</b> 건다 —
    /// 스크롤 막대까지 흐리면 끝이 어디인지 막대가 알려 주지 못한다. 흐림은 테마 색이 아니라 불투명도라
    /// 라이트/다크 어디서나 배경을 따라간다.
    /// </summary>
    private void OnOverviewScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer scroller) return;

        var target = scroller.Template?.FindName("PART_ScrollContentPresenter", scroller) as UIElement ?? scroller;
        var height = target is FrameworkElement fe ? fe.ActualHeight : scroller.ViewportHeight;
        var start = OverviewScrollFade.FadeStart(height);

        if (!OverviewScrollFade.HasMoreBelow(scroller.ScrollableHeight, scroller.VerticalOffset) || start >= 1)
        {
            target.ClearValue(UIElement.OpacityMaskProperty);
            return;
        }

        // ★ 절대 좌표로 건다 — 상대(경계 상자) 좌표면 상자가 보이는 칸이 아니라 스크롤되는 내용 전체(화면 밖까지)라
        //   흐림이 화면 밖 맨 끝에 그려져 보이지 않았다(오프스크린 실측: 칸 647 · 내용 967).
        target.OpacityMask = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Colors.Black, 0),
                new GradientStop(Colors.Black, start),
                new GradientStop(Colors.Transparent, 1),
            },
            new Point(0, 0), new Point(0, height))
        {
            MappingMode = BrushMappingMode.Absolute,
        };
    }
}
