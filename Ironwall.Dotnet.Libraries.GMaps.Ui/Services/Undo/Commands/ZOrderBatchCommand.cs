using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : ZOrder 일괄 변경(단일 swap·밴드 정규화) 취소·재실행. before/after=(id,zOrder) 페어.
   Note         : Map_Edit_Undo_Redo FR-08. BatchUpdateZOrderAsync로 원자 적용.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class ZOrderBatchCommand : UndoableCommandBase
{
    private readonly IReadOnlyList<(bool isImage, int id, int zOrder)> _before, _after;
    public ZOrderBatchCommand(IUndoApplyContext ctx,
        IReadOnlyList<(bool isImage, int id, int zOrder)> before, IReadOnlyList<(bool isImage, int id, int zOrder)> after) : base(ctx)
    { _before = before; _after = after; }

    public override string Description => "순서 변경";
    public override async Task ExecuteAsync(CancellationToken ct = default)
    { await Ctx.ApplyZOrderAsync(_after, ct).ConfigureAwait(false); }
    public override async Task UndoAsync(CancellationToken ct = default)
    { await Ctx.ApplyZOrderAsync(_before, ct).ConfigureAwait(false); }
}
