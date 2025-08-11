using Caliburn.Micro;
using GMap.NET.WindowsPresentation;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
using System.Windows.Controls;
using System.Windows.Documents;
using System.ComponentModel;
using Action = System.Action;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using log4net.Core;
using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

/// <summary>
/// 선택적 Adorner 시스템을 적용한 GMapCustomControl
/// - 기본 모드: 기존 GMap.NET 기능 100% 활용
/// - 편집 모드: 선택된 객체만 Adorner로 변환하여 편집 기능 제공
/// </summary>
public class GMapCustomControl : GMapControl
{
    #region - Ctors -
    public GMapCustomControl()
    {
        _eventAggregator = IoC.Get<IEventAggregator>();
        _log = IoC.Get<ILogService>();
        
        Markers.CollectionChanged += Markers_CollectionChanged;

        // 커스텀 컬렉션 초기화
        CustomMarkers = new ObservableCollection<GMapCustomMarker>();
        CustomImages = new ObservableCollection<GMapCustomImage>();

        // Adorner 관리용 컬렉션 (편집 모드에서만 사용)
        AdornerItems = new ObservableCollection<GMapAdornerWrapper>();
        AdornerItems.CollectionChanged += AdornerItems_CollectionChanged;

        OnAreaChange += GMapCustomControl_OnAreaChange;
        _mgrsOverlay = new MGRSGridOverlayService(_log);
        _log?.Info("GMapCustomControl 초기화 완료 - 선택적 Adorner 시스템");
    }

    public GMapCustomControl(IEventAggregator eventAggregator, ILogService log)
        : this()
    {
        _eventAggregator = eventAggregator;
        _log = log;
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -

    protected override void OnInitialized(EventArgs e)
    {
        _eventAggregator?.SubscribeOnUIThread(this);
        base.OnInitialized(e);

        // 초기화가 완료된 후 Adorner 레이어 생성
        Loaded += OnGMapLoaded;
    }

    /// <summary>
    /// GMap이 로드된 후 Adorner 레이어 생성 (안전한 타이밍)
    /// </summary>
    private void OnGMapLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // SizeChanged 이벤트가 발생할 때까지 대기 (GMap이 완전히 초기화된 후)
            SizeChanged += OnFirstSizeChanged;
        }
        catch (Exception ex)
        {
            _log?.Error($"GMap 로드 이벤트 처리 실패: {ex.Message}");
        }

