using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>3D 성능 계측 — WPF 프레임 간격 요약 · perf.stats 해석 · 로그 한 줄 · 원격이면 네이티브 창.</summary>
public class Map3DPerfTests
{
    [Fact]
    public void should_summarize_avg_p95_max_when_samples_taken()
    {
        var s = new Map3DPerfSamples();
        for (int i = 1; i <= 100; i++) s.Add(i);
        Assert.Equal("50.5/95.0/100×100", s.Take());
        Assert.Equal("-", s.Take());   // 꺼내면 비운다
    }

    [Fact]
    public void should_count_each_wpf_frame_once_and_skip_long_gaps_when_rendering_ticks()
    {
        var s = new Map3DPerfSamples();
        var t = TimeSpan.FromMilliseconds(1000);
        s.AddFrame(t);
        s.AddFrame(t);                                    // 같은 프레임에 두 번 — 한 번만
        s.AddFrame(t + TimeSpan.FromMilliseconds(20));
        s.AddFrame(t + TimeSpan.FromMilliseconds(60));
        s.AddFrame(t + TimeSpan.FromMilliseconds(3000));  // 숨김 · 멈춤 틈 — 빼고 기준만 옮긴다
        s.AddFrame(t + TimeSpan.FromMilliseconds(3010));
        Assert.Equal("23.3/40.0/40.0×3", s.Take());
        s.Reset();
        s.AddFrame(t + TimeSpan.FromMilliseconds(9000));  // 다시 펼침 — 앞 기준과의 틈을 세지 않는다
        Assert.Equal("-", s.Take());
    }

    [Fact]
    public void should_read_perf_lines_when_viewer_sends_stats()
    {
        var m = Map3DMath.ParseMessage("{\"type\":\"perf.stats\",\"lines\":[\"move 5.0s frames=40 → gpu|present\",\"idle 5.0s frames=2 → ok\",3]}");
        Assert.NotNull(m);
        Assert.Equal("perf.stats", m!.Type);
        Assert.Equal(new[] { "move 5.0s frames=40 → gpu|present", "idle 5.0s frames=2 → ok" }, m.PerfLines);
        Assert.Null(Map3DMath.ParseMessage("{\"type\":\"perf.stats\",\"lines\":[]}"));
        var longLine = Map3DMath.ParseMessage("{\"type\":\"perf.stats\",\"lines\":[\"" + new string('x', 900) + "\"]}");
        Assert.Equal(600, longLine!.PerfLines![0].Length);
    }

    [Fact]
    public void should_join_gis_values_and_viewer_lines_when_logging_perf()
    {
        string line = Map3DPerf.LogLine(new[] { "move a", "idle b" }, "hwnd", 1600.4, 900, true, 0, "16.7/33.0/50.0×290", "-");
        Assert.Equal("[Map3D][perf] host=hwnd 1600x900 remote=true tier=0 wpf=16.7/33.0/50.0×290 sync2d=- | move a | idle b", line);
    }

    [Theory]
    [InlineData(true, true, false, true)]    // 로컬 — 설정대로 WPF 안
    [InlineData(true, true, true, false)]    // 원격 — 네이티브 창
    [InlineData(true, false, true, true)]    // 원격이어도 HwndWhenRemote 끔 — WPF 안
    [InlineData(false, false, false, false)] // 설정이 네이티브 창
    public void should_pick_native_window_when_remote_session(bool useComposition, bool hwndWhenRemote, bool remote, bool expected)
        => Assert.Equal(expected, Map3DPerf.UseComposition(useComposition, hwndWhenRemote, remote));
}
