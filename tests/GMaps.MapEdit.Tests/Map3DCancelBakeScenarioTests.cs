using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 만들기 중 취소(2026-10-19) — 창 · WebView2 없이 실제 MapViewModel 굽기 경로(영역 → 만들기 판 [만들기] → 진행 조회)를
/// 가짜 map-3d 서버(HttpMessageHandler)로 돌린다.
/// </summary>
public class Map3DCancelBakeScenarioTests
{
    private const string NewJob = "j-new", OldArea = "j-old";

    // ─────────────── 장치 ───────────────

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();
        /// <summary>GET /api/jobs/{id} 가 돌려줄 상태 — 바꾸면 다음 조회부터.</summary>
        public string Status { get; set; } = "running";
        /// <summary>취소 응답 — null 이면 옛 서버(404 Not Found).</summary>
        public string? CancelResult { get; set; } = "requested";
        /// <summary>취소를 받으면 작업 상태를 이것으로(서버가 멈춘 것처럼). null 이면 그대로.</summary>
        public string? StatusAfterCancel { get; set; } = "cancelled";
        public bool CancelUnreachable { get; set; }
        public TaskCompletionSource? CancelGate { get; set; }
        /// <summary>목록(GET /api/jobs)에 올릴 장면.</summary>
        public List<JObject> Scenes { get; } = new();
        private int _jobs;

