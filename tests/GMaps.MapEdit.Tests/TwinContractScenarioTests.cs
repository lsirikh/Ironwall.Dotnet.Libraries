using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
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
/// 3D 지도 꾸미기 계약(twin-contract.md, 2026-10-21) GIS 쪽 — 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 map-3d 엔진으로:
/// D 「건물 높이」 판 외관 칸 · [이 건물 숨기기](확인) · 판 안 되돌리기 · 「숨긴 건물」 되살리기 · 모두 되살리기 · 옛 엔진(404),
/// B 3D 에서 직접 그린 건물 고름(userbuilding.pick · user_uid 있는 building) → 2D 고치기 판 · 외관 저장 본문 · 상자 미리보기 키,
/// C 「꾸미기」 판 — 도구별 놓기(한 그루 · 담장 · 소품) · 본문 키 · decorations.reload · 고르기 · 방향 키 · 적용 · 지우기 · 되돌리기/다시 하기 ·
/// 옛 엔진(404) · 3D 에서 고름(decoration.pick), 판 템플릿 실제 그리기(아이콘 · 토큰 · AutomationId).
/// </summary>
public partial class TwinContractScenarioTests
{
    private const string Job = "j-twin";
    private const string Uid = "0123456789ab";

    // ─────────────── 장치 ───────────────

