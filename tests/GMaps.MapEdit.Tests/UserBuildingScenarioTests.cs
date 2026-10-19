using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Automation;
using System.Windows.Documents;
using System.Windows.Input;
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
/// 「건물 그리기」(직접 그린 건물, 2026-10-19) — 창 · WebView2 없이 실제 MapViewModel 경로로(실제 GMap 화면 ↔ 위경도 · 회전 포함):
/// 들어가기(툴바 · 영역 메뉴) · 권한 · 영역 밖 · 지도 표면 입력(IUserBuildingDrawHost) → 그리기 상태 기계 · 판(층수 · 높이 · 손잡이 · 키) ·
/// 저장 요청 형식(가짜 map-3d) · 3D 상자 미리보기 메시지 · GIS 편집 기록 되돌리기 · 고치기 · 지우기 · Esc 두 단계 · 모두 지우기 확인 ·
/// 3D 진입 막기 · 영역이 사라짐 · 서버 실패 · 판 템플릿(AutomationId · x:Name 없음).
/// </summary>
public class UserBuildingScenarioTests
{
    private const string Job = "j-ub";

    // ─────────────── 장치 ───────────────

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, JObject? Body)> Requests { get; } = new();
        public List<JObject> Items { get; } = new();
        public double[] Bbox { get; set; } = Array.Empty<double>();
        public HttpStatusCode? FailPutWith { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? text = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method, path, text == null ? null : JObject.Parse(text)));
            if (path == "/api/jobs" && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, new JObject
                {
                    ["scenes"] = new JArray(new JObject { ["id"] = Job, ["name"] = "시험 영역", ["bbox"] = new JArray(Bbox.Cast<object>().ToArray()),
                                                          ["user_buildings"] = Items.Count }),
                }.ToString());
            if (path.StartsWith($"/api/jobs/{Job}/buildings/"))   // 3D 「건물 높이」 판 — 다시 만든 직접 그린 건물(서버가 user-buildings 파일을 고친다)
            {
                int id = int.Parse(path.Split('/')[^2]);
                double h = request.Method == HttpMethod.Put ? JObject.Parse(text!).Value<double>("height") : 12.0;
                return Json(HttpStatusCode.OK, new JObject { ["building"] = new JObject { ["id"] = id, ["height"] = h, ["origin"] = "user",
                    ["height_source"] = "user", ["height_from"] = "user", ["user_uid"] = "0123456789ab", ["center"] = new JArray(127.0, 37.5) } }.ToString());
            }
            if (!path.StartsWith($"/api/jobs/{Job}/user-buildings")) return Json(HttpStatusCode.NotFound, "{\"detail\":\"없음\"}");
            string? uid = path.Split('/').Length == 6 ? path.Split('/')[5] : null;
            if (request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, new JObject { ["items"] = new JArray(Items), ["count"] = Items.Count }.ToString());
            if (request.Method == HttpMethod.Put)
            {
                if (FailPutWith is { } code) return Json(code, "{\"detail\":\"윤곽이 이 3D 영역 밖에 있습니다\"}");
                var body = JObject.Parse(text!);
                var item = new JObject { ["uid"] = uid, ["outline"] = body["outline"], ["height"] = body["height"], ["floors"] = body["floors"],
                                         ["name"] = body["name"], ["baked_id"] = null };
                Items.RemoveAll(i => i.Value<string>("uid") == uid);
                Items.Add(item);
                return Json(HttpStatusCode.OK, new JObject { ["item"] = item, ["count"] = Items.Count }.ToString());
            }
            if (uid == null)
            {
                int n = Items.Count;
                Items.Clear();
                return Json(HttpStatusCode.OK, $"{{\"deleted\":{n},\"count\":0}}");
            }
            int removed = Items.RemoveAll(i => i.Value<string>("uid") == uid);
            return removed == 0 ? Json(HttpStatusCode.NotFound, "{\"detail\":\"그 건물이 없습니다\"}") : Json(HttpStatusCode.OK, "{\"deleted\":1,\"count\":0}");
        }

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
            Scenario.SetField(Vm, "_isEditModeEnabled", false);
            W.Map.SetEditMode(false);
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
            Areas();
        }

        public void Areas(int userBuildings = 0)
            => Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)new[]
            {
                new Map3DSceneInfo(Job, "시험 영역", Server.Bbox, null, 2, null) { UserBuildings = userBuildings },
            });

        public Map3DAreaItem Area => Vm.Map3DAreas.Single(a => a.Id == Job);

        /// <summary>그 장면을 미리 불러 둔 3D 로 「3D 로 보기」 → 펼침(재사용 경로 · WebView2 없음 — 「건물 높이」 시험과 같은 장치).</summary>
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
        public IUserBuildingDrawHost Host => Vm;

        /// <summary>위경도(지금 지도 중심에서 dx · dy m) → 지금 지도 화면 점.</summary>
        public UbPoint ScreenOf(double eastM, double northM)
        {
            var g = Geo(eastM, northM);
            var l = W.Map.FromLatLngToLocal(new PointLatLng(g.Y, g.X));
            return new UbPoint(l.X, l.Y);
        }

        public UbPoint Geo(double eastM, double northM)
            => new(Center.Lng + eastM / (111320.0 * Math.Cos(Center.Lat * Math.PI / 180)), Center.Lat + northM / 110540.0);

        public void Click(UbPoint s) { Host.UserBuildingPointerDown(s, 1); Host.UserBuildingPointerUp(s, false); }
        public void Click(double eastM, double northM) => Click(ScreenOf(eastM, northM));

        /// <summary>약 20 × 16 m 네모를 찍고 첫 점을 눌러 닫는다.</summary>
        public void DrawSquare(double e0 = -10, double n0 = 8, double w = 20, double h = 16)
        {
            Click(e0, n0); Click(e0 + w, n0); Click(e0 + w, n0 - h); Click(e0, n0 - h);
            Click(e0, n0);
        }

        public bool Key(Key key, ModifierKeys mods = ModifierKeys.None, bool inBox = false)
            => (bool)Scenario.Call(Vm, "HandleUserBuildingKey", key, mods, inBox)!;

        public Task Apply() => (Task)Scenario.Call(Vm, "ApplyUserBuildingAsync")!;

        public List<(HttpMethod Method, string Path, JObject? Body)> Writes
            => Server.Requests.Where(r => r.Method != HttpMethod.Get).ToList();

        public List<JObject> Sent(string type) => Posts.Where(p => p.Value<string>("type") == type).ToList();

        public bool HasSurface => AdornerLayer.GetAdornerLayer(W.Map)?.GetAdorners(W.Map)?.OfType<UserBuildingAdorner>().Any() == true;

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

    private static void Run(Action<Rig> body, bool canEdit = true) => Scenario.Sta(() => { var rig = new Rig(canEdit); body(rig); rig.Done(); });

    private static void RunAsync(Func<Rig, Task> body, bool canEdit = true)
        => Scenario.StaAsync(async () => { var rig = new Rig(canEdit); await body(rig); rig.Done(); });

    // ─────────────── 1. 들어가기 · 권한 · 영역 ───────────────

    [Fact]
    public void should_start_from_toolbar_in_center_area_and_block_3d_and_attach_surface() => RunAsync(async rig =>
    {
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal(Job, rig.Vm.UserBuildingArea!.Id);
        Assert.True(rig.HasSurface);
        Assert.True((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);   // 그리는 동안 3D 에 들어가지 않는다
        Assert.Equal("Map.UserBuilding.Panel", AutomationProperties.GetAutomationId(rig.Vm.UserBuildingPanel));
        Assert.Equal("지도를 눌러 첫 꼭짓점을 찍으세요", rig.Vm.UserBuildingPointCountText);
        Assert.Equal("9.0", rig.Vm.UserBuildingHeightText);
        Assert.Equal("3", rig.Vm.UserBuildingFloorsText);
        await Rig.Until(() => rig.Server.Requests.Any(r => r.Path == $"/api/jobs/{Job}/user-buildings"), "저장된 건물 목록 요청");
        // 툴바 다시 누르기 = 끝내기
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.HasSurface);
        Assert.False((bool)Scenario.Call(rig.Vm, "IsMap3DBlockedByInputMode")!);
    });

    [Fact]
    public void should_refuse_start_without_map_edit_permission() => Run(rig =>
    {
        rig.Area.Actions.Single(a => a.Header == "건물 그리기").Command.Execute(null);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.HasSurface);
        Assert.Empty(rig.Server.Requests);
        Assert.NotEmpty(rig.W.Published);   // 권한 없음 안내
    }, canEdit: false);

    [Fact]
    public void should_not_start_when_center_is_outside_every_area() => Run(rig =>
    {
        rig.W.Map.Position = new PointLatLng(rig.Center.Lat + 0.01, rig.Center.Lng);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
    });

    [Fact]
    public void should_offer_draw_and_clear_all_actions_in_area_menu() => Run(rig =>
    {
        var draw = rig.Area.Actions.Single(a => a.Header == "건물 그리기");
        Assert.Equal($"Map.UserBuilding.Draw.{Job}", draw.AutomationId);
        Assert.Equal("이 영역 3D 로 보기", rig.Area.Actions[0].Header);       // 예전 순서 그대로
        Assert.DoesNotContain(rig.Area.Actions, a => a.Header.Contains("모두 지우기"));
        rig.Areas(userBuildings: 2);
        Assert.Contains("직접 그린 건물 2개", rig.Area.MenuHeader);
        Assert.Contains("직접 그린 건물 2개", rig.Area.ToolTip);
        Assert.Contains(rig.Area.Actions, a => a.Header == "직접 그린 건물 2개 — 모두 지우기…");
        draw = rig.Area.Actions.Single(a => a.Header == "건물 그리기");
        draw.Command.Execute(null);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
    });

    // ─────────────── 2. 그리기 · 저장 · 미리보기 ───────────────

    [Fact]
    public void should_put_outline_height_floors_and_post_box_preview_when_saved() => RunAsync(async rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.False(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));      // 닫기 전에는 저장 못 함
        rig.DrawSquare();
        Assert.True(rig.Vm.UserBuildingDraft!.IsClosed);
        Assert.Equal("꼭짓점 4개 · 닫힘", rig.Vm.UserBuildingPointCountText);
        Assert.Contains("바닥 3", rig.Vm.UserBuildingAreaText);              // 약 320 m²
        rig.Vm.UserBuildingFloorsText = "5";
        Assert.Equal("15.0", rig.Vm.UserBuildingHeightText);
        rig.Vm.UserBuildingName = " 경비동 ";
        Assert.Contains("15.0 m(5층)", rig.Vm.UserBuildingSummary);
        Assert.True(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));
        await rig.Apply();

        var put = Assert.Single(rig.Writes);
        Assert.Equal(HttpMethod.Put, put.Method);
        Assert.Matches($"^/api/jobs/{Job}/user-buildings/[0-9a-f]{{12}}$", put.Path);
        var outline = (JArray)put.Body!["outline"]!;
        Assert.Equal(4, outline.Count);
        var want = new[] { rig.Geo(-10, 8), rig.Geo(10, 8), rig.Geo(10, -8), rig.Geo(-10, -8) };
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(want[i].X, outline[i]![0]!.Value<double>(), 4);   // 화면 1 px 반올림 안(줌 18 ≈ 0.6 m)
            Assert.Equal(want[i].Y, outline[i]![1]!.Value<double>(), 4);
        }
        Assert.Equal(15.0, put.Body.Value<double>("height"));
        Assert.Equal(5, put.Body.Value<int>("floors"));
        Assert.Equal("경비동", put.Body.Value<string>("name"));
        var box = Assert.Single(rig.Sent("userbuilding.preview"));
        Assert.Equal(put.Path.Split('/')[^1], box.Value<string>("uid"));
        Assert.Equal(15.0, box.Value<double>("height"));
        Assert.Equal(4, ((JArray)box["outline"]!).Count);
        Assert.StartsWith(Map3DUserBuilding.UndoPrefix, rig.W.Undo.NextUndoDescription);
        Assert.Empty(rig.Vm.UserBuildingDraft!.Points);                     // 다음 건물을 바로 그릴 수 있게
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        await Rig.Until(() => rig.Vm.UserBuildingItems.Count == 1, "저장 뒤 목록 다시 받음");
        await Rig.Until(() => rig.Area.Scene.UserBuildings == 1, "영역 목록 「직접 그린 건물 1개」");
    });

    [Fact]
    public void should_draw_correct_geo_outline_on_rotated_map() => RunAsync(async rig =>
    {
        rig.W.Map.MapRotation = 35;
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        Assert.True(rig.Vm.UserBuildingDraft!.IsClosed);
        await rig.Apply();
        var outline = (JArray)rig.Writes.Single().Body!["outline"]!;
        var want = new[] { rig.Geo(-10, 8), rig.Geo(10, 8), rig.Geo(10, -8), rig.Geo(-10, -8) };
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(want[i].X, outline[i]![0]!.Value<double>(), 4);
            Assert.Equal(want[i].Y, outline[i]![1]!.Value<double>(), 4);
        }
    });

    [Fact]
    public void should_refuse_points_outside_area_and_show_reason() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Click(-10, 8);
        rig.Click(400, 8);                                                    // 영역(±170 m) 밖
        Assert.Single(rig.Vm.UserBuildingDraft!.Points);
        Assert.Equal(Map3DUserBuilding.OutsideText, rig.Vm.UserBuildingError);
        Assert.True(rig.Vm.HasUserBuildingError);
    });

    [Fact]
    public void should_keep_draft_and_show_server_reason_when_put_fails() => RunAsync(async rig =>
    {
        rig.Server.FailPutWith = HttpStatusCode.BadRequest;
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        await rig.Apply();
        Assert.Equal("저장하지 못했습니다: 윤곽이 이 3D 영역 밖에 있습니다", rig.Vm.UserBuildingError);
        Assert.Equal(4, rig.Vm.UserBuildingDraft!.Points.Count);
        Assert.Empty(rig.Sent("userbuilding.preview"));
        Assert.False(rig.W.Undo.CanUndo);
    });

    [Fact]
    public void should_block_apply_on_bad_height_input_and_step_floors_and_drag_handle() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        rig.Vm.UserBuildingHeightText = "abc";
        Assert.Equal("높이는 숫자로 넣어 주세요(예: 21.5).", rig.Vm.UserBuildingError);
        Assert.False(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));
        rig.Vm.UserBuildingHeightText = "21.54";
        Assert.Equal(21.5, rig.Vm.UserBuildingHeight);
        Assert.True(rig.Vm.ApplyUserBuildingCommand.CanExecute(null));
        rig.Vm.StepUserBuildingFloorCommand.Execute("1");
        Assert.Equal(24.0, rig.Vm.UserBuildingHeight);                       // 7층 → 8층
        rig.Vm.UserBuildingFloorHeightText = "4.0";
        Assert.Equal("6", rig.Vm.UserBuildingFloorsText);                    // 높이 그대로, 층수만
        var host = rig.Vm.UserBuildingPanel.Host!;
        host.BeginHeightDrag();
        host.DragHeight(-100);                                               // 위로 100 px = +10 m
        Assert.Equal(34.0, rig.Vm.UserBuildingHeight);
        host.EndHeightDrag(canceled: true);                                  // Esc = 끌기 전으로
        Assert.Equal(24.0, rig.Vm.UserBuildingHeight);
        Assert.True(host.StepHeightByKey(up: false, shift: true));
        Assert.Equal(23.9, rig.Vm.UserBuildingHeight);
    });

    // ─────────────── 2-1. 형상 도구 · 지붕 · 외벽 ───────────────

    /// <summary>지도 표면 끌기(누르기 → 두 번 움직임 → 놓기).</summary>
    private static void DragOnMap(Rig rig, UbPoint from, UbPoint to, bool shift = false)
    {
        rig.Host.UserBuildingPointerDown(from, 1);
        rig.Host.UserBuildingPointerMove(new UbPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2), shift);
        rig.Host.UserBuildingPointerMove(to, shift);
        rig.Host.UserBuildingPointerUp(to, shift);
    }

    [Fact]
    public void should_draw_circle_suggest_dome_and_put_roof_and_use() => RunAsync(async rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.True(rig.Vm.IsUserBuildingToolPolygon);                       // 기본 도구 = 다각형
        rig.Vm.SelectUserBuildingToolCommand.Execute("Circle");
        Assert.True(rig.Vm.IsUserBuildingToolCircle);
        Assert.False(rig.Vm.IsUserBuildingToolPolygon);
        rig.Vm.UserBuildingCircleSidesText = "24";
        var c = rig.ScreenOf(0, 0);
        DragOnMap(rig, c, rig.ScreenOf(12, 0));                               // 반지름 약 12 m
        var d = rig.Vm.UserBuildingDraft!;
        Assert.True(d.IsClosed);
        Assert.Equal(24, d.Points.Count);
        Assert.Equal(Map3DUserBuilding.RoofDome, rig.Vm.UserBuildingRoof);  // 원 → 돔 제안
        rig.Vm.UserBuildingUse = Map3DUserBuilding.UseIndustrial;
        await rig.Apply();
        var put = rig.Writes.Single();
        Assert.Equal(("dome", "industrial"), (put.Body!.Value<string>("roof"), put.Body.Value<string>("use")));
        Assert.Equal(24, ((JArray)put.Body["outline"]!).Count);
        // 다음 건물은 같은 도구 · 기본 지붕
        Assert.True(rig.Vm.IsUserBuildingToolCircle);
        Assert.Equal(Map3DUserBuilding.RoofFlat, rig.Vm.UserBuildingRoof);
        rig.Vm.UserBuildingCircleSidesText = "3";                              // 범위 밖 → 이유 한 줄, 값 그대로
        Assert.Equal("24", rig.Vm.UserBuildingCircleSidesText);
        Assert.True(rig.Vm.HasUserBuildingError);
    });

    [Fact]
    public void should_keep_user_chosen_roof_when_circle_completes() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Circle");
        rig.Vm.UserBuildingRoof = Map3DUserBuilding.RoofGable;                // 먼저 고른 지붕은 덮지 않는다
        DragOnMap(rig, rig.ScreenOf(0, 0), rig.ScreenOf(10, 0));
        Assert.Equal(Map3DUserBuilding.RoofGable, rig.Vm.UserBuildingRoof);
    });

    [Fact]
    public void should_draw_screen_aligned_rectangle_on_rotated_map_and_square_with_shift() => RunAsync(async rig =>
    {
        rig.W.Map.MapRotation = 30;
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Rect");
        var a = rig.ScreenOf(-15, 10);
        DragOnMap(rig, a, new UbPoint(a.X + 40, a.Y + 25), shift: true);      // Shift = 정사각형(40 px)
        var d = rig.Vm.UserBuildingDraft!;
        Assert.True(d.IsClosed);
        var s = d.Points.Select(g => rig.W.Map.FromLatLngToLocal(new PointLatLng(g.Y, g.X))).ToList();
        Assert.InRange(s[1].Y - s[0].Y, -1, 1);                              // 돌린 지도에서도 화면에 반듯(1 px 반올림 안)
        Assert.InRange(s[1].X - s[0].X, 39, 41);
        Assert.InRange(s[2].Y - s[1].Y, 39, 41);
        await rig.Apply();
        Assert.Equal(4, ((JArray)rig.Writes.Single().Body!["outline"]!).Count);
    });

    [Fact]
    public void should_restart_with_new_tool_when_tool_changes_mid_drawing() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Click(-10, 8); rig.Click(10, 8);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Ellipse");
        Assert.Empty(rig.Vm.UserBuildingDraft!.Points);                      // 그리던 다각형은 버리고
        Assert.Equal(UbTool.Ellipse, rig.Vm.UserBuildingDraft.Tool);         // 새 도구로
        rig.Click(0, 0); rig.Click(20, 0); rig.Click(0, 8);                    // 가운데 · 긴 축 · 짧은 축
        Assert.True(rig.Vm.UserBuildingDraft.IsClosed);
        Assert.Equal(UbTool.Ellipse, rig.Vm.UserBuildingDraft.CompletedBy);
    });

    [Fact]
    public void should_pick_tools_with_r_c_e_p_keys_on_panel_but_not_in_text_box() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        var panel = rig.Vm.UserBuildingPanel;
        Assert.Equal("Rect", UserBuildingPanelControl.ToolForKey(Key.R));
        Assert.Null(UserBuildingPanelControl.ToolForKey(Key.X));
        bool Press(Key key, object source)
        {
            var e = new KeyEventArgs(Keyboard.PrimaryDevice, new HeadlessSource { RootVisual = panel }, Environment.TickCount, key)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent, Source = source };
            panel.RaiseEvent(e);
            return e.Handled;
        }
        Assert.True(Press(Key.C, panel));
        Assert.True(rig.Vm.IsUserBuildingToolCircle);
        Assert.True(Press(Key.E, panel));
        Assert.True(rig.Vm.IsUserBuildingToolEllipse);
        Assert.True(Press(Key.R, panel));
        Assert.True(rig.Vm.IsUserBuildingToolRect);
        Assert.False(Press(Key.P, new System.Windows.Controls.TextBox()));   // 입력칸 안 = 글자 그대로
        Assert.True(rig.Vm.IsUserBuildingToolRect);
        Assert.True(Press(Key.P, panel));
        Assert.True(rig.Vm.IsUserBuildingToolPolygon);
    });

    private sealed class HeadlessSource : System.Windows.PresentationSource
    {
        private System.Windows.Media.Visual? _root;
        public override System.Windows.Media.Visual RootVisual { get => _root!; set { var old = _root; _root = value; RootChanged(old!, value); } }
        public override bool IsDisposed => false;
        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
    }

    [Fact]
    public void should_move_and_rotate_whole_outline_on_map() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        var d = rig.Vm.UserBuildingDraft!;
        var before = d.Points.ToArray();
        var inside = rig.ScreenOf(0, 0);
        DragOnMap(rig, inside, new UbPoint(inside.X + 20, inside.Y));         // 몸통 끌기 = 통째로
        Assert.Equal(4, d.Points.Count);
        Assert.All(d.Points.Zip(before), p => Assert.True(p.First.X > p.Second.X));
        var rot = d.RotateHandleScreen()!.Value;
        DragOnMap(rig, rot, new UbPoint(rot.X + 60, rot.Y + 60));            // 회전 손잡이
        Assert.NotEqual(before.Select(p => Math.Round(p.Y, 6)), d.Points.Select(p => Math.Round(p.Y, 6)));
        Assert.Null(d.Validate());
    });

    // ─────────────── 3. 키 · Esc · 되돌리기 ───────────────

    [Fact]
    public void should_escape_in_two_stages_and_close_with_enter_and_edit_with_keys() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Click(-10, 8); rig.Click(10, 8); rig.Click(10, -8);
        Assert.True(rig.Key(Key.Back));                                       // 마지막 점
        Assert.Equal(2, rig.Vm.UserBuildingDraft!.Points.Count);
        rig.Click(10, -8);
        Assert.True(rig.Key(Key.Enter));                                      // 닫기
        Assert.True(rig.Vm.UserBuildingDraft.IsClosed);
        Assert.False(rig.Key(Key.Back, inBox: true));                         // 입력칸 안 Backspace 는 글자 지우기
        Assert.True(rig.Key(Key.Escape));                                     // 닫은 윤곽 → 빈 윤곽
        Assert.Empty(rig.Vm.UserBuildingDraft!.Points);
        rig.Click(-10, 8); rig.Click(10, 8);
        Assert.True(rig.Key(Key.Escape));                                     // 그리던 점 지움
        Assert.Empty(rig.Vm.UserBuildingDraft!.Points);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.True(rig.Key(Key.Escape));                                     // 빈 윤곽 → 그리기 끝
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.HasSurface);
    });

    [Fact]
    public void should_cancel_vertex_drag_with_escape_and_insert_point_by_midpoint_drag() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        var d = rig.Vm.UserBuildingDraft!;
        var before = d.Points.ToArray();
        var a = rig.ScreenOf(10, 8);
        rig.Host.UserBuildingPointerDown(a, 1);
        rig.Host.UserBuildingPointerMove(new UbPoint(a.X + 40, a.Y + 10), false);
        Assert.True(d.IsDragging);
        Assert.True(rig.Key(Key.Escape));
        Assert.Equal(before, d.Points);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        // 선분 가운데 손잡이 끌기 = 점 넣기, 오른쪽 클릭 = 지우기
        var mid = rig.ScreenOf(0, 8);
        rig.Host.UserBuildingPointerDown(mid, 1);
        rig.Host.UserBuildingPointerMove(new UbPoint(mid.X, mid.Y - 20), false);
        rig.Host.UserBuildingPointerUp(new UbPoint(mid.X, mid.Y - 20), false);
        Assert.Equal(5, d.Points.Count);
        rig.Host.UserBuildingRightClick(new UbPoint(mid.X, mid.Y - 20));
        Assert.Equal(4, d.Points.Count);
    });

    [Fact]
    public void should_undo_outline_steps_then_saved_building_with_ctrl_z() => RunAsync(async rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        await rig.Apply();
        string uid = rig.Writes.Single().Path.Split('/')[^1];
        rig.Click(-10, 30); rig.Click(10, 30);
        Assert.True(rig.Key(Key.Z, ModifierKeys.Control));                   // 윤곽 편집부터
        Assert.Single(rig.Vm.UserBuildingDraft!.Points);
        Assert.True(rig.Key(Key.Z, ModifierKeys.Control));
        Assert.Empty(rig.Vm.UserBuildingDraft!.Points);
        Assert.True(rig.Key(Key.Z, ModifierKeys.Control));                   // 그다음은 저장 기록 → 지움
        await Rig.Until(() => rig.Writes.Count == 2, "되돌리기 DELETE");
        Assert.Equal((HttpMethod.Delete, $"/api/jobs/{Job}/user-buildings/{uid}"), (rig.Writes[1].Method, rig.Writes[1].Path));
        await Rig.Until(() => rig.Sent("userbuilding.preview").Count == 2, "상자 지움 메시지");
        Assert.Equal(JTokenType.Null, rig.Sent("userbuilding.preview")[1]["height"]!.Type);
        Assert.Empty(rig.Server.Items);
        Assert.True(rig.W.Undo.CanRedo);
        await rig.W.Undo.RedoAsync();                                         // 다시 하기 = 같은 uid 로 다시 저장
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/user-buildings/{uid}"), (rig.Writes[2].Method, rig.Writes[2].Path));
        Assert.Single(rig.Server.Items);
    });

    [Fact]
    public void should_not_undo_unrelated_edit_record_from_drawing_mode() => Run(rig =>
    {
        rig.W.Undo.Push(new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.ImageCropCommand("다각형으로 자르기", 1, default, default,
                                                                                                       (_, _, _) => throw new InvalidOperationException("되돌리면 안 됨")));
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.True(rig.Key(Key.Z, ModifierKeys.Control));
        Assert.True(rig.W.Undo.CanUndo);                                      // 다른 기능의 기록은 그대로
        Assert.Empty(rig.Writes);
    });

    // ─────────────── 4. 고치기 · 지우기 · 모두 지우기 ───────────────

    private static JObject SavedItem(Rig rig, string uid, double height = 12)
        => new()
        {
            ["uid"] = uid, ["height"] = height, ["floors"] = 4, ["name"] = "창고", ["baked_id"] = 3,
            ["outline"] = new JArray(new[] { rig.Geo(-30, -20), rig.Geo(-10, -20), rig.Geo(-10, -40), rig.Geo(-30, -40) }
                                     .Select(p => (object)new JArray(Math.Round(p.X, 7), Math.Round(p.Y, 7)))),
        };

    [Fact]
    public void should_edit_saved_building_by_clicking_inside_and_put_same_uid() => RunAsync(async rig =>
    {
        rig.Server.Items.Add(SavedItem(rig, "0123456789ab"));
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Vm.UserBuildingItems.Count == 1, "저장된 건물 목록");
        rig.Click(-20, -30);                                                  // 그 안을 누름 → 고치기
        Assert.True(rig.Vm.IsUserBuildingEditingSaved);
        Assert.Equal("건물 고치기 · 창고", rig.Vm.UserBuildingTitle);
        Assert.Equal("12.0", rig.Vm.UserBuildingHeightText);
        Assert.Equal(4, rig.Vm.UserBuildingDraft!.Points.Count);
        Assert.True(rig.Vm.UserBuildingDraft.IsClosed);
        // 꼭짓점 끌기 → 저장 = 같은 uid
        var c = rig.ScreenOf(-10, -20);
        rig.Host.UserBuildingPointerDown(c, 1);
        rig.Host.UserBuildingPointerMove(new UbPoint(c.X + 10, c.Y), false);
        rig.Host.UserBuildingPointerUp(new UbPoint(c.X + 10, c.Y), false);
        rig.Vm.UserBuildingFloorsText = "6";
        await rig.Apply();
        var put = rig.Writes.Single();
        Assert.Equal($"/api/jobs/{Job}/user-buildings/0123456789ab", put.Path);
        Assert.Equal(18.0, put.Body!.Value<double>("height"));
        Assert.Equal("창고", put.Body.Value<string>("name"));
        Assert.Equal($"{Map3DUserBuilding.UndoPrefix} 고치기", rig.W.Undo.NextUndoDescription);
        // 되돌리기 = 앞 모양 · 높이로 다시 저장
        await rig.W.Undo.UndoAsync();
        var back = rig.Writes[^1];
        Assert.Equal(HttpMethod.Put, back.Method);
        Assert.Equal(12.0, back.Body!.Value<double>("height"));
    });

    [Fact]
    public void should_delete_saved_building_and_undo_restores_it() => RunAsync(async rig =>
    {
        rig.Server.Items.Add(SavedItem(rig, "0123456789ab"));
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Vm.UserBuildingItems.Count == 1, "저장된 건물 목록");
        Assert.False(rig.Vm.DeleteUserBuildingCommand.CanExecute(null));    // 새 건물엔 지우기 없음
        rig.Click(-20, -30);
        Assert.True(rig.Vm.DeleteUserBuildingCommand.CanExecute(null));
        rig.Vm.DeleteUserBuildingCommand.Execute(null);
        await Rig.Until(() => rig.Server.Items.Count == 0, "지움");
        Assert.Equal(HttpMethod.Delete, rig.Writes.Single().Method);
        await Rig.Until(() => rig.Sent("userbuilding.preview").Count == 1, "상자 지움");
        Assert.False(rig.Vm.IsUserBuildingEditingSaved);
        await rig.W.Undo.UndoAsync();
        Assert.Single(rig.Server.Items);
        Assert.Equal(12.0, rig.Server.Items[0].Value<double>("height"));
    });

    [Fact]
    public void should_clear_all_after_confirm_and_send_clear_message() => RunAsync(async rig =>
    {
        rig.Server.Items.Add(SavedItem(rig, "0123456789ab"));
        rig.Server.Items.Add(SavedItem(rig, "abcdefabcdef"));
        rig.Areas(userBuildings: 2);
        rig.Area.Actions.Single(a => a.Header.EndsWith("모두 지우기…")).Command.Execute(null);
        await Rig.Until(() => rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Any(), "확인 창");
        var confirm = rig.W.Published.OfType<OpenConfirmPopupMessageModel>().Last();
        Assert.Contains("직접 그린 건물 2개", confirm.Explain);
        Assert.Empty(rig.Writes);                                             // 확인 전에는 지우지 않는다
        await rig.Vm.HandleAsync((CallClearUserBuildingsProcessMessageModel)confirm.MessageModel!, CancellationToken.None);
        Assert.Equal((HttpMethod.Delete, $"/api/jobs/{Job}/user-buildings"), (rig.Writes.Single().Method, rig.Writes.Single().Path));
        Assert.Single(rig.Sent("userbuilding.clear"));
        Assert.Contains(rig.W.Published, m => m is ClosePopupMessageModel);
    });

    // ─────────────── 5. 3D · 영역 사라짐 ───────────────

    [Fact]
    public void should_exit_drawing_when_area_is_retired() => Run(rig =>
    {
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Click(-10, 8);
        Scenario.Call(rig.Vm, "RetireMap3DJob", Job, "area-delete", true);
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.HasSurface);
    });

    [Fact]
    public void should_show_user_drawn_source_when_baked_user_building_is_picked_in_3d() => Run(rig =>
    {
        var pick = Map3DBuilding.Parse(new JObject { ["id"] = 3, ["height"] = 12.0, ["origin"] = "user", ["height_source"] = "user", ["height_from"] = "user" })!;
        Assert.Equal("직접 그린 건물", Map3DBuilding.SourceText(pick.HeightFrom, pick.Origin));
        Assert.False(pick.Manual);                                            // 「자동으로 되돌리기」 없음(자동 높이 없음)
    });

    [Fact]
    public void should_return_to_2d_when_drawing_starts_from_area_menu_in_3d() => Run(rig =>
    {
        rig.Enter3D();
        rig.Area.Actions.Single(a => a.Header == "건물 그리기").Command.Execute(null);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.NotEqual("3D", rig.Vm.Map3DStatusText);                       // 그리기는 2D — 3D 를 접었다
        rig.Vm.EnterMap3DCommand.Execute(null);                               // 그리는 동안 3D 로 들어가지 않는다
        Assert.NotEqual("3D", rig.Vm.Map3DStatusText);
    });

    [Fact]
    public void should_undo_3d_lift_of_drawn_building_to_previous_height_not_auto() => RunAsync(async rig =>
    {
        rig.Enter3D();
        Scenario.Call(rig.Vm, "OnMap3DMessage", Map3DMath.ParseMessage(new JObject { ["type"] = "building", ["id"] = 3, ["origin"] = "user", ["height"] = 12.0,
            ["height_source"] = "user", ["height_from"] = "user", ["levels"] = 4, ["lon"] = 127.0, ["lat"] = 37.5, ["manual"] = false }.ToString()));
        Assert.True(rig.Vm.IsBuildingHeightPanelOpen);
        Assert.Equal("직접 그린 건물", rig.Vm.BuildingHeightSourceText);
        Assert.False(rig.Vm.ResetBuildingHeightCommand.CanExecute(null));    // 자동 높이 없음
        var host = (IBuildingHeightPanelHost)rig.Vm;
        host.BeginHeightDrag();
        host.DragHeight(-60);                                                // 손잡이로 6 m 들어올림
        host.EndHeightDrag(false);
        Assert.Equal(18.0, rig.Vm.BuildingHeightPreview);
        await (Task)Scenario.Call(rig.Vm, "ApplyBuildingHeightAsync")!;
        Assert.Equal((HttpMethod.Put, $"/api/jobs/{Job}/buildings/3/height", 18.0),
                     (rig.Writes[0].Method, rig.Writes[0].Path, rig.Writes[0].Body!.Value<double>("height")));
        await (Task)Scenario.Call(rig.Vm, "UndoBuildingHeightAsync")!;
        Assert.Equal(HttpMethod.Put, rig.Writes[1].Method);                  // DELETE(자동으로)가 아니라 앞 높이로 다시 저장
        Assert.Equal(12.0, rig.Writes[1].Body!.Value<double>("height"));
    });

    // ─────────────── 6. 판 템플릿 ───────────────

    [Fact]
    public void should_expose_automation_ids_without_x_name_in_panel_template()
    {
        var xaml = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Ironwall.Dotnet.Libraries.GMaps.Ui", "Themes", "UserBuildingPanelStyle.xaml"));
        foreach (var id in new[] { "Map.UserBuilding.PointCount", "Map.UserBuilding.Floors", "Map.UserBuilding.Height", "Map.UserBuilding.FloorHeight",
                                   "Map.UserBuilding.Apply", "Map.UserBuilding.Cancel", "Map.UserBuilding.Delete", "Map.UserBuilding.Handle",
                                   "Map.UserBuilding.Name", "Map.UserBuilding.Undo", "Map.UserBuilding.Close", "Map.UserBuilding.Error",
                                   "Map.UserBuilding.Tool.Rect", "Map.UserBuilding.Tool.Circle", "Map.UserBuilding.Tool.Ellipse", "Map.UserBuilding.Tool.Polygon",
                                   "Map.UserBuilding.Roof", "Map.UserBuilding.Use", "Map.UserBuilding.CircleSides" })
            Assert.Contains($"AutomationProperties.AutomationId=\"{id}\"", xaml);
        Assert.DoesNotContain("x:Name", xaml);
        Assert.DoesNotContain("#", xaml.Split("<Style TargetType")[1].Replace("✓", ""));   // 색은 토큰만(하드코딩 색 없음)
        var view = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));
        Assert.Contains("AutomationProperties.AutomationId=\"Map.UserBuilding.Draw\"", view);
        Assert.Contains("{Binding UserBuildingPanel}", view);
    }
}
