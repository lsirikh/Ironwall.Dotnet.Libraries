using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.LineModules;

/// <summary>
/// 라인 드로잉 상태
/// </summary>
public enum LineDrawingState
{
    None,           // 드로잉 없음
    FirstClick,     // 첫 번째 클릭 대기
    Drawing,        // 드로잉 중
    Completed,      // 완료
    Cancelled       // 취소
}

/// <summary>
/// 라인 드로잉 이벤트 아규먼트
/// </summary>
public class LineDrawingEventArgs : EventArgs
{
    public ILineEditableMarker LineMarker { get; }
    public LineDrawingState State { get; }
    public List<PointLatLng> Points { get; }
    public PointLatLng? CurrentMousePosition { get; }

    public LineDrawingEventArgs(ILineEditableMarker marker, LineDrawingState state,
        List<PointLatLng> points = null, PointLatLng? mousePos = null)
    {
        LineMarker = marker;
        State = state;
        Points = points ?? new List<PointLatLng>();
        CurrentMousePosition = mousePos;
    }
}

/// <summary>
/// 라인 드로잉을 위한 통합 매니저
/// - 드로잉 로직과 마커 생성을 하나의 클래스에서 처리
/// - Adorner 패턴과 유사한 생명주기 관리
/// - 다양한 라인 심볼 타입에 재사용 가능
/// </summary>
public class LineDrawingManager : IDisposable
{
    #region Fields

    private readonly ILogService _log;
    private readonly GMapCustomControl _mapControl;
    private readonly object _lock = new object();
    private bool _disposed = false;

    // 드로잉 상태
    private LineDrawingState _currentState = LineDrawingState.None;
    private ILineEditableMarker _currentLineMarker;
    private List<PointLatLng> _confirmedPoints = new List<PointLatLng>();
    private PointLatLng? _currentMousePosition;

    // 라인 설정 (생성 시 전달받음)
    private string _lineTitle = "New Line";
    private EnumLinePattern _linePattern = EnumLinePattern.Solid;
    private double _lineOpacity = 1.0;
    private EnumColorType _strokeColor = EnumColorType.Red;
    private double _strokeThickness = 2.0;

    #endregion

    #region Events

    /// <summary>
    /// 라인 드로잉 상태 변경 이벤트
    /// </summary>
    public event EventHandler<LineDrawingEventArgs> DrawingStateChanged;

    /// <summary>
    /// 라인 포인트 추가 이벤트
    /// </summary>
    public event EventHandler<LineDrawingEventArgs> PointAdded;

    /// <summary>
    /// 라인 드로잉 완료 이벤트 (최종 마커 포함)
    /// </summary>
    public event EventHandler<LineDrawingEventArgs> DrawingCompleted;

    /// <summary>
    /// 라인 드로잉 취소 이벤트
    /// </summary>
    public event EventHandler<LineDrawingEventArgs> DrawingCancelled;

    #endregion

    #region Constructor

