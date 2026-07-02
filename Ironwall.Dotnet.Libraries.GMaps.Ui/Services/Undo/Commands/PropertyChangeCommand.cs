using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 속성패널 단일 속성 변경 커맨드(Title/색/두께/ShowShape/Zoom…). before/after=값.
   Note         : Map_Edit_Undo_Redo FR-04. After는 coalescing(연속 변경 병합)용 internal set.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class PropertyChangeCommand : UndoableCommandBase
{
    private readonly int _id;
    public string Property { get; }
    public int MarkerId => _id;
    public object? Before { get; }
    /// <summary>coalescing 시 최신값으로 갱신(EditRecorder 전용).</summary>
    public object? After { get; internal set; }

    public PropertyChangeCommand(IUndoApplyContext ctx, int markerId, string property, object? before, object? after) : base(ctx)
    {
        _id = markerId; Property = property; Before = before; After = after;
    }

    public override string Description => $"속성 변경: {Property}";

    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(After, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(Before, ct);

    private async Task Apply(object? v, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id);
        if (m == null) return;
        ApplyProperty(m, Property, v);
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}
