using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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
/// 「건물 높이」 판(3D 건물 높이 직접 입력, 2026-10-19) — 창 · WebView2 없이 실제 MapViewModel 경로로:
/// 3D 진입(재사용 장면) → 3D 의 building 메시지(OnMap3DMessage) → 판 열림 · 입력 검증 · 층수↔높이 · 층고 · ±층 · 키 · 손잡이 끌기 →
/// 3D 로 보낸 미리보기 메시지(building.preview/select/commit) · map-3d 요청(가짜 HttpMessageHandler) · 적용 · 취소 · 닫기 · Esc · 3D 나가기 ·
/// 다른 건물 고르기 · 권한 없음 · 연타 · 서버 실패 · 자동으로 되돌리기 · 판 안 되돌리기 · 3D 영역 목록 「모두 자동으로」 · 판 템플릿(AutomationId · 토큰).
/// </summary>
public class BuildingHeightScenarioTests
{
    private const string Job = "j-area";

    // ─────────────── 장치 ───────────────

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
                ? Row(id, request.Method == HttpMethod.Put ? JObject.Parse(text!).Value<double>("height") : 0, JObject.Parse(text!)["floors"]?.Type == JTokenType.Integer ? JObject.Parse(text!).Value<int>("floors") : null, "u1")
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
        public PointLatLng Center { get; }
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        public Rig(bool canEdit = true)
        {
            W = WiredMapRig.Create(canEdit);
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
            // 생성자 없이 만든 VM — 「건물 높이」 입력칸 글 초기값을 제품과 같게
            Scenario.SetField(Vm, "_bhFloorHeightText", "3.0");
            Scenario.SetField(Vm, "_bhFloorsText", "");
            Scenario.SetField(Vm, "_bhHeightText", "");
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(json => Posts.Add(JObject.Parse(json))));
        }

        /// <summary>그 장면을 미리 불러 둔 3D 로 「3D 로 보기」 → 펼침(재사용 경로 · WebView2 없음).</summary>
        public void Enter3D(int manualHeights = 0, int unmatched = 0)
        {
            Areas(new Map3DSceneInfo(Job, "시험 영역", new[] { Center.Lng - 0.002, Center.Lat - 0.002, Center.Lng + 0.002, Center.Lat + 0.002 }, null, 2, null)
                  { ManualHeights = manualHeights, ManualUnmatched = unmatched });
            var host = Vm.Map3DHost;
            SetHost(host, "_job", Job);
            SetHost(host, "_ready", true);
            SetHost(host, "_rendered", true);
            SetHost(host, "_failed", false);
            SetHost(host, "<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow.AddSeconds(-10));
            Scenario.Call(Vm, "EvaluateMap3DGate");
            Scenario.Call(Vm, "UpdateMap3DAreaDisplay");
            Vm.EnterMap3DCommand.Execute(null);
            Scenario.Call(Vm, "OnMap3DGuideTick");
            Assert.Equal("3D", Vm.Map3DStatusText);
        }

        public void Areas(params Map3DSceneInfo[] scenes) => Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)scenes);

        private static void SetHost(Map3DViewHost host, string name, object? value)
            => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);

        /// <summary>3D 가 보낸 메시지 그대로(JSON → Map3DMath.ParseMessage → OnMap3DMessage). 해석이 버린 메시지면 false.</summary>
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
            var source = new HeadlessSource { RootVisual = W.Deco };
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            return (bool)Scenario.Call(Vm, "TryHandleMap3DKey", e)!;
        }

        /// <summary>3D 로 보낸 건물 메시지 중 그 종류만.</summary>
        public List<JObject> Sent(string type) => Posts.Where(p => p.Value<string>("type") == type).ToList();

        public void Done()
        {
            Assert.Equal("none", Vm.Map3DHost.HostMode);
            Assert.Empty(W.Errors);
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }

        private sealed class HeadlessSource : PresentationSource
        {
            private Visual? _root;
            public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
            public override bool IsDisposed => false;
            protected override CompositionTarget GetCompositionTargetCore() => null!;
        }
    }

    private static void Run(Action<Rig> body, bool canEdit = true) => Scenario.Sta(() => { var rig = new Rig(canEdit); body(rig); rig.Done(); });

    private static void RunAsync(Func<Rig, Task> body, bool canEdit = true)
        => Scenario.StaAsync(async () => { var rig = new Rig(canEdit); await body(rig); rig.Done(); });

    private static void AssertPreview(JObject msg, int id, double? height)
    {
        Assert.Equal(id, msg.Value<int>("id"));
        if (height is double h) Assert.Equal(h, msg.Value<double>("height")); else Assert.Equal(JTokenType.Null, msg["height"]!.Type);
    }

    // ─────────────── 1. 고르기 · 판 열림 ───────────────

    [Fact]
    public void should_open_panel_with_height_floors_and_plain_source_when_building_is_clicked_in_3d() => Run(rig =>
    {
        // 2D 에서는(3D 가 아니면) 건물 메시지를 받아도 판을 열지 않는다
        rig.PickBuilding(7, 14.5);
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);

        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        Assert.True(vm.IsBuildingHeightPanelOpen);
        Assert.Equal("지금 14.5 m · 5층(층고 3.0 m)", vm.BuildingHeightCurrentText);
        Assert.Equal("그림자로 잼", vm.BuildingHeightSourceText);
        Assert.Equal("5", vm.BuildingHeightFloorsText);
        Assert.Equal("14.5", vm.BuildingHeightText);
        Assert.Equal("3.0", vm.BuildingHeightFloorHeightText);
        Assert.True(vm.IsBuildingHeightEditable);
        Assert.False(vm.IsBuildingHeightReadOnly);
        Assert.False(vm.ApplyBuildingHeightCommand.CanExecute(null));   // 바꾼 것 없음
        Assert.False(vm.ResetBuildingHeightCommand.CanExecute(null));   // 직접 입력이 아님
        Assert.False(vm.UndoBuildingHeightCommand.CanExecute(null));
        Assert.Equal("아직 바꾸지 않았습니다", vm.BuildingHeightSummary);
        Assert.Empty(rig.Posts);                                       // 고르기만으로는 3D 를 바꾸지 않는다

        // 이상한 메시지는 해석이 버린다(판 그대로)
        Assert.False(rig.From3D("{\"type\":\"building\",\"id\":\"8\",\"height\":10}"));
        Assert.False(rig.From3D("{\"type\":\"building\",\"id\":8,\"height\":-1}"));
        Assert.Equal(7, vm.BuildingHeightBuilding!.Id);
    });

    // ─────────────── 2. 입력 → 미리보기 ───────────────

    [Fact]
    public void should_preview_only_that_building_when_floors_height_floor_height_steps_and_keys_change() => Run(rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;

        vm.BuildingHeightFloorsText = "10";
        Assert.Equal(30.0, vm.BuildingHeightPreview);
        Assert.Equal("30.0", vm.BuildingHeightText);
        AssertPreview(rig.Sent("building.preview").Last(), 7, 30.0);
        Assert.True(vm.IsBuildingHeightChanged);
        Assert.Equal("적용하면 14.5 m → 30.0 m(10층)", vm.BuildingHeightSummary);
        Assert.True(vm.ApplyBuildingHeightCommand.CanExecute(null));

        vm.BuildingHeightText = "21.54";
        Assert.Equal(21.5, vm.BuildingHeightPreview);
        Assert.Equal("7", vm.BuildingHeightFloorsText);                  // 21.5 ÷ 3.0 = 7.2 → 7층

        vm.BuildingHeightFloorHeightText = "2.5";                       // 층고만 — 높이는 그대로, 층수가 따라 바뀐다
        Assert.Equal(21.5, vm.BuildingHeightPreview);
        Assert.Equal("9", vm.BuildingHeightFloorsText);
        Assert.Equal("지금 14.5 m · 6층(층고 2.5 m)", vm.BuildingHeightCurrentText);

        vm.StepBuildingFloorCommand.Execute("1");                        // 9층 + 1 = 10층 × 2.5
        Assert.Equal(25.0, vm.BuildingHeightPreview);
        vm.StepBuildingFloorCommand.Execute("-1");
        Assert.Equal(22.5, vm.BuildingHeightPreview);
        Assert.True(rig.Host.StepHeightByKey(up: true, shift: false));   // ↑ = +1층
        Assert.Equal(25.0, vm.BuildingHeightPreview);
        Assert.True(rig.Host.StepHeightByKey(up: false, shift: true));   // Shift+↓ = −0.1 m
        Assert.Equal(24.9, vm.BuildingHeightPreview);

        // 잘못된 입력 — 이유 한 줄, 미리보기 · 3D 메시지는 그대로
        int sent = rig.Posts.Count;
        foreach (var (field, text, reason) in new[] { ("floors", "0", "1 ~ 200"), ("floors", "3.5", "정수"), ("height", "abc", "숫자"),
                                                      ("height", "600.1", "1 ~ 600"), ("height", "", "넣어"), ("fh", "11", "층고") })
        {
            if (field == "floors") vm.BuildingHeightFloorsText = text;
            else if (field == "height") vm.BuildingHeightText = text;
            else vm.BuildingHeightFloorHeightText = text;
            Assert.True(vm.HasBuildingHeightError, text);
            Assert.Contains(reason, vm.BuildingHeightError);
            Assert.Equal(24.9, vm.BuildingHeightPreview);
        }
        Assert.Equal(sent, rig.Posts.Count);
        vm.BuildingHeightText = "200";                                   // 바른 값이 오면 오류 지움
        Assert.False(vm.HasBuildingHeightError);

        // 원래 높이로 돌아오면 3D 에는 "원래 모양"(null)
        vm.BuildingHeightText = "14.5";
        AssertPreview(rig.Sent("building.preview").Last(), 7, null);
        Assert.False(vm.IsBuildingHeightChanged);
        Assert.All(rig.Sent("building.preview"), p => Assert.Equal(7, p.Value<int>("id")));   // 그 건물만
    });

    [Fact]
    public void should_follow_handle_drag_and_go_back_when_drag_is_cancelled_or_escape_is_pressed() => Run(rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 12.0, from: "levels", origin: "map");
        var vm = rig.Vm;
        Assert.Equal("지도 자료", vm.BuildingHeightSourceText);

        rig.Host.BeginHeightDrag();
        rig.Host.DragHeight(-3);                                          // 데드존 안 — 그대로
        Assert.Equal(12.0, vm.BuildingHeightPreview);
        Assert.Empty(rig.Sent("building.preview"));
        rig.Host.DragHeight(-100);                                        // 위로 100 px = +10 m
        Assert.Equal(22.0, vm.BuildingHeightPreview);
        rig.Host.DragHeight(-150);
        Assert.Equal(27.0, vm.BuildingHeightPreview);
        rig.Host.EndHeightDrag(canceled: false);                          // 놓음 = 그 높이 유지
        Assert.Equal(27.0, vm.BuildingHeightPreview);

        rig.Host.BeginHeightDrag();
        rig.Host.DragHeight(80);
        Assert.Equal(19.0, vm.BuildingHeightPreview);
        rig.Host.EndHeightDrag(canceled: true);                           // 캡처 잃음 = 이 끌기 전으로
        Assert.Equal(27.0, vm.BuildingHeightPreview);

        rig.Host.BeginHeightDrag();
        rig.Host.DragHeight(-40);
        Assert.Equal(31.0, vm.BuildingHeightPreview);
        Assert.True(rig.PressEsc());                                      // 끄는 중 Esc = 끌기만 취소(판 · 3D 그대로)
        Assert.Equal(27.0, vm.BuildingHeightPreview);
        Assert.True(vm.IsBuildingHeightPanelOpen);
        Assert.Equal("3D", vm.Map3DStatusText);
        rig.Host.DragHeight(-40);                                         // 끝난 끌기는 더 따라가지 않는다
        Assert.Equal(27.0, vm.BuildingHeightPreview);
    });

    // ─────────────── 3. 적용 · 취소 · 닫기 · Esc · 나가기 ───────────────

    [Fact]
    public void should_save_with_put_and_commit_to_3d_when_apply_is_pressed() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        await rig.Apply();

        var (method, path, body) = Assert.Single(rig.Server.Requests);
        Assert.Equal(HttpMethod.Put, method);
        Assert.Equal($"/api/jobs/{Job}/buildings/7/height", path);
        Assert.Equal(30.0, body!.Value<double>("height"));
        Assert.Equal(10, body.Value<int>("floors"));
        var commit = Assert.Single(rig.Sent("building.commit"));
        Assert.Equal(7, commit.Value<int>("id"));
        Assert.Equal("manual", commit["building"]!.Value<string>("height_source"));
        Assert.Equal("지금 30.0 m · 10층(층고 3.0 m)", vm.BuildingHeightCurrentText);
        Assert.Equal("직접 입력", vm.BuildingHeightSourceText);
        Assert.False(vm.IsBuildingHeightChanged);
        Assert.True(vm.BuildingHeightBuilding!.Manual);
        Assert.Equal(1, vm.BuildingHeightUndoCount);
        Assert.True(vm.UndoBuildingHeightCommand.CanExecute(null));
        Assert.True(vm.ResetBuildingHeightCommand.CanExecute(null));
        Assert.Equal("건물 높이를 30.0 m(10층)로 저장했습니다", vm.AimStatusMessage);
        Assert.True(vm.IsBuildingHeightPanelOpen);                        // 적용 뒤에도 판은 남는다(취소 = 이제 30 m)
        int previews = rig.Sent("building.preview").Count;
        vm.CancelBuildingHeightCommand.Execute(null);
        Assert.Equal(previews, rig.Sent("building.preview").Count);       // 저장된 값 그대로라 되돌릴 미리보기 없음
    });

    [Fact]
    public void should_put_original_back_when_cancel_close_escape_or_leaving_3d() => Run(rig =>
    {
        rig.Enter3D();
        var vm = rig.Vm;
        foreach (var how in new[] { "cancel", "close", "esc", "leave" })
        {
            if (how == "leave") rig.Enter3D();
            rig.Posts.Clear();
            rig.PickBuilding(7, 14.5);
            vm.BuildingHeightFloorsText = "12";
            switch (how)
            {
                case "cancel": vm.CancelBuildingHeightCommand.Execute(null); break;
                case "close": vm.BuildingHeightPanel.CloseCommand!.Execute(null); break;
                case "esc": Assert.True(rig.PressEsc()); break;
                case "leave": vm.ExitMap3DCommand.Execute(null); break;
            }
            Assert.False(vm.IsBuildingHeightPanelOpen, how);
            AssertPreview(rig.Sent("building.preview").Last(), 7, null);   // 3D 의 그 건물 = 원래대로
            var select = rig.Sent("building.select").Last();
            Assert.Equal(JTokenType.Null, select["id"]!.Type);              // 강조 지움
            Assert.Empty(rig.Server.Requests);                              // 저장하지 않았다
            Assert.Equal(how == "leave" ? "2D" : "3D", vm.Map3DStatusText); // Esc 는 판만 닫고 3D 에 남는다
        }
        Assert.Same(vm.CancelBuildingHeightCommand, vm.BuildingHeightPanel.CloseCommand);
        // 판이 닫혀 있어도 Esc 로는 3D 를 나가지 않는다(2026-10-19 — 「나가기」 단추로만)
        rig.Enter3D();
        Assert.False(rig.PressEsc());
        Assert.Equal("3D", vm.Map3DStatusText);
    });

    [Fact]
    public void should_cancel_previous_preview_when_another_building_is_picked() => Run(rig =>
    {
        rig.Enter3D();
        var vm = rig.Vm;
        rig.PickBuilding(7, 14.5);
        vm.BuildingHeightFloorsText = "10";
        rig.PickBuilding(7, 14.5);                                         // 같은 건물 다시 = 넣던 값 그대로
        Assert.Equal(30.0, vm.BuildingHeightPreview);

        rig.PickBuilding(8, 6.0, from: "default");
        AssertPreview(rig.Sent("building.preview").Last(), 7, null);       // 앞 건물 미리보기 취소
        Assert.Equal(8, vm.BuildingHeightBuilding!.Id);
        Assert.Equal(6.0, vm.BuildingHeightPreview);
        Assert.Equal("모름(기본값)", vm.BuildingHeightSourceText);
        Assert.Equal("2", vm.BuildingHeightFloorsText);

        int count = rig.Sent("building.preview").Count;
        rig.PickBuilding(9, 9.0);                                           // 바꾸지 않은 건물에서 옮기면 보낼 것 없음
        Assert.Equal(count, rig.Sent("building.preview").Count);
    });

    // 3D 빈 땅 클릭(2026-10-19 사용자 결정) — map-3d 는 건물 · 빈 땅 모두 빈 pick 을 먼저 보내고, 건물 아닌 땅이면 building.none 을 더 보낸다.
    // 빈 pick 만으로는 판을 닫지 않고(건물 클릭과 구분 못 함 — 빈 pick 경로는 바꾸지 않았다) building.none 이면 판을 닫는다 — 미리보기 중이면 원래 높이(취소).
    [Fact]
    public void should_close_panel_and_cancel_preview_when_empty_ground_is_clicked_in_3d() => Run(rig =>
    {
        const string None = "{\"type\":\"building.none\"}";
        rig.Enter3D();
        var vm = rig.Vm;
        rig.PickBuilding(7, 14.5);
        vm.BuildingHeightFloorsText = "12";                                 // 미리보기 36 m
        rig.PickBuilding(7, 14.5);                                         // 같은 건물 다시 누름 — 판 그대로
        Assert.True(vm.IsBuildingHeightPanelOpen);
        Assert.Equal(36.0, vm.BuildingHeightPreview);

        rig.Posts.Clear();
        Assert.True(rig.From3D(None));                                      // 빈 땅(map-3d: 빈 pick 뒤 building.none)
        Assert.False(vm.IsBuildingHeightPanelOpen);
        AssertPreview(rig.Sent("building.preview").Single(), 7, null);     // 원래 높이로
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Single()["id"]!.Type);
        Assert.Empty(rig.Server.Requests);                                  // 저장하지 않았다
        Assert.Equal("3D", vm.Map3DStatusText);                             // 3D 에는 남는다

        rig.Posts.Clear();
        Assert.True(rig.From3D(None));                                      // 판이 닫혀 있으면 아무 일 없음
        Assert.Empty(rig.Posts);

        rig.PickBuilding(8, 6.0);                                           // 바꾸지 않은 판도 닫는다(미리보기 보낼 것 없음)
        Assert.True(rig.From3D(None));
        Assert.False(vm.IsBuildingHeightPanelOpen);
        Assert.Empty(rig.Sent("building.preview"));
    });

    // 판이 열린 채 3D 방향 링 · 이동 단추(2026-10-19 두 기능 조합) — 카메라만 움직이고 판 · 미리보기는 그대로, Esc 는 판만, 나가면 둘 다 정리.
    [Fact]
    public void should_keep_panel_and_preview_when_ring_and_move_buttons_are_used_while_panel_is_open() => Run(rig =>
    {
        bool Moving() => Scenario.GetField<Map3DNavMover?>(rig.Vm, "_map3DNavMover")?.IsActive == true;
        rig.Enter3D();
        var vm = rig.Vm;
        rig.PickBuilding(7, 14.5);
        vm.BuildingHeightFloorsText = "12";                                 // 미리보기 36 m
        rig.Posts.Clear();

        vm.NudgeMap3D(new Map3DNudge(-30, 5, 0, 0, 0));                    // 링 끌기 · 지구본
        vm.Map3DNorthCommand.Execute(null);
        vm.Map3DTopViewCommand.Execute(null);
        vm.StartMap3DMove(Map3DNavMove.Forward);                            // 이동 단추 누름
        Scenario.Call(vm, "TickMap3DMove");
        Assert.True(Moving());
        Assert.True(vm.IsBuildingHeightPanelOpen, "카메라를 움직여도 판은 그대로");
        Assert.Equal(36.0, vm.BuildingHeightPreview);
        Assert.Empty(rig.Posts);                                            // 건물 메시지(미리보기 취소 · 강조 지움)는 보내지 않는다

        Assert.True(rig.PressEsc());                                        // 이동 단추를 누른 채 Esc — 판만 취소
        Assert.False(vm.IsBuildingHeightPanelOpen);
        Assert.Equal("3D", vm.Map3DStatusText);
        Assert.True(Moving(), "Esc 는 판 몫 — 누르고 있는 이동은 그대로");
        vm.StopMap3DMove(Map3DNavMove.Forward);

        rig.PickBuilding(7, 14.5);                                          // 판을 다시 열고 이동을 누른 채 「나가기」
        vm.BuildingHeightFloorsText = "12";
        vm.StartMap3DMove(Map3DNavMove.ZoomOut);
        vm.ExitMap3DCommand.Execute(null);
        Assert.False(vm.IsBuildingHeightPanelOpen);
        Assert.False(Moving());
        AssertPreview(rig.Sent("building.preview").Last(), 7, null);
        Assert.Empty(rig.Server.Requests);
    });

    // 저장 중 빈 땅 클릭 — 판은 닫히고(미리보기 취소 글을 먼저 보냄) 늦게 온 저장 응답은 3D 에 저장된 높이로 알린다(화면 = 서버).
    [Fact]
    public void should_close_panel_and_still_commit_saved_height_when_empty_ground_is_clicked_during_save() => RunAsync(async rig =>
    {
        rig.Enter3D();
        var vm = rig.Vm;
        rig.PickBuilding(7, 14.5);
        vm.BuildingHeightFloorsText = "10";                                 // 30 m
        rig.Server.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var save = rig.Apply();
        await Task.Delay(20);
        rig.Posts.Clear();
        Assert.True(rig.From3D("{\"type\":\"building.none\"}"));
        Assert.False(vm.IsBuildingHeightPanelOpen);
        rig.Server.Gate.SetResult();
        await save;
        await Task.Delay(20);
        Assert.Single(rig.Server.Requests);
        Assert.Equal(new[] { "building.preview", "building.select", "building.commit" }, rig.Posts.Select(p => p.Value<string>("type")));
        Assert.False(vm.IsBuildingHeightPanelOpen, "늦게 온 응답이 판을 다시 열지 않는다");
        Assert.Equal(1, vm.BuildingHeightUndoCount);                        // 같은 3D 세션 — 판 안 되돌리기로 되돌릴 수 있다
        Assert.Equal("3D", vm.Map3DStatusText);
    });

    // ─────────────── 4. 권한 · 연타 · 실패 ───────────────

    [Fact]
    public void should_only_show_with_reason_when_map_edit_permission_is_missing() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        Assert.True(vm.IsBuildingHeightPanelOpen);
        Assert.True(vm.IsBuildingHeightReadOnly);
        Assert.False(vm.IsBuildingHeightEditable);
        Assert.Equal(Map3DBuilding.ReadOnlyReason, vm.BuildingHeightReadOnlyText);

        vm.BuildingHeightFloorsText = "10";
        vm.BuildingHeightText = "40";
        vm.StepBuildingFloorCommand.Execute("1");
        Assert.False(rig.Host.StepHeightByKey(up: true, shift: false));
        rig.Host.BeginHeightDrag();
        rig.Host.DragHeight(-200);
        Assert.Equal(14.5, vm.BuildingHeightPreview);
        Assert.Empty(rig.Posts);
        Assert.False(vm.ApplyBuildingHeightCommand.CanExecute(null));
        Assert.False(vm.StepBuildingFloorCommand.CanExecute(null));
        await rig.Apply();                                                   // 억지로 불러도 저장하지 않는다
        Assert.Empty(rig.Server.Requests);
        vm.BuildingHeightFloorHeightText = "4";                             // 층고(보기 환산)는 바꿀 수 있다
        Assert.Equal("지금 14.5 m · 4층(층고 4.0 m)", vm.BuildingHeightCurrentText);
    }, canEdit: false);

    [Fact]
    public void should_send_one_request_when_apply_is_pressed_repeatedly() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";
        rig.Server.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = rig.Apply();
        await Task.Delay(20);
        Assert.False(vm.IsBuildingHeightEditable);                          // 저장 중 — 입력 · 단추 잠금
        Assert.False(vm.ApplyBuildingHeightCommand.CanExecute(null));
        vm.ApplyBuildingHeightCommand.Execute(null);
        var third = rig.Apply();
        vm.BuildingHeightFloorsText = "20";                                  // 저장 중 입력도 막힌다
        rig.Server.Gate.SetResult();
        await Task.WhenAll(first, third);
        await Task.Delay(20);
        Assert.Single(rig.Server.Requests);
        Assert.Single(rig.Sent("building.commit"));
        Assert.Equal(1, vm.BuildingHeightUndoCount);
        Assert.Equal(30.0, vm.BuildingHeightBuilding!.Height);
        Assert.True(vm.IsBuildingHeightEditable);
    });

    [Fact]
    public void should_keep_preview_and_show_reason_when_server_refuses_or_is_off() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var vm = rig.Vm;
        vm.BuildingHeightFloorsText = "10";

        rig.Server.FailWith = HttpStatusCode.Conflict;
        await rig.Apply();
        Assert.Equal("저장하지 못했습니다: 만드는 중인 작업은 바꿀 수 없습니다", vm.BuildingHeightError);
        Assert.True(vm.IsBuildingHeightChanged);                             // 다시 [적용] 할 수 있게 미리보기 유지
        Assert.Equal(0, vm.BuildingHeightUndoCount);
        Assert.Empty(rig.Sent("building.commit"));

        rig.Server.FailWith = null;
        rig.Server.Unreachable = true;
        await rig.Apply();
        Assert.Equal("3D 서버(map-3d)가 꺼져 있습니다", vm.BuildingHeightError);
        Assert.True(vm.IsBuildingHeightEditable);

        rig.Server.Unreachable = false;
        await rig.Apply();
        Assert.False(vm.HasBuildingHeightError);
        Assert.Equal(30.0, vm.BuildingHeightBuilding!.Height);
    });

    // ─────────────── 5. 자동으로 되돌리기 · 판 안 되돌리기 ───────────────

    [Fact]
    public void should_reset_to_auto_and_step_back_through_saves_with_panel_undo() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 12.0, from: "levels", origin: "national");
        var vm = rig.Vm;
        Assert.Equal("국가 자료", vm.BuildingHeightSourceText);

        vm.BuildingHeightFloorsText = "10";
        await rig.Apply();                                                    // ① 자동 12 → 직접 30
        vm.BuildingHeightText = "33";
        await rig.Apply();                                                    // ② 직접 30 → 직접 33
        Assert.Equal(2, vm.BuildingHeightUndoCount);

        await rig.Reset();                                                    // ③ 직접 33 → 자동 12
        Assert.Equal(HttpMethod.Delete, rig.Server.Requests.Last().Method);
        Assert.Equal($"/api/jobs/{Job}/buildings/7/height", rig.Server.Requests.Last().Path);
        Assert.Equal(12.0, vm.BuildingHeightBuilding!.Height);
        Assert.False(vm.BuildingHeightBuilding.Manual);
        Assert.Equal("국가 자료", vm.BuildingHeightSourceText);
        Assert.Equal("자동으로 잰 높이로 되돌렸습니다", vm.AimStatusMessage);
        Assert.Equal(3, vm.BuildingHeightUndoCount);

        Assert.True(rig.Host.UndoHeight());                                   // Ctrl+Z(판 안) = 되돌리기 단추
        await Task.Delay(30);
        Assert.Equal(HttpMethod.Put, rig.Server.Requests.Last().Method);      // ③ 되돌림 → 33 다시
        Assert.Equal(33.0, rig.Server.Requests.Last().Body!.Value<double>("height"));
        Assert.Equal(33.0, vm.BuildingHeightBuilding!.Height);
        await rig.Undo();                                                     // ② 되돌림 → 30
        Assert.Equal(30.0, rig.Server.Requests.Last().Body!.Value<double>("height"));
        Assert.Equal(10, rig.Server.Requests.Last().Body!.Value<int>("floors"));
        await rig.Undo();                                                     // ① 되돌림 → 자동(DELETE)
        Assert.Equal(HttpMethod.Delete, rig.Server.Requests.Last().Method);
        Assert.Equal(12.0, vm.BuildingHeightBuilding!.Height);
        Assert.Equal(0, vm.BuildingHeightUndoCount);
        Assert.False(vm.UndoBuildingHeightCommand.CanExecute(null));
        Assert.False(rig.Host.UndoHeight());
        Assert.Equal(6, rig.Sent("building.commit").Count);                    // 저장마다 3D 에 그 줄

        // 되돌리기는 이 3D 동안만 — 나갔다 오면 비어 있다
        vm.BuildingHeightFloorsText = "5";
        await rig.Apply();
        Assert.Equal(1, vm.BuildingHeightUndoCount);
        vm.ExitMap3DCommand.Execute(null);
        Assert.Equal(0, vm.BuildingHeightUndoCount);
    });

    // ─────────────── 6. 3D 영역 목록 ───────────────

    [Fact]
    public void should_show_manual_count_warn_unmatched_and_reset_all_after_confirm_in_area_menu() => RunAsync(async rig =>
    {
        rig.Enter3D(manualHeights: 3, unmatched: 1);
        var vm = rig.Vm;
        var item = Assert.Single(vm.Map3DAreas);
        Assert.Contains("직접 입력한 건물 높이 3개", item.MenuHeader);
        Assert.Contains("다시 붙이지 못한 직접 입력 1개", item.ToolTip);
        var reset = Assert.Single(item.Actions, a => a.Header.Contains("모두 자동으로"));
        Assert.Equal("직접 입력한 건물 높이 3개 — 모두 자동으로…", reset.Header);

        // 못 붙인 직접 입력 알림 — 영역마다 그 수로 한 번
        rig.Areas(item.Scene);
        Assert.Contains("다시 붙이지 못한 직접 입력 1개", vm.AimStatusMessage);
        vm.AimStatusMessage = "";
        rig.Areas(item.Scene);
        Assert.Equal("", vm.AimStatusMessage);

        // 확인 팝업 → 확인 = 서버 DELETE → 띄워 둔 3D 에 되돌린 줄들
        rig.PickBuilding(7, 30.0, manual: true);
        reset.Command.Execute(null);
        await Task.Delay(20);
        var popup = Assert.IsType<OpenConfirmPopupMessageModel>(rig.W.Published.Last());
        Assert.Contains("직접 입력한 건물 높이 3개", popup.Explain);
        Assert.IsType<CallResetMap3DHeightsProcessMessageModel>(popup.MessageModel);
        Assert.Empty(rig.Server.Requests);                                    // 확인 전엔 지우지 않는다
        await vm.HandleAsync(new CallResetMap3DHeightsProcessMessageModel(), CancellationToken.None);
        var (method, path, _) = Assert.Single(rig.Server.Requests);
        Assert.Equal((HttpMethod.Delete, $"/api/jobs/{Job}/height-overrides"), (method, path));
        Assert.Equal(new[] { 7, 8 }, rig.Sent("building.commit").Select(c => c.Value<int>("id")));
        Assert.False(vm.IsBuildingHeightPanelOpen);                            // 그 장면 판은 닫는다
        Assert.Equal("건물 2개를 자동으로 잰 높이로 되돌렸습니다", vm.AimStatusMessage);

        // 직접 입력이 없는 영역은 메뉴에 넣지 않는다
        rig.Areas(item.Scene with { ManualHeights = 0, ManualUnmatched = 0 });
        Assert.DoesNotContain(Assert.Single(vm.Map3DAreas).Actions, a => a.Header.Contains("자동으로"));
    });

    // ─────────────── 7. 판 템플릿 ───────────────

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
    public void should_build_panel_with_every_automation_id_drag_handle_keys_and_light_dark_tokens() => Run(rig =>
    {
        // 칸 글이 VM 을 따라가는지 보려면 알림이 나가야 한다 — 생성자 없이 만든 VM 은 IsNotifying 이 꺼져 있다(제품 생성자는 켠다).
        // 알림은 앱과 같은 길(이 STA 디스패처 · XamlPlatformProvider — 다른 화면 시험과 같은 처리)
        rig.Vm.IsNotifying = true;
        var platform = Caliburn.Micro.PlatformProvider.Current;
        Caliburn.Micro.PlatformProvider.Current = new Caliburn.Micro.XamlPlatformProvider();
        try { PanelTemplateScenario(rig); }
        finally { Caliburn.Micro.PlatformProvider.Current = platform; }
    });

    private static void PanelTemplateScenario(Rig rig)
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var panel = rig.Vm.BuildingHeightPanel;
        Assert.Same(rig.Vm, panel.DataContext);
        Assert.Same(rig.Vm, panel.Host);
        Assert.Equal("Map.BuildingHeight.Panel", AutomationProperties.GetAutomationId(panel));
        Assert.NotNull(System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(panel));

        var dict = PropertyStyleLoader.Load("BuildingHeightPanelStyle.xaml");
        panel.Resources.MergedDictionaries.Add(dict);
        panel.Style = (Style)dict[typeof(BuildingHeightPanelControl)];
        Scenario.Layout(panel, 320, 700);

        var ids = Descendants(panel).Select(AutomationProperties.GetAutomationId).Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        foreach (var id in new[] { "Map.BuildingHeight.Floors", "Map.BuildingHeight.Height", "Map.BuildingHeight.FloorHeight", "Map.BuildingHeight.PlusFloor",
                     "Map.BuildingHeight.MinusFloor", "Map.BuildingHeight.Handle", "Map.BuildingHeight.Apply", "Map.BuildingHeight.Cancel", "Map.BuildingHeight.Reset",
                     "Map.BuildingHeight.Source", "Map.BuildingHeight.Undo", "Map.BuildingHeight.Close", "Map.BuildingHeight.Current", "Map.BuildingHeight.Summary",
                     "Help.Map.BuildingHeight.Panel" })
            Assert.Contains(id, ids);

        // 바인딩이 실제 VM 값을 보인다
        Assert.Equal("그림자로 잼", ById<TextBlock>(panel, "Map.BuildingHeight.Source").Text);
        Assert.Equal("5", ById<TextBox>(panel, "Map.BuildingHeight.Floors").Text);
        Assert.True(ById<TextBox>(panel, "Map.BuildingHeight.Height").IsEnabled);

        // 손잡이(Thumb) 끌기 이벤트 → 판 → VM(미리보기). 끌기 시작 · 움직임 · 취소
        var handle = ById<Thumb>(panel, "Map.BuildingHeight.Handle");
        handle.RaiseEvent(new DragStartedEventArgs(0, 0));
        handle.RaiseEvent(new DragDeltaEventArgs(0, -60));
        Assert.Equal(20.5, rig.Vm.BuildingHeightPreview);
        handle.RaiseEvent(new DragCompletedEventArgs(0, -60, canceled: true));
        Assert.Equal(14.5, rig.Vm.BuildingHeightPreview);

        // 판 안 키 — ↑ = +1층
        var floors = ById<TextBox>(panel, "Map.BuildingHeight.Floors");
        var source = PresentationSource.FromVisual(panel) ?? new HeadlessKeySource(panel);
        floors.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Up) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert.Equal(18.0, rig.Vm.BuildingHeightPreview);                       // 5층 + 1 = 6층 × 3.0
        Assert.Equal("6", rig.Vm.BuildingHeightFloorsText);
        rig.Vm.BuildingHeightText = "21";
        Assert.Equal("7", rig.Vm.BuildingHeightFloorsText);
        Assert.Equal("21.0", ById<TextBox>(panel, "Map.BuildingHeight.Height").Text);
        Assert.Equal("7", floors.Text);

        // 머리 띠 색 = 토큰(PrimaryBrush) — 라이트 → 다크로 바꾸면 따라 바뀐다(하드코딩 아님)
        var header = Descendants(panel).OfType<Border>().First(bd => bd.Cursor == Cursors.SizeAll);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#0C6B89"), ((SolidColorBrush)header.Background).Color);
        var light = dict.MergedDictionaries.First(d => d.Source?.OriginalString.Contains("Tokens.Light") == true);
        dict.MergedDictionaries[dict.MergedDictionaries.IndexOf(light)] = new ResourceDictionary
        { Source = new Uri("pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml") };
        Scenario.Layout(panel, 320, 700);
        Assert.Equal((Color)ColorConverter.ConvertFromString("#22B8D9"), ((SolidColorBrush)header.Background).Color);
    }

    [Fact]
    public void should_disable_inputs_and_show_reason_in_panel_when_permission_is_missing() => Run(rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7, 14.5);
        var panel = rig.Vm.BuildingHeightPanel;
        var dict = PropertyStyleLoader.Load("BuildingHeightPanelStyle.xaml");
        panel.Resources.MergedDictionaries.Add(dict);
        panel.Style = (Style)dict[typeof(BuildingHeightPanelControl)];
        Scenario.Layout(panel, 320, 700);
        foreach (var id in new[] { "Map.BuildingHeight.Floors", "Map.BuildingHeight.Height" })
            Assert.False(ById<TextBox>(panel, id).IsEnabled, id);
        Assert.False(ById<Thumb>(panel, "Map.BuildingHeight.Handle").IsEnabled);
        Assert.True(ById<TextBox>(panel, "Map.BuildingHeight.FloorHeight").IsEnabled);
        var reason = ById<TextBlock>(panel, "Map.BuildingHeight.ReadOnly");
        Assert.Equal(Visibility.Visible, reason.Visibility);
        Assert.Equal(Map3DBuilding.ReadOnlyReason, reason.Text);
    }, canEdit: false);

    private sealed class HeadlessKeySource : PresentationSource
    {
        private Visual? _root;
        public HeadlessKeySource(Visual root) { RootVisual = root; }
        public override Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }
}
