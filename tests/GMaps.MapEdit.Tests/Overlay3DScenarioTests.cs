using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;
using Rig = GMaps.MapEdit.Tests.Map3DEntryScenarioTests.Rig;
using X = GMaps.MapEdit.Tests.Map3DExitInputScenarioTests;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 3D 에서 지도 심볼 고르기 · 고치기(2026-10-22 사용자 보고 — "3D 에서 심볼을 선택하면 Adorner 가 다 안 보이고 사라진다"):
/// 2D 편집 어도너는 3D 화면 밑에 가리므로 GIS 가 고름(overlay.select)과 손잡이(edit.start target kind "overlay")를 3D 에 보내고,
/// 3D 손잡이로 고친 것(edit.commit overlay)을 2D 어도너와 같은 저장 · 되돌리기 경로로 받는다.
/// 창 · WebView2 없이 실제 MapViewModel 경로 + 가짜 3D 메시지(JSON). 3D 로 보낸 것은 _map3DBuildingPost 로 잡는다.
/// 이 파일은 새 API 를 부르지 않는다(JSON · VM 처리기만) — 고치기 전 코드로 돌리면 실패하는 것으로 증명한다.
/// </summary>
public class Overlay3DScenarioTests
{
    // ─────────────── 장치 ───────────────

    private static void RunAsync(Func<Rig, List<JObject>, Task> body) => Scenario.StaAsync(async () =>
    {
        var rig = new Rig(Map3DModel.EntryManual);
        var posts = new List<JObject>();
        Scenario.SetField(rig.Vm, "_map3DBuildingPost", (Action<string>)(json => posts.Add(JObject.Parse(json))));
        await body(rig, posts);
        rig.Done();
    });

    private static void Run(Action<Rig, List<JObject>> body) => RunAsync((rig, posts) => { body(rig, posts); return Task.CompletedTask; });

    private static List<JObject> Of(List<JObject> posts, string type) => posts.Where(p => p.Value<string>("type") == type).ToList();

    private static List<(string, int)> Items(JObject select) => select["items"]!.Select(i => (i.Value<string>("kind")!, i.Value<int>("id"))).ToList();

    private static JObject? LastSelect(List<JObject> posts) => Of(posts, "overlay.select").LastOrDefault();

    /// <summary>실제 팩토리로 만든 도형 심볼(편집 어도너가 붙는 Shape) — 지도에 올린다.</summary>
    private static GMapGeometricMarker Symbol(Rig rig, int id, double dLng = 0, bool locked = false)
    {
        var s = (GMapGeometricMarker)new Ironwall.Dotnet.Libraries.GMaps.Ui.Factories.MarkerFactory(Mock.Of<ILogService>(), rig.W.Devices).CreateMarker(new GeometricSymbolModel
        {
            Id = id, Title = $"초소{id}", Latitude = rig.Center.Lat, Longitude = rig.Center.Lng + dLng, Width = 40, Height = 40, ShowShape = true, IsLocked = locked,
        });
        rig.W.Map.Markers.Add(s);
        Scenario.Layout(rig.W.Deco, 1200, 800);
        return s;
    }

    /// <summary>선 심볼 — 템플릿 없는 Shape(선 템플릿의 DropShadowEffect 가 STA 스레드를 넘으면 깨진다 — GroupRotationScenarioTests 와 같은 이유).</summary>
    private static GMapLineMarker Line(Rig rig, int id)
    {
        double lat = rig.Center.Lat, lng = rig.Center.Lng;
        var line = new GMapLineMarker(Mock.Of<ILogService>(), new LineSymbolModel
        {
            Id = id, Title = "경계선", Latitude = lat, Longitude = lng, StrokeThickness = 2,
            LinePoints = new List<GeoPoint> { new(lat, lng), new(lat, lng + 0.0008), new(lat + 0.0005, lng + 0.0008) },
        });
        line.Shape = new System.Windows.Controls.Border { Width = 40, Height = 40 };
        rig.W.Map.Markers.Add(line);
        return line;
    }

