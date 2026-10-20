using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 엔진(map-3d) 다시 연결(2026-10-20) — 엔진이 죽었다 다시 뜨면 GIS 가 스스로 되살아나는지. 실제 MapViewModel 경로(영역 목록 · 3D 진입 ·
/// 3D 화면 불러오기 실패 · 엔진 확인)를 가짜 map-3d(HttpMessageHandler)로 돌린다. 시간은 가짜 틱(IMap3DTicker)을 꽂아 틱을 직접 부른다(기다림 없음).
/// 창 · WebView2 는 만들지 않는다 — 장면 페이지 열기는 이음매(_map3DSceneLoader)로 받기만 한다.
/// </summary>
public class Map3DReconnectScenarioTests
{
    private const string Area = "area-a";

    // ─────────────── 장치 ───────────────

    private sealed class FakeTicker : IMap3DTicker
    {
        private Func<Task>? _tick;
        public bool IsRunning { get; private set; }
        public TimeSpan Interval { get; private set; }
        public int Starts { get; private set; }
        public void Start(TimeSpan interval, Func<Task> tick) { Interval = interval; _tick = tick; IsRunning = true; Starts++; }
        public void Stop() => IsRunning = false;
        /// <summary>간격이 지난 것처럼 한 번 부른다.</summary>
        public Task FireAsync() { Assert.True(IsRunning, "틱이 돌고 있지 않은데 부름"); return _tick!(); }
    }

    private sealed class FakeEngine : HttpMessageHandler
    {
        public bool Up { get; set; } = true;
        /// <summary>/api/health 가 없는 옛 엔진(404 → /api/config).</summary>
        public bool OldEngine { get; set; }
        public List<string> Paths { get; } = new();
        public List<JObject> Scenes { get; } = new();
        public int Count(string path) => Paths.Count(p => p == path);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            if (!Up) throw new HttpRequestException("대상 컴퓨터에서 연결을 거부했으므로 연결하지 못했습니다.");
            if (path == "/api/health") return Task.FromResult(OldEngine ? Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" })
                                                                         : Json(HttpStatusCode.OK, new JObject { ["ok"] = true }));
            if (path == "/api/config") return Task.FromResult(Json(HttpStatusCode.OK, new JObject()));
            if (path == "/api/jobs") return Task.FromResult(Json(HttpStatusCode.OK, new JObject { ["scenes"] = new JArray(Scenes.ToArray()), ["running"] = new JArray() }));
            return Task.FromResult(Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" }));
        }

        private static HttpResponseMessage Json(HttpStatusCode code, JObject o) => new(code) { Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; }
        public MapViewModel Vm => W.Vm;
        public FakeEngine Engine { get; } = new();
        public FakeTicker Reconnect { get; } = new();
        public FakeTicker Watch { get; } = new();
        public List<(string Job, string Url)> Loads { get; } = new();
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        public Rig(bool enabled = true)
        {
            W = WiredMapRig.Create();
            Scenario.SetField(Vm, "_isEditModeEnabled", false);
            W.Map.SetEditMode(false);
            W.Map.SetEffectiveZoom(17d);
            var settings = new Map3DModel { EntryMode = Map3DModel.EntryManual, EnterZoom = 17, ExitZoom = 16.5, GuideMinSeconds = 0, IsEnabled = enabled };
            settings.Normalize();
            Scenario.SetField(Vm, "_map3DSettings", settings);
            Scenario.SetField(Vm, "_map3DScenes", new List<Map3DScene>());
            Scenario.SetField(Vm, "_map3DAreaMarkers", new Dictionary<string, Map3DAreaMarker>());
            Scenario.SetField(Vm, "<Map3DAreas>k__BackingField", new ObservableCollection<Map3DAreaItem>());
            Scenario.SetField(Vm, "_map3DAreaZooms", new Map3DAreaZoomStore(_zoomFile));
            foreach (var f in typeof(MapViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (f.Name.StartsWith("_map3D", StringComparison.Ordinal) && f.GetValue(Vm) == null && !f.FieldType.IsAbstract
                    && f.FieldType.IsClass && f.FieldType != typeof(string) && f.FieldType.GetConstructor(Type.EmptyTypes) != null
                    && (f.FieldType.IsGenericType || f.FieldType.Name == "Map3DPerfSamples"))
                    f.SetValue(Vm, Activator.CreateInstance(f.FieldType));
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Engine));
            Scenario.SetField(Vm, "_map3DReconnectTicker", Reconnect);
            Scenario.SetField(Vm, "_map3DWatchTicker", Watch);
            Scenario.SetField(Vm, "_map3DSceneLoader", (Func<string, string, Task>)((job, url) => { Loads.Add((job, url)); return Task.CompletedTask; }));

            var c = W.Map.Position;
            Engine.Scenes.Add(new JObject { ["id"] = Area, ["bbox"] = new JArray(c.Lng - 0.002, c.Lat - 0.002, c.Lng + 0.002, c.Lat + 0.002) });
        }

        public Task RefreshAreasAsync() => (Task)Scenario.Call(Vm, "RefreshMap3DAreasAsync")!;

        /// <summary>영역 목록을 받고 「3D 로 보기」로 들어가 펼친다(호스트는 그 장면 ready · rendered — 재사용 경로, WebView2 없음).</summary>
        public async Task Enter3DAsync()
        {
            await RefreshAreasAsync();
            var host = Vm.Map3DHost;
            SetHost(host, "_job", Area);
            SetHost(host, "_ready", true);
            SetHost(host, "_rendered", true);
            SetHost(host, "<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow.AddSeconds(-10));
            Scenario.Call(Vm, "UpdateMap3DAreaDisplay");
            Vm.EnterMap3DCommand.Execute(null);
            if (State == "Pending") Scenario.Call(Vm, "OnMap3DGuideTick");
            Assert.Equal("Shown", State);
        }

        private static void SetHost(Map3DViewHost host, string name, object? value)
            => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);

        public string State => Scenario.GetField<object>(Vm, "_map3DState").ToString()!;
        public bool Down => Scenario.GetField<bool>(Vm, "_map3DEngineDown");
        public bool BannerShown => Vm.IsAimStatusVisible && Vm.AimStatusMessage == "3D 서버(map-3d)가 꺼져 있습니다";
        public int Health => Engine.Count("/api/health") + Engine.Count("/api/config");

        public void Done()
        {
            Assert.Equal("none", Vm.Map3DHost.HostMode);
            Assert.Empty(W.Errors);
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }
    }

    private static void Run(Func<Rig, Task> body, bool enabled = true)
        => Scenario.StaAsync(async () => { var rig = new Rig(enabled); await body(rig); rig.Done(); });

    // ─────────────── 끊김 → 배너 + 5 초마다 확인 ───────────────

    [Fact]
    public void should_start_polling_every_5s_when_area_list_cannot_reach_engine()
        => Run(async rig =>
        {
            rig.Engine.Up = false;
            await rig.RefreshAreasAsync();

            Assert.True(rig.Down);
            Assert.True(rig.Reconnect.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(5), rig.Reconnect.Interval);
            Assert.False(rig.Watch.IsRunning);
        });

    [Fact]
    public void should_show_banner_and_start_polling_when_3d_page_cannot_connect_while_shown()
        => Run(async rig =>
        {
            await rig.Enter3DAsync();
            Scenario.Call(rig.Vm, "OnMap3DLoadFailed", "3D 화면을 열지 못했습니다(CannotConnect)");   // 3D 호스트 탐색 실패와 같은 글

            Assert.True(rig.BannerShown);
            Assert.True(rig.Down);
            Assert.True(rig.Reconnect.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(5), rig.Reconnect.Interval);
            Assert.Equal("Off", rig.State);
        });

    [Fact]
    public void should_keep_polling_and_banner_state_when_engine_still_down_on_tick()
        => Run(async rig =>
        {
            rig.Engine.Up = false;
            await rig.RefreshAreasAsync();
            await rig.Reconnect.FireAsync();
            await rig.Reconnect.FireAsync();

            Assert.True(rig.Down);
            Assert.True(rig.Reconnect.IsRunning);
            Assert.Equal(1, rig.Reconnect.Starts);   // 이미 끊김이면 다시 시작하지 않는다(로그도 한 번)
            Assert.Equal(2, rig.Engine.Count("/api/health"));
        });

    // ─────────────── 다시 연결 ───────────────

    [Fact]
    public void should_clear_banner_refresh_areas_and_reload_view_when_engine_back_after_3d_shown()
        => Run(async rig =>
        {
            await rig.Enter3DAsync();
            Scenario.Call(rig.Vm, "OnMap3DLoadFailed", "3D 화면을 열지 못했습니다(CannotConnect)");
            Assert.True(rig.BannerShown);
            int listsBefore = rig.Engine.Count("/api/jobs");

            rig.Engine.Up = true;   // 감시 프로그램이 엔진을 다시 띄웠다
            await rig.Reconnect.FireAsync();

            Assert.False(rig.Down);
            Assert.False(rig.Reconnect.IsRunning);                       // 살아나면 묻기를 멈춘다
            Assert.False(rig.Vm.IsAimStatusVisible);                     // 배너 내림
            Assert.Equal(listsBefore + 1, rig.Engine.Count("/api/jobs")); // 영역 목록 다시 받음
            var load = Assert.Single(rig.Loads);                          // 옛 페이지를 다시 쓰지 않고 새로 연다
            Assert.Equal(Area, load.Job);
            Assert.Contains(Area, load.Url);
            Assert.StartsWith("http://127.0.0.1:8765", load.Url);
            Assert.Equal("Pending", rig.State);                          // 다시 들어가는 중(ready 를 기다린다)
        });

    [Fact]
    public void should_refresh_areas_without_reloading_view_when_engine_back_while_2d()
        => Run(async rig =>
        {
            rig.Engine.Up = false;
            await rig.RefreshAreasAsync();
            Assert.Empty(rig.Vm.Map3DAreas);

            rig.Engine.Up = true;
            await rig.Reconnect.FireAsync();

            Assert.False(rig.Down);
            Assert.False(rig.Reconnect.IsRunning);
            Assert.Single(rig.Vm.Map3DAreas);   // 영역 목록 다시 받음
            Assert.Empty(rig.Loads);            // 2D 였으면 3D 를 열지 않는다
            Assert.Equal("Off", rig.State);
        });

    [Fact]
    public void should_reconnect_through_config_when_old_engine_has_no_health()
        => Run(async rig =>
        {
            rig.Engine.Up = false;
            await rig.RefreshAreasAsync();
            rig.Engine.Up = true;
            rig.Engine.OldEngine = true;   // /api/health 404

            await rig.Reconnect.FireAsync();

            Assert.False(rig.Down);
            Assert.Equal(1, rig.Engine.Count("/api/config"));
            Assert.Single(rig.Vm.Map3DAreas);
        });

    // ─────────────── 3D 중 엔진 확인(10 초) ───────────────

    [Fact]
    public void should_enter_down_after_two_failed_watch_checks_and_reload_when_back_while_3d_shown()
        => Run(async rig =>
        {
            await rig.Enter3DAsync();
            Assert.True(rig.Watch.IsRunning);
            Assert.Equal(TimeSpan.FromSeconds(10), rig.Watch.Interval);

            await rig.Watch.FireAsync();          // 살아 있음
            rig.Engine.Up = false;
            await rig.Watch.FireAsync();          // 실패 1번 — 아직 끊김 아님
            Assert.False(rig.Down);
            Assert.False(rig.BannerShown);
            await rig.Watch.FireAsync();          // 실패 2번 잇달아 → 끊김

            Assert.True(rig.Down);
            Assert.True(rig.BannerShown);
            Assert.True(rig.Reconnect.IsRunning);
            Assert.False(rig.Watch.IsRunning);
            Assert.Equal("Shown", rig.State);     // 보던 3D 는 그대로 둔다

            rig.Engine.Up = true;
            await rig.Reconnect.FireAsync();

            Assert.False(rig.Vm.IsAimStatusVisible);
            Assert.Equal(Area, Assert.Single(rig.Loads).Job);   // 지금 자리에서 3D 를 다시 연다
            Assert.Equal("Pending", rig.State);
            Assert.False(rig.Watch.IsRunning);                  // 다시 펼쳐지면(Shown) 그때 다시 돈다
        });

    [Fact]
    public void should_reset_watch_failures_when_check_succeeds_between_failures()
        => Run(async rig =>
        {
            await rig.Enter3DAsync();
            rig.Engine.Up = false;
            await rig.Watch.FireAsync();
            rig.Engine.Up = true;
            await rig.Watch.FireAsync();
            rig.Engine.Up = false;
            await rig.Watch.FireAsync();

            Assert.False(rig.Down);   // 잇달아 2번이 아니다
            Assert.True(rig.Watch.IsRunning);
        });

    [Fact]
    public void should_stop_watch_when_leaving_3d()
        => Run(async rig =>
        {
            await rig.Enter3DAsync();
            Assert.True(rig.Watch.IsRunning);
            rig.Vm.ExitMap3DCommand.Execute(null);
            Assert.False(rig.Watch.IsRunning);
        });

    // ─────────────── 3D 꺼짐 · 정리 ───────────────

    [Fact]
    public void should_not_poll_at_all_when_3d_disabled()
        => Run(async rig =>
        {
            rig.Engine.Up = false;
            await rig.RefreshAreasAsync();
            Scenario.Call(rig.Vm, "NoteMap3DEngineDown", "test", true, true);
            Scenario.Call(rig.Vm, "SyncMap3DEngineWatch");

            Assert.False(rig.Down);
            Assert.False(rig.Reconnect.IsRunning);
            Assert.False(rig.Watch.IsRunning);
            Assert.Equal(0, rig.Health);
            Assert.False(rig.BannerShown);
        }, enabled: false);

    [Fact]
    public void should_stop_polling_when_map_deactivated()
        => Run(async rig =>
        {
            rig.Engine.Up = false;
            await rig.RefreshAreasAsync();
            Assert.True(rig.Reconnect.IsRunning);

            Scenario.Call(rig.Vm, "CleanupMap3D", false);

            Assert.False(rig.Reconnect.IsRunning);
            Assert.False(rig.Down);
        });

    [Fact]
    public void should_not_enter_down_when_page_fails_for_other_reason_and_engine_healthy()
        => Run(async rig =>
        {
            await rig.Enter3DAsync();
            Scenario.Call(rig.Vm, "OnMap3DLoadFailed", "3D 화면이 멈췄습니다(RenderProcessExited · 1)");
            await Task.Yield();

            Assert.False(rig.Down);
            Assert.False(rig.Reconnect.IsRunning);
            Assert.Equal(1, rig.Engine.Count("/api/health"));   // 엔진을 한 번 물어 본다
        });
}
