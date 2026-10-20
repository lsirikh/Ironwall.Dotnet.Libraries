using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using X = GMaps.MapEdit.Tests.Map3DExitInputScenarioTests;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 진입/이탈 · 3D 영역 · 3D 상호작용 — 기능끼리 엮인 복합 시나리오(2026-10-20).
/// 회전 영역 + 건물 높이 덮어쓰기 + 다시 만들기 + 진입 · 만들기 취소 직후 「3D 로 보기」 · 3D 중 건물 그리기 · 방향 링 끄는 중 scene-missing ·
/// 수동 방식 + 회전 영역 + 지도 회전 중 게이트 · 3D 중 레이어 다중 선택 · 연속 들어가기/나가기 20회 등.
/// 실제 MapViewModel 경로(게이트 · 「3D 로 보기」 · 「이 영역 3D 로 보기」 · 「나가기」 · Esc · 3D 메시지)를 그대로 탄다.
/// 창 · WebView2 는 만들지 않는다(Map3DEntryScenarioTests.Rig — 3D 호스트에는 상태만 꽂는다), map-3d 는 가짜 서버.
/// </summary>
public class Map3DCompositeScenarioTests
{
    private const string A = "area-a", B = "area-b", NewJob = "j-new";
    private static readonly BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

    // ─────────────── 장치 ───────────────

    /// <summary>가짜 map-3d — 굽기 · 진행 조회 · 취소 · 지우기 · 줌 바꾸기 · 건물 높이 저장. 목록(GET /api/jobs)은 503(목록은 시험이 직접 단다).</summary>
    private sealed class Server : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();
        public Queue<string> NextIds { get; } = new();
        public string PostStatus { get; set; } = "queued";
        public string Status { get; set; } = "running";

