using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 영역마다 3D 줌(2026-10-18) — 영역별 게이트 · 줌 선택지 · 굽기 요청 enter_zoom · 장면 목록 해석 · 줌 바꾸기(PATCH, 404/405 → 로컬) ·
/// 로컬 우선 규칙 · 로컬 설정 파일.
/// </summary>
public class Map3DAreaZoomTests
{
    // ── 영역별 게이트 ──

    [Fact]
    public void should_keep_settings_gate_when_area_zoom_equals_settings()
        => Assert.Equal(new Map3DZoomGate(17, 16.5, 15), Map3DMath.AreaGate(17, 17, 16.5, 15));

    [Theory]
    [InlineData(15.0, 14.5, 14.0)]   // 영역 진입 15 → 이탈 14.5 · 미리 준비 min(15, 14) = 14
    [InlineData(16.0, 15.5, 15.0)]
    [InlineData(19.0, 18.5, 15.0)]   // 높이면 미리 준비는 세팅 그대로
    public void should_shift_exit_and_cap_preload_when_area_zoom_differs(double areaEnter, double exit, double preload)
        => Assert.Equal(new Map3DZoomGate(areaEnter, exit, preload), Map3DMath.AreaGate(areaEnter, 17, 16.5, 15));

    [Fact]
    public void should_keep_settings_hysteresis_width_when_settings_gap_is_wider()
        => Assert.Equal(new Map3DZoomGate(18, 17, 15), Map3DMath.AreaGate(18, 17, 16, 15));

    [Theory]
    [InlineData(15.4, false, false)]
    [InlineData(15.5, false, true)]    // 영역 진입 15.5 — 세팅(17)보다 낮은 줌에서 3D
    [InlineData(15.0, true, true)]     // 3D 였으면 이탈 15.0 까지 유지
    [InlineData(14.9, true, false)]
    public void should_enter_and_leave_at_area_zooms_when_gate_decided(double zoom, bool wasActive, bool expected)
    {
        var gate = Map3DMath.AreaGate(15.5, 17, 16.5, 15);
        Assert.Equal(expected, Map3DMath.Decide(zoom, wasActive, hasScene: true, gate.Enter, gate.Exit));
    }

    [Fact]
    public void should_leave_3d_below_area_exit_when_camera_reports_zoom()
    {
        var gate = Map3DMath.AreaGate(19, 17, 16.5, 15);
        Assert.True(Map3DMath.ShouldLeave3D(18.4, gate.Exit));
        Assert.False(Map3DMath.ShouldLeave3D(18.5, gate.Exit));
    }

    // ── 로컬 우선 규칙 ──

    [Theory]
    [InlineData(16.0, 18.0, 17.0, 16.0)]           // 로컬 > 서버
    [InlineData(null, 18.0, 17.0, 18.0)]           // 서버 > 세팅
    [InlineData(null, null, 17.0, 17.0)]           // 아무것도 없으면 세팅
    [InlineData(double.NaN, 18.0, 17.0, 18.0)]     // 수가 아닌 로컬은 건너뛴다
    [InlineData(null, double.PositiveInfinity, 16.5, 16.5)]
    [InlineData(null, 3.0, 17.0, 10.0)]            // 범위 밖은 10~20.5 로 자른다
    [InlineData(25.0, null, 17.0, 20.5)]
    public void should_prefer_local_then_server_then_settings_when_resolving_area_zoom(double? local, double? server, double settings, double expected)
        => Assert.Equal(expected, Map3DMath.ResolveAreaEnterZoom(local, server, settings));

    // ── 줌 선택지 ──

    [Fact]
    public void should_offer_half_steps_from_15_to_19_when_choices_listed()
        => Assert.Equal(new[] { 15, 15.5, 16, 16.5, 17, 17.5, 18, 18.5, 19 }, Map3DGuide.EnterZoomChoices);

