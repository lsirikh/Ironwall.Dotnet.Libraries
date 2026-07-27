using System;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : 측정 툴(길이/넓이) VM 파트 — 툴바 토글 명령, HUD 리드아웃 바인딩,
                  윈도우 키 후킹(ESC 취소·Enter 완료·Backspace/Ctrl+Z 되돌리기), 모드 상호배제.
                  Measure_Tools FR-05~09. 계산·렌더는 MeasureController/MeasureAdorner가 담당.
   Note         : 리드아웃/HUD는 컨트롤(MainMap)의 MeasureReadoutChanged/MeasureStopped 이벤트로 갱신.
                  키는 aim 모드처럼 윈도우 레벨 PreviewKeyDown 후킹(맵 포커스 미확보 대비).
   Created On   : 2026-07-24 · Sensorway Co., Ltd.
****************************************************************************/
public partial class MapViewModel
{
    #region - 명령 -
    /// <summary>길이 측정 토글.</summary>
    public RelayCommand? MeasureLengthCommand { get; private set; }
    /// <summary>넓이 측정 토글.</summary>
    public RelayCommand? MeasureAreaCommand { get; private set; }

    private void InitializeMeasureCommands()
    {
        MeasureLengthCommand = new RelayCommand(_ => ToggleMeasure(MeasureKind.Length));
        MeasureAreaCommand = new RelayCommand(_ => ToggleMeasure(MeasureKind.Area));
    }
    #endregion

    #region - 토글 상태(툴바 버튼 IsChecked) -
    private bool _isMeasureLengthActive;
    public bool IsMeasureLengthActive
    {
        get => _isMeasureLengthActive;
        set { _isMeasureLengthActive = value; NotifyOfPropertyChange(nameof(IsMeasureLengthActive)); }
    }

    private bool _isMeasureAreaActive;
    public bool IsMeasureAreaActive
    {
        get => _isMeasureAreaActive;
        set { _isMeasureAreaActive = value; NotifyOfPropertyChange(nameof(IsMeasureAreaActive)); }
    }
    #endregion

    #region - HUD 리드아웃 바인딩 -
    private bool _isMeasureReadoutVisible;
    public bool IsMeasureReadoutVisible
    {
        get => _isMeasureReadoutVisible;
        set { _isMeasureReadoutVisible = value; NotifyOfPropertyChange(nameof(IsMeasureReadoutVisible)); }
    }

    private string _measurePrimaryLabel = string.Empty;
    public string MeasurePrimaryLabel
    {
        get => _measurePrimaryLabel;
        set { _measurePrimaryLabel = value; NotifyOfPropertyChange(nameof(MeasurePrimaryLabel)); }
    }

    private string _measurePrimaryValue = string.Empty;
    public string MeasurePrimaryValue
    {
        get => _measurePrimaryValue;
        set { _measurePrimaryValue = value; NotifyOfPropertyChange(nameof(MeasurePrimaryValue)); }
    }

    private string _measurePrimaryUnit = string.Empty;
    public string MeasurePrimaryUnit
    {
        get => _measurePrimaryUnit;
        set { _measurePrimaryUnit = value; NotifyOfPropertyChange(nameof(MeasurePrimaryUnit)); }
    }

    private bool _measureHasSecondary;
    public bool MeasureHasSecondary
    {
        get => _measureHasSecondary;
        set { _measureHasSecondary = value; NotifyOfPropertyChange(nameof(MeasureHasSecondary)); }
    }

    private string _measureSecondaryLabel = string.Empty;
    public string MeasureSecondaryLabel
    {
        get => _measureSecondaryLabel;
        set { _measureSecondaryLabel = value; NotifyOfPropertyChange(nameof(MeasureSecondaryLabel)); }
    }

    private string _measureSecondaryValue = string.Empty;
    public string MeasureSecondaryValue
    {
        get => _measureSecondaryValue;
        set { _measureSecondaryValue = value; NotifyOfPropertyChange(nameof(MeasureSecondaryValue)); }
    }

    private string _measureSecondaryUnit = string.Empty;
    public string MeasureSecondaryUnit
    {
        get => _measureSecondaryUnit;
        set { _measureSecondaryUnit = value; NotifyOfPropertyChange(nameof(MeasureSecondaryUnit)); }
    }

    private string _measureHint = string.Empty;
    public string MeasureHint
    {
        get => _measureHint;
        set { _measureHint = value; NotifyOfPropertyChange(nameof(MeasureHint)); }
    }
    #endregion

