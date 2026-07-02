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
            _undo.Push(new TransformCommand(Context!, e.Marker.Id, before, after));
        }
        catch (Exception ex) { _log?.Error($"RecordTransform 실패: {ex.Message}"); }
    }

    public void RecordPropertyChange(IEditableMarker marker, string property, object? oldValue, object? newValue)
    {
        if (!Ready || marker == null || string.IsNullOrEmpty(property)) return;
        if (Equals(oldValue, newValue)) return;
        try
        {
            // coalescing: 같은 (id,prop) 연속이면 마지막 커맨드의 After만 갱신
            if (_lastProp != null && _lastPropId == marker.Id && _lastPropName == property)
            {
                _lastProp.After = newValue;
                return;
            }
            var cmd = new PropertyChangeCommand(Context!, marker.Id, property, oldValue, newValue);
            _undo.Push(cmd);
            _lastProp = cmd; _lastPropId = marker.Id; _lastPropName = property;
        }
        catch (Exception ex) { _log?.Error($"RecordPropertyChange 실패: {ex.Message}"); }
    }

    public void RecordLabelOffset(IEditableMarker marker)
    {
        if (!Ready || marker == null) return;
        ResetCoalesce();
        try
        {
            var before = _labelBaseline.TryGetValue(marker.Id, out var b) ? b : (marker.LabelOffsetX, marker.LabelOffsetY);
            var after = (marker.LabelOffsetX, marker.LabelOffsetY);
            if (before.Equals(after)) return;
            _undo.Push(new LabelOffsetCommand(Context!, marker.Id, before, after));
            _labelBaseline[marker.Id] = after;   // 다음 드래그 before
        }
        catch (Exception ex) { _log?.Error($"RecordLabelOffset 실패: {ex.Message}"); }
    }

    public void RecordAdd(IEditableMarker marker)
    {
        if (!Ready || marker == null) return;
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
        try { return marker == null ? null : SymbolSnapshot.Capture(marker); }
        catch (Exception ex) { _log?.Error($"CaptureForDelete 실패: {ex.Message}"); return null; }
    }

    public void RecordDelete(ISymbolSnapshot? snapshot)
    {
        if (!Ready || snapshot == null) return;
        ResetCoalesce();
        _undo.Push(new DeleteSymbolCommand(Context!, snapshot));
    }

    public void RecordZOrder(IReadOnlyList<(int id, int zOrder)> before, IReadOnlyList<(int id, int zOrder)> after)
    {
        if (!Ready || before == null || after == null || after.Count == 0) return;
        ResetCoalesce();
        _undo.Push(new ZOrderBatchCommand(Context!, before, after));
    }

    public void RecordPositionChange(IEditableMarker marker, PointLatLng before)
    {
        if (!Ready || marker == null) return;
        var after = marker.Position;
        if (before.Lat == after.Lat && before.Lng == after.Lng) return;   // 이동 없음(잠금 멤버)
        try
        {
            // 위치 전용 변경 = TransformCommand(크기·방위 불변). 배치 중이면 매크로에 합류.
            _undo.Push(new TransformCommand(Context!, marker.Id,
                (before, marker.Width, marker.Height, marker.Bearing),
                (after, marker.Width, marker.Height, marker.Bearing)));
        }
        catch (Exception ex) { _log?.Error($"RecordPositionChange 실패: {ex.Message}"); }
    }

    public void RecordLock(IEditableMarker marker, bool before, bool after)
    {
        if (!Ready || marker == null || before == after) return;
        ResetCoalesce();
        _undo.Push(new LockCommand(Context!, marker.Id, before, after));
    }

    public void RecordRename(IEditableMarker marker, string before, string after)
    {
        if (!Ready || marker == null || string.Equals(before, after)) return;
        ResetCoalesce();
        _undo.Push(new RenameSymbolCommand(Context!, marker.Id, before ?? string.Empty, after ?? string.Empty));
    }

    public IDisposable BeginBatch(string description)
    {
        ResetCoalesce();
        return _undo.BeginBatch(description);
    }
}
