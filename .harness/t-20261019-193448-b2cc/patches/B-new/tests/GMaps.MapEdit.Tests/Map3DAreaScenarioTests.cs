using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 가짜 map-3d 서버(영역 B 전수 시나리오) — 요청을 본문까지 적고, 굽기 상태 · 지우기 응답 · 목록을 시험이 정한다.
/// 목록(GET /api/jobs)은 기본 503 — 받은 것으로 친 목록은 시험이 ApplyMap3DAreas 로 직접 단다(배경 스레드에서 지도를 만지지 않게, Map3DStaleJobScenarioTests 와 같은 이유).
/// </summary>
internal sealed class Map3DFakeServer : HttpMessageHandler
{
    public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();
    /// <summary>POST /api/jobs 가 차례로 돌려줄 작업 id(비면 "j-new").</summary>
    public Queue<string> NextIds { get; } = new();
    /// <summary>POST /api/jobs 응답 상태 — "done" 이면 진행 조회 없이 끝난다.</summary>
    public string PostStatus { get; set; } = "queued";
    /// <summary>GET /api/jobs/{id} 상태 — 바꾸면 다음 조회부터.</summary>
    public string Status { get; set; } = "running";
    /// <summary>DELETE /api/jobs/{id} 응답 코드(200 = 지움).</summary>
    public HttpStatusCode DeleteCode { get; set; } = HttpStatusCode.OK;
    public bool DeleteUnreachable { get; set; }
    public string CancelResult { get; set; } = "requested";
    public bool PostUnreachable { get; set; }
    /// <summary>POST /api/jobs 를 400 으로 거절할 detail(null 이면 받음).</summary>
    public string? PostReject { get; set; }
    /// <summary>GET /api/jobs/{id} 의 message(실패 이유 등).</summary>
    public string? JobMessage { get; set; } = "OSM 조회";

    public int Count(HttpMethod m, string path) { lock (Requests) return Requests.Count(r => r.Method == m && r.Path == path); }
    public List<JObject> Posts() { lock (Requests) return Requests.Where(r => r.Method == HttpMethod.Post && r.Path == "/api/jobs").Select(r => JObject.Parse(r.Body!)).ToList(); }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
        lock (Requests) Requests.Add((request.Method, path, body));
        if (request.Method == HttpMethod.Post && path == "/api/jobs" && PostUnreachable) throw new HttpRequestException("연결 거부");
        if (request.Method == HttpMethod.Post && path == "/api/jobs" && PostReject is { } detail)
            return Json(HttpStatusCode.BadRequest, new JObject { ["detail"] = detail });
        if (request.Method == HttpMethod.Post && path == "/api/jobs")
            return Json(HttpStatusCode.Accepted, new JObject { ["id"] = NextIds.Count > 0 ? NextIds.Dequeue() : "j-new", ["status"] = PostStatus, ["progress"] = 0 });
        if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.Ordinal))
        {
            Status = "cancelled";
            return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^2], ["result"] = CancelResult, ["status"] = Status });
        }
        if (request.Method == HttpMethod.Delete)
        {
            if (DeleteUnreachable) throw new HttpRequestException("연결 거부");
            return DeleteCode == HttpStatusCode.OK ? Json(HttpStatusCode.OK, new JObject { ["deleted"] = path.Split('/')[^1] })
                                                   : Json(DeleteCode, new JObject { ["detail"] = $"지울 수 없음 {(int)DeleteCode}" });
        }
        if (request.Method == HttpMethod.Get && path.StartsWith("/api/jobs/", StringComparison.Ordinal))
            return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^1], ["status"] = Status, ["progress"] = 0.4, ["message"] = JobMessage });
        if (request.Method.Method == "PATCH") return Json(HttpStatusCode.OK, JObject.Parse(body ?? "{}"));
        if (path == "/api/scenes/at") return Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" });
        return Json(HttpStatusCode.ServiceUnavailable, new JObject { ["detail"] = "목록은 이 시험 밖" });
    }

    private static HttpResponseMessage Json(HttpStatusCode code, JObject o) => new(code) { Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json") };
}

