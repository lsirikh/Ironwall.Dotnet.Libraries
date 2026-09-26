using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : Width="*" DataGrid 열이 MinWidth 없이 WPF 기본값(20px)까지
                  눌리던 결함(Reports.Ui 8fa2cb5e에서 실증)의 재발 방지.
                  EventDashboardView.xaml(4개 별 열)과
                  EventSuppressionSchedulePanelView.xaml(작업명 별 열)이
                  대상 — 두 뷰 모두 열을 코드비하인드가 아니라 정적 XAML로
                  선언하므로, 순수 함수 대신 소스 XAML 텍스트를 직접 검사한다
                  (UnitTest.cs의 기존 "XAML 텍스트 직접 검사" 관례를 따른다).
   Created By   : GHLee
   Created On   : 2026-09-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventDataGridColumnFloorTests
{
    private const string StarColumnMinWidth = "140";

    private static string ReadViewXaml(params string[] relativeSegments)
    {
        var segments = new[] { AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".." }
            .Concat(relativeSegments)
            .ToArray();
        var path = Path.Combine(segments);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// XAML 소스 안 모든 &lt;DataGridTextColumn&gt;/&lt;DataGridTemplateColumn&gt; 여는 태그에서
    /// Width 와 MinWidth 를 뽑는다. 별 열(Width="*")은 <see cref="StarColumnMinWidth"/> 를,
    /// 고정 열은 자기 자신의 Width 값을 MinWidth 로 기대한다 — 8fa2cb5e 가 실측한 대로
    /// 고정 열도 걸어 두지 않으면 리사이즈 도중 과도 폭에 영구히 눌어붙는다.
    /// </summary>
    private static IEnumerable<(string Width, string? MinWidth)> ExtractColumnFloors(string xaml)
    {
        foreach (Match tag in Regex.Matches(xaml, @"<DataGrid(?:Text|Template)Column\b[^>]*>", RegexOptions.Singleline))
        {
            var widthMatch = Regex.Match(tag.Value, @"Width=""([^""]+)""");
            if (!widthMatch.Success) continue; // Width 없는 열은 이 회귀 대상이 아니다

            var minWidthMatch = Regex.Match(tag.Value, @"MinWidth=""([^""]+)""");
            yield return (widthMatch.Groups[1].Value, minWidthMatch.Success ? minWidthMatch.Groups[1].Value : null);
        }
    }

    [Fact]
    public void should_floor_every_column_at_its_designed_width_when_event_dashboard_grids_render()
    {
        // Arrange
        var xaml = ReadViewXaml("Views", "Dashboards", "EventDashboardView.xaml");

        // Act
        var columns = ExtractColumnFloors(xaml).ToList();

        // Assert — 4개 탐지/장애/연결/조치 그리드를 합쳐 26개 열이 있다.
        // U-18: 탐지 그리드의 장비 별 열 바닥은 140 이 아니라 120 이다(고정 합 954 가 1340 표면을 넘어 폭을 줄이고 덜 중요한 열은 접는다) —
        // 별 열은 그리드마다 하나(반올림이 쌓이면 2px 가로 스크롤이 선다), 바닥은 값이 한 줄로 읽히는 폭(80 이상)이다.
        Assert.Equal(26, columns.Count);
        var starColumns = columns.Where(c => c.Width.EndsWith("*", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, starColumns.Count);
        foreach (var (width, minWidth) in columns)
        {
            Assert.True(minWidth is not null, $"Width=\"{width}\" 열에 MinWidth 가 없다 — WPF 기본값 20px 로 눌릴 수 있다.");
            if (width.EndsWith("*", StringComparison.Ordinal))
                Assert.True(double.Parse(minWidth!, System.Globalization.CultureInfo.InvariantCulture) >= 80, $"별 열 바닥 {minWidth} 는 값을 못 읽는다.");
            else
                Assert.Equal(width, minWidth);
        }
    }

    /// <summary>
    /// U-18 — 탐지 그리드의 가로 스크롤 회귀 가드(GIS 실창 1340: 결과 · 신호가 스크롤 밖). 목록 실효 폭마다
    /// 접히지 않고 남는 열의 바닥 합이 뷰포트(세로 스크롤막대 10 을 뺀 폭) 안에 들어가야 한다.
    /// </summary>
    [Theory]
    [InlineData(816)]   // 1340 도킹(상세 340)
    [InlineData(966)]   // 1150 서랍 닫힘
    [InlineData(606)]   // 1150 서랍 열림
    [InlineData(844)]   // 900 접힘 · 서랍 닫힘
    [InlineData(484)]   // 900 접힘 · 서랍 열림
    public void should_fit_the_detection_columns_that_remain_at_each_list_width(double listWidth)
    {
        // Arrange — 탐지 그리드 한 벌만 떼어 낸다
        var xaml = ReadViewXaml("Views", "Dashboards", "EventDashboardView.xaml");
        var start = xaml.IndexOf("AutomationProperties.AutomationId=\"Console.Events.Grid.Detection\"", StringComparison.Ordinal);
        var end = xaml.IndexOf("</DataGrid>", start, StringComparison.Ordinal);
        var grid = xaml[start..end];

        // Act — 남는 열의 바닥 합(고정 열은 제 폭, 별 열은 MinWidth)
        double sum = 0;
        // 여는 태그만(<DataGridTemplateColumn.CellTemplate> 같은 속성 요소는 뺀다 — 이름 뒤에 공백이 와야 한다).
        foreach (Match tag in Regex.Matches(grid, @"<DataGrid(?:Text|Template)Column\s[^>]*>", RegexOptions.Singleline))
        {
            var min = Regex.Match(tag.Value, @"\bMinWidth=""([^""]+)""");
            var below = Regex.Match(tag.Value, @"CollapseBelow=""([^""]+)""");
            var threshold = below.Success ? double.Parse(below.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            if (threshold > 0 && listWidth < threshold) continue;
            sum += double.Parse(min.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        // Assert
        Assert.True(sum <= listWidth - 10, $"목록 {listWidth}: 남는 열 바닥 합 {sum} 이 뷰포트를 넘는다 — 가로 스크롤이 선다.");
    }

    [Fact]
    public void should_floor_job_name_star_column_at_140_when_suppression_schedule_grid_renders()
    {
        // Arrange
        var xaml = ReadViewXaml("Views", "Panels", "EventSuppressionSchedulePanelView.xaml");

        // Act
        var columns = ExtractColumnFloors(xaml).ToList();

        // Assert — 작업명 하나만 별 열이고, 나머지 7개 고정 열도 제 폭을 바닥으로 건다.
        Assert.Equal(8, columns.Count);
        var starColumns = columns.Where(c => c.Width == "*").ToList();
        var starColumn = Assert.Single(starColumns);
        Assert.Equal(StarColumnMinWidth, starColumn.MinWidth);
        foreach (var (width, minWidth) in columns)
        {
            Assert.True(minWidth is not null, $"Width=\"{width}\" 열에 MinWidth 가 없다 — WPF 기본값 20px 로 눌릴 수 있다.");
            var expected = width == "*" ? StarColumnMinWidth : width;
            Assert.Equal(expected, minWidth);
        }
    }
}
