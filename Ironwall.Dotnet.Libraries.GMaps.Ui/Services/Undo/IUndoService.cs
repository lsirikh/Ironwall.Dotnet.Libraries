using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : Undo/Redo 스택 소유·실행 서비스. 편집 사후기록(Push)·되돌리기·재실행·맵별 파티션·배치.
   Note         : Map_Edit_Undo_Redo FR-01/12. Push는 재실행하지 않고 기록만(이미 실행된 편집).
                  Undo/Redo는 직렬화(재진입 방지, NFR-06) + SuspendRecording으로 유령기록 차단.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public interface IUndoService
{
    /// <summary>현재 맵 Id(스택 파티션 키). 맵 전환 시 세팅.</summary>
    int CurrentMapId { get; set; }

    bool CanUndo { get; }
    bool CanRedo { get; }
    string? NextUndoDescription { get; }
    string? NextRedoDescription { get; }

    /// <summary>스택 상태 변경(Push/Undo/Redo/Clear) 통지 — 버튼 CanExecute·툴팁 갱신용.</summary>
    event EventHandler? StateChanged;

    /// <summary>이미 실행된 편집을 기록(재실행 안 함). Redo 스택 비움 + 깊이 상한 트림. 기록 억제 중이면 무시.</summary>
    void Push(IUndoableCommand command);

    /// <summary>마지막 편집 되돌리기. 성공 시 true. 직렬화·억제 하에 실행.</summary>
    Task<bool> UndoAsync(CancellationToken ct = default);

    /// <summary>되돌린 편집 재실행. 성공 시 true.</summary>
    Task<bool> RedoAsync(CancellationToken ct = default);

    /// <summary>스택 비우기. mapId=null이면 현재 맵, 지정 시 해당 맵(음수 등 특수값=전체는 구현부 규약).</summary>
    void Clear(int? mapId = null);

    /// <summary>배치 스코프 시작 — 반환 IDisposable Dispose까지의 Push들을 MacroCommand 1건으로 묶음.</summary>
    IDisposable BeginBatch(string description);

    /// <summary>기록 억제 스코프 — Dispose까지 Push 무시(Undo/Redo 재적용 중 유령기록 방지).</summary>
    IDisposable SuspendRecording();

    /// <summary>기록 억제 중 여부.</summary>
    bool IsRecordingSuspended { get; }
}
