using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>Map_Edit_Undo_Redo — UndoService/커맨드/스냅샷/coalescing/재진입 단위테스트(격리: 페이크 seam).</summary>
public class UndoRedoTests
{
    // ─────────────── Fakes ───────────────
    private sealed class FakeEditableMarker : IEditableMarker
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public double TitleSize { get; set; }
        public PointLatLng Position { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Zoom { get; set; }
        public double Bearing { get; set; }
        public bool IsSelected { get; set; }
        public bool IsVisible { get; set; }
        public bool IsLayerEnabled { get; set; }
        public bool IsLocked { get; set; }
        public bool ShowShape { get; set; }
        public bool ShowTitle { get; set; }
        public double LabelOffsetX { get; set; }
        public double LabelOffsetY { get; set; }
        public EnumColorType FillColor { get; set; }
        public EnumColorType StrokeColor { get; set; }
        public double StrokeThickness { get; set; }
        public int ZOrder { get; set; }
        public EnumOperationState OperationState { get; set; }
        public bool IsDisposed { get; private set; }
        public bool EnableShapeAnimation { get; set; }
        public void UpdateLocation(PointLatLng p) => Position = p;
        public void UpdateSize(double w, double h) { Width = w; Height = h; }
        public void UpdateRotation(double b) => Bearing = b;
        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeApplyContext : IUndoApplyContext
    {
        public readonly Dictionary<int, IEditableMarker> Markers = new();
        public int ApplyCount;
        public IReadOnlyList<(int id, int zOrder)>? LastZOrder;
        public readonly List<int> Removed = new();
        public readonly List<int> Restored = new();
        public IGMapDbSymbolService Db => null!;
        public IEditableMarker? FindMarkerById(int id) => Markers.TryGetValue(id, out var m) ? m : null;
        public Task ApplyMarkerUpdateAsync(IEditableMarker marker, CancellationToken ct = default) { ApplyCount++; return Task.CompletedTask; }
        public Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot snap, CancellationToken ct = default)
        {
            var m = new FakeEditableMarker { Id = snap.Id };
            Markers[snap.Id] = m; Restored.Add(snap.Id);
            return Task.FromResult<IEditableMarker?>(m);
        }
        public Task RemoveMarkerAsync(IEditableMarker marker, CancellationToken ct = default)
        { Markers.Remove(marker.Id); Removed.Add(marker.Id); return Task.CompletedTask; }
        public Task ApplyZOrderAsync(IReadOnlyList<(int id, int zOrder)> pairs, CancellationToken ct = default)
        { LastZOrder = pairs; return Task.CompletedTask; }
        public void ResyncTree() { }
        public readonly List<int> SyncedNodes = new();
        public void SyncMarkerNode(int id) => SyncedNodes.Add(id);

        // v2 커버리지 fake
        public readonly List<(int id, bool show)> VisApplied = new();
        public Task ApplyVisibilityAsync(int id, bool show, CancellationToken ct = default) { VisApplied.Add((id, show)); return Task.CompletedTask; }
        public readonly List<(int id, double rot)> RotApplied = new();
        public Task ApplyCustomImageRotationAsync(int id, double rotation, CancellationToken ct = default) { RotApplied.Add((id, rotation)); return Task.CompletedTask; }
        public readonly List<(int id, GMap.NET.RectLatLng bounds, double rot)> ImgEditApplied = new();
        public Task ApplyCustomImageEditAsync(int id, GMap.NET.RectLatLng bounds, double rotation, CancellationToken ct = default) { ImgEditApplied.Add((id, bounds, rotation)); return Task.CompletedTask; }
        public readonly List<(int id, string? name, double? opacity, int? zOrder)> LayerApplied = new();
        public bool ThrowOnLayer;   // BUG-04 검증 — apply 실패(DB오류 등) 모사
        public Task ApplyLayerFieldsAsync(int layerId, string? name, double? opacity, int? zOrder, CancellationToken ct = default)
        {
            if (ThrowOnLayer) throw new System.InvalidOperationException("db fail");
            LayerApplied.Add((layerId, name, opacity, zOrder)); return Task.CompletedTask;
        }
    }

