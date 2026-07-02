using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 배치(그룹) 편집을 1 undo 단위로 묶는 복합 커맨드. Execute=자식 정순, Undo=자식 역순.
   Note         : Map_Edit_Undo_Redo FR-07. 그룹 이동/삭제·밴드 정규화 등.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class MacroCommand : IUndoableCommand
{
    private readonly IReadOnlyList<IUndoableCommand> _children;
    public MacroCommand(string description, IEnumerable<IUndoableCommand> children)
    {
        Description = description;
        _children = children.ToList();
    }

    public string Description { get; }
    public int ScopeMapId { get; set; }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        foreach (var c in _children) await c.ExecuteAsync(ct).ConfigureAwait(false);
    }

    public async Task UndoAsync(CancellationToken ct = default)
    {
        for (int i = _children.Count - 1; i >= 0; i--) await _children[i].UndoAsync(ct).ConfigureAwait(false);
    }
}
