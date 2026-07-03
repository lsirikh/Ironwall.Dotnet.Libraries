using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : v2 커버리지 커맨드 — 런타임 가시성 / 파일이미지 회전 / MapLayers 노드.
                  Map_Edit_Undo_Redo v2. seam(IUndoApplyContext)으로만 적용.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>런타임 가시성 토글(개별 심볼 / 그룹 멤버) — DB 미영속, marker ShowShape/IsLayerEnabled/IsVisible만.</summary>
public sealed class VisibilityCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly bool _before, _after;
    public VisibilityCommand(IUndoApplyContext ctx, int id, bool before, bool after) : base(ctx)
    { _id = id; _before = before; _after = after; }

    public override string Description => _after ? "표시" : "숨김";
    public override Task ExecuteAsync(CancellationToken ct = default) => Ctx.ApplyVisibilityAsync(_id, _after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Ctx.ApplyVisibilityAsync(_id, _before, ct);
}

/// <summary>파일 기반 오버레이 이미지(GMapCustomImage) 회전 프리셋 취소/재실행(UserRotation, UpdateImageAsync 영속).</summary>
public sealed class CustomImageRotationCommand : UndoableCommandBase
{
    private readonly int _id;
    private readonly double _before, _after;
    public CustomImageRotationCommand(IUndoApplyContext ctx, int id, double before, double after) : base(ctx)
    { _id = id; _before = before; _after = after; }

    public override string Description => "이미지 회전";
    public override Task ExecuteAsync(CancellationToken ct = default) => Ctx.ApplyCustomImageRotationAsync(_id, _after, ct);
    public override Task UndoAsync(CancellationToken ct = default) => Ctx.ApplyCustomImageRotationAsync(_id, _before, ct);
}

/// <summary>MapLayers 노드 필드(이름/투명도/ZOrder) 스냅샷 — null=미변경.</summary>
public readonly record struct LayerFields(int Id, string? Name, double? Opacity, int? ZOrder);

/// <summary>레이어 패널 노드 조작(이름 변경 / ZOrder 스왑) 취소·재실행 — MapLayers 테이블 필드 기준.</summary>
public sealed class LayerNodeCommand : UndoableCommandBase
{
    private readonly string _desc;
    private readonly IReadOnlyList<LayerFields> _before, _after;
    public LayerNodeCommand(IUndoApplyContext ctx, string desc, IReadOnlyList<LayerFields> before, IReadOnlyList<LayerFields> after) : base(ctx)
    { _desc = desc; _before = before; _after = after; }

    public override string Description => _desc;
    public override async Task ExecuteAsync(CancellationToken ct = default)
    { foreach (var f in _after) await Ctx.ApplyLayerFieldsAsync(f.Id, f.Name, f.Opacity, f.ZOrder, ct).ConfigureAwait(false); }
    public override async Task UndoAsync(CancellationToken ct = default)
    { foreach (var f in _before) await Ctx.ApplyLayerFieldsAsync(f.Id, f.Name, f.Opacity, f.ZOrder, ct).ConfigureAwait(false); }
}
