using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 서버 콘솔 창(지표 이력 · 확인)이 요청한 크기로 뜨는가.
/// </summary>
/// <remarks>
/// 2026-09-30 사용자 보고: 지표 이력 창이 화면 폭만큼 늘고 머리 줄 높이로 납작했다 — Caliburn 은 뷰를 새 창에 담을 때
/// <c>SizeToContent=WidthAndHeight</c> 로 만들어 Width · Height 설정을 무시하는데, 서버 창 입구만 <c>SizeToContent.Manual</c> 을 넣지 않았다.
/// 빈 목록 안내 글도 표 머리("디스크") 위에 겹쳤다.
/// </remarks>
public class ServerConsoleDialogSizingTests
{
    private sealed class CapturingWindows : IWindowManager
    {
        public List<(object Model, IDictionary<string, object>? Settings)> Shown { get; } = new();

        public Task<bool?> ShowDialogAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null)
        {
            Shown.Add((rootModel, settings));
            return Task.FromResult<bool?>(false);
        }

        public Task ShowWindowAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
        public Task ShowPopupAsync(object rootModel, object? context = null, IDictionary<string, object>? settings = null) => Task.CompletedTask;
    }

    [Fact]
    public async Task should_open_metric_history_at_the_requested_size_when_the_window_manager_wraps_the_view()
    {
        var windows = new CapturingWindows();
        var dialogs = new ServerConsoleDialogs(windows, new FakeServerConsoleService(), new FixedClock(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)));

        await dialogs.ShowMetricHistoryAsync(7, "VMS-ab1120");

        var settings = Assert.Single(windows.Shown).Settings!;
        Assert.Equal(SizeToContent.Manual, settings["SizeToContent"]);
        Assert.Equal(560d, settings["Width"]);
        Assert.Equal(520d, settings["Height"]);
    }

    [Fact]
    public async Task should_open_the_confirm_prompt_at_the_requested_size_when_the_window_manager_wraps_the_view()
    {
        var windows = new CapturingWindows();
        var dialogs = new ServerConsoleDialogs(windows, new FakeServerConsoleService(), new FixedClock(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)));

        await dialogs.ConfirmAsync("확인", "보낼까요?");

        Assert.Equal(SizeToContent.Manual, Assert.Single(windows.Shown).Settings!["SizeToContent"]);
    }

    [Fact]
    public void should_place_the_empty_text_below_the_grid_header_when_there_are_no_records()
    {
        var xaml = File.ReadAllText(ViewPath());
        var empty = Regex.Match(xaml, @"<TextBlock\b[^>]*AutomationId=""Servers\.MetricHistory\.Empty""[^>]*/>", RegexOptions.Singleline);
        Assert.True(empty.Success);

        var top = double.Parse(Regex.Match(empty.Value, @"Margin=""[\d.]+,([\d.]+),").Groups[1].Value);
        Assert.True(top >= 44, $"빈 목록 안내 글 위 여백 {top} — 표 머리 줄(약 36) 아래여야 한다");
    }

    private static string ViewPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "Consoles", "Servers", "ServerMetricHistoryView.xaml");
}
