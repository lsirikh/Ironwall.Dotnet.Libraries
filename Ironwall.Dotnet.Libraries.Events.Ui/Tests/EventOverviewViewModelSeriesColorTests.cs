using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>
/// 시간대별 추이 차트의 계열 색이 계열 칩(<c>Console.Events.Overview.SeriesChips</c>)과 <b>같은 출처</b>에서
/// 나오는지 검증한다.
/// </summary>
/// <remarks>
/// <para>회귀 대상: 2026-09-24 라이브 캡처(<c>chart-live.png</c>)에서 차트 선이 <c>ChartHelper.TrendCategories</c>
/// 하드코딩 팔레트(#FFCD00 등)를 썼고, 칩은 테마 토큰 브러시(StatusWarningBrush=#B26A00 등)를 썼다 —
/// 완전히 다른 값이라 같은 계열인데 색이 안 맞았다. 지금은 둘 다
/// <c>EventOverviewViewModel.SeriesColor</c>(내부적으로 <see cref="EventOverviewViewModel.BrushTokenResolver"/>)
/// 를 거쳐 <b>같은 <see cref="EventSeriesSpec.BrushKey"/></b> 를 묻는다.</para>
/// <para>Application 이 없는 헤드리스 환경이라 실제 <c>Application.Current.TryFindResource</c> 는 부를 수 없다 —
/// <see cref="EventOverviewViewModel.BrushTokenResolver"/> 를 가짜 리소스 사전으로 갈아 끼워, "칩이 읽는 바로 그
/// 키를 실제로 물어봤는가"를 증명한다. 실제 렌더 픽셀(칩·차트가 같은 화면에서 같은 색으로 보이는가)은 라이브
/// 창에서만 확정된다 — 그건 이 테스트의 범위 밖이다.</para>
/// </remarks>
public sealed class EventOverviewViewModelSeriesColorTests : IDisposable
{
    private readonly Func<string, Brush?> _originalResolver = EventOverviewViewModel.BrushTokenResolver;

    // 실제 토큰 값과 일부러 다르게 잡는다 — 우연히 맞아떨어지는 값을 쓰면 "같은 키를 실제로 물어봤는가"를
    // 못 잡아낸다(예: 둘 다 우연히 파란색이면 하드코딩 팔레트를 써도 통과해 버린다).
    private static readonly Dictionary<string, Color> FakeTokens = new(StringComparer.Ordinal)
    {
        ["StatusWarningBrush"] = Color.FromArgb(255, 0x11, 0x22, 0x33),
        ["AccentBrush"] = Color.FromArgb(255, 0x44, 0x55, 0x66),
        ["StatusCriticalBrush"] = Color.FromArgb(255, 0x77, 0x88, 0x99),
        ["PrimaryBrush"] = Color.FromArgb(255, 0xAA, 0xBB, 0xCC),
        ["StatusNormalBrush"] = Color.FromArgb(255, 0xDD, 0xEE, 0xFF),
    };

    public EventOverviewViewModelSeriesColorTests()
    {
        EventOverviewViewModel.BrushTokenResolver = key =>
            FakeTokens.TryGetValue(key, out var c) ? new SolidColorBrush(c) : null;
    }

    public void Dispose() => EventOverviewViewModel.BrushTokenResolver = _originalResolver;

    private static EventDashboardDto BuildDashboard()
    {
        var start = new DateTime(2026, 9, 22, 0, 0, 0);
        var buckets = Enumerable.Range(0, 24).Select(h => new EventTrendItemDto
        {
            TimeBucket = start.AddHours(h).ToString("O"),
            SensorDetection = 1 + h % 5,
            CameraDetection = 1 + h % 3,
            Malfunction = h % 4,
            Connection = h % 2,
            Action = 1 + h % 6,
        }).ToList();

        return new EventDashboardDto
        {
            Summary = new EventSummaryDto { DaysInRange = 1 },
            Trend = new EventTrendDto { Interval = "hour", Series = buckets },
        };
    }

    [Fact]
    public void should_stroke_every_series_with_the_same_brush_key_the_chip_reads()
    {
        var model = new EventOverviewViewModel();
        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        Assert.Equal(5, model.Series.Count);
        Assert.Equal(5, model.Slices.Count);

        foreach (var slice in model.Slices)
        {
            // 칩이 실제로 읽는 키(Rectangle.conv:TokenBrushAssist.FillToken="{Binding BrushKey}")
            var expected = FakeTokens[slice.BrushKey];

            var series = Assert.Single(model.Series.Cast<LineSeries<int>>(), s => s.Name == slice.Name);
            var stroke = Assert.IsType<SolidColorPaint>(series.Stroke);
            var geometryFill = Assert.IsType<SolidColorPaint>(series.GeometryFill);

            Assert.Equal(expected.R, stroke.Color.Red);
            Assert.Equal(expected.G, stroke.Color.Green);
            Assert.Equal(expected.B, stroke.Color.Blue);

            Assert.Equal(expected.R, geometryFill.Color.Red);
            Assert.Equal(expected.G, geometryFill.Color.Green);
            Assert.Equal(expected.B, geometryFill.Color.Blue);
        }
    }

    [Fact]
    public void should_recolor_series_from_the_new_token_when_theme_changes()
    {
        var model = new EventOverviewViewModel();
        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        // 테마가 바뀌면(뷰가 IThemeService.ThemeChanged 를 받아 ApplyTheme 를 부른다) 리소스 사전 값도
        // 바뀐다 — 다음 조회에서 새 색을 받아야 한다(칩과 같은 이유로: DynamicResource 는 매번 다시 읽는다).
        EventOverviewViewModel.BrushTokenResolver = _ => new SolidColorBrush(Color.FromArgb(255, 0x01, 0x02, 0x03));

        model.ApplyTheme(MaterialDesignThemes.Wpf.BaseTheme.Dark);

        foreach (var series in model.Series.Cast<LineSeries<int>>())
        {
            var stroke = Assert.IsType<SolidColorPaint>(series.Stroke);
            Assert.Equal(0x01, stroke.Color.Red);
            Assert.Equal(0x02, stroke.Color.Green);
            Assert.Equal(0x03, stroke.Color.Blue);
        }
    }

    [Fact]
    public void should_fall_back_without_throwing_when_token_is_unresolvable()
    {
        // Application.Current 가 없는(또는 아직 안 뜬) 상황을 흉내 — 기본 리졸버가 실제로 겪는 경우다.
        EventOverviewViewModel.BrushTokenResolver = _ => null;

        var model = new EventOverviewViewModel();
        model.Load(BuildDashboard(), new DateTime(2026, 9, 22), new DateTime(2026, 9, 23));

        Assert.Equal(5, model.Series.Count);   // 예외 없이, 대체색으로만
    }
}