        public int Count(HttpMethod m, string pathEnd) => Requests.Count(r => r.Method == m && r.Path.EndsWith(pathEnd, StringComparison.Ordinal));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method, path));
            if (request.Method == HttpMethod.Post && path == "/api/jobs")
            {
                _jobs++;
                return Json(HttpStatusCode.Accepted, new JObject { ["id"] = _jobs == 1 ? NewJob : $"{NewJob}{_jobs}", ["status"] = "queued", ["progress"] = 0 });
            }
            if (request.Method == HttpMethod.Get && path == "/api/jobs")
                return Json(HttpStatusCode.OK, new JObject { ["scenes"] = new JArray(Scenes.ToArray()), ["running"] = new JArray() });
            if (request.Method == HttpMethod.Post && path.EndsWith("/cancel", StringComparison.Ordinal))
            {
                if (CancelGate != null) await CancelGate.Task;
                if (CancelUnreachable) throw new HttpRequestException("연결 거부");
                if (CancelResult == null) return Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" });
                if (StatusAfterCancel != null && CancelResult is "requested" or "cancelled") Status = StatusAfterCancel;
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^2], ["result"] = CancelResult, ["status"] = Status });
            }
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/jobs/", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, new JObject { ["id"] = path.Split('/')[^1], ["status"] = Status, ["progress"] = 0.4, ["message"] = "OSM 조회" });
            return Json(HttpStatusCode.NotFound, new JObject { ["detail"] = "Not Found" });
        }

        private static HttpResponseMessage Json(HttpStatusCode code, JObject o) => new(code) { Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; }
        public MapViewModel Vm => W.Vm;
        public FakeServer Server { get; } = new();
        public PointLatLng Center { get; }
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        public Rig()
        {
            W = WiredMapRig.Create();
            Scenario.SetField(Vm, "_isEditModeEnabled", false);
            W.Map.SetEditMode(false);
            W.Map.SetEffectiveZoom(12d);   // 미리 준비 줌(15) 아래 — 다 만든 뒤 게이트가 3D 화면(WebView2)을 미리 열지 않게
            Center = W.Map.Position;
            var settings = new Map3DModel { EntryMode = Map3DModel.EntryManual, EnterZoom = 17, ExitZoom = 16.5, GuideMinSeconds = 0, IsEnabled = true };
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
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
        }

        public double[] Bbox => new[] { Center.Lng - 0.002, Center.Lat - 0.002, Center.Lng + 0.002, Center.Lat + 0.002 };

        /// <summary>이미 만든 영역(옛 장면) — 서버 목록 · 2D 영역 표시에 올린다.</summary>
        public void OldAreaExists()
        {
            Server.Scenes.Add(new JObject { ["id"] = OldArea, ["bbox"] = new JArray(Bbox.Cast<object>().ToArray()), ["created"] = "2026-10-18T10:00:00", ["counts"] = new JObject { ["buildings"] = 5 } });
            Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)Map3DClientTestAccess.Scenes(Server.Scenes));
        }

        /// <summary>새 영역 그리기 끝 → 만들기 판 [만들기].</summary>
        public void BakeNewArea()
        {
            Scenario.Call(Vm, "TakeMap3DArea", new Map3DArea(Bbox, null));
            Assert.True(Vm.IsMap3DBakeOptionsOpen);
            Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
        }

        /// <summary>영역 배지(또는 목록) 「다시 만들기」 → 만들기 판 [만들기].</summary>
        public void Rebake(string id)
        {
            Area(id).Actions.Single(a => a.Header == "다시 만들기").Command.Execute(null);
            Assert.True(Vm.IsMap3DBakeOptionsOpen);
            Vm.ConfirmMap3DBakeOptionsCommand.Execute(null);
        }

        public Map3DAreaItem Area(string id) => Vm.Map3DAreas.Single(a => a.Id == id);
        public bool Baking => Scenario.GetField<bool>(Vm, "_isMap3DBaking");
        public string Status => Vm.AimStatusMessage ?? "";

        /// <summary>조건이 될 때까지 디스패처를 돌리며 기다린다(진행 조회는 1 초마다).</summary>
        public static async Task<bool> Until(Func<bool> condition, double seconds = 6)
        {
            var end = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                await Task.Delay(20);
            }
            return condition();
        }

        public void Done()
        {
            Assert.Equal("none", Vm.Map3DHost.HostMode);
            Assert.Empty(W.Errors);
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }
    }

    private static void RunAsync(Func<Rig, Task> body) => Scenario.StaAsync(async () => { var rig = new Rig(); await body(rig); rig.Done(); });

    // ─────────────── 서버가 취소로 끝냈다고 알릴 때 ───────────────

    /// <summary>
    /// ★ 수정 전 실패 증명: 옛 GIS 는 작업 상태 "cancelled" 를 몰라(done · error 만 끝으로 봄) 진행 조회를 끝없이 되풀이했다 —
    /// 「만드는 중」에서 빠져나오지 못해 다시 만들 수도 없었다.
    /// </summary>
    [Fact]
    public void should_end_bake_with_cancelled_notice_when_server_reports_cancelled()
        => RunAsync(async rig =>
        {
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Server.Count(HttpMethod.Get, "/" + NewJob) >= 1), "진행 조회를 시작하지 않음");
            rig.Server.Status = "cancelled";   // 다른 곳(map-3d 웹 화면 등)에서 취소됨
            Assert.True(await Rig.Until(() => !rig.Baking, 4), "취소된 작업을 끝으로 보지 않고 계속 조회함");
            Assert.Contains("취소", rig.Status);
            Assert.False(rig.Vm.IsMap3DBusy);
        });

    // ─────────────── 단추 보임 ───────────────

    [Fact]
    public void should_put_cancel_first_instead_of_rebake_when_area_item_baking()
    {
        var scene = new Map3DSceneInfo("a", "영역", new[] { 127.0, 37.0, 127.01, 37.01 }, null, null, null);
        int cancelled = 0;
        var baking = new Map3DAreaItem(scene, 17, _ => { }, _ => { }, null, _ => { }, _ => { }, _ => cancelled++);
        var idle = new Map3DAreaItem(scene, 17, _ => { }, _ => { }, null, _ => { }, _ => { }, null);

        Assert.True(baking.IsBaking);
        Assert.Equal(Map3DAreaItem.CancelBakeHeader, baking.Actions[0].Header);
        Assert.Equal("GMaps.Map3D.CancelBake", baking.Actions[0].AutomationId);
        Assert.DoesNotContain(baking.Actions, a => a.Header == "다시 만들기");
        Assert.EndsWith("다시 만드는 중", baking.MenuHeader);
        baking.Actions[0].Command.Execute(null);
        Assert.Equal(1, cancelled);

        Assert.False(idle.IsBaking);
        Assert.Equal("이 영역 3D 로 보기", idle.Actions[0].Header);   // 예전 순서 그대로
        Assert.Contains(idle.Actions, a => a.Header == "다시 만들기");
        Assert.All(idle.Actions, a => Assert.Null(a.AutomationId));
        Assert.DoesNotContain("만드는 중", idle.MenuHeader);
    }

    [Fact]
    public void should_show_cancel_button_only_while_bake_progress_shown_when_baking()
        => RunAsync(async rig =>
        {
            Assert.False(rig.Vm.IsMap3DCancelBakeVisible);                 // 만들기 전
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible), "굽는 중 진행 판에 「만들기 취소」가 없음");
            Assert.True(rig.Vm.IsMap3DBusy);
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
            Assert.False(rig.Vm.IsMap3DCancelBakeVisible);                 // 다 만든 뒤
            Assert.Equal(0, rig.Server.Count(HttpMethod.Post, "/cancel"));
        });

    [Fact]
    public void should_put_cancel_on_rebaking_area_only_when_rebake_runs()
        => RunAsync(async rig =>
        {
            rig.OldAreaExists();
            Assert.DoesNotContain(rig.Area(OldArea).Actions, a => a.Header == Map3DAreaItem.CancelBakeHeader);
            rig.Rebake(OldArea);
            Assert.True(await Rig.Until(() => rig.Area(OldArea).IsBaking), "다시 만드는 영역 항목에 「만들기 취소」가 없음");
            var first = rig.Area(OldArea).Actions[0];
            Assert.Equal(Map3DAreaItem.CancelBakeHeader, first.Header);
            Assert.Equal("GMaps.Map3D.CancelBake", first.AutomationId);
            Assert.DoesNotContain(rig.Area(OldArea).Actions, a => a.Header == "다시 만들기");   // 만드는 중엔 다시 만들기 대신
            Assert.Contains("다시 만드는 중", rig.Area(OldArea).MenuHeader);
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
            Assert.True(await Rig.Until(() => !rig.Area(OldArea).IsBaking, 2));
            Assert.Contains(rig.Area(OldArea).Actions, a => a.Header == "다시 만들기");
        });

    [Fact]
    public void should_not_put_cancel_on_other_areas_when_new_area_baking()
        => RunAsync(async rig =>
        {
            rig.OldAreaExists();
            rig.BakeNewArea();                                              // 새 영역 — 옛 영역 항목과 무관
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            Assert.False(rig.Area(OldArea).IsBaking);
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
        });

    // ─────────────── 취소 요청 · 상태 전환 ───────────────

    [Fact]
    public void should_send_cancel_and_return_to_before_bake_state_when_cancel_pressed()
        => RunAsync(async rig =>
        {
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.True(await Rig.Until(() => !rig.Baking, 2), "취소 요청이 받아졌는데 아직 만드는 중");
            Assert.Equal(1, rig.Server.Count(HttpMethod.Post, $"/api/jobs/{NewJob}/cancel"));
            Assert.Equal(Map3DGuide.CancelledBakeText, rig.Status);
            Assert.False(rig.Vm.IsMap3DBusy);                               // 진행 판 닫힘
            Assert.False(rig.Vm.IsMap3DCancelBakeVisible);
            Assert.True(rig.Vm.DrawMap3DAreaCommand.CanExecute(null));      // 다시 그리기 · 만들기 가능
            int polls = rig.Server.Count(HttpMethod.Get, "/" + NewJob);
            await Task.Delay(1500);
            Assert.Equal(polls, rig.Server.Count(HttpMethod.Get, "/" + NewJob));   // 진행 조회도 멈춤
            Assert.Empty(rig.Vm.Map3DAreas);                                // 처음 만들던 영역 — 영역 없음
        });

    [Fact]
    public void should_keep_previous_scene_when_rebake_cancelled()
        => RunAsync(async rig =>
        {
            rig.OldAreaExists();
            rig.Rebake(OldArea);
            Assert.True(await Rig.Until(() => rig.Area(OldArea).IsBaking));
            rig.Area(OldArea).Actions[0].Command.Execute(null);             // 배지 · 목록의 「만들기 취소」
            Assert.True(await Rig.Until(() => !rig.Baking, 2));
            Assert.Equal(1, rig.Server.Count(HttpMethod.Post, $"/api/jobs/{NewJob}/cancel"));
            Assert.Equal(new[] { OldArea }, rig.Vm.Map3DAreas.Select(a => a.Id));   // 이전 장면 그대로
            Assert.False(rig.Area(OldArea).IsBaking);
            Assert.Contains(rig.Area(OldArea).Actions, a => a.Header == "다시 만들기");
            Assert.Contains(Scenario.GetField<List<Map3DScene>>(rig.Vm, "_map3DScenes"), s => s.Id == OldArea);
            Assert.DoesNotContain(Scenario.GetField<List<Map3DScene>>(rig.Vm, "_map3DScenes"), s => s.Id == NewJob);
            Assert.Equal(Map3DGuide.CancelledBakeText, rig.Status);
        });

    [Fact]
    public void should_allow_new_bake_right_after_cancel_when_server_still_stopping()
        => RunAsync(async rig =>
        {
            rig.Server.StatusAfterCancel = null;                            // 서버는 아직 멈추는 중(running)
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.True(await Rig.Until(() => !rig.Baking, 2));
            rig.Server.Status = "running";
            rig.BakeNewArea();                                              // 곧바로 다시 만들기
            Assert.True(await Rig.Until(() => rig.Server.Count(HttpMethod.Post, "/api/jobs") == 2));
            Assert.True(rig.Baking);
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            Assert.Equal(NewJob + "2", Scenario.GetField<string?>(rig.Vm, "_map3DBakeJob"));
            await Task.Delay(1300);                                         // 앞 굽기의 끊긴 조회가 새 굽기 상태를 건드리지 않는다
            Assert.True(rig.Baking);
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
            Assert.Contains("3D 준비됨", rig.Status);
        });

    [Fact]
    public void should_send_one_cancel_when_pressed_repeatedly()
        => RunAsync(async rig =>
        {
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            rig.Server.CancelGate = new TaskCompletionSource();             // 응답을 붙잡아 둔 채 연타
            for (int i = 0; i < 5; i++) rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.False(rig.Vm.IsMap3DCancelBakeVisible);                 // 보내는 중엔 단추 숨김
            await Task.Delay(100);
            Assert.Equal(1, rig.Server.Count(HttpMethod.Post, "/cancel"));
            rig.Server.CancelGate.SetResult();
            Assert.True(await Rig.Until(() => !rig.Baking, 2));
            rig.Vm.CancelMap3DBakeCommand.Execute(null);                    // 끝난 뒤 눌러도 아무것도 안 보낸다
            await Task.Delay(100);
            Assert.Equal(1, rig.Server.Count(HttpMethod.Post, "/cancel"));
        });

    // ─────────────── 옛 서버 · 실패 ───────────────

    [Fact]
    public void should_tell_unsupported_and_hide_button_when_old_server()
        => RunAsync(async rig =>
        {
            rig.Server.CancelResult = null;                                 // 옛 map-3d — 취소 API 없음(404)
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.True(await Rig.Until(() => rig.Status == Map3DGuide.CancelBakeUnsupportedText, 2));
            Assert.True(rig.Baking);                                        // 계속 만든다
            await Task.Delay(1200);
            Assert.False(rig.Vm.IsMap3DCancelBakeVisible);                 // 이번 굽기 동안 단추 숨김
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
            Assert.Contains("3D 준비됨", rig.Status);
        });

    [Fact]
    public void should_keep_baking_when_cancel_too_late()
        => RunAsync(async rig =>
        {
            rig.Server.CancelResult = Map3DCancelResult.TooLate;
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.True(await Rig.Until(() => rig.Status == Map3DGuide.CancelBakeTooLateText, 2));
            Assert.True(rig.Baking);
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
            Assert.Contains("3D 준비됨", rig.Status);
        });

    [Fact]
    public void should_keep_baking_and_tell_when_cancel_cannot_reach_server()
        => RunAsync(async rig =>
        {
            rig.Server.CancelUnreachable = true;
            rig.BakeNewArea();
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible));
            rig.Vm.CancelMap3DBakeCommand.Execute(null);
            Assert.True(await Rig.Until(() => rig.Status.Contains("꺼져"), 2), "서버에 닿지 않음을 알리지 않음");
            Assert.True(await Rig.Until(() => rig.Vm.IsMap3DCancelBakeVisible, 2));   // 다시 누를 수 있다
            Assert.True(rig.Baking);
            rig.Server.Status = "done";
            Assert.True(await Rig.Until(() => !rig.Baking, 4));
        });
}

/// <summary>서버 목록 JSON → 장면 항목(Map3DClient 의 해석 그대로).</summary>
internal static class Map3DClientTestAccess
{
    public static IReadOnlyList<Map3DSceneInfo> Scenes(IEnumerable<JObject> scenes)
        => (IReadOnlyList<Map3DSceneInfo>)typeof(Map3DClient).GetMethod("ParseScenes", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { new JObject { ["scenes"] = new JArray(scenes.ToArray()) }.ToString() })!;
}
