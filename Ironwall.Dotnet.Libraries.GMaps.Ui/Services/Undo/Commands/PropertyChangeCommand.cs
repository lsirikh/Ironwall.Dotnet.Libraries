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
    private readonly bool _isImage;   // Id 충돌 대비 대상 타입 캡처(이미지↔심볼 오적용 방지 — TransformCommand 동형)
    public string Property { get; }
    public int MarkerId => _id;
    public object? Before { get; }
    /// <summary>coalescing 시 최신값으로 갱신(EditRecorder 전용).</summary>
    public object? After { get; internal set; }

    public PropertyChangeCommand(IUndoApplyContext ctx, int markerId, string property, object? before, object? after, bool isImage = false) : base(ctx)
    {
        _id = markerId; Property = property; Before = before; After = after; _isImage = isImage;
    }

    public override string Description => $"속성 변경: {Property}";

    public override Task ExecuteAsync(CancellationToken ct = default) => Apply(After, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Apply(Before, ct);

    private async Task Apply(object? v, CancellationToken ct)
    {
        var m = Ctx.FindMarkerById(_id, _isImage);   // 타입인지 조회 — 속성창 이미지 크기(Width/Height) undo가 엉뚱한 Controller에 적용되던 손상 차단
        if (m == null) return;
        ApplyProperty(m, Property, v);
        await Ctx.ApplyMarkerUpdateAsync(m, ct).ConfigureAwait(false);
        // 트리 노드에 표시되는 속성(이름/체크/잠금)은 노드 캐시가 별도 → 타겟 동기화(전체 리로드 회피).
        // 타입인지 — 속성창 이미지 이름변경 undo가 같은 Id 심볼(제어기1)을 패널에 표시하던 손상 차단.
        if (Property is "Title" or "ShowShape" or "IsLocked")
            Ctx.SyncMarkerNode(_id, _isImage);
    }
}