    /// <summary>빌보드 Shape(군대부호 · PIDS 2D 처럼 화면에 늘 서 있음 — 2D 어도너가 돌리기 손잡이를 그리지 않는다).</summary>
    private sealed class BillboardShape : System.Windows.Controls.Border, IMapRotationAwareShape
    {
        public bool AppliesMapRotation => true;
        public bool IsBillboard => true;
        public void OnMapBearingChanged(double mapBearing) { }
    }

    private static void Pick(Rig rig, string kind, int id, bool ctrl = false)
        => X.From3D(rig, new JObject { ["type"] = "pick", ["action"] = "click", ["kind"] = kind, ["id"] = id, ["ctrl"] = ctrl, ["shift"] = false });

    /// <summary>빈 곳 클릭 — 「지도에서 보기」 강조 필드(제품은 필드 초기값 new())를 채운 뒤(Map3DPickScenarioTests 와 같다).</summary>
    private static void PickEmpty(Rig rig)
    {
        foreach (var name in new[] { "_locateHighlight", "_locateRings" })
            if (Scenario.GetField<object?>(rig.Vm, name) == null)
                Scenario.SetField(rig.Vm, name, Activator.CreateInstance(typeof(MapViewModel).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.FieldType));
        X.From3D(rig, new JObject { ["type"] = "pick", ["action"] = "click" });
    }

