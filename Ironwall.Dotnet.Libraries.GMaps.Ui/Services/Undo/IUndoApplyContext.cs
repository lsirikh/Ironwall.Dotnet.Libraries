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

    /// <summary>타입 인지 조회 — Id 네임스페이스 충돌(이미지 Images.Id ↔ 심볼 Symbols.Id, 같은 Markers 컬렉션) 시
    /// 올바른 대상만 반환. isImage=true→이미지 마커, false→비이미지(심볼). undo가 엉뚱한 마커 변형(데이터손상) 방지.</summary>
    IEditableMarker? FindMarkerById(int id, bool isImage);

    /// <summary>호출자가 이미 마커 모델/속성을 세팅한 상태 → 타입별 DbUpdate 영속 + 시각/트리 동기화.</summary>
    Task ApplyMarkerUpdateAsync(IEditableMarker marker, CancellationToken ct = default);

    /// <summary>스냅샷으로 삭제된 심볼 복원(Id 보존 DB Restore + 마커 재생성 + 맵/provider/트리 추가). 실패 시 null.</summary>
    Task<IEditableMarker?> RestoreDeletedAsync(ISymbolSnapshot snapshot, CancellationToken ct = default);

    /// <summary>마커 제거(DB 삭제 + 맵/provider/트리 제거). Add 취소 시 사용.</summary>
    Task RemoveMarkerAsync(IEditableMarker marker, CancellationToken ct = default);

    /// <summary>ZOrder 일괄 적용((id,zOrder) 페어) + 렌더순서 반영.</summary>
    Task ApplyZOrderAsync(IReadOnlyList<(int id, int zOrder)> pairs, CancellationToken ct = default);

    /// <summary>레이어 트리 전체 재구성(추가/삭제 후 — 전체 리로드).</summary>
    void ResyncTree();

    /// <summary>단일 심볼 리프 노드만 라이브 마커 기준으로 갱신(이름/잠금/체크). 전체 리로드 없이(열린 패널 dispose 회피).</summary>
    void SyncMarkerNode(int id);

    // ── v2 커버리지 seam ──
    /// <summary>런타임 가시성(ShowShape/IsLayerEnabled/IsVisible) 적용 + 시각·트리 노드 갱신(DB 미영속).</summary>
    Task ApplyVisibilityAsync(int id, bool show, CancellationToken ct = default);

    /// <summary>파일 오버레이 이미지(GMapCustomImage) 회전 적용 + UpdateImageAsync 영속.</summary>
    Task ApplyCustomImageRotationAsync(int id, double rotation, CancellationToken ct = default);

    /// <summary>파일 오버레이 이미지 이동/크기/회전(ImageBounds+UserRotation) 적용 + UpdateImageAsync 영속(D1 핸들 편집).</summary>
    Task ApplyCustomImageEditAsync(int id, GMap.NET.RectLatLng bounds, double rotation, CancellationToken ct = default);

    /// <summary>MapLayers 노드 필드(이름/투명도/ZOrder, null=미변경) 적용 + 이미지마커 동기화 + 트리 리로드.</summary>
    Task ApplyLayerFieldsAsync(int layerId, string? name, double? opacity, int? zOrder, CancellationToken ct = default);
}
