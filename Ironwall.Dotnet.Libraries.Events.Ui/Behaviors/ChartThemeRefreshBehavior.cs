using System;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Theme.Services;
using LiveChartsCore.Measure;
using MaterialDesignThemes.Wpf;
using LvcChart = LiveChartsCore.SkiaSharpView.WPF.Chart;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Behaviors;
/****************************************************************************
   Purpose      : 차트 호버 툴팁의 테마 액티브 리프레시 (event-chart-theme FR-8, 사용자 지시).
                  툴팁은 차트 컨트롤이 호버 시점에 만드는 뷰 요소라, VM의 페인트 재할당만으론
                  "이미 떠 있는" 툴팁 박스가 구테마로 잔존한다 → 차트가 IThemeService.ThemeChanged를
                  직접 수신해 TooltipPosition을 Hidden으로 눌러 열린 툴팁을 즉시 소멸시키고,
                  다음 프레임에 원래 위치로 복원한다(재호버/재표시 시 새 테마 페인트로 재생성).
   Note         : Loaded/Unloaded 쌍으로 구독 수명 관리(해제-후-구독 — 탭 전환 재로드 중복 방지).
                  IThemeService 미등록 환경(테스트/부트 전)은 조용히 no-op.
   Created On   : 2026-08-07 · Sensorway Co., Ltd.
 ****************************************************************************/
public static class ChartThemeRefreshBehavior
{
    /// <summary>XAML: behavior_inner:ChartThemeRefreshBehavior.IsEnabled="True" (CartesianChart/PieChart 공용).</summary>
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ChartThemeRefreshBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    // 구독 해제용 핸들러 보관(차트 인스턴스별)
    private static readonly DependencyProperty HandlerProperty =
        DependencyProperty.RegisterAttached("Handler", typeof(EventHandler<BaseTheme>), typeof(ChartThemeRefreshBehavior),
            new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LvcChart chart) return;
        if ((bool)e.NewValue)
        {
            chart.Loaded += Chart_Loaded;
            chart.Unloaded += Chart_Unloaded;
            if (chart.IsLoaded) Subscribe(chart);
        }
        else
        {
            chart.Loaded -= Chart_Loaded;
            chart.Unloaded -= Chart_Unloaded;
            Unsubscribe(chart);
        }
    }

    private static void Chart_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is LvcChart chart) Subscribe(chart);
    }

    private static void Chart_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is LvcChart chart) Unsubscribe(chart);
    }

    private static void Subscribe(LvcChart chart)
    {
        var svc = TryResolveThemeService();
        if (svc == null) return;

        Unsubscribe(chart);   // 해제-후-구독(재로드 중복 방지)
        EventHandler<BaseTheme> handler = (_, _) => RefreshTooltip(chart);
        chart.SetValue(HandlerProperty, handler);
        svc.ThemeChanged += handler;
    }

    private static void Unsubscribe(LvcChart chart)
    {
        if (chart.GetValue(HandlerProperty) is not EventHandler<BaseTheme> handler) return;
        chart.SetValue(HandlerProperty, null);
        var svc = TryResolveThemeService();
        if (svc != null) svc.ThemeChanged -= handler;
    }

    /// <summary>열린 툴팁 강제 소멸 → 다음 프레임 위치 복원(새 테마 페인트로 재생성 유도).</summary>
    private static void RefreshTooltip(LvcChart chart)
        => chart.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var pos = chart.TooltipPosition;
            if (pos == TooltipPosition.Hidden) return;   // 원래 숨김 정책이면 무개입
            chart.TooltipPosition = TooltipPosition.Hidden;
            chart.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => chart.TooltipPosition = pos);
        });

    private static IThemeService? TryResolveThemeService()
    {
        try { return IoC.Get<IThemeService>(); }
        catch { return null; }
    }
}
