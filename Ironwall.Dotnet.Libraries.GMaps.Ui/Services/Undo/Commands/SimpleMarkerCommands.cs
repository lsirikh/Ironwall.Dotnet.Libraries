using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 단일 마커 값 커맨드 3종 — 라벨오프셋/잠금/이름. Map_Edit_Undo_Redo FR-05/09.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>라벨 오프셋 드래그 취소/재실행 — 도메인은 마커 타입 따름: 이미지=U/V(정규화, Overlay_Title FR-02) / 그 외=px.</summary>
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
        if (m is GMapSymbols.IImageEditableMarker img) { img.LabelOffsetU = s.x; img.LabelOffsetV = s.y; }   // U/V INPC → 어도너 즉시 재렌더(P1-04)
        else { m.LabelOffsetX = s.x; m.LabelOffsetY = s.y; }
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
    }
}

/// <summary>라벨 폭 WYSIWYG 조절 취소/재실행 — edge-pinned 리사이즈가 (오프셋,폭)을 함께 바꾸므로 쌍을 원자 복원(Overlay_Title FR-13).
/// 오프셋 도메인은 마커 타입 따름(이미지=U/V, 그 외=px).</summary>
public sealed class TitleWidthResizeCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly bool _isImage;
    private readonly (double a, double b, double w) _before, _after;
    public TitleWidthResizeCommand(IUndoApplyContext ctx, int id, (double a, double b, double w) before,
        (double a, double b, double w) after, bool isImage = false) : base(ctx)
    { _id = id; _before = before; _after = after; _isImage = isImage; }

    public override string Description => "라벨 폭 조절";
    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(_after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(_before, ct);
    private async Task Apply((double a, double b, double w) s, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, _isImage); if (m == null) return;
        if (m is GMapSymbols.IImageEditableMarker img) { img.LabelOffsetU = s.a; img.LabelOffsetV = s.b; }
        else { m.LabelOffsetX = s.a; m.LabelOffsetY = s.b; }
        m.TitleMaxWidth = s.w;
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
        Ctx.SyncMarkerNode(_id, _isImage);   // 타겟 노드 잠금아이콘 갱신(타입인지 — 같은 Id 반대타입 오염 차단)
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
        Ctx.SyncMarkerNode(_id, _isImage);   // 타겟 노드 이름 갱신(타입인지 — 같은 Id 반대타입 오염 차단)
    }
}