    /// <summary>Undo 시 예외를 던지는 커맨드 — 매크로 자식별 격리 검증용.</summary>
    private sealed class ThrowingCommand : IUndoableCommand
    {
        public string Description => "throw"; public int ScopeMapId { get; set; }
        public Task ExecuteAsync(CancellationToken ct = default) => throw new System.InvalidOperationException("boom");
        public Task UndoAsync(CancellationToken ct = default) => throw new System.InvalidOperationException("boom");
    }

    /// <summary>공유 셀을 토글하는 단순 커맨드 — UndoService 메커니즘 검증.</summary>
    private sealed class ValueCommand : IUndoableCommand
    {
        private readonly int[] _cell; private readonly int _before, _after;
        public ValueCommand(int[] cell, int before, int after) { _cell = cell; _before = before; _after = after; }
        public string Description => "값";
        public int ScopeMapId { get; set; }
        public Task ExecuteAsync(CancellationToken ct = default) { _cell[0] = _after; return Task.CompletedTask; }
        public Task UndoAsync(CancellationToken ct = default) { _cell[0] = _before; return Task.CompletedTask; }
    }

    /// <summary>undo 순서 검증용 로그 커맨드.</summary>
    private sealed class LogCommand : IUndoableCommand
    {
        private readonly List<string> _log; private readonly string _name;
        public LogCommand(List<string> log, string name) { _log = log; _name = name; }
        public string Description => _name;
        public int ScopeMapId { get; set; }
        public Task ExecuteAsync(CancellationToken ct = default) { _log.Add("E:" + _name); return Task.CompletedTask; }
        public Task UndoAsync(CancellationToken ct = default) { _log.Add("U:" + _name); return Task.CompletedTask; }
    }

    private sealed class Probe { public int Concurrent; public int Max; }
    /// <summary>실행 중 동시성 카운터 — 직렬화 검증.</summary>
    private sealed class ProbeCommand : IUndoableCommand
    {
        private readonly Probe _p; public ProbeCommand(Probe p) => _p = p;
        public string Description => "probe"; public int ScopeMapId { get; set; }
        public Task ExecuteAsync(CancellationToken ct = default) => Run();
        public Task UndoAsync(CancellationToken ct = default) => Run();
        private async Task Run()
        {
            int now = Interlocked.Increment(ref _p.Concurrent);
            if (now > _p.Max) _p.Max = now;
            await Task.Delay(20);
            Interlocked.Decrement(ref _p.Concurrent);
        }
    }

    // ─────────────── UndoService ───────────────
    [Fact(DisplayName = "UndoService — push→undo→redo가 상태를 원복/재적용")]
    public async Task should_restore_state_when_undo_then_redo()
    {
        var svc = new UndoService();
        var cell = new[] { 5 };
        svc.Push(new ValueCommand(cell, before: 1, after: 5));
        Assert.True(svc.CanUndo);
        await svc.UndoAsync();
        Assert.Equal(1, cell[0]);
        Assert.True(svc.CanRedo);
        await svc.RedoAsync();
        Assert.Equal(5, cell[0]);
    }

    [Fact(DisplayName = "UndoService — 새 push 시 redo 스택 비움")]
    public async Task should_clear_redo_when_new_push()
    {
        var svc = new UndoService(); var cell = new[] { 1 };
        svc.Push(new ValueCommand(cell, 0, 1));
        await svc.UndoAsync();
        Assert.True(svc.CanRedo);
        svc.Push(new ValueCommand(cell, 0, 2));
        Assert.False(svc.CanRedo);
    }

    [Fact(DisplayName = "UndoService — 깊이 50 초과 시 가장 오래된 것 트림")]
    public async Task should_trim_oldest_when_exceeding_depth_50()
    {
        var svc = new UndoService(); var cell = new[] { 0 };
        for (int i = 0; i < 60; i++) svc.Push(new ValueCommand(cell, i, i + 1));
        int count = 0;
        while (svc.CanUndo) { await svc.UndoAsync(); count++; }
        Assert.Equal(50, count);
    }

