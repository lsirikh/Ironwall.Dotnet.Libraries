using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 개요(T3) 시간대별 추이 차트 — 플롯 크기(Resize)와 데이터(Load)가 <b>어느 순서로 도착해도</b>
/// 도형(TrendSeries.Points · TrendLabels.X)이 실제로 펼쳐져야 한다.
/// 회귀: 플롯이 완전히 비고 X 축 라벨이 왼쪽 한 점에 뭉치는 결함(버그 리포트 2026-09-23).
/// </summary>
public class EventOverviewViewModelTrendGeometryTests
{
    private static EventDashboardDto BuildDashboard()
    {
        var start = new DateTime(2026, 9, 22, 0, 0, 0);
        var buckets = Enumerable.Range(0, 24).Select(h => new EventTrendItemDto
        {
            TimeBucket = start.AddHours(h).ToString("O"),
            SensorDetection = 3 + (h * 7) % 9,
            CameraDetection = 1 + (h * 5) % 5,
            Malfunction = h % 5 == 0 ? 2 : h % 3 == 0 ? 1 : 0,
            Connection = h % 4 == 0 ? 2 : 0,
            Action = 1 + (h * 3) % 4,
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
            },
            Trend = new EventTrendDto { Interval = "hour", Series = buckets },
        };
    }

    private static void AssertGeometrySpread(EventOverviewViewModel model)
    {
        Assert.NotEmpty(model.TrendSeries);
        foreach (var series in model.TrendSeries)
        {
            var distinctX = series.Points.Select(p => p.X).Distinct().Count();
            Assert.True(distinctX > 1,
                $"{series.Name} 의 Points 가 X={series.Points.FirstOrDefault().X} 한 점에 뭉쳐 있다(플롯이 빈 것처럼 보인다).");
        }

        Assert.NotEmpty(model.TrendLabels);
        var distinctLabelX = model.TrendLabels.Select(l => l.X).Distinct().Count();
        Assert.True(distinctLabelX > 1,
            "TrendLabels 의 X 가 전부 같은 값이다 — 시간 라벨이 한 지점에 겹쳐 찍힌다.");
    }

    [Fact]
    public void should_spread_trend_geometry_when_resize_arrives_before_load()
    {
        var model = new EventOverviewViewModel();

        // 뷰가 Loaded 시점에 먼저 실제 크기를 알려 준다 — 데이터는 아직 없다.
        model.Resize(720, 220);

        var dto = BuildDashboard();
        model.Load(dto, new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        AssertGeometrySpread(model);
    }

    [Fact]
    public void should_spread_trend_geometry_when_load_arrives_before_resize()
    {
        var model = new EventOverviewViewModel();

        // 통계가 먼저 도착한다(레일 전환 시점 Overview.Load) — 뷰의 실제 폭은 아직 모른다.
        var dto = BuildDashboard();
        model.Load(dto, new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        // 뷰가 Loaded/SizeChanged 에서 실제 크기를 나중에 알려 준다.
        model.Resize(720, 220);

        AssertGeometrySpread(model);
    }

    [Fact]
    public void should_spread_trend_geometry_when_only_default_size_is_known()
    {
        // 뷰가 Resize 를 한 번도 못 부른 경우(PushSize 가 null Model 때문에 스킵된 경우)에도
        // 뷰모델 기본 플롯 크기(640x200)로는 최소한 도형이 뭉치면 안 된다.
        var model = new EventOverviewViewModel();

        var dto = BuildDashboard();
        model.Load(dto, new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        AssertGeometrySpread(model);
    }
}
