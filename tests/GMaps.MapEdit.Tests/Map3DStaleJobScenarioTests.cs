using System.Collections;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 지운 · 다시 만든 3D 장면의 옛 job 참조(2026-10-19 사용자 보고 — 3D 가운데 "불러오지 못했습니다: TypeError…" + 옛 장면 「건물 높이」 판,
/// map-3d 기록: DELETE 뒤 지운 job 의 scene.json 을 다시 불러 404) 와 지도 회전 때 3D 영역 테두리가 그대로 남던 결함 — 실제 MapViewModel 경로.
/// 창 · WebView2 는 만들지 않는다(3D 호스트에는 ready 상태만 꽂는다). map-3d 는 가짜 HttpMessageHandler.
/// </summary>
public class Map3DStaleJobScenarioTests
{
    private const string Old = "20261019-182721-023f6d", New = "20261019-183014-f366a1";

    // ─────────────── 장치 ───────────────

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();
        /// <summary>GET /api/scenes/at 이 돌려줄 장면(지우기 전에 보낸 조회의 늦은 응답 흉내).</summary>
        public JObject? SceneAt { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            lock (Requests) Requests.Add((request.Method, path));
            if (request.Method == HttpMethod.Post && path == "/api/jobs")
                return Json(HttpStatusCode.Accepted, new JObject { ["id"] = New, ["status"] = "done", ["progress"] = 1.0 });
            if (request.Method == HttpMethod.Delete) return Json(HttpStatusCode.OK, new JObject { ["deleted"] = path.Split('/')[^1] });
            if (path == "/api/scenes/at")
                return SceneAt == null ? Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" }) : Json(HttpStatusCode.OK, SceneAt);
            // 목록(GET /api/jobs)은 이 시험 밖 — 받은 것으로 친 목록은 ApplyMap3DAreas 로 직접 단다(배경 스레드에서 지도를 만지지 않게)
            return Json(HttpStatusCode.ServiceUnavailable, new JObject { ["detail"] = "목록은 이 시험 밖" });
        }

        public bool Got(HttpMethod method, string path) { lock (Requests) return Requests.Any(r => r.Method == method && r.Path == path); }

        private static Task<HttpResponseMessage> Json(HttpStatusCode code, JObject body)
            => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") });
    }

    private sealed class Rig
    {
        public Map3DEntryScenarioTests.Rig R { get; }
        public MapViewModel Vm => R.Vm;
        public FakeServer Server { get; } = new();
        public List<JObject> Posts { get; } = new();

        public Rig(string mode = Map3DModel.EntryManual)
        {
            R = new Map3DEntryScenarioTests.Rig(mode);
            R.Settings.OverlayMode = Map3DBake.ModeTiles;   // 만들기 확정이 「마지막 굽기 방식」을 설정 파일에 저장하지 않게(겹치는 이미지 없음 = 타일)
            Scenario.SetField(Vm, "_bhFloorHeightText", "3.0");
            Scenario.SetField(Vm, "_bhFloorsText", "");
            Scenario.SetField(Vm, "_bhHeightText", "");
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(json => Posts.Add(JObject.Parse(json))));
        }

        public Map3DSceneInfo Area(string id) => R.AreaAround(id);

        /// <summary>미리 불러오기가 일어나지 않는 줌(15.5 &lt; 미리 준비 줌 16) — 큐에 쌓인 게이트가 돌아도 WebView2 를 만들지 않게. 수동 방식은 단추로 들어간다.</summary>
        public void NoPreload() { R.Settings.PreloadZoom = 16; R.SetZoom(15.5); }

        /// <summary>3D 가 보낸 메시지 그대로(JSON → Map3DMath.ParseMessage → OnMap3DMessage).</summary>
        public void From3D(JObject json)
        {
            var msg = Map3DMath.ParseMessage(json.ToString());
            Assert.NotNull(msg);
            Scenario.Call(Vm, "OnMap3DMessage", msg);
        }

        /// <summary>옛 장면으로 「3D 로 보기」 → 펼침 → 건물을 골라 「건물 높이」 판을 연다.</summary>
        public void ShowWithBuildingPanel(string job)
        {
            R.HostReady(job);
            R.Gate();
            R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(R);
            Assert.True(R.Shown);
            Assert.Equal(job, R.Job);
            Map3DEntryScenarioTests.UseEditMode(R);   // 2026-10-21 — 건물 고르기 · 그리기는 지도 편집 모드에서만
            From3D(new JObject { ["type"] = "building", ["id"] = 172, ["origin"] = "map", ["height"] = 9.0, ["height_source"] = "levels", ["height_from"] = "levels",
                                 ["levels"] = null, ["area_m2"] = 120.0, ["lon"] = 127.0, ["lat"] = 37.5, ["manual"] = false });
            Assert.True(Vm.IsBuildingHeightPanelOpen);
            Assert.Equal(job, Scenario.GetField<string?>(Vm, "_bhJob"));
        }

        public Map3DScene? SceneAtCenter() => (Map3DScene?)Scenario.Call(Vm, "FindMap3DSceneAt", R.Center);
        public Map3DViewHost Host => Vm.Map3DHost;
    }

    private static void Run(Action<Rig> body, string mode = Map3DModel.EntryManual)
        => Scenario.Sta(() => { var rig = new Rig(mode); body(rig); rig.R.Done(); });

    private static void RunAsync(Func<Rig, Task> body, string mode = Map3DModel.EntryManual)
        => Scenario.StaAsync(async () => { var rig = new Rig(mode); await body(rig); rig.R.Done(); });

    private static async Task Until(Func<bool> condition, string what)
    {
        for (int i = 0; i < 300 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition(), what);
    }

    /// <summary>
    /// VM 의 3D · 「건물 높이」 상태 필드 전부(_map3D* · _bh* · 영역 목록)와 3D 호스트에서 그 job 을 가리키는 것 — 비어야 "옛 job 참조 0".
    /// 지운 job 을 기억하는 _map3DGoneJobs 만 뺀다(그 job 을 되살리지 않으려고 일부러 기억한다).
    /// </summary>
    private static List<string> RefsTo(MapViewModel vm, string job)
    {
        var hits = new List<string>();
        foreach (var f in typeof(MapViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            bool stateField = f.Name.StartsWith("_map3D", StringComparison.Ordinal) || f.Name.StartsWith("_bh", StringComparison.Ordinal)
                              || f.Name == "<Map3DAreas>k__BackingField";
            if (!stateField || f.Name == "_map3DGoneJobs" || f.Name == "_map3DHost") continue;
            if (Mentions(f.GetValue(vm), job, 0)) hits.Add(f.Name);
        }
        if (vm.Map3DHost.Job == job || vm.Map3DHost.IsReadyFor(job)) hits.Add("Map3DHost");
        return hits;
    }

    private static bool Mentions(object? v, string job, int depth)
    {
        if (v == null || depth > 3) return false;
        if (v is string s) return s == job;
        if (v is IDictionary d)
        {
            foreach (DictionaryEntry e in d) if (Mentions(e.Key, job, depth + 1) || Mentions(e.Value, job, depth + 1)) return true;
            return false;
        }
        if (v is IEnumerable list)
        {
            foreach (var x in list) if (Mentions(x, job, depth + 1)) return true;
            return false;
        }
        foreach (var name in new[] { "Id", "Job" })
            if (v.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } p
                && p.PropertyType == typeof(string) && (string?)p.GetValue(v) == job) return true;
        return v.GetType().GetProperty("Item") is { } item && item.PropertyType == typeof(Map3DAreaItem) && Mentions(item.GetValue(v), job, depth + 1);
    }

    private static void AssertNoRefs(Rig rig, string job) => Assert.True(RefsTo(rig.Vm, job).Count == 0, $"옛 job 참조 남음: {string.Join(", ", RefsTo(rig.Vm, job))}");

    // ─────────────── 1. 다시 만들기 ───────────────

    [Theory]
    [InlineData(false)]   // 2D — 옛 장면이 3D 호스트에 1×1 로 열려 있다(미리 불러오기 · 나온 뒤)
    [InlineData(true)]    // 3D 로 옛 장면을 보는 중에 다시 만들기
    public void should_drop_every_reference_to_old_job_and_delete_it_when_rebake_completes(bool showingOld)
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.Area(Old));
            rig.ShowWithBuildingPanel(Old);
            if (!showingOld) rig.R.PressExit();
            Assert.Equal(Old, rig.Host.Job);

            Scenario.Call(rig.Vm, "RebakeMap3DArea", rig.Vm.Map3DAreas.Single(a => a.Id == Old));
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Scenario.Call(rig.Vm, "ConfirmMap3DBakeOptions");
            await Until(() => !Scenario.GetField<bool>(rig.Vm, "_isMap3DBaking") && rig.Server.Got(HttpMethod.Delete, $"/api/jobs/{Old}"), "다시 만들기 끝 + 옛 job 지움");

            Assert.True(rig.Server.Got(HttpMethod.Post, "/api/jobs"));
            Assert.Equal("Off", rig.R.State);                         // 옛 장면을 보던 3D 는 2D 로
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            AssertNoRefs(rig, Old);
            Assert.Equal(New, rig.SceneAtCenter()!.Id);               // 같은 자리 = 새 장면

            // 지우기 전에 보낸 목록 요청의 늦은 응답(옛 영역이 아직 있다) — 되살리지 않는다
            rig.R.Areas(rig.Area(New), rig.Area(Old));
            AssertNoRefs(rig, Old);
            Assert.Equal(new[] { New }, rig.Vm.Map3DAreas.Select(a => a.Id).ToArray());

            // 「3D 로 보기」 = 새 장면(미리 불러온 새 페이지 — WebView2 없이)
            rig.R.HostReady(New);
            rig.R.Gate();
            rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            Assert.Equal(New, rig.R.Job);
            AssertNoRefs(rig, Old);
        });

    [Fact]
    public void should_pick_newest_scene_first_when_bake_finishes_over_old_overlapping_area()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.Area(Old));
            rig.R.HostReady(Old);
            rig.R.Gate();
            // 새 영역 그리기(다시 만들기 아님) — 옛 영역은 지우지 않는다
            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(rig.Area(New).Bbox, null));
            Scenario.Call(rig.Vm, "ConfirmMap3DBakeOptions");
            await Until(() => !Scenario.GetField<bool>(rig.Vm, "_isMap3DBaking"), "만들기 끝");

            Assert.False(rig.Server.Got(HttpMethod.Delete, $"/api/jobs/{Old}"), "영역 그리기는 옛 영역을 지우지 않는다");
            Assert.Equal(New, rig.SceneAtCenter()!.Id);   // 겹치면 새 장면이 먼저
            rig.R.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            rig.R.HostReady(New);
            rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.Equal(New, rig.R.Job);
        });

    // ─────────────── 2. 지우기 ───────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_unload_host_and_never_revive_area_when_delete_confirmed(bool showing)
        => RunAsync(async rig =>
        {
            rig.R.Areas(rig.Area(Old));
            if (showing) rig.ShowWithBuildingPanel(Old);
            else { rig.R.HostReady(Old); rig.R.Gate(); }   // 미리 불러온 옛 장면(1×1)
            Assert.True(rig.Host.IsReadyFor(Old));

            Scenario.SetField(rig.Vm, "_pendingMap3DAreaDelete", rig.Vm.Map3DAreas.Single(a => a.Id == Old));
            await rig.Vm.HandleAsync(new CallDeleteMap3DAreaProcessMessageModel(), CancellationToken.None);

            Assert.True(rig.Server.Got(HttpMethod.Delete, $"/api/jobs/{Old}"));
            Assert.Equal("Off", rig.R.State);
            Assert.Null(rig.Host.Job);                         // 재사용 · 네이티브 창 전환 다시 열기로 지운 장면을 다시 부르지 않는다
            AssertNoRefs(rig, Old);
            Assert.Contains("지웠습니다", rig.R.Status);

            // 지우기 전에 보낸 장면 조회의 늦은 응답 · 목록의 늦은 응답 — 되살리지 않는다
            rig.Server.SceneAt = new JObject { ["id"] = Old, ["bbox"] = new JArray(rig.Area(Old).Bbox) };
            await (Task)Scenario.Call(rig.Vm, "LookupMap3DSceneAsync")!;
            rig.R.Areas(rig.Area(Old));
            AssertNoRefs(rig, Old);
            Assert.Null(rig.SceneAtCenter());

            // 게이트 · 단추 — 지운 장면으로 들어가거나 미리 불러오지 않는다
            rig.R.SetZoom(18); rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
            rig.R.PressEnter();
            Assert.Equal("Off", rig.R.State);
            Assert.Null(rig.Host.Job);
        });

    [Theory]
    [InlineData(Map3DModel.EntryAuto)]
    [InlineData(Map3DModel.EntryManual)]
    public void should_leave_to_2d_with_notice_when_shown_job_disappears_from_server_list(string mode)
        => Run(rig =>
        {
            rig.R.Areas(rig.Area(Old));
            rig.ShowWithBuildingPanel(Old);

            rig.R.Areas(rig.Area(New));   // 다른 곳에서 옛 영역을 지우고 새로 만들었다
            Assert.Equal("Off", rig.R.State);
            Assert.False(rig.Vm.IsMap3DShown);
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Equal(Map3DGuide.SceneMissingText, rig.R.Status);
            AssertNoRefs(rig, Old);

            rig.R.HostReady(New);   // 새 장면 페이지가 이미 준비돼 있어도
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown, "같은 줌에서 곧바로 새 장면으로 다시 들어가지 않는다(안내를 읽게)");
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "새 장면은 「3D 로 보기」로");
        }, mode);

    // ─────────────── 3. 3D 화면의 error 메시지(map-3d sceneload.js) ───────────────

    [Theory]
    [InlineData("Shown")]
    [InlineData("Pending")]
    [InlineData("Parked")]   // 2D — 1×1 로 미리 불러오던 페이지가 실패를 알렸다
    public void should_retire_job_and_refresh_list_when_viewer_reports_scene_missing(string state)
        => RunAsync(async rig =>
        {
            rig.R.Areas(rig.Area(Old));
            if (state == "Shown") rig.ShowWithBuildingPanel(Old);
            else
            {
                rig.R.HostLoading(Old);
                rig.R.Gate();
                if (state == "Pending") { rig.R.PressEnter(); Assert.Equal("Pending", rig.R.State); }
            }

            rig.From3D(new JObject { ["type"] = "error", ["reason"] = "scene-missing", ["job"] = Old, ["detail"] = "HTTP 404" });

            Assert.Equal("Off", rig.R.State);
            Assert.False(rig.Vm.IsMap3DBusy && rig.Vm.Map3DBusyText == Map3DGuide.EnterTitle, "전환 안내가 남지 않는다");
            Assert.Equal(Map3DGuide.SceneMissingText, rig.R.Status);
            AssertNoRefs(rig, Old);
            await Until(() => rig.Server.Got(HttpMethod.Get, "/api/jobs"), "영역 목록 새로 고침");
            rig.R.Areas(rig.Area(Old));   // 늦은 목록 — 되살리지 않는다
            AssertNoRefs(rig, Old);
        });

    [Fact]
    public void should_cancel_entry_without_retiring_area_when_viewer_reports_broken_scene()
        => Run(rig =>
        {
            rig.R.Areas(rig.Area(Old));
            rig.R.HostLoading(Old);
            rig.R.Gate();
            rig.R.PressEnter();
            Assert.Equal("Pending", rig.R.State);

            rig.From3D(new JObject { ["type"] = "error", ["reason"] = "scene-broken", ["job"] = Old });

            Assert.Equal("Off", rig.R.State);
            Assert.Equal(Map3DGuide.SceneLoadFailedText("scene-broken"), rig.R.Status);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == Old);   // 깨진 장면은 지워진 것이 아니다 — 영역은 남아 다시 만들 수 있다
        });

    [Fact]
    public void should_parse_error_message_and_mark_host_failed_but_ignore_stale_ready()
        => Run(rig =>
        {
            var msg = Map3DMath.ParseMessage("{\"type\":\"error\",\"reason\":\"scene-missing\",\"job\":\"" + Old + "\",\"detail\":\"HTTP 404\"}")!;
            Assert.Equal(("error", "scene-missing", Old), (msg.Type, msg.Reason, msg.Job));
            Assert.Equal(("error", (string?)null, (string?)null), (Map3DMath.ParseMessage("{\"type\":\"error\",\"reason\":3,\"job\":[1]}")!.Type,
                                                                   Map3DMath.ParseMessage("{\"type\":\"error\",\"reason\":3,\"job\":[1]}")!.Reason,
                                                                   Map3DMath.ParseMessage("{\"type\":\"error\",\"reason\":3,\"job\":[1]}")!.Job));

            var host = rig.Host;
            rig.R.HostLoading(New);
            var seen = new List<string>();
            host.MessageReceived += m => seen.Add(m.Type);
            Scenario.Call(host, "OnWebMessage", "{\"type\":\"ready\",\"job\":\"" + Old + "\"}");   // 내려놓은 옛 페이지의 늦은 ready
            Assert.False(host.IsReadyFor(New));
            Assert.True(host.IsLoading);
            Scenario.Call(host, "OnWebMessage", "{\"type\":\"error\",\"reason\":\"scene-missing\",\"job\":\"" + New + "\"}");
            Assert.False(host.IsLoading, "실패 — 같은 장면을 다시 미리 열지 않는다");
            Assert.False(host.IsReadyFor(New));
            Assert.Equal(new[] { "error" }, seen);

            host.Unload();
            Assert.Null(host.Job);
            Assert.False(host.IsLoading);
            Assert.Equal("none", host.HostMode);
        });

    // ─────────────── 4. 회전 반영 ───────────────

    private static PointCollection Outline(MapViewModel vm, string id)
        => Scenario.GetField<Polygon>(Scenario.GetField<Dictionary<string, Map3DAreaMarker>>(vm, "_map3DAreaMarkers")[id], "_outline").Points;

    /// <summary>지금 지도(방위 · 줌 · 위치)로 그 영역 네 모서리를 화면에 투영한 테두리 — 마커 자리(왼쪽 위 모서리) 기준.</summary>
    private static List<System.Windows.Point> Expected(Rig rig, string id)
    {
        var map = rig.R.W.Map;
        var item = rig.Vm.Map3DAreas.Single(a => a.Id == id);
        var marker = Scenario.GetField<Dictionary<string, Map3DAreaMarker>>(rig.Vm, "_map3DAreaMarkers")[id];
        var o = map.FromLatLngToLocal(marker.Position);
        return item.Area.Corners.Select(c => map.FromLatLngToLocal(new PointLatLng(c.Lat, c.Lon))).Select(p => new System.Windows.Point(p.X - o.X, p.Y - o.Y)).ToList();
    }

    private static void AssertOutline(Rig rig, string id, string what)
    {
        var actual = Outline(rig.Vm, id);
        var expected = Expected(rig, id);
        Assert.Equal(4, actual.Count);
        for (int i = 0; i < 4; i++)
            Assert.True(Math.Abs(actual[i].X - expected[i].X) < 0.5 && Math.Abs(actual[i].Y - expected[i].Y) < 0.5,
                        $"{what}: 꼭짓점 {i} {actual[i]} ≠ {expected[i]}");
    }

    private static void Rotated(Action<Rig> body) => Run(rig =>
    {
        bool before = RotationFeature.IsEnabled;
        RotationFeature.IsEnabled = true;
        try { body(rig); }
        finally { RotationFeature.IsEnabled = before; }
    });

    public enum RotatePath { SetMapRotation, MapRotationProperty, Sync3DCamera }

    [Theory]
    [InlineData(RotatePath.SetMapRotation)]       // 키 · 툴바 · 회전 토글
    [InlineData(RotatePath.MapRotationProperty)]  // 나침반 · 바인딩
    [InlineData(RotatePath.Sync3DCamera)]         // 3D 카메라를 2D 에 맞춤(Apply3DCameraTo2D)
    public void should_redraw_outline_with_new_rotation_without_pan_when_bearing_changes(RotatePath path) => Rotated(rig =>
    {
        var map = rig.R.W.Map;
        var rect = new Map3DRotRect(rig.R.Center.Lng, rig.R.Center.Lat, 300, 160, 20);
        rig.R.Areas(rig.Area("plain"), new Map3DSceneInfo("rot", null, rect.Bbox(), null, 3, "job") { Rect = rect });
        rig.R.HostReady("plain");   // 화면 가운데 영역은 이미 열어 둔 장면 — 게이트가 미리 불러오기(WebView2)를 하지 않게
        Scenario.Pump(DispatcherPriority.Background);
        AssertOutline(rig, "plain", "처음");
        var position = map.Position;
        double zoom = map.EffectiveZoom;

        foreach (var bearing in new[] { 30d, 90d, 0d, -45d })
        {
            switch (path)
            {
                case RotatePath.SetMapRotation: map.SetMapRotation(bearing); break;
                case RotatePath.MapRotationProperty: map.MapRotation = bearing; break;
                case RotatePath.Sync3DCamera:
                    Scenario.Call(rig.Vm, "Apply3DCameraTo2D", new Map3DCamera(position.Lng, position.Lat,
                        Map3DMath.ToMercatorZoom(Math.Min(map.Zoom, map.MaxZoom), map.DigitalZoomScale), Map3DMath.ToMap3DBearing(bearing), 50));
                    break;
            }
            Scenario.Pump(DispatcherPriority.Background);   // 한 프레임(Render) — 팬 · 줌 없이
            Assert.Equal((float)Map3DMath.ToGMapBearing(Map3DMath.ToMap3DBearing(bearing)), map.Bearing, 3);
            Assert.Equal(position, map.Position);
            Assert.Equal(zoom, map.EffectiveZoom);
            AssertOutline(rig, "plain", $"방위 {bearing}");
            AssertOutline(rig, "rot", $"회전 영역 · 방위 {bearing}");
        }
    });

    [Fact]
    public void should_redraw_outline_at_most_once_per_frame_when_rotating_continuously() => Rotated(rig =>
    {
        var map = rig.R.W.Map;
        rig.R.Areas(rig.Area("a1"), rig.Area("a2"));
        rig.R.HostReady("a1");
        Scenario.Pump(DispatcherPriority.Background);
        var counts = new Dictionary<string, int> { ["a1"] = 0, ["a2"] = 0 };
        var descriptor = DependencyPropertyDescriptor.FromProperty(Polygon.PointsProperty, typeof(Polygon));
        var outlines = counts.Keys.ToDictionary(id => id, id => Scenario.GetField<Polygon>(Scenario.GetField<Dictionary<string, Map3DAreaMarker>>(rig.Vm, "_map3DAreaMarkers")[id], "_outline"));
        var handlers = counts.Keys.ToDictionary(id => id, id => (EventHandler)((_, _) => counts[id]++));
        foreach (var id in counts.Keys.ToList()) descriptor.AddValueChanged(outlines[id], handlers[id]);
        try
        {
            // 한 프레임 안에 휠 회전 45 스텝(1°씩) — 테두리는 프레임 끝에 한 번만
            for (int b = 1; b <= 45; b++) map.SetMapRotation(b);
            Scenario.Pump(DispatcherPriority.Background);
            Assert.Equal(1, counts["a1"]);
            Assert.Equal(1, counts["a2"]);
            AssertOutline(rig, "a1", "연속 회전 끝(45°)");

            // 프레임마다 한 스텝이면 프레임마다 한 번(그보다 많지 않다)
            for (int b = 46; b <= 55; b++) { map.SetMapRotation(b); Scenario.Pump(DispatcherPriority.Background); }
            Assert.InRange(counts["a1"], 2, 11);
            AssertOutline(rig, "a1", "55°");

            // 같은 방위로 다시 — 다시 그리지 않는다
            int now = counts["a1"];
            map.SetMapRotation(55); Scenario.Pump(DispatcherPriority.Background);
            Assert.Equal(now, counts["a1"]);
        }
        finally { foreach (var id in counts.Keys) descriptor.RemoveValueChanged(outlines[id], handlers[id]); }
    });
}