    [Fact(DisplayName = "UndoService — 억제 스코프 중 push 무시")]
    public void should_ignore_push_when_recording_suspended()
    {
        var svc = new UndoService();
        using (svc.SuspendRecording())
            svc.Push(new ValueCommand(new[] { 0 }, 0, 1));
        Assert.False(svc.CanUndo);
    }

    [Fact(DisplayName = "UndoService — 배치는 1 undo 단위, 자식 역순 실행")]
    public async Task should_group_children_reverse_when_batch()
    {
        var svc = new UndoService(); var log = new List<string>();
        using (svc.BeginBatch("batch"))
        {
            svc.Push(new LogCommand(log, "A"));
            svc.Push(new LogCommand(log, "B"));
            svc.Push(new LogCommand(log, "C"));
        }
        Assert.True(svc.CanUndo);
        await svc.UndoAsync();
        Assert.False(svc.CanUndo);   // 1 단위 → 한 번에 소진
        Assert.Equal(new[] { "U:C", "U:B", "U:A" }, log);
    }

    [Fact(DisplayName = "UndoService — push/undo/redo 시 StateChanged 발화")]
    public async Task should_raise_statechanged_when_push_undo_redo()
    {
        var svc = new UndoService(); int n = 0; svc.StateChanged += (_, _) => n++;
        svc.Push(new ValueCommand(new[] { 0 }, 0, 1));
        await svc.UndoAsync();
        await svc.RedoAsync();
        Assert.True(n >= 3);
    }

    [Fact(DisplayName = "UndoService — 동시 Undo 호출은 직렬화(인터리브 없음)")]
    public async Task should_serialize_when_concurrent_undo()
    {
        var svc = new UndoService(); var p = new Probe();
        svc.Push(new ProbeCommand(p));
        svc.Push(new ProbeCommand(p));
        await Task.WhenAll(svc.UndoAsync(), svc.UndoAsync());
        Assert.True(p.Max <= 1);
    }

