using System;
using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;

/****************************************************************************
   Purpose      : IEditRecorder 구현 — 편집 이벤트를 커맨드로 변환·Push. 속성 coalescing.
   Note         : Map_Edit_Undo_Redo FR-02/04. Context null·억제 중이면 무시.
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class EditRecorder : IEditRecorder
{
    private readonly IUndoService _undo;
    private readonly ILogService? _log;

    // 라벨 before용 baseline(마커 Id → 오프셋)
    private readonly Dictionary<int, (double x, double y)> _labelBaseline = new();

    // 속성 coalescing 상태
    private PropertyChangeCommand? _lastProp;
    private int _lastPropId;
    private string? _lastPropName;

    public EditRecorder(IUndoService undo, ILogService? log = null) { _undo = undo; _log = log; }

    public IUndoApplyContext? Context { get; set; }

    private bool Ready => Context != null && !_undo.IsRecordingSuspended;
    private void ResetCoalesce() { _lastProp = null; _lastPropName = null; }

    public void CaptureSelectionBaseline(IEditableMarker marker)
    {
        if (marker == null) return;
        _labelBaseline[marker.Id] = (marker.LabelOffsetX, marker.LabelOffsetY);
        ResetCoalesce();   // 선택 전환 → 병합 종료
    }

    public void RecordTransform(MarkerEditCompletedEventArgs e)
    {
        if (!Ready || e?.Marker == null || !e.HasChanges) return;
        ResetCoalesce();
        try
        {
            var before = (e.OriginalPosition, e.OriginalWidth, e.OriginalHeight, e.OriginalBearing);
            var after = (e.FinalPosition, e.FinalSize.Width, e.FinalSize.Height, e.FinalBearing);
            _undo.Push(new TransformCommand(Context!, e.Marker.Id, e.Marker is GMapImageMarker, before, after, _log));
        }
        catch (Exception ex) { _log?.Error($"RecordTransform 실패: {ex.Message}"); }
    }

    public void RecordLineGeometry(IEditableMarker marker, List<PointLatLng> beforePoints, PointLatLng beforePosition)
    {
        // ★ HasChanges 게이트 없음 — line 스케일은 _model.Width 불변이라 HasChanges=false. 점/위치 실제 diff로 판정(§5-C R-07).
        if (!Ready || marker is not ILineEditableMarker line || beforePoints == null) return;
        ResetCoalesce();
        try
        {
            var afterPoints = line.RuntimePoints;   // 현재(스케일된) 점
            var afterPosition = marker.Position;
            if (SamePoints(beforePoints, afterPoints) && SamePos(beforePosition, afterPosition)) return;   // 실제 변화 없으면 미기록
            _undo.Push(new LineGeometryCommand(Context!, marker.Id,
                (new List<PointLatLng>(beforePoints), beforePosition),
                (new List<PointLatLng>(afterPoints), afterPosition)));
        }
        catch (Exception ex) { _log?.Error($"RecordLineGeometry 실패: {ex.Message}"); }
    }

    private static bool SamePos(PointLatLng a, PointLatLng b) => a.Lat == b.Lat && a.Lng == b.Lng;
    private static bool SamePoints(List<PointLatLng> a, List<PointLatLng> b)
    {
        if (a == null || b == null || a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Lat != b[i].Lat || a[i].Lng != b[i].Lng) return false;
        return true;
    }

    public void RecordPropertyChange(IEditableMarker marker, string property, object? oldValue, object? newValue)
    {
        if (!Ready || marker == null || string.IsNullOrEmpty(property)) return;
        if (!Commands.UndoableCommandBase.IsReplayableProperty(property)) return;   // 복원 불가 속성=죽은 undo 엔트리 방지(CMD-02)
        if (Equals(oldValue, newValue)) return;
        try
        {
            // coalescing: 같은 (id,prop) 연속이면 마지막 커맨드의 After만 갱신
            if (_lastProp != null && _lastPropId == marker.Id && _lastPropName == property)
            {
                _lastProp.After = newValue;
                return;
            }
            var cmd = new PropertyChangeCommand(Context!, marker.Id, property, oldValue, newValue, marker is GMapImageMarker);
            _undo.Push(cmd);
            _lastProp = cmd; _lastPropId = marker.Id; _lastPropName = property;
        }
        catch (Exception ex) { _log?.Error($"RecordPropertyChange 실패: {ex.Message}"); }
    }

    public void RecordLabelOffset(IEditableMarker marker, double beforeX, double beforeY)
    {
        if (!Ready || marker == null) return;
        ResetCoalesce();
        try
        {
            // before=드래그 시작 시점 오프셋(caller 명시 전달) — 마커 선택 여부와 무관하게 항상 정확(RecordPositionChange 동형).
            // 도메인: 이미지=U/V(정규화, Overlay_Title FR-02) / 그 외=px. LabelAdorner.RaiseOffsetChanged와 동일 규약.
            var before = (beforeX, beforeY);
            var after = marker is IImageEditableMarker img
                ? (img.LabelOffsetU, img.LabelOffsetV)
                : (marker.LabelOffsetX, marker.LabelOffsetY);
            if (before.Equals(after)) return;
            _undo.Push(new LabelOffsetCommand(Context!, marker.Id, before, after, marker is GMapImageMarker));
            _labelBaseline[marker.Id] = after;   // 다음 드래그 before
        }
        catch (Exception ex) { _log?.Error($"RecordLabelOffset 실패: {ex.Message}"); }
    }

    public void RecordTitleWidthResize(IEditableMarker marker, double beforeA, double beforeB, double beforeWidth)
    {
        if (!Ready || marker == null) return;
        ResetCoalesce();
        try
        {
            // edge-pinned 폭 조절은 (오프셋,폭)을 한 제스처로 바꿈 → 쌍을 단일 커맨드로 원자 기록(FR-13, 2차 검증 W그룹).
            var before = (beforeA, beforeB, beforeWidth);
            var after = marker is IImageEditableMarker img
                ? (img.LabelOffsetU, img.LabelOffsetV, marker.TitleMaxWidth)
                : (marker.LabelOffsetX, marker.LabelOffsetY, marker.TitleMaxWidth);
            if (before.Equals(after)) return;
            _undo.Push(new TitleWidthResizeCommand(Context!, marker.Id, before, after, marker is GMapImageMarker));
            _labelBaseline[marker.Id] = (after.Item1, after.Item2);   // 오프셋 baseline 동기(다음 이동 드래그 before)
        }
        catch (Exception ex) { _log?.Error($"RecordTitleWidthResize 실패: {ex.Message}"); }
    }

    public void RecordAdd(IEditableMarker marker)
    {
        if (!Ready || marker == null) return;
        // 오버레이 이미지(GMapImageMarker)는 undo 대상에서 제외 — 물리 파일 백업 + 하드 삭제(DeleteLocalImage)라
        // undo(=RemoveMarker)가 PNG를 영구 삭제하고 복원은 DB 행만 되살려 파일이 사라진다(데이터 손실). 시드/복원 로드 오염도 차단.
        if (marker is GMapImageMarker) return;
        ResetCoalesce();
        try
        {
            var snap = SymbolSnapshot.Capture(marker);
            if (snap != null) _undo.Push(new AddSymbolCommand(Context!, snap));
        }
        catch (Exception ex) { _log?.Error($"RecordAdd 실패: {ex.Message}"); }
    }

    public ISymbolSnapshot? CaptureForDelete(IEditableMarker marker)
    {
        // 오버레이 이미지는 undo 제외(파일 하드삭제·복구 불가) → 스냅샷 안 뜸 → RecordDelete(null) no-op.
        if (marker == null || marker is GMapImageMarker) return null;
        try { return SymbolSnapshot.Capture(marker); }
        catch (Exception ex) { _log?.Error($"CaptureForDelete 실패: {ex.Message}"); return null; }
    }

    public void RecordDelete(ISymbolSnapshot? snapshot)
    {
        if (!Ready || snapshot == null) return;
        ResetCoalesce();
        _undo.Push(new DeleteSymbolCommand(Context!, snapshot));
    }

    public void RecordZOrder(IReadOnlyList<(bool isImage, int id, int zOrder)> before, IReadOnlyList<(bool isImage, int id, int zOrder)> after)
    {
        if (!Ready || before == null || after == null || after.Count == 0) return;
        ResetCoalesce();
        _undo.Push(new ZOrderBatchCommand(Context!, before, after));
    }

    public void RecordPositionChange(IEditableMarker marker, PointLatLng before)
        => RecordPositionChange(marker, before, marker?.Position ?? before);

    public void RecordPositionChange(IEditableMarker marker, PointLatLng before, PointLatLng after)
    {
        if (!Ready || marker == null) return;
        if (before.Lat == after.Lat && before.Lng == after.Lng) return;   // 이동 없음(잠금 멤버)
        try
        {
            // 위치 전용 변경 = TransformCommand(크기·방위 불변). 배치 중이면 매크로에 합류.
            _undo.Push(new TransformCommand(Context!, marker.Id, marker is GMapImageMarker,
                (before, marker.Width, marker.Height, marker.Bearing),
                (after, marker.Width, marker.Height, marker.Bearing), _log));
        }
        catch (Exception ex) { _log?.Error($"RecordPositionChange 실패: {ex.Message}"); }
    }

    public void RecordLock(IEditableMarker marker, bool before, bool after)
    {
        if (!Ready || marker == null || before == after) return;
        ResetCoalesce();
        _undo.Push(new LockCommand(Context!, marker.Id, before, after, marker is GMapImageMarker));
    }

    public void RecordRename(IEditableMarker marker, string before, string after)
    {
        if (!Ready || marker == null || string.Equals(before, after)) return;
        ResetCoalesce();
        _undo.Push(new RenameSymbolCommand(Context!, marker.Id, before ?? string.Empty, after ?? string.Empty, marker is GMapImageMarker));
    }

    public IDisposable BeginBatch(string description)
    {
        ResetCoalesce();
        return _undo.BeginBatch(description);
    }

    // ── v2 커버리지 ──
    public void RecordVisibility(IEditableMarker marker, bool before, bool after)
    {
        if (!Ready || marker == null || before == after) return;
        ResetCoalesce();
        _undo.Push(new VisibilityCommand(Context!, marker.Id, before, after, marker is GMapImageMarker));
    }

    public void RecordCustomImageRotation(int imageId, double before, double after)
    {
        if (!Ready || before == after) return;
        ResetCoalesce();
        _undo.Push(new CustomImageRotationCommand(Context!, imageId, before, after));
    }

    public void RecordCustomImageEdit(int imageId, RectLatLng beforeBounds, double beforeRotation, RectLatLng afterBounds, double afterRotation)
    {
        if (!Ready || imageId <= 0) return;
        if (beforeBounds.Equals(afterBounds) && beforeRotation == afterRotation) return;   // 변화 없음(짧은클릭 등)
        ResetCoalesce();
        _undo.Push(new CustomImageEditCommand(Context!, imageId, beforeBounds, beforeRotation, afterBounds, afterRotation));
    }

    public void RecordLayerChange(string description,
        System.Collections.Generic.IReadOnlyList<LayerFields> before,
        System.Collections.Generic.IReadOnlyList<LayerFields> after)
    {
        if (!Ready || before == null || after == null || before.Count == 0) return;
        ResetCoalesce();
        _undo.Push(new LayerNodeCommand(Context!, description, before, after));
    }
}
