using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : Undo/Redo 한 스텝(편집 하나)을 캡슐화한 커맨드. 편집 직전(before)·직후(after)
                  상태를 품고, Execute(=Redo, after 적용)·Undo(before 적용)를 화면+DB에 반영.
   Note         : Map_Edit_Undo_Redo FR-01. 커맨드는 IUndoApplyContext로만 앱에 적용(MapViewModel 비의존).
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public interface IUndoableCommand
{
    /// <summary>사람이 읽는 설명(툴팁/히스토리). 예: "심볼 이동", "속성 변경", "그룹 삭제 3개".</summary>
    string Description { get; }

    /// <summary>이 커맨드가 속한 맵 Id(맵별 스택 파티션). 0=기본.</summary>
    int ScopeMapId { get; set; }

    /// <summary>Redo — 편집 직후(after) 상태를 재적용.</summary>
    Task ExecuteAsync(CancellationToken ct = default);

    /// <summary>Undo — 편집 직전(before) 상태를 적용.</summary>
    Task UndoAsync(CancellationToken ct = default);
}
