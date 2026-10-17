using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>2D → 3D 전환 빠르게 · 확실하게(2026-10-17) — 미리 불러오기 조건 · rendered 해석 · 펼침 시점 · 전환 시간 로그 · 이미지 캐시 키 · 인코딩 선택.</summary>
public class Map3DTransitionTests
{
    // ── rendered 메시지 ──

    [Fact]
    public void should_parse_rendered_with_job_and_items_when_page_drew_snapshot()
    {
        var msg = Map3DMath.ParseMessage("{\"type\":\"rendered\",\"job\":\"j1\",\"items\":21}")!;

        Assert.Equal("rendered", msg.Type);
        Assert.Equal("j1", msg.Job);
        Assert.Equal(21, msg.Items);
        Assert.Null(msg.Camera);
    }

    [Fact]
    public void should_parse_rendered_without_items_when_page_sends_string_json()
    {
        var msg = Map3DMath.ParseMessage("\"{\\\"type\\\":\\\"rendered\\\",\\\"job\\\":\\\"j2\\\"}\"")!;

        Assert.Equal("rendered", msg.Type);
        Assert.Equal("j2", msg.Job);
        Assert.Null(msg.Items);
    }

    // ── 미리 불러오기 ──

    [Theory]
    [InlineData(15.0, "s1", null, false, false, true)]     // 줌 15 · 영역 안 · 아직 안 엶
    [InlineData(16.5, "s1", "s0", false, false, true)]     // 다른 장면이 열려 있으면 바꿔 연다(한 번에 한 장면)
    [InlineData(14.9, "s1", null, false, false, false)]    // 줌 15 밑
    [InlineData(15.0, null, null, false, false, false)]    // 영역 밖
    [InlineData(15.0, "s1", "s1", false, false, false)]    // 이미 열었다(실패했어도 다시 열지 않는다)
    [InlineData(15.0, "s1", "s0", true, false, false)]     // 다른 장면을 불러오는 중
    [InlineData(15.0, "s1", null, false, true, false)]     // 3D 표시 · 굽는 중 · 입력 모드
    [InlineData(double.NaN, "s1", null, false, false, false)]
    public void should_preload_only_when_zoom15_inside_area_and_host_free(double zoom, string? scene, string? hostJob, bool loading, bool blocked, bool expected)
        => Assert.Equal(expected, Map3DMath.ShouldPreload(zoom, scene, hostJob, loading, blocked));

    // ── 펼침 시점 ──

    [Theory]
    [InlineData(false, false, 5000, Map3DRevealDecision.Wait)]      // ready 전
    [InlineData(true, false, 100, Map3DRevealDecision.Wait)]        // rendered 기다리는 중
    [InlineData(true, true, 0, Map3DRevealDecision.Rendered)]
    [InlineData(true, false, 1500, Map3DRevealDecision.TimedOut)]   // 1.5 s 넘으면 그냥 펼침
    public void should_reveal_after_rendered_or_timeout_when_ready(bool ready, bool rendered, int sinceReadyMs, Map3DRevealDecision expected)
        => Assert.Equal(expected, Map3DGuide.DecideReveal(ready, rendered, TimeSpan.FromMilliseconds(sinceReadyMs)));

    [Fact]
    public void should_use_point4_seconds_reuse_guide_and_1point5_seconds_rendered_timeout_when_spec()
    {
        Assert.Equal(TimeSpan.FromSeconds(0.4), Map3DGuide.MinReuseShow);
        Assert.Equal(TimeSpan.FromSeconds(1.5), Map3DGuide.RenderedTimeout);
        Assert.True(Map3DGuide.MinReuseShow < Map3DGuide.MinEnterShow);
    }

    // ── 전환 시간 로그 ──

    private static readonly DateTime T0 = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static DateTime At(double seconds) => T0.AddSeconds(seconds);

