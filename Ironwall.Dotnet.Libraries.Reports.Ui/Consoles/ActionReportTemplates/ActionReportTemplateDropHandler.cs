using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.ActionReportTemplates;

/// <summary>순서가 실제로 바뀌었다 — 커밋(<c>POST /reorder</c>) 전 순서를 함께 들려 준다(실패 시 되돌리기 재료).</summary>
public sealed class ActionReportTemplateReorderedEventArgs : EventArgs
{
    public ActionReportTemplateReorderedEventArgs(IReadOnlyList<int> previousOrderIds) => PreviousOrderIds = previousOrderIds;

    /// <summary>이 이동 전 화면 순서(id) — 커밋 실패 시 되돌리는 기준이 아니라(그때는 서버 재조회),
    /// 커밋 <b>성공</b> 시 "되돌리기(Undo)" 단추가 다시 보낼 순서다.</summary>
    public IReadOnlyList<int> PreviousOrderIds { get; }
}

/// <summary>
/// 조치보고 문구 순서 끌어 놓기 — 커널(<see cref="IDragDropHandler"/>)이 부르는 판정 · 처리.
/// </summary>
/// <remarks>
/// <para>마우스 드래그(<c>CaptureDragBehavior</c>) · 키보드 폴백(<c>ReorderKeyboardBehavior</c>, Alt+↑/↓) ·
/// ▲▼ 단추(뷰 코드비하인드) <b>세 경로가 전부 이 클래스의 <see cref="Drop"/> 하나</b>를 부른다 —
/// 결과가 같아야 회귀 단언이 성립한다(레포 관행, <c>TemplateComponentDropHandler</c> 동일 계약).</para>
/// <para>서버를 직접 부르지 않는다 — 로컬 이동만 하고 <see cref="Reordered"/> 로 알리면
/// 소유 뷰모델이 <c>ReorderTemplatesAsync</c> 단일 호출로 커밋한다.</para>
/// </remarks>
public sealed class ActionReportTemplateDropHandler : IDragDropHandler
{
    /// <summary>드롭존 종류. 뷰의 목록에 <c>drag:DropZone.Key</c> 로 붙인다.</summary>
    public const string ZoneKey = "action-report-template";

    private readonly ActionReportTemplateBoard _board;
    private readonly Func<bool> _canEdit;

    /// <param name="board">순서를 쥔 보드.</param>
    /// <param name="canEdit">읽기 전용(권한 없음 · 적재 중 · 커밋 중)이면 거짓을 돌려준다.</param>
    public ActionReportTemplateDropHandler(ActionReportTemplateBoard board, Func<bool>? canEdit = null)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _canEdit = canEdit ?? (() => true);
    }

    /// <summary>순서가 실제로 바뀌었다 — 뷰모델이 구독해 서버에 커밋한다.</summary>
    public event EventHandler<ActionReportTemplateReorderedEventArgs>? Reordered;

    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (payload is null || target is null) return false;
        if (!string.Equals(target.ZoneKey, ZoneKey, StringComparison.Ordinal)) return false;
        if (!target.IsReorder) return false;                 // 이 화면에는 순서 드롭존만 있다
        if (!_canEdit()) return false;
        if (payload.Items.Count == 0) return false;

        // 다른 목록에서 끌어온 것은 받지 않는다(같은 보드 안 이동만).
        return payload.Items.All(item => item is ActionReportTemplateItem i && _board.Items.Contains(i));
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (!CanDrop(payload, target)) return;
        var previousOrder = _board.OrderedIds;
        if (_board.Move(payload.IndexesIn(_board.Items), target.InsertionIndex))
            Reordered?.Invoke(this, new ActionReportTemplateReorderedEventArgs(previousOrder));
    }

    /// <summary>▲▼ 단추 · 그 밖의 코드 경로가 같은 판정을 타도록 만드는 생성 헬퍼(마우스 드래그와 동일 코드path).</summary>
    public static DropTarget TargetFor(string zoneKey, int insertionIndex) => new(zoneKey, null, insertionIndex);
}