    private sealed class FakeEngine : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, JObject? Body)> Requests { get; } = new();
        public double[] Bbox { get; set; } = Array.Empty<double>();
        /// <summary>looks · decorations 주소가 없는 옛 엔진(FastAPI 기본 404 "Not Found").</summary>
        public bool OldEngine { get; set; }
        /// <summary>look 응답에 needs_rebake=true(다시 굽기에서 숨겨 장면에 없는 건물).</summary>
        public bool NeedsRebake { get; set; }
        /// <summary>look 응답에 facade_live=true(엔진이 외벽 무늬를 바로 반영).</summary>
        public bool FacadeLive { get; set; }
        /// <summary>user-buildings · decorations PUT 을 400 {"detail": 이 글}로 거절(값 범위 · 개수 상한 등 서버 문구).</summary>
        public string? RejectPut { get; set; }
        /// <summary>PUT …/buildings/{bid}/look 을 422 {"detail": 이 글}로 거절(범위 밖 값 등 서버 문구).</summary>
        public string? RejectLook { get; set; }
        /// <summary>user-buildings PUT 의 taper_deg 가 이보다 크면 400 {"detail":"위로 갈수록 너무 좁아져 윗면이 뒤집힙니다"}(엔진의 모양 거절).</summary>
        public double? RejectTaperAbove { get; set; }
        /// <summary>장면 파일(GET …/files/scene.json)의 buildings 줄 — null 이면 404.</summary>
        public JArray? SceneBuildings { get; set; }
        public List<JObject> UserBuildings { get; } = new();
        public Dictionary<int, JObject> Looks { get; } = new();
        public List<JObject> Decorations { get; } = new();

        /// <summary>외벽 무늬 목록 · .iw3d 패키지 · 넣은 모델(facade-package-contract) — 먼저 묻고 답하면 그 응답(본문이 JSON 이 아닐 수 있다: multipart · zip).</summary>
        public Func<HttpRequestMessage, Task<HttpResponseMessage?>>? Extra { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Extra != null && await Extra(request) is { } extra) return extra;
            string? text = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.AbsolutePath;
            var body = text == null ? null : JObject.Parse(text);
            Requests.Add((request.Method, path, body));
            var m = request.Method;
            if (path == "/api/jobs" && m == HttpMethod.Get)
                return Json(HttpStatusCode.OK, new JObject { ["scenes"] = new JArray(new JObject { ["id"] = Job, ["name"] = "시험 영역", ["bbox"] = new JArray(Bbox.Cast<object>().ToArray()) }) }.ToString());
            var seg = path.Split('/');   // "", "api", "jobs", {job}, …
            if (seg.Length < 5 || seg[3] != Job) return Json(HttpStatusCode.NotFound, "{\"detail\":\"작업이 없습니다\"}");
            if (RejectPut != null && m == HttpMethod.Put && seg[4] is "user-buildings" or "decorations")
                return Json(HttpStatusCode.BadRequest, new JObject { ["detail"] = RejectPut }.ToString());
            if (RejectTaperAbove is double max && m == HttpMethod.Put && seg[4] == "user-buildings" && body?["taper_deg"]?.Value<double>() > max)
                return Json(HttpStatusCode.BadRequest, "{\"detail\":\"위로 갈수록 너무 좁아져 윗면이 뒤집힙니다\"}");
            switch (seg[4])
            {
                case "files" when seg.Length == 6 && seg[5] == "scene.json":
                    return SceneBuildings == null ? Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}")
                                                  : Json(HttpStatusCode.OK, new JObject { ["buildings"] = SceneBuildings }.ToString());
                case "user-buildings":
                    if (m == HttpMethod.Get) return Json(HttpStatusCode.OK, new JObject { ["items"] = new JArray(UserBuildings) }.ToString());
                    if (m == HttpMethod.Put)
                    {
                        var item = (JObject)body!.DeepClone();
                        item["uid"] = seg[5];
                        UserBuildings.RemoveAll(i => i.Value<string>("uid") == seg[5]);
                        UserBuildings.Add(item);
                        return Json(HttpStatusCode.OK, new JObject { ["item"] = item, ["count"] = UserBuildings.Count }.ToString());
                    }
                    UserBuildings.RemoveAll(i => i.Value<string>("uid") == seg[5]);
                    return Json(HttpStatusCode.OK, "{\"deleted\":1}");
                case "buildings" when seg.Length == 7 && seg[6] == "height":   // 여러 개 함께 적용 시험(2026-10-22) — 저장 = 직접 입력, 지움 = 자동 12 m
                {
                    var row = m == HttpMethod.Delete
                        ? new JObject { ["id"] = int.Parse(seg[5]), ["origin"] = "photo", ["height"] = 12.0, ["height_from"] = "shadow", ["manual"] = false }
                        : new JObject { ["id"] = int.Parse(seg[5]), ["origin"] = "photo", ["height"] = body!["height"], ["height_from"] = "manual", ["manual"] = true, ["levels"] = body["floors"] };
                    // 엔진 응답 = scene.json 건물 줄 그대로 — 저장된 외관은 look:{…} 안(heights._set_height · looks._set_row_look)
                    if (Looks.TryGetValue(int.Parse(seg[5]), out var saved)) row["look"] = saved.DeepClone();
                    return Json(HttpStatusCode.OK, new JObject { ["building"] = row }.ToString());
                }
                case "buildings" when seg.Length == 7 && seg[6] == "look":
                    if (OldEngine) return Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");
                    if (RejectLook != null) return Json(HttpStatusCode.UnprocessableEntity, new JObject { ["detail"] = RejectLook }.ToString());
                    int id = int.Parse(seg[5]);
                    var look = Looks.TryGetValue(id, out var l) ? l : new JObject { ["hidden"] = false, ["wall_color"] = null, ["roof_color"] = null, ["facade"] = "auto" };
                    look.Merge(body, new Newtonsoft.Json.Linq.JsonMergeSettings { MergeNullValueHandling = MergeNullValueHandling.Merge });
                    Looks[id] = look;
                    var b = (JObject)look.DeepClone();
                    b["id"] = id; b["origin"] = "photo";
                    if (NeedsRebake) b["needs_rebake"] = true;
                    if (FacadeLive) b["facade_live"] = true;
                    return Json(HttpStatusCode.OK, new JObject { ["building"] = b, ["looks"] = new JObject { ["count"] = Looks.Count } }.ToString());
                case "looks":
                    if (OldEngine) return Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");
                    if (m == HttpMethod.Delete) { int n = Looks.Count; Looks.Clear(); return Json(HttpStatusCode.OK, $"{{\"reset\":{n}}}"); }
                    var items = new JArray(Looks.Select(kv =>
                    {
                        var o = (JObject)kv.Value.DeepClone();
                        o["id"] = kv.Key;
                        o["outline"] = new JArray(new JArray(127.0, 37.5), new JArray(127.0002, 37.5), new JArray(127.0002, 37.5002));
                        return (object)o;
                    }).ToArray());
                    return Json(HttpStatusCode.OK, new JObject { ["items"] = items, ["count"] = items.Count }.ToString());
                case "decorations":
                    if (OldEngine) return Json(HttpStatusCode.NotFound, "{\"detail\":\"Not Found\"}");
                    if (m == HttpMethod.Get) return Json(HttpStatusCode.OK, new JObject { ["items"] = new JArray(Decorations), ["count"] = Decorations.Count }.ToString());
                    if (m == HttpMethod.Put)
                    {
                        var item = (JObject)body!.DeepClone();
                        item["uid"] = seg[5];
                        int at = Decorations.FindIndex(i => i.Value<string>("uid") == seg[5]);
                        if (at >= 0) Decorations[at] = item; else Decorations.Add(item);
                        return Json(HttpStatusCode.OK, new JObject { ["item"] = item, ["count"] = Decorations.Count }.ToString());
                    }
                    if (seg.Length == 5) { Decorations.Clear(); return Json(HttpStatusCode.OK, "{\"deleted\":0}"); }
                    return Decorations.RemoveAll(i => i.Value<string>("uid") == seg[5]) > 0
                        ? Json(HttpStatusCode.OK, "{\"deleted\":1}") : Json(HttpStatusCode.NotFound, "{\"detail\":\"그 항목이 없습니다\"}");
            }
            return Json(HttpStatusCode.NotFound, "{\"detail\":\"없음\"}");
        }

        private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; }
        public MapViewModel Vm => W.Vm;
        public FakeEngine Server { get; } = new();
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
            W.Map.SetEffectiveZoom(18d);
            Center = W.Map.Position;
            Server.Bbox = new[] { Center.Lng - 0.002, Center.Lat - 0.002, Center.Lng + 0.002, Center.Lat + 0.002 };
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
            Scenario.SetField(Vm, "_map3DClient", new Map3DClient("http://127.0.0.1:8765", Server));
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(json => Posts.Add(JObject.Parse(json))));
            Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)new[] { new Map3DSceneInfo(Job, "시험 영역", Server.Bbox, null, 2, null) });
        }

        public void Enter3D()
        {
            var host = Vm.Map3DHost;
            SetHost(host, "_job", Job);
            SetHost(host, "_ready", true);
            SetHost(host, "_rendered", true);
            SetHost(host, "_failed", false);
            SetHost(host, "<ReadyAt>k__BackingField", (DateTime?)DateTime.UtcNow.AddSeconds(-10));
            Scenario.SetField(Vm, "_bhFloorsText", "");
            Scenario.SetField(Vm, "_bhHeightText", "");
            Scenario.Call(Vm, "EvaluateMap3DGate");
            Scenario.Call(Vm, "UpdateMap3DAreaDisplay");
            Vm.EnterMap3DCommand.Execute(null);
            Scenario.Call(Vm, "OnMap3DGuideTick");
            Assert.Equal("3D", Vm.Map3DStatusText);
        }

        private static void SetHost(Map3DViewHost host, string name, object? value)
            => typeof(Map3DViewHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, value);

        public void From3D(JObject o)
        {
            var msg = Map3DMath.ParseMessage(o.ToString());
            Assert.NotNull(msg);
            Scenario.Call(Vm, "OnMap3DMessage", msg);
        }

        public void PickBuilding(int id, string origin = "photo", JObject? extra = null)
        {
            var o = new JObject { ["type"] = "building", ["id"] = id, ["origin"] = origin, ["height"] = 12.0, ["height_from"] = "shadow", ["lon"] = 127.0, ["lat"] = 37.5 };
            if (extra != null) o.Merge(extra);
            From3D(o);
        }

        public UbPoint ScreenOf(double eastM, double northM)
        {
            var l = W.Map.FromLatLngToLocal(new PointLatLng(Center.Lat + northM / 110540.0, Center.Lng + eastM / (111320.0 * Math.Cos(Center.Lat * Math.PI / 180))));
            return new UbPoint(l.X, l.Y);
        }

        public void DecoClick(double eastM, double northM) => ((IDecorationHost)Vm).DecorationPointerDown(ScreenOf(eastM, northM), 1);
        public bool DecoKey(Key key, ModifierKeys mods = ModifierKeys.None) => (bool)Scenario.Call(Vm, "HandleDecorationKey", key, mods, false)!;

        public List<(HttpMethod Method, string Path, JObject? Body)> Writes => Server.Requests.Where(r => r.Method != HttpMethod.Get).ToList();
        public List<JObject> Sent(string type) => Posts.Where(p => p.Value<string>("type") == type).ToList();

        public void AddUserBuilding()
            => Server.UserBuildings.Add(new JObject
            {
                ["uid"] = Uid, ["height"] = 9.0, ["floors"] = 3, ["name"] = "경비동", ["roof"] = "gable", ["use"] = "auto",
                ["outline"] = new JArray(new JArray(Center.Lng, Center.Lat), new JArray(Center.Lng + 0.0002, Center.Lat), new JArray(Center.Lng + 0.0002, Center.Lat + 0.0002),
                                         new JArray(Center.Lng, Center.Lat + 0.0002)),
            });

        public static async Task Until(Func<bool> cond, string what)
        {
            for (int i = 0; i < 300 && !cond(); i++) await Task.Delay(10);
            Assert.True(cond(), what);
        }

        public void Done()
        {
            Assert.Empty(W.Errors);
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }
    }

    private static void RunAsync(Func<Rig, Task> body, bool canEdit = true)
        => Scenario.StaAsync(async () => { var rig = new Rig(canEdit); await body(rig); rig.Done(); });

    // ─────────────── D. 「건물 높이」 판 — 외관 · 숨기기 ───────────────

    [Fact]
    public void should_hide_building_after_confirm_then_undo_restore_and_restore_all() => RunAsync(async rig =>
    {
        rig.Enter3D();
        rig.PickBuilding(7);
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/looks"), "숨긴 건물 목록 요청");
        Assert.Equal("숨긴 건물 없음", rig.Vm.BuildingHiddenCountText);
        Assert.Equal("이 건물 지우기", rig.Vm.BuildingHideButtonText);                  // 2026-10-22 사용자가 찾는 말(실제로는 숨김 · 되살리기 가능)
        Assert.Empty(rig.Posts);                                                         // 고르기만으로는 3D 를 바꾸지 않는다

        rig.Vm.HideBuildingCommand.Execute(null);
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Any(), "확인 창");
        Assert.Empty(rig.Writes);                                                        // 확인 전에는 숨기지 않는다
        var confirm = rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Last();
        await rig.Vm.HandleAsync((CallHideMap3DBuildingProcessMessageModel)confirm.MessageModel!, CancellationToken.None);
        var put = rig.Writes.Single();
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/buildings/7/look"), (put.Method, put.Path));
        Assert.Equal("{\"hidden\":true}", put.Body!.ToString(Newtonsoft.Json.Formatting.None));
        var look = rig.Sent("building.look").Single();
        Assert.Equal((7, true), (look.Value<int>("id"), look.Value<bool>("hidden")));
        Assert.Equal(JTokenType.Null, rig.Sent("building.select").Last()["id"]!.Type);  // 사라진 건물의 강조 윤곽도 지움
        Assert.True(rig.Vm.IsBuildingHidden);
        Assert.False(rig.Vm.HideBuildingCommand.CanExecute(null));
        Assert.Equal("숨긴 건물 1개", rig.Vm.BuildingHiddenCountText);
        Assert.Contains(rig.W.Published, m => m is ClosePopupMessageModel);

        // 판 안 되돌리기 = 되살리기(앞 모양으로 다시 저장)
        rig.Vm.UndoBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2 && !rig.Vm.IsBuildingHidden, "되돌리기");
        Assert.False(rig.Writes[1].Body!.Value<bool>("hidden"));
        Assert.Equal("auto", rig.Writes[1].Body!.Value<string>("facade"));
        Assert.False(rig.Sent("building.look").Last().Value<bool>("hidden"));
        Assert.Equal("숨긴 건물 없음", rig.Vm.BuildingHiddenCountText);

        // 다시 숨기고 목록의 [되살리기]
        rig.Vm.HideBuildingCommand.Execute(null);
        await rig.Vm.HandleAsync(new CallHideMap3DBuildingProcessMessageModel(), CancellationToken.None);
        var hidden = Assert.Single(rig.Vm.BuildingHiddenItems);
        Assert.StartsWith("건물 7", hidden.Text);
        Assert.True(rig.Vm.RestoreHiddenBuildingCommand.CanExecute(hidden));
        rig.Vm.RestoreHiddenBuildingCommand.Execute(hidden);
        await Rig.Until(() => !rig.Vm.HasBuildingHidden, "되살리기");
        Assert.Equal("{\"hidden\":false}", rig.Writes.Last().Body!.ToString(Newtonsoft.Json.Formatting.None));

        // [모두 되살리기](확인) = DELETE …/looks → 알던 건물은 3D 에 자동 모양으로
        rig.Vm.RestoreAllHiddenBuildingsCommand.Execute(null);
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Count() == 3, "모두 되살리기 확인 창");
        int looksBefore = rig.Sent("building.look").Count;
        await rig.Vm.HandleAsync(new CallRestoreMap3DLooksProcessMessageModel(), CancellationToken.None);
        Assert.Equal((HttpMethod.Delete, $"/api/jobs/{Job}/looks"), (rig.Writes.Last().Method, rig.Writes.Last().Path));
        Assert.Equal(looksBefore + 1, rig.Sent("building.look").Count);
        Assert.Empty(rig.Server.Looks);
        Assert.Equal(0, rig.Vm.BuildingHeightUndoCount);                                  // 지운 모양으로 되돌리지 않게 외관 칸은 비운다
    });

    [Fact]
    public void should_apply_facade_and_colors_with_partial_body_and_live_3d_message() => RunAsync(async rig =>
    {
        rig.Server.Looks[8] = new JObject { ["hidden"] = false, ["wall_color"] = "#f2f2f2", ["roof_color"] = null, ["facade"] = "auto" };
        rig.Enter3D();
        rig.PickBuilding(8, extra: new JObject { ["wall_color"] = "#F2F2F2", ["facade"] = "auto" });
        Assert.Equal("#f2f2f2", rig.Vm.BuildingWallColor);
        Assert.Equal("", rig.Vm.BuildingRoofColor);                                       // 자동
        Assert.False(rig.Vm.ApplyBuildingLookCommand.CanExecute(null));
        rig.Vm.BuildingFacade = "glass";
        rig.Vm.BuildingRoofColor = "#4a4a4a";
        Assert.True(rig.Vm.IsBuildingLookChanged);
        Assert.Empty(rig.Writes);
        rig.Vm.ApplyBuildingLookCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 1 && !rig.Vm.IsBuildingLookChanged, "외관 저장");
        var body = rig.Writes[0].Body!;
        Assert.Equal(new[] { "wall_color", "roof_color", "facade" }, body.Properties().Select(p => p.Name));   // hidden 은 보내지 않는다(그대로)
        Assert.Equal(("#f2f2f2", "#4a4a4a", "glass"), (body.Value<string>("wall_color"), body.Value<string>("roof_color"), body.Value<string>("facade")));
        var msg = rig.Sent("building.look").Single();
        Assert.Equal(("#f2f2f2", "#4a4a4a", false), (msg.Value<string>("wall_color"), msg.Value<string>("roof_color"), msg.Value<bool>("hidden")));
        Assert.Equal("glass", rig.Vm.BuildingFacade);
        Assert.Equal(Map3DLook.FacadeRebakeText, rig.Vm.BuildingLookNotice);                // 외벽 무늬는 다시 만들 때 바뀐다
        // 되돌리기 = 앞 색 · 무늬로
        rig.Vm.UndoBuildingHeightCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기");
        Assert.Equal(("auto", JTokenType.Null), (rig.Writes[1].Body!.Value<string>("facade"), rig.Writes[1].Body!["roof_color"]!.Type));
        Assert.Equal("auto", rig.Vm.BuildingFacade);
    });

    [Fact]
    public void should_send_facade_live_and_skip_rebake_notice_when_engine_applies_facade_now() => RunAsync(async rig =>
    {
        rig.Server.FacadeLive = true;
        rig.Enter3D();
        rig.PickBuilding(8);
        rig.Vm.BuildingFacade = "brick";
        rig.Vm.ApplyBuildingLookCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 1 && !rig.Vm.IsBuildingLookChanged, "외관 저장");
        Assert.Equal("brick", rig.Sent("building.look").Single().Value<string>("facade"));   // 3D 가 바로 무늬를 바꾸게
        Assert.Null(rig.Vm.BuildingLookNotice);                                               // 바로 반영됐으니 「다시 만들면」 안내 없음
    });

    [Fact]
    public void should_turn_off_look_tools_and_say_why_on_old_engine() => RunAsync(async rig =>
    {
        rig.Server.OldEngine = true;
        rig.Enter3D();
        rig.PickBuilding(7);
        await Rig.Until(() => rig.Vm.IsBuildingLookUnsupported, "옛 엔진 판정");
        Assert.Equal(Map3DLook.OldEngineText, rig.Vm.BuildingLookUnsupportedText);
        Assert.False(rig.Vm.IsBuildingLookAvailable);
        Assert.False(rig.Vm.HideBuildingCommand.CanExecute(null));
        rig.Vm.BuildingFacade = "brick";
        Assert.False(rig.Vm.ApplyBuildingLookCommand.CanExecute(null));
        Assert.True(rig.Vm.ApplyBuildingHeightCommand.CanExecute(null) || !rig.Vm.IsBuildingHeightChanged);   // 높이 칸은 그대로 쓸 수 있다
    });

    [Fact]
    public void should_not_try_to_show_revived_building_missing_from_scene_and_say_rebake() => RunAsync(async rig =>
    {
        rig.Server.Looks[9] = new JObject { ["hidden"] = true, ["wall_color"] = null, ["roof_color"] = null, ["facade"] = "auto" };
        rig.Enter3D();
        rig.PickBuilding(7);
        await Rig.Until(() => rig.Vm.HasBuildingHidden, "숨긴 건물 목록");
        rig.Server.NeedsRebake = true;
        rig.Vm.RestoreHiddenBuildingCommand.Execute(rig.Vm.BuildingHiddenItems.Single());
        await Rig.Until(() => !rig.Vm.HasBuildingHidden, "되살리기");
        Assert.Empty(rig.Sent("building.look"));                                         // 장면(GLB)에 없는 건물 — 보이게 하려 하지 않는다
        Assert.Equal(Map3DLook.NeedsRebakeText, rig.Vm.BuildingLookNotice);
    });

    // ─────────────── B. 3D 에서 직접 그린 건물 고름 → 2D 고치기 판 ───────────────

    [Fact]
    public void should_open_edit_panel_over_3d_for_user_building_picked_in_3d_and_save_look() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "userbuilding.pick", ["uid"] = Uid });
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);                                      // 3D 에 머문 채 3D 위 판(edit3d-contract 1단계)
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
        Assert.Equal("건물 고치기 · 경비동", rig.Vm.UserBuildingTitle);
        Assert.Equal(Uid, rig.Sent("userbuilding.select").First().Value<string>("uid"));
        Assert.Equal("gable", rig.Vm.UserBuildingRoof);

        rig.Vm.UserBuildingFacade = "brick";
        rig.Vm.UserBuildingWallColor = "#A0522D";
        rig.Vm.UserBuildingRoof = "hip";
        await (Task)Scenario.Call(rig.Vm, "ApplyUserBuildingAsync")!;
        var put = rig.Writes.Single(w => w.Method == HttpMethod.Put);
        Assert.Equal(("hip", "#a0522d", JTokenType.Null, "brick"),
                     (put.Body!.Value<string>("roof"), put.Body.Value<string>("wall_color"), put.Body["roof_color"]!.Type, put.Body.Value<string>("facade")));
        var preview = rig.Sent("userbuilding.preview").Last();
        Assert.Equal((Uid, 3, "brick", "#a0522d", "hip"),
                     (preview.Value<string>("uid"), preview.Value<int>("floors"), preview.Value<string>("facade"), preview.Value<string>("wall_color"), preview.Value<string>("roof")));
        Assert.True(rig.W.Undo.CanUndo);
        Assert.Equal(JTokenType.Null, rig.Sent("userbuilding.select").Last()["uid"]!.Type);   // 저장 뒤 새 윤곽 = 고름 표시 끔
    });

    [Fact]
    public void should_route_baked_user_building_with_user_uid_to_edit_panel_not_height_panel() => RunAsync(async rig =>
    {
        rig.AddUserBuilding();
        rig.Enter3D();
        rig.PickBuilding(3, origin: "user", extra: new JObject { ["user_uid"] = Uid });
        Assert.False(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        await Rig.Until(() => rig.Vm.UserBuildingEditingUid == Uid, "그 건물 고치기");
    });

    // ─────────────── C. 「꾸미기」 ───────────────

    [Fact]
    public void should_place_tree_and_wall_with_contract_bodies_reload_3d_and_undo_redo() => RunAsync(async rig =>
    {
        rig.Vm.DecorateCommand.Execute(null);
        Assert.True(rig.Vm.IsDecorating);
        Assert.True((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
        Assert.True(AdornerLayer.GetAdornerLayer(rig.W.Map)!.GetAdorners(rig.W.Map)!.OfType<DecorationAdorner>().Any());
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/decorations"), "꾸미기 목록 요청");
        Assert.Equal("도구를 고르거나 지도 위 항목을 눌러 고치세요", rig.Vm.DecorationStatusText);
        Assert.False(rig.Vm.HasDecorationValues);

        // 한 그루 = 누르면 바로 저장
        rig.Vm.SelectDecorationToolCommand.Execute("TreePoint");
        Assert.True(rig.Vm.IsDecorationTreeValues);
        rig.Vm.DecorationSpecies = "conifer";
        rig.Vm.DecorationHeightText = "12";
        rig.DecoClick(5, 5);
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 1, "나무 저장");
        var tree = rig.Writes.Single().Body!;
        Assert.Equal(new[] { "kind", "geometry", "props" }, tree.Properties().Select(p => p.Name));
        Assert.Equal(("tree", "Point"), (tree.Value<string>("kind"), tree["geometry"]!.Value<string>("type")));
        Assert.Equal(("conifer", 12.0), (tree["props"]!.Value<string>("species"), tree["props"]!.Value<double>("height_m")));
        Assert.Single(rig.Sent("decorations.reload"));
        Assert.Equal("나무 한 그루 · 침엽수", rig.Vm.DecorationItems[0].Text);

        // 담장 = 두 점 + Enter
        rig.Vm.SelectDecorationToolCommand.Execute("Wall");
        rig.DecoClick(-20, -10);
        Assert.Equal("점 1개 — 2개 이상 찍으세요", rig.Vm.DecorationStatusText);
        Assert.True(rig.DecoKey(Key.Enter));
        Assert.Equal("점이 2개 이상 있어야 선이 됩니다.", rig.Vm.DecorationError);
        rig.DecoClick(20, -10);
        Assert.True(rig.Vm.IsDecorationFinishVisible);
        Assert.True(rig.DecoKey(Key.Enter));
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 2, "담장 저장");
        var wall = rig.Writes.Last().Body!;
        Assert.Equal(("wall", "LineString", 2), (wall.Value<string>("kind"), wall["geometry"]!.Value<string>("type"), ((JArray)wall["geometry"]!["coordinates"]!).Count));
        Assert.Equal(("fence", 2.0), (wall["props"]!.Value<string>("type"), wall["props"]!.Value<double>("height_m")));

        // Ctrl+Z = 담장 지우기, Ctrl+Y = 다시
        Assert.True(rig.DecoKey(Key.Z, ModifierKeys.Control));
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 1, "되돌리기");
        Assert.Equal(HttpMethod.Delete, rig.Writes.Last().Method);
        Assert.True(rig.DecoKey(Key.Y, ModifierKeys.Control));
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 2, "다시 하기");
        Assert.Equal(HttpMethod.Put, rig.Writes.Last().Method);

        // Esc = 도구 내려놓기 → 나무 점을 눌러 고르기 → 3D 강조
        Assert.True(rig.DecoKey(Key.Escape));
        Assert.Null(rig.Vm.DecorationTool);
        rig.DecoClick(5, 5);
        Assert.Equal(rig.Vm.DecorationItems[0].Uid, rig.Vm.DecorationSelectedUid);
        Assert.Equal(rig.Vm.DecorationSelectedUid, rig.Sent("decoration.select").Last().Value<string>("uid"));
        Assert.Equal("conifer", rig.Vm.DecorationSpecies);
        // Delete = 고른 항목 지우기
        Assert.True(rig.DecoKey(Key.Delete));
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 1 && rig.Vm.DecorationSelectedUid == null, "지우기");
        // Esc 단계 끝 = 꾸미기 끝내기
        Assert.True(rig.DecoKey(Key.Escape));
        Assert.False(rig.Vm.IsDecorating);
        Assert.False((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
    });

    [Fact]
    public void should_turn_prop_with_panel_keys_and_apply_then_replace_it() => RunAsync(async rig =>
    {
        rig.Vm.DecorateCommand.Execute(null);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/decorations"), "목록");
        rig.Vm.SelectDecorationToolCommand.Execute("Prop");
        rig.Vm.DecorationPropType = "container";
        rig.DecoClick(0, 0);
        await Rig.Until(() => rig.Vm.DecorationItems.Count == 1, "소품 저장");
        Assert.Equal("container", rig.Writes[0].Body!["props"]!.Value<string>("type"));
        rig.Vm.SelectDecorationToolCommand.Execute("Prop");                              // 같은 도구 한 번 더 = 고르기로
        Assert.Null(rig.Vm.DecorationTool);
        rig.Vm.DecorationSelectedUid = rig.Vm.DecorationItems[0].Uid;                     // 목록에서 고름
        var host = rig.Vm.DecorationPanel.Host!;                                          // 판 안 ↑/↓ 가 부르는 곳
        Assert.True(host.StepHeightByKey(up: false, shift: false));                       // ↓ = 왼쪽 15°
        Assert.Equal("345", rig.Vm.DecorationHeadingText);
        Assert.True(host.StepHeightByKey(up: true, shift: true));                         // Shift+↑ = 1°
        Assert.Equal("346", rig.Vm.DecorationHeadingText);
        rig.Vm.DecorationScaleText = "5";
        Assert.Equal("크기는 0.5 ~ 3 사이로 넣어 주세요.", rig.Vm.DecorationError);
        Assert.False(rig.Vm.ApplyDecorationCommand.CanExecute(null));
        rig.Vm.DecorationScaleText = "2";
        rig.Vm.DecorationName = "정문 컨테이너";
        rig.Vm.ApplyDecorationCommand.Execute(null);
        await Rig.Until(() => rig.Writes.Count == 2, "적용");
        Assert.Equal((346.0, 2.0, "정문 컨테이너"), (rig.Writes[1].Body!["props"]!.Value<double>("heading_deg"), rig.Writes[1].Body!["props"]!.Value<double>("scale"),
                                                    rig.Writes[1].Body!.Value<string>("name")));
        // [다시 놓기] = 같은 uid 로 새 자리
        var uid = rig.Vm.DecorationSelectedUid!;
        rig.Vm.ReplaceDecorationCommand.Execute(null);
        Assert.Equal("지도를 눌러 새 자리에 놓으세요", rig.Vm.DecorationStatusText);
        rig.DecoClick(30, 30);
        await Rig.Until(() => rig.Writes.Count == 3, "다시 놓기");
        Assert.EndsWith(uid, rig.Writes[2].Path);
        Assert.Equal(uid, rig.Vm.DecorationSelectedUid);
        Assert.Single(rig.Vm.DecorationItems);
    });

    [Fact]
    public void should_say_old_engine_and_disable_tools_when_decorations_route_is_missing() => RunAsync(async rig =>
    {
        rig.Server.OldEngine = true;
        rig.Vm.DecorateCommand.Execute(null);
        await Rig.Until(() => rig.Vm.IsDecorationUnsupported, "옛 엔진 판정");
        Assert.Equal("엔진이 옛 판이라 꾸미기를 쓸 수 없습니다", rig.Vm.DecorationUnsupportedText);
        Assert.False(rig.Vm.SelectDecorationToolCommand.CanExecute("TreePoint"));
        rig.Vm.SelectDecorationToolCommand.Execute("TreePoint");
        rig.DecoClick(0, 0);
        Assert.Empty(rig.Writes);
    });

    [Fact]
    public void should_select_decoration_picked_in_3d_in_panel_over_3d() => RunAsync(async rig =>
    {
        rig.Server.Decorations.Add(new JObject
        {
            ["uid"] = Uid, ["kind"] = "ground", ["props"] = new JObject { ["surface"] = "water" },
            ["geometry"] = new JObject { ["type"] = "Polygon", ["coordinates"] = new JArray((object)new JArray(new JArray(rig.Center.Lng, rig.Center.Lat),
                new JArray(rig.Center.Lng + 0.0002, rig.Center.Lat), new JArray(rig.Center.Lng + 0.0002, rig.Center.Lat + 0.0002), new JArray(rig.Center.Lng, rig.Center.Lat))) },
        });
        rig.Enter3D();
        rig.From3D(new JObject { ["type"] = "decoration.pick", ["uid"] = Uid });
        Assert.True(rig.Vm.IsDecorating);
        Assert.Equal("3D", rig.Vm.Map3DStatusText);                                      // 3D 에 머문다(edit3d-contract 1단계)
        await Rig.Until(() => rig.Vm.DecorationSelectedUid == Uid, "그 항목 고름");
        Assert.True(rig.Vm.IsDecorationGroundValues);
        Assert.Equal("water", rig.Vm.DecorationSurface);
        Assert.Equal("꾸미기 · 바닥 · 물", rig.Vm.DecorationTitle);
    });

    // ─────────────── 판 템플릿(실제로 그려 아이콘 · 바인딩 · AutomationId 확인) ───────────────

    [Fact]
    public void should_render_decoration_height_and_user_building_panels_with_ids_and_tokens() => RunAsync(async rig =>
    {
        rig.Vm.DecorateCommand.Execute(null);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/decorations"), "목록");
        rig.Vm.SelectDecorationToolCommand.Execute("Prop");
        AssertRendered(rig.Vm.DecorationPanel, "DecorationPanelStyle.xaml", "Map.Decoration.Tool.TreePoint", "Map.Decoration.Tool.TreeLine", "Map.Decoration.Tool.TreeArea",
                       "Map.Decoration.Tool.Ground", "Map.Decoration.Tool.Prop", "Map.Decoration.Tool.Wall", "Map.Decoration.PropType", "Map.Decoration.Heading",
                       "Map.Decoration.Apply", "Map.Decoration.Replace", "Map.Decoration.Delete", "Map.Decoration.ClearAll", "Map.Decoration.Undo",
                       "Map.Decoration.Redo", "Map.Decoration.List", "Map.Decoration.Close");
        AssertRendered(rig.Vm.BuildingHeightPanel, "BuildingHeightPanelStyle.xaml", "Map.BuildingHeight.Facade", "Map.BuildingHeight.WallColor",
                       "Map.BuildingHeight.RoofColor", "Map.BuildingHeight.ApplyLook", "Map.BuildingHeight.Hide", "Map.BuildingHeight.HiddenCount",
                       "Map.BuildingHeight.RestoreAll", "Map.BuildingHeight.HiddenList", "Map.BuildingHeight.HideAndRedraw",
                       "Map.BuildingHeight.HideSelected", "Map.BuildingHeight.HideAllAuto", "Map.BuildingHeight.MultiSelectText");
        AssertRendered(rig.Vm.UserBuildingPanel, "UserBuildingPanelStyle.xaml", "Map.UserBuilding.Facade", "Map.UserBuilding.WallColor", "Map.UserBuilding.RoofColor",
                       "Map.UserBuilding.Taper", "Map.UserBuilding.TaperDeg", "Map.UserBuilding.TaperReset", "Map.UserBuilding.TaperStatus");
        var view = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"Map.Decoration.Start\"", view);
        Assert.Contains("{Binding DecorationPanel}", view);
        Assert.NotNull(Ironwall.Dotnet.Libraries.GMaps.Ui.Help.MapsHelp.Entries.SingleOrDefault(e => e.Key == "Map.Decoration.Panel"));
    });

    private static void AssertRendered(System.Windows.Controls.Control panel, string styleFile, params string[] ids)
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Ironwall.Dotnet.Libraries.GMaps.Ui", "Themes", styleFile));
        Assert.DoesNotContain("x:Name", xaml);
        Assert.DoesNotContain("#", xaml.Split("<Style TargetType=\"{x:Type ctrl:")[1].Replace("✓", ""));   // 색은 토큰만
        Render(panel, styleFile);
        var found = new HashSet<string>();
        void Walk(DependencyObject d)
        {
            if (AutomationProperties.GetAutomationId(d) is { Length: > 0 } id) found.Add(id);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Walk(VisualTreeHelper.GetChild(d, i));
        }
        Walk(panel);
        foreach (var id in ids) Assert.Contains(id, found);
    }

    /// <summary>판에 실제 테마 템플릿을 입혀 그린다(틀린 아이콘 이름 · 없는 리소스면 여기서 예외) — 바인딩이 살아 있는 콤보 · 단추를 시험이 직접 만진다.</summary>
    private static void Render(System.Windows.Controls.Control panel, string styleFile)
    {
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{styleFile}", UriKind.Relative) });
        panel.Style = (Style)panel.Resources[panel.GetType()];
        Scenario.Layout(panel, 320, 1400);
    }
}