        public int Count(HttpMethod m, string path) { lock (Requests) return Requests.Count(r => r.Method == m && r.Path == path); }
        public int Count(string method) { lock (Requests) return Requests.Count(r => r.Method.Method == method); }
        public List<JObject> Posts() { lock (Requests) return Requests.Where(r => r.Method == HttpMethod.Post && r.Path == "/api/jobs").Select(r => JObject.Parse(r.Body!)).ToList(); }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            lock (Requests) Requests.Add((request.Method, path, body));
            if (path.Contains("/user-buildings")) return Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" });   // 옛 서버 = 빈 목록
            if (request.Method == HttpMethod.Post && path == "/api/jobs")
                return Json(HttpStatusCode.Accepted, new JObject { ["id"] = NextIds.Count > 0 ? NextIds.Dequeue() : NewJob, ["status"] = PostStatus, ["progress"] = 0 });
            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.Ordinal))
            {
                Status = "cancelled";
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^2], ["result"] = "requested", ["status"] = Status });
            }
            if (request.Method == HttpMethod.Put && path.EndsWith("/height", StringComparison.Ordinal))
            {
                var b = JObject.Parse(body!);
                return Json(HttpStatusCode.OK, new JObject
                {
                    ["building"] = new JObject { ["id"] = int.Parse(path.Split('/')[^2]), ["height"] = b["height"], ["height_source"] = "manual", ["height_from"] = "manual",
                                                 ["levels"] = b["floors"], ["origin"] = "map", ["center"] = new JArray(127.0, 37.5), ["manual"] = new JObject { ["uid"] = "u1" } },
                });
            }
            if (request.Method == HttpMethod.Delete && path.Split('/').Length == 4)
                return Json(HttpStatusCode.OK, new JObject { ["deleted"] = path.Split('/')[^1] });
            if (request.Method.Method == "PATCH") return Json(HttpStatusCode.OK, JObject.Parse(body ?? "{}"));
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/jobs/", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^1], ["status"] = Status, ["progress"] = 0.4, ["message"] = "OSM 조회" });
            return Json(HttpStatusCode.ServiceUnavailable, new JObject { ["detail"] = "목록은 이 시험 밖" });
        }

        private static HttpResponseMessage Json(HttpStatusCode code, JObject o) => new(code) { Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public Map3DEntryScenarioTests.Rig R { get; }
        public MapViewModel Vm => R.Vm;
        public Server Server { get; } = new();
        public List<JObject> Posts { get; } = new();
        public List<string> Infos { get; } = new();

        public Rig(string mode)
        {
            R = new Map3DEntryScenarioTests.Rig(mode);
            R.Settings.OverlayMode = Map3DBake.ModeTiles;   // 만들기 확정이 「마지막 굽기 방식」을 설정 파일에 저장하지 않게
            Scenario.SetField(Vm, "_bhFloorHeightText", "3.0");
            Scenario.SetField(Vm, "_bhFloorsText", "");
            Scenario.SetField(Vm, "_bhHeightText", "");
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(json => Posts.Add(JObject.Parse(json))));
            R.W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                   .Callback<string, string, string, int>((m, _, _, _) => { lock (Infos) Infos.Add(m); });
        }

        /// <summary>미리 불러오기가 일어나지 않는 줌(15.5 &lt; 미리 준비 줌 16) — 큐에 쌓인 게이트가 돌아도 WebView2 를 만들지 않게.</summary>
        public void NoPreload() { R.Settings.PreloadZoom = 16; R.SetZoom(15.5); }

        public Map3DAreaItem Item(string id) => Vm.Map3DAreas.Single(a => a.Id == id);
        public bool Baking => Scenario.GetField<bool>(Vm, "_isMap3DBaking");
        public List<Map3DScene> Scenes => Scenario.GetField<List<Map3DScene>>(Vm, "_map3DScenes");
        public bool Suppressed => Scenario.GetField<bool>(Vm, "_map3DEntrySuppressed");
        public Map3DCamera? Return => Scenario.GetField<Map3DCamera?>(Vm, "_map3DReturn");
        public string? AreaViewJob => Scenario.GetField<string?>(Vm, "_map3DAreaViewJob");
        public string? BhJob => Scenario.GetField<string?>(Vm, "_bhJob");
        public bool Moving => Scenario.GetField<Map3DNavMover?>(Vm, "_map3DNavMover")?.IsActive == true;
        public bool NavTimerOn => Scenario.GetField<DispatcherTimer?>(Vm, "_map3DNavTimer")?.IsEnabled == true;

        public void From3D(JObject json)
        {
            var msg = Map3DMath.ParseMessage(json.ToString());
            Assert.NotNull(msg);
            Scenario.Call(Vm, "OnMap3DMessage", msg);
        }

        /// <summary>그 장면을 열어 둔 3D 로 「3D 로 보기」 → 펼침.</summary>
        public void Enter(string job)
        {
            R.HostReady(job);
            R.Gate();
            R.PressEnter();
            Map3DEntryScenarioTests.SettleEntry(R);
            Assert.True(R.Shown, $"{job} 로 들어가지 못함(상태 {R.State} · 안내 {R.Status})");
            Assert.Equal(job, R.Job);
        }

        public void PickBuilding(int id, double height)
            => From3D(new JObject { ["type"] = "building", ["id"] = id, ["origin"] = "map", ["height"] = height, ["height_source"] = "levels", ["height_from"] = "levels",
                                    ["levels"] = null, ["area_m2"] = 120.0, ["lon"] = 127.0, ["lat"] = 37.5, ["manual"] = false });

        public void SceneMissing(string job) => From3D(new JObject { ["type"] = "error", ["reason"] = "scene-missing", ["job"] = job, ["detail"] = "HTTP 404" });

        /// <summary>배지 · 목록 「다시 만들기」 → 만들기 판(아직 [만들기] 안 누름).</summary>
        public void OpenRebake(string id)
        {
            Item(id).Actions.Single(a => a.Header == "다시 만들기").Command.Execute(null);
            Assert.True(Vm.IsMap3DBakeOptionsOpen);
        }

        public Task ConfirmDelete(string id)
        {
            Scenario.SetField(Vm, "_pendingMap3DAreaDelete", Item(id));
            return Vm.HandleAsync(new CallDeleteMap3DAreaProcessMessageModel(), CancellationToken.None);
        }

        public List<JObject> Sent(string type) => Posts.Where(p => p.Value<string>("type") == type).ToList();

        /// <summary>3D 의 그 건물이 원래 높이로 돌아갔다(미리보기 취소) · 강조 지움.</summary>
        public void AssertPreviewReverted(int id, string when)
        {
            var preview = Sent("building.preview").LastOrDefault();
            Assert.True(preview != null, $"{when}: 미리보기 취소를 보내지 않았다");
            Assert.Equal(id, preview!.Value<int>("id"));
            Assert.Equal(JTokenType.Null, preview["height"]!.Type);
        }

        public void Done() => R.Done();
    }

    private static void Run(Action<Rig> body, string mode = Map3DModel.EntryManual)
        => Scenario.Sta(() => { var rig = new Rig(mode); body(rig); rig.Done(); });

    private static void RunAsync(Func<Rig, Task> body, string mode = Map3DModel.EntryManual)
        => Scenario.StaAsync(async () => { var rig = new Rig(mode); await body(rig); rig.Done(); });

    private static void Rotated(Action body)
    {
        bool before = RotationFeature.IsEnabled;
        RotationFeature.IsEnabled = true;
        try { body(); }
        finally { RotationFeature.IsEnabled = before; }
    }

    private static async Task Until(Func<bool> condition, string what, double seconds = 6)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end && !condition()) await Task.Delay(20);
        Assert.True(condition(), what);
    }

    private static void AssertAt(Rig rig, PointLatLng where, double zoom, string because)
    {
        Assert.True(zoom == rig.R.W.Map.EffectiveZoom, $"{because}: 줌 {zoom} 이어야 하는데 {rig.R.W.Map.EffectiveZoom}");
        Assert.True(Math.Abs(rig.R.W.Map.Position.Lat - where.Lat) < 1e-6 && Math.Abs(rig.R.W.Map.Position.Lng - where.Lng) < 1e-6,
                    $"{because}: 자리 ({where.Lat:F6},{where.Lng:F6}) 이어야 하는데 ({rig.R.W.Map.Position.Lat:F6},{rig.R.W.Map.Position.Lng:F6})");
    }

    private static void Camera3D(Rig rig, double bearing, double zoom = 17.3) => X.Camera3D(rig.R, bearing, zoom);

    private static void SetHostElement(Rig rig, object? el) => typeof(Map3DViewHost).GetField("_el", Inst)!.SetValue(rig.Vm.Map3DHost, el);

    // ─────────────── 1. 회전 영역 + 건물 높이 덮어쓰기 + 다시 만들기 + 3D 진입 ───────────────

    /// <summary>
    /// 회전 영역 3D 에서 건물 높이를 직접 넣어 저장(PUT) → 나가기 → 「다시 만들기」(같은 rect) → 다 만들면 옛 장면 지움 →
    /// 새 장면으로 「3D 로 보기」 · 「이 영역 3D 로 보기」. 옛 장면의 판 · 되돌리기 · 진입 표지는 하나도 남지 않고, 새 장면 판은 새로 연다.
    /// </summary>
    [Fact]
    public void should_enter_new_scene_with_same_rotated_rect_and_drop_old_height_state_when_rotated_area_rebaked_after_height_override()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 300, 160, 40);
            rig.R.Areas(new Map3DSceneInfo("rot", "회전", rect.Bbox(), null, 3, "job") { Rect = rect, EnterZoom = 17.5 });
            rig.Enter("rot");

            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            await (Task)Scenario.Call(rig.Vm, "ApplyBuildingHeightAsync")!;
            Assert.Equal(1, rig.Server.Count(HttpMethod.Put, "/api/jobs/rot/buildings/7/height"));
            rig.R.PressExit();
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen, "나가면 판을 닫는다");

            rig.Server.PostStatus = "done";
            rig.Server.NextIds.Enqueue(NewJob);
            rig.OpenRebake("rot");
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => !rig.Baking && rig.Server.Count(HttpMethod.Delete, "/api/jobs/rot") == 1, "다시 만들기 끝 · 옛 장면 지움");

            var post = Assert.Single(rig.Server.Posts());
            var sent = Map3DRotRect.Parse(post["rect"]);
            Assert.NotNull(sent);
            Assert.Equal(40d, sent!.RotationDeg, 6);
            Assert.Equal(300d, sent.WidthM, 6);
            Assert.Equal(17.5, post.Value<double>("enter_zoom"));
            await Until(() => rig.Scenes.All(s => s.Id != "rot"), "옛 장면 참조가 캐시에서 빠짐");
            var fresh = rig.Scenes.First();
            Assert.Equal(NewJob, fresh.Id);
            Assert.Equal(rect, fresh.Rect);
            Assert.Null(rig.BhJob);
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == "rot");

            // 새 장면으로 「3D 로 보기」 → 건물을 고르면 새 장면의 판(옛 미리보기 값이 남지 않는다)
            rig.Enter(NewJob);
            rig.Posts.Clear();
            rig.PickBuilding(7, 36.0);
            Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Equal(NewJob, rig.BhJob);
            Assert.Equal(36.0, rig.Vm.BuildingHeightPreview);
            rig.R.PressExit();

            // 목록이 새 영역을 실어 오면 「이 영역 3D 로 보기」 — 그 회전각으로 연다
            rig.R.Areas(new Map3DSceneInfo(NewJob, "회전", rect.Bbox(), null, 3, "job") { Rect = rect, EnterZoom = 17.5 });
            rig.R.HostReady(NewJob);
            rig.R.ViewArea(NewJob);
            Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            Assert.Equal(NewJob, rig.AreaViewJob);
            Assert.Equal(40d, Map3DMath.EnterBearing(rig.R.W.Map.Bearing, rig.Scenes.Single(s => s.Id == NewJob).Rect), 6);
        });

    // ─────────────── 2 · 3. 만들기 취소 직후 「3D 로 보기」 ───────────────

    [Fact]
    public void should_enter_old_scene_when_3d_view_pressed_right_after_rebake_panel_cancelled()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.R.HostReady(A);
            rig.R.Gate();
            rig.OpenRebake(A);
            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);
            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen);

            rig.Enter(A);
            Assert.Equal(0, rig.Server.Count(HttpMethod.Post, "/api/jobs"));
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeReplaces"));
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeOptionsSourceId"));
        });

    [Fact]
    public void should_enter_old_scene_without_delete_when_3d_view_pressed_right_after_rebake_cancelled()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(A));
            rig.R.HostReady(A);
            rig.R.Gate();
            rig.OpenRebake(A);
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => rig.Server.Count(HttpMethod.Get, "/api/jobs/" + NewJob) >= 1, "진행 조회 시작");

            rig.R.PressEnter();
            Assert.Equal("Off", rig.R.State);   // 만드는 중에는 막힌다
            Assert.Contains("만드는 중", rig.R.Status);

            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            await Until(() => !rig.Baking, "취소로 굽기 끝");
            rig.Enter(A);                         // 곧바로 「3D 로 보기」 = 옛 장면
            Assert.Equal(0, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + A));
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == A);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == NewJob);
        });

    // ─────────────── 4. 3D 중 건물 그리기 ───────────────

    [Theory]
    [InlineData(Map3DModel.EntryManual)]
    [InlineData(Map3DModel.EntryAuto)]
    public void should_refuse_3d_while_drawing_building_started_in_3d_and_reenter_only_by_rule_after_drawing_ends(string mode)
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.Vm.DrawUserBuildingCommand.Execute(null);                // 툴바 「건물 그리기」 — 그리기는 2D
            try
            {
                Assert.True(rig.Vm.IsUserBuildingDrawing);
                Assert.False(rig.R.Shown);
                Assert.True(rig.R.W.Map.IsHitTestVisible, "그리려면 2D 가 누름을 받아야 한다");
                rig.R.PressEnter();
                Assert.Equal("Off", rig.R.State);
                Assert.Contains("들어가지 않습니다", rig.R.Status);
                Assert.Null(rig.Return);
                rig.R.SetZoom(18); rig.R.Gate(); rig.R.Tick();
                Assert.Equal("Off", rig.R.State);
            }
            finally { rig.Vm.DrawUserBuildingCommand.Execute(null); }   // 다시 누르기 = 끝내기
            Assert.False(rig.Vm.IsUserBuildingDrawing);
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown, "그리기를 끝낸 그 자리에서 곧바로 다시 들어가지 않는다");
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            if (mode == Map3DModel.EntryAuto)
            {
                rig.R.SetZoom(16); rig.R.Gate();
                rig.R.SetZoom(17); rig.R.Gate(); Map3DEntryScenarioTests.SettleEntry(rig.R);
                Assert.True(rig.R.Shown, "자동 — 진입 줌 밑으로 한 번 내렸다 올리면 다시 들어간다");
            }
            else
            {
                rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
                Assert.True(rig.R.Shown);
            }
        }, mode);

    [Fact]
    public void should_cancel_pending_entry_and_start_drawing_when_building_draw_pressed_during_entry_guide()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.R.HostReady(A);
            rig.R.Gate();
            rig.R.PressEnter();
            Assert.Equal("Pending", rig.R.State);
            rig.Vm.DrawUserBuildingCommand.Execute(null);
            try
            {
                Assert.Equal("Off", rig.R.State);
                Assert.False(rig.Vm.IsMap3DBusy, "전환 안내가 남지 않는다");
                Assert.Null(rig.Return);
                rig.R.Tick();
                Assert.False(rig.R.Shown, "안내 타이머가 늦게 펼치지 않는다");
            }
            finally { rig.Vm.DrawUserBuildingCommand.Execute(null); }
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown);
            rig.Enter(A);
        });

    // ─────────────── 5. 방향 링 끄는 중 scene-missing ───────────────

    [Fact]
    public void should_stop_ring_and_moves_and_ignore_late_input_when_scene_missing_arrives_while_ring_dragged()
        => Run(rig =>
        {
            X.Enter(rig.R);   // 벤더 코어 시작 + 영역 A — 나간 뒤 끌기 팬까지 잰다
            Camera3D(rig, 0);
            var drag = new Map3DNavDrag(ring: true, deadZone: 8);
            drag.Press(50, 50, 50, 10);
            rig.Vm.NudgeMap3D(drag.Move(90, 50)!.Value);
            Assert.Equal("방위 270°", rig.Vm.Map3DNavBearingText);
            rig.Vm.StartMap3DMove(Map3DNavMove.Forward);
            Assert.True(rig.Moving);

            rig.SceneMissing(A);

            Assert.Equal("Off", rig.R.State);
            Assert.False(rig.Moving, "장면이 사라지면 누르던 이동도 멈춘다");
            Assert.False(rig.NavTimerOn);
            Assert.Equal(Map3DGuide.SceneMissingText, rig.R.Status);
            if (drag.Move(50, 90) is { } late) rig.Vm.NudgeMap3D(late);   // 접힌 링에 늦게 온 끌기
            rig.Vm.StartMap3DMove(Map3DNavMove.Back);
            Assert.Equal("방위 270°", rig.Vm.Map3DNavBearingText);
            Assert.False(rig.Moving);
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == A);
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
            Assert.Null(rig.Vm.Map3DHost.Job);
            X.AssertMap2DInputAlive(rig.R, "scene-missing(링 끄는 중)");
            rig.SceneMissing(A);                                          // 같은 알림이 한 번 더 — 무해
            Assert.Equal("Off", rig.R.State);
        });

    // ─────────────── 6 · 7. 회전 영역 + 지도 회전 중 게이트 ───────────────

    [Fact]
    public void should_judge_by_rotated_rect_at_every_map_bearing_and_return_to_entry_bearing_when_manual()
        => Rotated(() => Run(rig =>
        {
            var map = rig.R.W.Map;
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 300, 160, 45);
            var b = rect.Bbox();
            rig.R.Areas(new Map3DSceneInfo("rot", null, b, null, 3, "job") { Rect = rect, EnterZoom = 17 });
            rig.R.HostReady("rot");
            var corner = new PointLatLng(b[3] - 0.00003, b[2] - 0.00003);   // bbox 안 · 회전 사각형 밖

            foreach (var bearing in new[] { 0d, 30d, 90d, 180d, 270d, -45d })
            {
                map.SetMapRotation(bearing);
                Scenario.Pump(DispatcherPriority.Background);
                map.Position = corner;
                foreach (var z in new[] { 17d, 18d })
                {
                    rig.R.SetZoom(z); rig.R.Gate(); rig.R.Tick();
                    Assert.Equal("Off", rig.R.State);
                    Assert.False(rig.Vm.IsMap3DEnterButtonVisible, $"방위 {bearing} · 줌 {z} — 사각형 밖인데 단추");
                    Assert.False(rig.Vm.IsMap3DChipVisible);
                }
                map.Position = c;
                rig.R.SetZoom(18); rig.R.Gate(); rig.R.Tick();
                Assert.Equal("Off", rig.R.State);   // 수동 — 줌으로는 안 들어간다
                Assert.True(rig.Vm.IsMap3DEnterButtonVisible, $"방위 {bearing} — 사각형 안인데 단추 없음");
            }

            map.SetMapRotation(90);
            Scenario.Pump(DispatcherPriority.Background);
            rig.R.SetZoom(17); rig.R.Gate();
            double before = map.Bearing;
            rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            // 3D 에서 돌림 — WPF 안 3D 는 2D 가 3D 카메라를 따라간다(Apply3DCameraTo2D — 이 장치의 호스트는 "none" 이라 직접 부른다)
            Scenario.Call(rig.Vm, "Apply3DCameraTo2D", new Map3DCamera(c.Lng, c.Lat, 18.2, 200, 55));
            Camera3D(rig, 200, 18.2);
            Assert.NotEqual(before, map.Bearing);
            rig.R.PressExit();
            Assert.Equal(before, map.Bearing, 3);
            AssertAt(rig, c, 17, "들어가기 직전 2D 자리");
        }));

    [Fact]
    public void should_not_reenter_while_rotating_after_exit_and_reenter_after_zoom_dip_when_auto_rotated_area()
        => Rotated(() => Run(rig =>
        {
            var map = rig.R.W.Map;
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 300, 160, 60);
            rig.R.Areas(new Map3DSceneInfo("rot", null, rect.Bbox(), null, 3, "job") { Rect = rect, EnterZoom = 17 });
            rig.R.HostReady("rot");
            rig.R.Gate(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown, "자동 — 줌 17 · 사각형 안이면 들어간다");
            rig.R.PressExit();

            for (int bearing = 10; bearing <= 350; bearing += 40)
            {
                map.SetMapRotation(bearing);
                Scenario.Pump(DispatcherPriority.Background);
                rig.R.Gate(); rig.R.Tick();
                Assert.False(rig.R.Shown, $"나간 뒤 지도만 돌렸는데(방위 {bearing}) 다시 들어갔다");
            }
            Assert.True(rig.Suppressed);
            rig.R.SetZoom(16); rig.R.Gate();
            Assert.False(rig.Suppressed, "진입 줌 밑 = 보류 풀림");
            rig.R.SetZoom(17); rig.R.Gate(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            Assert.Equal("rot", rig.R.Job);
        }, Map3DModel.EntryAuto));

    [Fact]
    public void should_return_to_original_bearing_zoom_and_spot_when_rotated_area_viewed_from_rotated_far_map_and_exited()
        => Rotated(() => Run(rig =>
        {
            var map = rig.R.W.Map;
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng + 0.01, c.Lat - 0.005, 300, 160, 300);
            rig.R.Areas(rig.R.AreaAround(A), new Map3DSceneInfo("rot", null, rect.Bbox(), null, 3, "job") { Rect = rect });
            rig.R.SetZoom(12);                                            // 미리 불러오기 줌 밑 — 큐에 쌓인 게이트가 A 를 미리 열지 않게
            map.SetMapRotation(30);
            Scenario.Pump(DispatcherPriority.Background);
            map.Position = c;
            double before = map.Bearing;

            rig.R.HostReady("rot");
            rig.R.ViewArea("rot");
            Assert.Equal((float)Map3DMath.ToGMapBearing(300), map.Bearing, 3);   // 2D 도 그 회전각
            Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            Camera3D(rig, 100, 18.5);
            rig.R.PressExit();
            Assert.Equal(before, map.Bearing, 3);
            AssertAt(rig, c, 12, "「영역 보기」 누르기 전 자리");
        }));

    // ─────────────── 8. 3D 중 레이어 다중 선택 ───────────────

    [Fact]
    public void should_keep_map_selection_and_3d_when_layer_rows_multi_selected_in_3d()
        => Run(rig =>
        {
            var w = rig.R.W;
            var provider = Scenario.GetField<SymbolProvider>(w.Vm, "_symbolProvider");
            var center = w.Map.FromLatLngToLocal(w.Map.Position);
            foreach (var (id, dx) in new[] { (401, -150), (402, -50), (403, 50) })
            {
                var geo = w.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y);
                var model = new GeometricSymbolModel { Id = id, Title = $"도형-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, ShowShape = true, Visible = true };
                var marker = new MarkerFactory(Mock.Of<ILogService>(), w.Devices).CreateMarker(model);
                w.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
                provider.Add(model);
            }
            Scenario.Layout(w.Deco, 1200, 800);
            var roots = LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), provider);
            Scenario.SetField(w.Vm, "_layerTreeNodes", roots);
            w.Vm.ShowLayerPanel();
            LayerTreeNode Leaf(int id) => LayerTreeBuilder.Flatten(roots).Single(n => n.Symbol?.Id == id);
            LayerSelection.ExpandToShow(new[] { Leaf(401) });

            X.Enter(rig.R);
            w.Vm.LayerPanel!.SelectNode(Leaf(401), ModifierKeys.None);
            w.Vm.LayerPanel!.SelectNode(Leaf(402), ModifierKeys.Control);
            w.Vm.LayerPanel!.SelectNode(Leaf(403), ModifierKeys.Shift);

            Assert.True(rig.R.Shown, "레이어 행을 골라도 3D 를 나가지 않는다");
            Assert.False(w.Vm.IsEditModeEnabled);
            Assert.Null(w.Vm.SelectedMarker);
            Assert.Empty(w.Vm.SelectedMarkers);
            Assert.False(w.Map.IsHitTestVisible, "3D 중 2D 는 여전히 막혀 있다");

            rig.R.PressExit();
            X.AssertMap2DInputAlive(rig.R, "레이어 다중 선택 뒤 나가기");
            Assert.Empty(w.Vm.SelectedMarkers);
        });

    // ─────────────── 9. 연속 들어가기/나가기 20회 ───────────────

    [Fact]
    public void should_restore_2d_input_and_clear_return_state_every_time_when_entered_and_exited_20_times()
        => Run(rig =>
        {
            var map = rig.R.W.Map;
            X.Enter(rig.R);   // 벤더 코어 시작 + 영역 A
            rig.R.PressExit();
            var home = rig.R.Center;
            for (int i = 0; i < 20; i++)
            {
                map.Position = home;
                rig.R.SetZoom(17);
                switch (i % 3)
                {
                    case 0: rig.R.Gate(); rig.R.PressEnter(); break;                              // 「3D 로 보기」
                    case 1: rig.R.SetZoom(16); rig.R.Gate(); rig.R.SetZoom(17); rig.R.Gate(); break;   // 자동 줌 게이트
                    case 2: rig.R.ViewArea("area-a"); break;                                        // 「이 영역 3D 로 보기」
                }
                Map3DEntryScenarioTests.SettleEntry(rig.R);
                Assert.True(rig.R.Shown, $"{i + 1}회차({i % 3}) 들어가지 못함 — 상태 {rig.R.State}");
                Assert.False(map.IsHitTestVisible);
                Assert.False(rig.R.PressEsc(), "WPF 안 3D 는 Esc 로 안 나간다");
                Camera3D(rig, (i * 37) % 360, 17 + (i % 4) * 0.4);
                rig.R.PressExit();

                Assert.False(rig.R.Shown);
                Assert.True(map.IsHitTestVisible, $"{i + 1}회차 — 지도 누름 판정");
                Assert.Equal(DependencyProperty.UnsetValue, map.ReadLocalValue(UIElement.IsHitTestVisibleProperty));
                Assert.Null(Scenario.GetField<object?>(rig.Vm, "_map3DHeldHitTest"));
                Assert.Null(rig.Return);
                Assert.Null(rig.AreaViewJob);
                Assert.False(rig.Moving);
                AssertAt(rig, home, 17, $"{i + 1}회차 나간 자리");
                Assert.Equal(0d, map.Bearing, 3);
            }
            X.AssertMap2DInputAlive(rig.R, "20회 뒤");
        }, Map3DModel.EntryAuto);

    // ─────────────── 10 · 11. 건물 높이 판 + Esc / 지우기 + 링 ───────────────

    [Fact]
    public void should_close_height_panel_only_by_esc_then_leave_and_clear_height_state_when_scene_missing_follows()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            Assert.True(rig.R.PressEsc(), "판이 열려 있으면 Esc 는 판을 닫는다");
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.True(rig.R.Shown);
            Assert.False(rig.R.PressEsc(), "판이 닫히면 Esc 로 3D 를 나가지 않는다");
            Assert.True(rig.R.Shown);

            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "5";
            rig.Posts.Clear();
            rig.SceneMissing(A);
            Assert.Equal("Off", rig.R.State);
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            rig.AssertPreviewReverted(7, "scene-missing");
            Assert.Null(rig.BhJob);
            Assert.Equal(Map3DGuide.SceneMissingText, rig.R.Status);
            Assert.Empty(rig.Server.Requests.Where(r => r.Method == HttpMethod.Put));
            Assert.False(rig.R.PressEsc());
        });

    [Fact]
    public void should_leave_stop_moves_close_panel_and_never_revive_when_area_deleted_while_previewing_and_moving()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.Vm.StartMap3DMove(Map3DNavMove.Forward);
            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            rig.Posts.Clear();

            await rig.ConfirmDelete(A);

            Assert.Equal("Off", rig.R.State);
            Assert.False(rig.Moving);
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            rig.AssertPreviewReverted(7, "영역 지움");
            Assert.Equal(1, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + A));
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == A);
            rig.R.Areas(rig.R.AreaAround(A));                              // 늦은 목록 — 되살리지 않는다
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == A);
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown);
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
            Assert.True(rig.R.W.Map.IsHitTestVisible);
        });

    // ─────────────── 12 · 13. 3D 에서 영역 그리기 → 만들기 / 만들기 판 취소 → 3D ───────────────

    [Fact]
    public void should_enter_new_overlapping_scene_after_area_drawn_from_3d_and_baked_when_auto()
        => RunAsync(async rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.Vm.DrawMap3DAreaCommand.Execute(null);
            Assert.Equal("Off", rig.R.State);
            Assert.True(rig.R.W.Map.IsAnchorDrawMode);

            var bbox = rig.R.AreaAround(A).Bbox;
            Assert.True((bool)Scenario.Call(rig.Vm, "TryTakeMap3DArea", new PointLatLng(bbox[3], bbox[0]), new PointLatLng(bbox[1], bbox[2]))!);
            Assert.False(rig.R.W.Map.IsAnchorDrawMode);
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            rig.Server.PostStatus = "done";
            rig.R.HostReady(NewJob);                                       // 새 장면 페이지(재사용 경로 — WebView2 없음)
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);

            await Until(() => rig.R.Shown && rig.R.Job == NewJob, "다 만든 뒤 자동 — 지도를 안 움직여도 새(겹친) 장면으로 들어간다");
            Assert.Equal(NewJob, rig.Scenes.First().Id);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == A);            // 새 영역 그리기는 옛 영역을 지우지 않는다
            Assert.Equal(0, rig.Server.Count("DELETE"));
            rig.R.PressExit();
            Assert.True(rig.R.W.Map.IsHitTestVisible);
        }, Map3DModel.EntryAuto);

    [Fact]
    public void should_enter_existing_scene_when_new_area_bake_panel_cancelled_then_3d_view_pressed()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.R.HostReady(A);
            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(rig.R.AreaAround("n", d: 0.001).Bbox, null));
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);
            rig.Enter(A);
            Assert.Equal(0, rig.Server.Count(HttpMethod.Post, "/api/jobs"));
            Assert.False(rig.Baking);
        });

    // ─────────────── 14 · 15. 판이 열린 채 그 영역이 사라짐 ───────────────

    public enum Gone { ListMissing, SceneMissing, Deleted }

    /// <summary>
    /// 「다시 만들기」 판을 연 채 그 영역이 사라졌다(다른 곳에서 지움 · 3D 화면 scene-missing · 지우기 확인) — 판을 닫고 다시 만들기 표지를 버린다.
    /// 남기면 [만들기] 가 사라진 영역을 다시 구워 지운 영역이 되살아난다(D1 과 같은 결과).
    /// </summary>
    [Theory]
    [InlineData(Gone.ListMissing)]
    [InlineData(Gone.SceneMissing)]
    [InlineData(Gone.Deleted)]
    public void should_close_rebake_panel_and_send_nothing_when_that_area_disappears_while_panel_open(Gone how)
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(A), rig.R.AreaAround(B, dLon: 0.01));
            rig.OpenRebake(A);
            switch (how)
            {
                case Gone.ListMissing: rig.R.Areas(rig.R.AreaAround(B, dLon: 0.01)); break;
                case Gone.SceneMissing: rig.SceneMissing(A); break;
                case Gone.Deleted: await rig.ConfirmDelete(A); break;
            }
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == A);
            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen, "사라진 영역의 「다시 만들기」 판이 남았다");
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeReplaces"));
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeOptionsSourceId"));
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);           // 늦게 누른 [만들기]
            await Task.Delay(50);
            Assert.Equal(0, rig.Server.Count(HttpMethod.Post, "/api/jobs"));
            Assert.False(rig.Baking);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == B);            // 다른 영역은 그대로
        });

    [Fact]
    public void should_close_zoom_editor_and_send_no_patch_when_that_area_disappears_while_editor_open()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(A), rig.R.AreaAround(B, dLon: 0.01));
            rig.Item(A).Actions.Single(a => a.Header == "3D 줌 바꾸기…").Command.Execute(null);
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.Equal("3D 줌 바꾸기", rig.Vm.Map3DBakeOptionsTitle);
            rig.R.Areas(rig.R.AreaAround(B, dLon: 0.01));                  // 다른 곳에서 A 를 지웠다
            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen, "사라진 영역의 「3D 줌 바꾸기」 판이 남았다");
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Task.Delay(50);
            Assert.Equal(0, rig.Server.Count("PATCH"));
        });

    // ─────────────── 16. 3D 에서 시작한 다시 만들기를 목록에서 취소 ───────────────

    [Fact]
    public void should_keep_old_scene_and_enter_it_when_rebake_started_in_3d_is_cancelled_from_area_menu()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.OpenRebake(A);
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => rig.Server.Count(HttpMethod.Get, "/api/jobs/" + NewJob) >= 1, "진행 조회 시작");
            Assert.True(rig.R.Shown, "3D 에서 시작한 다시 만들기 — 보던 3D 는 그대로");
            rig.R.PressExit();

            var cancel = rig.Item(A).Actions.First();
            Assert.Equal(Map3DAreaItem.CancelBakeHeader, cancel.Header);
            cancel.Command.Execute(null);
            await Until(() => !rig.Baking, "목록 「만들기 취소」로 끝");
            Assert.Equal(Map3DGuide.CancelledBakeText, rig.R.Status);
            Assert.Equal(0, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + A));
            Assert.Equal("다시 만들기", rig.Item(A).Actions.Single(a => a.Header == "다시 만들기").Header);
            rig.Enter(A);
        });

    // ─────────────── 17 · 18. 마커 우선 고르기 + 건물 높이 판 ───────────────

    private static GMapPidsMarker AddCamera(Rig rig, int id)
    {
        var c = rig.R.Center;
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Id = id, Title = $"cam{id}", DeviceType = EnumDeviceType.IpCamera, Latitude = c.Lat, Longitude = c.Lng, Zoom = 1,
        });
        marker.Width = 40; marker.Height = 40; marker.IsLayerEnabled = true; marker.IsVisible = true;
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT", InService = true });
        var status = new DeviceStatusModel();
        status.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "OK", ObservedAt = "2026-09-30T00:00:00.000000+09:00" };
        marker.LinkedDevice = new CameraDeviceModel
        {
            Id = 100 + id, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, DeviceName = $"cam{id}",
            Axes = new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status, Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status" }) },
        };
        rig.R.W.Map.Markers.Add(marker);
        return marker;
    }

    private static void Pick(Rig rig, string action, string kind, int id)
        => rig.From3D(new JObject { ["type"] = "pick", ["action"] = action, ["kind"] = kind, ["id"] = id });

    [Fact]
    public void should_leave_3d_revert_preview_and_open_card_when_marker_clicked_over_building_while_height_preview()
        => Run(rig =>
        {
            var cam = AddCamera(rig, 1);
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            rig.Posts.Clear();

            Pick(rig, "click", "pids", 1);                                // 마커가 건물보다 먼저 — pick 만 온다

            var card = Scenario.GetField<ComponentCardService>(rig.Vm, "_componentCard");
            Assert.Same(cam, card.Current);
            Assert.False(rig.R.Shown, "이 호스트는 WPF 를 덮는다 — 카드를 보려고 2D 로");
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            rig.AssertPreviewReverted(7, "마커 클릭으로 나감");
            Assert.True(rig.Suppressed);
            rig.PickBuilding(7, 9.0);                                     // 2D 로 늦게 온 건물 메시지 — 판을 열지 않는다
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Empty(rig.Server.Requests.Where(r => r.Method == HttpMethod.Put));
        });

    [Fact]
    public void should_leave_with_camera_popup_cause_and_revert_preview_when_camera_double_clicked_while_height_panel_open()
        => Run(rig =>
        {
            Scenario.SetField(rig.Vm, "_overlaySettingsResolved", true);
            Scenario.SetField(rig.Vm, "_cameraPopupHostResolved", true);
            AddCamera(rig, 2);
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.PickBuilding(8, 6.0);
            rig.Vm.BuildingHeightText = "21";
            rig.Posts.Clear();

            Pick(rig, "dblclick", "pids", 2);

            Assert.False(rig.R.Shown);
            Assert.Contains(rig.Infos, l => l.StartsWith("[Map3D] 2D 복귀(camera-popup)", StringComparison.Ordinal));
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            rig.AssertPreviewReverted(8, "카메라 더블클릭으로 나감");
            Assert.True(rig.Suppressed);
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown);
        });

    // ─────────────── 19. 이벤트 이동이 장면 밖/안에서 끝남 + 판 + 이동 ───────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_follow_scene_bounds_with_panel_and_moves_when_event_move_ends_in_3d(bool outside)
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.Vm.StartMap3DMove(Map3DNavMove.Right);
            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            rig.Posts.Clear();

            var c = rig.R.Center;
            rig.R.W.Map.Position = outside ? new PointLatLng(c.Lat, c.Lng + 0.01) : new PointLatLng(c.Lat + 0.0005, c.Lng + 0.0005);
            Scenario.Call(rig.Vm, "Sync3DAfter2DMove", "event-flyto");

            if (outside)
            {
                Assert.False(rig.R.Shown, "장면 밖에서 끝난 이동 — 2D");
                Assert.False(rig.Moving);
                Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
                rig.AssertPreviewReverted(7, "장면 밖 이동");
                rig.R.Gate(); rig.R.Tick();
                Assert.False(rig.R.Shown);
                Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
                Assert.True(rig.R.W.Map.IsHitTestVisible);
            }
            else
            {
                Assert.True(rig.R.Shown, "장면 안 — 3D 카메라만 옮긴다");
                Assert.True(rig.Moving);
                Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
                Assert.Empty(rig.Sent("building.preview"));
                rig.R.PressExit();
                Assert.False(rig.Moving);
            }
        });

    // ─────────────── 20 · 21. Esc · 네이티브 창 전환 + 이동 · 판 ───────────────

    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static bool PressKey(UIElement target, PresentationSource source, Key key, bool down)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = down ? Keyboard.PreviewKeyDownEvent : Keyboard.PreviewKeyUpEvent };
        target.RaiseEvent(e);
        return e.Handled;
    }

    [Fact]
    public void should_keep_key_held_move_in_3d_when_esc_pressed_and_stop_only_when_exit_pressed()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            var ctrl = new Map3DNavControl { DataContext = rig.Vm };
            var source = new HeadlessSource { RootVisual = ctrl };
            Assert.True(PressKey(ctrl, source, Key.W, down: true));
            Assert.True(rig.Moving);
            Assert.False(rig.R.PressEsc(), "WPF 안 3D — Esc 로 안 나간다");
            Assert.True(rig.R.Shown);
            Assert.True(rig.Moving, "Esc 가 누르고 있는 이동을 끊지 않는다");
            rig.R.PressExit();
            Assert.False(rig.Moving);
            Assert.False(rig.NavTimerOn);
            PressKey(ctrl, source, Key.W, down: false);                  // 2D 에서 늦게 온 뗌
            Assert.False(rig.Moving);
            Assert.False(rig.R.PressEsc());
        });

    [Fact]
    public void should_return_to_2d_close_panel_and_stop_moves_when_host_falls_back_while_height_preview_and_moving()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.Vm.StartMap3DMove(Map3DNavMove.ZoomIn);
            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            rig.Posts.Clear();
            var fallback = (Action<string>)typeof(Map3DViewHost).GetField("HostFallback", Inst)!.GetValue(rig.Vm.Map3DHost)!;

            fallback("CompositionControl 실패 → 네이티브 창으로: 시험");

            Assert.False(rig.R.Shown);
            Assert.False(rig.Moving);
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            rig.AssertPreviewReverted(7, "네이티브 창 전환");
            Assert.True(rig.R.W.Map.IsHitTestVisible);
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown, "수동 — 줌으로 다시 안 들어간다");
            rig.Enter(A);
        });

    // ─────────────── 22. 준비 중 「영역 보기」 + scene-missing ───────────────

    [Fact]
    public void should_cancel_pending_area_view_and_leave_no_return_spot_when_scene_missing_arrives_during_guide()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A), rig.R.AreaAround(B, dLon: 0.01));
            rig.R.HostLoading(B);                                        // 불러오는 중(ready 전) — 탐색 없이 기다린다
            rig.R.SetZoom(12);
            rig.R.ViewArea(B);
            Assert.Equal("Pending", rig.R.State);
            Assert.Equal(B, rig.AreaViewJob);

            rig.SceneMissing(B);

            Assert.Equal("Off", rig.R.State);
            Assert.False(rig.Vm.IsMap3DBusy && rig.Vm.Map3DBusyText == Map3DGuide.EnterTitle, "전환 안내가 남았다");
            Assert.Null(rig.Return);
            Assert.Null(rig.AreaViewJob);
            Assert.Null(rig.Vm.Map3DHost.Job);
            Assert.Equal(Map3DGuide.SceneMissingText, rig.R.Status);
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == B);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == A);
            rig.R.Tick();
            Assert.False(rig.R.Shown);

            rig.R.W.Map.Position = rig.R.Center;                          // 남은 영역 A 는 그대로 들어간다
            rig.R.SetZoom(17);
            rig.Enter(A);
            rig.R.PressExit();
            AssertAt(rig, rig.R.Center, 17, "A 진입 직전 자리(B 의 옛 돌아갈 자리가 아니라)");
        });

    // ─────────────── 23. 3D 중 이동을 누른 채 다른 영역 「이 영역 3D 로 보기」 ───────────────

    [Fact]
    public void should_stop_held_move_and_return_to_spot_before_first_entry_when_other_area_viewed_while_moving_in_3d()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A), rig.R.AreaAround(B, dLon: 0.01));
            rig.Enter(A);
            rig.Vm.StartMap3DMove(Map3DNavMove.Left);
            Camera3D(rig, 70, 18.4);
            rig.R.HostReady(B);

            rig.R.ViewArea(B);
            Assert.False(rig.Moving, "다른 영역으로 옮기며 누르던 이동을 멈춘다");
            Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            Assert.Equal(B, rig.R.Job);
            rig.Vm.StartMap3DMove(Map3DNavMove.Back);                     // 새 3D 에서 이동이 다시 된다
            Assert.True(rig.Moving);
            rig.R.PressExit();
            Assert.False(rig.Moving);
            AssertAt(rig, rig.R.Center, 17, "처음 들어가기 전 자리");
            Assert.Equal(0d, rig.R.W.Map.Bearing, 3);
        });

    // ─────────────── 24. 편집 모드 + 판 + 다시 들어가기 ───────────────

    [Fact]
    public void should_reopen_fresh_height_panel_after_edit_mode_cycle_and_button_reentry_when_manual()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.PickBuilding(7, 9.0);
            rig.Vm.BuildingHeightFloorsText = "12";
            Assert.Equal(36.0, rig.Vm.BuildingHeightPreview);
            rig.Posts.Clear();
            try
            {
                rig.Vm.IsEditModeEnabled = true;
                Assert.False(rig.R.Shown);
                Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
                rig.AssertPreviewReverted(7, "편집 모드로 나감");
                rig.R.PressEnter();
                Assert.Equal("Off", rig.R.State);
                Assert.Contains("들어가지 않습니다", rig.R.Status);
            }
            finally
            {
                rig.Vm.IsEditModeEnabled = false;
                Mouse.OverrideCursor = null;
            }
            rig.R.Gate(); rig.R.Tick();
            Assert.False(rig.R.Shown);
            rig.Enter(A);
            rig.PickBuilding(7, 9.0);
            Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Equal(9.0, rig.Vm.BuildingHeightPreview);             // 옛 미리보기(36 m)가 남지 않는다
        });

    // ─────────────── 25. 나간 뒤 늦게 온 3D 메시지 ───────────────

    [Fact]
    public void should_ignore_late_building_cursor_and_camera_messages_after_exit_and_accept_them_after_reentry()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            rig.R.PressExit();
            var spot = rig.R.W.Map.Position;

            rig.PickBuilding(7, 9.0);
            X.From3D(rig.R, new JObject { ["type"] = "cursor", ["lon"] = rig.R.Center.Lng, ["lat"] = rig.R.Center.Lat, ["alt"] = 58.33 });
            Camera3D(rig, 120, 19);
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Null(rig.Vm.CurrentAltitudeText);
            Assert.Equal(spot, rig.R.W.Map.Position);
            Assert.Equal(17d, rig.R.W.Map.EffectiveZoom);

            rig.Enter(A);
            rig.PickBuilding(7, 9.0);
            X.From3D(rig.R, new JObject { ["type"] = "cursor", ["lon"] = rig.R.Center.Lng, ["lat"] = rig.R.Center.Lat, ["alt"] = 58.33 });
            Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Equal("58.3 m", rig.Vm.CurrentAltitudeText);
        });

    // ─────────────── 26. 네이티브 창 3D + 건물 고르기 + Esc ───────────────

    [Fact]
    public void should_refuse_height_panel_in_native_window_and_still_exit_by_esc_and_hold_reentry()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(A));
            rig.Enter(A);
            SetHostElement(rig, new Border());
            try
            {
                Assert.Equal("hwnd", rig.Vm.Map3DHost.HostMode);
                rig.PickBuilding(7, 9.0);
                Assert.False(rig.Vm.IsBuildingHeightPanelOpen, "네이티브 창은 WPF 판을 덮는다 — 열지 않는다");
                Assert.Equal(JTokenType.Null, rig.Sent("building.select").Last()["id"]!.Type);
                Assert.True(rig.R.PressEsc(), "판이 없으니 첫 Esc 로 나간다");
                Assert.False(rig.R.Shown);
                Assert.True(rig.Suppressed);
                Assert.True(rig.R.W.Map.IsHitTestVisible);
                Assert.False(rig.R.PressEsc());
            }
            finally { SetHostElement(rig, null); }
        });

    // ─────────────── 27. 회전 영역 만들기 판 취소 직후 회전 지도에서 게이트 ───────────────

    [Fact]
    public void should_keep_rotated_area_gate_and_enter_by_button_when_rotated_draw_cancelled_on_rotated_map()
        => Rotated(() => Run(rig =>
        {
            var map = rig.R.W.Map;
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 300, 160, 45);
            rig.R.Areas(new Map3DSceneInfo("rot", null, rect.Bbox(), null, 3, "job") { Rect = rect, EnterZoom = 17 });
            rig.R.HostReady("rot");
            map.SetMapRotation(45);
            Scenario.Pump(DispatcherPriority.Background);

            rig.Vm.DrawMap3DAreaCommand.Execute(null);                    // 회전 지도에서 새 영역 그리기 → 판 [취소]
            Assert.True(map.IsAnchorDrawMode);
            var corners = new[] { new PointLatLng(c.Lat + 0.0005, c.Lng - 0.0005), new PointLatLng(c.Lat + 0.0005, c.Lng + 0.0005),
                                  new PointLatLng(c.Lat - 0.0005, c.Lng + 0.0005), new PointLatLng(c.Lat - 0.0005, c.Lng - 0.0005) };
            var area = (Map3DArea)Scenario.Call(rig.Vm, "Map3DAreaOf", (IReadOnlyList<PointLatLng>)corners)!;
            Assert.NotNull(area.Rect);                                    // 회전 지도 = 회전 영역
            Scenario.Call(rig.Vm, "ExitAnchorDrawMode");
            Scenario.Call(rig.Vm, "TakeMap3DArea", area);
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            rig.Vm.CancelMap3DBakeOptionsCommand.Execute(null);

            var corner = new PointLatLng(rect.Bbox()[3] - 0.00003, rect.Bbox()[2] - 0.00003);
            map.Position = corner;
            rig.R.Gate();
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible, "옛 회전 영역 판정은 그대로 — 모서리는 밖");
            map.Position = c;
            rig.R.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            Assert.Equal(0, rig.Server.Count(HttpMethod.Post, "/api/jobs"));
        }));
}
