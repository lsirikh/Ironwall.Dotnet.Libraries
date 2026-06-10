using Caliburn.Micro;
using GMap.NET.WindowsPresentation;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using System.Globalization;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using System.Diagnostics.Eventing.Reader;

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

    /// <summary>
    /// 오버레이 맵 타일 Canvas — OnRender에서 base(타일) 후, 심볼(ItemsPresenter) 전에 렌더링
    /// CustomMapOverlayService가 이 Canvas에 Image를 배치
    /// </summary>
    public System.Windows.Controls.Canvas? OverlayMapCanvas { get; set; }

    public GMapCustomControl()
    {
        _eventAggregator = IoC.Get<IEventAggregator>();
        _log = IoC.Get<ILogService>();

        InitializeCollections();
        InitializeEvents();
        InitializeAdornerManager();
        // 라인 드로잉 서비스 초기화
        InitializeLineDrawingService();

        _mgrsOverlay = new MGRSGridOverlayService(_log);
        _log?.Info("GMapCustomControl 초기화 완료");
    }

    public GMapCustomControl(ILogService log,
                            IEventAggregator ea) : this()
    {
        _log = log;
        _eventAggregator = ea;
    }

    #endregion
    
    #region Initialization

    /// <summary>
    /// 컬렉션 초기화
    /// </summary>
    private void InitializeCollections()
    {
        //CustomMarkers = new ObservableCollection<IEditableMarker>();
        CustomImages = new ObservableCollection<GMapCustomImage>();
    }

    /// <summary>
    /// 이벤트 핸들러 등록
    /// </summary>
    private void InitializeEvents()
    {
        Markers.CollectionChanged += Markers_CollectionChanged;

        OnAreaChange += GMapCustomControl_OnAreaChange;

        // 기존 GMapControl의 줌 이벤트 활용
        OnMapZoomChanged += GMapCustomControl_OnMapZoomChanged;

        // 위치 변경도 함께 연결
        OnPositionChanged += GMapCustomControl_OnPositionChanged;
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
    
    #region Line Drawing Fields

    private LineDrawingService _lineDrawingService;

    #endregion

    #region Line Drawing Properties

    /// <summary>
    /// 라인 드로잉 서비스
    /// </summary>
    public LineDrawingService LineDrawingService => _lineDrawingService;

    /// <summary>
    /// 라인 드로잉 중 여부
    /// </summary>
    public bool IsLineDrawing => _lineDrawingService?.IsDrawing ?? false;

    #endregion

    #region Integration Events
    /// <summary>
    /// 지도 클릭 이벤트 - ViewModel에 클릭 위치 전달
    /// </summary>
    public event Action<PointLatLng, Point> OnMapClicked;

    /// <summary>
    /// 마커 클릭 이벤트 - ViewModel에 클릭된 마커 전달
    /// </summary>
    public event Action<IEditableMarker> OnMarkerClicked;

    /// <summary>
    /// 마커 우클릭 이벤트 - ViewModel에 우클릭된 마커 전달 (컨텍스트 메뉴용)
    /// </summary>
    public event Action<IEditableMarker>? OnMarkerRightClicked;

    /// <summary>
    /// 이미지 클릭 이벤트 - ViewModel에 클릭된 이미지 전달
    /// </summary>
    public event Action<GMapCustomImage> OnImageClicked;

    /// <summary>
    /// 이미지 우클릭 이벤트 - ViewModel에 우클릭된 이미지 전달 (회전 초기화/입력 컨텍스트 메뉴용, FR-9)
    /// </summary>
    public event Action<GMapCustomImage>? OnImageRightClicked;

    /// <summary>
    /// 이미지 편집(이동/리사이즈/회전) 완료 이벤트 - DB 영속화 트리거 (FR-8)
    /// </summary>
    public Action<GMapCustomImage>? OnImageEditCompleted;

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
    
    #region Line Drawing Methods

    /// <summary>
    /// 라인 드로잉 서비스 초기화
    /// </summary>
    private void InitializeLineDrawingService()
    {
        _lineDrawingService = new LineDrawingService(this, _log);

        // 이벤트 구독
        _lineDrawingService.StateChanged += OnLineDrawingStateChanged;
        _lineDrawingService.PointAdded += OnLinePointAdded;
        _lineDrawingService.LineCompleted += OnLineCompleted;
        _lineDrawingService.DrawingCancelled += OnLineDrawingCancelled;

        _log?.Info("라인 드로잉 서비스 초기화 완료");
    }

    /// <summary>
    /// 라인 드로잉 시작
    /// </summary>
    public async Task<bool> StartLineDrawingAsync(LineDrawingParameters parameters = null)
    {
        try
        {
            // 편집 모드 활성화
            //IsEditMode = true;

            // 라인 드로잉 시작
            return await _lineDrawingService.StartLineDrawingAsync(parameters);
        }
        catch (Exception ex)
        {
            _log?.Error($"라인 드로잉 시작 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 라인 드로잉 완료
    /// </summary>
    public async Task<bool> CompleteLineDrawingAsync()
    {
        return await _lineDrawingService?.CompleteDrawingAsync();
    }

    /// <summary>
    /// 라인 드로잉 취소
    /// </summary>
    public async Task<bool> CancelLineDrawingAsync()
    {
        return await _lineDrawingService?.CancelDrawingAsync();
    }

    #endregion

    #region Line Drawing Event Handlers

    /// <summary>
    /// 라인 드로잉 상태 변경 이벤트 핸들러
    /// </summary>
    private void OnLineDrawingStateChanged(object? sender, LineDrawingState state)
    {
        _log?.Info($"라인 드로잉 상태 변경: {state}");

        // 완료/취소 시 편집 모드 해제 고려
        if (state == LineDrawingState.Completed || state == LineDrawingState.Cancelled)
        {
            // 필요시 편집 모드 해제
            // IsEditingMode = false;
        }
    }

    /// <summary>
    /// 라인 포인트 추가 이벤트 핸들러
    /// </summary>
    private void OnLinePointAdded(object? sender, PointLatLng point)
    {
        _log?.Info($"라인 포인트 추가: {point}");
    }

    /// <summary>
    /// 라인 완성 이벤트 핸들러
    /// </summary>
    private void OnLineCompleted(object? sender, ILineEditableMarker lineMarker)
    {
        _log?.Info($"라인 완성: {lineMarker.Title}");

        // 완성된 라인 마커 이벤트 발생
        //OnMarkerCreated?.Invoke(lineMarker);

    }

    /// <summary>
    /// 라인 드로잉 취소 이벤트 핸들러
    /// </summary>
    private void OnLineDrawingCancelled(object? sender, EventArgs e)
    {
        _log?.Info("라인 드로잉 취소됨");
    }

    #endregion
    
    #region Override Methods

    // OnRender 시점에 항상 Visual Tree에 연결된 상태이므로 null 반환 없음. 폴백 1.0은 96DPI 동작 유지.
    internal double PixelsPerDip
        => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

    protected override void OnInitialized(EventArgs e)
    {
        _eventAggregator?.SubscribeOnUIThread(this);

        // 맵 패닝을 좌클릭으로 변경 (GMap.NET 기본: Right)
        DragButton = System.Windows.Input.MouseButton.Left;

        base.OnInitialized(e);

        var tier = System.Windows.Media.RenderCapability.Tier >> 16;
        if (tier == 0)
            _log?.Warning("[GMapCustomControl] 소프트웨어 렌더링 모드 감지 (Tier=0). RDP/가상화 환경 가능성. 패닝 성능 저하 예상.");
        else if (tier == 1)
            _log?.Info("[GMapCustomControl] 부분 하드웨어 가속 모드 (Tier=1).");
        else
            _log?.Info($"[GMapCustomControl] 하드웨어 가속 렌더링 (Tier={tier}).");
    }

    /// <summary>
    /// 메인 렌더링 메서드
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        // 오버레이 맵 타일 렌더링 (베이스맵 위, 심볼 아래)
        RenderOverlayMapTiles(drawingContext);

        RenderImageOverlays(drawingContext);

        if (ShowMGRSGrid)
        {
            _mgrsOverlay.DrawMGRSGrid(drawingContext, ViewArea, (int)Zoom, this);
        }

        if (IsSnapToGridEnabled)
        {
            _snapGridOverlay.DrawGrid(drawingContext, this, PixelsPerDip);
        }

        if (ShowRotationControl)
        {
            RenderRotationInfo(drawingContext);
        }
    }

    #endregion
    
    #region Event Handlers
    /// <summary>
    /// 줌 변경 이벤트 핸들러
    /// </summary>
    private void GMapCustomControl_OnMapZoomChanged()
    {
        // ★ NFR-3(b) 백스톱 — 드래그 중 프로그램적 줌 발생 시 즉시 드래그 종료 (점프 bounds 미커밋)
        if (_isImageDrag)
        {
            ResetDragState();
            return;
        }

        try
        {
            _log?.Info($"줌 변경됨: {Zoom}");

            // ViewArea 계산하여 OnAreaChange 이벤트 발생
            var viewArea = ViewArea;
            var zoom = Zoom;

            // OnAreaChange 이벤트 발생 (MapViewModel이 구독)
            TriggerSelectionChange(viewArea, zoom, false);

            DeselectAllMarkers();

            //_log?.Info($"OnAreaChange 이벤트 발생: Zoom={zoom}");
        }
        catch (Exception ex)
        {
            _log?.Error($"줌 변경 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 위치 변경 이벤트 핸들러
    /// </summary>
    private void GMapCustomControl_OnPositionChanged(PointLatLng point)
    {
        var now = DateTime.Now;

        // 드래그 시작 감지 (false → true 전환)
        if (IsDragging && !_prevDragging)
        {
            _panStartTime = now;
            _panSkipCount = 0;
            _log?.Info($"[PAN] ===DRAG-START=== t={now:HH:mm:ss.fff} lat={point.Lat:F5} lng={point.Lng:F5} zoom={Zoom}");
        }
        _prevDragging = IsDragging;

        // 드래그 중에는 TriggerSelectionChange → OnAreaChange → UpdateMarkersVisibilityByZoom + InvalidateVisual
        // 체인이 매 프레임 실행되어 심볼이 타일과 어긋나는 버그 유발 (RDP 환경 특히 심각).
        // 드래그 완료 후(IsDragging=false)에만 영역 변경 처리를 허용한다.
        if (IsDragging)
        {
            _panSkipCount++;
            // RDP 이벤트 압축 진단: 10프레임마다 한 번 로그 (너무 많으면 로그 폭주)
            if (_panSkipCount % 10 == 1)
                _log?.Info($"[PAN] SKIP#{_panSkipCount} t={now:HH:mm:ss.fff} lat={point.Lat:F5} lng={point.Lng:F5}");
            return;
        }

        try
        {
            var viewArea = ViewArea;
            var zoom = Zoom;
            _log?.Info($"[PAN] OnPositionChanged EXEC — drag=false lat={point.Lat:F5} lng={point.Lng:F5} t={now:HH:mm:ss.fff}");
            TriggerSelectionChange(viewArea, zoom, false);
        }
        catch (Exception ex)
        {
            _log?.Error($"위치 변경 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 줌 레벨에 따른 마커 가시성 업데이트
    /// </summary>
    private void UpdateMarkersVisibilityByZoom()
    {
        try
        {
            if (Markers == null) return;
            //현재 Markers가 Add 될때마다 UpdateMarkersVisibilityByZoom로직이 수행되는 비효율성이 있다.
            //*****버그****** 이 문제를 해결해야된다.
            foreach (var marker in Markers.OfType<IEditableMarker>().ToList())
            {
                if (SetMarkerVisibility(marker))
                {
                    marker.IsVisible = true;
                }
                else
                {
                    marker.IsVisible = false;
                }
            }

            //_log?.Info($"마커 가시성 업데이트 완료: Zoom={Zoom}, 마커 수={Markers?.Count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 가시성 업데이트 실패: {ex.Message}");
        }
    }

    private bool SetMarkerVisibility(IEditableMarker marker)
        => Zoom >= marker.Zoom && marker.IsLayerEnabled;
    /// <summary>
    /// 지도 영역 변경 이벤트
    /// </summary>
    private void GMapCustomControl_OnAreaChange(RectLatLng selection, double zoom, bool zoomToFit)
    {
       try
       {
            //_log?.Info($"지도 영역 변경: Zoom={zoom}, ZoomToFit={zoomToFit}");
            //_log?.Info($"영역: Lat={selection.Lat:F6}, Lng={selection.Lng:F6}, W={selection.WidthLng:F6}, H={selection.HeightLat:F6}");

            // 줌 레벨에 따른 마커 가시성 처리
            UpdateMarkersVisibilityByZoom();

            // 드래그 중 InvalidateVisual 차단:
            // Markers.Add 등이 드래그 중 호출되면 GMap.NET 내부 ForceUpdateOverlays(newItems)
            // → TriggerSelectionChange → 이 핸들러 경로로 InvalidateVisual이 발화됨.
            // RDP 환경에서 드래그 중 InvalidateVisual은 GMapCustomControl_OnPositionChanged 주석 참조.
            // 드래그 종료 시 GMap.NET OnMouseUp → ForceUpdateOverlays(all) + OnMouseLeftButtonUp
            // → TriggerSelectionChange에서 InvalidateVisual이 IsDragging=false 상태로 호출됨.
            if (!IsDragging)
            {
                InvalidateVisual();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 영역 변경 처리 실패: {ex.Message}");
        }
    }



    /// <summary>
    /// 마커 컬렉션 변경 이벤트
    /// </summary>
    private void Markers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var newItem in e.NewItems?.OfType<IEditableMarker>() ?? Enumerable.Empty<IEditableMarker>())
                {
                    RegisterMarkerForAdorner(newItem);
                    //_log?.Info($"마커 Adorner 등록: {newItem.Title}");
                }
                _log?.Info($"Markers 최종 개수: {Markers.Count}");
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var oldItem in e.OldItems?.OfType<IEditableMarker>() ?? Enumerable.Empty<IEditableMarker>())
                {
                    UnregisterMarkerFromAdorner(oldItem);
                    //_log?.Info($"마커 Adorner 해제: {oldItem.Title}");
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // 기존 마커들 Adorner 해제
                var oldMarkers = e.OldItems?.OfType<IEditableMarker>() ?? Enumerable.Empty<IEditableMarker>();
                var newMarkers = e.NewItems?.OfType<IEditableMarker>() ?? Enumerable.Empty<IEditableMarker>();

                foreach (var oldMarker in oldMarkers)
                {
                    UnregisterMarkerFromAdorner(oldMarker);
                }

                // 새 마커들 Adorner 등록
                foreach (var newMarker in newMarkers)
                {
                    RegisterMarkerForAdorner(newMarker);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // Reset은 컬렉션이 완전히 비워지거나 대량 변경될 때 발생
                // 모든 기존 Adorner 정리
                AdornerManager?.DeselectAllMarkers(this);

                // 현재 마커들에 대해 Adorner 재등록
                foreach (var marker in Markers.OfType<IEditableMarker>())
                {
                    RegisterMarkerForAdorner(marker);
                }

                _log?.Info($"마커 컬렉션 Reset 완료: {Markers.Count}개 마커 재등록");
                break;
        }
    }
    public void TriggerMarkerClicked(GMapMarker marker)
    {
        try
        {
            if (marker is IEditableMarker editableMarker)
            {
                OnMarkerClicked?.Invoke(editableMarker);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"TriggerMarkerClicked 실패: {ex.Message}");
        }
    }

    public void TriggerMarkerRightClicked(GMapMarker marker)
    {
        try
        {
            if (marker is IEditableMarker editableMarker)
                OnMarkerRightClicked?.Invoke(editableMarker);
        }
        catch (Exception ex)
        {
            _log?.Error($"TriggerMarkerRightClicked 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// AdornerManager 이벤트 핸들러들
    /// </summary>
    private void OnMarkerEditStarted(object? sender, MarkerEditStartedEventArgs e)
    {
        _log?.Info($"마커 편집 시작: {e.Marker.Title}");
        MarkerEditStarted?.Invoke(this, e);
    }

    private void OnMarkerEditCompleted(object? sender, MarkerEditCompletedEventArgs e)
    {
        _log?.Info($"마커 편집 완료: {e.Marker.Title}, 변경: {e.GetChangesSummary()}");
        MarkerEditCompleted?.Invoke(this, e);
    }

    private void OnMarkerEditCancelled(object? sender, MarkerEditCancelledEventArgs e)
    {
        _log?.Info($"마커 편집 취소: {e.Marker.Title}");
        MarkerEditCancelled?.Invoke(this, e);
    }

    private void OnAdornerCreated(object? sender, AdornerLifecycleEventArgs e)
    {
        _log?.Info($"Adorner 생성: {e.Marker.Title}");
        AdornerCreated?.Invoke(this, e);
    }

    private void OnAdornerRemoved(object? sender, AdornerLifecycleEventArgs e)
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

        var mousePos = e.GetPosition(this);
        var geoPos = FromLocalToLatLng((int)mousePos.X, (int)mousePos.Y);

        _log?.Info($"마우스 위치: 화면({mousePos.X:F2}, {mousePos.Y:F2}) -> 지리({geoPos.Lat:F6}, {geoPos.Lng:F6})");

        // [FP-1] base 호출 전 처리: base.OnMouseLeftButtonDown이 GMap.NET 내부 _core.MouseDown을
        // 기록하여 팬을 Armed 상태로 만든다. 라인 드로잉·이미지 편집이 이벤트를 소비할 경우
        // base를 호출하지 않아 팬 Armed를 방지한다.
        if (IsLineDrawing)
        {
            OnMapClicked?.Invoke(geoPos, mousePos);
            e.Handled = true;
            return;
        }

        if (IsEditMode)
        {
            _log?.Info("편집 모드에서 처리 시작");

            if (HandleImageEdit(mousePos, geoPos, e))
            {
                _log?.Info("이미지 편집 처리 완료 — base 호출 없이 팬 Armed 방지");
                return;
            }
            _log?.Info("이미지 편집 해당 없음");
        }

        // 이미지/라인 편집 소비 없음 → base 호출하여 팬 및 기타 처리 위임
        base.OnMouseLeftButtonDown(e);

        _log?.Info("클릭된 객체 검색 시작");
        var clickedImage = GetImageAtScreen(mousePos);
        var clickedMarker = GetMarkerAtScreen(mousePos);

        _log?.Info($"검색 결과 - 이미지: {clickedImage?.Title ?? "없음"}, 마커: {clickedMarker?.Title ?? "없음"}");

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
        // [FP-2] 이미지 드래그 활성 시 base 호출 차단.
        // base.OnMouseMove가 _core.BeginDrag()를 발동시켜 맵 팬이 이중으로 적용되는 것을 방지.
        // CaptureMouse() 상태이므로 base 건너뜀이 다른 수신자에 영향을 주지 않는다.
        if (IsEditMode && _isDragging && _isImageDrag && _draggedImage != null)
        {
            Point currentPos = e.GetPosition(this);
            double deltaX = currentPos.X - _dragStartPoint.X;
            double deltaY = currentPos.Y - _dragStartPoint.Y;

            if (Math.Abs(deltaX) >= 2 || Math.Abs(deltaY) >= 2)
            {
                ProcessImageDrag(currentPos, deltaX, deltaY);
            }
            return;
        }

        base.OnMouseMove(e);
    }

    /// <summary>
    /// 마우스 버튼 해제
    /// </summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        // [FP-3] 이미지 드래그 완료 시 ResetDragState() 먼저 실행 후 return.
        // 팬이 미Armed 상태(FP-1 효과)이므로 base의 GMap.NET EndDrag 처리가 불필요하고,
        // ReleaseMouseCapture()를 먼저 실행해야 WPF 이벤트 라우팅이 즉시 정상화된다.
        if (_isDragging && _isImageDrag)
        {
            // ★ FR-8 — ResetDragState가 _draggedImage를 null로 만들기 전에 캡처 후 편집완료 발화(DB 영속화)
            var edited = _draggedImage;
            ResetDragState();
            if (edited != null) OnImageEditCompleted?.Invoke(edited);
            _log?.Info("이미지 드래그 완료");
            e.Handled = true;
            return;
        }

        base.OnMouseLeftButtonUp(e);

        if (_isDragging)
        {
            ResetDragState();
            var elapsed = (DateTime.Now - _panStartTime).TotalMilliseconds;
            _log?.Info($"[PAN] ===DRAG-END=== t={DateTime.Now:HH:mm:ss.fff} skippedFrames={_panSkipCount} elapsed={elapsed:F0}ms → TriggerSelectionChange once");

            // OnPositionChanged를 드래그 중 skip했으므로 종료 시점에 한 번 영역 갱신
            TriggerSelectionChange(ViewArea, Zoom, false);
        }
    }

    /// <summary>
    /// 우클릭 처리 — EditMode OFF 시 수동 히트테스트로 컨텍스트 메뉴 트리거
    /// (IsHitTestVisible=false 상태에서 마커 Shape가 이벤트를 받지 못하므로)
    /// </summary>
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);

        if (!IsEditMode)
        {
            var mousePos = e.GetPosition(this);

            // ★ NFR-7 — 마커 우선 else-if 체인 (마커/이미지 이중 컨텍스트 메뉴 방지)
            var clickedMarker = GetMarkerAtScreen(mousePos);
            if (clickedMarker != null)
            {
                OnMarkerRightClicked?.Invoke(clickedMarker);
                e.Handled = true;
                return;
            }

            // ★ FR-9 — 회전 보정된 히트테스트로 이미지 우클릭 감지 → 회전 초기화/입력 메뉴
            var clickedImage = GetImageAtScreen(mousePos);
            if (clickedImage != null)
            {
                OnImageRightClicked?.Invoke(clickedImage);
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// 마우스 캡처 손실 시(창 비활성화/포커스 손실 등) 진행 중 드래그/회전 상태 정리 (NFR-4, S06/S13)
    /// </summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_isDragging || _isImageDrag || _draggedImage != null)
        {
            ResetDragState();
            _log?.Info("OnLostMouseCapture — 진행 중 드래그 상태 정리");
        }
    }

    #endregion
    
    #region Object Detection Methods
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
    private IEditableMarker? GetMarkerAtScreen(Point screenPosition)
    {
        _log?.Info($"GetMarkerAtScreen 호출: 화면위치({screenPosition.X:F2}, {screenPosition.Y:F2})");

        if (Markers == null || !Markers.Any())
            return null;

        var validMarkers = Markers.OfType<IEditableMarker>()
            .Where(m => m != null && !string.IsNullOrEmpty(m.Title)).ToList();

        // 클릭 범위 내 모든 후보 수집
        var candidates = new List<(IEditableMarker marker, double distance, int zIndex, double area)>();

        foreach (var marker in validMarkers)
        {
            try
            {
                if (marker.IsDisposed) continue;
                if (!SetMarkerVisibility(marker)) continue;

                var markerScreenPos = FromLatLngToLocal(marker.Position);
                var markerScreenPoint = new Point(markerScreenPos.X, markerScreenPos.Y);
                var screenDistance = CalculateScreenDistance(screenPosition, markerScreenPoint);

                // 렌더된 화면 크기 — Shape.ActualWidth/Height 우선, 폴백 32px
                var shape = (marker as GMap.NET.WindowsPresentation.GMapMarker)?.Shape as FrameworkElement;
                var renderedW = shape?.ActualWidth is > 0 ? shape.ActualWidth : 32.0;
                var renderedH = shape?.ActualHeight is > 0 ? shape.ActualHeight : 32.0;

                // ★ AABB 히트테스트 (MarkerHitTest_AABB_Fix R-1) — 원형 반경(Math.Max(W,H)/2+8) 대신
                //   마커 Width×Height 사각형으로 판정. 마커 Offset=(-W/2,-H/2)이므로 markerScreenPoint가
                //   시각 중심 → 중심 기준 AABB가 정확. 원형은 라인/비정방형 심볼에서 빈 공간 오선택 발생.
                var halfW = renderedW / 2.0;
                var halfH = renderedH / 2.0;
                var dx = Math.Abs(screenPosition.X - markerScreenPoint.X);
                var dy = Math.Abs(screenPosition.Y - markerScreenPoint.Y);

                if (dx <= halfW && dy <= halfH)
                {
                    var z = shape != null ? System.Windows.Controls.Panel.GetZIndex(shape) : 0;
                    var area = renderedW * renderedH;
                    candidates.Add((marker, screenDistance, z, area));
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"마커 '{marker.Title}' 화면 좌표 계산 실패: {ex.Message}");
            }
        }

        if (candidates.Count == 0)
        {
            _log?.Info("클릭 위치에서 마커를 찾을 수 없음");
            return null;
        }

        // 우선순위: ZIndex 높은 순 → 면적 작은 순 → 거리 가까운 순
        var selected = candidates
            .OrderByDescending(c => c.zIndex)
            .ThenBy(c => c.area)
            .ThenBy(c => c.distance)
            .First();

        _log?.Info($"GetMarkerAtScreen 선택: '{selected.marker.Title}' ZIndex={selected.zIndex} Area={selected.area:F0} Dist={selected.distance:F1}px (후보 {candidates.Count}개)");
        return selected.marker;
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
    /// 특정 위치의 이미지 찾기 (LatLng 기반 — 내부 유틸 용도)
    /// </summary>
    private GMapCustomImage GetImageAt(PointLatLng position)
    {
        return CustomImages.FirstOrDefault(img =>
            img.Visibility && (img.Zoom <= 0 || Zoom >= img.Zoom) && img.Contains(position));
    }

    /// <summary>
    /// 화면 좌표 기반 이미지 히트테스트 — 렌더러와 동일한 FromLatLngToLocal 경로를 사용하여
    /// LatLng 이중 변환 오차 제거. 고줌(18+)에서 정밀도 보장.
    /// </summary>
    private GMapCustomImage? GetImageAtScreen(Point screenPos)
    {
        return CustomImages.FirstOrDefault(img =>
        {
            if (!img.Visibility || (img.Zoom > 0 && Zoom < img.Zoom)) return false;
            if (img.Opacity <= 0) return false;             // ★ FR-10 (AABB PRD R-2 흡수) — 투명 이미지 클릭 차단
            return HitTestImageScreen(img, screenPos);      // ★ NFR-1 (AABB PRD R-3 흡수) — 회전 보정 AABB
        });
    }
    #endregion
    
    #region Marker Adorner Management

    /// <summary>
    /// 마커를 Adorner 시스템에 등록
    /// </summary>
    private void RegisterMarkerForAdorner(IEditableMarker marker)
    {
        try
        {
            // 마커의 UI 컨트롤을 찾아서 등록
            // 실제 구현에서는 마커와 연결된 UI 컨트롤을 찾아야 함
            if (marker != null && AdornerManager != null)
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
    private void UnregisterMarkerFromAdorner(IEditableMarker marker)
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
    #endregion
    
    #region Public Methods - Marker Selection

    /// <summary>
    /// 마커 선택 (Adorner 자동 생성)
    /// </summary>
    /// <param name="marker">선택할 마커</param>
    /// <returns>성공 여부</returns>
    public bool SelectMarker(IEditableMarker marker)
    {
        if (marker == null || AdornerManager == null)
        {
            _log?.Warning("마커 또는 AdornerManager가 null입니다.");
            return false;
        }

        try
        {
            _log?.Info($"마커 선택 시도: {marker.Title}");

            if (marker != null)
            {
                _log?.Info($"마커 컨트롤 찾음: {marker.GetType().Name}");

                // 마커를 선택 상태로 설정
                marker.IsSelected = true;
                var markerControl = FindMarkerControlByMarker(marker);

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

    private IMarkerControl FindMarkerControlByMarker(IEditableMarker marker)
    {
        // 마커 자체가 IMarkerControl을 구현하는 경우 (예: GMapImageMarker)
        if (marker is IMarkerControl markerControl)
        {
            _log?.Info($"마커 자체가 IMarkerControl 구현: {marker.GetType().Name}");
            return markerControl;
        }

        // Visual Tree를 순회하면서 해당 마커와 연결된 컨트롤 찾기
        return FindMarkerControlInVisualTree(this, marker);
    }

    private IMarkerControl FindMarkerControlInVisualTree(DependencyObject parent, IEditableMarker targetMarker)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            // IMarkerControl인지 확인
            if (child is IMarkerControl markerControl &&
                markerControl.EditableMarker == targetMarker)
            {
                return markerControl;
            }

            // 재귀적으로 하위 요소 탐색
            var result = FindMarkerControlInVisualTree(child, targetMarker);
            if (result != null)
                return result;
        }
        return null;
    }

    /// <summary>
    /// 마커 선택 해제 (Adorner 자동 제거)
    /// </summary>
    /// <param name="marker">선택 해제할 마커</param>
    /// <returns>성공 여부</returns>
    public bool DeselectMarker(IEditableMarker marker)
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
            //if (CustomMarkers != null)
            //{
            //    foreach (var marker in CustomMarkers)
            //    {
            //        marker.IsSelected = false;
            //    }
            //}

            if (Markers != null)
            {
                foreach (IEditableMarker marker in Markers)
                {
                    marker.IsSelected = false;
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
        // ★ NFR-3(a) — 이미지 드래그/회전 중에는 줌 차단 (좌표계 변동으로 인한 점프 방지)
        if (_isImageDrag)
        {
            e.Handled = true;
            return;
        }

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
                //foreach (var marker in CustomMarkers)
                //{
                //    SelectMarker(marker);
                //}
                //_log?.Info($"모든 마커 선택 완료: {CustomMarkers.Count}개");

                foreach (IEditableMarker marker in Markers)
                {
                    SelectMarker(marker);
                }
                //_log?.Info($"모든 마커 선택 완료: {Markers.Count}개");
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

        // ★ Move-폴백도 회전 보정 히트테스트 사용 (NFR-1, S12).
        //   기존 Contains(geoPos)는 비회전 AABB라 회전 이미지에서 빈 모서리 오선택 / 시각영역 미선택 발생.
        if (HitTestImageScreen(selectedImage, mousePos))
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

        // ★ 회전 드래그: 이미지 중심/시작각/기준 회전값 캐싱 (FR-5, 절대각 누적 기반)
        if (handle == ResizeHandle.Rotate)
        {
            var imageRect = GetImageScreenRect(image);
            var cx = imageRect.X + imageRect.Width / 2;
            var cy = imageRect.Y + imageRect.Height / 2;
            _rotationCenterScreen = new Point(cx, cy);
            _rotationStartAngle = CalculateRotationAngle(new GPoint((long)cx, (long)cy), mousePos);
            _rotationBaseUserRotation = image.UserRotation;
        }

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

        // ★ 회전 케이스 (FR-4): 절대각 누적. deltaX/deltaY 사용 금지 — 아래 _dragStartPoint 매 프레임
        //   리셋 때문에 누적이 안 되므로, 시작각 캐시(_rotationStartAngle) 기준 절대각으로 계산한다.
        if (_resizeHandle == ResizeHandle.Rotate)
        {
            var cur = CalculateRotationAngle(
                new GPoint((long)_rotationCenterScreen.X, (long)_rotationCenterScreen.Y), currentPos);
            // 드래그 시작 이후 회전량을 최단호(-180,180]로 정규화 (0/360 경계 명시적 처리).
            var deltaAngle = NormalizeAngle(cur - _rotationStartAngle);
            var newAngle = ApplySnapAngle(NormalizeAngle(_rotationBaseUserRotation + deltaAngle));
            _draggedImage.UserRotation = newAngle;
            InvalidateVisual();
            _dragStartPoint = currentPos;
            return; // bounds 변경 없음
        }

        // ★ 회전 상태 Resize 시 화면 델타를 이미지 로컬축으로 역회전 (NFR-2).
        //   Move(평행이동)는 화면 델타 그대로가 맞으므로 제외.
        if (_resizeHandle != ResizeHandle.Move && _draggedImage.EffectiveRotation != 0)
        {
            (deltaX, deltaY) = RotateVector(deltaX, deltaY, -_draggedImage.EffectiveRotation);
        }

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
            default:
                // ★ S07 — 미처리 핸들 회귀 감지 (silent no-op 방지)
                _log?.Warning($"[ProcessImageDrag] 미처리 핸들: {_resizeHandle}");
                break;
        }

        if (newBounds.WidthLng > 0.0001 && newBounds.HeightLat > 0.0001)
        {
            _draggedImage.ImageBounds = newBounds;
            InvalidateVisual();
        }
        // [FP-5] bounds 유효성과 무관하게 항상 갱신.
        // 조건 미충족 프레임에서 갱신이 누락되면 delta가 누적되어 다음 프레임에 점프 현상 발생.
        _dragStartPoint = currentPos;
    }

    #endregion
    
    #region Handle Detection Methods

    /// <summary>
    /// 이미지 화면 사각형(AABB) 계산. bounds → FromLatLngToLocal. (NFR-1 공통 헬퍼)
    /// </summary>
    private Rect GetImageScreenRect(GMapCustomImage image)
    {
        var bounds = image.ImageBounds;
        var topLeft = FromLatLngToLocal(bounds.LocationTopLeft);
        var bottomRight = FromLatLngToLocal(bounds.LocationRightBottom);
        return new Rect(topLeft.X, topLeft.Y,
            bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>
    /// 이미지 중심(화면 좌표) 기준 회전 변환. 렌더·히트·드래그 델타가 공유하는 단일 진실원. (NFR-1)
    /// </summary>
    private RotateTransform GetImageRotateTransform(GMapCustomImage image, Rect imageRect)
    {
        var cx = imageRect.X + imageRect.Width / 2;
        var cy = imageRect.Y + imageRect.Height / 2;
        return new RotateTransform(image.EffectiveRotation, cx, cy);
    }

    /// <summary>
    /// 화면 좌표를 이미지 비회전 로컬 좌표로 역변환. EffectiveRotation==0이면 그대로 반환. (NFR-1)
    /// </summary>
    private Point InverseRotateMouse(GMapCustomImage image, Rect imageRect, Point mousePos)
    {
        if (image.EffectiveRotation == 0) return mousePos;
        var m = GetImageRotateTransform(image, imageRect).Value; // Matrix (회전이므로 항상 가역)
        m.Invert();
        return m.Transform(mousePos);
    }

    /// <summary>
    /// 화면 델타 벡터를 회전 (NFR-2, 평행이동 성분 제외 순수 벡터 회전).
    /// </summary>
    private (double dx, double dy) RotateVector(double dx, double dy, double degrees)
    {
        if (degrees == 0) return (dx, dy);
        var rad = degrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        return (dx * cos - dy * sin, dx * sin + dy * cos);
    }

    /// <summary>
    /// 화면 좌표가 이미지의 (회전 보정된) 사각형 안에 있는지 — 히트테스트 공통 코어. (NFR-1, S12)
    /// </summary>
    private bool HitTestImageScreen(GMapCustomImage image, Point screenPos)
    {
        var imageRect = GetImageScreenRect(image);
        var local = InverseRotateMouse(image, imageRect, screenPos);
        return local.X >= imageRect.Left && local.X <= imageRect.Right &&
               local.Y >= imageRect.Top && local.Y <= imageRect.Bottom;
    }

    /// <summary>
    /// 클릭된 이미지 핸들 감지
    /// </summary>
    private ResizeHandle GetClickedImageHandle(GMapCustomImage image, Point mousePos)
    {
        var imageRect = GetImageScreenRect(image);

        // ★ 회전 역변환 — 렌더러(GetImageRotateTransform)와 동일 좌표계 보장 (NFR-1).
        //   회전 핸들/8핸들 모두 비회전 imageRect 기준 좌표로 그려지므로 마우스를 역회전해 비교한다.
        var local = InverseRotateMouse(image, imageRect, mousePos);

        var handleSize = 8;
        var tolerance = handleSize + 2;

        // ★ 회전 핸들을 8핸들보다 먼저 검사하여 Contains() Move-폴백보다 우선되게 한다 (FR-3, S02 R2).
        var rotateHandle = new Point(imageRect.Left + imageRect.Width / 2,
                                     imageRect.Top - ROTATE_HANDLE_DISTANCE);
        if (Math.Abs(local.X - rotateHandle.X) <= tolerance &&
            Math.Abs(local.Y - rotateHandle.Y) <= tolerance)
        {
            return ResizeHandle.Rotate;
        }

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
            if (Math.Abs(local.X - handlePos.X) <= tolerance &&
                Math.Abs(local.Y - handlePos.Y) <= tolerance)
            {
                return handleType;
            }
        }

        return ResizeHandle.None;
    }

    #endregion
    
    #region Rendering Methods

    /// <summary>
    /// 오버레이 맵 타일 렌더링 (DrawingContext 직접 렌더링)
    /// base.OnRender(타일) 후, 심볼(ItemsPresenter) 전에 호출됨
    /// </summary>
    private void RenderOverlayMapTiles(DrawingContext drawingContext)
    {
        if (OverlayMapCanvas == null) return;

        // ZOrder 순으로 렌더링 (낮은 ZOrder = 먼저 그림 = 아래 레이어)
        foreach (System.Windows.Controls.Canvas childCanvas in
            OverlayMapCanvas.Children.OfType<System.Windows.Controls.Canvas>()
                .OrderBy(c => System.Windows.Controls.Panel.GetZIndex(c)))
        {
            if (childCanvas.Visibility != Visibility.Visible) continue;

            var opacity = childCanvas.Opacity;

            foreach (var img in childCanvas.Children.OfType<System.Windows.Controls.Image>())
            {
                if (img.Source == null) continue;

                var left = System.Windows.Controls.Canvas.GetLeft(img);
                var top = System.Windows.Controls.Canvas.GetTop(img);
                if (double.IsNaN(left) || double.IsNaN(top)) continue;

                var rect = new Rect(left, top, img.Width, img.Height);

                if (opacity < 1.0)
                    drawingContext.PushOpacity(opacity);

                drawingContext.DrawImage(img.Source, rect);

                if (opacity < 1.0)
                    drawingContext.Pop();
            }
        }
    }

    /// <summary>
    /// 이미지 오버레이 렌더링
    /// </summary>
    private void RenderImageOverlays(DrawingContext drawingContext)
    {
        try
        {
            foreach (var customImage in CustomImages.Where(img => img.Visibility && (img.Zoom <= 0 || Zoom >= img.Zoom)))
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

        int pushCount = 0;   // ★ NFR-5 — Push 횟수 추적, finally에서 균형 복원
        try
        {
            var imageRect = GetImageScreenRect(customImage);

            // 회전 처리 (EffectiveRotation = UserRotation + MapCorrectionRotation, NFR-1 단일 진실원)
            if (customImage.EffectiveRotation != 0)
            {
                drawingContext.PushTransform(GetImageRotateTransform(customImage, imageRect));
                pushCount++;
            }

            // 투명도 처리
            if (customImage.Opacity < 1.0)
            {
                drawingContext.PushOpacity(customImage.Opacity);
                pushCount++;
            }

            // 이미지 그리기
            drawingContext.DrawImage(customImage.Img, imageRect);

            // 선택된 이미지 테두리 및 핸들 표시 (PushTransform 범위 내부 → 핸들이 회전 위치에 그려짐)
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
                        new Typeface("Arial"), 12, Brushes.Red, PixelsPerDip);
                    drawingContext.DrawText(nameText, new Point(imageRect.X, imageRect.Y - 15));
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"단일 이미지 렌더링 실패: {ex.Message}");
        }
        finally
        {
            // ★ NFR-5 — 예외 발생 시에도 PushTransform/PushOpacity 스택을 정확히 복원
            for (int i = 0; i < pushCount; i++) drawingContext.Pop();
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

        // ★ 회전 핸들 (FR-2): 상단 중앙에서 위쪽으로 연결선 + 초록 원.
        //   PushTransform(회전) 범위 내부이므로 imageRect 로컬좌표로 그리면
        //   GetClickedImageHandle의 역회전 히트(NFR-1)와 위치가 정확히 일치한다.
        var rotateBrush = Brushes.LimeGreen;
        var cx = imageRect.Left + imageRect.Width / 2;
        var topMid = new Point(cx, imageRect.Top);
        var rotatePt = new Point(cx, imageRect.Top - ROTATE_HANDLE_DISTANCE);
        drawingContext.DrawLine(handlePen, topMid, rotatePt);
        drawingContext.DrawEllipse(rotateBrush, handlePen, rotatePt, handleSize / 2.0, handleSize / 2.0);
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
                new Typeface("Arial"), 14, Brushes.Black, PixelsPerDip);

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
                FlowDirection.LeftToRight, new Typeface("Arial"), 12, Brushes.Black, PixelsPerDip);

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
            //foreach (GMapMarker marker in CustomMarkers)
            //{
            //    marker.ForceUpdateLocalPosition(this);
            //}
            foreach (GMapMarker marker in Markers)
            {
                marker.ForceUpdateLocalPosition(this);
            }

            // 이미지 오버레이 회전 보정 (FR-7, NFR-6)
            // ★ Rotation(=UserRotation) 덮어쓰기 금지 — 사용자 편집 회전값 보존.
            //   맵 보정값만 MapCorrectionRotation에 반영하고, 렌더는 EffectiveRotation(합산)을 사용한다.
            foreach (var customImage in CustomImages)
            {
                customImage.MapCorrectionRotation = -MapRotation;
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

        // 마커 Shape IsHitTestVisible 토글 — EditMode OFF 시 맵 패닝/줌 투과
        foreach (var marker in Markers)
        {
            if (marker is GMapMarker gMarker && gMarker.Shape is UIElement shape)
            {
                shape.IsHitTestVisible = enabled;
            }
        }

        if (!IsEditMode)
        {
            // ★ NFR-4 (S06) — 진행 중 드래그/회전 강제 종료 (CaptureMouse 해제, 입력 락업 방지)
            if (_isDragging || _isImageDrag || _draggedImage != null)
            {
                ResetDragState();
                _log?.Info("SetEditMode(false) — 진행 중 드래그 강제 종료(CaptureMouse 해제)");
            }

            // 편집 모드 해제 시 모든 선택 해제
            foreach (var img in CustomImages) img.IsSelected = false;
            //foreach (var marker in CustomMarkers) marker.IsSelected = false;
            foreach (IEditableMarker marker in Markers) marker.IsSelected = false;

            // 모든 Adorner 제거
            AdornerManager?.DeselectAllMarkers(this);

            ShowImageBounds = false;
            InvalidateVisual();
        }

        _log?.Info($"편집 모드: {(enabled ? "활성화" : "비활성화")}, 마커 HitTest={enabled}");
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
        //var markers = CustomMarkers.Where(m =>
        //    Math.Abs(m.Position.Lat - position.Lat) < 0.0001 &&
        //    Math.Abs(m.Position.Lng - position.Lng) < 0.0001).ToList();

        var markers = Markers.Where(m =>
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
        // ★ 회전 드래그 전용 상태 초기화 (NFR-4 — 누수 방지)
        _rotationCenterScreen = default;
        _rotationStartAngle = 0;
        _rotationBaseUserRotation = 0;
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

        // [FP-4] drag = Math.Max(|deltaX|, |deltaY|) 이므로 지배적 축 크기를 scale에 사용한다.
        // OR 조건은 비지배적 축의 방향이 expand를 결정해 드래그 방향과 반대로 동작하는 버그를 유발.
        // 지배적 축의 방향으로 expand를 판정하여 drag 크기와 방향을 일치시킨다.
        bool expand = corner switch
        {
            ResizeHandle.TopLeft =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX < 0 : deltaY < 0,
            ResizeHandle.TopRight =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX > 0 : deltaY < 0,
            ResizeHandle.BottomLeft =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX < 0 : deltaY > 0,
            ResizeHandle.BottomRight =>
                Math.Abs(deltaX) >= Math.Abs(deltaY) ? deltaX > 0 : deltaY > 0,
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

        // [FP-6] (long) 직접 캐스트는 소수점 이하를 항상 버려 천천히 드래그 시 stutter 유발.
        // Math.Round로 교체하여 오차를 ±0.5px로 분산한다.
        if (adjustLeft) topLeft.X += (long)Math.Round(deltaX);
        if (adjustTop) topLeft.Y += (long)Math.Round(deltaY);
        if (adjustRight) bottomRight.X += (long)Math.Round(deltaX);
        if (adjustBottom) bottomRight.Y += (long)Math.Round(deltaY);

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
    /// 격자 스냅 활성화 DependencyProperty
    /// </summary>
    public static readonly DependencyProperty IsSnapToGridEnabledProperty =
        DependencyProperty.Register(nameof(IsSnapToGridEnabled), typeof(bool), typeof(GMapCustomControl),
            new PropertyMetadata(false, OnIsSnapToGridEnabledChanged));

    public bool IsSnapToGridEnabled
    {
        get => (bool)GetValue(IsSnapToGridEnabledProperty);
        set => SetValue(IsSnapToGridEnabledProperty, value);
    }

    private static void OnIsSnapToGridEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
            control.InvalidateVisual();
    }

    /// <summary>
    /// 격자 크기(px) DependencyProperty — 기본 32px, 8px 하한은 SnapGridOverlayService에서 적용
    /// </summary>
    public static readonly DependencyProperty GridSizePxProperty =
        DependencyProperty.Register(nameof(GridSizePx), typeof(double), typeof(GMapCustomControl),
            new PropertyMetadata(32.0, OnGridSizePxChanged));

    public double GridSizePx
    {
        get => (double)GetValue(GridSizePxProperty);
        set => SetValue(GridSizePxProperty, value);
    }

    private static void OnGridSizePxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GMapCustomControl control)
        {
            control._snapGridOverlay?.InvalidateCache();
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
    //public ObservableCollection<IEditableMarker> CustomMarkers { get; private set; }

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
    #endregion
    
    #region Enums

    public enum ResizeHandle
    {
        None, TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleRight,
        BottomLeft, BottomCenter, BottomRight,
        Move, Rotate
    }

    #endregion
    
    #region Private Fields

    private IEventAggregator? _eventAggregator;
    private ILogService? _log;
    private MGRSGridOverlayService _mgrsOverlay;
    private SnapGridOverlayService _snapGridOverlay = new();

    // 드래그 진단 필드 — RDP 패닝 버그 분석용
    private bool _prevDragging;
    private int _panSkipCount;
    private DateTime _panStartTime;

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

    // 회전 드래그 전용 상태 (FR-5) — 절대각 누적용
    private Point _rotationCenterScreen;        // 이미지 중심 화면 좌표 (드래그 시작 시 캐싱)
    private double _rotationStartAngle;         // 드래그 시작 시 atan2 절대각
    private double _rotationBaseUserRotation;   // 드래그 시작 시 UserRotation 스냅샷
    private const double ROTATE_HANDLE_DISTANCE = 30; // 상단 중앙에서 위쪽 오프셋(px)
    #endregion
}