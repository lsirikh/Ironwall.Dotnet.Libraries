using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 「건물 높이」 판 경계 · 실패 · 경쟁 시나리오(영역 C 전수 시나리오, 2026-10-19) — <see cref="BuildingHeightScenarioTests"/> 와 같은 장치
/// (창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d HttpMessageHandler)로 그 시험이 덮지 않은 길을 본다:
/// 낮은 건물 −1층(C-59) · 저장 중 3D 나가기(C-81) · 네이티브 창 3D 건물 클릭(C-101) · 잘못된 입력이 남은 [적용](C-105) ·
/// 권한 없음 강제 호출(C-70/71) · 되돌리기 실패(C-78/79) · 저장 중 다른 건물(C-82/83) · 장면 사라짐(C-90) · 「모두 자동으로」 실패(C-93) ·
/// 판 컨트롤 Enter/Esc(C-103/104).
/// </summary>
public class BuildingHeightEdgeScenarioTests
{
    private const string Job = "j-area";

    // ─────────────── 장치(BuildingHeightScenarioTests 와 같은 모양) ───────────────

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, JObject? Body)> Requests { get; } = new();
        public double AutoHeight { get; set; } = 12.0;
        public HttpStatusCode? FailWith { get; set; }
        public bool Unreachable { get; set; }
        public TaskCompletionSource? Gate { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? text = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get) return Json(HttpStatusCode.ServiceUnavailable, "{\"detail\":\"목록은 이 시험 밖\"}");
            Requests.Add((request.Method, path, text == null ? null : JObject.Parse(text)));
            if (Gate != null) await Gate.Task;
            if (Unreachable) throw new HttpRequestException("연결 거부");
            if (FailWith is { } code) return Json(code, "{\"detail\":\"만드는 중인 작업은 바꿀 수 없습니다\"}");
            if (path.EndsWith("/height-overrides"))
                return Json(HttpStatusCode.OK, "{\"reset\":2,\"buildings\":[" + Row(7, AutoHeight, null, null) + "," + Row(8, 6.0, null, null) + "]}");
            int id = int.Parse(path.Split('/')[^2]);
            var body = request.Method == HttpMethod.Put
                ? Row(id, JObject.Parse(text!).Value<double>("height"), JObject.Parse(text!)["floors"]?.Type == JTokenType.Integer ? JObject.Parse(text!).Value<int>("floors") : null, "u1")
                : Row(id, AutoHeight, null, null);
            return Json(HttpStatusCode.OK, "{\"building\":" + body + "}");
        }

        private static string Row(int id, double height, int? floors, string? uid)
            => uid != null
                ? new JObject { ["id"] = id, ["height"] = height, ["height_source"] = "manual", ["height_from"] = "manual", ["levels"] = floors, ["origin"] = "map",
                                ["center"] = new JArray(127.0, 37.5), ["manual"] = new JObject { ["uid"] = uid } }.ToString()
                : new JObject { ["id"] = id, ["height"] = height, ["height_source"] = "levels", ["height_from"] = "levels", ["origin"] = "national",
                                ["center"] = new JArray(127.0, 37.5) }.ToString();

        private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; }
        public MapViewModel Vm => W.Vm;
        public FakeServer Server { get; } = new();
        public List<JObject> Posts { get; } = new();
        public List<string> Infos { get; } = new();
        public PointLatLng Center { get; }
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        public Rig(bool canEdit = true)
        {
            W = WiredMapRig.Create(canEdit);
            W.Log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
             .Callback<string, string, string, int>((m, _, _, _) => Infos.Add(m));
            // 2026-10-21 사용자 결정 — 건물 고르기 · 그리기 · 외관 · 꾸미기는 지도 편집 모드에서만. 고치기 시험이라 편집 모드로 시작한다
            // (권한 없는 계정 시험도 켜 둔다 — 편집 모드 중 권한을 잃은 경우의 CanEditMap 막힘을 그대로 본다). 보기 전용 시험은 끈다.
            Scenario.SetField(Vm, "_isEditModeEnabled", true);
            W.Map.SetEditMode(true);
            W.Map.SetEffectiveZoom(17d);
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
            Scenario.SetField(Vm, "_bhFloorHeightText", "3.0");
            Scenario.SetField(Vm, "_bhFloorsText", "");
            Scenario.SetField(Vm, "_bhHeightText", "");
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(json => Posts.Add(JObject.Parse(json))));
        }

        public void Enter3D(int manualHeights = 0)
        {
            Areas(new Map3DSceneInfo(Job, "시험 영역", new[] { Center.Lng - 0.002, Center.Lat - 0.002, Center.Lng + 0.002, Center.Lat + 0.002 }, null, 2, null)
                  { ManualHeights = manualHeights });
            var host = Vm.Map3DHost;
            SetHost("_job", Job);
            SetHost("_ready", true);
            SetHost("_rendered", true);
            SetHost("_failed", false);
            SetHost("<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow.AddSeconds(-10));
            Scenario.Call(Vm, "EvaluateMap3DGate");
            Scenario.Call(Vm, "UpdateMap3DAreaDisplay");
            Vm.EnterMap3DCommand.Execute(null);
            Scenario.Call(Vm, "OnMap3DGuideTick");
            Assert.Equal("3D", Vm.Map3DStatusText);
        }

        public void Areas(params Map3DSceneInfo[] scenes) => Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)scenes);

        public void SetHost(string name, object? value)
            => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Vm.Map3DHost, value);

        public bool From3D(string json)
        {
            var msg = Map3DMath.ParseMessage(json);
            if (msg == null) return false;
            Scenario.Call(Vm, "OnMap3DMessage", msg);
            return true;
        }

        public void PickBuilding(int id, double height, bool manual = false, string from = "shadow", string origin = "photo")
            => Assert.True(From3D(new JObject { ["type"] = "building", ["id"] = id, ["origin"] = origin, ["height"] = height, ["height_source"] = manual ? "manual" : "overlay",
                                                ["height_from"] = manual ? "manual" : from, ["levels"] = null, ["area_m2"] = 320.4, ["lon"] = 127.0, ["lat"] = 37.5,
                                                ["manual"] = manual }.ToString()));

        public IBuildingHeightPanelHost Host => Vm;
        public Task Apply() => (Task)Scenario.Call(Vm, "ApplyBuildingHeightAsync")!;
        public Task Reset() => (Task)Scenario.Call(Vm, "ResetBuildingHeightAsync")!;
        public Task Undo() => (Task)Scenario.Call(Vm, "UndoBuildingHeightAsync")!;

        public bool PressEsc()
        {
            var source = new HeadlessSource(W.Deco);
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            return (bool)Scenario.Call(Vm, "TryHandleMap3DKey", e)!;
        }

        public List<JObject> Sent(string type) => Posts.Where(p => p.Value<string>("type") == type).ToList();

        public void Done()
        {
            Assert.Equal("none", Vm.Map3DHost.HostMode);
            Assert.Empty(W.Errors);
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }
    }

    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public HeadlessSource(Visual root) { RootVisual = root; }
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static void Run(Action<Rig> body, bool canEdit = true) => Scenario.Sta(() => { var rig = new Rig(canEdit); body(rig); rig.Done(); });

    private static void RunAsync(Func<Rig, Task> body, bool canEdit = true)
        => Scenario.StaAsync(async () => { var rig = new Rig(canEdit); await body(rig); rig.Done(); });

    // ─────────────── C-59(D1) 1층보다 낮은 건물 −1층 · ↓ ───────────────

    [Theory]
    [InlineData(2.0)]
    [InlineData(1.2)]
    public void should_keep_low_building_height_when_minus_floor_button_or_down_key_pressed(double height) => Run(rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, height);
        var vm = rig.Vm;

        vm.StepBuildingFloorCommand.Execute("-1");
        Assert.Equal(height, vm.BuildingHeightPreview);
        Assert.True(rig.Host.StepHeightByKey(up: false, shift: false));
        Assert.Equal(height, vm.BuildingHeightPreview);                       // ↓ 도 제자리
        Assert.Empty(rig.Sent("building.preview"));                        // 3D 에 높인 미리보기를 보내지 않는다
        Assert.False(vm.IsBuildingHeightChanged);
        Assert.False(vm.ApplyBuildingHeightCommand.CanExecute(null));

        Assert.Equal("1", vm.BuildingHeightFloorsText);                     // 판에는 1층으로 보인다
        vm.StepBuildingFloorCommand.Execute("1");                           // 올리기는 보이는 층수 + 1 = 2층 × 3.0
        Assert.Equal(6.0, vm.BuildingHeightPreview);
    });

    // ─────────────── C-81(D2) 저장 중 3D 나가기 → 늦은 응답 ───────────────

    [Fact]
    public void should_not_refill_undo_nor_offer_previous_session_undo_when_save_response_arrives_after_leaving_3d() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        rig.Server.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = rig.Apply();
        await Task.Delay(20);

        vm.ExitMap3DCommand.Execute(null);                                   // 저장 응답 전에 3D 나가기
        Assert.Equal("2D", vm.Map3DStatusText);
        Assert.Equal(0, vm.BuildingHeightUndoCount);

        rig.Server.Gate.SetResult();
        await save;
        await Task.Delay(20);
        Assert.Single(rig.Server.Requests);                                   // 서버 저장은 그대로
        Assert.Single(rig.Sent("building.commit"));                           // 3D(접힌 페이지)에도 저장된 줄
        Assert.Equal(0, vm.BuildingHeightUndoCount);                          // ★ 나간 뒤 온 응답은 되돌리기 칸에 쌓지 않는다

        // 다시 3D — 앞 3D 의 저장을 판 [되돌리기]로 되돌릴 수 없다(머리말 "3D 를 나가면 비움")
        rig.Enter3D();
        rig.PickBuilding(7, 30.0, manual: true);
        Assert.False(vm.UndoBuildingHeightCommand.CanExecute(null), "앞 3D 세션의 저장이 되돌리기 칸에 남았다");
        Assert.False(rig.Host.UndoHeight());
        Assert.Single(rig.Server.Requests);
    });

    [Fact]
    public void should_not_put_back_undo_step_when_undo_fails_after_leaving_3d() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        await rig.Apply();
        Assert.Equal(1, vm.BuildingHeightUndoCount);

        rig.Server.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Server.FailWith = HttpStatusCode.Conflict;
        var undo = rig.Undo();
        await Task.Delay(20);
        vm.ExitMap3DCommand.Execute(null);
        rig.Server.Gate.SetResult();
        await undo;
        Assert.Equal(0, vm.BuildingHeightUndoCount);                          // 실패해도 나간 뒤에는 칸을 되살리지 않는다
    });

    // ─────────────── C-101(D3) 네이티브 창(hwnd) 3D 에서 건물 클릭 ───────────────

    [Fact]
    public void should_not_open_panel_and_tell_reason_and_let_esc_exit_when_building_clicked_in_native_window_3d() => Run(rig =>
    {
        rig.Enter3D();
        var vm = rig.Vm;
        rig.SetHost("_el", new Border());                                    // 네이티브 창 3D 로 바뀐 호스트
        try
        {
            Assert.Equal("hwnd", vm.Map3DHost.HostMode);
            rig.PickBuilding(7, 14.5);

            Assert.False(vm.IsBuildingHeightPanelOpen, "WPF 판은 네이티브 창 3D 에 가려 보이지 않는다 — 열면 안 된다");
            Assert.Contains("네이티브 창 3D", vm.AimStatusMessage);
            Assert.Contains("건물 높이", vm.AimStatusMessage);
            var select = Assert.Single(rig.Sent("building.select"));          // 3D 가 스스로 그린 윤곽을 지운다
            Assert.Equal(JTokenType.Null, select["id"]!.Type);
            Assert.Contains(rig.Infos, l => l.StartsWith("[Map3D] 건물 고름 — 네이티브 창 3D", StringComparison.Ordinal) && l.Contains("id=7"));
            Assert.DoesNotContain(rig.Infos, l => l.StartsWith("[Map3D] 건물 고름 id=", StringComparison.Ordinal));

            Assert.True(rig.PressEsc(), "첫 Esc 가 3D 를 나가야 한다(네이티브 창 3D 는 「나가기」가 가려진다)");
            Assert.Equal("2D", vm.Map3DStatusText);
        }
        finally { rig.SetHost("_el", null); }
    });

    // ─────────────── C-105(D7) 잘못된 입력이 남은 채 [적용] ───────────────

    [Theory]
    [InlineData("height", "abc")]
    [InlineData("height", "600.1")]
    [InlineData("floors", "0")]
    [InlineData("floors", "3.5")]
    [InlineData("fh", "11")]
    public void should_refuse_apply_and_keep_error_when_invalid_input_remains(string field, string text) => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";                                  // 앞 미리보기 30 m
        Assert.True(vm.ApplyBuildingHeightCommand.CanExecute(null));

        if (field == "height") vm.BuildingHeightText = text;
        else if (field == "floors") vm.BuildingHeightFloorsText = text;
        else vm.BuildingHeightFloorHeightText = text;
        Assert.True(vm.HasBuildingHeightError);
        Assert.False(vm.ApplyBuildingHeightCommand.CanExecute(null), $"잘못된 입력 '{text}' 이 남았는데 [적용] 이 켜져 있다");

        await rig.Apply();                                                    // 억지로 불러도 앞 미리보기(30 m)를 저장하지 않는다
        Assert.Empty(rig.Server.Requests);
        Assert.True(vm.HasBuildingHeightError);

        vm.BuildingHeightText = "33";                                        // 바른 값 → 다시 켜지고 그 값으로 저장
        Assert.False(vm.HasBuildingHeightError);
        Assert.True(vm.ApplyBuildingHeightCommand.CanExecute(null));
        await rig.Apply();
        Assert.Equal(33.0, Assert.Single(rig.Server.Requests).Body!.Value<double>("height"));
    });

    [Fact]
    public void should_still_allow_retry_when_error_line_is_from_server_failure_not_input() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        rig.Server.FailWith = HttpStatusCode.Conflict;
        await rig.Apply();
        Assert.True(vm.HasBuildingHeightError);
        Assert.True(vm.ApplyBuildingHeightCommand.CanExecute(null));         // 서버 실패 이유 줄은 다시 [적용] 을 막지 않는다
        rig.Server.FailWith = null;
        await rig.Apply();
        Assert.Equal(2, rig.Server.Requests.Count);
        Assert.False(vm.HasBuildingHeightError);
    });

    // ─────────────── C-70/71 권한 없음 + 강제 호출 ───────────────

    [Fact]
    public void should_send_nothing_and_show_permission_info_when_reset_undo_or_reset_all_forced_without_permission() => RunAsync(async rig =>
    {
        rig.Enter3D(manualHeights: 2);
        rig.PickBuilding(7, 30.0, manual: true);
        var vm = rig.Vm;
        Assert.True(vm.IsBuildingHeightReadOnly);
        Assert.False(vm.ResetBuildingHeightCommand.CanExecute(null));
        Assert.False(vm.UndoBuildingHeightCommand.CanExecute(null));

        await rig.Reset();
        await rig.Undo();
        var item = Assert.Single(vm.Map3DAreas);
        var resetAll = item.Actions.SingleOrDefault(a => a.Header.Contains("모두 자동으로"));
        resetAll?.Command.Execute(null);
        await Task.Delay(20);
        await vm.HandleAsync(new CallResetMap3DHeightsProcessMessageModel(), CancellationToken.None);   // 확인 없이 온 콜백 — 대상 없음

        Assert.Empty(rig.Server.Requests);
        Assert.Empty(rig.Sent("building.commit"));
        Assert.DoesNotContain(rig.W.Published, m => m is OpenConfirmPopupMessageModel);
        var infos = rig.W.Published.OfType<OpenInfoPopupMessageModel>().ToList();
        Assert.Equal(resetAll == null ? 2 : 3, infos.Count);
        Assert.All(infos, i => Assert.Equal("권한 없음", i.Title));
        Assert.Equal(30.0, vm.BuildingHeightBuilding!.Height);
        Assert.True(vm.BuildingHeightBuilding.Manual);
    }, canEdit: false);

    // ─────────────── C-78/79 되돌리기 · 자동으로 되돌리기 실패 ───────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_keep_undo_step_reason_and_manual_when_undo_or_reset_fails(bool unreachable) => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        await rig.Apply();
        Assert.Equal(1, vm.BuildingHeightUndoCount);
        int commits = rig.Sent("building.commit").Count;

        if (unreachable) rig.Server.Unreachable = true; else rig.Server.FailWith = HttpStatusCode.Conflict;
        string reason = unreachable ? "3D 서버(map-3d)가 꺼져 있습니다" : "저장하지 못했습니다: 만드는 중인 작업은 바꿀 수 없습니다";

        await rig.Undo();                                                     // C-78 되돌리기 실패
        Assert.Equal(1, vm.BuildingHeightUndoCount);                          // 칸이 다시 남는다(다시 할 수 있게)
        Assert.True(vm.UndoBuildingHeightCommand.CanExecute(null));
        Assert.Equal(reason, vm.BuildingHeightError);
        Assert.Equal(30.0, vm.BuildingHeightBuilding!.Height);
        Assert.True(vm.BuildingHeightBuilding.Manual);

        await rig.Reset();                                                    // C-79 자동으로 되돌리기 실패
        Assert.Equal(reason, vm.BuildingHeightError);
        Assert.True(vm.BuildingHeightBuilding!.Manual);                       // 직접 입력 그대로
        Assert.Equal(1, vm.BuildingHeightUndoCount);                          // 실패한 자동으로는 칸을 쌓지 않는다
        Assert.Equal(commits, rig.Sent("building.commit").Count);
        Assert.True(vm.IsBuildingHeightEditable);

        rig.Server.Unreachable = false; rig.Server.FailWith = null;           // 서버가 돌아오면 그 칸으로 되돌린다
        await rig.Undo();
        Assert.Equal(HttpMethod.Delete, rig.Server.Requests.Last().Method);
        Assert.False(vm.BuildingHeightBuilding!.Manual);
        Assert.Equal(0, vm.BuildingHeightUndoCount);
    });

    // ─────────────── C-82/83 저장 중 다른 건물 · 다른 건물 판에서 되돌리기 ───────────────

    [Fact]
    public void should_keep_new_panel_commit_previous_and_undo_only_previous_when_other_building_picked_during_save() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        rig.Server.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = rig.Apply();
        await Task.Delay(20);

        rig.PickBuilding(8, 6.0, from: "default");                            // C-82 저장 중 다른 건물
        Assert.Equal(8, vm.BuildingHeightBuilding!.Id);
        rig.Server.Gate.SetResult();
        await save;
        await Task.Delay(20);

        Assert.Equal(8, vm.BuildingHeightBuilding!.Id);                      // 늦은 응답이 새 판을 덮지 않는다
        Assert.Equal(6.0, vm.BuildingHeightPreview);
        Assert.Equal("지금 6.0 m · 2층(층고 3.0 m)", vm.BuildingHeightCurrentText);
        var commit = Assert.Single(rig.Sent("building.commit"));              // 앞 건물은 3D 에 저장된 줄
        Assert.Equal(7, commit.Value<int>("id"));
        Assert.Equal(1, vm.BuildingHeightUndoCount);
        Assert.True(vm.IsBuildingHeightEditable);

        Assert.True(vm.UndoBuildingHeightCommand.CanExecute(null));          // C-83 다른 건물 판에서 되돌리기 = 앞 건물(7)만
        await rig.Undo();
        var (method, path, _) = rig.Server.Requests.Last();
        Assert.Equal((HttpMethod.Delete, $"/api/jobs/{Job}/buildings/7/height"), (method, path));
        Assert.Equal(8, vm.BuildingHeightBuilding!.Id);
        Assert.Equal(6.0, vm.BuildingHeightPreview);
        Assert.Equal(7, rig.Sent("building.commit").Last().Value<int>("id"));
        Assert.Equal(0, vm.BuildingHeightUndoCount);
    });

    // ─────────────── C-90 장면 사라짐(scene-missing) ───────────────

    [Fact]
    public void should_close_panel_clear_undo_and_job_when_scene_goes_missing() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        await rig.Apply();
        Assert.Equal(1, vm.BuildingHeightUndoCount);

        Assert.True(rig.From3D(new JObject { ["type"] = "error", ["job"] = Job, ["reason"] = Map3DGuide.SceneMissingReason }.ToString()));
        Assert.Equal("2D", vm.Map3DStatusText);
        Assert.False(vm.IsBuildingHeightPanelOpen);
        Assert.Equal(0, vm.BuildingHeightUndoCount);
        Assert.Null(Scenario.GetField<string?>(vm, "_bhJob"));
        Assert.False(vm.UndoBuildingHeightCommand.CanExecute(null));
        Assert.Equal(Map3DGuide.SceneMissingText, vm.AimStatusMessage);
    });

    // ─────────────── C-93 「모두 자동으로」 실패 ───────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_tell_reason_close_popup_and_commit_nothing_when_reset_all_fails(bool unreachable) => RunAsync(async rig =>
    {
        rig.Enter3D(manualHeights: 3);
        var vm = rig.Vm;
        rig.PickBuilding(7, 30.0, manual: true);
        Assert.Single(Assert.Single(vm.Map3DAreas).Actions, a => a.Header.Contains("모두 자동으로")).Command.Execute(null);
        await Task.Delay(20);
        Assert.IsType<OpenConfirmPopupMessageModel>(rig.W.Published.Last());

        if (unreachable) rig.Server.Unreachable = true; else rig.Server.FailWith = HttpStatusCode.Conflict;
        await vm.HandleAsync(new CallResetMap3DHeightsProcessMessageModel(), CancellationToken.None);

        Assert.Single(rig.Server.Requests);
        Assert.Equal(unreachable ? "3D 서버(map-3d)가 꺼져 있습니다" : "건물 높이를 되돌릴 수 없습니다: 만드는 중인 작업은 바꿀 수 없습니다", vm.AimStatusMessage);
        Assert.IsType<ClosePopupMessageModel>(rig.W.Published.Last());       // 확인 팝업은 닫는다
        Assert.Empty(rig.Sent("building.commit"));
        Assert.False(vm.IsBuildingHeightPanelOpen);                           // 요청 전에 그 장면 판을 닫았다(실패해도 닫힌 채 — 기록)
    });

    // ─────────────── C-94 「모두 자동으로」 성공 → 그 장면 되돌리기 칸 비움 ───────────────

    [Fact]
    public void should_drop_that_area_undo_steps_when_reset_all_succeeds() => RunAsync(async rig =>
    {
        rig.Enter3D(manualHeights: 1);
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        await rig.Apply();
        Assert.Equal(1, vm.BuildingHeightUndoCount);

        Assert.Single(Assert.Single(vm.Map3DAreas).Actions, a => a.Header.Contains("모두 자동으로")).Command.Execute(null);
        await Task.Delay(20);
        await vm.HandleAsync(new CallResetMap3DHeightsProcessMessageModel(), CancellationToken.None);

        Assert.Equal(0, vm.BuildingHeightUndoCount);                          // 지운 직접 입력으로 되돌리지 않게
        Assert.Equal(new[] { 7, 7, 8 }, rig.Sent("building.commit").Select(c => c.Value<int>("id")));
        Assert.False(vm.IsBuildingHeightPanelOpen);
        Assert.IsType<ClosePopupMessageModel>(rig.W.Published.Last());
    });

    // ─────────────── C-103/104 판 컨트롤 입력칸 Enter · 판 Esc ───────────────

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            yield return c;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    private static T ById<T>(DependencyObject root, string id) where T : DependencyObject
        => Descendants(root).OfType<T>().First(d => AutomationProperties.GetAutomationId(d) == id);

    [Fact]
    public void should_apply_typed_value_on_enter_and_cancel_panel_on_escape_inside_panel() => Run(rig =>
    {
        rig.Vm.IsNotifying = true;
        var platform = Caliburn.Micro.PlatformProvider.Current;
        Caliburn.Micro.PlatformProvider.Current = new Caliburn.Micro.XamlPlatformProvider();
        try
        {
            rig.Enter3D();
            rig.PickBuilding(7, 14.5);
            var panel = rig.Vm.BuildingHeightPanel;
            var dict = PropertyStyleLoader.Load("BuildingHeightPanelStyle.xaml");
            panel.Resources.MergedDictionaries.Add(dict);
            panel.Style = (Style)dict[typeof(BuildingHeightPanelControl)];
            Scenario.Layout(panel, 320, 700);
            var source = new HeadlessSource(panel);

            KeyEventArgs KeyArgs(Key key) => new(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };

            // C-103 층수 칸에 글만 바꾸면(포커스 그대로) 아직 반영 전 — Enter 로 바로 넣는다
            var floors = ById<TextBox>(panel, "Map.BuildingHeight.Floors");
            floors.Text = "10";
            Assert.Equal(14.5, rig.Vm.BuildingHeightPreview);
            var enter = KeyArgs(System.Windows.Input.Key.Enter);
            floors.RaiseEvent(enter);
            Assert.True(enter.Handled);
            Assert.Equal(30.0, rig.Vm.BuildingHeightPreview);
            Assert.Equal("30.0", ById<TextBox>(panel, "Map.BuildingHeight.Height").Text);

            var height = ById<TextBox>(panel, "Map.BuildingHeight.Height");
            height.Text = "abc";
            height.RaiseEvent(KeyArgs(System.Windows.Input.Key.Enter));
            Assert.True(rig.Vm.HasBuildingHeightError);
            Assert.Equal(Visibility.Visible, ById<TextBlock>(panel, "Map.BuildingHeight.Error").Visibility);
            height.Text = "21.5";
            height.RaiseEvent(KeyArgs(System.Windows.Input.Key.Enter));
            Assert.Equal(21.5, rig.Vm.BuildingHeightPreview);
            Assert.Equal("7", floors.Text);

            // C-104 판 Esc = 판 취소(원래 높이로 · 강조 지움) — 3D 는 그대로
            var esc = KeyArgs(System.Windows.Input.Key.Escape);
            floors.RaiseEvent(esc);
            Assert.True(esc.Handled);
            Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
            Assert.Equal(JTokenType.Null, rig.Sent("building.preview").Last()["height"]!.Type);
            Assert.Equal(JTokenType.Null, rig.Sent("building.select").Last()["id"]!.Type);
            Assert.Equal("3D", rig.Vm.Map3DStatusText);
            Assert.Empty(rig.Server.Requests);
        }
        finally { Caliburn.Micro.PlatformProvider.Current = platform; }
    });
}
