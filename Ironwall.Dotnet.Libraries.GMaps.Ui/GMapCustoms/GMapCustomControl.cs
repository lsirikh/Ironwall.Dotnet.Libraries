using Caliburn.Micro;
using GMap.NET.WindowsPresentation;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
/****************************************************************************
   Purpose      : 이미지와 마커 편집 기능을 제공하는 GMapCustomControl                                                        
   Created By   : GHLee                                                
   Created On   : 8/12/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 이미지와 마커 편집 기능을 제공하는 GMapCustomControl
/// - 기본 모드: 기존 GMap.NET 기능 100% 활용
/// - 편집 모드: 선택된 객체를 직접 렌더링으로 편집 기능 제공
/// </summary>
public class GMapCustomControl : GMapControl
{
    #region Constructor

    public GMapCustomControl()
    {
        _eventAggregator = IoC.Get<IEventAggregator>();
        _log = IoC.Get<ILogService>();

        InitializeCollections();
        InitializeEvents();
        InitializeAdornerManager();


        _mgrsOverlay = new MGRSGridOverlayService(_log);
        _log?.Info("GMapCustomControl 초기화 완료");
    }

    public GMapCustomControl(IEventAggregator eventAggregator, ILogService log) : this()
    {
        _eventAggregator = eventAggregator;
        _log = log;
    }

    #endregion
    #region Initialization

    /// <summary>
    /// 컬렉션 초기화
    /// </summary>
    private void InitializeCollections()
    {
        CustomMarkers = new ObservableCollection<GMapCustomMarker>();
        CustomImages = new ObservableCollection<GMapCustomImage>();
    }

    /// <summary>
    /// 이벤트 핸들러 등록
    /// </summary>
    private void InitializeEvents()
    {
        Markers.CollectionChanged += Markers_CollectionChanged;
        OnAreaChange += GMapCustomControl_OnAreaChange;
    }

    /// <summary>
    /// AdornerManager 초기화
    /// </summary>
    private void InitializeAdornerManager()
    {
        AdornerManager = new AdornerManagerService(this, _log);

        // AdornerManager 이벤트 구독
        SubscribeAdornerManagerEvents();

        _log?.Info("AdornerManager 초기화 및 이벤트 구독 완료");
    }
    #endregion
    #region Integration Events
    /// <summary>
    /// 지도 클릭 이벤트 - ViewModel에 클릭 위치 전달
    /// </summary>
    public event Action<PointLatLng, Point> OnMapClicked;

    /// <summary>
    /// 마커 클릭 이벤트 - ViewModel에 클릭된 마커 전달  
    /// </summary>
    public event Action<GMapCustomMarker> OnMarkerClicked;

    /// <summary>
    /// 이미지 클릭 이벤트 - ViewModel에 클릭된 이미지 전달
    /// </summary>
    public event Action<GMapCustomImage> OnImageClicked;

    /// <summary>
    /// 마커 편집 관련 이벤트들 (외부로 전파)
    /// </summary>
    public event EventHandler<MarkerEditStartedEventArgs> MarkerEditStarted;
    public event EventHandler<MarkerEditCompletedEventArgs> MarkerEditCompleted;
    public event EventHandler<MarkerEditCancelledEventArgs> MarkerEditCancelled;
    public event EventHandler<AdornerLifecycleEventArgs> AdornerCreated;
    public event EventHandler<AdornerLifecycleEventArgs> AdornerRemoved;
    #endregion
    #region AdornerManager Integration
    /// <summary>
    /// Adorner 관리 서비스 (ViewModel에서 주입)
    /// </summary>
    public AdornerManagerService AdornerManager { get; private set; }

    /// <summary>
    /// 외부에서 AdornerManager를 설정하는 메서드 (기존 호환성 유지)
    /// </summary>
    /// <param name="adornerManager">외부 AdornerManager (null이면 기본 사용)</param>
    public void SetAdornerManager(AdornerManagerService adornerManager)
    {
        if (adornerManager != null)
        {
            // 기존 AdornerManager 정리
            if (AdornerManager != null)
            {
                UnsubscribeAdornerManagerEvents();
                AdornerManager.Dispose();
            }

            AdornerManager = adornerManager;
            SubscribeAdornerManagerEvents();
        }

        _log?.Info("외부 AdornerManager 설정 완료");
    }

    /// <summary>
    /// AdornerManager 이벤트 구독
    /// </summary>
    private void SubscribeAdornerManagerEvents()
    {
        AdornerManager.MarkerEditStarted += OnMarkerEditStarted;
        AdornerManager.MarkerEditCompleted += OnMarkerEditCompleted;
        AdornerManager.MarkerEditCancelled += OnMarkerEditCancelled;
        AdornerManager.AdornerCreated += OnAdornerCreated;
        AdornerManager.AdornerRemoved += OnAdornerRemoved;
    }

    /// <summary>
    /// AdornerManager 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeAdornerManagerEvents()
    {
        AdornerManager.MarkerEditStarted -= OnMarkerEditStarted;
        AdornerManager.MarkerEditCompleted -= OnMarkerEditCompleted;
        AdornerManager.MarkerEditCancelled -= OnMarkerEditCancelled;
        AdornerManager.AdornerCreated -= OnAdornerCreated;
        AdornerManager.AdornerRemoved -= OnAdornerRemoved;
    }
    #endregion
    #region Override Methods

    protected override void OnInitialized(EventArgs e)
    {
        _eventAggregator?.SubscribeOnUIThread(this);
        base.OnInitialized(e);
    }

    /// <summary>
    /// 메인 렌더링 메서드
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        RenderImageOverlays(drawingContext);

        if (ShowMGRSGrid)
        {
            _mgrsOverlay.DrawMGRSGrid(drawingContext, ViewArea, (int)Zoom, this);
        }

        if (ShowRotationControl)
        {
            RenderRotationInfo(drawingContext);
        }
    }

    #endregion
    #region Event Handlers

    /// <summary>
    /// 지도 영역 변경 이벤트
    /// </summary>
    private void GMapCustomControl_OnAreaChange(RectLatLng selection, double zoom, bool zoomToFit)
    {
        _log?.Info($"지도 영역 변경: Zoom={zoom}");

        // 줌 레벨에 따른 마커 가시성 처리
        Markers.OfType<GMapCustomMarker>().ToList().ForEach(marker =>
        {
            marker.Visibility = Zoom >= VISIBILITY_ZOOM;
        });
    }

    /// <summary>
    /// 마커 컬렉션 변경 이벤트
    /// </summary>
    private void Markers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var newItem in e.NewItems?.OfType<GMapCustomMarker>() ?? Enumerable.Empty<GMapCustomMarker>())
                {
                    _log?.Info($"CustomMarkers에 추가 중: {newItem.Title}");
                    CustomMarkers.Add(newItem);
                    RegisterMarkerForAdorner(newItem);
                }
                _log?.Info($"CustomMarkers 최종 개수: {CustomMarkers.Count}");
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var oldItem in e.OldItems?.OfType<GMapCustomMarker>() ?? Enumerable.Empty<GMapCustomMarker>())
                {
                    var entity = CustomMarkers.FirstOrDefault(m => m.Id == oldItem.Id);
                    if (entity != null)
                    {
                        CustomMarkers.Remove(entity);
                        UnregisterMarkerFromAdorner(entity);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // 기존 마커 제거 후 새 마커 추가
                var oldMarkers = e.OldItems?.OfType<GMapCustomMarker>() ?? Enumerable.Empty<GMapCustomMarker>();
                var newMarkers = e.NewItems?.OfType<GMapCustomMarker>() ?? Enumerable.Empty<GMapCustomMarker>();

                foreach (var oldMarker in oldMarkers)
                {
                    var entity = CustomMarkers.FirstOrDefault(m => m.Id == oldMarker.Id);
                    if (entity != null)
                    {
                        var index = CustomMarkers.IndexOf(entity);
                        CustomMarkers.Remove(entity);
                        UnregisterMarkerFromAdorner(entity);

                        foreach (var newMarker in newMarkers)
                        {
                            CustomMarkers.Insert(index, newMarker);
                            RegisterMarkerForAdorner(newMarker);
                        }
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // 기존 마커들 Adorner 정리
                foreach (var marker in CustomMarkers)
                {
                    UnregisterMarkerFromAdorner(marker);
                }

                CustomMarkers.Clear();
                foreach (var marker in Markers.OfType<GMapCustomMarker>())
                {
                    CustomMarkers.Add(marker);
                    RegisterMarkerForAdorner(marker);
                }
                break;
        }
    }

    public void TriggerMarkerClicked(GMapCustomMarker marker)
    {
        try
        {
            _log?.Info($"TriggerMarkerClicked 호출: {marker?.Title}");
            OnMarkerClicked?.Invoke(marker);
        }
        catch (Exception ex)
        {
            _log?.Error($"TriggerMarkerClicked 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// AdornerManager 이벤트 핸들러들
    /// </summary>
    private void OnMarkerEditStarted(object sender, MarkerEditStartedEventArgs e)
    {
        _log?.Info($"마커 편집 시작: {e.Marker.Title}");
        MarkerEditStarted?.Invoke(this, e);
    }

    private void OnMarkerEditCompleted(object sender, MarkerEditCompletedEventArgs e)
    {
        _log?.Info($"마커 편집 완료: {e.Marker.Title}, 변경: {e.GetChangesSummary()}");
        MarkerEditCompleted?.Invoke(this, e);
    }

    private void OnMarkerEditCancelled(object sender, MarkerEditCancelledEventArgs e)
    {
        _log?.Info($"마커 편집 취소: {e.Marker.Title}");
        MarkerEditCancelled?.Invoke(this, e);
    }

    private void OnAdornerCreated(object sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 생성: {e.Marker.Title}");
        AdornerCreated?.Invoke(this, e);
    }

    private void OnAdornerRemoved(object sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 제거: {e.Marker.Title}");
        AdornerRemoved?.Invoke(this, e);
    }

    #endregion
    #region Mouse Input Handling

    /// <summary>
    /// 마우스 왼쪽 버튼 클릭
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _log?.Info("=== GMapCustomControl.OnMouseLeftButtonDown 시작 ===");
        _log?.Info($"편집 모드: {IsEditMode}");

        base.OnMouseLeftButtonDown(e);

        var mousePos = e.GetPosition(this);
        var geoPos = FromLocalToLatLng((int)mousePos.X, (int)mousePos.Y);

        _log?.Info($"마우스 위치: 화면({mousePos.X:F2}, {mousePos.Y:F2}) -> 지리({geoPos.Lat:F6}, {geoPos.Lng:F6})");

        // 편집 모드일 때만 편집 처리
        if (IsEditMode)
        {
            _log?.Info("편집 모드에서 처리 시작");

            // 이미지 편집 처리 (기존 방식 유지)
            if (HandleImageEdit(mousePos, geoPos, e))
            {
                _log?.Info("이미지 편집 처리 완료");
                return;
            }
            _log?.Info("이미지 편집 해당 없음");

            // 마커 편집은 이벤트를 통해 ViewModel에 위임
            // Adorner는 자동으로 처리됨
        }

        // 클릭된 객체 검색
        _log?.Info("클릭된 객체 검색 시작");
        var clickedImage = GetImageAt(geoPos);
        //var clickedMarker = GetMarkerAt(geoPos);
        var clickedMarker = GetMarkerAtScreen(mousePos);

        _log?.Info($"검색 결과 - 이미지: {clickedImage?.Title ?? "없음"}, 마커: {clickedMarker?.Title ?? "없음"}");

        // 이벤트 발생으로 ViewModel에 위임
        if (clickedMarker != null)
        {
            _log?.Info($"마커 클릭 이벤트 발생: {clickedMarker.Title}");
            OnMarkerClicked?.Invoke(clickedMarker);
        }
        else if (clickedImage != null)
        {
            _log?.Info($"이미지 클릭 이벤트 발생: {clickedImage.Title}");
            OnImageClicked?.Invoke(clickedImage);
        }
        else
        {
            _log?.Info("빈 공간 클릭 이벤트 발생");
            OnMapClicked?.Invoke(geoPos, mousePos);
        }

        _log?.Info("=== GMapCustomControl.OnMouseLeftButtonDown 완료 ===");
    }

    /// <summary>
    /// 마우스 이동
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!IsEditMode || !_isDragging) return;

        Point currentPos = e.GetPosition(this);
        double deltaX = currentPos.X - _dragStartPoint.X;
        double deltaY = currentPos.Y - _dragStartPoint.Y;

        if (Math.Abs(deltaX) < 2 && Math.Abs(deltaY) < 2) return;

        // 이미지 드래그 처리 (기존 방식 유지)
        if (_isImageDrag && _draggedImage != null)
        {
            ProcessImageDrag(currentPos, deltaX, deltaY);
        }

        // 마커 드래그는 Adorner에서 자동 처리됨

    }

    /// <summary>
    /// 마우스 버튼 해제
    /// </summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isDragging)
        {
            ResetDragState();
            _log?.Info("드래그 완료");
        }
    }

    #endregion
    #region Object Detection Methods
    /// <summary>
    /// 특정 위치의 마커 찾기
    /// </summary>
    private GMapCustomMarker? GetMarkerAt(PointLatLng position)
    {
        _log?.Info($"GetMarkerAt 호출: 위치({position.Lat:F6}, {position.Lng:F6})");
        _log?.Info($"총 커스텀 마커 수: {CustomMarkers?.Count ?? 0}");

        if (CustomMarkers == null || !CustomMarkers.Any())
        {
            _log?.Info("커스텀 마커가 없음");
            return null;
        }

        foreach (var marker in CustomMarkers)
        {
            var distance = CalculateDistance(marker.Position, position);
            _log?.Info($"마커 '{marker.Title}': 위치({marker.Position.Lat:F6}, {marker.Position.Lng:F6}), 거리: {distance:F8}");

            // 허용 범위를 0.001 -> 0.005로 늘림 (더 관대하게)
            if (distance < 0.005)
            {
                _log?.Info($"마커 '{marker.Title}' 선택됨 (거리: {distance:F8})");
                return marker;
            }
        }

        _log?.Info("클릭 위치에서 마커를 찾을 수 없음");
        return null;
    }

    /// <summary>
    /// 두 지점 간의 거리 계산 (간단한 유클리드 거리)
    /// </summary>
    private double CalculateDistance(PointLatLng pos1, PointLatLng pos2)
    {
        var latDiff = Math.Abs(pos1.Lat - pos2.Lat);
        var lngDiff = Math.Abs(pos1.Lng - pos2.Lng);
        return Math.Sqrt(latDiff * latDiff + lngDiff * lngDiff);
    }

    // GMapCustomControl.cs - GetMarkerAt 메서드를 화면 좌표 기반으로 수정
    private GMapCustomMarker GetMarkerAtScreen(Point screenPosition)
    {
        _log?.Info($"GetMarkerAtScreen 호출: 화면위치({screenPosition.X:F2}, {screenPosition.Y:F2})");
        _log?.Info($"총 커스텀 마커 수: {CustomMarkers?.Count ?? 0}");

        if (CustomMarkers == null || !CustomMarkers.Any())
        {
            _log?.Info("커스텀 마커가 없음");
            return null;
        }

        foreach (var marker in CustomMarkers)
        {
            try
            {
                // 마커의 화면 좌표 계산
                var markerScreenPos = FromLatLngToLocal(marker.Position);
                var markerScreenPoint = new Point(markerScreenPos.X, markerScreenPos.Y);

                // 화면상에서의 거리 계산
                var screenDistance = CalculateScreenDistance(screenPosition, markerScreenPoint);

                // 마커 크기 고려한 클릭 반경 (마커 크기의 절반 + 여유분)
                var markerRadius = Math.Max(marker.Width, marker.Height) / 2.0 + 10; // 10px 여유분

                _log?.Info($"마커 '{marker.Title}': 화면위치({markerScreenPoint.X:F2}, {markerScreenPoint.Y:F2}), " +
                          $"화면거리: {screenDistance:F2}px, 클릭반경: {markerRadius:F2}px");

                if (screenDistance <= markerRadius)
                {
                    _log?.Info($"마커 '{marker.Title}' 선택됨 (화면거리: {screenDistance:F2}px <= 반경: {markerRadius:F2}px)");
                    return marker;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"마커 '{marker.Title}' 화면 좌표 계산 실패: {ex.Message}");
            }
        }

        _log?.Info("클릭 위치에서 마커를 찾을 수 없음");
        return null;
    }

    /// <summary>
    /// 화면상 두 점 간의 거리 계산
    /// </summary>
    private double CalculateScreenDistance(Point p1, Point p2)
    {
        var deltaX = p1.X - p2.X;
        var deltaY = p1.Y - p2.Y;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>
    /// 특정 위치의 이미지 찾기  
    /// </summary>
    private GMapCustomImage GetImageAt(PointLatLng position)
    {
        return CustomImages.FirstOrDefault(img =>
            img.Visibility && img.Contains(position));
    }
    #endregion
    #region Marker Adorner Management

    /// <summary>
    /// 마커를 Adorner 시스템에 등록
    /// </summary>
    private void RegisterMarkerForAdorner(GMapCustomMarker marker)
    {
        try
        {
            // 마커의 UI 컨트롤을 찾아서 등록
            // 실제 구현에서는 마커와 연결된 UI 컨트롤을 찾아야 함
            var markerControl = FindMarkerControl(marker);
            if (markerControl != null && AdornerManager != null)
            {
                // Adorner는 선택 시에만 생성되므로 여기서는 등록만
                _log?.Info($"마커 Adorner 등록: {marker.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 Adorner 등록 실패: {marker?.Title}, {ex.Message}");
        }
    }

    /// <summary>
    /// 마커를 Adorner 시스템에서 해제
    /// </summary>
    private void UnregisterMarkerFromAdorner(GMapCustomMarker marker)
    {
        try
        {
            if (AdornerManager != null)
            {
                // 선택 해제하여 Adorner 제거
                AdornerManager.DeselectMarker(marker, this);
                _log?.Info($"마커 Adorner 해제: {marker.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 Adorner 해제 실패: {marker?.Title}, {ex.Message}");
        }
    }

    /// <summary>
    /// 마커의 UI 컨트롤 찾기
    /// </summary>
    private GMapMarkerBasicCustomControl? FindMarkerControl(GMapCustomMarker marker)
    {
        try
        {
            _log?.Info($"마커 컨트롤 검색 중: {marker.Title}");

            // 방법 1: marker.Shape 확인
            if (marker.Shape is GMapMarkerBasicCustomControl markerControl)
            {
                _log?.Info($"marker.Shape에서 컨트롤 찾음: {markerControl.GetType().Name}");
                return markerControl;
            }

            _log?.Warning($"마커 '{marker.Title}'의 Shape가 GMapMarkerBasicCustomControl이 아님: {marker.Shape?.GetType().Name ?? "null"}");
            return null;
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 컨트롤 찾기 실패: {marker?.Title}, {ex.Message}");
            return null;
        }
    }

    #endregion
    #region Public Methods - Marker Selection

    /// <summary>
    /// 마커 선택 (Adorner 자동 생성)
    /// </summary>
    /// <param name="marker">선택할 마커</param>
    /// <returns>성공 여부</returns>
    public bool SelectMarker(GMapCustomMarker marker)
    {
        if (marker == null || AdornerManager == null)
        {
            _log?.Warning("마커 또는 AdornerManager가 null입니다.");
            return false;
        }

        try
        {
            _log?.Info($"마커 선택 시도: {marker.Title}");

            var markerControl = FindMarkerControl(marker);
            if (markerControl != null)
            {
                _log?.Info($"마커 컨트롤 찾음: {markerControl.GetType().Name}");

                // 마커를 선택 상태로 설정
                marker.IsSelected = true;

                // AdornerManager를 통한 선택
                bool result = AdornerManager.SelectMarker(marker, markerControl, this);
                _log?.Info($"AdornerManager.SelectMarker 결과: {result}");

                return result;
            }
            else
            {
                _log?.Warning($"마커 '{marker.Title}'의 컨트롤을 찾을 수 없습니다.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 선택 실패: {marker.Title}, {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 마커 선택 해제 (Adorner 자동 제거)
    /// </summary>
    /// <param name="marker">선택 해제할 마커</param>
    /// <returns>성공 여부</returns>
    public bool DeselectMarker(GMapCustomMarker marker)
    {
        if (marker == null || AdornerManager == null) return false;

        try
        {
            marker.IsSelected = false;
            return AdornerManager.DeselectMarker(marker, this);
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 선택 해제 실패: {marker.Title}, {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 모든 마커 선택 해제
    /// </summary>
    public void DeselectAllMarkers()
    {
        try
        {
            if (CustomMarkers != null)
            {
                foreach (var img in CustomMarkers)
                {
                    img.IsSelected = false;
                }
            }

            AdornerManager?.DeselectAllMarkers(this);
        }
        catch (Exception ex)
        {
            _log?.Error($"모든 마커 선택 해제 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 다중 선택 모드 설정
    /// </summary>
    /// <param name="enabled">다중 선택 활성화 여부</param>
    public void SetMultiSelectMode(bool enabled)
    {
        try
        {
            AdornerManager?.SetMultiSelectMode(enabled);
            _log?.Info($"다중 선택 모드: {(enabled ? "활성화" : "비활성화")}");
        }
        catch (Exception ex)
        {
            _log?.Error($"다중 선택 모드 설정 실패: {ex.Message}");
        }
    }

    #endregion
    #region Keyboard Input Handling

    /// <summary>
    /// 키보드 입력 처리 (회전 등)
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 편집 모드에서 ESC 키로 모든 편집 취소
        if (IsEditMode && e.Key == Key.Escape)
        {
            AdornerManager?.CancelAllEditing(this);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Left:
                    RotateMap(-5);
                    e.Handled = true;
                    break;
                case Key.Right:
                    RotateMap(5);
                    e.Handled = true;
                    break;
                case Key.R:
                    ResetRotation();
                    e.Handled = true;
                    break;
                case Key.A: // Ctrl+A: 모든 마커 선택 (다중 선택 모드에서)
                    if (IsEditMode && AdornerManager?.MultiSelectEnabled == true)
                    {
                        SelectAllMarkers();
                        e.Handled = true;
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// 마우스 휠 처리 (Shift + 휠 = 회전)
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            double rotationDelta = e.Delta > 0 ? 5 : -5;
            RotateMap(rotationDelta);
            e.Handled = true;
            return;
        }

        base.OnMouseWheel(e);
    }

    /// <summary>
    /// 모든 마커 선택 (다중 선택 모드)
    /// </summary>
    private void SelectAllMarkers()
    {
        try
        {
            if (AdornerManager?.MultiSelectEnabled == true)
            {
                foreach (var marker in CustomMarkers)
                {
                    SelectMarker(marker);
                }
                _log?.Info($"모든 마커 선택 완료: {CustomMarkers.Count}개");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"모든 마커 선택 실패: {ex.Message}");
        }
    }

    #endregion
    #region Image Edit Methods

    /// <summary>
    /// 이미지 편집 처리
    /// </summary>
    private bool HandleImageEdit(Point mousePos, PointLatLng geoPos, MouseButtonEventArgs e)
    {
        var selectedImage = CustomImages.FirstOrDefault(img => img.IsSelected);
        if (selectedImage == null) return false;

        _resizeHandle = GetClickedImageHandle(selectedImage, mousePos);

        if (_resizeHandle != ResizeHandle.None)
        {
            StartImageDrag(selectedImage, mousePos, _resizeHandle);
            e.Handled = true;
            return true;
        }

        if (selectedImage.Contains(geoPos))
        {
            StartImageDrag(selectedImage, mousePos, ResizeHandle.Move);
            e.Handled = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 이미지 드래그 시작
    /// </summary>
    private void StartImageDrag(GMapCustomImage image, Point mousePos, ResizeHandle handle)
    {
        _draggedImage = image;
        _resizeHandle = handle;
        _dragStartPoint = mousePos;
        _isDragging = true;
        _isImageDrag = true;

        SetupImageDragData(image);
        this.CaptureMouse();
        _log?.Info($"이미지 편집 시작: {handle}");
    }

    /// <summary>
    /// 이미지 드래그 처리
    /// </summary>
    private void ProcessImageDrag(Point currentPos, double deltaX, double deltaY)
    {
        var curBounds = _draggedImage.ImageBounds;
        RectLatLng newBounds = curBounds;

        switch (_resizeHandle)
        {
            case ResizeHandle.Move:
                newBounds = MoveBounds(curBounds, deltaX, deltaY);
                break;
            case ResizeHandle.TopLeft:
            case ResizeHandle.TopRight:
            case ResizeHandle.BottomLeft:
            case ResizeHandle.BottomRight:
                newBounds = ResizeBoundsWithRatio(curBounds, deltaX, deltaY, _resizeHandle);
                break;
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

        if (newBounds.WidthLng > 0.0001 && newBounds.HeightLat > 0.0001)
        {
            _draggedImage.ImageBounds = newBounds;
            InvalidateVisual();
            _dragStartPoint = currentPos;
        }
    }

    #endregion
    #region Handle Detection Methods
    /// <summary>
    /// 클릭된 이미지 핸들 감지
    /// </summary>
    private ResizeHandle GetClickedImageHandle(GMapCustomImage image, Point mousePos)
    {
        var bounds = image.ImageBounds;
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

        var imageRect = new Rect(topLeft.X, topLeft.Y,
            bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

        var handleSize = 8;
        var tolerance = handleSize + 2;

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
    #region Rendering Methods

    /// <summary>
    /// 이미지 오버레이 렌더링
    /// </summary>
    private void RenderImageOverlays(DrawingContext drawingContext)
    {
        try
        {
            foreach (var customImage in CustomImages.Where(img => img.Visibility))
            {
                RenderSingleImageOverlay(drawingContext, customImage);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 렌더링 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 단일 이미지 렌더링
    /// </summary>
    private void RenderSingleImageOverlay(DrawingContext drawingContext, GMapCustomImage customImage)
    {
        if (customImage?.Img == null) return;

        try
        {
            var bounds = customImage.ImageBounds;
            var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
            var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

            var imageRect = new Rect(topLeft.X, topLeft.Y,
                bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

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

            // 선택된 이미지 테두리 및 핸들 표시
            if (ShowImageBounds || customImage.IsSelected)
            {
                var boundsPen = new Pen(Brushes.Red, 2) { DashStyle = DashStyles.Dash };
                drawingContext.DrawRectangle(null, boundsPen, imageRect);

                if (customImage.IsSelected && IsEditMode)
                {
                    DrawResizeHandles(drawingContext, imageRect);
                }

                if (!string.IsNullOrEmpty(customImage.Title))
                {
                    var nameText = new FormattedText(customImage.Title,
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                        new Typeface("Arial"), 12, Brushes.Red, 96);
                    drawingContext.DrawText(nameText, new Point(imageRect.X, imageRect.Y - 15));
                }
            }

            // Transform 스택 정리
            if (customImage.Opacity < 1.0) drawingContext.Pop();
            if (customImage.Rotation != 0) drawingContext.Pop();
        }
        catch (Exception ex)
        {
            _log?.Error($"단일 이미지 렌더링 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 이미지 크기 조정 핸들 그리기
    /// </summary>
    private void DrawResizeHandles(DrawingContext drawingContext, Rect imageRect)
    {
        var handleSize = 8;
        var cornerHandleBrush = Brushes.Blue;        // 모서리: 파란색 (비율 유지)
        var edgeHandleBrush = Brushes.Orange;        // 변 중앙: 주황색 (자유 조정)
        var handlePen = new Pen(Brushes.White, 1);

        // 모서리 핸들 (사각형)
        var cornerHandles = new[]
        {
            new Point(imageRect.Left, imageRect.Top),      // 좌상
            new Point(imageRect.Right, imageRect.Top),     // 우상  
            new Point(imageRect.Right, imageRect.Bottom),  // 우하
            new Point(imageRect.Left, imageRect.Bottom),   // 좌하
        };

        // 변 중앙 핸들 (원형)
        var edgeHandles = new[]
        {
            new Point(imageRect.Left + imageRect.Width/2, imageRect.Top),    // 상중
            new Point(imageRect.Right, imageRect.Top + imageRect.Height/2),  // 우중
            new Point(imageRect.Left + imageRect.Width/2, imageRect.Bottom), // 하중
            new Point(imageRect.Left, imageRect.Top + imageRect.Height/2)    // 좌중
        };

        // 모서리 핸들 그리기
        foreach (var handle in cornerHandles)
        {
            var handleRect = new Rect(handle.X - handleSize / 2, handle.Y - handleSize / 2,
                handleSize, handleSize);
            drawingContext.DrawRectangle(cornerHandleBrush, handlePen, handleRect);
        }

        // 변 중앙 핸들 그리기
        foreach (var handle in edgeHandles)
        {
            drawingContext.DrawEllipse(edgeHandleBrush, handlePen, handle, handleSize / 2, handleSize / 2);
        }
    }

    #endregion
    #region Map Rotation Methods

    /// <summary>
    /// 회전 정보 렌더링
    /// </summary>
    private void RenderRotationInfo(DrawingContext drawingContext)
    {
        try
        {
            // 회전 각도 텍스트
            var rotationText = new FormattedText($"회전: {MapRotation:F1}°",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Arial"), 14, Brushes.Black, 96);

            // 배경 사각형
            var textRect = new Rect(10, 10, rotationText.Width + 10, rotationText.Height + 6);
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                new Pen(Brushes.Gray, 1), textRect);

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
                new Pen(Brushes.Black, 2), center, radius, radius);

            // 북쪽 화살표 (회전 적용)
            var northAngle = -MapRotation * Math.PI / 180;
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

    /// <summary>
    /// 지도 회전 (상대적)
    /// </summary>
    public void RotateMap(double deltaAngle)
    {
        double newRotation = MapRotation + deltaAngle;
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

    /// <summary>
    /// 지도 회전 적용
    /// </summary>
    private void ApplyMapRotation(double rotation)
    {
        try
        {
            rotation = NormalizeAngle(rotation);
            Bearing = (float)rotation;
            UpdateOverlaysAfterRotation();
            _log?.Info($"지도 회전 적용: {rotation:F1}도");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 적용 실패: {ex.Message}");
        }
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
            foreach (var customImage in CustomImages)
            {
                customImage.Rotation = -MapRotation;
            }

            InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"회전 후 오버레이 업데이트 실패: {ex.Message}");
        }
    }

    #endregion
    #region Image Management Methods

    /// <summary>
    /// 이미지 오버레이 추가
    /// </summary>
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

    /// <summary>
    /// 이미지 오버레이 제거
    /// </summary>
    public void RemoveImageOverlay(GMapCustomImage customImage)
    {
        if (customImage == null) return;

        try
        {
            if (CustomImages.Remove(customImage))
            {
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

    /// <summary>
    /// 모든 이미지 오버레이 제거
    /// </summary>
    public void ClearImageOverlays()
    {
        try
        {
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

    /// <summary>
    /// 특정 위치의 이미지 오버레이 찾기
    /// </summary>
    public List<GMapCustomImage> GetImageOverlaysAt(PointLatLng position)
    {
        return CustomImages.Where(img => img.Visibility && img.Contains(position)).ToList();
    }

    /// <summary>
    /// 경계 영역과 교차하는 이미지 오버레이 찾기
    /// </summary>
    public List<GMapCustomImage> GetImageOverlaysIntersecting(RectLatLng bounds)
    {
        return CustomImages.Where(img => img.Visibility && img.IntersectsWith(bounds)).ToList();
    }

    /// <summary>
    /// 모든 이미지 투명도 설정
    /// </summary>
    public void SetAllImageOverlaysOpacity(double opacity)
    {
        foreach (var customImage in CustomImages)
        {
            customImage.Opacity = opacity;
        }
        InvalidateVisual();
    }

    /// <summary>
    /// 모든 이미지 가시성 설정
    /// </summary>
    public void SetAllImageOverlaysVisibility(bool isVisible)
    {
        foreach (var customImage in CustomImages)
        {
            customImage.Visibility = isVisible;
        }
        InvalidateVisual();
    }

    #endregion
    #region Edit Mode Management

    /// <summary>
    /// 편집 모드 활성화/비활성화
    /// </summary>
    public void SetEditMode(bool enabled)
    {
        if (IsEditMode == enabled) return;

        IsEditMode = enabled;

        if (!IsEditMode)
        {
            // 편집 모드 해제 시 모든 선택 해제
            foreach (var img in CustomImages) img.IsSelected = false;
            foreach (var marker in CustomMarkers) marker.IsSelected = false;

            // 모든 Adorner 제거
            AdornerManager?.DeselectAllMarkers(this);

            ShowImageBounds = false;
            InvalidateVisual();
        }

        _log?.Info($"편집 모드: {(enabled ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 특정 위치의 객체 찾기
    /// </summary>
    public object GetObjectAt(PointLatLng position)
    {
        // 이미지 우선 확인
        var images = GetImageOverlaysAt(position);
        if (images.Any()) return images.First();

        // 마커 확인
        var markers = CustomMarkers.Where(m =>
            Math.Abs(m.Position.Lat - position.Lat) < 0.0001 &&
            Math.Abs(m.Position.Lng - position.Lng) < 0.0001).ToList();

        return markers.FirstOrDefault();
    }

    #endregion
    #region Helper Methods

    /// <summary>
    /// 드래그 상태 초기화
    /// </summary>
    private void ResetDragState()
    {
        _isDragging = false;
        _isImageDrag = false;
        _draggedImage = null;
        _resizeHandle = ResizeHandle.None;
        this.ReleaseMouseCapture();
    }

    /// <summary>
    /// 이미지 드래그 데이터 설정
    /// </summary>
    private void SetupImageDragData(GMapCustomImage selectedImage)
    {
        var bounds = selectedImage.ImageBounds;
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

        _originalSize = new Size(Math.Abs(bottomRight.X - topLeft.X), Math.Abs(bottomRight.Y - topLeft.Y));

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
        }

        var deltaX = _originalDragPoint.X - _originalFixedPoint.X;
        var deltaY = _originalDragPoint.Y - _originalFixedPoint.Y;
        _originalDiagonal = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
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
    /// 회전 각도 계산
    /// </summary>
    private double CalculateRotationAngle(GPoint center, Point point)
    {
        var deltaX = point.X - center.X;
        var deltaY = point.Y - center.Y;
        var angle = Math.Atan2(deltaX, -deltaY) * 180 / Math.PI;
        return angle < 0 ? angle + 360 : angle;
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

    #endregion
    #region Image Resize Helper Methods

    /// <summary>
    /// 이미지 경계 이동
    /// </summary>
    private RectLatLng MoveBounds(RectLatLng bounds, double deltaX, double deltaY)
    {
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var newTopLeft = new Point(topLeft.X + deltaX, topLeft.Y + deltaY);
        var newGeoTopLeft = FromLocalToLatLng((int)newTopLeft.X, (int)newTopLeft.Y);

        return new RectLatLng(newGeoTopLeft.Lat, newGeoTopLeft.Lng, bounds.WidthLng, bounds.HeightLat);
    }

    /// <summary>
    /// 비율 유지하며 이미지 크기 조정
    /// </summary>
    private RectLatLng ResizeBoundsWithRatio(RectLatLng bounds, double deltaX, double deltaY, ResizeHandle corner)
    {
        GPoint tlGP = FromLatLngToLocal(bounds.LocationTopLeft);
        GPoint brGP = FromLatLngToLocal(bounds.LocationRightBottom);

        double curW = brGP.X - tlGP.X;
        double curH = brGP.Y - tlGP.Y;
        if (curW <= 2 || curH <= 2) return bounds;

        double aspect = curW / curH;
        double drag = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        double diag = Math.Sqrt(curW * curW + curH * curH);
        if (drag < 0.1 || diag < 1.0) return bounds;

        bool expand = corner switch
        {
            ResizeHandle.TopLeft => deltaX < 0 || deltaY < 0,
            ResizeHandle.TopRight => deltaX > 0 || deltaY < 0,
            ResizeHandle.BottomLeft => deltaX < 0 || deltaY > 0,
            ResizeHandle.BottomRight => deltaX > 0 || deltaY > 0,
            _ => false
        };

        double scale = Math.Max(0.05, 1.0 + (expand ? drag : -drag) / diag);
        double newW = curW * scale;
        double newH = newW / aspect;

        Point newTL, newBR;
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

        var geoTL = FromLocalToLatLng((int)Math.Round(newTL.X), (int)Math.Round(newTL.Y));
        var geoBR = FromLocalToLatLng((int)Math.Round(newBR.X), (int)Math.Round(newBR.Y));

        return new RectLatLng(geoTL.Lat, geoTL.Lng,
            Math.Abs(geoBR.Lng - geoTL.Lng), Math.Abs(geoTL.Lat - geoBR.Lat));
    }

    /// <summary>
    /// 자유 형태로 이미지 크기 조정
    /// </summary>
    private RectLatLng ResizeBoundsFree(RectLatLng bounds, double deltaX, double deltaY,
        bool adjustLeft, bool adjustTop, bool adjustRight, bool adjustBottom)
    {
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);

        if (adjustLeft) topLeft.X += (long)deltaX;
        if (adjustTop) topLeft.Y += (long)deltaY;
        if (adjustRight) bottomRight.X += (long)deltaX;
        if (adjustBottom) bottomRight.Y += (long)deltaY;

        var minSize = 20;
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

        var newTopLeft = FromLocalToLatLng((int)topLeft.X, (int)topLeft.Y);
        var newBottomRight = FromLocalToLatLng((int)bottomRight.X, (int)bottomRight.Y);

        return new RectLatLng(newTopLeft.Lat, newTopLeft.Lng,
            Math.Abs(newBottomRight.Lng - newTopLeft.Lng),
            Math.Abs(newTopLeft.Lat - newBottomRight.Lat));
    }

    #endregion
    #region Dependency Properties

    /// <summary>
    /// 편집 모드 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(nameof(IsEditMode), typeof(bool), typeof(GMapCustomControl),
            new PropertyMetadata(false, OnIsEditModeChanged));

    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    private static void OnIsEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            control.SetEditMode((bool)e.NewValue);
        }
    }

    /// <summary>
    /// MGRS 그리드 표시 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty ShowMGRSGridProperty =
        DependencyProperty.Register(nameof(ShowMGRSGrid), typeof(bool), typeof(GMapCustomControl),
            new PropertyMetadata(false, OnShowMGRSGridChanged));

    public bool ShowMGRSGrid
    {
        get => (bool)GetValue(ShowMGRSGridProperty);
        set => SetValue(ShowMGRSGridProperty, value);
    }

    private static void OnShowMGRSGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            control.InvalidateVisual();
        }
    }

    /// <summary>
    /// 지도 회전 각도 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty MapRotationProperty =
        DependencyProperty.Register(nameof(MapRotation), typeof(double), typeof(GMapCustomControl),
            new PropertyMetadata(0.0, OnMapRotationChanged));

    public double MapRotation
    {
        get => (double)GetValue(MapRotationProperty);
        set => SetValue(MapRotationProperty, value);
    }

    private static void OnMapRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            control.ApplyMapRotation((double)e.NewValue);
        }
    }

    /// <summary>
    /// 회전 중심점 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty RotationCenterProperty =
        DependencyProperty.Register(nameof(RotationCenter), typeof(PointLatLng?), typeof(GMapCustomControl),
            new PropertyMetadata(null));

    public PointLatLng? RotationCenter
    {
        get => (PointLatLng?)GetValue(RotationCenterProperty);
        set => SetValue(RotationCenterProperty, value);
    }

    /// <summary>
    /// 회전 스냅 각도 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty RotationSnapAngleProperty =
        DependencyProperty.Register(nameof(RotationSnapAngle), typeof(double), typeof(GMapCustomControl),
            new PropertyMetadata(0.0));

    public double RotationSnapAngle
    {
        get => (double)GetValue(RotationSnapAngleProperty);
        set => SetValue(RotationSnapAngleProperty, value);
    }

    /// <summary>
    /// 회전 컨트롤 표시 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty ShowRotationControlProperty =
        DependencyProperty.Register(nameof(ShowRotationControl), typeof(bool), typeof(GMapCustomControl),
            new PropertyMetadata(false));

    public bool ShowRotationControl
    {
        get => (bool)GetValue(ShowRotationControlProperty);
        set => SetValue(ShowRotationControlProperty, value);
    }

    #endregion
    #region Public Properties

    /// <summary>
    /// 커스텀 마커 컬렉션
    /// </summary>
    public ObservableCollection<GMapCustomMarker> CustomMarkers { get; private set; }

    /// <summary>
    /// 커스텀 이미지 컬렉션
    /// </summary>
    public ObservableCollection<GMapCustomImage> CustomImages { get; private set; }

    /// <summary>
    /// 이미지 경계선 표시 여부
    /// </summary>
    public bool ShowImageBounds { get; set; } = false;

    /// <summary>
    /// 현재 활성화된 이미지 오버레이 개수
    /// </summary>
    public int ActiveImageOverlayCount => CustomImages?.Count(img => img.Visibility) ?? 0;

    /// <summary>
    /// 전체 이미지 오버레이 개수
    /// </summary>
    public int TotalImageOverlayCount => CustomImages?.Count ?? 0;

    #endregion
    #region Public Methods

    /// <summary>
    /// 두 점을 연결하는 선에 맞춰 회전
    /// </summary>
    public void AlignToLine(PointLatLng point1, PointLatLng point2)
    {
        var screenPoint1 = FromLatLngToLocal(point1);
        var screenPoint2 = FromLatLngToLocal(point2);

        double deltaX = screenPoint2.X - screenPoint1.X;
        double deltaY = screenPoint2.Y - screenPoint1.Y;
        double angle = Math.Atan2(deltaY, deltaX) * 180 / Math.PI - 90;

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

    /// <summary>
    /// AdornerManager 상태 로그 출력
    /// </summary>
    public string? LogAdornerStatistics()
    {
        if (AdornerManager == null) return null;
        return AdornerManager?.LogStatistics();
    }

    /// <summary>
    /// 메모리 정리 실행
    /// </summary>
    public void TrimMemory()
    {
        AdornerManager?.TrimMemory();
    }

    #endregion
    #region IDisposable Support

    ///// <summary>
    ///// 리소스 정리
    ///// </summary>
    //protected override void Dispose(bool disposing)
    //{
    //    if (disposing)
    //    {
    //        try
    //        {
    //            // AdornerManager 정리
    //            if (AdornerManager != null)
    //            {
    //                UnsubscribeAdornerManagerEvents();
    //                AdornerManager.Dispose();
    //                AdornerManager = null;
    //            }

    //            // 이벤트 정리
    //            OnMapClicked = null;
    //            OnMarkerClicked = null;
    //            OnImageClicked = null;
    //            MarkerEditStarted = null;
    //            MarkerEditCompleted = null;
    //            MarkerEditCancelled = null;
    //            AdornerCreated = null;
    //            AdornerRemoved = null;

    //            _log?.Info("GMapCustomControl 리소스 정리 완료");
    //        }
    //        catch (Exception ex)
    //        {
    //            _log?.Error($"GMapCustomControl 정리 중 오류: {ex.Message}");
    //        }
    //    }

    //    base.Dispose(disposing);
    //}

    #endregion
    #region Enums

    public enum ResizeHandle
    {
        None, TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
        Move
    }

    #endregion
    #region Private Fields

    private IEventAggregator? _eventAggregator;
    private ILogService? _log;
    private MGRSGridOverlayService _mgrsOverlay;

    // 상수
    public int VISIBILITY_ZOOM = 14;

    // 드래그 관련
    private bool _isDragging = false;
    private Point _dragStartPoint;

    // 이미지 편집 관련
    private GMapCustomImage _draggedImage = null;
    private ResizeHandle _resizeHandle = ResizeHandle.None;
    private bool _isImageDrag = false;
    private Size _originalSize;
    private Point _originalFixedPoint;
    private Point _originalDragPoint;
    private double _originalDiagonal;
    #endregion
}