    // ─────────────── 커맨드 왕복(페이크 seam) ───────────────
    [Fact(DisplayName = "TransformCommand — undo/redo가 위치·크기·방위 원복/재적용")]
    public async Task should_restore_transform_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 7 };
        ctx.Markers[7] = m;
        var before = (new PointLatLng(10, 20), 50.0, 60.0, 30.0);
        var after = (new PointLatLng(11, 21), 55.0, 65.0, 45.0);
        m.Position = after.Item1; m.Width = 55; m.Height = 65; m.Bearing = 45;   // 편집 후 상태
        var cmd = new TransformCommand(ctx, 7, before, after);

        await cmd.UndoAsync();
        Assert.Equal(10, m.Position.Lat); Assert.Equal(50, m.Width); Assert.Equal(30, m.Bearing);
        await cmd.ExecuteAsync();
        Assert.Equal(11, m.Position.Lat); Assert.Equal(55, m.Width); Assert.Equal(45, m.Bearing);
    }

    [Fact(DisplayName = "PropertyChangeCommand — undo/redo가 Title 값 원복/재적용")]
    public async Task should_restore_property_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 4, Title = "new" };
        ctx.Markers[4] = m;
        var cmd = new PropertyChangeCommand(ctx, 4, "Title", "old", "new");
        await cmd.UndoAsync();
        Assert.Equal("old", m.Title);
        await cmd.ExecuteAsync();
        Assert.Equal("new", m.Title);
    }

    [Fact(DisplayName = "LabelOffsetCommand — undo/redo가 오프셋 원복/재적용")]
    public async Task should_restore_offset_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 2, LabelOffsetX = 30, LabelOffsetY = 40 };
        ctx.Markers[2] = m;
        var cmd = new LabelOffsetCommand(ctx, 2, (0, 0), (30, 40));
        await cmd.UndoAsync();
        Assert.Equal(0, m.LabelOffsetX); Assert.Equal(0, m.LabelOffsetY);
        await cmd.ExecuteAsync();
        Assert.Equal(30, m.LabelOffsetX); Assert.Equal(40, m.LabelOffsetY);
    }

    [Fact(DisplayName = "LockCommand — undo/redo가 잠금상태 원복/재적용")]
    public async Task should_restore_lock_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 1, IsLocked = true };
        ctx.Markers[1] = m;
        var cmd = new LockCommand(ctx, 1, before: false, after: true);
        await cmd.UndoAsync();
        Assert.False(m.IsLocked);
        await cmd.ExecuteAsync();
        Assert.True(m.IsLocked);
    }

    [Fact(DisplayName = "RenameSymbolCommand — undo/redo가 이름 원복/재적용")]
    public async Task should_restore_name_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 8, Title = "새이름" };
        ctx.Markers[8] = m;
        var cmd = new RenameSymbolCommand(ctx, 8, "옛이름", "새이름");
        await cmd.UndoAsync();
        Assert.Equal("옛이름", m.Title);
        await cmd.ExecuteAsync();
        Assert.Equal("새이름", m.Title);
    }

    [Fact(DisplayName = "DeleteSymbolCommand — undo가 스냅샷으로 복원, redo가 재삭제")]
    public async Task should_restore_deleted_when_undo_and_remove_when_redo()
    {
        var ctx = new FakeApplyContext();
        var model = new SymbolModel { Id = 12, Title = "삭제대상" };
        var snap = SymbolSnapshot.Capture(model, 12, "GMapCustomMarker", false)!;
        var cmd = new DeleteSymbolCommand(ctx, snap);
        await cmd.UndoAsync();                       // 복원
        Assert.Contains(12, ctx.Restored);
        Assert.True(ctx.Markers.ContainsKey(12));
        await cmd.ExecuteAsync();                    // 재삭제
        Assert.Contains(12, ctx.Removed);
    }

    // ─────────────── coalescing ───────────────
    [Fact(DisplayName = "EditRecorder — 같은 속성 연속 변경은 1 undo 단위로 병합(earliest before)")]
    public async Task should_coalesce_when_same_property_changed_twice()
    {
        var svc = new UndoService();
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 3, Title = "b" };
        ctx.Markers[3] = m;
        var rec = new EditRecorder(svc) { Context = ctx };
        rec.RecordPropertyChange(m, "Title", "orig", "a");
        rec.RecordPropertyChange(m, "Title", "a", "b");
        await svc.UndoAsync();
        Assert.Equal("orig", m.Title);   // 병합된 earliest before
        Assert.False(svc.CanUndo);       // 1 단위였음
    }

    // ─────────────── snapshot 독립성 ───────────────
    [Fact(DisplayName = "SymbolSnapshot — 원본 모델 변경이 스냅샷에 영향 없음(딥클론)")]
    public void should_be_independent_when_original_mutated()
    {
        var model = new SymbolModel { Id = 9, Title = "orig", Width = 50 };
        var snap = SymbolSnapshot.Capture(model, 9, "GMapCustomMarker", false);
        Assert.NotNull(snap);
        model.Title = "changed"; model.Width = 999;
        var cloned = (SymbolModel)snap!.Model;
        Assert.Equal("orig", cloned.Title);
        Assert.Equal(50, cloned.Width);
    }

    // ─────────────── 회귀수정 검증(감사 Round1) ───────────────
    [Fact(DisplayName = "MacroCommand — 자식 하나가 예외를 던져도 나머지 형제는 계속 취소(그룹이동 회귀)")]
    public async Task should_continue_siblings_when_one_child_throws()
    {
        var log = new List<string>();
        var macro = new MacroCommand("group", new IUndoableCommand[]
        {
            new LogCommand(log, "A"),
            new ThrowingCommand(),
            new LogCommand(log, "C"),
        });
        await macro.UndoAsync();   // 역순: C, throw(흡수), A
        Assert.Contains("U:C", log);
        Assert.Contains("U:A", log);   // 예외에도 A까지 취소됨(중단 안 됨)
    }

    [Fact(DisplayName = "PropertyChangeCommand — Title undo 시 트리 노드 타겟 동기화 호출(SyncMarkerNode)")]
    public async Task should_sync_tree_node_when_undo_title_change()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 5, Title = "new" };
        ctx.Markers[5] = m;
        var cmd = new PropertyChangeCommand(ctx, 5, "Title", "old", "new");
        await cmd.UndoAsync();
        Assert.Contains(5, ctx.SyncedNodes);   // 트리 노드 이름 동기화 호출됨
    }

    [Fact(DisplayName = "SymbolSnapshot — Line 모델의 LinePoints가 딥클론서 보존(개수·값)")]
    public void should_preserve_linepoints_when_snapshot_line()
    {
        var model = new LineSymbolModel
        {
            Id = 21,
            Title = "라인",
            LinePoints = new List<GeoPoint> { new GeoPoint(10, 20, 1), new GeoPoint(30, 40, 2), new GeoPoint(50, 60, 3) }
        };
        var snap = SymbolSnapshot.Capture(model, 21, "GMapLineMarker", false);
        Assert.NotNull(snap);
        var clone = (LineSymbolModel)snap!.Model;
        Assert.Equal(3, clone.LinePoints.Count);
        Assert.Equal(30, clone.LinePoints[1].Latitude);
        Assert.Equal(40, clone.LinePoints[1].Longitude);
        // 독립성 — 원본 변경이 스냅샷에 영향 없음
        model.LinePoints.Add(new GeoPoint(70, 80, 4));
        Assert.Equal(3, clone.LinePoints.Count);
    }

    // ─────────────── v2 커버리지 ───────────────
    [Fact(DisplayName = "VisibilityCommand — 표시/숨김 do→undo→redo")]
    public async Task should_toggle_visibility_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var svc = new UndoService();
        svc.Push(new VisibilityCommand(ctx, id: 7, before: true, after: false));   // 숨김 do
        await svc.UndoAsync();   // before=표시
        Assert.Equal((7, true), ctx.VisApplied[^1]);
        await svc.RedoAsync();   // after=숨김
        Assert.Equal((7, false), ctx.VisApplied[^1]);
    }

    [Fact(DisplayName = "CustomImageRotationCommand — 회전 do→undo→redo")]
    public async Task should_rotate_customimage_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var svc = new UndoService();
        svc.Push(new CustomImageRotationCommand(ctx, id: 3, before: 0, after: 90));
        await svc.UndoAsync();
        Assert.Equal((3, 0d), ctx.RotApplied[^1]);
        await svc.RedoAsync();
        Assert.Equal((3, 90d), ctx.RotApplied[^1]);
    }

    [Fact(DisplayName = "CustomImageEditCommand — 핸들 편집(bounds+rotation) do→undo→redo (D1)")]
    public async Task should_edit_customimage_bounds_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var svc = new UndoService();
        var beforeB = new GMap.NET.RectLatLng(37.0, 127.0, 0.10, 0.10);
        var afterB = new GMap.NET.RectLatLng(37.5, 127.5, 0.20, 0.20);
        svc.Push(new CustomImageEditCommand(ctx, id: 8, beforeBounds: beforeB, beforeRot: 0, afterBounds: afterB, afterRot: 45));
        await svc.UndoAsync();
        Assert.Equal((8, beforeB, 0d), ctx.ImgEditApplied[^1]);   // undo → before
        await svc.RedoAsync();
        Assert.Equal((8, afterB, 45d), ctx.ImgEditApplied[^1]);   // redo → after
    }

    [Fact(DisplayName = "LayerNodeCommand — 이름 필드 do→undo→redo")]
    public async Task should_apply_layerfields_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var svc = new UndoService();
        var before = new[] { new LayerFields(5, "옛이름", null, null) };
        var after = new[] { new LayerFields(5, "새이름", null, null) };
        svc.Push(new LayerNodeCommand(ctx, "이름 변경", before, after));
        await svc.UndoAsync();
        Assert.Equal("옛이름", ctx.LayerApplied[^1].name);
        await svc.RedoAsync();
        Assert.Equal("새이름", ctx.LayerApplied[^1].name);
    }

    // ─────────────── silent-success 방지(감사 Round1 — BUG-03/04) ───────────────
    [Fact(DisplayName = "UndoService — 커맨드 예외 시 undo 스택 유지·redo로 이동 안 함(silent-success 방지)")]
    public async Task should_keep_in_undo_stack_when_command_throws()
    {
        var svc = new UndoService();
        svc.Push(new ThrowingCommand());
        var ok = await svc.UndoAsync();
        Assert.False(ok);            // 실패를 보고(true 아님)
        Assert.True(svc.CanUndo);    // undo 스택에 유지 → 재시도 가능
        Assert.False(svc.CanRedo);   // 실패인데 redo로 잘못 이동하지 않음
    }

    [Fact(DisplayName = "LayerNodeCommand — apply 예외 전파 시 UndoService가 실패 인지(redo 오이동 방지, BUG-04)")]
    public async Task should_not_move_to_redo_when_layer_apply_throws()
    {
        var ctx = new FakeApplyContext { ThrowOnLayer = true };   // 실 ApplyLayerFieldsAsync가 swallow 대신 전파하도록 수정된 계약
        var svc = new UndoService();
        var before = new[] { new LayerFields(5, "옛이름", null, null) };
        var after = new[] { new LayerFields(5, "새이름", null, null) };
        svc.Push(new LayerNodeCommand(ctx, "이름 변경", before, after));
        var ok = await svc.UndoAsync();
        Assert.False(ok);
        Assert.True(svc.CanUndo);    // 실패 → undo 스택 유지
        Assert.False(svc.CanRedo);   // redo로 이동 안 함
    }

    // ─────────────── 재감사 수정(CMD-01/CMD-02) ───────────────
    [Fact(DisplayName = "PropertyChangeCommand — Width undo/redo 복원(CMD-01)")]
    public async Task should_restore_width_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 6, Width = 80, Height = 40 };
        ctx.Markers[6] = m;
        var cmd = new PropertyChangeCommand(ctx, 6, "Width", 50.0, 80.0);
        await cmd.UndoAsync();
        Assert.Equal(50, m.Width);
        await cmd.ExecuteAsync();
        Assert.Equal(80, m.Width);
    }

    [Fact(DisplayName = "PropertyChangeCommand — Height undo/redo 복원(CMD-01)")]
    public async Task should_restore_height_when_undo_redo()
    {
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 6, Width = 40, Height = 60 };
        ctx.Markers[6] = m;
        var cmd = new PropertyChangeCommand(ctx, 6, "Height", 30.0, 60.0);
        await cmd.UndoAsync();
        Assert.Equal(30, m.Height);
        await cmd.ExecuteAsync();
        Assert.Equal(60, m.Height);
    }

    [Fact(DisplayName = "EditRecorder — 복원 불가 속성은 undo 엔트리 미생성(CMD-02)")]
    public void should_drop_undo_entry_when_property_not_replayable()
    {
        var svc = new UndoService();
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 3 };
        ctx.Markers[3] = m;
        var rec = new EditRecorder(svc) { Context = ctx };
        rec.RecordPropertyChange(m, "FOVColor", "Purple", "Red");   // ApplyProperty 미지원 → 죽은 엔트리 방지
        Assert.False(svc.CanUndo);
    }

    [Fact(DisplayName = "EditRecorder — 복원 가능 속성은 undo 엔트리 생성(CMD-02)")]
    public void should_keep_undo_entry_when_property_replayable()
    {
        var svc = new UndoService();
        var ctx = new FakeApplyContext();
        var m = new FakeEditableMarker { Id = 3, Title = "b" };
        ctx.Markers[3] = m;
        var rec = new EditRecorder(svc) { Context = ctx };
        rec.RecordPropertyChange(m, "Title", "a", "b");
        Assert.True(svc.CanUndo);
    }
}
