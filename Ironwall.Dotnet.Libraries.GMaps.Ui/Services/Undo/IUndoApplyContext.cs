using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : 커맨드가 앱(맵/모델/DB/트리)에 적용하는 최소 seam. **MapViewModel이 구현**(다음 Phase).
                  커맨드는 이 인터페이스에만 의존 → MapViewModel 비의존, 테스트 용이.
   Note         : Map_Edit_Undo_Redo FR-06/10. 모든 메서드는 Undo/Redo 재적용 시 호출되며,
                  구현부는 SuspendRecording 하에서 실행되어야 유령 커맨드/암묵저장 재기록을 막음.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public interface IUndoApplyContext
{
    /// <summary>DB 서비스(커맨드가 필요 시 직접 사용 — 주로 Restore).</summary>
    IGMapDbSymbolService Db { get; }

    /// <summary>현재 맵의 라이브 마커를 Id로 조회(없으면 null).</summary>
    IEditableMarker? FindMarkerById(int id);

    /// <summary>호출자가 이미 마커 모델/속성을 세팅한 상태 → 타입별 DbUpdate 영속 + 시각/트리 동기화.</summary>
    Task ApplyMarkerUpdateAsync(IEditableMarker marker, CancellationToken ct = default);

    /// <summary>스냅샷으로 삭제된 심볼 복원(Id 보존 DB Restore + 마커 재생성 + 맵/provider/트리 추가). 실패 시 null.</summary>
    Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot snapshot, CancellationToken ct = default);

    /// <summary>마커 제거(DB 삭제 + 맵/provider/트리 제거). Add 취소 시 사용.</summary>
    Task RemoveMarkerAsync(IEditableMarker marker, CancellationToken ct = default);

    /// <summary>ZOrder 일괄 적용((id,zOrder) 페어) + 렌더순서 반영.</summary>
    Task ApplyZOrderAsync(IReadOnlyList<(int id, int zOrder)> pairs, CancellationToken ct = default);

    /// <summary>레이어 트리 노드 동기화(잠금/가시성/이름 반영).</summary>
    void ResyncTree();
}