        Loaded -= OnGMapLoaded; // 이벤트 해제
    }


    /// <summary>
    /// 첫 번째 SizeChanged 이벤트에서 Adorner 레이어 생성
    /// </summary>
    private void OnFirstSizeChanged(object sender, SizeChangedEventArgs e)
    {
        SizeChanged -= OnFirstSizeChanged; // 이벤트 해제

        _log?.Info("OnFirstSizeChanged 호출됨 - AdornerLayer 생성 시작");

        // 약간의 지연 후 Adorner 레이어 생성 (GMap 완전 초기화 대기)
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _log?.Info("지연된 CreateAdornerLayer 호출");
            CreateAdornerLayer();

            // 생성 확인
            _log?.Info($"CreateAdornerLayer 완료 후 _adornerLayer: {_adornerLayer != null}");

        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void GMapCustomControl_OnAreaChange(RectLatLng selection, double zoom, bool zoomToFit)
    {
        _log?.Info($"Selection Changed: {selection}, Zoom: {zoom}, ZoomToFit: {zoomToFit}");

        // 기존 GMap 마커들의 가시성 처리
        Markers.OfType<GMapCustomMarker>().ToList().ForEach(entity =>
        {
            if (Zoom <= VISIBILITY_ZOOM)
                entity.Visibility = false;
            else
                entity.Visibility = true;
        });

        // 이미지 오버레이 가시성 업데이트
        UpdateImageOverlaysVisibility();

        // Adorner 위치 업데이트 (편집 모드인 경우)
        if (IsEditMode)
        {
            UpdateAdornerPositions();
        }
    }

    /// <summary>
    /// 모든 Adorner의 위치 업데이트
    /// </summary>
    private void UpdateAdornerPositions()
    {
        foreach (var adorner in AdornerItems)
        {
            adorner.UpdatePosition();
        }
    }

    private void Markers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var newItem in (e.NewItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    CustomMarkers.Add(newItem);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var oldItem in (e.OldItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    var entity = CustomMarkers.Where(entity => entity.Id == oldItem.Id).FirstOrDefault();
                    if (entity != null)
                        CustomMarkers.Remove(entity);

                    // Adorner 모드인 경우 해당 Adorner도 제거
                    var adorner = AdornerItems.OfType<MarkerAdornerWrapper>()
                        .FirstOrDefault(a => a.CustomMarker.Id == oldItem.Id);
                    if (adorner != null)
                    {
                        AdornerItems.Remove(adorner);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                int index = 0;
                foreach (var oldItem in (e.OldItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    var entity = CustomMarkers.Where(entity => entity.Id == oldItem.Id).FirstOrDefault();
                    if (entity != null)
                    {
                        index = CustomMarkers.IndexOf(entity);
                        CustomMarkers.Remove(entity);
                    }
                }

                foreach (var newItem in (e.NewItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    CustomMarkers.Insert(index, newItem);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                CustomMarkers.Clear();
                foreach (var newItem in Markers.OfType<GMapCustomMarker>().ToList())
                {
                    CustomMarkers.Add(newItem);
                }
                break;
        }
    }
   
    /// <summary>
    /// 이미지 오버레이 렌더링 (기존 기능 유지)
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        RenderImageOverlays(drawingContext);

        RenderMarkerEditHandles(drawingContext);
        if (ShowMGRSGrid)
        {
            _mgrsOverlay.DrawMGRSGrid(drawingContext, ViewArea, (int)Zoom, this);
        }

        // 회전 정보 표시
        if (ShowRotationControl)
        {
            RenderRotationInfo(drawingContext);
        }
    }

    private void RenderMarkerEditHandles(DrawingContext drawingContext)
    {
        foreach (var marker in CustomMarkers.Where(m => m.IsSelected))
        {
            var screenPos = FromLatLngToLocal(marker.Position);
            var handleRect = new Rect(screenPos.X - 20, screenPos.Y - 20, 40, 40);

            // 마커 주변에 편집 영역 표시
            var editBrush = new SolidColorBrush(Colors.Blue) { Opacity = 0.3 };
            var editPen = new Pen(Brushes.Blue, 2) { DashStyle = DashStyles.Dash };

            drawingContext.DrawEllipse(editBrush, editPen,
                new Point(screenPos.X, screenPos.Y), 25, 25);

            // 이동 핸들 (중앙)
            drawingContext.DrawEllipse(Brushes.Blue, new Pen(Brushes.White, 1),
                new Point(screenPos.X, screenPos.Y), 8, 8);

            // 회전 핸들 (상단)
            drawingContext.DrawEllipse(Brushes.Green, new Pen(Brushes.White, 1),
                new Point(screenPos.X, screenPos.Y - 30), 6, 6);

            // 크기 조정 핸들 (우측)
            drawingContext.DrawRectangle(Brushes.Orange, new Pen(Brushes.White, 1),
                new Rect(screenPos.X + 20, screenPos.Y - 4, 8, 8));
        }
    }

    private void RenderImageOverlays(DrawingContext drawingContext)
    {
        try
        {
            foreach (var customImage in CustomImages)
            {
                // Adorner 모드인지 확인
                var isInAdornerMode = AdornerItems.OfType<ImageAdornerWrapper>()
                    .Any(a => a.CustomImage == customImage);

                // Adorner 모드가 아닌 경우에만 원본 이미지 렌더링
                // Adorner 모드인 경우 Adorner가 렌더링을 담당
                if (!isInAdornerMode && customImage.Visibility)
                {
                    RenderSingleImageOverlay(drawingContext, customImage);
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 렌더링 실패: {ex.Message}");
        }
    }

    private void RenderSingleImageOverlay(DrawingContext drawingContext, GMapCustomImage customImage)
    {
        if (customImage?.Img == null) return;

        try
        {
            var bounds = customImage.ImageBounds;
            var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
            var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

            var imageRect = new Rect(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y
            );

            // 회전 처리
            if (customImage.Rotation != 0)
            {
                var centerX = imageRect.X + imageRect.Width / 2;
                var centerY = imageRect.Y + imageRect.Height / 2;
                var rotateTransform = new RotateTransform(customImage.Rotation, centerX, centerY);
                drawingContext.PushTransform(rotateTransform);
            }

            // 투명도 처리
            if (customImage.Opacity < 1.0)
            {
                drawingContext.PushOpacity(customImage.Opacity);
            }

            // 이미지 그리기
            drawingContext.DrawImage(customImage.Img, imageRect);

            // 📌 선택된 이미지인 경우 테두리와 핸들 표시
            if (ShowImageBounds || customImage.IsSelected)
            {
                var boundsPen = new Pen(Brushes.Red, 2) { DashStyle = DashStyles.Dash };
                drawingContext.DrawRectangle(null, boundsPen, imageRect);

                // 📌 선택된 이미지에만 핸들 표시
                if (customImage.IsSelected && IsEditMode)
                {
                    DrawResizeHandles(drawingContext, imageRect);
                }

                if (!string.IsNullOrEmpty(customImage.Title))
                {
                    var nameText = new FormattedText(
                        customImage.Title,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        12,
                        Brushes.Red,
                        96);

                    drawingContext.DrawText(nameText, new Point(imageRect.X, imageRect.Y - 15));
                }
            }

            if (customImage.Opacity < 1.0)
                drawingContext.Pop();

            if (customImage.Rotation != 0)
                drawingContext.Pop();
        }
        catch (Exception ex)
        {
            _log?.Error($"단일 이미지 오버레이 렌더링 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 크기 조정 핸들 그리기 (모서리와 변 중앙 구분)
    /// </summary>
    private void DrawResizeHandles(DrawingContext drawingContext, Rect imageRect)
    {
        var handleSize = 8;
        var cornerHandleBrush = Brushes.Blue;        // 📌 모서리: 파란색 (비율 유지)
        var edgeHandleBrush = Brushes.Orange;        // 📌 변 중앙: 주황색 (자유 조정)
        var handlePen = new Pen(Brushes.White, 1);

        // 📌 모서리 핸들 (사각형 모양) - 비율 유지
        var cornerHandles = new[]
        {
        new Point(imageRect.Left, imageRect.Top),      // 좌상
        new Point(imageRect.Right, imageRect.Top),     // 우상  
        new Point(imageRect.Right, imageRect.Bottom),  // 우하
        new Point(imageRect.Left, imageRect.Bottom),   // 좌하
    };

        // 📌 변 중앙 핸들 (원형 모양) - 자유 조정
        var edgeHandles = new[]
        {
        new Point(imageRect.Left + imageRect.Width/2, imageRect.Top),    // 상중
        new Point(imageRect.Right, imageRect.Top + imageRect.Height/2),  // 우중
        new Point(imageRect.Left + imageRect.Width/2, imageRect.Bottom), // 하중
        new Point(imageRect.Left, imageRect.Top + imageRect.Height/2)    // 좌중
    };

        // 모서리 핸들 그리기 (사각형)
        foreach (var handle in cornerHandles)
        {
            var handleRect = new Rect(
                handle.X - handleSize / 2,
                handle.Y - handleSize / 2,
                handleSize,
                handleSize
            );
            drawingContext.DrawRectangle(cornerHandleBrush, handlePen, handleRect);
        }

        // 변 중앙 핸들 그리기 (원형)
        foreach (var handle in edgeHandles)
        {
            drawingContext.DrawEllipse(edgeHandleBrush, handlePen, handle, handleSize / 2, handleSize / 2);
        }

        // 📌 범례 표시 (이미지 위쪽에)
        var legendY = imageRect.Top - 25;
        if (legendY > 0)
        {
            // 비율 유지 범례
            var cornerLegendRect = new Rect(imageRect.Left, legendY, 8, 8);
            drawingContext.DrawRectangle(cornerHandleBrush, handlePen, cornerLegendRect);

            var cornerText = new FormattedText("비율유지",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Arial"), 10, Brushes.Blue, 96);
            drawingContext.DrawText(cornerText, new Point(imageRect.Left + 12, legendY - 2));

            // 자유 조정 범례  
            var edgeLegendCenter = new Point(imageRect.Left + 80, legendY + 4);
            drawingContext.DrawEllipse(edgeHandleBrush, handlePen, edgeLegendCenter, 4, 4);

            var edgeText = new FormattedText("자유조정",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Arial"), 10, Brushes.Orange, 96);
            drawingContext.DrawText(edgeText, new Point(imageRect.Left + 92, legendY - 2));
        }
    }

    private void UpdateImageOverlaysVisibility()
    {
        foreach (var customImage in CustomImages)
        {
            // Adorner 모드인지 확인
            var isInAdornerMode = AdornerItems.OfType<ImageAdornerWrapper>()
                .Any(a => a.CustomImage == customImage);


            // Adorner 모드가 아닌 경우에만 가시성 업데이트
            if (!isInAdornerMode)
            {
                if (Zoom < IMAGE_VISIBILITY_MIN_ZOOM)
                {
                    customImage.Visibility = false;
                }
                else
                {
                    var viewArea = ViewArea;
                    customImage.Visibility = customImage.ImageBounds.IntersectsWith(viewArea);
                }
            }
            // Adorner 모드인 경우 원본 이미지는 항상 true 유지
            else
            {
                customImage.Visibility = true;
            }
        }

        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!IsEditMode) return;

        var mousePos = e.GetPosition(this);
        var geoPos = FromLocalToLatLng((int)mousePos.X, (int)mousePos.Y);


        var selectedImage = CustomImages.FirstOrDefault(img => img.IsSelected);
        if (selectedImage != null)
        {
            _resizeHandle = GetClickedHandle(selectedImage, mousePos);

            if (_resizeHandle != ResizeHandle.None)
            {
                _draggedImage = selectedImage;
                _dragStartPoint = mousePos;
                _isDragging = true;

                // 원본 크기와 대각선 길이 저장
                var bounds = selectedImage.ImageBounds;
                var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
                var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

                _originalSize = new Size(
                    Math.Abs(bottomRight.X - topLeft.X),
                    Math.Abs(bottomRight.Y - topLeft.Y)
                );

                // 고정점과 드래그 시작점 저장
                switch (_resizeHandle)
                {
                    case ResizeHandle.TopLeft:
                        _originalFixedPoint = new Point(bottomRight.X, bottomRight.Y);
                        _originalDragPoint = new Point(topLeft.X, topLeft.Y);
                        break;
                    case ResizeHandle.TopRight:
                        _originalFixedPoint = new Point(topLeft.X, bottomRight.Y);
                        _originalDragPoint = new Point(bottomRight.X, topLeft.Y);
                        break;
                    case ResizeHandle.BottomLeft:
                        _originalFixedPoint = new Point(bottomRight.X, topLeft.Y);
                        _originalDragPoint = new Point(topLeft.X, bottomRight.Y);
                        break;
                    case ResizeHandle.BottomRight:
                        _originalFixedPoint = new Point(topLeft.X, topLeft.Y);
                        _originalDragPoint = new Point(bottomRight.X, bottomRight.Y);
                        break;
                    case ResizeHandle.Move:
                        // 이동의 경우는 기존 로직 사용
                        break;
                }

                // 원본 대각선 길이 계산
                var deltaX = _originalDragPoint.X - _originalFixedPoint.X;
                var deltaY = _originalDragPoint.Y - _originalFixedPoint.Y;
                _originalDiagonal = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                _isResizing = (_resizeHandle != ResizeHandle.Move);

                this.CaptureMouse();
                _log?.Info($"리사이즈 시작: {_resizeHandle}, 원본크기: {_originalSize.Width}x{_originalSize.Height}");
                e.Handled = true;
                return;
            }

            if (selectedImage.Contains(geoPos))
            {
                _resizeHandle = ResizeHandle.Move;
                _draggedImage = selectedImage;
                _dragStartPoint = mousePos;
                _isDragging = true;
                this.CaptureMouse();

                _log?.Info($"이미지 이동 시작: {selectedImage.Title}");
                e.Handled = true;
                return;
            }
        }
    }


    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        // 편집 모드 + 드래그 상태 + 대상 이미지가 있어야 처리
        if (!IsEditMode || !_isDragging || _draggedImage == null)
            return;

        // 현재 마우스 위치 및 증분(dX/dY) 계산
        Point currentPos = e.GetPosition(this);
        double deltaX = currentPos.X - _dragStartPoint.X;
        double deltaY = currentPos.Y - _dragStartPoint.Y;

        // ‘미세 흔들림’ 무시
        if (Math.Abs(deltaX) < 2 && Math.Abs(deltaY) < 2)
            return;

        RectLatLng curBounds = _draggedImage.ImageBounds;
        RectLatLng newBounds = curBounds;

        switch (_resizeHandle)
        {
            // ── ① 이미지 이동 ────────────────────
            case ResizeHandle.Move:
                newBounds = MoveBounds(curBounds, deltaX, deltaY);
                break;

            // ── ② 모서리(파란색) : 종횡비 고정 ───
            case ResizeHandle.TopLeft:
            case ResizeHandle.TopRight:
            case ResizeHandle.BottomLeft:
            case ResizeHandle.BottomRight:
                newBounds = ResizeBoundsWithRatio(curBounds, deltaX, deltaY, _resizeHandle);
                break;

            // ── ③ 변 중앙(주황색) : 자유 비율 ────
            case ResizeHandle.TopCenter:
                newBounds = ResizeBoundsFree(curBounds, 0, deltaY, false, true, false, false);
                break;
            case ResizeHandle.BottomCenter:
                newBounds = ResizeBoundsFree(curBounds, 0, deltaY, false, false, false, true);
                break;
            case ResizeHandle.MiddleLeft:
                newBounds = ResizeBoundsFree(curBounds, deltaX, 0, true, false, false, false);
                break;
            case ResizeHandle.MiddleRight:
                newBounds = ResizeBoundsFree(curBounds, deltaX, 0, false, false, true, false);
                break;
        }

        // 최소 크기(≈1픽셀) 보장 후 적용
        if (newBounds.WidthLng > 0.0001 &&
            newBounds.HeightLat > 0.0001)
        {
            _draggedImage.ImageBounds = newBounds;
            InvalidateVisual();
            _dragStartPoint = currentPos;   // ← 증분 기준점 갱신
        }
    }


    /// <summary>
    /// 모서리(파란 핸들) 드래그 - 종횡비 고정 리사이즈
    /// </summary>
    private RectLatLng ResizeBoundsWithRatio(
    RectLatLng bounds, double deltaX, double deltaY, ResizeHandle corner)
    {
        // ───── 1. 현재 픽셀 좌표, 종횡비 계산 ─────
        GPoint tlGP = FromLatLngToLocal(bounds.LocationTopLeft);
        GPoint brGP = FromLatLngToLocal(bounds.LocationRightBottom);

        double curW = brGP.X - tlGP.X;
        double curH = brGP.Y - tlGP.Y;
        if (curW <= 2 || curH <= 2)       // 안전장치
            return bounds;

        double aspect = curW / curH;

        // ───── 2. 드래그 방향에 따른 확대/축소 스케일 산출 ─────
        double drag = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));           // 가장 많이 움직인 축
        double diag = Math.Sqrt(curW * curW + curH * curH);                   // 대각선 길이
        if (drag < 0.1 || diag < 1.0)                                           // 미세 이동 무시
            return bounds;

        bool expand =
            (corner == ResizeHandle.TopLeft && (deltaX < 0 || deltaY < 0)) ||
            (corner == ResizeHandle.TopRight && (deltaX > 0 || deltaY < 0)) ||
            (corner == ResizeHandle.BottomLeft && (deltaX < 0 || deltaY > 0)) ||
            (corner == ResizeHandle.BottomRight && (deltaX > 0 || deltaY > 0));

        double scale = 1.0 + (expand ? drag : -drag) / diag;
        scale = Math.Max(0.05, scale);       // 최소 5 %

        double newW = curW * scale;
        double newH = newW / aspect;

        // ───── 3. 고정점 기준으로 새 픽셀 사각형 계산 ─────
        Point newTL, newBR;                  // ← System.Windows.Point 사용!

        switch (corner)
        {
            case ResizeHandle.TopLeft:
                newBR = new Point(brGP.X, brGP.Y);
                newTL = new Point(brGP.X - newW, brGP.Y - newH);
                break;

            case ResizeHandle.TopRight:
                newTL = new Point(tlGP.X, brGP.Y - newH);
                newBR = new Point(tlGP.X + newW, brGP.Y);
                break;

            case ResizeHandle.BottomLeft:
                newTL = new Point(brGP.X - newW, tlGP.Y);
                newBR = new Point(brGP.X, tlGP.Y + newH);
                break;

            default: // BottomRight
                newTL = new Point(tlGP.X, tlGP.Y);
                newBR = new Point(tlGP.X + newW, tlGP.Y + newH);
                break;
        }

        // ───── 4. 픽셀 → 지리 좌표 변환 후 RectLatLng 생성 ─────
        var geoTL = FromLocalToLatLng((int)Math.Round(newTL.X), (int)Math.Round(newTL.Y));
        var geoBR = FromLocalToLatLng((int)Math.Round(newBR.X), (int)Math.Round(newBR.Y));

        return new RectLatLng(
            geoTL.Lat,
            geoTL.Lng,
            Math.Abs(geoBR.Lng - geoTL.Lng),
            Math.Abs(geoTL.Lat - geoBR.Lat));
    }


    /// <summary>
    /// 자유 크기 조정 (변 중앙 핸들용, 비율 무시)
    /// </summary>
    private RectLatLng ResizeBoundsFree(RectLatLng bounds, double deltaX, double deltaY,
        bool adjustLeft, bool adjustTop, bool adjustRight, bool adjustBottom)
    {
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

        // 각 변 조정
        if (adjustLeft) topLeft.X += (long)deltaX;
        if (adjustTop) topLeft.Y += (long)deltaY;
        if (adjustRight) bottomRight.X += (long)deltaX;
        if (adjustBottom) bottomRight.Y += (long)deltaY;

        // 최소 크기 보장
        var minSize = 20; // 최소 20픽셀
        if (Math.Abs(bottomRight.X - topLeft.X) < minSize)
        {
            if (adjustLeft) topLeft.X = bottomRight.X - minSize;
            if (adjustRight) bottomRight.X = topLeft.X + minSize;
        }

        if (Math.Abs(bottomRight.Y - topLeft.Y) < minSize)
        {
            if (adjustTop) topLeft.Y = bottomRight.Y - minSize;
            if (adjustBottom) bottomRight.Y = topLeft.Y + minSize;
        }

        // 지리 좌표로 변환
        var newTopLeft = FromLocalToLatLng((int)topLeft.X, (int)topLeft.Y);
        var newBottomRight = FromLocalToLatLng((int)bottomRight.X, (int)bottomRight.Y);

        return new RectLatLng(
            newTopLeft.Lat,
            newTopLeft.Lng,
            Math.Abs(newBottomRight.Lng - newTopLeft.Lng),
            Math.Abs(newTopLeft.Lat - newBottomRight.Lat)
        );
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isDragging)
        {
            _isDragging = false;
            _isResizing = false;
            _draggedImage = null;
            _resizeHandle = ResizeHandle.None;
            this.ReleaseMouseCapture();

            _log?.Info("드래그 완료 - 상태 초기화");
        }
    }

    private RectLatLng MoveBounds(RectLatLng bounds, double deltaX, double deltaY)
    {
        // 화면 좌표 변화를 지리 좌표 변화로 변환
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var newTopLeft = new Point(topLeft.X + deltaX, topLeft.Y + deltaY);
        var newGeoTopLeft = FromLocalToLatLng((int)newTopLeft.X, (int)newTopLeft.Y);

        return new RectLatLng(
            newGeoTopLeft.Lat,
            newGeoTopLeft.Lng,
            bounds.WidthLng,
            bounds.HeightLat
        );
    }

    private RectLatLng ResizeBounds(RectLatLng bounds, double deltaX, double deltaY,
        bool adjustLeft, bool adjustTop, bool adjustRight, bool adjustBottom)
    {
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

        // 각 모서리 조정
        if (adjustLeft) topLeft.X += (long)deltaX;
        if (adjustTop) topLeft.Y += (long)deltaY;
        if (adjustRight) bottomRight.X += (long)deltaX;
        if (adjustBottom) bottomRight.Y += (long)deltaY;

        // 지리 좌표로 변환
        var newTopLeft = FromLocalToLatLng((int)topLeft.X, (int)topLeft.Y);
        var newBottomRight = FromLocalToLatLng((int)bottomRight.X, (int)bottomRight.Y);

        return new RectLatLng(
            newTopLeft.Lat,
            newTopLeft.Lng,
            Math.Abs(newBottomRight.Lng - newTopLeft.Lng),
            Math.Abs(newTopLeft.Lat - newBottomRight.Lat)
        );
    }

    private ResizeHandle GetClickedHandle(GMapCustomImage image, Point mousePos)
    {
        var bounds = image.ImageBounds;
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

        var imageRect = new Rect(topLeft.X, topLeft.Y,
            bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        var handleSize = 8;
        var tolerance = handleSize + 2;

        // 8개 핸들 위치
        var handles = new[]
        {
        (new Point(imageRect.Left, imageRect.Top), ResizeHandle.TopLeft),
        (new Point(imageRect.Left + imageRect.Width/2, imageRect.Top), ResizeHandle.TopCenter),
        (new Point(imageRect.Right, imageRect.Top), ResizeHandle.TopRight),
        (new Point(imageRect.Right, imageRect.Top + imageRect.Height/2), ResizeHandle.MiddleRight),
        (new Point(imageRect.Right, imageRect.Bottom), ResizeHandle.BottomRight),
        (new Point(imageRect.Left + imageRect.Width/2, imageRect.Bottom), ResizeHandle.BottomCenter),
        (new Point(imageRect.Left, imageRect.Bottom), ResizeHandle.BottomLeft),
        (new Point(imageRect.Left, imageRect.Top + imageRect.Height/2), ResizeHandle.MiddleLeft)
    };

        foreach (var (handlePos, handleType) in handles)
        {
            if (Math.Abs(mousePos.X - handlePos.X) <= tolerance &&
                Math.Abs(mousePos.Y - handlePos.Y) <= tolerance)
            {
                return handleType;
            }
        }

        return ResizeHandle.None;
    }

    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #region - Adorner Layer Management -

    /// <summary>
    /// Adorner 전용 레이어 생성
    /// </summary>
    private void CreateAdornerLayer()
    {
        
    }

    /// <summary>
    /// Adorner 컬렉션 변경 시 레이어 동기화
    /// </summary>
    private void AdornerItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var adorner in e.NewItems?.OfType<GMapAdornerWrapper>() ?? Enumerable.Empty<GMapAdornerWrapper>())
                {
                    AddAdornerToLayer(adorner);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var adorner in e.OldItems?.OfType<GMapAdornerWrapper>() ?? Enumerable.Empty<GMapAdornerWrapper>())
                {
                    RemoveAdornerFromLayer(adorner);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                foreach (var oldAdorner in e.OldItems?.OfType<GMapAdornerWrapper>() ?? Enumerable.Empty<GMapAdornerWrapper>())
                {
                    RemoveAdornerFromLayer(oldAdorner);
                }
                foreach (var newAdorner in e.NewItems?.OfType<GMapAdornerWrapper>() ?? Enumerable.Empty<GMapAdornerWrapper>())
                {
                    AddAdornerToLayer(newAdorner);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                ClearAdornerLayer();
                break;
        }
    }

    /// <summary>
    /// Adorner를 레이어에 추가
    /// </summary>
    private void AddAdornerToLayer(GMapAdornerWrapper adorner)
    {
        if (adorner == null || _adornerLayer == null)
        {
            _log?.Warning($"AddAdornerToLayer 실패: adorner={adorner != null}, _adornerLayer={_adornerLayer != null}");
            return;
        }

        try
        {
            if (!_adornerLayer.Children.Contains(adorner))
            {
                _adornerLayer.Children.Add(adorner);
                adorner.UpdatePosition();
                ApplyAdornerStyle(adorner);

                // 추가 디버깅 정보
                _log?.Info($"Adorner 레이어에 추가: {adorner}");
                _log?.Info($"  - Adorner Layer Children 수: {_adornerLayer.Children.Count}");
                _log?.Info($"  - Adorner Visibility: {adorner.Visibility}");
                _log?.Info($"  - Adorner Size: {adorner.Width}x{adorner.Height}");
                _log?.Info($"  - Adorner Position: ({Canvas.GetLeft(adorner)}, {Canvas.GetTop(adorner)})");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 레이어 추가 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Adorner를 레이어에서 제거
    /// </summary>
    private void RemoveAdornerFromLayer(GMapAdornerWrapper adorner)
    {
        if (adorner == null || _adornerLayer == null) return;

        try
        {
            if (_adornerLayer.Children.Contains(adorner))
            {
                _adornerLayer.Children.Remove(adorner);
                _log?.Info($"Adorner 레이어에서 제거: {adorner}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 레이어 제거 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Adorner 레이어 전체 초기화
    /// </summary>
    private void ClearAdornerLayer()
    {
        if (_adornerLayer == null) return;

        try
        {
            var count = _adornerLayer.Children.Count;
            _adornerLayer.Children.Clear();
            _log?.Info($"Adorner 레이어 전체 초기화: {count}개 제거");
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 레이어 초기화 실패: {ex.Message}");
        }
    }

    #endregion
    #region - Edit Mode Management -

    /// <summary>
    /// 편집 모드 활성화/비활성화
    /// </summary>
    public void SetEditMode(bool enabled)
    {
        if (IsEditMode == enabled) return;

        IsEditMode = enabled;

        if (IsEditMode)
        {
            _log?.Info("편집 모드 활성화");
        }
        else
        {
            _log?.Info("편집 모드 비활성화 - 모든 선택 해제");

            // 편집 모드 해제 시 모든 이미지 선택 해제
            foreach (var img in CustomImages)
            {
                img.IsSelected = false;
            }

            // 모든 마커 선택 해제
            foreach (var marker in CustomMarkers)
            {
                marker.IsSelected = false;
            }

            // 경계선 표시 해제
            ShowImageBounds = false;

            // 화면 갱신
            InvalidateVisual();

            ExitEditModeForAllItems();
        }
    }

    /// <summary>
    /// 선택된 마커를 Adorner 모드로 전환
    /// </summary>
    public MarkerAdornerWrapper? EnableAdornerMode(GMapCustomMarker marker)
    {
        if (!IsEditMode || marker == null) return null;

        try
        {
            // 이미 Adorner 모드인지 확인
            var existingAdorner = AdornerItems.OfType<MarkerAdornerWrapper>()
                .FirstOrDefault(a => a.CustomMarker.Id == marker.Id);

            if (existingAdorner != null)
                return existingAdorner;

            // 원본 마커 숨김
            if (marker.Shape != null)
            {
                marker.Shape.Visibility = Visibility.Hidden;
            }

            // Adorner 래퍼 생성
            var adornerWrapper = new MarkerAdornerWrapper(marker, this);
            AdornerItems.Add(adornerWrapper);

            _log?.Info($"마커 Adorner 모드 활성화: {marker.Title}");
            return adornerWrapper;
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 Adorner 모드 활성화 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 선택된 이미지 혹은 지도를 Adorner 모드로 전환
    /// </summary>
    public ImageAdornerWrapper? EnableAdornerMode(GMapCustomImage image)
    {
        if (!IsEditMode || image == null) return null;

        _log?.Info($"EnableAdornerMode 호출됨: {image.Title}");

        try
        {
            //기존 선택 모두 해제
            foreach (var img in CustomImages)
            {
                img.IsSelected = false;
            }

            // 새 이미지 선택
            image.IsSelected = true;

            // 화면 갱신으로 핸들 표시
            ShowImageBounds = true;
            InvalidateVisual();

            _log?.Info($"이미지 '{image.Title}' 선택 표시 완료 (핸들 포함)");

            return null; // 아직 실제 Adorner는 생성하지 않음
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 선택 표시 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Adorner 모드 해제하고 일반 모드로 복원
    /// </summary>
    public bool DisableAdornerMode(GMapAdornerWrapper adorner)
    {
        _log?.Info("DisableAdornerMode 호출됨 (단순 모드)");

        try
        {
            // 모든 이미지 선택 해제
            foreach (var img in CustomImages)
            {
                img.IsSelected = false;
            }

            ShowImageBounds = false; // 경계선 표시 비활성화
            InvalidateVisual(); // 화면 갱신

            _log?.Info("이미지 선택 해제 완료");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 해제 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 모든 항목의 Adorner 모드 해제
    /// </summary>
    private void ExitEditModeForAllItems()
    {
        var adorners = AdornerItems.ToList();
        foreach (var adorner in adorners)
        {
            DisableAdornerMode(adorner);
        }
    }

    /// <summary>
    /// 선택된 항목들만 Adorner 모드로 전환
    /// </summary>
    public void EnableAdornerModeForSelected()
    {
        if (!IsEditMode) return;

        try
        {
            // 선택된 마커들 처리
            var selectedMarkers = CustomMarkers.Where(m => m.IsSelected).ToList();
            foreach (var marker in selectedMarkers)
            {
                EnableAdornerMode(marker);
            }

            // 선택된 이미지들 처리 (선택 상태가 있다면)
            // 여기서는 예시로 모든 이미지를 처리하지만, 실제로는 선택 상태에 따라 결정

            _log?.Info($"선택된 항목 Adorner 모드 활성화: 마커 {selectedMarkers.Count}개");
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 항목 Adorner 모드 활성화 실패: {ex.Message}");
        }
    }


    /// <summary>
    /// 특정 위치의 객체 찾기 헬퍼 메서드
    /// </summary>
    public object GetObjectAt(PointLatLng position)
    {
        // 이미지 확인
        var images = GetImageOverlaysAt(position);
        if (images.Any())
            return images.First();

        // 마커 확인
        var markers = CustomMarkers
            .Where(m => Math.Abs(m.Position.Lat - position.Lat) < 0.0001 &&
                       Math.Abs(m.Position.Lng - position.Lng) < 0.0001)
            .ToList();

        if (markers.Any())
            return markers.First();

        return null;
    }
    #endregion
    #region - Image Management Methods (기존 기능 유지) -
    public void AddImageOverlay(GMapCustomImage customImage)
    {
        if (customImage == null) return;

        try
        {
            CustomImages.Add(customImage);
            InvalidateVisual();
            _log?.Info($"이미지 오버레이 추가: {customImage.Title}");
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 추가 실패: {ex.Message}");
        }
    }

    public void RemoveImageOverlay(GMapCustomImage customImage)
    {
        if (customImage == null) return;

        try
        {
            if (CustomImages.Remove(customImage))
            {
                // Adorner 모드인 경우 해당 Adorner도 제거
                var adorner = AdornerItems.OfType<ImageAdornerWrapper>()
                    .FirstOrDefault(a => a.CustomImage == customImage);
                if (adorner != null)
                {
                    AdornerItems.Remove(adorner);
                }

                customImage.Dispose();
                InvalidateVisual();
                _log?.Info($"이미지 오버레이 제거: {customImage.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 제거 실패: {ex.Message}");
        }
    }

    public void ClearImageOverlays()
    {
        try
        {
            // Adorner 먼저 정리
            var imageAdorners = AdornerItems.OfType<ImageAdornerWrapper>().ToList();
            foreach (var adorner in imageAdorners)
            {
                AdornerItems.Remove(adorner);
            }

            foreach (var customImage in CustomImages.ToList())
            {
                customImage.Dispose();
            }

            CustomImages.Clear();
            InvalidateVisual();
            _log?.Info("모든 이미지 오버레이 제거 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 전체 제거 실패: {ex.Message}");
        }
    }

    // 기타 이미지 관리 메서드들 (기존과 동일)
    public List<GMapCustomImage> GetImageOverlaysAt(PointLatLng position)
    {
        return CustomImages.Where(img => img.Visibility && img.Contains(position)).ToList();
    }

    public List<GMapCustomImage> GetImageOverlaysIntersecting(RectLatLng bounds)
    {
        return CustomImages.Where(img => img.Visibility && img.IntersectsWith(bounds)).ToList();
    }

    public void SetAllImageOverlaysOpacity(double opacity)
    {
        foreach (var customImage in CustomImages)
        {
            customImage.Opacity = opacity;
        }
        InvalidateVisual();
    }

    public void SetAllImageOverlaysVisibility(bool isVisible)
    {
        foreach (var customImage in CustomImages)
        {
            customImage.Visibility = isVisible;
        }
        InvalidateVisual();
    }

    #endregion
    #region - Helper Methods -
    /// <summary>
    /// Adorner 스타일 적용
    /// </summary>
    private void ApplyAdornerStyle(GMapAdornerWrapper adorner)
    {
        try
        {
            _log?.Info($"=== ApplyAdornerStyle 시작 ===");
            _log?.Info($"Application.Current: {Application.Current != null}");

            if (Application.Current?.Resources != null)
            {
                _log?.Info($"Resources 개수: {Application.Current.Resources.Count}");

                bool hasStyle = Application.Current.Resources.Contains("GMapDesignerItemStyle");
                _log?.Info($"GMapDesignerItemStyle 존재: {hasStyle}");

                if (hasStyle)
                {
                    var style = Application.Current.Resources["GMapDesignerItemStyle"] as Style;
                    _log?.Info($"Style 타입: {style?.GetType().Name}");
                    adorner.Style = style;
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"ApplyAdornerStyle 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 통계 정보
    /// </summary>
    public (int TotalMarkers, int TotalImages, int AdornerCount, bool IsEditMode) GetStatistics()
    {
        return (
            TotalMarkers: CustomMarkers.Count,
            TotalImages: CustomImages.Count,
            AdornerCount: AdornerItems.Count,
            IsEditMode: IsEditMode
        );
    }

    #endregion
    #endregion
    #region - IHanldes -
    #endregion
    #region - IsEditMode DependencyProperty -

    /// <summary>
    /// IsEditMode DependencyProperty
    /// </summary>
    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(
            nameof(IsEditMode),
            typeof(bool),
            typeof(GMapCustomControl),
            new PropertyMetadata(false, OnIsEditModeChanged));

    /// <summary>
    /// 편집 모드 여부
    /// </summary>
    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        private set => SetValue(IsEditModeProperty, value);
    }

    /// <summary>
    /// IsEditMode 속성 변경 시 호출
    /// </summary>
    private static void OnIsEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            bool newValue = (bool)e.NewValue;
            control._log?.Info($"편집 모드 변경: {e.OldValue} -> {newValue}");
        }
    }

    /// <summary>
    /// ShowMGRSGrid DependencyProperty
    /// </summary>
    public static readonly DependencyProperty ShowMGRSGridProperty =
        DependencyProperty.Register(
            nameof(ShowMGRSGrid),
            typeof(bool),
            typeof(GMapCustomControl),
            new PropertyMetadata(false, OnShowMGRSGridChanged));

    /// <summary>
    /// MGRS 그리드 표시 여부
    /// </summary>
    public bool ShowMGRSGrid
    {
        get => (bool)GetValue(ShowMGRSGridProperty);
        set => SetValue(ShowMGRSGridProperty, value);
    }

    /// <summary>
    /// ShowMGRSGrid 속성 변경 시 호출
    /// </summary>
    private static void OnShowMGRSGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            bool newValue = (bool)e.NewValue;
            control._log?.Info($"MGRS 그리드 표시 변경: {e.OldValue} -> {newValue}");

            // 화면 갱신
            control.InvalidateVisual();
        }
    }
    #endregion
    #region - Map Rotation Features -
    /// <summary>
    /// 지도 회전 각도 (도 단위)
    /// </summary>
    public static readonly DependencyProperty MapRotationProperty =
        DependencyProperty.Register(
            nameof(MapRotation),
            typeof(double),
            typeof(GMapCustomControl),
            new PropertyMetadata(0.0, OnMapRotationChanged));

    public double MapRotation
    {
        get => (double)GetValue(MapRotationProperty);
        set => SetValue(MapRotationProperty, value);
    }

    /// <summary>
    /// 회전 중심점 (기본값: 지도 중심)
    /// </summary>
    public static readonly DependencyProperty RotationCenterProperty =
        DependencyProperty.Register(
            nameof(RotationCenter),
            typeof(PointLatLng?),
            typeof(GMapCustomControl),
            new PropertyMetadata(null));

    public PointLatLng? RotationCenter
    {
        get => (PointLatLng?)GetValue(RotationCenterProperty);
        set => SetValue(RotationCenterProperty, value);
    }

    /// <summary>
    /// 회전 스냅 각도 (예: 15도 단위로 스냅)
    /// </summary>
    public static readonly DependencyProperty RotationSnapAngleProperty =
        DependencyProperty.Register(
            nameof(RotationSnapAngle),
            typeof(double),
            typeof(GMapCustomControl),
            new PropertyMetadata(0.0)); // 0이면 스냅 비활성화

    public double RotationSnapAngle
    {
        get => (double)GetValue(RotationSnapAngleProperty);
        set => SetValue(RotationSnapAngleProperty, value);
    }

    /// <summary>
    /// 지도 회전 각도 변경 시 호출
    /// </summary>
    private static void OnMapRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            double newRotation = (double)e.NewValue;
            control.ApplyMapRotation(newRotation);
        }
    }

    /// <summary>
    /// 지도 회전 적용
    /// </summary>
    private void ApplyMapRotation(double rotation)
    {
        try
        {
            // 각도 정규화 (-180 ~ 180)
            rotation = NormalizeAngle(rotation);

            // 즉시 회전 적용
            Bearing = (float)rotation;

            // 회전 후 오버레이 업데이트
            UpdateOverlaysAfterRotation();

            _log?.Info($"지도 회전 적용: {rotation:F1}도");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 적용 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 각도 정규화 (-180 ~ 180)
    /// </summary>
    private double NormalizeAngle(double angle)
    {
        angle = angle % 360;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
        return angle;
    }

    /// <summary>
    /// 스냅 각도 적용
    /// </summary>
    private double ApplySnapAngle(double angle)
    {
        if (RotationSnapAngle <= 0) return angle;

        return Math.Round(angle / RotationSnapAngle) * RotationSnapAngle;
    }

    /// <summary>
    /// 회전 후 오버레이 업데이트
    /// </summary>
    private void UpdateOverlaysAfterRotation()
    {
        try
        {
            // 마커 위치 업데이트
            foreach (var marker in CustomMarkers)
            {
                marker.ForceUpdateLocalPosition(this);
            }

            // 이미지 오버레이 회전 보정
            UpdateImageOverlaysRotation();

            // Adorner 위치 업데이트
            if (IsEditMode)
            {
                UpdateAdornerPositions();
            }

            // MGRS 그리드 업데이트
            InvalidateVisual();

            _log?.Info("회전 후 오버레이 업데이트 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"회전 후 오버레이 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 이미지 오버레이 회전 보정
    /// </summary>
    private void UpdateImageOverlaysRotation()
    {
        foreach (var customImage in CustomImages)
        {
            // 이미지의 상대적 회전각 계산
            // (지도 회전과 반대 방향으로 회전하여 수평 유지)
            customImage.Rotation = -MapRotation;
        }
    }

    #endregion
    #region - Rotation Input Handling -

    /// <summary>
    /// 키보드 회전 처리
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Left:
                    RotateMap(-5); // 5도씩 반시계방향
                    e.Handled = true;
                    break;
                case Key.Right:
                    RotateMap(5); // 5도씩 시계방향
                    e.Handled = true;
                    break;
                case Key.R:
                    ResetRotation(); // 회전 초기화
                    e.Handled = true;
                    break;
            }
        }
    }

    /// <summary>
    /// 마우스 휠 + Shift로 회전
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            // Shift + 휠로 회전
            double rotationDelta = e.Delta > 0 ? 5 : -5;
            RotateMap(rotationDelta);
            e.Handled = true;
            return;
        }

        base.OnMouseWheel(e);
    }

    /// <summary>
    /// 지도 회전 (상대적)
    /// </summary>
    public void RotateMap(double deltaAngle)
    {
        double newRotation = MapRotation + deltaAngle;

        // 스냅 적용
        newRotation = ApplySnapAngle(newRotation);

        MapRotation = newRotation;
    }

    /// <summary>
    /// 지도 회전 (절대적)
    /// </summary>
    public void SetMapRotation(double angle)
    {
        MapRotation = ApplySnapAngle(angle);
    }

    /// <summary>
    /// 회전 초기화
    /// </summary>
    public void ResetRotation()
    {
        SetMapRotation(0);
    }

    #endregion
    #region - Rotation UI Components -

    /// <summary>
    /// 회전 컨트롤 표시 여부
    /// </summary>
    public static readonly DependencyProperty ShowRotationControlProperty =
        DependencyProperty.Register(
            nameof(ShowRotationControl),
            typeof(bool),
            typeof(GMapCustomControl),
            new PropertyMetadata(false));

    public bool ShowRotationControl
    {
        get => (bool)GetValue(ShowRotationControlProperty);
        set => SetValue(ShowRotationControlProperty, value);
    }

   

    /// <summary>
    /// 회전 정보 렌더링
    /// </summary>
    private void RenderRotationInfo(DrawingContext drawingContext)
    {
        try
        {
            // 회전 각도 텍스트
            var rotationText = new FormattedText(
                $"회전: {MapRotation:F1}°",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Arial"),
                14,
                Brushes.Black,
                96);

            // 배경 사각형
            var textRect = new Rect(10, 10, rotationText.Width + 10, rotationText.Height + 6);
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                new Pen(Brushes.Gray, 1),
                textRect);

            // 텍스트 그리기
            drawingContext.DrawText(rotationText, new Point(15, 13));

            // 나침반 표시
            DrawCompass(drawingContext, new Point(ActualWidth - 80, 80));
        }
        catch (Exception ex)
        {
            _log?.Error($"회전 정보 렌더링 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 나침반 그리기
    /// </summary>
    private void DrawCompass(DrawingContext drawingContext, Point center)
    {
        try
        {
            double radius = 30;

            // 배경 원
            drawingContext.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                new Pen(Brushes.Black, 2),
                center, radius, radius);

            // 북쪽 화살표 (회전 적용)
            var northAngle = -MapRotation * Math.PI / 180; // 지도 회전의 반대
            var northTip = new Point(
                center.X + Math.Sin(northAngle) * (radius - 5),
                center.Y - Math.Cos(northAngle) * (radius - 5));

            var arrowGeometry = new StreamGeometry();
            using (var ctx = arrowGeometry.Open())
            {
                ctx.BeginFigure(northTip, true, true);

                var leftWing = new Point(
                    center.X + Math.Sin(northAngle - 0.3) * (radius - 15),
                    center.Y - Math.Cos(northAngle - 0.3) * (radius - 15));
                var rightWing = new Point(
                    center.X + Math.Sin(northAngle + 0.3) * (radius - 15),
                    center.Y - Math.Cos(northAngle + 0.3) * (radius - 15));

                ctx.LineTo(leftWing, true, false);
                ctx.LineTo(center, true, false);
                ctx.LineTo(rightWing, true, false);
                ctx.LineTo(northTip, true, false);
            }

            drawingContext.DrawGeometry(Brushes.Red, new Pen(Brushes.DarkRed, 1), arrowGeometry);

            // N 표시
            var nText = new FormattedText("N", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Arial"), 12, Brushes.Black, 96);

            var nPos = new Point(
                center.X + Math.Sin(northAngle) * (radius + 15) - nText.Width / 2,
                center.Y - Math.Cos(northAngle) * (radius + 15) - nText.Height / 2);

            drawingContext.DrawText(nText, nPos);
        }
        catch (Exception ex)
        {
            _log?.Error($"나침반 그리기 실패: {ex.Message}");
        }
    }

    #endregion

    #region - Public Rotation Methods -

    /// <summary>
    /// 두 점을 연결하는 선에 맞춰 회전
    /// </summary>
    public void AlignToLine(PointLatLng point1, PointLatLng point2)
    {
        var screenPoint1 = FromLatLngToLocal(point1);
        var screenPoint2 = FromLatLngToLocal(point2);

        double deltaX = screenPoint2.X - screenPoint1.X;
        double deltaY = screenPoint2.Y - screenPoint1.Y;

        double angle = Math.Atan2(deltaY, deltaX) * 180 / Math.PI;
        angle = angle - 90; // 북쪽 기준으로 보정

        SetMapRotation(angle);
        _log?.Info($"선분 정렬 회전: {angle:F1}도");
    }

    /// <summary>
    /// 현재 회전 상태 정보
    /// </summary>
    public RotationInfo GetRotationInfo()
    {
        return new RotationInfo
        {
            CurrentRotation = MapRotation,
            IsRotated = Math.Abs(MapRotation) > 0.1,
            RotationCenter = RotationCenter ?? Position,
            SnapAngle = RotationSnapAngle
        };
    }

    #endregion
    #region - Properties -

    /// <summary>
    /// 기존 마커 컬렉션 (GMap.NET 기본 기능 유지)
    /// </summary>
    public ObservableCollection<GMapCustomMarker> CustomMarkers { get; private set; }

    /// <summary>
    /// 기존 이미지 컬렉션 (GMap.NET 기본 기능 유지)
    /// </summary>
    public ObservableCollection<GMapCustomImage> CustomImages { get; private set; }

    /// <summary>
    /// Adorner 항목 컬렉션 (편집 모드에서만 사용)
    /// </summary>
    public ObservableCollection<GMapAdornerWrapper> AdornerItems { get; private set; }

    /// <summary>
    /// 이미지 경계선 표시 여부 (디버그용)
    /// </summary>
    public bool ShowImageBounds { get; set; } = false;

    /// <summary>
    /// 이미지 오버레이가 표시되는 최소 줌 레벨
    /// </summary>
    public int IMAGE_VISIBILITY_MIN_ZOOM { get; set; } = 8;

    /// <summary>
    /// 현재 활성화된 이미지 오버레이 개수
    /// </summary>
    public int ActiveImageOverlayCount => CustomImages?.Count(img => img.Visibility) ?? 0;

    /// <summary>
    /// 전체 이미지 오버레이 개수
    /// </summary>
    public int TotalImageOverlayCount => CustomImages?.Count ?? 0;
    #endregion
    #region - Attributes -
    private IEventAggregator? _eventAggregator;
    private ILogService? _log;

    private MGRSGridOverlayService _mgrsOverlay;

    private Canvas _adornerLayer;
    public int VISIBILITY_ZOOM = 14;


    private GMapCustomImage _draggedImage = null;
    private Point _dragStartPoint;
    private bool _isDragging = false;
    private ResizeHandle _resizeHandle = ResizeHandle.None;

    private Size _originalSize;
    private Point _originalFixedPoint;
    private Point _originalDragPoint;
    private double _originalDiagonal;
    private bool _isResizing = false;
    #endregion

    public enum ResizeHandle
    {
        None,
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
        Move // 전체 이미지 이동
    }
}
