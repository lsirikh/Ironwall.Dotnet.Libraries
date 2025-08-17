using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using GMap.NET.WindowsPresentation;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;

/****************************************************************************
   Purpose      : 마커 편집을 위한 Adorner 클래스                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 마커 편집을 위한 Adorner
/// - 편집 핸들 렌더링 및 드래그 처리
/// - 실시간 편집 피드백 제공
/// - GMapControl과 연동하여 지리 좌표 변환
/// </summary>
public class MarkerEditAdorner : Adorner, IDisposable
{
    #region Fields

    private readonly ILogService _log;
    private bool _disposed = false;
    private readonly GMapControl _mapControl;
    private readonly GMapCustomMarker _targetMarker;

    // 편집 상태
    private MarkerEditState _editState;
    private bool _isDragging;
    private Point _dragStartPoint;
    private MarkerHandle _activeHandle = MarkerHandle.None;

    // 원본 데이터 백업 (Undo용)
    private PointLatLng _originalPosition;
    private double _originalWidth;
    private double _originalHeight;
    private double _originalBearing;

    // 시각적 요소들
    private Pen _handlePen;
    private Pen _editAreaPen;
    private Brush _moveHandleBrush;
    private Brush _rotateHandleBrush;
    private Brush _resizeHandleBrush;
    private Brush _infoBackgroundBrush;

    #endregion

    #region Events

    /// <summary>
    /// 편집 시작 이벤트
    /// </summary>
    public event EventHandler<MarkerEditStartedEventArgs> EditStarted;

    /// <summary>
    /// 편집 중 이벤트 (실시간)
    /// </summary>
    public event EventHandler<MarkerEditingEventArgs> Editing;

    /// <summary>
    /// 편집 완료 이벤트
    /// </summary>
    public event EventHandler<MarkerEditCompletedEventArgs> EditCompleted;

    /// <summary>
    /// 편집 취소 이벤트
    /// </summary>
    public event EventHandler<MarkerEditCancelledEventArgs> EditCancelled;

    #endregion

    #region Constructor

    /// <summary>
    /// MarkerEditAdorner 생성자
    /// </summary>
    /// <param name="adornedElement">장식할 UI 요소 (GMapMarkerBasicCustomControl)</param>
    /// <param name="targetMarker">편집 대상 마커</param>
    /// <param name="mapControl">지도 컨트롤</param>
    /// <param name="log">로깅 서비스</param>
    public MarkerEditAdorner(UIElement adornedElement, GMapCustomMarker targetMarker,
        GMapControl mapControl, ILogService log = null)
        : base(adornedElement)
    {
        _log = log;
        _mapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));
        _targetMarker = targetMarker ?? throw new ArgumentNullException(nameof(targetMarker));

        // 편집 상태 초기화
        _editState = new MarkerEditState
        {
            IsEditing = false,
            EditMode = MarkerEditMode.None,
            TargetMarker = _targetMarker
        };

        // 시각적 요소 초기화
        InitializeVisualElements();
       
        // 키보드 포커스 가능하도록 설정
        this.Focusable = true;

        _log?.Info($"MarkerEditAdorner 생성: {_targetMarker.Title}");
    }

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
                // 드래그 상태 정리
                if (_isDragging)
                {
                    CancelEditing();
                }

                // 리소스 정리
                _handlePen?.Freeze();
                _editAreaPen?.Freeze();

                // 이벤트 해제 (필요한 경우)
                EditStarted = null;
                Editing = null;
                EditCompleted = null;
                EditCancelled = null;

                _log?.Info($"MarkerEditAdorner 리소스 해제: {_targetMarker?.Title}");
            }
            catch (Exception ex)
            {
                _log?.Error($"MarkerEditAdorner 해제 중 오류: {ex.Message}");
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
    ~MarkerEditAdorner()
    {
        Dispose(false);
    }

    #endregion

    #region Initialization

    /// <summary>
    /// 시각적 요소들 초기화
    /// </summary>
    private void InitializeVisualElements()
    {
        // 핸들 테두리 펜
        _handlePen = new Pen(Brushes.White, 1);
        _handlePen.Freeze();

        // 편집 영역 펜 (점선)
        _editAreaPen = new Pen(Brushes.Blue, 1)
        {
            DashStyle = DashStyles.Dash,
        };
        _editAreaPen.Freeze();

        // 핸들 브러시들
        _moveHandleBrush = Brushes.Blue;
        _rotateHandleBrush = Brushes.Green;
        _resizeHandleBrush = Brushes.Orange;

        // 정보 표시 배경
        _infoBackgroundBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
        _infoBackgroundBrush.Freeze();
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Adorner 렌더링
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        try
        {
            if (_targetMarker == null || _mapControl == null) return;

            // AdornedElement의 실제 렌더 영역 사용
            var elementBounds = new Rect(AdornedElement.RenderSize);
            var markerCenter = new Point(elementBounds.Width / 2, elementBounds.Height / 2);
            _log?.Info($"Adorner 렌더링 - 요소크기: {elementBounds.Width}x{elementBounds.Height}, 중심: ({markerCenter.X}, {markerCenter.Y})");


            _log?.Info($"Adorner 렌더링 - 요소크기: {elementBounds.Width}x{elementBounds.Height}, 중심: ({markerCenter.X}, {markerCenter.Y})");

            var editRadius = CalculateEditRadius();

            // 편집 영역 및 핸들 렌더링
            RenderEditArea(drawingContext, markerCenter, editRadius);
            RenderEditHandles(drawingContext, markerCenter, editRadius);

            if (_editState.ShowInfo)
            {
                RenderMarkerInfo(drawingContext, markerCenter, editRadius);
            }

            if (_isDragging)
            {
                RenderDragFeedback(drawingContext, markerCenter);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"MarkerEditAdorner 렌더링 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 편집 영역 배경 렌더링 (마커 크기에 맞춤)
    /// </summary>
    private void RenderEditArea(DrawingContext drawingContext, Point markerCenter, double editRadius)
    {
        var editBrush = new SolidColorBrush(Colors.Blue) { Opacity = 0.1 };

        // 실제 마커 크기를 반영한 사각형
        var markerWidth = _targetMarker.Width;
        var markerHeight = _targetMarker.Height;

        var editRect = new Rect(
            markerCenter.X - markerWidth / 2 - PADDING,
            markerCenter.Y - markerHeight / 2 - PADDING,
            markerWidth + PADDING * 2,
            markerHeight + PADDING * 2);

        drawingContext.DrawRectangle(editBrush, _editAreaPen, editRect);
    }

    /// <summary>
    /// 편집 핸들들 렌더링 (마커 크기 기준)
    /// </summary>
    private void RenderEditHandles(DrawingContext drawingContext, Point markerCenter, double editRadius)
    {
        var handleSize = MarkerEditSettings.HandleSize;

        // 실제 마커 크기를 기준으로 사각형 계산 (편집 영역과 동일)
        var markerWidth = _targetMarker.Width;
        var markerHeight = _targetMarker.Height;

        var markerBounds = new Rect(
            markerCenter.X - markerWidth / 2 - PADDING,
            markerCenter.Y - markerHeight / 2 - PADDING,
            markerWidth + PADDING * 2,
            markerHeight + PADDING * 2);

        // 1. 이동 핸들 (중심, 원형, 파란색)
        drawingContext.DrawEllipse(_moveHandleBrush, _handlePen, markerCenter, handleSize, handleSize);

        // 2. 회전 핸들 (북쪽, 원형, 초록색)
        var rotateHandlePos = new Point(markerCenter.X, markerBounds.Top - MarkerEditSettings.RotateHandleDistance);
        drawingContext.DrawEllipse(_rotateHandleBrush, _handlePen, rotateHandlePos, handleSize * 0.75, handleSize * 0.75);

        // 회전 핸들 연결선
        var connectionPen = new Pen(_rotateHandleBrush, 1) { DashStyle = DashStyles.Dot };
        drawingContext.DrawLine(connectionPen,
            new Point(markerCenter.X, markerBounds.Top),
            new Point(rotateHandlePos.X, rotateHandlePos.Y + handleSize * 0.75));

        // 3. 모서리 핸들들 (사각형, 파란색 - 비율 유지)
        var cornerHandleBrush = Brushes.Blue;
        var cornerHandles = new[]
        {
        new Point(markerBounds.Left, markerBounds.Top),      // 좌상단
        new Point(markerBounds.Right, markerBounds.Top),     // 우상단
        new Point(markerBounds.Right, markerBounds.Bottom),  // 우하단
        new Point(markerBounds.Left, markerBounds.Bottom)    // 좌하단
    };

        foreach (var handlePos in cornerHandles)
        {
            var handleRect = new Rect(
                handlePos.X - handleSize / 2,
                handlePos.Y - handleSize / 2,
                handleSize, handleSize);
            drawingContext.DrawRectangle(cornerHandleBrush, _handlePen, handleRect);
        }

        // 4. 변 중앙 핸들들 (원형, 주황색 - 자유 조정)
        var edgeHandleBrush = Brushes.Orange;
        var edgeHandles = new[]
        {
        new Point(markerCenter.X, markerBounds.Top),         // 상단 중점
        new Point(markerBounds.Right, markerCenter.Y),       // 우측 중점
        new Point(markerCenter.X, markerBounds.Bottom),      // 하단 중점
        new Point(markerBounds.Left, markerCenter.Y)         // 좌측 중점
    };

        foreach (var handlePos in edgeHandles)
        {
            drawingContext.DrawEllipse(edgeHandleBrush, _handlePen, handlePos, handleSize / 2, handleSize / 2);
        }
    }

    /// <summary>
    /// 마커 정보 표시
    /// </summary>
    private void RenderMarkerInfo(DrawingContext drawingContext, Point markerScreenPos, double editRadius)
    {
        var infoText = CreateInfoText();
        var textPos = new Point(markerScreenPos.X - infoText.Width / 2,
            markerScreenPos.Y + editRadius + 20);

        // 배경 사각형
        var textBackground = new Rect(textPos.X - 4, textPos.Y - 2,
            infoText.Width + 8, infoText.Height + 4);
        drawingContext.DrawRectangle(_infoBackgroundBrush,
            new Pen(Brushes.Gray, 1), textBackground);

        // 텍스트
        drawingContext.DrawText(infoText, textPos);
    }

    /// <summary>
    /// 드래그 피드백 렌더링
    /// </summary>
    private void RenderDragFeedback(DrawingContext drawingContext, Point markerScreenPos)
    {
        // 드래그 중 실시간 좌표/크기/각도 표시
        var feedbackText = CreateDragFeedbackText();
        var feedbackPos = new Point(markerScreenPos.X + 30, markerScreenPos.Y - 30);

        var feedbackBackground = new Rect(feedbackPos.X - 2, feedbackPos.Y - 2,
            feedbackText.Width + 4, feedbackText.Height + 4);

        drawingContext.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(200, 255, 255, 0)), // 노란 배경
            new Pen(Brushes.Orange, 1), feedbackBackground);

        drawingContext.DrawText(feedbackText, feedbackPos);
    }

    #endregion

    #region Mouse Event Handling

    /// <summary>
    /// 마우스 왼쪽 버튼 다운
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        try
        {
            var mousePos = e.GetPosition(this);

            // Adorner 내부 좌표계 사용 (지도 좌표 변환 제거)
            var elementBounds = new Rect(AdornedElement.RenderSize);
            var markerCenter = new Point(elementBounds.Width / 2, elementBounds.Height / 2);

            _log?.Info($"마우스 클릭 - 위치: ({mousePos.X:F1}, {mousePos.Y:F1}), 마커중심: ({markerCenter.X:F1}, {markerCenter.Y:F1})");

            // 클릭된 핸들 감지
            _activeHandle = DetectClickedHandle(mousePos, markerCenter);

            if (_activeHandle != MarkerHandle.None)
            {
                StartEditing(mousePos);
                e.Handled = true;
            }

            base.OnMouseLeftButtonDown(e);
        }
        catch (Exception ex)
        {
            _log?.Error($"마우스 다운 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마우스 이동
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        try
        {
            var mousePos = e.GetPosition(this);

            if (_isDragging && _activeHandle != MarkerHandle.None)
            {
                ProcessDrag(mousePos);
                InvalidateVisual();
            }
            else
            {
                // 마우스 커서 변경
                UpdateCursor(mousePos);
            }

            base.OnMouseMove(e);
        }
        catch (Exception ex)
        {
            _log?.Error($"마우스 이동 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마우스 왼쪽 버튼 업
    /// </summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        try
        {
            if (_isDragging)
            {
                CompleteEditing();
                e.Handled = true;
            }

            base.OnMouseLeftButtonUp(e);
        }
        catch (Exception ex)
        {
            _log?.Error($"마우스 업 처리 실패: {ex.Message}");
        }
    }

    #endregion

    #region Keyboard Event Handling

    /// <summary>
    /// 키보드 이벤트 처리
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        try
        {
            switch (e.Key)
            {
                case Key.Escape:
                    CancelEditing();
                    e.Handled = true;
                    break;

                case Key.Delete:
                    // 마커 삭제 요청
                    RequestMarkerDeletion();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    if (_isDragging)
                    {
                        CompleteEditing();
                        e.Handled = true;
                    }
                    break;
            }
            base.OnKeyDown(e);
        }
        catch (Exception ex)
        {
            _log?.Error($"키보드 이벤트 처리 실패: {ex.Message}");
        }
    }

    #endregion

    #region Edit State Management

    /// <summary>
    /// 편집 시작
    /// </summary>
    private void StartEditing(Point mousePos)
    {
        _isDragging = true;
        _dragStartPoint = mousePos;
        _editState.IsEditing = true;
        _editState.EditMode = ConvertHandleToEditMode(_activeHandle);

        // 원본 데이터 백업
        BackupOriginalData();

        // 마우스 캡처
        this.CaptureMouse();

        // 이벤트 발생
        EditStarted?.Invoke(this, new MarkerEditStartedEventArgs(_targetMarker, _activeHandle));

        _log?.Info($"편집 시작: {_targetMarker.Title}, 핸들: {_activeHandle}");
    }

    /// <summary>
    /// 편집 완료
    /// </summary>
    private void CompleteEditing()
    {
        if (!_isDragging) return;

        _isDragging = false;
        _editState.IsEditing = false;
        _editState.EditMode = MarkerEditMode.None;
        _activeHandle = MarkerHandle.None;

        // 마우스 캡처 해제
        this.ReleaseMouseCapture();

        // 이벤트 발생
        EditCompleted?.Invoke(this, new MarkerEditCompletedEventArgs(_targetMarker,
            _originalPosition, _originalWidth, _originalHeight, _originalBearing));

        _log?.Info($"편집 완료: {_targetMarker.Title}");

        InvalidateVisual();
    }

    /// <summary>
    /// 편집 취소
    /// </summary>
    private void CancelEditing()
    {
        if (!_isDragging) return;

        // 원본 데이터 복원
        RestoreOriginalData();

        _isDragging = false;
        _editState.IsEditing = false;
        _editState.EditMode = MarkerEditMode.None;
        _activeHandle = MarkerHandle.None;

        // 마우스 캡처 해제
        this.ReleaseMouseCapture();

        // 이벤트 발생
        EditCancelled?.Invoke(this, new MarkerEditCancelledEventArgs(_targetMarker));

        _log?.Info($"편집 취소: {_targetMarker.Title}");

        InvalidateVisual();
    }

    #endregion

    #region Drag Processing

    // MarkerEditAdorner.cs - ProcessDrag 수정
    private void ProcessDrag(Point currentPos)
    {
        var deltaX = currentPos.X - _dragStartPoint.X;
        var deltaY = currentPos.Y - _dragStartPoint.Y;

        switch (_activeHandle)
        {
            case MarkerHandle.Move:
                ProcessMoveOperation(currentPos);
                break;

            case MarkerHandle.Rotate:
                ProcessRotateOperation(currentPos);
                break;

            // 모서리 핸들 - 비율 유지 크기 조정
            case MarkerHandle.ResizeTopLeft:
            case MarkerHandle.ResizeTopRight:
            case MarkerHandle.ResizeBottomLeft:
            case MarkerHandle.ResizeBottomRight:
                ProcessProportionalResize(deltaX, deltaY); // corner 매개변수 제거
                break;

            // 변 중앙 핸들 - 자유 크기 조정
            case MarkerHandle.ResizeTop:
            case MarkerHandle.ResizeBottom:
                ProcessVerticalResize(deltaY);
                break;

            case MarkerHandle.ResizeLeft:
            case MarkerHandle.ResizeRight:
                ProcessHorizontalResize(deltaX); // 쉼표 제거
                break;
        }

        // 실시간 편집 이벤트 발생
        Editing?.Invoke(this, new MarkerEditingEventArgs(_targetMarker, _activeHandle, deltaX, deltaY));

        // 드래그 시작점 업데이트
        _dragStartPoint = currentPos;
    }
    /// <summary>
    /// 이동 처리
    /// </summary>
    private void ProcessMoveOperation(Point currentPos)
    {
        try
        {
            // AdornedElement를 기준으로 상대 좌표 계산
            var elementToMap = AdornedElement.TransformToAncestor(_mapControl);
            var mapRelativePos = elementToMap.Transform(currentPos);

            _log?.Info($"좌표 변환: Adorner({currentPos.X:F1}, {currentPos.Y:F1}) -> Map({mapRelativePos.X:F1}, {mapRelativePos.Y:F1})");

            // 지리 좌표로 변환
            var newGeoPos = _mapControl.FromLocalToLatLng((int)mapRelativePos.X, (int)mapRelativePos.Y);

            _log?.Info($"지리 좌표: ({newGeoPos.Lat:F6}, {newGeoPos.Lng:F6})");

            _targetMarker.UpdateLocation(newGeoPos);
        }
        catch (Exception ex)
        {
            _log?.Error($"이동 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 회전 처리
    /// </summary>
    private void ProcessRotateOperation(Point currentPos)
    {
        try
        {
            // AdornedElement 기준으로 마커 중심 계산
            var elementBounds = new Rect(AdornedElement.RenderSize);
            var markerCenter = new Point(elementBounds.Width / 2, elementBounds.Height / 2);

            var angle = MarkerEditUtils.CalculateAngle(markerCenter, currentPos);

            // 15도 단위로 스냅 (Shift 키를 누르지 않은 경우)
            if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                angle = Math.Round(angle / 15.0) * 15.0;
            }

            _targetMarker.UpdateRotation(angle);
            _log?.Info($"회전 조정: {angle:F1}°");
        }
        catch (Exception ex)
        {
            _log?.Error($"회전 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 비율 유지 마커 크기 조정 (모서리 핸들)
    /// </summary>
    private void ProcessProportionalResize(double deltaX, double deltaY)
    {
        try
        {
            var currentWidth = _targetMarker.Width;
            var currentHeight = _targetMarker.Height;
            var aspectRatio = currentWidth / currentHeight;

            // 더 직관적인 크기 계산
            double sizeChange = 0;

            switch (_activeHandle)
            {
                case MarkerHandle.ResizeTopLeft:
                    sizeChange = -(deltaX + deltaY) / 2; // 축소/확대
                    break;
                case MarkerHandle.ResizeTopRight:
                    sizeChange = (deltaX - deltaY) / 2;
                    break;
                case MarkerHandle.ResizeBottomLeft:
                    sizeChange = (-deltaX + deltaY) / 2;
                    break;
                case MarkerHandle.ResizeBottomRight:
                    sizeChange = (deltaX + deltaY) / 2;  // 확대/축소
                    break;
            }

            // 새로운 크기 계산
            double newWidth = MarkerEditUtils.Clamp(currentWidth + sizeChange, 10, 500);
            double newHeight = MarkerEditUtils.Clamp(newWidth / aspectRatio, 10, 500);

            // 비율 재조정
            if (newHeight * aspectRatio != newWidth)
            {
                newWidth = newHeight * aspectRatio;
                newWidth = MarkerEditUtils.Clamp(newWidth, 10, 500);
            }

            _targetMarker.UpdateSize(newWidth, newHeight);
            _log?.Info($"비율 유지 크기 조정: {newWidth:F0}×{newHeight:F0}");
        }
        catch (Exception ex)
        {
            _log?.Error($"비율 유지 크기 조정 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 수직 크기 조정 (자유 조정 - 비율 무시)
    /// </summary>
    private void ProcessVerticalResize(double deltaY)
    {
        try
        {
            var currentHeight = _targetMarker.Height;

            double newHeight = _activeHandle switch
            {
                // 상단 핸들: 위로 드래그(-) = 확대, 아래로 드래그(+) = 축소
                MarkerHandle.ResizeTop => currentHeight - deltaY,
                // 하단 핸들: 아래로 드래그(+) = 확대, 위로 드래그(-) = 축소
                MarkerHandle.ResizeBottom => currentHeight + deltaY,
                _ => currentHeight
            };

            newHeight = MarkerEditUtils.Clamp(newHeight, 10, 500);

            _targetMarker.UpdateSize(_targetMarker.Width, newHeight);

            if (AdornedElement is GMapMarkerBasicCustomControl markerControl)
            {
                markerControl.Width = _targetMarker.Width;
                markerControl.Height = newHeight;
                markerControl.InvalidateVisual();
            }

            _log?.Info($"높이 자유 조정: {_targetMarker.Width:F0}×{newHeight:F0}");
        }
        catch (Exception ex)
        {
            _log?.Error($"수직 크기 조정 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 수평 크기 조정 (자유 조정 - 비율 무지)
    /// </summary>
    private void ProcessHorizontalResize(double deltaX)
    {
        try
        {
            var currentWidth = _targetMarker.Width;

            double newWidth = _activeHandle switch
            {
                // 왼쪽 핸들: 왼쪽으로 드래그(-) = 확대, 오른쪽으로 드래그(+) = 축소
                MarkerHandle.ResizeLeft => currentWidth - deltaX,
                // 오른쪽 핸들: 오른쪽으로 드래그(+) = 확대, 왼쪽으로 드래그(-) = 축소
                MarkerHandle.ResizeRight => currentWidth + deltaX,
                _ => currentWidth
            };

            newWidth = MarkerEditUtils.Clamp(newWidth, 10, 500);

            _targetMarker.UpdateSize(newWidth, _targetMarker.Height);

            if (AdornedElement is GMapMarkerBasicCustomControl markerControl)
            {
                markerControl.Width = newWidth;
                markerControl.Height = _targetMarker.Height;
                markerControl.InvalidateVisual();
            }

            _log?.Info($"너비 자유 조정: {newWidth:F0}×{_targetMarker.Height:F0}");
        }
        catch (Exception ex)
        {
            _log?.Error($"수평 크기 조정 실패: {ex.Message}");
        }
    }
    #endregion

    #region Helper Methods

    /// <summary>
    /// 클릭된 핸들 감지 (마커 크기 기준)
    /// </summary>
    private MarkerHandle DetectClickedHandle(Point mousePos, Point markerCenter)
    {
        var tolerance = MarkerEditSettings.HandleTolerance;

        // 🔧 실제 마커 크기를 기준으로 사각형 계산 (편집 영역과 동일)
        var markerWidth = _targetMarker.Width;
        var markerHeight = _targetMarker.Height;
       
        var markerBounds = new Rect(
            markerCenter.X - markerWidth / 2 - PADDING,
            markerCenter.Y - markerHeight / 2 - PADDING,
            markerWidth + PADDING * 2,
            markerHeight + PADDING * 2);

        _log?.Info($"핸들 감지 - 마우스: ({mousePos.X:F1}, {mousePos.Y:F1}), 마커중심: ({markerCenter.X:F1}, {markerCenter.Y:F1})");

        // 1. 이동 핸들 (중심)
        if (IsPointNear(mousePos, markerCenter, tolerance))
        {
            _log?.Info("이동 핸들 감지됨");
            return MarkerHandle.Move;
        }

        // 2. 회전 핸들 (북쪽)
        var rotateHandlePos = new Point(markerCenter.X, markerBounds.Top - MarkerEditSettings.RotateHandleDistance);
        if (IsPointNear(mousePos, rotateHandlePos, tolerance))
        {
            _log?.Info("회전 핸들 감지됨");
            return MarkerHandle.Rotate;
        }

        // 3. 모서리 핸들들 (비율 유지)
        var cornerHandles = new[]
        {
        (new Point(markerBounds.Left, markerBounds.Top), MarkerHandle.ResizeTopLeft),
        (new Point(markerBounds.Right, markerBounds.Top), MarkerHandle.ResizeTopRight),
        (new Point(markerBounds.Right, markerBounds.Bottom), MarkerHandle.ResizeBottomRight),
        (new Point(markerBounds.Left, markerBounds.Bottom), MarkerHandle.ResizeBottomLeft)
    };

        foreach (var (handlePos, handleType) in cornerHandles)
        {
            if (IsPointNear(mousePos, handlePos, tolerance))
            {
                _log?.Info($"{handleType} 핸들 감지됨 (비율 유지)");
                return handleType;
            }
        }

        // 4. 변 중앙 핸들들 (자유 조정)
        var edgeHandles = new[]
        {
        (new Point(markerCenter.X, markerBounds.Top), MarkerHandle.ResizeTop),
        (new Point(markerBounds.Right, markerCenter.Y), MarkerHandle.ResizeRight),
        (new Point(markerCenter.X, markerBounds.Bottom), MarkerHandle.ResizeBottom),
        (new Point(markerBounds.Left, markerCenter.Y), MarkerHandle.ResizeLeft)
    };

        foreach (var (handlePos, handleType) in edgeHandles)
        {
            if (IsPointNear(mousePos, handlePos, tolerance))
            {
                _log?.Info($"{handleType} 핸들 감지됨 (자유 조정)");
                return handleType;
            }
        }

        return MarkerHandle.None;
    }

    /// <summary>
    /// 편집 반경 계산 (호환성 유지)
    /// </summary>
    private double CalculateEditRadius()
    {
        // 실제 마커 크기 기반으로 계산 (기존 로직 변경)
        var markerWidth = _targetMarker.Width;
        var markerHeight = _targetMarker.Height;
        return Math.Max(markerWidth, markerHeight) / 2.0 + PADDING; // padding 1과 일치
    }

    /// <summary>
    /// 두 점이 허용 범위 내에 있는지 확인
    /// </summary>
    private bool IsPointNear(Point p1, Point p2, double tolerance)
    {
        var distance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        return distance <= tolerance;
    }

    /// <summary>
    /// 마우스 커서 업데이트
    /// </summary>
    private void UpdateCursor(Point mousePos)
    {
        var elementBounds = new Rect(AdornedElement.RenderSize);
        var markerCenter = new Point(elementBounds.Width / 2, elementBounds.Height / 2);
        var handle = DetectClickedHandle(mousePos, markerCenter);

        this.Cursor = handle switch
        {
            MarkerHandle.Move => Cursors.SizeAll,
            MarkerHandle.Rotate => Cursors.Hand,

            // 모서리 핸들 - 대각선 커서
            MarkerHandle.ResizeTopLeft or MarkerHandle.ResizeBottomRight => Cursors.SizeNWSE,
            MarkerHandle.ResizeTopRight or MarkerHandle.ResizeBottomLeft => Cursors.SizeNESW,

            // 변 중앙 핸들 - 직선 커서  
            MarkerHandle.ResizeTop or MarkerHandle.ResizeBottom => Cursors.SizeNS,
            MarkerHandle.ResizeLeft or MarkerHandle.ResizeRight => Cursors.SizeWE,

            _ => Cursors.Arrow
        };
    }

    /// <summary>
    /// 원본 데이터 백업
    /// </summary>
    private void BackupOriginalData()
    {
        _originalPosition = _targetMarker.Position;
        _originalWidth = _targetMarker.Width;
        _originalHeight = _targetMarker.Height;
        _originalBearing = _targetMarker.Bearing;
    }

    /// <summary>
    /// 원본 데이터 복원
    /// </summary>
    private void RestoreOriginalData()
    {
        _targetMarker.UpdateLocation(_originalPosition);
        _targetMarker.UpdateSize(_originalWidth, _originalHeight);
        _targetMarker.UpdateRotation(_originalBearing);
    }

    /// <summary>
    /// 핸들을 편집 모드로 변환
    /// </summary>
    private MarkerEditMode ConvertHandleToEditMode(MarkerHandle handle)
    {
        return handle switch
        {
            MarkerHandle.Move => MarkerEditMode.Move,
            MarkerHandle.Rotate => MarkerEditMode.Rotate,

            // 새로운 핸들 타입들 추가
            MarkerHandle.ResizeTopLeft or MarkerHandle.ResizeTopRight or
            MarkerHandle.ResizeBottomLeft or MarkerHandle.ResizeBottomRight or
            MarkerHandle.ResizeTop or MarkerHandle.ResizeBottom or
            MarkerHandle.ResizeLeft or MarkerHandle.ResizeRight => MarkerEditMode.Resize,

            _ => MarkerEditMode.None
        };
    }

    /// <summary>
    /// 정보 텍스트 생성
    /// </summary>
    private FormattedText CreateInfoText()
    {
        var infoString = $"{_targetMarker.Title}\n" +
                        $"크기: {_targetMarker.Width:F0}×{_targetMarker.Height:F0}\n" +
                        $"회전: {_targetMarker.Bearing:F0}°\n" +
                        $"위치: ({_targetMarker.Position.Lat:F6}, {_targetMarker.Position.Lng:F6})";

        return new FormattedText(infoString,
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Arial"), 10, Brushes.Black, 96);
    }

    /// <summary>
    /// 드래그 피드백 텍스트 생성
    /// </summary>
    private FormattedText CreateDragFeedbackText()
    {
        var feedbackString = _activeHandle switch
        {
            MarkerHandle.Move => $"위치: ({_targetMarker.Position.Lat:F6}, {_targetMarker.Position.Lng:F6})",
            MarkerHandle.Rotate => $"회전: {_targetMarker.Bearing:F0}°",

            // 🔧 새로운 핸들 타입들 추가
            MarkerHandle.ResizeLeft or MarkerHandle.ResizeRight => $"너비: {_targetMarker.Width:F0}px",
            MarkerHandle.ResizeTop or MarkerHandle.ResizeBottom => $"높이: {_targetMarker.Height:F0}px",
            MarkerHandle.ResizeTopLeft or MarkerHandle.ResizeTopRight or
            MarkerHandle.ResizeBottomLeft or MarkerHandle.ResizeBottomRight => $"크기: {_targetMarker.Width:F0}×{_targetMarker.Height:F0}",

            _ => "편집 중..."
        };

        return new FormattedText(feedbackString,
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Arial"), 9, Brushes.Black, 96);
    }

    /// <summary>
    /// 마커 삭제 요청
    /// </summary>
    private void RequestMarkerDeletion()
    {
        // 마커 삭제는 외부에서 처리하도록 이벤트 발생
        // TODO: MarkerDeletionRequested 이벤트 추가 고려
        _log?.Info($"마커 삭제 요청: {_targetMarker.Title}");
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// 편집 상태
    /// </summary>
    public MarkerEditState EditState => _editState;

    /// <summary>
    /// 대상 마커
    /// </summary>
    public GMapCustomMarker TargetMarker => _targetMarker;

    /// <summary>
    /// 정보 표시 여부
    /// </summary>
    public bool ShowInfo
    {
        get => _editState.ShowInfo;
        set
        {
            _editState.ShowInfo = value;
            InvalidateVisual();
        }
    }

    public const int PADDING = 5;

    #endregion
}