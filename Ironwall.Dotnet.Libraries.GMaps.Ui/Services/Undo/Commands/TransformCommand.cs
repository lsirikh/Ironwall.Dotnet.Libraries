using System.Threading;
using System.Threading.Tasks;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 이동/크기/회전 통합 커맨드(adorner 편집). before/after=위치·너비·높이·방위.
   Note         : Map_Edit_Undo_Redo FR-03. before/after는 MarkerEditCompletedEventArgs에서 확보.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class TransformCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly (PointLatLng pos, double w, double h, double bearing) _before, _after;

    public TransformCommand(IUndoApplyContext ctx, int markerId,
        (PointLatLng pos, double w, double h, double bearing) before,
        (PointLatLng pos, double w, double h, double bearing) after) : base(ctx)
    {
        _id = markerId; _before = before; _after = after;
    }

    public override string Description => "심볼 이동/크기/회전";

    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);

    private async Task Apply((PointLatLng pos, double w, double h, double bearing) s, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id);
        if (m == null) return;
        m.UpdateSize(s.w, s.h);
        m.UpdateRotation(s.bearing);
        m.UpdateLocation(s.pos);
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}
