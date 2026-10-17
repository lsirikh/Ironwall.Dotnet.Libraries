using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;

/// <summary>
/// 템플릿 구성 순서 끌어 놓기 — 커널(<see cref="IDragDropHandler"/>)이 부르는 판정 · 처리.
/// </summary>
/// <remarks>
/// <para>설계 근거: all-windows-drag-wireframe.html <b>L355-356</b> 은 보고서 콘솔의 드래그를
/// "템플릿 구성 순서(확인)" 하나로 적고 <b>"서버에 순서 필드가 있는지 확인 후 결정"</b> 하라고 했다.
/// 확인 결과 <c>ReportComponentConfigDto.Order</c>(<c>order</c>)가 생성 · 수정 본문 양쪽에 있다 → 만든다.</para>
/// <para><b>서버를 부르지 않는다.</b> 순서는 보드 안에서만 바뀌고 [적용] 한 번에 PATCH 1건으로 나간다
/// (드래그 규칙: 행마다 순차 PATCH 금지 — 드래그 1회가 N 왕복이 된다).</para>
/// <para>키보드 폴백(Alt+위/아래)은 커널 <c>ReorderKeyboardBehavior</c> 가 <b>같은 담당</b>을 부르므로
/// 결과가 같다 — 회귀 단언은 그 경로로 잡는다.</para>
/// </remarks>
public sealed class TemplateComponentDropHandler : IDragDropHandler
{
    /// <summary>드롭존 종류. 뷰의 목록에 <c>drag:DropZone.Key</c> 로 붙인다.</summary>
    public const string ZoneKey = "report-template-component";

    private readonly TemplateComponentBoard _board;
    private readonly Func<bool> _canEdit;

    /// <param name="board">순서를 쥔 보드.</param>
    /// <param name="canEdit">읽기 전용(권한 없음 · 적재 중)이면 거짓을 돌려준다.</param>
    public TemplateComponentDropHandler(TemplateComponentBoard board, Func<bool>? canEdit = null)
    {
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _canEdit = canEdit ?? (() => true);
    }

    /// <summary>순서가 실제로 바뀌었다 — 뷰모델이 구독해 미적용 칸을 갱신한다.</summary>
    public event EventHandler? Reordered;

    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (payload is null || target is null) return false;
        if (!string.Equals(target.ZoneKey, ZoneKey, StringComparison.Ordinal)) return false;
        if (!target.IsReorder) return false;                 // 이 화면에는 순서 드롭존만 있다
        if (!_canEdit()) return false;
        if (payload.Items.Count == 0) return false;

        // 다른 목록에서 끌어온 것은 받지 않는다(같은 보드 안 이동만).
        return payload.Items.All(item => item is TemplateComponentItem c && _board.Items.Contains(c));
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (!CanDrop(payload, target)) return;
        if (_board.Move(payload.IndexesIn(_board.Items), target.InsertionIndex))
            Reordered?.Invoke(this, EventArgs.Empty);
    }
}