    [Fact]
    public void should_split_transition_time_by_stage_when_first_entry()
    {
        string line = Map3DGuide.TransitionTimingText(T0, At(4.36), At(4.73), At(5.65), At(5.80), At(5.85), prewarmed: false, preloaded: false);

        Assert.Equal("[Map3D] 전환 시간 total=5.85s webview=4.36s page=0.37s snapshot=0.92s rendered=0.15s reveal=0.05s (prewarmed=false, preloaded=false)", line);
    }

    [Fact]
    public void should_count_stages_done_before_entry_as_zero_when_preloaded()
    {
        string line = Map3DGuide.TransitionTimingText(T0, At(-60), At(-10), At(-9.5), At(-9.3), At(0.4), prewarmed: true, preloaded: true);

        Assert.Equal("[Map3D] 전환 시간 total=0.40s webview=0.00s page=0.00s snapshot=0.00s rendered=0.00s reveal=0.40s (prewarmed=true, preloaded=true)", line);
    }

    [Fact]
    public void should_mark_missing_rendered_with_dash_when_timed_out()
    {
        string line = Map3DGuide.TransitionTimingText(T0, At(-30), At(0.5), At(0.6), null, At(2.1), prewarmed: true, preloaded: false);

        Assert.Contains("rendered=- reveal=1.50s", line);
        Assert.Contains("total=2.10s webview=0.00s page=0.50s snapshot=0.10s", line);
    }

    [Fact]
    public void should_show_rendering_stage_text_when_waiting_for_rendered()
        => Assert.Equal("3D 화면을 그리는 중…", Map3DGuide.StageText("rendering"));

    // ── 이미지 디스크 캐시 키 · 인코딩 선택 ──

    [Fact]
    public void should_keep_same_cache_key_when_same_file_path_size_and_time()
    {
        var time = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        string key = Map3DOverlay.ImageCacheKey(@"C:\Maps\Anyang.png", 6_300_000, time);

        Assert.Equal(key, Map3DOverlay.ImageCacheKey(@"c:\maps\ANYANG.png", 6_300_000, time));   // 경로 대소문자 무시
        Assert.Matches("^[0-9a-f]{16}$", key);
    }

    [Fact]
    public void should_change_cache_key_when_file_size_time_or_path_changes()
    {
        var time = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        string key = Map3DOverlay.ImageCacheKey(@"C:\Maps\Anyang.png", 6_300_000, time);

        Assert.NotEqual(key, Map3DOverlay.ImageCacheKey(@"C:\Maps\Anyang.png", 6_300_001, time));
        Assert.NotEqual(key, Map3DOverlay.ImageCacheKey(@"C:\Maps\Anyang.png", 6_300_000, time.AddSeconds(1)));
        Assert.NotEqual(key, Map3DOverlay.ImageCacheKey(@"C:\Maps\Other.png", 6_300_000, time));
    }

    [Fact]
    public void should_detect_transparency_when_any_alpha_below_255()
    {
        Assert.False(Map3DOverlay.HasTransparentPixel(new byte[] { 1, 2, 3, 255, 9, 9, 9, 255 }));
        Assert.True(Map3DOverlay.HasTransparentPixel(new byte[] { 1, 2, 3, 255, 9, 9, 9, 254 }));
        Assert.False(Map3DOverlay.HasTransparentPixel(Array.Empty<byte>()));
    }

    [Fact]
    public void should_choose_png_when_transparent_and_jpeg_when_opaque()
    {
        Assert.Equal("image/png", Map3DOverlay.ImageMime(hasTransparency: true));
        Assert.Equal("image/jpeg", Map3DOverlay.ImageMime(hasTransparency: false));
        Assert.Equal("data:image/jpeg;base64,AQI=", Map3DOverlay.DataUrl(new byte[] { 1, 2 }, "image/jpeg"));
        Assert.Equal(2048, Map3DOverlay.CachedImageSide);
        Assert.Equal(85, Map3DOverlay.JpegQuality);
    }
}