/// <summary>
/// 3D 영역 시나리오(영역 B 전수, 2026-10-19) — 실제 MapViewModel 영역 표시 · 게이트 · 다시 만들기 · 지우기 · 3D 화면 error 경로.
/// 창 · WebView2 없음(Map3DEntryScenarioTests.Rig — 3D 호스트에는 상태만 꽂는다), map-3d 는 <see cref="Map3DFakeServer"/>.
/// </summary>
public class Map3DAreaScenarioTests
{
    private const string X = "area-x", Y = "area-y", NewJob = "j-new";

    private sealed class Rig
    {
        public Map3DEntryScenarioTests.Rig R { get; }
        public MapViewModel Vm => R.Vm;
        public Map3DFakeServer Server { get; } = new();
        public List<string> Infos { get; } = new();
        public List<string> Warnings { get; } = new();

        public Rig(string mode)
        {
            R = new Map3DEntryScenarioTests.Rig(mode);
            R.Settings.OverlayMode = Map3DBake.ModeTiles;   // 만들기 확정이 「마지막 굽기 방식」을 설정 파일에 저장하지 않게(겹치는 이미지 없음 = 타일)
            R.W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                   .Callback<string, string, string, int>((m, _, _, _) => { lock (Infos) Infos.Add(m); });
            R.W.Log.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                   .Callback<string, string, string, int>((m, _, _, _) => { lock (Warnings) Warnings.Add(m); });
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
        }

        /// <summary>미리 불러오기가 일어나지 않는 줌(15.5 &lt; 미리 준비 줌 16) — 큐에 쌓인 게이트가 돌아도 WebView2 를 만들지 않게.</summary>
        public void NoPreload() { R.Settings.PreloadZoom = 16; R.SetZoom(15.5); }

        public Map3DAreaItem Area(string id) => Vm.Map3DAreas.Single(a => a.Id == id);
        public bool Baking => Scenario.GetField<bool>(Vm, "_isMap3DBaking");
        public List<Map3DScene> Scenes => Scenario.GetField<List<Map3DScene>>(Vm, "_map3DScenes");
        public string Status => Vm.AimStatusMessage ?? "";

        /// <summary>배지 · 목록 「다시 만들기」 → 만들기 판 [만들기].</summary>
        public void Rebake(string id)
        {
            Area(id).Actions.Single(a => a.Header == "다시 만들기").Command.Execute(null);
            Assert.True(Vm.IsMap3DBakeOptionsOpen);
            Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
        }

        /// <summary>「지우기…」 확인(표준 확인 창의 "확인" → HandleAsync).</summary>
        public Task ConfirmDelete(string id)
        {
            Scenario.SetField(Vm, "_pendingMap3DAreaDelete", Area(id));
            return Vm.HandleAsync(new CallDeleteMap3DAreaProcessMessageModel(), CancellationToken.None);
        }

        public void From3D(JObject json) => Scenario.Call(Vm, "OnMap3DMessage", Map3DMath.ParseMessage(json.ToString())!);

        public void Done() => R.Done();
    }

    private static void Run(Action<Rig> body, string mode = Map3DModel.EntryManual)
        => Scenario.Sta(() => { var rig = new Rig(mode); body(rig); rig.Done(); });

    private static void RunAsync(Func<Rig, Task> body, string mode = Map3DModel.EntryManual)
        => Scenario.StaAsync(async () => { var rig = new Rig(mode); await body(rig); rig.Done(); });

