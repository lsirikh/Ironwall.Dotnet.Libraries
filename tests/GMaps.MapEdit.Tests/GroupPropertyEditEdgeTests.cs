using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 다중 선택 속성 판(그룹 편집) 경계(t-20261019-193448-b2cc 영역 D) — 종류 섞인 공통 칸 · Redo · 권한 상실 · 저장 실패.
/// 판은 VM 의 실제 경로(ShowGroupPropertyPanelIfNeeded → PropertyPanelFactory.CreateCommonPropertyPanel → 이벤트 배선)로 만든다.
/// </summary>
public class GroupPropertyEditEdgeTests
{
    private sealed class Rig
    {
        public required WiredMapRig W;
        public List<(int id, double width, double zoom)> Saved { get; } = new();
        public HashSet<int> FailIds { get; } = new();
        public GroupSelectionService Group => Scenario.GetField<GroupSelectionService>(W.Vm, "_groupSelection");
    }

    private static Rig Create(bool canEdit = true)
    {
        var r = new Rig { W = WiredMapRig.Create(canEdit) };
        r.W.Db.Setup(d => d.UpdateGeometrySymbolAsync(It.IsAny<IGeometricSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IGeometricSymbolModel, CancellationToken>((m, _) =>
            {
                r.Saved.Add((m.Id, m.Width, m.Zoom));
                if (r.FailIds.Contains(m.Id)) throw new InvalidOperationException("가짜 DB 실패");
                return Task.FromResult<IGeometricSymbolModel?>(null);
            });
        r.W.Db.Setup(d => d.UpdatePidsSymbolAsync(It.IsAny<IPidsSymbolModel>(), It.IsAny<CancellationToken>()))
            .Returns<IPidsSymbolModel, CancellationToken>((m, _) =>
            {
                r.Saved.Add((m.Id, m.Width, m.Zoom));
                if (r.FailIds.Contains(m.Id)) throw new InvalidOperationException("가짜 DB 실패");
                return Task.FromResult<IPidsSymbolModel?>(null);
            });
        return r;
    }

    private static GMapGeometricMarker Shape(Rig r, int id, int dx, double width = 40)
    {
        var center = r.W.Map.FromLatLngToLocal(r.W.Map.Position);
        var geo = r.W.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y);
        var m = (GMapGeometricMarker)new MarkerFactory(Mock.Of<ILogService>(), r.W.Devices).CreateMarker(new GeometricSymbolModel
        { Id = id, Title = $"도형-{id}", Latitude = geo.Lat, Longitude = geo.Lng, Width = width, Height = 40, ShowShape = true, Zoom = 10 });
        r.W.Map.Markers.Add(m);
        return m;
    }

    private static GMapPidsMarker Device(Rig r, int id, int dx)
    {
        var center = r.W.Map.FromLatLngToLocal(r.W.Map.Position);
        var geo = r.W.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y);
        var m = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        { Id = id, Title = $"장비-{id}", DeviceType = EnumDeviceType.IpCamera, Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, Zoom = 12 });
        r.W.Map.Markers.Add(m);
        return m;
    }

    private static Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties.GMapPropertyBaseControl OpenGroup(Rig r, params IEditableMarker[] ms)
    {
        Scenario.Layout(r.W.Deco, 1200, 800);
        r.Group.SetSelection(ms);
        Scenario.Call(r.W.Vm, "ShowGroupPropertyPanelIfNeeded");
        var panel = r.W.Vm.PropertyPanel!;
        Assert.True(panel.IsGroupEditing);
        return panel;
    }

    [Fact]
    [Trait("Scenario", "D-74")]
    public void should_apply_common_field_to_every_kind_and_undo_as_one_unit_when_group_mixes_kinds() => Scenario.StaAsync(async () =>
    {
        var r = Create();
        var s = Shape(r, 701, -100);
        var d = Device(r, 702, 100);
        var panel = OpenGroup(r, s, d);
        Assert.True(double.IsNaN(panel.MarkerZoom));            // 서로 다름 = 빈칸(Pending)

        panel.MarkerZoom = 15;
        await Task.Delay(60);
        Assert.Equal(15, s.Zoom, 9);
        Assert.Equal(15, d.Zoom, 9);
        Assert.Contains(r.Saved, x => x.id == 701 && x.zoom == 15);   // 종류마다 제 저장 경로
        Assert.Contains(r.Saved, x => x.id == 702 && x.zoom == 15);
        Assert.Equal("그룹 속성 변경: Zoom (2개)", r.W.Undo.NextUndoDescription);

        r.W.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        Assert.Equal(10, s.Zoom, 9);                            // 각자 원래 값
        Assert.Equal(12, d.Zoom, 9);
        Assert.True(double.IsNaN(panel.MarkerZoom));            // 판도 다시 '서로 다름'
        Assert.False(r.W.Undo.CanUndo);
        Assert.Empty(r.W.Errors);
    });

    [Fact]
    [Trait("Scenario", "D-75")]
    public void should_reapply_group_change_to_all_and_refill_panel_when_redo_follows_undo() => Scenario.StaAsync(async () =>
    {
        var r = Create();
        var a = Shape(r, 711, -100);
        var b = Shape(r, 712, 100);
        var panel = OpenGroup(r, a, b);
        panel.MarkerWidth = 72;
        await Task.Delay(60);

        r.W.Vm.UndoCommand!.Execute(null);
        await Task.Delay(60);
        Assert.Equal(40, a.Width, 9);
        Assert.Equal(40, panel.MarkerWidth, 9);
        r.Saved.Clear();

        r.W.Vm.RedoCommand!.Execute(null);
        await Task.Delay(60);
        Assert.Equal(72, a.Width, 9);
        Assert.Equal(72, b.Width, 9);
        Assert.Equal(72, panel.MarkerWidth, 9);                 // 판도 따라온다
        Assert.Equal(new[] { 711, 712 }, r.Saved.Select(x => x.id).Distinct().OrderBy(x => x));   // 다시 저장
        Assert.Equal("그룹 속성 변경: Width (2개)", r.W.Undo.NextUndoDescription);
        Assert.False(r.W.Undo.CanRedo);
    });

    [Fact]
    [Trait("Scenario", "D-76")]
    public void should_block_group_property_save_with_notice_when_permission_is_lost_while_panel_is_open() => Scenario.StaAsync(async () =>
    {
        var r = Create();
        var a = Shape(r, 721, -100);
        var b = Shape(r, 722, 100);
        var panel = OpenGroup(r, a, b);
        r.W.Permission.Setup(p => p.CanEdit("map")).Returns(false);   // 판이 열린 채 권한 회수

        panel.MarkerWidth = 90;
        await Task.Delay(60);
        Assert.Equal(40, a.Width, 9);                           // 그룹 모드는 VM 이 일괄 적용 — 막히면 아무도 안 바뀐다
        Assert.Equal(40, b.Width, 9);
        Assert.Empty(r.Saved);
        Assert.False(r.W.Undo.CanUndo);
        Assert.Contains(r.W.Published.OfType<OpenInfoPopupMessageModel>(), p => p.Title == "권한 없음");
        Assert.Contains(r.W.Log.Invocations, i => i.Method.Name == "Warning" && (i.Arguments[0] as string ?? "").Contains("[RBAC] 맵 편집 권한 없음 — 속성 저장 차단: Width"));
    });

    [Fact]
    [Trait("Scenario", "D-77")]
    public void should_not_record_undo_for_member_whose_group_property_save_failed() => Scenario.StaAsync(async () =>
    {
        // 결함 ④: 그룹 속성 저장이 실패해도 Undo 에 기록했다(단일 경로는 실패면 기록 안 함).
        var r = Create();
        var ok = Shape(r, 731, -100);
        var bad = Shape(r, 732, 100);
        var panel = OpenGroup(r, ok, bad);
        r.FailIds.Add(732);

        panel.MarkerWidth = 55;
        await Task.Delay(60);
        Assert.Contains(r.W.AllErrors, e => e.Contains("그룹 속성 영속 실패"));
        Assert.True(r.W.Undo.CanUndo);

        r.Saved.Clear();
        r.W.Vm.UndoCommand!.Execute(null);                      // 저장된 것만 되돌린다 — 실패한 732 를 되돌리며 또 저장 시도하지 않는다
        await Task.Delay(60);
        Assert.Equal(40, ok.Width, 9);
        Assert.Equal(new[] { 731 }, r.Saved.Select(x => x.id).Distinct());
        Assert.False(r.W.Undo.CanUndo);

        // 전원 실패 = 기록 없음
        r.FailIds.Add(731);
        panel.MarkerWidth = 66;
        await Task.Delay(60);
        Assert.False(r.W.Undo.CanUndo);
    });
}
