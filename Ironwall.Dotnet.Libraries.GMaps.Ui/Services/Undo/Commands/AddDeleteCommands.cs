using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 심볼 추가/삭제 커맨드 — 스냅샷 기반(삭제 취소=Id 보존 복원).
   Note         : Map_Edit_Undo_Redo FR-06. RestoreDeletedAsync/RemoveMarkerAsync는 컨텍스트가 DB Restore/Delete 처리.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>심볼 추가 — Redo=재추가(복원), Undo=제거.</summary>
public sealed class AddSymbolCommand : UndoableCommandBase
{
    private readonly ISymbolSnapshot _snapshot;
    public AddSymbolCommand(IUndoApplyContext ctx, ISymbolSnapshot snapshot) : base(ctx) => _snapshot = snapshot;

    public override string Description => "심볼 추가";

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Redo of add = 스냅샷으로 재복원(Id 보존)
        await Ctx.RestoreDeletedAsync(_snapshot, ct).ConfigureAwait(false);
    }

    public override async Task UndoAsync(CancellationToken ct = default)
    {
        var m = Ctx.FindMarkerById(_snapshot.Id, _snapshot.IsImage);   // 타입인지 — 같은 Id의 반대타입 마커 오제거 차단
        if (m != null) await Ctx.RemoveMarkerAsync(m, ct).ConfigureAwait(false);
    }
}

/// <summary>심볼 삭제 — Redo=제거, Undo=Id 보존 복원(사전 스냅샷).</summary>
public sealed class DeleteSymbolCommand : UndoableCommandBase
{
    private readonly ISymbolSnapshot _snapshot;
    public DeleteSymbolCommand(IUndoApplyContext ctx, ISymbolSnapshot snapshot) : base(ctx) => _snapshot = snapshot;

    public override string Description => "심볼 삭제";

    public override async Task ExecuteAsync(CancellationToken ct = default)
    {
        var m = Ctx.FindMarkerById(_snapshot.Id, _snapshot.IsImage);   // 타입인지 — 같은 Id의 반대타입 마커 오제거 차단
        if (m != null) await Ctx.RemoveMarkerAsync(m, ct).ConfigureAwait(false);
    }

    public override async Task UndoAsync(CancellationToken ct = default)
    {
        await Ctx.RestoreDeletedAsync(_snapshot, ct).ConfigureAwait(false);
    }
}