    /// <summary>
    /// LineDrawingManager 생성자
    /// </summary>
    /// <param name="mapControl">지도 컨트롤</param>
    /// <param name="log">로깅 서비스</param>
    public LineDrawingManager(GMapCustomControl mapControl, ILogService log = null)
    {
        _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
        _log = log;

        SubscribeToMapEvents();
        _log?.Info("LineDrawingManager 초기화 완료");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 라인 드로잉 시작
    /// </summary>
    /// <param name="title">라인 제목</param>
    /// <param name="pattern">라인 패턴</param>
    /// <param name="opacity">투명도</param>
    /// <param name="strokeColor">선 색상</param>
    /// <param name="strokeThickness">선 두께</param>
    /// <returns>시작 성공 여부</returns>
    public bool StartDrawing(string title = "New Line",
        EnumLinePattern pattern = EnumLinePattern.Solid,
        double opacity = 1.0,
        EnumColorType strokeColor = EnumColorType.Red,
        double strokeThickness = 2.0)
    {
        lock (_lock)
        {
            try
            {
                // 기존 드로잉 취소
                if (_currentState != LineDrawingState.None)
                {
                    CancelDrawing();
                }

                // 설정 저장
                _lineTitle = title;
                _linePattern = pattern;
                _lineOpacity = opacity;
                _strokeColor = strokeColor;
                _strokeThickness = strokeThickness;

                // 임시 라인 마커 생성
                _currentLineMarker = CreateTempLineMarker();

                // 지도에 임시 마커 추가
                if (_currentLineMarker is GMapMarker gMapMarker)
                {
                    _mapControl.Markers.Add(gMapMarker);
                }

                // 상태 설정
                _currentState = LineDrawingState.FirstClick;
                _confirmedPoints.Clear();
                _currentMousePosition = null;

                // 마커 드로잉 모드 활성화
                _currentLineMarker.StartDrawing();

                // 맵 커서 변경
                _mapControl.Cursor = Cursors.Cross;

                _log?.Info($"라인 드로잉 시작: {_lineTitle}");

                // 이벤트 발생
                RaiseStateChangedEvent();

                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 시작 실패: {ex.Message}");
                CleanupTempMarker();
                return false;
            }
        }
    }

    /// <summary>
    /// 라인 드로잉 완료
    /// </summary>
    /// <returns>완료 성공 여부</returns>
    public bool CompleteDrawing()
    {
        lock (_lock)
        {
            try
            {
                if (_currentState != LineDrawingState.Drawing || _currentLineMarker == null)
                {
                    _log?.Warning("완료할 수 있는 드로잉이 없습니다.");
                    return false;
                }

                // 최소 2개 포인트 필요
                if (_confirmedPoints.Count < 2)
                {
                    _log?.Warning("라인 완료 실패: 최소 2개 포인트 필요");
                    return false;
                }

                // 마커 드로잉 완료
                _currentLineMarker.FinishDrawing();

                // 상태 변경
                _currentState = LineDrawingState.Completed;

                // 맵 커서 복원
                _mapControl.Cursor = Cursors.Arrow;

                _log?.Info($"라인 드로잉 완료: {_lineTitle}, {_confirmedPoints.Count}개 포인트, 총 거리: {_currentLineMarker.TotalDistance:F1}m");

                // 완료 이벤트 발생
                RaiseCompletedEvent();

                // 상태 초기화 (완료 후)
                var completedMarker = _currentLineMarker;
                ResetDrawingState();

                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 완료 실패: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 라인 드로잉 취소
    /// </summary>
    /// <returns>취소 성공 여부</returns>
    public bool CancelDrawing()
    {
        lock (_lock)
        {
            try
            {
                if (_currentState == LineDrawingState.None)
                {
                    return false;
                }

                _log?.Info($"라인 드로잉 취소: {_currentLineMarker?.Title ?? _lineTitle}");

                // 상태 변경
                _currentState = LineDrawingState.Cancelled;

                // 마커 드로잉 취소
                _currentLineMarker?.CancelDrawing();

                // 맵 커서 복원
                _mapControl.Cursor = Cursors.Arrow;

                // 취소 이벤트 발생
                RaiseCancelledEvent();

                // 임시 마커 정리
                CleanupTempMarker();

                // 상태 초기화
                ResetDrawingState();

                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 드로잉 취소 실패: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 마지막 포인트 제거 (Undo)
    /// </summary>
    /// <returns>제거 성공 여부</returns>
    public bool UndoLastPoint()
    {
        lock (_lock)
        {
            try
            {
                if (_currentState != LineDrawingState.Drawing || _confirmedPoints.Count == 0)
                {
                    return false;
                }

                // 마지막 포인트 제거
                var removedPoint = _confirmedPoints[_confirmedPoints.Count - 1];
                _confirmedPoints.RemoveAt(_confirmedPoints.Count - 1);
                _currentLineMarker.RemoveLastPoint();

                // 포인트가 1개 미만이면 FirstClick 상태로 복귀
                if (_confirmedPoints.Count < 1)
                {
                    _currentState = LineDrawingState.FirstClick;
                }

                _log?.Info($"마지막 포인트 제거: {removedPoint} (남은 포인트: {_confirmedPoints.Count}개)");

                // 상태 변경 이벤트 발생
                RaiseStateChangedEvent();

                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"마지막 포인트 제거 실패: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region Map Event Handlers

    /// <summary>
    /// 맵 이벤트 구독
    /// </summary>
    private void SubscribeToMapEvents()
    {
        _mapControl.OnMapClicked += OnMapClicked;
        _mapControl.MouseMove += OnMapMouseMove;
        _mapControl.KeyDown += OnMapKeyDown;
    }

    /// <summary>
    /// 맵 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromMapEvents()
    {
        _mapControl.OnMapClicked -= OnMapClicked;
        _mapControl.MouseMove -= OnMapMouseMove;
        _mapControl.KeyDown -= OnMapKeyDown;
    }

    /// <summary>
    /// 맵 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapClicked(PointLatLng geoPosition, Point screenPosition)
    {
        if (_currentState == LineDrawingState.None) return;

        lock (_lock)
        {
            try
            {
                switch (_currentState)
                {
                    case LineDrawingState.FirstClick:
                        HandleFirstClick(geoPosition);
                        break;

                    case LineDrawingState.Drawing:
                        HandleSubsequentClick(geoPosition);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"맵 클릭 처리 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 첫 번째 클릭 처리
    /// </summary>
    private void HandleFirstClick(PointLatLng geoPosition)
    {
        // 첫 번째 포인트 추가
        _confirmedPoints.Add(geoPosition);
        _currentLineMarker.AddPoint(geoPosition);

        // 상태를 Drawing으로 변경
        _currentState = LineDrawingState.Drawing;

        _log?.Info($"첫 번째 포인트 추가: {geoPosition}");

        // 포인트 추가 이벤트 발생
        RaisePointAddedEvent(geoPosition);
    }

    /// <summary>
    /// 두 번째 이후 클릭 처리
    /// </summary>
    private void HandleSubsequentClick(PointLatLng geoPosition)
    {
        // 포인트 추가
        _confirmedPoints.Add(geoPosition);
        _currentLineMarker.AddPoint(geoPosition);

        _log?.Info($"포인트 추가: {geoPosition} (총 {_confirmedPoints.Count}개)");

        // 포인트 추가 이벤트 발생
        RaisePointAddedEvent(geoPosition);
    }

    /// <summary>
    /// 마우스 이동 이벤트 핸들러
    /// </summary>
    private void OnMapMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentState != LineDrawingState.Drawing) return;

        try
        {
            // 현재 마우스 위치를 지리 좌표로 변환
            var mousePos = e.GetPosition(_mapControl);
            var geoPos = _mapControl.FromLocalToLatLng((int)mousePos.X, (int)mousePos.Y);

            _currentMousePosition = geoPos;

            // 라인 컨트롤에 미리보기 업데이트 요청
            UpdatePreviewLine(mousePos);

            // 상태 변경 이벤트 발생 (미리보기 업데이트용)
            RaiseStateChangedEvent();
        }
        catch (Exception ex)
        {
            _log?.Error($"마우스 이동 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 키보드 이벤트 핸들러
    /// </summary>
    private void OnMapKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (_currentState == LineDrawingState.Drawing)
                    {
                        // ESC: 드로잉 완료 (2개 이상 포인트) 또는 취소
                        if (_confirmedPoints.Count >= 2)
                        {
                            CompleteDrawing();
                        }
                        else
                        {
                            CancelDrawing();
                        }
                        e.Handled = true;
                    }
                    break;

                case Key.Back:
                case Key.Z when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                    // Backspace 또는 Ctrl+Z: 마지막 포인트 제거
                    UndoLastPoint();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    // Enter: 강제 완료
                    if (_currentState == LineDrawingState.Drawing && _confirmedPoints.Count >= 2)
                    {
                        CompleteDrawing();
                        e.Handled = true;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"키보드 이벤트 처리 실패: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 임시 라인 마커 생성
    /// </summary>
    private ILineEditableMarker CreateTempLineMarker()
    {
        var lineModel = new LineSymbolModel
        {
            Title = _lineTitle,
            TitleSize = 12,
            Latitude = _mapControl.Position.Lat,
            Longitude = _mapControl.Position.Lng,
            Zoom = _mapControl.Zoom,
            Width = 60,
            Height = 60,
            Bearing = 0,
            Category = EnumMarkerCategory.AREA_BOUNDARY,
            ShowShape = true,
            ShowTitle = false,
            OperationState = EnumOperationState.ACTIVE,
            StrokeColor = _strokeColor,
            StrokeThickness = _strokeThickness,
            FillColor = EnumColorType.Transparent,
            LinePattern = _linePattern,
            LineOpacity = _lineOpacity,
            IsClosedPath = false,
            ShowArrowHead = false,
            LinePoints = new List<GeoPoint>()
        };

        return new GMapLineMarker(_log, lineModel);
    }

    /// <summary>
    /// 임시 마커 정리
    /// </summary>
    private void CleanupTempMarker()
    {
        if (_currentLineMarker != null && _currentLineMarker is GMapMarker gMapMarker)
        {
            _mapControl?.Markers?.Remove(gMapMarker);

            if (_currentLineMarker is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _currentLineMarker = null;
            _log?.Info("임시 라인 마커 정리 완료");
        }
    }

    /// <summary>
    /// 미리보기 라인 업데이트
    /// </summary>
    private void UpdatePreviewLine(Point mousePosition)
    {
        if (_currentLineMarker is GMapLineMarker lineMarker &&
            lineMarker.Shape is GMapMarkerLineControl lineControl)
        {
            lineControl.UpdatePreviewLine(mousePosition);
        }
    }

    /// <summary>
    /// 드로잉 상태 초기화
    /// </summary>
    private void ResetDrawingState()
    {
        _currentState = LineDrawingState.None;
        _currentLineMarker = null;
        _confirmedPoints.Clear();
        _currentMousePosition = null;
    }

    /// <summary>
    /// 상태 변경 이벤트 발생
    /// </summary>
    private void RaiseStateChangedEvent()
    {
        var eventArgs = new LineDrawingEventArgs(_currentLineMarker, _currentState,
            new List<PointLatLng>(_confirmedPoints), _currentMousePosition);
        DrawingStateChanged?.Invoke(this, eventArgs);
    }

    /// <summary>
    /// 포인트 추가 이벤트 발생
    /// </summary>
    private void RaisePointAddedEvent(PointLatLng point)
    {
        var eventArgs = new LineDrawingEventArgs(_currentLineMarker, _currentState,
            new List<PointLatLng>(_confirmedPoints));
        PointAdded?.Invoke(this, eventArgs);
    }

    /// <summary>
    /// 드로잉 완료 이벤트 발생
    /// </summary>
    private void RaiseCompletedEvent()
    {
        var eventArgs = new LineDrawingEventArgs(_currentLineMarker, LineDrawingState.Completed,
            new List<PointLatLng>(_confirmedPoints));
        DrawingCompleted?.Invoke(this, eventArgs);
    }

    /// <summary>
    /// 드로잉 취소 이벤트 발생
    /// </summary>
    private void RaiseCancelledEvent()
    {
        var eventArgs = new LineDrawingEventArgs(_currentLineMarker, LineDrawingState.Cancelled,
            new List<PointLatLng>());
        DrawingCancelled?.Invoke(this, eventArgs);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// 현재 드로잉 상태
    /// </summary>
    public LineDrawingState CurrentState => _currentState;

    /// <summary>
    /// 현재 드로잉 중인 라인 마커
    /// </summary>
    public ILineEditableMarker CurrentLineMarker => _currentLineMarker;

    /// <summary>
    /// 확정된 포인트 수
    /// </summary>
    public int ConfirmedPointCount => _confirmedPoints.Count;

    /// <summary>
    /// 드로잉 중 여부
    /// </summary>
    public bool IsDrawing => _currentState != LineDrawingState.None && _currentState != LineDrawingState.Cancelled;

    /// <summary>
    /// 현재 라인의 총 거리 (미터)
    /// </summary>
    public double CurrentTotalDistance => _currentLineMarker?.TotalDistance ?? 0.0;

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// 리소스 해제
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 리소스 해제 (보호된 메서드)
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                // 드로잉 취소
                if (_currentState != LineDrawingState.None)
                {
                    CancelDrawing();
                }

                // 맵 이벤트 구독 해제
                UnsubscribeFromMapEvents();

                _log?.Info("LineDrawingManager 리소스 해제 완료");
            }
            catch (Exception ex)
            {
                _log?.Error($"LineDrawingManager 해제 중 오류: {ex.Message}");
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 소멸자
    /// </summary>
    ~LineDrawingManager()
    {
        Dispose(false);
    }

    #endregion
}