    private static JObject PointCommit(string kind, int id, double lon, double lat, double? heading = null)
    {
        var o = new JObject { ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "overlay", ["overlayKind"] = kind, ["id"] = id }, ["lon"] = lon, ["lat"] = lat };
        if (heading is double h) o["heading_deg"] = h;
        return o;
    }

    private static void EditOn3D(Rig rig)
    {
        X.Enter(rig);
        rig.Vm.IsEditModeEnabled = true;
        Assert.True(rig.Shown);
    }

    private static GroupSelectionService Group(Rig rig) => Scenario.GetField<GroupSelectionService>(rig.Vm, "_groupSelection");

    private static List<(double lat, double lng)> RecordSaves(Rig rig)
    {
        var saves = new List<(double lat, double lng)>();
        rig.W.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
                .Callback<IGeometricSymbolModel, CancellationToken>((m, _) => saves.Add((m.Latitude, m.Longitude)))
                .ReturnsAsync((IGeometricSymbolModel?)null);
        return saves;
    }

    // ─────────────── 1. 고름 → overlay.select · 손잡이 ───────────────

    [Fact]
    public void should_send_one_overlay_select_and_rotatable_handles_when_symbol_clicked_in_3d_edit_mode() => Run((rig, posts) =>
    {
        EditOn3D(rig);
        var s = Symbol(rig, 9201);
        posts.Clear();

        Pick(rig, "geometric", 9201);
        Scenario.Pump();

        Assert.Same(s, rig.Vm.SelectedMarker);                                         // 2D 클릭과 같은 편집 선택
        var select = Assert.Single(Of(posts, "overlay.select"));                       // 「풀기 → 고르기」 사이의 빈 고름은 보내지 않는다
        Assert.Equal(new[] { ("geometric", 9201) }, Items(select));
        var start = Assert.Single(Of(posts, "edit.start"));
        Assert.Equal(("overlay", "geometric", 9201, true),
                     (start["target"]!.Value<string>("kind"), start["target"]!.Value<string>("overlayKind"), start["target"]!.Value<int>("id"), start["target"]!.Value<bool>("rotatable")));
        Assert.True(rig.Shown, "고르기만으로는 3D 그대로");
    });

    [Fact]
    public void should_add_and_remove_with_ctrl_click_and_clear_on_empty_click_in_3d() => Run((rig, posts) =>
    {
        EditOn3D(rig);
        var a = Symbol(rig, 9211, -0.0002);
        var b = Symbol(rig, 9212, 0.0002);
        Pick(rig, "geometric", 9211);
        Scenario.Pump();
        posts.Clear();

        Pick(rig, "geometric", 9212, ctrl: true);                                       // Ctrl+클릭 = 넣기 → 묶음 2개
        Scenario.Pump();
        Assert.Equal(new[] { a.Id, b.Id }, Group(rig).Selection.Select(m => m.Id).OrderBy(i => i));
        Assert.Equal(new[] { ("geometric", 9211), ("geometric", 9212) }, Items(LastSelect(posts)!).OrderBy(i => i.Item2));
        Assert.Single(Of(posts, "edit.stop"));                                         // 여럿 = 손잡이 없음(고름 표시만)
        Assert.Empty(Of(posts, "edit.start"));

        posts.Clear();
        Pick(rig, "geometric", 9212, ctrl: true);                                       // 다시 Ctrl+클릭 = 빼기 → 하나 남음 = 단일 선택
        Scenario.Pump();
        Assert.Same(a, rig.Vm.SelectedMarker);
        Assert.Equal(new[] { ("geometric", 9211) }, Items(LastSelect(posts)!));
        Assert.Equal(9211, Assert.Single(Of(posts, "edit.start"))["target"]!.Value<int>("id"));

        posts.Clear();
        PickEmpty(rig);                                                                 // 빈 곳 = 2D 빈 곳 클릭처럼 고름 풀기
        Scenario.Pump();
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.Empty(Items(Assert.Single(Of(posts, "overlay.select"))));
        Assert.Single(Of(posts, "edit.stop"));
    });

    [Fact]
    public void should_not_select_or_send_handles_in_view_mode_and_clear_when_edit_mode_turned_off() => Run((rig, posts) =>
    {
        X.Enter(rig);
        Symbol(rig, 9221);
        posts.Clear();
        Pick(rig, "geometric", 9221);                                                   // 보기 전용 — 고르지 않는다(조립 카드 경로)
        Pick(rig, "geometric", 9221, ctrl: true);
        Scenario.Pump();
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.Empty(Of(posts, "edit.start"));
        Assert.All(Of(posts, "overlay.select"), p => Assert.Empty(Items(p)));

        rig.Vm.IsEditModeEnabled = true;
        Pick(rig, "geometric", 9221);
        Scenario.Pump();
        Assert.Single(Of(posts, "edit.start"));
        posts.Clear();
        rig.Vm.IsEditModeEnabled = false;                                               // 끄면 고름 · 손잡이를 내린다
        Scenario.Pump();
        Assert.Empty(Items(Assert.Single(Of(posts, "overlay.select"))));
        Assert.Single(Of(posts, "edit.stop"));
        Assert.True(rig.Shown);
    });

    [Fact]
    public void should_show_only_selection_for_locked_symbol_and_no_rotate_for_billboard() => Run((rig, posts) =>
    {
        EditOn3D(rig);
        var locked = Symbol(rig, 9231, locked: true);
        posts.Clear();
        rig.Vm.SelectedMarker = locked;                                                 // 레이어 판 등에서 고른 잠긴 심볼
        Scenario.Pump();
        Assert.Equal(new[] { ("geometric", 9231) }, Items(LastSelect(posts)!));
        Assert.Empty(Of(posts, "edit.start"));                                          // 잠김 = 손잡이 없음

        var flat = Symbol(rig, 9232, 0.0003);
        flat.Shape = new BillboardShape { Width = 40, Height = 40 };
        posts.Clear();
        rig.Vm.SelectedMarker = flat;
        Scenario.Pump();
        var start = Assert.Single(Of(posts, "edit.start"));
        Assert.False(start["target"]!.Value<bool>("rotatable"), "2D 에서 돌리기 손잡이가 없는 종류는 3D 도 옮기기만");
    });

    [Fact]
    public void should_send_selection_already_made_in_2d_when_entering_3d_and_again_after_reentry() => Run((rig, posts) =>
    {
        var s = Symbol(rig, 9241);
        rig.Vm.IsEditModeEnabled = true;
        rig.Vm.SelectedMarker = s;                                                      // 2D 에서 고른 채
        Scenario.Pump();
        Assert.Empty(posts);                                                            // 2D 동안은 보내지 않는다

        X.Enter(rig);
        Assert.Equal(new[] { ("geometric", 9241) }, Items(Assert.Single(Of(posts, "overlay.select"))));
        Assert.Single(Of(posts, "edit.start"));

        rig.PressExit();
        Assert.False(rig.Shown);
        posts.Clear();
        X.Enter(rig);                                                                   // 접어 둔 페이지는 고름을 기억 — 고름은 그대로, 손잡이만 다시 세운다
        Assert.Empty(Of(posts, "overlay.select"));
        Assert.Single(Of(posts, "edit.start"));

        rig.PressExit();
        rig.Vm.SelectedMarker = null;                                                   // 나가 있는 동안 2D 에서 풀었다
        Scenario.Pump();
        posts.Clear();
        X.Enter(rig);
        Assert.Empty(Items(Assert.Single(Of(posts, "overlay.select"))));               // 3D 에 남은 고름 표시를 지운다
        Assert.Single(Of(posts, "edit.stop"));
    });

    [Fact]
    public void should_send_nothing_to_3d_when_entering_without_selection_and_resend_after_new_page() => Run((rig, posts) =>
    {
        rig.Vm.IsEditModeEnabled = true;
        X.Enter(rig);
        Assert.Empty(posts);                                                            // 고른 것이 없으면 3D 에 아무것도 보내지 않는다(건물 시험의 「보낸 것 없음」 규칙)
        var s = Symbol(rig, 9242);
        rig.Vm.SelectedMarker = s;
        Scenario.Pump();
        Assert.Single(Of(posts, "overlay.select"));
        posts.Clear();

        X.From3D(rig, new JObject { ["type"] = "ready", ["job"] = rig.Job });           // 3D 페이지가 새로 떴다(다시 연결) — 고름 · 손잡이를 모른다
        Assert.Equal(new[] { ("geometric", 9242) }, Items(Assert.Single(Of(posts, "overlay.select"))));
        Assert.Single(Of(posts, "edit.start"));
    });

    [Fact]
    public void should_clear_selection_without_edit_stop_when_3d_ends_handles_by_esc() => Run((rig, posts) =>
    {
        EditOn3D(rig);
        Symbol(rig, 9251);
        Pick(rig, "geometric", 9251);
        Scenario.Pump();
        posts.Clear();

        X.From3D(rig, new JObject { ["type"] = "edit.end", ["reason"] = "esc" });       // 바뀐 것 없이 3D 에서 Esc
        Scenario.Pump();
        Assert.Null(rig.Vm.SelectedMarker);                                             // 2D Esc 처럼 고름 풀기
        Assert.Empty(Items(Assert.Single(Of(posts, "overlay.select"))));
        Assert.Empty(Of(posts, "edit.stop"));                                           // 3D 가 이미 내렸다
    });

    [Fact]
    public void should_clear_3d_selection_when_selected_symbol_is_deleted() => RunAsync(async (rig, posts) =>
    {
        EditOn3D(rig);
        rig.W.Db.Setup(d => d.DeleteGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var s = Symbol(rig, 9261);
        Pick(rig, "geometric", 9261);
        Scenario.Pump();
        posts.Clear();

        rig.Vm.DeleteSelectedCommand!.Execute(null);                                   // 띠 「선택 항목 삭제」 → 확인 팝업
        await Task.Delay(20);
        await rig.Vm.HandleAsync(new CallDeleteSelectedProcessMessageModel(), CancellationToken.None);   // 「확인」
        Scenario.Pump();
        Assert.DoesNotContain(s, rig.W.Map.Markers);
        Assert.Empty(Items(LastSelect(posts)!));
        Assert.Single(Of(posts, "edit.stop"));
    });

    // ─────────────── 2. 3D 손잡이 → 2D 와 같은 저장 · 되돌리기 ───────────────

    [Fact]
    public void should_save_point_move_and_rotation_like_2d_adorner_and_undo_it() => RunAsync(async (rig, posts) =>
    {
        EditOn3D(rig);
        var saves = RecordSaves(rig);
        var s = Symbol(rig, 9271);
        Pick(rig, "geometric", 9271);
        Scenario.Pump();
        var before = s.Position;
        double lon = Math.Round(rig.Center.Lng + 0.0003, 7), lat = Math.Round(rig.Center.Lat + 0.0001, 7);

        X.From3D(rig, PointCommit("geometric", 9271, lon, lat, heading: 30));
        await Task.Delay(50);

        Assert.Equal((lat, lon), (s.Position.Lat, s.Position.Lng));
        Assert.Equal(30, s.Bearing, 6);
        Assert.Equal((lat, lon), saves[^1]);                                            // DB 저장(DbUpdateProcess)
        Assert.True(rig.Vm.CanUndo);
        Assert.Empty(Of(posts, "edit.revert"));
        Assert.True(rig.Shown);

        rig.Vm.UndoCommand!.Execute(null);                                              // 2D 와 같은 편집 기록 한 단위
        await Task.Delay(50);
        Assert.Equal(before.Lat, s.Position.Lat, 9);
        Assert.Equal(before.Lng, s.Position.Lng, 9);
        Assert.Equal(0, s.Bearing, 6);
    });

    [Fact]
    public void should_ignore_heading_for_billboard_symbol_commit() => RunAsync(async (rig, posts) =>
    {
        EditOn3D(rig);
        RecordSaves(rig);
        var s = Symbol(rig, 9272);
        s.Shape = new BillboardShape { Width = 40, Height = 40 };
        rig.Vm.SelectedMarker = s;
        double lon = Math.Round(rig.Center.Lng + 0.0002, 7);
        X.From3D(rig, PointCommit("geometric", 9272, lon, rig.Center.Lat, heading: 45));
        await Task.Delay(50);
        Assert.Equal(lon, s.Position.Lng, 9);
        Assert.Equal(0, s.Bearing, 6);                                                  // 2D 에서도 돌리지 못하는 종류
    });

    [Fact]
    public void should_save_line_vertices_from_3d_and_undo_restores_old_points() => RunAsync(async (rig, posts) =>
    {
        EditOn3D(rig);
        var line = Line(rig, 9281);
        rig.Vm.SelectedMarker = line;
        Scenario.Pump();
        var start = Assert.Single(Of(posts, "edit.start"));
        Assert.Equal(("line", false), (start["target"]!.Value<string>("overlayKind"), start["target"]!.Value<bool>("rotatable")));
        var old = line.RuntimePoints.ToList();
        var moved = new JArray(new JArray(old[0].Lng, old[0].Lat), new JArray(old[1].Lng + 0.0002, old[1].Lat), new JArray(old[2].Lng, old[2].Lat + 0.0001));

        X.From3D(rig, new JObject
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "overlay", ["overlayKind"] = "line", ["id"] = 9281 },
            ["lon"] = old[0].Lng, ["lat"] = old[0].Lat, ["points"] = moved,
        });
        await Task.Delay(50);

        var now = line.RuntimePoints;
        Assert.Equal(3, now.Count);
        Assert.Equal(old[1].Lng + 0.0002, now[1].Lng, 9);                               // 꼭짓점 옮기기
        Assert.Equal(old[2].Lat + 0.0001, now[2].Lat, 9);
        Assert.Equal(old[2].Lat + 0.0001, line.Model.LinePoints[2].Latitude, 9);        // 모델(DB 로 갈 값)도
        Assert.True(rig.Vm.CanUndo);

        rig.Vm.UndoCommand!.Execute(null);
        await Task.Delay(50);
        Assert.Equal(old.Select(p => (p.Lat, p.Lng)), line.RuntimePoints.Select(p => (p.Lat, p.Lng)));
    });

    [Fact]
    public void should_revert_3d_commit_when_edit_mode_off_or_locked_and_ignore_unknown_or_old_engine() => RunAsync(async (rig, posts) =>
    {
        EditOn3D(rig);
        var saves = RecordSaves(rig);
        var s = Symbol(rig, 9291);
        var locked = Symbol(rig, 9292, 0.0003, locked: true);
        var before = (s.Position, locked.Position);
        double lon = Math.Round(rig.Center.Lng + 0.0004, 7);
        posts.Clear();

        X.From3D(rig, PointCommit("geometric", 9292, lon, rig.Center.Lat));             // 잠김
        rig.Vm.IsEditModeEnabled = false;
        X.From3D(rig, PointCommit("geometric", 9291, lon, rig.Center.Lat));             // 편집 모드 꺼짐
        await Task.Delay(50);
        var reverts = Of(posts, "edit.revert");
        Assert.Equal(new[] { 9292, 9291 }, reverts.Select(r => r["target"]!.Value<int>("id")));
        Assert.All(reverts, r => Assert.Equal(("overlay", "geometric"), (r["target"]!.Value<string>("kind"), r["target"]!.Value<string>("overlayKind"))));
        Assert.Equal(before, (s.Position, locked.Position));
        Assert.Empty(saves);
        Assert.False(rig.Vm.CanUndo);

        rig.Vm.IsEditModeEnabled = true;
        posts.Clear();
        X.From3D(rig, PointCommit("geometric", 99999, lon, rig.Center.Lat));            // 지도에 없는 id = 무시
        X.From3D(rig, PointCommit("pids", 9291, lon, rig.Center.Lat));                   // 종류가 다르면 다른 심볼
        Assert.Null(Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Map3DMath.ParseMessage(new JObject   // 구판 엔진 — 자리 없음 = 버린다(3D 호스트는 null 을 넘기지 않는다)
        {
            ["type"] = "edit.commit", ["target"] = new JObject { ["kind"] = "overlay", ["overlayKind"] = "geometric", ["id"] = 9291 },
        }.ToString()));
        await Task.Delay(50);
        Assert.Empty(Of(posts, "edit.revert"));
        Assert.Equal(before.Item1, s.Position);
        Assert.Empty(saves);
    });

    // ─────────────── 3. 3D {Shift}+끌기 상자 ───────────────

    [Fact]
    public void should_merge_box_like_2d_rubber_band_or_replace_and_skip_locked() => Run((rig, posts) =>
    {
        EditOn3D(rig);
        var a = Symbol(rig, 9301, -0.0003);
        var b = Symbol(rig, 9302, 0);
        var c = Symbol(rig, 9303, 0.0003);
        Symbol(rig, 9304, 0.0004, locked: true);
        JObject Box(bool? add, params int[] ids)
        {
            var o = new JObject { ["type"] = "overlay.box", ["items"] = new JArray(ids.Select(i => new JObject { ["kind"] = "geometric", ["id"] = i })) };
            if (add is bool v) o["add"] = v;
            return o;
        }
        int[] Selected() => (Group(rig).HasSelection ? Group(rig).Selection : new[] { rig.Vm.SelectedMarker! }).Select(m => m.Id).OrderBy(i => i).ToArray();

        Pick(rig, "geometric", 9301);
        Scenario.Pump();
        X.From3D(rig, Box(true, 9302, 9304));                                           // 더하기 — 잠긴 것은 빼고
        Scenario.Pump();
        Assert.Equal(new[] { 9301, 9302 }, Selected());
        Assert.Equal(new[] { 9301, 9302 }, Items(LastSelect(posts)!).Select(i => i.Item2).OrderBy(i => i));

        X.From3D(rig, Box(null, 9302, 9303));                                           // add 없음 = 2D 러버밴드 규칙(겹치면 빼기)
        Scenario.Pump();
        Assert.Equal(new[] { 9301, 9303 }, Selected());

        X.From3D(rig, Box(false, 9302));                                                 // add=false = 상자 안 것으로 바꾸기
        Scenario.Pump();
        Assert.Same(b, rig.Vm.SelectedMarker);
        Assert.False(Group(rig).HasSelection);
        Assert.Equal(new[] { ("geometric", 9302) }, Items(LastSelect(posts)!));

        rig.Vm.IsEditModeEnabled = false;                                               // 보기 전용 = 상자 무시
        X.From3D(rig, Box(true, 9301, 9302, 9303));
        Scenario.Pump();
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.False(Group(rig).HasSelection);
        GC.KeepAlive(a); GC.KeepAlive(c);
    });
}