    private static async Task Until(Func<bool> condition, string what, double seconds = 6)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end && !condition()) await Task.Delay(20);
        Assert.True(condition(), what);
    }

    // ─────────────── 표시 ───────────────

    /// <summary>B-19 — 줌 13 밑에서는 테두리 · 배지를 접는다(Refresh(false)), 13 이상이면 다시 보인다.</summary>
    [Fact]
    public void should_collapse_area_outline_below_zoom_13_and_show_again_at_13()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(X));
            rig.R.HostReady(X);   // 줌 15 이상에서 게이트가 미리 불러오기(WebView2)를 하지 않게 — 이미 열어 둔 장면
            var marker = Scenario.GetField<Dictionary<string, Map3DAreaMarker>>(rig.Vm, "_map3DAreaMarkers")[X];
            foreach (var (zoom, visible) in new[] { (12d, false), (12.5d, false), (13d, true), (16d, true), (12d, false) })
            {
                rig.R.SetZoom(zoom);
                rig.R.Gate();
                Assert.Equal(visible ? Visibility.Visible : Visibility.Collapsed, ((UIElement)marker.Shape).Visibility);
            }
        });

    /// <summary>B-14 — 3D 를 보는 중 「영역 그리기」 → 먼저 2D 로 돌아와 그리기 모드(그리는 동안 줌 · 단추로 다시 들어가지 않는다).</summary>
    [Fact]
    public void should_leave_3d_first_when_draw_area_pressed_while_3d_shown()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(X));
            rig.R.HostReady(X); rig.R.Gate();
            rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            Assert.True(rig.R.Shown);
            try
            {
                rig.Vm.DrawMap3DAreaCommand.Execute(null);
                Assert.Equal("Off", rig.R.State);
                Assert.True(rig.R.W.Map.IsAnchorDrawMode);
                Assert.True(Scenario.GetField<bool>(rig.Vm, "_isMap3DDrawMode"));
                rig.R.Gate(); rig.R.Tick();
                Assert.Equal("Off", rig.R.State);
                rig.R.PressEnter();
                Assert.Equal("Off", rig.R.State);
                Assert.Contains("들어가지 않습니다", rig.Status);
            }
            finally { Scenario.Call(rig.Vm, "ExitAnchorDrawMode"); }
        });

    // ─────────────── 게이트 — 회전 영역 안/밖 ───────────────

    /// <summary>B-23 — 화면 가운데가 감싸는 bbox 모서리(회전 사각형 밖)면 「3D 로 보기」 · 칩 · 줌 진입 모두 없음, 사각형 안이면 있다.</summary>
    [Theory]
    [InlineData(Map3DModel.EntryManual)]
    [InlineData(Map3DModel.EntryAuto)]
    public void should_use_rotated_rect_not_bbox_when_center_in_bbox_corner(string mode)
        => Run(rig =>
        {
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 300, 160, 45);
            var b = rect.Bbox();
            rig.R.Areas(new Map3DSceneInfo("rot", null, b, null, 3, "job") { Rect = rect, EnterZoom = 17 });
            rig.R.HostReady("rot");   // 들어간다면 재사용 경로(WebView2 없음)

            var corner = new PointLatLng(b[3] - 0.00003, b[2] - 0.00003);   // 북동 모서리 — bbox 안, 회전 사각형 밖
            Assert.True(Map3DMath.BboxContains(b, corner.Lng, corner.Lat));
            Assert.False(rect.Contains(corner.Lng, corner.Lat));
            rig.R.W.Map.Position = corner;
            rig.R.SetZoom(16); rig.R.Gate();
            Assert.False(rig.Vm.IsMap3DChipVisible, "사각형 밖 — 칩 없음");
            rig.R.SetZoom(17); rig.R.Gate(); rig.R.Tick();
            Assert.Equal("Off", rig.R.State);                         // 자동이어도 줌으로 들어가지 않는다
            Assert.False(rig.Vm.IsMap3DEnterButtonVisible);
            Assert.False(rig.Vm.IsMap3DChipVisible);

            rig.R.W.Map.Position = new PointLatLng(rect.Lat, rect.Lon);   // 사각형 안
            rig.R.SetZoom(16); rig.R.Gate();
            Assert.Equal(mode == Map3DModel.EntryAuto, rig.Vm.IsMap3DChipVisible);   // 칩은 자동 · 진입 줌 밑에서만
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible);
            rig.R.SetZoom(17); rig.R.Gate(); Map3DEntryScenarioTests.SettleEntry(rig.R);
            if (mode == Map3DModel.EntryAuto) { Assert.True(rig.R.Shown); Assert.Equal("rot", rig.R.Job); }
            else { Assert.False(rig.R.Shown); Assert.True(rig.Vm.IsMap3DEnterButtonVisible); }
        }, mode);

    /// <summary>
    /// B-28 — 회전 영역 「이 영역 3D 로 보기」: 3D 화면에 보내는 enter-view 방위 = rect 회전각. WebView2 없이는 PostJson 을 가로챌 수 없어
    /// Reveal3D 가 그 방위를 셈하는 입력(「영역 보기」 job = 보는 job, 장면 캐시의 rect)과 같은 식(Map3DMath.EnterBearing)을 확인한다.
    /// </summary>
    [Fact]
    public void should_open_area_view_with_rect_rotation_when_rotated_area_viewed()
        => Run(rig =>
        {
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng + 0.01, c.Lat - 0.005, 300, 160, 300);
            rig.R.Areas(new Map3DSceneInfo("rot", null, rect.Bbox(), null, 3, "job") { Rect = rect });
            rig.R.HostReady("rot");
            rig.R.SetZoom(12); rig.R.Gate();

            rig.R.ViewArea("rot");
            Map3DEntryScenarioTests.SettleEntry(rig.R);

            Assert.True(rig.R.Shown);
            Assert.Equal("rot", rig.R.Job);
            Assert.Equal("rot", Scenario.GetField<string?>(rig.Vm, "_map3DAreaViewJob"));
            Assert.Equal(rect.Lat, rig.R.W.Map.Position.Lat, 6);
            Assert.Equal(rect.Lon, rig.R.W.Map.Position.Lng, 6);
            var cached = rig.Scenes.Single(s => s.Id == "rot").Rect!;
            Assert.Equal(300d, Map3DMath.EnterBearing(rig.R.W.Map.Bearing, cached), 6);   // Reveal3D 의 식 그대로
            rig.R.PressExit();
            Assert.Equal(12d, rig.R.W.Map.EffectiveZoom);
        });

    // ─────────────── 굽는 중 다시 누름 ───────────────

    /// <summary>B-36 · B-53 — 굽는 중 [만들기] · 「다시 만들기」 → "이미 3D 를 만드는 중" · POST 더 없음 · 「영역 그리기」 막힘.</summary>
    [Fact]
    public void should_refuse_second_bake_and_disable_draw_when_already_baking()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(X, dLon: 0.01));
            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(rig.R.AreaAround("a").Bbox, null));
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            await Until(() => rig.Server.Count(HttpMethod.Get, "/api/jobs/" + NewJob) >= 1, "진행 조회 시작");
            Assert.True(rig.Baking);
            Assert.False(rig.Vm.DrawMap3DAreaCommand.CanExecute(null));

            rig.Area(X).Actions.Single(a => a.Header == "다시 만들기").Command.Execute(null);   // B-53
            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.Equal("이미 3D 를 만드는 중입니다", rig.Status);

            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(rig.R.AreaAround("b", dLon: -0.01).Bbox, null));   // 열려 있던 판의 [만들기]
            Assert.True(rig.Vm.IsMap3DBakeOptionsOpen);
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            Assert.False(rig.Vm.IsMap3DBakeOptionsOpen);
            Assert.Equal("이미 3D 를 만드는 중입니다", rig.Status);
            await Task.Delay(100);
            Assert.Single(rig.Server.Posts());

            rig.Server.Status = "done";
            await Until(() => !rig.Baking, "굽기 끝");
            Assert.True(rig.Vm.DrawMap3DAreaCommand.CanExecute(null));
            Assert.Equal(0, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + X));   // 새 영역 — 옛 영역은 그대로
        });

    // ─────────────── 다시 만드는 중 지우기(D1) ───────────────

    /// <summary>
    /// B-50 ★결함 D1 — 다시 만드는 중인 그 영역의 「지우기…」: 메뉴에서 빠지고(「만들기 취소」가 맨 위), 어떻게든 지우기 확인이 와도 지우지 않고 안내한다.
    /// 예전에는 지웠다가 굽기가 끝나면 새 장면이 _map3DScenes 앞에 들어가 지운 영역이 되살아났다. 다 만든 뒤에는 정상으로 지운다.
    /// </summary>
    [Fact]
    public void should_refuse_delete_of_area_being_rebaked_and_allow_after_bake()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(X));
            Assert.Contains(rig.Area(X).Actions, a => a.Header == "지우기…");
            rig.Rebake(X);
            await Until(() => rig.Area(X).IsBaking, "다시 만드는 영역 표시");

            Assert.Equal(Map3DAreaItem.CancelBakeHeader, rig.Area(X).Actions[0].Header);
            Assert.DoesNotContain(rig.Area(X).Actions, a => a.Header == "지우기…");

            await rig.ConfirmDelete(X);   // 메뉴가 바뀌기 전에 열어 둔 메뉴 · 확인 창 등 — 지우기 확인이 그래도 왔다
            Assert.Equal(0, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + X));
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.Contains(rig.Scenes, s => s.Id == X);
            Assert.Contains("만드는 중에는 지울 수 없습니다", rig.Status);
            Assert.True(rig.Baking);

            rig.Server.Status = "done";
            await Until(() => !rig.Baking && rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + X) == 1, "다시 만들기 끝 — 옛 장면만 지움");
            Assert.Equal(NewJob, rig.Scenes[0].Id);
            rig.R.Areas(rig.R.AreaAround(NewJob));   // 서버 목록
            Assert.Contains(rig.Area(NewJob).Actions, a => a.Header == "지우기…");
            await rig.ConfirmDelete(NewJob);
            Assert.Equal(1, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + NewJob));
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == NewJob);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == NewJob);
        });

    /// <summary>B-50 — 지우기 확인 창을 열기 전에도 막는다(안내만, 확인 창 없음).</summary>
    [Fact]
    public void should_not_open_delete_confirm_when_area_being_rebaked()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(X));
            var stale = rig.Area(X);   // 굽기 전에 연 메뉴의 항목(「지우기…」가 아직 있다)
            rig.Rebake(X);
            await Until(() => rig.Area(X).IsBaking, "다시 만드는 영역 표시");

            stale.Actions.Single(a => a.Header == "지우기…").Command.Execute(null);
            await Task.Delay(50);
            Assert.DoesNotContain(rig.R.W.Published, m => m is OpenConfirmPopupMessageModel);
            Assert.Contains("만드는 중에는 지울 수 없습니다", rig.Status);

            rig.Server.Status = "done";
            await Until(() => !rig.Baking, "굽기 끝");
        });

    /// <summary>B-51 — 굽는 중 다른 영역 지우기 → 그 영역만 빠지고 굽기는 계속.</summary>
    [Fact]
    public void should_delete_other_area_and_keep_baking_when_area_deleted_during_bake()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.R.Areas(rig.R.AreaAround(X), rig.R.AreaAround(Y, dLon: 0.01));
            rig.Rebake(X);
            await Until(() => rig.Area(X).IsBaking, "다시 만드는 영역 표시");

            await rig.ConfirmDelete(Y);
            Assert.Equal(1, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + Y));
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == Y);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == Y);
            Assert.True(rig.Baking);
            Assert.True(rig.Area(X).IsBaking);
            Assert.True(rig.Vm.IsMap3DCancelBakeVisible || rig.Vm.IsMap3DBusy);

            rig.Server.Status = "done";
            await Until(() => !rig.Baking && rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + X) == 1, "다시 만들기 끝");
            Assert.Equal(NewJob, rig.Scenes[0].Id);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == Y);
            rig.R.Areas(rig.R.AreaAround(NewJob), rig.R.AreaAround(Y, dLon: 0.01));   // 지우기 전에 보낸 목록의 늦은 응답
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == Y);
        });

    // ─────────────── 다시 만들기 끝 — 옛 장면 지우기 실패 ───────────────

    /// <summary>B-59 — 다 만든 뒤 옛 장면 DELETE 가 405 · 닿지 않음 → 옛 영역 유지 · 경고 로그 · 새 장면이 앞.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_keep_old_area_and_warn_when_old_scene_delete_fails_after_rebake(bool unreachable)
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            if (unreachable) rig.Server.DeleteUnreachable = true; else rig.Server.DeleteCode = HttpStatusCode.MethodNotAllowed;
            rig.R.Areas(rig.R.AreaAround(X));
            rig.Server.PostStatus = "done";
            rig.Rebake(X);
            await Until(() => !rig.Baking, "다시 만들기 끝");

            Assert.Equal(NewJob, rig.Scenes[0].Id);
            Assert.Contains(rig.Scenes, s => s.Id == X);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.Equal(NewJob, ((Map3DScene?)Scenario.Call(rig.Vm, "FindMap3DSceneAt", rig.R.Center))!.Id);   // 겹치면 새 장면이 먼저
            Assert.Contains(rig.Warnings, w => w.StartsWith($"[Map3D] 다시 만들기 — 옛 장면 {X} 지우기 실패(새 장면 {NewJob} 이 앞선다)", StringComparison.Ordinal));
            Assert.Contains("3D 준비됨", rig.Status);
        });

    /// <summary>B-59 — 옛 장면이 서버에 이미 없음(404) → 목록이 판단(정보 로그), 상태는 지우지 않는다.</summary>
    [Fact]
    public void should_leave_old_area_to_server_list_when_old_scene_already_gone_after_rebake()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.Server.DeleteCode = HttpStatusCode.NotFound;
            rig.R.Areas(rig.R.AreaAround(X));
            rig.Server.PostStatus = "done";
            rig.Rebake(X);
            await Until(() => !rig.Baking, "다시 만들기 끝");
            Assert.Contains(rig.Infos, i => i == $"[Map3D] 다시 만들기 — 옛 장면 {X} 은 서버에 없음(목록이 판단) → {NewJob}");
            rig.R.Areas(rig.R.AreaAround(NewJob));   // 서버 목록에 옛 영역이 없다 → 내려놓는다
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == X);
        });

    // ─────────────── 지우기 — 서버에 닿지 않음 ───────────────

    /// <summary>
    /// B-60 — 지우기 DELETE 가 서버에 닿지 않음 → "꺼져" 안내 · 영역 유지(되살리기 막기 목록에도 넣지 않는다).
    /// 보던 3D 였으면 DELETE 를 보내기 전에 2D 로 먼저 나간다(D6 — 지금 동작 기록, 결정 필요).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_keep_area_and_tell_server_off_when_delete_cannot_reach_server(bool showing)
        => RunAsync(async rig =>
        {
            rig.R.Areas(rig.R.AreaAround(X));
            rig.R.HostReady(X);   // 이미 열어 둔 장면 — 게이트가 미리 불러오기(WebView2)를 하지 않게
            if (showing)
            {
                rig.R.Gate();
                rig.R.PressEnter(); Map3DEntryScenarioTests.SettleEntry(rig.R);
                Assert.True(rig.R.Shown);
            }
            rig.Server.DeleteUnreachable = true;

            await rig.ConfirmDelete(X);

            Assert.Equal(1, rig.Server.Count(HttpMethod.Delete, "/api/jobs/" + X));
            Assert.Contains("꺼져", rig.Status);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.Contains(rig.Scenes, s => s.Id == X);
            Assert.DoesNotContain(X, Scenario.GetField<HashSet<string>?>(rig.Vm, "_map3DGoneJobs") ?? new HashSet<string>());
            Assert.Equal("Off", rig.R.State);   // D6: 보던 3D 는 지우기 결과와 상관없이 이미 2D(지금 동작)
            rig.R.Gate();
            Assert.True(rig.Vm.IsMap3DEnterButtonVisible, "영역이 남아 다시 들어갈 수 있다");
        });

    // ─────────────── 회전 영역 다시 만들기 ───────────────

    /// <summary>B-61 — 회전 영역 「다시 만들기」 → POST rect 가 같은 회전각 · 크기 · 가운데, bbox == rect.Bbox(), 줌은 그 영역 줌.</summary>
    [Fact]
    public void should_post_same_rect_when_rotated_area_rebaked()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            var c = rig.R.Center;
            var rect = new Map3DRotRect(c.Lng, c.Lat, 640.5, 220.25, 33.5);
            rig.R.Areas(new Map3DSceneInfo("rot", null, rect.Bbox(), null, 3, "job") { Rect = rect, EnterZoom = 18 });
            rig.Server.PostStatus = "done";
            rig.Rebake("rot");
            await Until(() => !rig.Baking, "다시 만들기 끝");

            var post = Assert.Single(rig.Server.Posts());
            var r = (JObject)post["rect"]!;
            Assert.Equal((rect.Lon, rect.Lat, rect.WidthM, rect.HeightM, rect.RotationDeg),
                         (r.Value<double>("lon"), r.Value<double>("lat"), r.Value<double>("width_m"), r.Value<double>("height_m"), r.Value<double>("rotation_deg")));
            Assert.Equal(rect.Bbox(), post["bbox"]!.ToObject<double[]>());
            Assert.Equal(18d, post.Value<double>("enter_zoom"));
            Assert.Null(post["overlay_mode"]);
            var made = rig.Scenes[0];
            Assert.Equal(NewJob, made.Id);
            Assert.Equal(rect, made.Rect);   // 새 장면도 같은 회전 사각형으로 안/밖을 판정
        });

    // ─────────────── 3D 화면 error ───────────────

    /// <summary>B-67 — reason=server-unreachable → 안내 · 2D · 영역 유지 / job 없는 scene-missing → 무시.</summary>
    [Fact]
    public void should_tell_server_off_and_keep_area_when_viewer_reports_server_unreachable()
        => Run(rig =>
        {
            rig.R.Areas(rig.R.AreaAround(X));
            rig.R.HostLoading(X);
            rig.R.Gate();
            rig.R.PressEnter();
            Assert.Equal("Pending", rig.R.State);

            rig.From3D(new JObject { ["type"] = "error", ["reason"] = "scene-missing" });   // job 없음 — 무엇을 지울지 모른다
            Assert.Equal("Pending", rig.R.State);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.Equal(0, rig.Server.Count(HttpMethod.Get, "/api/jobs"));

            rig.From3D(new JObject { ["type"] = "error", ["reason"] = "server-unreachable", ["job"] = X });
            Assert.Equal("Off", rig.R.State);
            Assert.Equal("3D 서버(map-3d)가 꺼져 있습니다", rig.Status);
            Assert.Contains(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.Contains(rig.Scenes, s => s.Id == X);
            Assert.DoesNotContain(X, Scenario.GetField<HashSet<string>?>(rig.Vm, "_map3DGoneJobs") ?? new HashSet<string>());
            Assert.Contains(rig.Warnings, w => w == $"[Map3D] 3D 화면 불러오기 실패 job={X} reason=server-unreachable");
        });

    /// <summary>B-68 — 다시 만드는 원본 장면이 3D 화면에서 scene-missing → 원본만 내려놓고 굽기는 계속, 끝나면 새 장면 등록.</summary>
    [Fact]
    public void should_keep_baking_and_register_new_scene_when_rebake_source_reported_missing()
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            rig.Server.DeleteCode = HttpStatusCode.NotFound;   // 원본은 이미 서버에 없다
            rig.R.Areas(rig.R.AreaAround(X));
            rig.R.HostReady(X);
            rig.Rebake(X);
            await Until(() => rig.Area(X).IsBaking, "다시 만드는 영역 표시");

            rig.From3D(new JObject { ["type"] = "error", ["reason"] = "scene-missing", ["job"] = X });
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == X);
            Assert.True(rig.Baking);
            Assert.Equal(Map3DGuide.SceneMissingText, rig.Status);

            rig.Server.Status = "done";
            await Until(() => !rig.Baking, "다시 만들기 끝");
            Assert.Equal(NewJob, rig.Scenes[0].Id);
            Assert.Contains("3D 준비됨", rig.Status);
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeSourceId"));
            rig.R.Areas(rig.R.AreaAround(NewJob), rig.R.AreaAround(X));   // 늦은 목록에 원본이 남아 있어도 되살리지 않는다
            Assert.Equal(new[] { NewJob }, rig.Vm.Map3DAreas.Select(a => a.Id).ToArray());
        });

    /// <summary>B-66 — 이미 서버에서 지워진 영역(DELETE 404) 지우기 → "이미 지워진" 안내 · 표시에서 뺀다.</summary>
    [Fact]
    public void should_remove_area_and_say_already_deleted_when_delete_returns_404()
        => RunAsync(async rig =>
        {
            rig.R.Areas(rig.R.AreaAround(X));
            rig.R.HostReady(X);
            rig.Server.DeleteCode = HttpStatusCode.NotFound;
            await rig.ConfirmDelete(X);
            Assert.Equal("이미 지워진 3D 영역입니다", rig.Status);
            Assert.DoesNotContain(rig.Vm.Map3DAreas, a => a.Id == X);
            Assert.DoesNotContain(rig.Scenes, s => s.Id == X);
            Assert.Contains(rig.Infos, i => i == $"[Map3D] 영역 지움 id={X} removed=False");
        });

    // ─────────────── 만들기 시작 · 진행 실패 ───────────────

    public enum BakeFailure { Unreachable, Rejected, JobError }

    /// <summary>B-73 · B-74 · B-75 — 서버에 닿지 않음 · 400 거절 · 작업 error → 이유 안내, 만들기 전 상태(다시 그리기 가능), 장면 없음.</summary>
    [Theory]
    [InlineData(BakeFailure.Unreachable, "3D 서버(map-3d)가 꺼져 있습니다")]
    [InlineData(BakeFailure.Rejected, "3D 를 만들 수 없습니다: bbox 한 변이 2000 m 를 넘습니다")]
    [InlineData(BakeFailure.JobError, "3D 만들기 실패: OSM 서버 응답 없음")]
    public void should_tell_reason_and_return_to_before_bake_when_bake_fails(BakeFailure failure, string expected)
        => RunAsync(async rig =>
        {
            rig.NoPreload();
            switch (failure)
            {
                case BakeFailure.Unreachable: rig.Server.PostUnreachable = true; break;
                case BakeFailure.Rejected: rig.Server.PostReject = "bbox 한 변이 2000 m 를 넘습니다"; break;
                case BakeFailure.JobError: rig.Server.JobMessage = "OSM 서버 응답 없음"; break;
            }
            Scenario.Call(rig.Vm, "TakeMap3DArea", new Map3DArea(rig.R.AreaAround("a").Bbox, null));
            rig.Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
            if (failure == BakeFailure.JobError)
            {
                await Until(() => rig.Server.Count(HttpMethod.Get, "/api/jobs/" + NewJob) >= 1, "진행 조회 시작");
                rig.Server.Status = "error";
            }
            await Until(() => !rig.Baking, "만들기 끝");

            Assert.Equal(expected, rig.Status);
            Assert.False(rig.Vm.IsMap3DBusy);
            Assert.False(rig.Vm.IsMap3DCancelBakeVisible);
            Assert.True(rig.Vm.DrawMap3DAreaCommand.CanExecute(null));
            Assert.Empty(rig.Scenes);
            Assert.Null(Scenario.GetField<string?>(rig.Vm, "_map3DBakeJob"));
        });
}
