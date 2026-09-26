using Ironwall.Dotnet.Libraries.Events.Ui.Views.Consoles;
using System;
using System.Threading;
using Xunit;
using LvcCartesianChart = LiveChartsCore.SkiaSharpView.WPF.CartesianChart;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 이벤트 창을 닫았다 다시 열면 GIS 앱이 통째로 죽던 결함의 회귀망.
/// 실창 로그(2026-09-26 22:44:14): "Unhandled exception: Core not set yet." ←
/// LiveChartsCore.SkiaSharpView.WPF.Chart.get_CoreChart ← EventOverviewView.PushChartRect ← OnChartLoaded.
/// 뷰의 Loaded 가 차트 자신의 코어 생성보다 먼저 오면 CoreChart 가 던진다 — 코어가 없는 차트를 만들어 재현한다.
/// </summary>
public class EventOverviewChartCoreTests
{
    [Fact]
    public void should_throw_core_not_set_when_chart_core_is_read_before_the_chart_loads()
    {
        // 전제(결함의 원인): 코어가 만들어지기 전의 차트에서 CoreChart 를 읽으면 던진다.
        Exception? thrown = null;
        RunOnSta(() =>
        {
            var chart = new LvcCartesianChart();
            try { _ = chart.CoreChart; }
            catch (Exception ex) { thrown = ex; }
        });

        Assert.NotNull(thrown);
        Assert.True(EventOverviewView.IsCoreNotReady(thrown!), $"예상 밖 메시지: {thrown!.GetType().Name} — {thrown.Message}");
    }

    [Fact]
    public void should_report_not_ready_instead_of_throwing_when_chart_core_is_missing()
    {
        bool? ready = null;
        RunOnSta(() =>
        {
            var chart = new LvcCartesianChart();
            ready = EventOverviewView.TryReadDrawMargin(chart, out _, out _, out _, out _);
        });

        Assert.False(ready);
    }

    [Fact]
    public void should_not_swallow_other_exceptions_when_classifying_chart_errors()
    {
        Assert.False(EventOverviewView.IsCoreNotReady(new InvalidOperationException("다른 오류")));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(done.Wait(TimeSpan.FromSeconds(20)), "STA 스레드가 제시간에 끝나지 않았다");
        Assert.Null(failure);
    }
}
