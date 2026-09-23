using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 개요(T3) 시간대별 추이 — LiveChartsCore(CartesianChart) 로 옮긴 뒤의 데이터 계약(Series/XAxes)을 검사한다.
/// </summary>
/// <remarks>
/// <para>이 파일은 원래 손으로 그린 폴리라인의 <b>화면좌표</b>(TrendSeries.Points/TrendLabels.X)가 플롯 폭 0
/// 으로 뭉치는 결함(2026-09-23 — Label 래퍼가 Stretch 를 안 줘 TrendHost.ActualWidth 가 0에 머문 버그)을 잡았다.
/// LiveCharts 로 옮기며 그 픽셀 좌표 계산 자체가 사라졌다(차트가 제 <c>ActualWidth</c>로 스스로 배치·그린다) —
/// 즉 <b>이 결함군은 구조적으로 재발할 수 없다.</b> 계약이 Series.Values/XAxes.Labels 로 바뀌었으므로
/// 단언 대상도 옮긴다: Resize 가 오든 안 오든, 어느 순서로 오든 Load() 만으로 다섯 계열과 시간 축 라벨이
/// 채워져야 한다(빈 차트로 보이는 회귀를 잡는다).</para>
/// <para>PlotLeft/PlotWidth/PlotHeight(드래그 픽셀 수학)는 <see cref="EventOverviewViewModel.Resize"/> 에만
/// 의존한다. 2026-09-24 세 번째 실기 캡처 이후로는 뷰가 고정 여백을 추측해 빼는 대신 차트가 <b>실측</b>한
/// 그림 영역(<c>CoreChart.DrawMarginLocation</c>/<c>DrawMarginSize</c>)을 그대로 <c>Resize</c> 로 밀어 준다 —
/// 그래서 <c>Resize</c> 는 이제 (left, top, width, height) 네 값을 받고, 뷰모델은 그 값을 그대로 되돌려 줄
/// 뿐 더 이상 빼기를 하지 않는다. 이 값이 어긋나면 기간 드래그가 차트가 실제로 그린 자리와 안 맞게 된다.
/// 마지막 테스트가 Load() 가 그 값을 덮지 않는지를 별도로 지킨다.</para>
/// </remarks>
public class EventOverviewViewModelTrendGeometryTests
{
    private static EventDashboardDto BuildDashboard()
    {
        var start = new DateTime(2026, 9, 22, 0, 0, 0);
        var buckets = Enumerable.Range(0, 24).Select(h => new EventTrendItemDto
        {
            TimeBucket = start.AddHours(h).ToString("O"),
            SensorDetection = 3 + (h * 7) % 9,
            // 원래 1 + (h*5)%5 였다 — (h*5)%5 는 h 와 무관하게 항상 0 이라 카메라 계열이 24시간 내내
            // 값 1 로 고정되는 퇴화 데이터였다(예전 단언이 픽셀 X 위치만 봐서 안 드러났다). 실데이터라면
            // 흔치 않은 모양이라 값 자체가 뜨도록 고친다.
            CameraDetection = 1 + (h * 3 + 2) % 5,
            Malfunction = h % 5 == 0 ? 2 : h % 3 == 0 ? 1 : 0,
            Connection = h % 4 == 0 ? 2 : 0,
            Action = 1 + (h * 3) % 4,
            Alert = h % 3,
        }).ToList();

        return new EventDashboardDto
        {
            Summary = new EventSummaryDto
            {
                DaysInRange = 1,
                SensorDetection = buckets.Sum(b => b.SensorDetection),
                CameraDetection = buckets.Sum(b => b.CameraDetection),
                Malfunction = buckets.Sum(b => b.Malfunction),
                Connection = buckets.Sum(b => b.Connection),
                Action = buckets.Sum(b => b.Action),
                Alert = buckets.Sum(b => b.Alert),
            },
            Trend = new EventTrendDto { Interval = "hour", Series = buckets },
        };
    }

    private static void AssertSeriesPopulated(EventOverviewViewModel model)
    {
        Assert.Equal(EventSeriesSpec.All.Count, model.Series.Count);   // 센서 탐지 · 카메라 탐지 · 사전 경보 · 장애 · 연결 · 조치

        foreach (var series in model.Series.Cast<LineSeries<int>>())
        {
            var values = series.Values!.ToList();
            Assert.Equal(24, values.Count);
            Assert.True(values.Distinct().Count() > 1,
                $"{series.Name} 의 Values 가 한 값으로 뭉쳐 있다(빈 차트처럼 보인다).");
        }

        Assert.Single(model.XAxes);
        var labels = model.XAxes[0].Labels;
        Assert.NotNull(labels);
        Assert.Equal(24, labels!.Count);
        Assert.True(labels.Distinct().Count() > 1,
            "XAxes 의 시간 라벨이 전부 같은 값이다(시간 축이 한 지점에 뭉쳐 보인다).");
    }

