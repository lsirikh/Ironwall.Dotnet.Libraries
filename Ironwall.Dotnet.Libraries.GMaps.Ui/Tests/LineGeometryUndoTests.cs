using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>LineArea_Symbol_Resize FR-04 — LineGeometryCommand 스냅샷 점 Undo/Redo 왕복·타입인지(§5-C R-07) 단위테스트.</summary>
public class LineGeometryUndoTests
{
    // ─── Fake line marker (ILineEditableMarker) ───
    private sealed class FakeLineMarker : ILineEditableMarker
    {
        public List<PointLatLng> Pts = new();
        public int Id { get; set; }
        public string Title { get; set; } = "";
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
        public bool Visible { get; set; } = true;
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
        // line members
        public List<PointLatLng> RuntimePoints => new(Pts);
        public List<GeoPoint> LinePoints => new();
        public bool IsDrawing => false;
        public bool IsClosedPath { get; set; }
        public EnumLinePattern LinePattern { get; set; }
        public double LineOpacity { get; set; }
        public bool ShowArrowHead { get; set; }
        public double TotalDistance => 0;
        public void AddPoint(PointLatLng p) => Pts.Add(p);
        public void UpdatePoint(int i, PointLatLng p) => Pts[i] = p;
        public void RemoveLastPoint() { if (Pts.Count > 0) Pts.RemoveAt(Pts.Count - 1); }
        public void StartDrawing() { }
        public void FinishDrawing() { }
        public void CancelDrawing() { }
        public void ApplyGeometry(System.Collections.Generic.IReadOnlyList<PointLatLng> points, PointLatLng position)
        { Pts = new List<PointLatLng>(points); Position = position; }
    }

    private sealed class FakeCtx : IUndoApplyContext
    {
        public readonly Dictionary<(int, bool), IEditableMarker> Markers = new();
        public int ApplyCount;
        public IGMapDbSymbolService Db => null!;
        public IEditableMarker? FindMarkerById(int id) => FindMarkerById(id, false);
        public IEditableMarker? FindMarkerById(int id, bool isImage) => Markers.TryGetValue((id, isImage), out var m) ? m : null;
        public Task ApplyMarkerUpdateAsync(IEditableMarker marker, CancellationToken ct = default) { ApplyCount++; return Task.CompletedTask; }
        public Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot s, CancellationToken ct = default) => Task.FromResult<IEditableMarker?>(null);
        public Task RemoveMarkerAsync(IEditableMarker m, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyZOrderAsync(IReadOnlyList<(bool isImage, int id, int zOrder)> p, CancellationToken ct = default) => Task.CompletedTask;
        public void ResyncTree() { }
        public void SyncMarkerNode(int id) { }
        public void SyncMarkerNode(int id, bool isImage) { }
        public Task ApplyVisibilityAsync(int id, bool show, bool isImage = false, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyCustomImageRotationAsync(int id, double rotation, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyCustomImageEditAsync(int id, RectLatLng bounds, double rotation, CancellationToken ct = default) => Task.CompletedTask;
        public Task ApplyLayerFieldsAsync(int layerId, string? name, double? opacity, int? zOrder, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static List<PointLatLng> Pts(params (double lat, double lng)[] xs)
    {
        var l = new List<PointLatLng>();
        foreach (var (lat, lng) in xs) l.Add(new PointLatLng(lat, lng));
        return l;
    }

    [Fact]
    public async Task should_restore_points_when_undo()
    {
        var marker = new FakeLineMarker { Id = 5, Pts = Pts((37.7, 127.4)), Position = new PointLatLng(37.7, 127.4) };
        var ctx = new FakeCtx();
        ctx.Markers[(5, false)] = marker;
        var before = (Pts((37.5, 127.0)), new PointLatLng(37.5, 127.0));
        var after = (Pts((37.7, 127.4)), new PointLatLng(37.7, 127.4));
        var cmd = new LineGeometryCommand(ctx, 5, before, after);

        await cmd.UndoAsync();

        Assert.Single(marker.Pts);
        Assert.Equal(37.5, marker.Pts[0].Lat, 9);   // before 점으로 복원
        Assert.Equal(127.0, marker.Position.Lng, 9); // before 위치로 복원
    }

    [Fact]
    public async Task should_reapply_points_when_redo()
    {
        var marker = new FakeLineMarker { Id = 5, Pts = Pts((37.5, 127.0)), Position = new PointLatLng(37.5, 127.0) };
        var ctx = new FakeCtx();
        ctx.Markers[(5, false)] = marker;
        var cmd = new LineGeometryCommand(ctx, 5,
            (Pts((37.5, 127.0)), new PointLatLng(37.5, 127.0)),
            (Pts((37.9, 127.8)), new PointLatLng(37.9, 127.8)));

        await cmd.ExecuteAsync();   // redo=after

        Assert.Equal(37.9, marker.Pts[0].Lat, 9);
        Assert.Equal(127.8, marker.Position.Lng, 9);
    }

    [Fact]
    public async Task should_target_line_not_image_when_id_collision()
    {
        // 같은 Id=5로 line(non-image)과 image가 공존 — 커맨드는 isImage=false만 겨냥해야
        var line = new FakeLineMarker { Id = 5, Pts = Pts((37.7, 127.4)) };
        var image = new FakeLineMarker { Id = 5, Pts = Pts((10.0, 10.0)) };
        var ctx = new FakeCtx();
        ctx.Markers[(5, false)] = line;
        ctx.Markers[(5, true)] = image;   // 반대 타입
        var cmd = new LineGeometryCommand(ctx, 5,
            (Pts((37.5, 127.0)), new PointLatLng(37.5, 127.0)),
            (Pts((37.7, 127.4)), new PointLatLng(37.7, 127.4)));

        await cmd.UndoAsync();

        Assert.Equal(37.5, line.Pts[0].Lat, 9);    // line만 변경
        Assert.Equal(10.0, image.Pts[0].Lat, 9);   // image 불변
    }

    [Fact]
    public async Task should_persist_after_apply()
    {
        var marker = new FakeLineMarker { Id = 5, Pts = Pts((37.7, 127.4)) };
        var ctx = new FakeCtx();
        ctx.Markers[(5, false)] = marker;
        var cmd = new LineGeometryCommand(ctx, 5,
            (Pts((37.5, 127.0)), new PointLatLng(37.5, 127.0)),
            (Pts((37.7, 127.4)), new PointLatLng(37.7, 127.4)));

        await cmd.UndoAsync();

        Assert.Equal(1, ctx.ApplyCount);   // ApplyMarkerUpdateAsync 호출(영속)
    }
}