    #region - 토글/상호배제 -
    /// <summary>측정 토글 — 같은 종류 재클릭=종료, 다른 종류=전환. 진입 시 aim/라인/배치 모드 취소.</summary>
    private void ToggleMeasure(MeasureKind kind)
    {
        if (MainMap == null) return;

        if (MainMap.IsMeasuring && MainMap.ActiveMeasureKind == kind)
        {
            MainMap.StopMeasure();   // 토글 오프 → OnMeasureStopped가 정리
            return;
        }

        CancelConflictingModesForMeasure();
        MainMap.StartMeasure(kind);
        Mouse.OverrideCursor = Cursors.Cross;
        EnsureMeasureKeyHook();
        SyncMeasureToggleFlags();
        _log?.Info($"[Measure] 토글 진입 — {kind}");
    }

    /// <summary>측정 진입 전 충돌 모드 취소(편집 모드는 유지 — 측정은 편집 독립).</summary>
    private void CancelConflictingModesForMeasure()
    {
        try
        {
            if (MainMap == null) return;
            if (MainMap.IsTargetAimMode) ExitTargetAimMode();
            if (MainMap.IsSymbolPlacementMode) ExitSymbolPlacementMode();
            if (MainMap.IsHomePlacementMode) ExitHomePlacementMode();
            if (MainMap.IsAnchorDrawMode) ExitAnchorDrawMode();
            if (MainMap.IsLineDrawing) _ = MainMap.LineDrawingService?.CancelDrawingAsync();
        }
        catch (Exception ex) { _log?.Warning($"[Measure] 충돌 모드 종료 경고: {ex.Message}"); }
    }

    private void SyncMeasureToggleFlags()
    {
        bool m = MainMap?.IsMeasuring ?? false;
        var k = MainMap?.ActiveMeasureKind ?? MeasureKind.Length;
        IsMeasureLengthActive = m && k == MeasureKind.Length;
        IsMeasureAreaActive = m && k == MeasureKind.Area;
    }
    #endregion

    #region - 컨트롤 이벤트 배선 -
    private void AttachMeasureEvents()
    {
        if (MainMap == null) return;
        MainMap.MeasureReadoutChanged += OnMeasureReadoutChanged;
        MainMap.MeasureStopped += OnMeasureStopped;
    }

    private void DetachMeasureEvents()
    {
        if (MainMap == null) return;
        MainMap.MeasureReadoutChanged -= OnMeasureReadoutChanged;
        MainMap.MeasureStopped -= OnMeasureStopped;
        RemoveMeasureKeyHook();
        if (MainMap.IsMeasuring) MainMap.StopMeasure();
    }

    private void OnMeasureReadoutChanged(MeasureReadout r)
    {
        IsMeasureReadoutVisible = r.IsActive;
        MeasurePrimaryLabel = r.PrimaryLabel;
        MeasurePrimaryValue = r.PrimaryValue;
        MeasurePrimaryUnit = r.PrimaryUnit;
        MeasureHasSecondary = r.HasSecondary;
        MeasureSecondaryLabel = r.SecondaryLabel;
        MeasureSecondaryValue = r.SecondaryValue;
        MeasureSecondaryUnit = r.SecondaryUnit;
        MeasureHint = r.Hint;
    }

    private void OnMeasureStopped()
    {
        IsMeasureLengthActive = false;
        IsMeasureAreaActive = false;
        IsMeasureReadoutVisible = false;
        if (Mouse.OverrideCursor == Cursors.Cross) Mouse.OverrideCursor = null;
        RemoveMeasureKeyHook();
    }
    #endregion

    #region - 윈도우 키 후킹(ESC/Enter/Backspace/Ctrl+Z) -
    private System.Windows.Window? _measureKeyWindow;

    private void EnsureMeasureKeyHook()
    {
        try
        {
            if (_measureKeyWindow != null || MainMap == null) return;
            _measureKeyWindow = System.Windows.Window.GetWindow(MainMap);
            if (_measureKeyWindow != null)
                _measureKeyWindow.PreviewKeyDown += OnMeasureKeyDown;
        }
        catch (Exception ex) { _log?.Warning($"[Measure] 키 후킹 경고: {ex.Message}"); }
    }

    private void RemoveMeasureKeyHook()
    {
        if (_measureKeyWindow == null) return;
        try { _measureKeyWindow.PreviewKeyDown -= OnMeasureKeyDown; } catch { /* 종료 중 */ }
        _measureKeyWindow = null;
    }

    private void OnMeasureKeyDown(object sender, KeyEventArgs e)
    {
        if (MainMap == null || !MainMap.IsMeasuring) return;
        switch (e.Key)
        {
            case Key.Escape:
                MainMap.StopMeasure(); e.Handled = true; break;
            case Key.Enter:                     // == Key.Return
                MainMap.FinishMeasure(); e.Handled = true; break;
            case Key.Back:
                MainMap.MeasureUndo(); e.Handled = true; break;
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                MainMap.MeasureUndo(); e.Handled = true; break;
        }
    }
    #endregion
}
