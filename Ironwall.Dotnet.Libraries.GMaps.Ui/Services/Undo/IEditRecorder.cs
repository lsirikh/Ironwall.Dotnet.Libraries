using System;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : 편집을 커맨드로 사후기록하는 파사드 — MapViewModel 의미 핸들러에서 호출.
   Note         : Map_Edit_Undo_Redo FR-02/04. Context(IUndoApplyContext)는 MapViewModel이 세팅(순환주입 회피).
                  속성 변경은 (markerId,prop) 기준 coalescing. 억제 중/Context null이면 무시.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public interface IEditRecorder
{
    /// <summary>커맨드가 앱에 적용되는 seam — MapViewModel이 자신을 세팅.</summary>
    IUndoApplyContext? Context { get; set; }

    /// <summary>선택 시 baseline 캡처(before 없는 라벨 등에 사용).</summary>
    void CaptureSelectionBaseline(IEditableMarker marker);

    /// <summary>이동/크기/회전 완료 기록(before/after는 args).</summary>
    void RecordTransform(MarkerEditCompletedEventArgs e);

    /// <summary>라인/폴리곤 리사이즈(점 스케일) 완료 기록 — before=드래그시작 점+위치(명시 전달), after=현재 마커 점+위치.
    /// **`HasChanges` 게이트 무관·점/위치 실제 diff로 판정**(line 스케일은 W/H 불변이라 HasChanges=false → 기존 경로는 undo 미기록+즉시영속 파괴적, §5-C R-07). LineArea_Symbol_Resize FR-04.</summary>
    void RecordLineGeometry(IEditableMarker marker, System.Collections.Generic.List<PointLatLng> beforePoints, PointLatLng beforePosition);

    /// <summary>속성 변경 기록(coalescing).</summary>
    void RecordPropertyChange(IEditableMarker marker, string property, object? oldValue, object? newValue);

    /// <summary>라벨 오프셋 드래그 완료 기록(before=드래그 시작 오프셋 명시 전달, after=현재). 선택 여부 무관.</summary>
    void RecordLabelOffset(IEditableMarker marker, double beforeX, double beforeY);

    /// <summary>심볼 추가 기록(after=현재 모델, Id 포함).</summary>
    void RecordAdd(IEditableMarker marker);

    /// <summary>삭제 직전 완전 스냅샷 확보(호출부가 이후 RecordDelete에 전달).</summary>
    ISymbolSnapshot? CaptureForDelete(IEditableMarker marker);

    /// <summary>삭제 기록(사전 스냅샷).</summary>
    void RecordDelete(ISymbolSnapshot? snapshot);

    /// <summary>ZOrder 일괄 변경 기록. isImage=밴드 구분(이미지↔심볼 같은 Id 충돌 시 undo가 올바른 마커에 적용).</summary>
    void RecordZOrder(System.Collections.Generic.IReadOnlyList<(bool isImage, int id, int zOrder)> before,
                      System.Collections.Generic.IReadOnlyList<(bool isImage, int id, int zOrder)> after);

    /// <summary>위치 변경(그룹 이동 멤버) 기록 — before=이동 전 위치, after=현재 마커 위치.</summary>
    void RecordPositionChange(IEditableMarker marker, PointLatLng before);

    /// <summary>잠금 변경 기록(레이어트리/개별 심볼).</summary>
    void RecordLock(IEditableMarker marker, bool before, bool after);

    /// <summary>이름(Title) 변경 기록(레이어트리).</summary>
    void RecordRename(IEditableMarker marker, string before, string after);

    /// <summary>배치 스코프(그룹 이동/삭제 등) — Dispose까지 1 undo 단위.</summary>
    IDisposable BeginBatch(string description);

    // ── v2 커버리지 기록 ──
    /// <summary>런타임 가시성 토글 기록(개별/그룹, DB 미영속).</summary>
    void RecordVisibility(IEditableMarker marker, bool before, bool after);

    /// <summary>파일 오버레이 이미지 회전 기록.</summary>
    void RecordCustomImageRotation(int imageId, double before, double after);

    /// <summary>파일 오버레이 이미지 핸들 편집(이동/크기/회전) 기록(D1) — before/after Bounds+Rotation.</summary>
    void RecordCustomImageEdit(int imageId, GMap.NET.RectLatLng beforeBounds, double beforeRotation, GMap.NET.RectLatLng afterBounds, double afterRotation);

    /// <summary>MapLayers 노드 조작(이름/ZOrder) 기록.</summary>
    void RecordLayerChange(string description,
        System.Collections.Generic.IReadOnlyList<Commands.LayerFields> before,
        System.Collections.Generic.IReadOnlyList<Commands.LayerFields> after);
}