    [Theory]
    [InlineData(17.0, 17.0)]
    [InlineData(17.2, 17.0)]
    [InlineData(17.25, 17.5)]
    [InlineData(12.0, 15.0)]   // 세팅이 선택지 밖이면 가까운 끝
    [InlineData(20.5, 19.0)]
    [InlineData(double.NaN, 17.0)]
    public void should_pick_nearest_choice_when_default_zoom_given(double zoom, double expected)
        => Assert.Equal(expected, Map3DGuide.EnterZoomChoiceFor(zoom));

    // ── 표시 글 ──

    [Fact]
    public void should_show_area_zoom_when_tooltip_and_menu_header_built()
    {
        string tip = Map3DGuide.AreaToolTip("본부", new DateTime(2026, 10, 18, 9, 30, 0), 12, 16.5);
        Assert.StartsWith(Map3DGuide.AreaToolTip("본부", new DateTime(2026, 10, 18, 9, 30, 0), 12), tip);
        Assert.EndsWith("\n3D 로 바뀌는 줌: 16.5 이상", tip);
        Assert.Equal("본부 · 줌 18 이상", Map3DGuide.AreaMenuHeader("본부", 18));
        Assert.Equal("3D 가능 — 줌 15.5 이상", Map3DGuide.ChipTextFor(15.5));
    }

    // ── 굽기 요청 본문 ──

