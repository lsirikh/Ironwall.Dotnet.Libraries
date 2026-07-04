using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 단일 마커 값 커맨드 3종 — 라벨오프셋/잠금/이름. Map_Edit_Undo_Redo FR-05/09.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>라벨 오프셋(X,Y) 드래그 취소/재실행.</summary>
public sealed class LabelOffsetCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly bool _isImage;   // Id 충돌 대비 대상 타입 캡처(TransformCommand 동형)
    private readonly (double x, double y) _before, _after;
    public LabelOffsetCommand(IUndoApplyContext ctx, int id, (double x, double y) before, (double x, double y) after, bool isImage = false) : base(ctx)
    { _id = id; _before = before; _after = after; _isImage = isImage; }

    public override string Description => "라벨 위치 이동";
    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);
    private async Task Apply((double x, double y) s, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, _isImage); if (m == null) return;
        m.LabelOffsetX = s.x; m.LabelOffsetY = s.y;
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}

/// <summary>잠금/해제 취소·재실행.</summary>
public sealed class LockCommand : UndoableCommandBase
{
    private readonly int _id; private readonly bool _before, _after;
    private readonly bool _isImage;   // Id 충돌 대비 대상 타입 캡처(이미지도 잠금 대상 — TransformCommand 동형)
    public LockCommand(IUndoApplyContext ctx, int id, bool before, bool after, bool isImage = false) : base(ctx)
    { _id = id; _before = before; _after = after; _isImage = isImage; }

    public override string Description => _after ? "심볼 잠금" : "심볼 잠금 해제";
    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);
    private async Task Apply(bool v, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, _isImage); if (m == null) return;
        m.IsLocked = v;
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
        Ctx.SyncMarkerNode(_id);   // 타겟 노드 잠금아이콘 갱신(전체 리로드→열린 패널 dispose 회피)
    }
}

/// <summary>이름(Title) 변경 취소·재실행.</summary>
public sealed class RenameSymbolCommand : UndoableCommandBase
{
    private readonly int _id; private readonly string _before, _after;
    private readonly bool _isImage;   // Id 충돌 대비 대상 타입 캡처(이미지도 이름변경 대상 — TransformCommand 동형)
    public RenameSymbolCommand(IUndoApplyContext ctx, int id, string before, string after, bool isImage = false) : base(ctx)
    { _id = id; _before = before ?? string.Empty; _after = after ?? string.Empty; _isImage = isImage; }

    public override string Description => "이름 변경";
    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);
    private async Task Apply(string v, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, _isImage); if (m == null) return;
        m.Title = v;
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
        Ctx.SyncMarkerNode(_id);   // 타겟 노드 이름 갱신(전체 리로드→열린 패널 dispose 회피)
    }
}
