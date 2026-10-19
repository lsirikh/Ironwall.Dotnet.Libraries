using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
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
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using K = System.Windows.Input.Key;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 「건물 그리기」 × 2D 편집 복합 시나리오 — 편집 모드에서 레이어 패널 다중 선택 · 묶음 회전 · 그룹 속성이 살아 있는 채 그리기.
/// 키는 제품 창의 순서대로 보낸다: 지도 창 터널 처리기(OnMapPreviewKeyDownForGroup, 초기화 때 구독)가 먼저, 처리 안 했을 때만
/// 그리기 처리기(그리기 시작 때 구독 — 같은 창 PreviewKeyDown, handledEventsToo=false). 가짜 map-3d · 창 없음.
/// </summary>
public class UserBuildingComboScenarioTests
{
    private const string Job = "j-ubc";

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, JObject? Body)> Requests { get; } = new();
        public List<JObject> Items { get; } = new();
        public double[] Bbox { get; set; } = Array.Empty<double>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? text = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method, path, text == null ? null : JObject.Parse(text)));
            if (path == "/api/jobs" && request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, new JObject
                {
                    ["scenes"] = new JArray(new JObject { ["id"] = Job, ["name"] = "시험 영역", ["bbox"] = new JArray(Bbox.Cast<object>().ToArray()), ["user_buildings"] = Items.Count }),
                }.ToString());
            if (!path.StartsWith($"/api/jobs/{Job}/user-buildings")) return Json(HttpStatusCode.NotFound, "{\"detail\":\"없음\"}");
            string? uid = path.Split('/').Length == 6 ? path.Split('/')[5] : null;
            if (request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, new JObject { ["items"] = new JArray(Items), ["count"] = Items.Count }.ToString());
            if (request.Method == HttpMethod.Put)
            {
                var body = JObject.Parse(text!);
                var item = new JObject { ["uid"] = uid, ["outline"] = body["outline"], ["height"] = body["height"], ["floors"] = body["floors"], ["name"] = body["name"], ["baked_id"] = null };
                Items.RemoveAll(i => i.Value<string>("uid") == uid);
                Items.Add(item);
                return Json(HttpStatusCode.OK, new JObject { ["item"] = item, ["count"] = Items.Count }.ToString());
            }
            int removed = Items.RemoveAll(i => i.Value<string>("uid") == uid);
            return removed == 0 ? Json(HttpStatusCode.NotFound, "{\"detail\":\"그 건물이 없습니다\"}") : Json(HttpStatusCode.OK, "{\"deleted\":1,\"count\":0}");
        }

        private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class Rig
    {
        public WiredMapRig W { get; } = WiredMapRig.Create();
        public MapViewModel Vm => W.Vm;
        public FakeServer Server { get; } = new();
        public PointLatLng Center { get; }
        public Dictionary<int, IEditableMarker> M { get; } = new();
        public List<int> Saves { get; } = new();
        private readonly string _zoomFile = Path.Combine(Path.GetTempPath(), $"map3d-areas-{Guid.NewGuid():N}.json");

        public Rig()
        {
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
            Scenario.SetField(Vm, "_map3DBuildingPost", (Action<string>)(_ => { }));
            Scenario.Call(Vm, "ApplyMap3DAreas", (IReadOnlyList<Map3DSceneInfo>)new[] { new Map3DSceneInfo(Job, "시험 영역", Server.Bbox, null, 2, null) });
            W.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
                .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => Saves.Add(m.Id)).ReturnsAsync((IGeometricSymbolModel?)null);
            W.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        /// <summary>지도 가운데 아래쪽(건물 그리는 자리 밖)에 도형 심볼들 + 레이어 패널.</summary>
        public void Symbols(params int[] ids)
        {
            var provider = Scenario.GetField<SymbolProvider>(Vm, "_symbolProvider");
            var c = W.Map.FromLatLngToLocal(W.Map.Position);
            int dx = -150;
            foreach (var id in ids)
            {
                var geo = W.Map.FromLocalToLatLng((int)c.X + dx, (int)c.Y + 250);
                dx += 100;
                var model = new GeometricSymbolModel { Id = id, Title = $"도형-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, ShowShape = true, Visible = true };
                var m = new MarkerFactory(Mock.Of<ILogService>(), W.Devices).CreateMarker(model);
                W.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)m);
                provider.Add(model);
                M[id] = m;
            }
            Scenario.Layout(W.Deco, 1200, 800);
            Scenario.SetField(Vm, "_layerTreeNodes", LayerTreeBuilder.Build(Array.Empty<IMapLayerModel>(), provider));
            Vm.ShowLayerPanel();
            LayerSelection.ExpandToShow(ids.Select(Leaf).ToList());
        }

        public ObservableCollection<LayerTreeNode> Roots => Scenario.GetField<ObservableCollection<LayerTreeNode>>(Vm, "_layerTreeNodes");
        public LayerTreeNode Leaf(int id) => LayerTreeBuilder.Flatten(Roots).Single(n => n.Symbol?.Id == id);
        public void Pick(int id, ModifierKeys mods = ModifierKeys.None) => Vm.LayerPanel!.SelectNode(Leaf(id), mods);
        public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(Vm, "_groupSelection");
        public IUserBuildingDrawHost Host => Vm;

        public UbPoint Geo(double eastM, double northM)
            => new(Center.Lng + eastM / (111320.0 * Math.Cos(Center.Lat * Math.PI / 180)), Center.Lat + northM / 110540.0);
        public UbPoint ScreenOf(double eastM, double northM)
        {
            var g = Geo(eastM, northM);
            var l = W.Map.FromLatLngToLocal(new PointLatLng(g.Y, g.X));
            return new UbPoint(l.X, l.Y);
        }
        public void Click(double eastM, double northM) { var s = ScreenOf(eastM, northM); Host.UserBuildingPointerDown(s, 1); Host.UserBuildingPointerUp(s, false); }
        public void DrawSquare() { Click(-10, 8); Click(10, 8); Click(10, -8); Click(-10, -8); Click(-10, 8); }

        /// <summary>제품 창과 같은 순서로 키 하나 — 지도 터널 처리기 → (처리 안 했으면) 그리기 처리기.</summary>
        public bool Key(K key)
        {
            var e = EditRig.Key(key);
            Scenario.Call(Vm, "OnMapPreviewKeyDownForGroup", W.Map, e);
            if (!e.Handled && Vm.IsUserBuildingDrawing) e.Handled = (bool)Scenario.Call(Vm, "HandleUserBuildingKey", key, ModifierKeys.None, false)!;
            return e.Handled;
        }

        public List<(HttpMethod Method, string Path, JObject? Body)> Writes => Server.Requests.Where(r => r.Method != HttpMethod.Get).ToList();

        public static async Task Until(Func<bool> cond, string what)
        {
            for (int i = 0; i < 300 && !cond(); i++) await Task.Delay(10);
            Assert.True(cond(), what);
        }

        public void Done()
        {
            var errors = W.Errors.Where(e => !(e.StartsWith("[Map3D]") && e.Contains("(격리)"))).ToList();
            Assert.True(errors.Count == 0, string.Join(" | ", errors));
            if (File.Exists(_zoomFile)) File.Delete(_zoomFile);
        }
    }

    // ───────────── B-01 그리기 중 레이어 다중 선택 + Delete ─────────────

    [Fact]
    [Trait("Scenario", "B-01")]
    public void should_remove_selected_vertex_not_symbols_when_delete_is_pressed_while_drawing_with_layer_picked_group() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        rig.Symbols(951, 952);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        rig.DrawSquare();
        rig.Pick(951);                                           // 그리는 동안 레이어 패널에서 두 행(편집 모드 → 지도 묶음)
        rig.Pick(952, ModifierKeys.Control);
        await Task.Delay(30);
        Assert.Equal(2, rig.Group.Selection.Count);
        var s = rig.ScreenOf(10, 8);
        rig.Host.UserBuildingPointerDown(s, 1);                  // 윤곽 꼭짓점 고름
        rig.Host.UserBuildingPointerUp(s, false);
        Assert.Equal(1, rig.Vm.UserBuildingDraft!.SelectedVertex);

        Assert.True(rig.Key(K.Delete));
        await Task.Delay(30);
        Assert.Equal(3, rig.Vm.UserBuildingDraft!.Points.Count); // 그리기 키 = 꼭짓점 지움
        Assert.DoesNotContain(rig.W.Published, m => m is OpenConfirmPopupMessageModel);   // 지도 심볼 삭제 확인 창 없음
        Assert.Contains(rig.M[951], rig.W.Map.Markers.OfType<IEditableMarker>());
        Assert.Equal(2, rig.Group.Selection.Count);
        rig.Done();
    });

    // ───────────── B-02 회전 판 열린 채 「건물 그리기」 ─────────────

    [Fact]
    [Trait("Scenario", "B-02")]
    public void should_confirm_open_group_rotation_before_drawing_starts_when_building_draw_is_pressed_with_rotation_panel_open() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        rig.Symbols(961, 962);
        rig.Pick(961);
        rig.Pick(962, ModifierKeys.Control);
        await Task.Delay(30);
        rig.Vm.StartGroupRotationCommand.Execute(null);
        rig.Vm.GroupRotationAngleText = "25";
        Assert.True(rig.Vm.IsGroupRotationPanelOpen);

        rig.Vm.DrawUserBuildingCommand.Execute(null);            // 다른 조작 = 열린 회전 확정
        await Task.Delay(60);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(25, rig.M[961].Bearing, 9);
        Assert.Equal(new[] { 961, 962 }, rig.Saves.Distinct().OrderBy(x => x));
        Assert.Equal("그룹 회전 2개", rig.W.Undo.NextUndoDescription);

        Assert.True(rig.Key(K.Escape));                          // 이제 Esc 는 그리기 몫(빈 윤곽 → 끝) — 회전을 되돌리지 않는다
        Assert.False(rig.Vm.IsUserBuildingDrawing);
        Assert.Equal(25, rig.M[961].Bearing, 9);
        rig.Done();
    });

    // ───────────── B-03 그리기 중 판에서 회전 시작 → Esc 두 번 ─────────────

    [Fact]
    [Trait("Scenario", "B-03")]
    public void should_cancel_rotation_first_then_clear_outline_points_when_escape_is_pressed_twice_while_drawing_and_rotating() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        rig.Symbols(971, 972);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Click(-10, 8); rig.Click(10, 8);                     // 그리던 점 2개
        rig.Pick(971);
        rig.Pick(972, ModifierKeys.Control);
        await Task.Delay(30);
        rig.Vm.StartGroupRotationCommand.Execute(null);
        rig.Vm.GroupRotationAngleText = "10";

        Assert.True(rig.Key(K.Escape));                          // 1 = 회전 취소(원위치 · 저장 없음)
        Assert.False(rig.Vm.IsGroupRotationPanelOpen);
        Assert.Equal(0, rig.M[971].Bearing, 9);
        Assert.Equal(2, rig.Vm.UserBuildingDraft!.Points.Count);  // 그리던 점은 그대로
        Assert.True(rig.Key(K.Escape));                          // 2 = 그리던 점 지움
        Assert.Empty(rig.Vm.UserBuildingDraft!.Points);
        Assert.True(rig.Vm.IsUserBuildingDrawing);
        Assert.Empty(rig.Saves);
        Assert.False(rig.W.Undo.CanUndo);
        rig.Done();
    });

    // ───────────── B-04 그룹 속성 → 건물 저장 → 전역 Undo 2회 ─────────────

    [Fact]
    [Trait("Scenario", "B-04")]
    public void should_undo_saved_building_then_group_width_when_toolbar_undo_runs_twice_after_both() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        rig.Symbols(981, 982);
        rig.Pick(981);
        rig.Pick(982, ModifierKeys.Control);
        await Task.Delay(30);
        rig.Vm.PropertyPanel!.MarkerWidth = 62;
        await Task.Delay(80);
        Assert.Equal(62, rig.M[981].Width, 9);

        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        await (Task)Scenario.Call(rig.Vm, "ApplyUserBuildingAsync")!;
        Assert.Single(rig.Server.Items);
        Assert.StartsWith(Map3DUserBuilding.UndoPrefix, rig.W.Undo.NextUndoDescription);

        rig.Vm.UndoCommand!.Execute(null);                       // 1 = 방금 저장한 건물 지움
        await Rig.Until(() => rig.Server.Items.Count == 0, "건물 되돌리기 DELETE");
        Assert.Equal(62, rig.M[981].Width, 9);
        rig.Vm.UndoCommand!.Execute(null);                       // 2 = 그룹 너비
        await Task.Delay(80);
        Assert.Equal(40, rig.M[981].Width, 9);
        Assert.Equal(40, rig.M[982].Width, 9);
        Assert.Equal(2, rig.Writes.Count);                       // PUT · DELETE 만(너비 Undo 는 map-3d 에 안 감)
        rig.Done();
    });

    // ───────────── B-05 그리기 중 편집 모드 끄기 · 켜기 + 레이어 선택 ─────────────

    [Fact]
    [Trait("Scenario", "B-05")]
    public void should_keep_outline_and_carry_panel_rows_to_map_when_edit_mode_turns_off_and_on_while_drawing() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        rig.Symbols(991, 992);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.DrawSquare();
        rig.Pick(991);
        rig.Pick(992, ModifierKeys.Control);
        await Task.Delay(30);

        rig.Vm.IsEditModeEnabled = false;
        await Task.Delay(30);
        Assert.False(rig.Group.HasSelection);
        Assert.True(rig.Vm.IsUserBuildingDrawing);               // 그리기는 편집 모드와 무관
        Assert.True(rig.Vm.UserBuildingDraft!.IsClosed);
        rig.Vm.IsEditModeEnabled = true;
        await Task.Delay(30);
        Assert.Equal(2, rig.Group.Selection.Count);              // 패널 선택이 다시 지도로
        Assert.True(rig.Vm.UserBuildingDraft!.IsClosed);

        Assert.True(rig.Key(K.Enter));                           // 닫힌 윤곽 Enter = 저장
        await Rig.Until(() => rig.Server.Items.Count == 1, "Enter 저장");
        Assert.Equal(2, rig.Group.Selection.Count);
        Assert.Empty(rig.Saves);                                 // 심볼은 저장 안 함
        rig.Done();
    });

    // ───────────── B-06 형상 도구(원 · 네모) + 묶음 선택 + Backspace/Delete ─────────────

    [Fact]
    [Trait("Scenario", "B-06")]
    public void should_route_backspace_and_delete_to_shape_tool_outline_and_keep_group_when_group_is_selected() => Scenario.StaAsync(async () =>
    {
        var rig = new Rig();
        rig.Symbols(1001, 1002);
        rig.Pick(1001);
        rig.Pick(1002, ModifierKeys.Control);
        await Task.Delay(30);
        rig.Vm.DrawUserBuildingCommand.Execute(null);
        rig.Vm.SelectUserBuildingToolCommand.Execute("Rect");
        Assert.True(rig.Vm.IsUserBuildingToolRect);
        rig.Click(-10, 8);                                       // 첫 모서리
        Assert.True(rig.Key(K.Back));                            // 형상 도구의 마지막 클릭 지움
        Assert.Empty(rig.Vm.UserBuildingDraft!.Anchors);
        rig.Click(-10, 8); rig.Click(10, -8);                    // 두 번 = 네모 완성
        Assert.True(rig.Vm.UserBuildingDraft!.IsClosed);
        Assert.Equal(4, rig.Vm.UserBuildingDraft.Points.Count);
        var s = rig.ScreenOf(10, -8);
        rig.Host.UserBuildingPointerDown(s, 1);
        rig.Host.UserBuildingPointerUp(s, false);
        Assert.True(rig.Key(K.Delete));
        Assert.Equal(3, rig.Vm.UserBuildingDraft!.Points.Count);
        Assert.DoesNotContain(rig.W.Published, m => m is OpenConfirmPopupMessageModel);
        Assert.Equal(2, rig.Group.Selection.Count);
        rig.Done();
    });
}