    [Fact]
    public void should_add_enter_zoom_to_body_when_zoom_chosen()
    {
        var bbox = new[] { 127.0, 37.0, 127.01, 37.01 };
        var tiles = Map3DBake.BuildBody(bbox, Map3DBake.ModeTiles, null, 16.5);
        Assert.Equal(16.5, tiles["enter_zoom"]!.Value<double>());
        Assert.Null(tiles["overlay_mode"]);

        var composite = Map3DBake.BuildBody(bbox, Map3DBake.ModeComposite, Array.Empty<Map3DBakeOverlay>(), 18);
        Assert.Equal(18, composite["enter_zoom"]!.Value<double>());
        Assert.Equal("extract", composite.Value<string>("overlay_mode"));   // 옛 값 composite = extract(2026-10-18)
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    public void should_leave_enter_zoom_out_when_zoom_missing(double? zoom)
        => Assert.Null(Map3DBake.BuildBody(new[] { 0.0, 0, 1, 1 }, Map3DBake.ModeTiles, null, zoom)["enter_zoom"]);

    [Fact]
    public void should_keep_old_body_when_old_overload_used()
        => Assert.Equal(Map3DBake.BuildBody(new[] { 0.0, 0, 1, 1 }, "extract", null).ToString(),
                        Map3DBake.BuildBody(new[] { 0.0, 0, 1, 1 }, "extract", null, null).ToString());

    // ── 서버 창구 ──

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = new();
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!.ToString(), body));
            return _respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task should_send_enter_zoom_when_bake_started_with_zoom()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.Accepted, "{\"id\":\"j1\",\"status\":\"queued\"}"));
        using var c = new Map3DClient("http://h", h);

        await c.StartBakeAsync(new[] { 127.0, 37.0, 127.01, 37.01 }, Map3DBake.ModeTiles, null, 15.5);

        Assert.Equal(15.5, JObject.Parse(h.Requests[0].Body!)["enter_zoom"]!.Value<double>());
    }

    [Fact]
    public void should_read_enter_zoom_when_scene_list_has_it()
    {
        var scenes = Map3DClient.ParseScenes(
            "{\"scenes\":[{\"id\":\"a\",\"bbox\":[0,0,1,1],\"enter_zoom\":16.5},{\"id\":\"b\",\"bbox\":[0,0,1,1]},"
            + "{\"id\":\"c\",\"bbox\":[0,0,1,1],\"enter_zoom\":\"16\"},{\"id\":\"d\",\"bbox\":[0,0,1,1],\"enter_zoom\":18}]}");

        Assert.Equal(new double?[] { 16.5, null, null, 18 }, new[] { scenes[0].EnterZoom, scenes[1].EnterZoom, scenes[2].EnterZoom, scenes[3].EnterZoom });
    }

    [Fact]
    public async Task should_read_enter_zoom_when_scene_found_at_point()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"a\",\"bbox\":[0,0,1,1],\"enter_zoom\":15}"));
        using var c = new Map3DClient("http://h", h);

        var scene = await c.FindSceneAtAsync(0.5, 0.5);

        Assert.Equal(15, scene!.EnterZoom);
    }

    [Fact]
    public async Task should_patch_enter_zoom_and_return_true_when_server_stores_it()
    {
        var h = new FakeHandler(_ => Json(HttpStatusCode.OK, "{\"id\":\"s1\",\"enter_zoom\":18.5}"));
        using var c = new Map3DClient("http://h/", h);

        Assert.True(await c.UpdateSceneEnterZoomAsync("s1", 18.5));

        var (method, url, body) = h.Requests[0];
        Assert.Equal("PATCH", method.Method);
        Assert.Equal("http://h/api/jobs/s1", url);
        Assert.Equal("{\"enter_zoom\":18.5}", body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    public async Task should_return_false_for_local_fallback_when_server_does_not_know_patch(HttpStatusCode code)
    {
        using var c = new Map3DClient("http://h", new FakeHandler(_ => Json(code, "{\"detail\":\"Method Not Allowed\"}")));
        Assert.False(await c.UpdateSceneEnterZoomAsync("a", 16));
    }

    [Fact]
    public async Task should_throw_request_exception_when_server_rejects_zoom()
    {
        using var c = new Map3DClient("http://h", new FakeHandler(_ => Json(HttpStatusCode.UnprocessableEntity, "{\"detail\":\"줌 범위 밖\"}")));
        var ex = await Assert.ThrowsAsync<Map3DRequestException>(() => c.UpdateSceneEnterZoomAsync("a", 30));
        Assert.Equal("줌 범위 밖", ex.Message);
    }

    // ── GIS 로컬 설정(map3d-areas.json) ──

    [Fact]
    public void should_parse_only_finite_numbers_when_local_file_read()
    {
        var map = Map3DAreaZoomStore.Parse("{\"a\":16.5,\"b\":\"17\",\"c\":18,\"d\":null}");
        Assert.Equal(new Dictionary<string, double> { ["a"] = 16.5, ["c"] = 18 }, map);
        Assert.Empty(Map3DAreaZoomStore.Parse("깨진 파일"));
        Assert.Empty(Map3DAreaZoomStore.Parse("[1,2]"));
    }

    [Fact]
    public void should_round_trip_and_remove_when_local_zoom_saved()
    {
        string dir = Path.Combine(Path.GetTempPath(), "map3d-areas-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "Gis", "map3d-areas.json");
        try
        {
            var store = new Map3DAreaZoomStore(path);
            Assert.Null(store.Get("a"));             // 파일이 없으면 빈 목록
            store.Set("a", 16.5);
            store.Set("b", 18);

            var reloaded = new Map3DAreaZoomStore(path);
            Assert.Equal(16.5, reloaded.Get("a"));
            Assert.Equal(18, reloaded.Get("b"));
            Assert.True(reloaded.Remove("a"));
            Assert.False(reloaded.Remove("a"));
            Assert.Null(new Map3DAreaZoomStore(path).Get("a"));
            Assert.False(File.Exists(path + ".tmp"), "임시 파일은 바꿔 끼운 뒤 남지 않는다");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void should_use_local_zoom_over_server_when_server_did_not_store_it()
    {
        string dir = Path.Combine(Path.GetTempPath(), "map3d-areas-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "map3d-areas.json");
        try
        {
            var store = new Map3DAreaZoomStore(path);
            store.Set("a", 15);
            Assert.Equal(15, Map3DMath.ResolveAreaEnterZoom(store.Get("a"), serverZoom: 18, settingsEnterZoom: 17));
            Assert.Equal(18, Map3DMath.ResolveAreaEnterZoom(store.Get("other"), serverZoom: 18, settingsEnterZoom: 17));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void should_point_default_path_into_local_app_data_when_store_created()
        => Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ironwall", "Gis", "map3d-areas.json"),
                        Map3DAreaZoomStore.DefaultPath);
}
