using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 배치(그룹) 편집을 1 undo 단위로 묶는 복합 커맨드. Execute=자식 정순, Undo=자식 역순.
   Note         : Map_Edit_Undo_Redo FR-07. 그룹 이동/삭제·밴드 정규화 등.
                  ★ 자식별 try/catch — 한 자식(예: 백그라운드 스레드서 UI 접근한 라인/그룹 마커)이
                  예외를 던져도 나머지 형제 취소/재실행이 중단되지 않게 함(그룹이동 "하나만 복원" 회귀 방지).
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class MacroCommand : IUndoableCommand
{
    private readonly IReadOnlyList<IUndoableCommand> _children;
    private readonly ILogService? _log;

    public MacroCommand(string description, IEnumerable<IUndoableCommand> children, ILogService? log = null)
    {
        Description = description;
        _children = children.ToList();
        _log = log;
    }

    public string Description { get; }
    public int ScopeMapId { get; set; }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        foreach (var c in _children)
        {
            try { await c.ExecuteAsync(ct).ConfigureAwait(false); }
            catch (Exception ex) { _log?.Error($"[Undo] 매크로 자식 재실행 실패(계속): {ex.Message}"); }
        }
    }

    public async Task UndoAsync(CancellationToken ct = default)
    {
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            try { await _children[i].UndoAsync(ct).ConfigureAwait(false); }
            catch (Exception ex) { _log?.Error($"[Undo] 매크로 자식 취소 실패(계속): {ex.Message}"); }
        }
    }
}
