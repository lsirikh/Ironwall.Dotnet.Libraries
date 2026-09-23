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

        // Assert — 4개 탐지/장애/연결/조치 그리드를 합쳐 26개 열이 있고, 그중 4개가 별 열이다.
        Assert.Equal(26, columns.Count);
        var starColumns = columns.Where(c => c.Width == "*").ToList();
        Assert.Equal(4, starColumns.Count);
        foreach (var (width, minWidth) in columns)
        {
            Assert.True(minWidth is not null, $"Width=\"{width}\" 열에 MinWidth 가 없다 — WPF 기본값 20px 로 눌릴 수 있다.");
            var expected = width == "*" ? StarColumnMinWidth : width;
            Assert.Equal(expected, minWidth);
        }
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