    [Fact]
    public void should_populate_series_when_resize_arrives_before_load()
    {
        var model = new EventOverviewViewModel();

        // 뷰가 차트의 첫 UpdateFinished 에서 먼저 실측 여백을 알려 준다 — 데이터는 아직 없다.
        model.Resize(plotLeft: 44, plotTop: 12, plotWidth: 720, plotHeight: 220);

        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        AssertSeriesPopulated(model);
    }

    [Fact]
    public void should_populate_series_when_load_arrives_before_resize()
    {
        var model = new EventOverviewViewModel();

        // 통계가 먼저 도착한다(레일 전환 시점 Overview.Load) — 차트는 아직 측정 전이다.
        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        // 차트가 UpdateFinished 에서 실측 여백을 나중에 알려 준다.
        model.Resize(plotLeft: 44, plotTop: 12, plotWidth: 720, plotHeight: 220);

        AssertSeriesPopulated(model);
    }

    [Fact]
    public void should_populate_series_when_resize_is_never_called()
    {
        // 뷰가 Resize 를 한 번도 못 부른 경우(차트가 아직 한 번도 측정을 못 끝낸 경우)에도
        // Series/XAxes 는 채워져야 한다 — 더 이상 플롯 크기에 의존하지 않는다.
        var model = new EventOverviewViewModel();

        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        AssertSeriesPopulated(model);
    }

    [Fact]
    public void should_report_the_exact_rect_resize_was_given_without_reinterpreting_it()
    {
        // 뷰모델은 더 이상 여백을 추측해 빼지 않는다 — Resize 로 받은 값을 그대로 되돌려 줄 뿐이다.
        // (예전엔 여기서 "720 - 고정 상수" 를 기대했는데, 그 고정 상수가 실제 차트 여백과 어긋난 게
        // 세 번째 실기 캡처까지 이어진 라벨 잘림/겹침의 원인이었다.)
        var model = new EventOverviewViewModel();

        model.Resize(plotLeft: 44, plotTop: 12, plotWidth: 588, plotHeight: 158);

        Assert.Equal(44, model.PlotLeft);
        Assert.Equal(12, model.PlotTop);
        Assert.Equal(588, model.PlotWidth);
        Assert.Equal(158, model.PlotHeight);
    }

    [Fact]
    public void should_keep_plot_rect_from_resize_when_load_runs_afterward()
    {
        // 드래그 픽셀 수학(PlotLeft/PlotWidth)은 Resize 에만 의존한다 — Load() 가 이를 덮으면
        // 끌어서 고른 자리가 차트가 실제로 그린 자리와 어긋난다.
        var model = new EventOverviewViewModel();
        model.Resize(plotLeft: 44, plotTop: 12, plotWidth: 720, plotHeight: 220);
        Assert.Equal(44, model.PlotLeft);
        Assert.Equal(720, model.PlotWidth);

        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        Assert.Equal(44, model.PlotLeft);
        Assert.Equal(720, model.PlotWidth);
    }

    [Fact]
    public void should_clamp_negative_measured_size_to_zero()
    {
        // 차트가 아직 완전히 안 그려진 과도기(측정값이 잠깐 음수/0 미만으로 흔들리는 경우) 방어.
        var model = new EventOverviewViewModel();

        model.Resize(plotLeft: 0, plotTop: 0, plotWidth: -5, plotHeight: -5);

        Assert.Equal(0, model.PlotWidth);
        Assert.Equal(0, model.PlotHeight);
    }

    [Fact]
    public void should_drop_series_when_its_chip_is_toggled_off()
    {
        var model = new EventOverviewViewModel();
        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));
        Assert.Equal(EventSeriesSpec.All.Count, model.Series.Count);

        var sensorSlice = model.Slices.Single(s => s.Spec.Key == "sensor");
        sensorSlice.IsOn = false;

        Assert.Equal(EventSeriesSpec.All.Count - 1, model.Series.Count);
        Assert.DoesNotContain(model.Series.Cast<LineSeries<int>>(), s => s.Name == "센서 탐지");
    }
}
