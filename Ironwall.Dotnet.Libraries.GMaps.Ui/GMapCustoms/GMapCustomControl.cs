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
        // 측정 툴(길이/넓이) 컨트롤러 초기화
        InitializeMeasure();

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

    #region Measure Tools (길이/넓이 측정 — Measure_Tools)

    private Services.MeasureController? _measureController;

    /// <summary>측정 리드아웃 변경(HUD 바인딩용) — VM이 구독.</summary>
    public event System.Action<Services.MeasureReadout>? MeasureReadoutChanged;
    /// <summary>측정 모드 종료 통지 — VM이 토글 해제·배너 숨김.</summary>
    public event System.Action? MeasureStopped;

    /// <summary>측정 모드 활성 여부.</summary>
    public bool IsMeasuring => _measureController?.IsActive ?? false;
    /// <summary>현재 측정 종류.</summary>
    public Adorners.MeasureKind ActiveMeasureKind => _measureController?.Mode ?? Adorners.MeasureKind.Length;

    private void InitializeMeasure()
    {
        _measureController = new Services.MeasureController(this, _log);
        _measureController.ReadoutChanged += r => MeasureReadoutChanged?.Invoke(r);
    }

    /// <summary>측정 시작(또는 종류 전환). 클릭 라우팅은 OnMouseLeftButtonDown의 IsMeasuring 분기.</summary>
    public void StartMeasure(Adorners.MeasureKind kind)
    {
        _measureController?.Start(kind);
        Focus();   // 키 수신 보조(윈도우 훅은 VM이 별도 설치)
    }

    /// <summary>측정 종료 — 어도너 해제 + MeasureStopped 통지.</summary>
    public void StopMeasure()
    {
        _measureController?.Stop();
        MeasureStopped?.Invoke();
    }

    /// <summary>측정 완료(더블클릭/Enter) — 결과 고정. 유효점 부족 시 false.</summary>
    public bool FinishMeasure() => _measureController?.Finish() ?? false;

    /// <summary>마지막 점 제거(Backspace/Ctrl+Z).</summary>
    public void MeasureUndo() => _measureController?.Undo();

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

    /// <summary>
    /// 카메라 "특정 위치 확인" 타겟 조준 모드 여부.
    /// ON이면 좌클릭을 가로채 <see cref="TargetAimClicked"/>로 전달(맵 팬·마커선택·이미지편집·더블클릭 모두 차단).
    /// 라인드로잉/편집 모드와 상호배타(ViewModel이 진입 시 강제 종료).
    /// </summary>
    private bool _isTargetAimMode;
    public bool IsTargetAimMode
    {
        get => _isTargetAimMode;
        set
        {
            if (_isTargetAimMode == value) return;
            _isTargetAimMode = value;
            // 반경 오버레이 adorner attach/detach (렌더는 AimOverlayAdorner로 이전 — 심볼·이미지 위, FR-AIM-01)
            if (value) ShowAimOverlay(); else HideAimOverlay();
        }
    }

    /// <summary>타겟 조준 반경 원의 중심(카메라 위치, WGS84). 타겟 모드 진입 시 설정, 종료 시 null.</summary>
    public PointLatLng? AimOverlayCenter { get; set; }

    /// <summary>타겟 조준 반경(m). 화면 원 크기 산출에 사용(지오→픽셀, 줌마다 재계산).</summary>
    public double AimOverlayRadiusMeters { get; set; }

    /// <summary>심볼 배치 모드 — ON이면 좌클릭을 가로채 <see cref="SymbolPlacementClicked"/>로 전달(그 위치에 심볼 추가).
    /// ViewModel(추가 버튼)이 진입/종료. 타겟조준·라인드로잉과 동급의 base-전 가로채기 모드.</summary>
    public bool IsSymbolPlacementMode { get; set; }

    #endregion

    #region Integration Events
    /// <summary>
    /// 지도 클릭 이벤트 - ViewModel에 클릭 위치 전달
    /// </summary>
    public event Action<PointLatLng, Point> OnMapClicked;

    /// <summary>
    /// 타겟 조준 모드 좌클릭 - 클릭 지점 좌표를 ViewModel에 전달(카메라 회전요청 발행용).
    /// </summary>
    public event Action<PointLatLng, Point>? TargetAimClicked;

    /// <summary>심볼 배치 모드 좌클릭 - 클릭 지점 좌표를 ViewModel에 전달(그 위치에 심볼 추가).</summary>
    public event Action<PointLatLng, Point>? SymbolPlacementClicked;

    /// <summary>홈 위치 배치 모드 — ON이면 좌클릭을 가로채 <see cref="HomePlacementClicked"/>로 전달(그 위치를 홈으로).
    /// 심볼배치와 동급 base-전 가로채기. ViewModel(홈 설정 버튼)이 진입/종료. (PRD GMap_Zoom_Anchor_Home FR-H2)</summary>
    public bool IsHomePlacementMode { get; set; }

    /// <summary>홈 배치 모드 좌클릭 - 클릭 지점 좌표를 ViewModel에 전달(그 위치를 홈으로).</summary>
    public event Action<PointLatLng, Point>? HomePlacementClicked;

    /// <summary>
    /// 마커 클릭 이벤트 - ViewModel에 클릭된 마커 전달
    /// </summary>
    public event Action<IEditableMarker> OnMarkerClicked;

    /// <summary>
    /// 마커 우클릭 이벤트 - ViewModel에 우클릭된 마커 전달 (컨텍스트 메뉴용)
    /// </summary>
    public event Action<IEditableMarker>? OnMarkerRightClicked;

    /// <summary>
    /// 마커 더블클릭 이벤트 - ViewModel에 더블클릭된 마커 전달 (RTSP 팝업 등). 편집/일반 모드 모두.
    /// </summary>
    public event Action<IEditableMarker>? OnMarkerDoubleClicked;

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
    public Action<GMapCustomImage, GMap.NET.RectLatLng, double>? OnImageEditCompleted;   // (편집됨, before Bounds, before Rotation) — Undo before-state 포함(D1)

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

            // 측정 모드와 상호배제 — 라인 드로잉 시작 시 측정 종료
            if (IsMeasuring) StopMeasure();

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

        // ★ T4 — 편집모드에서 마커 Shape(IsHitTestVisible=true) 위에 마우스가 있으면 GMapControl.IsMouseDirectlyOver=false가
        //   되어 base.OnMouseWheel의 줌 조건((IsMouseDirectlyOver || IgnoreMarkerOnMouseWheel))이 막힌다.
        //   마커/오브젝트 위에서도 휠 줌이 동작하도록 마커 무시 옵션을 켠다(WinForms 데모 표준 설정).
        IgnoreMarkerOnMouseWheel = true;

        // ★ 디지털 줌(SE-1/NFR-4): 리사이즈 시 ScaleTransform 중심(ActualWidth/2)이 바뀌므로 재적용.
        this.SizeChanged += (_, __) => { if (DigitalZoomLevel > 0) ApplyDigitalZoomTransform(); RecomputeAnchorViewportBounds(); };   // [MapAnchor] 크기 변경 시 inset 라이브 재계산(FR-4)

        base.OnInitialized(e);

        // [임시 진단 제거] 렌더 tier 감지 로그 (RDP 조사 종료). 필요 시(현장 SW렌더링 판별) 주석 해제.
        //var tier = System.Windows.Media.RenderCapability.Tier >> 16;
        //if (tier == 0)
        //    _log?.Warning("[GMapCustomControl] 소프트웨어 렌더링 모드 감지 (Tier=0). RDP/가상화 환경 가능성. 패닝 성능 저하 예상.");
        //else if (tier == 1)
        //    _log?.Info("[GMapCustomControl] 부분 하드웨어 가속 모드 (Tier=1).");
        //else
        //    _log?.Info($"[GMapCustomControl] 하드웨어 가속 렌더링 (Tier={tier}).");
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

        // 카메라 "특정 위치 확인" 반경 오버레이는 AimOverlayAdorner(맵 AdornerLayer)로 이전 —
        //   심볼·이미지 위 표시 + 등장/리플 애니메이션(FR-AIM-01~03). OnRender 직접 렌더 제거.

        // Shift+드래그 러버밴드 마퀴는 RubberBandAdorner(맵 AdornerLayer, 이미지·마커 위)로 이전 —
        //   OnRender 직접 렌더는 자식 이미지마커(GMapImageMarker)에 가려짐. OnRender 마퀴 제거.
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

        // [MapAnchor] 줌 변경 시 유효 뷰포트가 달라지므로 inset(BoundsOfMap) 라이브 재계산(FR-4).
        RecomputeAnchorViewportBounds();

        try
        {
            _log?.Info($"줌 변경됨: {Zoom}");
            //LogOverlayDesyncDiag("ZOOM");   // [임시 진단 제거]

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
        // [임시 진단 제거] 드래그 시작 계측 + 로그 (RDP desync 조사 종료)
        //var now = DateTime.Now;
        //if (IsDragging && !_prevDragging)
        //{
        //    _panStartTime = now;
        //    _panSkipCount = 0;
        //    _log?.Info($"[PAN] ===DRAG-START=== t={now:HH:mm:ss.fff} lat={point.Lat:F5} lng={point.Lng:F5} zoom={Zoom}");
        //    LogOverlayDesyncDiag("DRAG-START");
        //}
        //_prevDragging = IsDragging;

        // 드래그 중에는 TriggerSelectionChange → OnAreaChange → UpdateMarkersVisibilityByZoom + InvalidateVisual
        // 체인이 매 프레임 실행되어 심볼이 타일과 어긋나는 버그 유발 (RDP 환경 특히 심각).
        // 드래그 완료 후(IsDragging=false)에만 영역 변경 처리를 허용한다.
        if (IsDragging)
        {
            return;
        }

        // [MapAnchor] 비드래그 위치 변경(프로그램적 이동 등)이 앵커 구역 밖이면 안으로 되돌림.
        //   드래그 중 뷰포트-가두기는 벤더가 BoundsOfMap(=inset)의 Contains 스킵으로 직접 강제한다(RenderOffset 미이동, GMapControl:2090).
        if (!_isClampingToBounds && ClampCenterToBounds(point)) return;

        try
        {
            var viewArea = ViewArea;
            var zoom = Zoom;
            // [임시 진단 제거] 팬 실행 계측
            //_log?.Info($"[PAN] OnPositionChanged EXEC — drag=false lat={point.Lat:F5} lng={point.Lng:F5} t={now:HH:mm:ss.fff}");
            //LogOverlayDesyncDiag("DRAG-END/EXEC");
            TriggerSelectionChange(viewArea, zoom, false);
        }
        catch (Exception ex)
        {
            _log?.Error($"위치 변경 처리 실패: {ex.Message}");
        }
    }

    // [MapAnchor] 패닝 구역 강제 클램프(재진입 방지 플래그 + 로직) — 벤더 BoundsOfMap 미enforce 보완. (FR-B1)
    private bool _isClampingToBounds;

    /// <summary>지도 중심을 BoundsOfMap(= 라이브 뷰포트 inset 사각형) 안으로 클램프. BoundsOfMap이 이미 inset이므로
    /// 단순 중심 클램프가 곧 뷰포트-가두기다. 줌 변경 후 중심을 새 inset 안으로 넣는 nudge에도 사용. 되돌렸으면 true.</summary>
    private bool ClampCenterToBounds(PointLatLng point)
    {
        var bounds = BoundsOfMap;
        if (bounds == null) return false;
        var r = bounds.Value;
        double west = r.Lng, east = r.Lng + r.WidthLng;     // RectLatLng: Lng=west(left), +WidthLng=east
        double north = r.Lat, south = r.Lat - r.HeightLat;  //             Lat=north(top),  -HeightLat=south
        if (east <= west || north <= south) return false;   // 퇴화 구역 방어

        double lng = Math.Clamp(point.Lng, west, east);
        double lat = Math.Clamp(point.Lat, south, north);
        if (Math.Abs(lng - point.Lng) < 1e-7 && Math.Abs(lat - point.Lat) < 1e-7)
            return false;                                    // 이미 안쪽 — 클램프 불필요(1e-7≈1cm, 부동소수 오차 흡수)

        _isClampingToBounds = true;
        try { Position = new PointLatLng(lat, lng); }        // 중심을 inset 경계 안으로 (재진입은 위 가드로 스킵)
        finally { _isClampingToBounds = false; }
        return true;
    }

    // [MapAnchor] 앵커 원본 사이트 사각형(뷰포트 inset의 원천). null=앵커 비활성.
    private RectLatLng? _anchorSiteRect;

    // [Rotation V-06 옵션C] 현재 앵커의 회전 허용 모드 — SetAnchorSite에서 세팅.
    private bool _anchorAllowsRotation;

    /// <summary>앵커 사이트 사각형 설정(활성) 또는 해제(null). 즉시 현재 뷰포트로 inset을 계산해 BoundsOfMap에 반영.
    /// MapViewModel.ApplyMapAnchor에서 호출.
    /// [FR-02 원자 전이 + V-06 옵션C] allowRotation=false(기본): 정북 강제 → verify 0 → 활성,
    /// 이후 SSOT가 회전 차단(현행 A모드). allowRotation=true: 현재 회전 유지한 채 활성(B모드) —
    /// 가두기 inset은 회전 화면의 외접 bbox(FR-08 4코너 ViewArea)로 계산되고, 회전 변경 시
    /// UpdateOverlaysAfterRotation이 inset을 라이브 재계산해 보장 유지.</summary>
    public void SetAnchorSite(RectLatLng? site, bool allowRotation = false)
    {
        if (site != null && !allowRotation && Math.Abs(Bearing) > (float)Utils.RotationMath.Epsilon)
        {
            // 잠근 사이트 = 정립 표시 정책(A모드) — 앵커 걸기 전에 정북 강제(0은 항상 허용 경로)
            SetMapRotation(0);
            if (Math.Abs(Bearing) > (float)Utils.RotationMath.Epsilon)
            {
                // [사용자 버그 2026-07-28 "앵커도 풀려버림"] 종전엔 여기서 활성을 '중단(return)'해
                // 회전만 풀리고 앵커는 안 걸리는 어중간 상태가 됐다(DP가 이미 0인데 Bearing만 남은
                // desync면 DP 콜백이 안 돌아 리셋 무시). → 중단 대신 벤더 Bearing 직접 정북 강제
                // 폴백으로 항상 활성 완료(앵커가 어중간하게 풀리는 실패 모드 제거).
                Bearing = 0f;
                UpdateOverlaysAfterRotation();
                _log?.Warning($"앵커 정북 강제 폴백: DP 미변경 desync 경로 — Bearing 직접 0 적용 후 활성 계속");
            }
        }
        _anchorAllowsRotation = allowRotation;
        _anchorSiteRect = site;
        RecomputeAnchorViewportBounds();
    }

    /// <summary>_anchorSiteRect + 현재 뷰포트(디지털줌 보정)로 inset 사각형을 계산해 BoundsOfMap에 설정하고
    /// 중심을 그 안으로 되돌린다(nudge). 줌/디지털줌/크기 변경 시 라이브 호출. 앵커 비활성이면 BoundsOfMap 해제.
    /// BoundsOfMap=inset이므로 벤더의 드래그 Contains 스킵(GMapControl:2090)이 곧 뷰포트-가두기가 된다.</summary>
    private void RecomputeAnchorViewportBounds()
    {
        if (_isClampingToBounds) return;
        var site = _anchorSiteRect;
        if (site == null) { BoundsOfMap = null; return; }
        var r = site.Value;
        double west = r.Lng, east = r.Lng + r.WidthLng;
        double north = r.Lat, south = r.Lat - r.HeightLat;
        if (east <= west || north <= south) { BoundsOfMap = null; return; }

        var view = ViewArea;
        var (n, s, e, w) = Helpers.AnchorViewportClamp.InsetBounds(
            north, south, east, west, view.WidthLng, view.HeightLat, DigitalZoomScale);
        BoundsOfMap = RectLatLng.FromLTRB(w, n, e, s);   // FromLTRB(leftLng, topLat, rightLng, bottomLat)
        ClampCenterToBounds(Position);                   // 중심을 새 inset 안으로 nudge
        _log?.Info($"[MapAnchor] inset 재계산: view=({view.WidthLng:F6}×{view.HeightLat:F6}) dz={DigitalZoomScale:F2} bounds={BoundsOfMap}");
    }

    /* [임시 진단 제거] RDP 오버레이 desync 확정 계측 — 현장 검증 완료(canvasSame/transformSame=True, err 약 0). 필요 시 이 블록주석 해제.
    /// <summary>[임시 진단] RDP 오버레이 desync 확정용.
    /// 기대 화면좌표(현재 투영 + Marker.Offset)와 실제 ItemContainer 화면좌표를 비교하고,
    /// 실제 ItemsHost Canvas와 GMap이 캐시한 MapCanvas의 identity/transform/source 연결을 기록한다.</summary>
    private void LogOverlayDesyncDiag(string tag)
    {
        try
        {
            int tier = RenderCapability.Tier >> 16;
            var m = Markers?.FirstOrDefault(x => x is IEditableMarker) ?? Markers?.FirstOrDefault();
            if (m == null)
            {
                _log?.Info($"[DESYNC-DIAG:{tag}] markers=0 center={Position.Lat:F6},{Position.Lng:F6} zoom={Zoom} drag={IsDragging} tier={tier}");
                return;
            }
            var projected = FromLatLngToLocal(m.Position);
            double expectedX = projected.X + m.Offset.X;
            double expectedY = projected.Y + m.Offset.Y;

            // Shape의 직계 부모는 보통 ContentPresenter이므로 Canvas가 나올 때까지 조상으로 올라간다.
            System.Windows.Controls.Canvas? actualCanvas = null;
            FrameworkElement? itemContainer = null;
            DependencyObject? current = m.Shape;
            while (current != null && !ReferenceEquals(current, this))
            {
                var parent = VisualTreeHelper.GetParent(current);
                if (parent is System.Windows.Controls.Canvas canvas)
                {
                    actualCanvas = canvas;
                    itemContainer = current as FrameworkElement;
                    break;
                }
                current = parent;
            }
            // MapCanvas getter는 GMap이 최초 탐색 후 캐시한 Canvas를 반환한다.
            var cachedCanvas = MapCanvas;
            var mapSource = PresentationSource.FromVisual(this);
            var cachedSource = cachedCanvas == null ? null : PresentationSource.FromVisual(cachedCanvas);
            var actualSource = actualCanvas == null ? null : PresentationSource.FromVisual(actualCanvas);
            bool canvasSame = actualCanvas != null && ReferenceEquals(actualCanvas, cachedCanvas);
            bool transformSame = actualCanvas != null && cachedCanvas != null &&
                                 ReferenceEquals(actualCanvas.RenderTransform, cachedCanvas.RenderTransform);
            bool cachedSourceSame = cachedSource != null && ReferenceEquals(cachedSource, mapSource);
            bool actualSourceSame = actualSource != null && ReferenceEquals(actualSource, mapSource);

            var cachedMatrix = cachedCanvas?.RenderTransform?.Value ?? Matrix.Identity;
            var actualMatrix = actualCanvas?.RenderTransform?.Value ?? Matrix.Identity;
            string actual = "N/A";
            try
            {
                if (itemContainer != null && itemContainer.IsLoaded)
                {
                    var screen = itemContainer.TransformToAncestor(this).Transform(new Point(0, 0));
                    actual = $"({screen.X:F1},{screen.Y:F1}) err=({screen.X - expectedX:F1},{screen.Y - expectedY:F1})";
                }
            }
            catch (Exception ex) { actual = $"ERR:{ex.GetType().Name}"; }
            _log?.Info($"[DESYNC-DIAG:{tag}] center={Position.Lat:F6},{Position.Lng:F6} zoom={Zoom} drag={IsDragging} tier={tier} dzl={DigitalZoomLevel} " +
                       $"markerPos={m.Position.Lat:F6},{m.Position.Lng:F6} offset=({m.Offset.X:F1},{m.Offset.Y:F1}) " +
                       $"local=({m.LocalPositionX},{m.LocalPositionY}) expected=({expectedX:F1},{expectedY:F1}) actual={actual} " +
                       $"canvasSame={canvasSame} transformSame={transformSame} cachedSourceSame={cachedSourceSame} actualSourceSame={actualSourceSame} " +
                       $"cachedT=({cachedMatrix.OffsetX:F1},{cachedMatrix.OffsetY:F1}) actualT=({actualMatrix.OffsetX:F1},{actualMatrix.OffsetY:F1})");
        }
        catch (Exception ex) { _log?.Warning($"[DESYNC-DIAG:{tag}] {ex.Message}"); }
    }
    */

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
    /// 단일 마커의 유효 가시성을 현재 줌/레이어 기준으로 즉시 재계산 + 리렌더.
    /// 속성창에서 "최소 줌"(marker.Zoom)/레이어토글(IsLayerEnabled) 편집 시 호출 — 팬/줌 전에도 즉시 반영.
    /// 게이트 술어는 <see cref="SetMarkerVisibility"/> 단일원천을 재사용(드리프트 방지).
    /// </summary>
    public void RefreshMarkerVisibility(IEditableMarker marker)
    {
        if (marker == null) return;
        marker.IsVisible = SetMarkerVisibility(marker);
        InvalidateVisual();
    }
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
                //_log?.Info($"Markers 최종 개수: {Markers.Count}");
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

    /// <summary>마커 더블클릭을 ViewModel로 전달(편집모드=자식 Shape 경로, 일반모드=OnMouseDoubleClick 경로 공용 진입점).</summary>
    public void TriggerMarkerDoubleClicked(GMapMarker marker)
    {
        try
        {
            if (marker is IEditableMarker editableMarker)
                OnMarkerDoubleClicked?.Invoke(editableMarker);
        }
        catch (Exception ex)
        {
            _log?.Error($"TriggerMarkerDoubleClicked 실패: {ex.Message}");
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
        //_log?.Info($"Adorner 생성: {e.Marker.Title}");
        AdornerCreated?.Invoke(this, e);
    }

    private void OnAdornerRemoved(object? sender, AdornerLifecycleEventArgs e)
    {
        //_log?.Info($"Adorner 제거: {e.Marker.Title}");
        AdornerRemoved?.Invoke(this, e);
    }

    #endregion
    
    #region Mouse Input Handling

    // 마커 더블클릭 감지(컨트롤 레벨 — 편집/일반 모드 공통, 자식 Shape 이벤트 비의존)
    private const int MarkerDoubleClickIntervalMs = 500;
    private DateTime _lastMarkerClickTime = DateTime.MinValue;
    private IEditableMarker? _lastClickedMarkerForDbl;

    /// <summary>
    /// 마우스 왼쪽 버튼 클릭
    /// </summary>
    // ─── Shift+드래그 러버밴드 영역 다중선택 (GMap_RubberBand_MultiSelect FR-MS-01/02) ───
    private bool _isRubberBanding;
    private Point? _rubberStart;
    private Point? _rubberCurrent;
    // [앵커 그리기] 사이트 고정 영역을 지도에서 드래그로 그리는 모드 — 러버밴드 마퀴 재사용, 릴리스 시 NW/SE 통지. (FR-B3)
    private bool _rubberForAnchor;
    /// <summary>앵커(사이트 고정) 영역 그리기 모드 — true면 좌드래그가 러버밴드로 구역을 그린다.</summary>
    public bool IsAnchorDrawMode { get; set; }
    /// <summary>앵커 영역 드래그 완료 — (NW, SE) 지리좌표 통지. VM이 구역 입력을 채운다.</summary>
    public event System.Action<PointLatLng, PointLatLng>? AnchorAreaDrawn;
    private static readonly Pen _rubberPen = CreateDashedPen(Color.FromArgb(210, 0, 170, 255), 1.5d);
    private static readonly Brush _rubberFill = CreateFrozenBrush(Color.FromArgb(40, 0, 170, 255));
    private static Brush CreateFrozenBrush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    /// <summary>Shift+드래그 선택 시작 — VM/서비스가 이전 그룹·단일선택 clear.</summary>
    public event System.Action? RubberBandStarted;
    /// <summary>Shift+드래그 릴리스 — 사각형 내 편집가능 마커(잠금 포함, FR-MS-07) 집합 통지. 비어도 발화(그룹 해제).</summary>
    public event System.Action<IReadOnlyList<IEditableMarker>>? MarkersRubberBandSelected;

    /// <summary>Ctrl+클릭 — 해당 심볼/이미지마커를 그룹 선택에 토글(추가/해제). VM 처리.</summary>
    public event System.Action<IEditableMarker>? MarkerToggleRequested;

    /// <summary>화면 사각형과 교차하는 편집가능 마커 목록(가시·비잠금·비이미지 마커만). AABB 교차(비회전 근사).</summary>
    internal IReadOnlyList<IEditableMarker> GetMarkersInRect(Rect screenRect)
    {
        var result = new List<IEditableMarker>();
        if (Markers == null) return result;
        foreach (var marker in Markers.OfType<IEditableMarker>())
        {
            try
            {
                if (marker.IsDisposed) continue;
                if (marker.IsLocked) continue;                // 잠금 심볼 제외(M1 — 사용자 요청: 그룹 대상서 배제)
                if (!SetMarkerVisibility(marker)) continue;   // 가시(레이어/줌) 마커만
                var shape = (marker as GMap.NET.WindowsPresentation.GMapMarker)?.Shape as FrameworkElement;
                if (shape is GMapMarkerImageControl) continue;   // 오버레이 이미지 마커 제외(M1)
                var sp = FromLatLngToLocal(marker.Position);
                double w = shape?.ActualWidth is > 0 ? shape.ActualWidth : 32.0;
                double h = shape?.ActualHeight is > 0 ? shape.ActualHeight : 32.0;
                var mrect = new Rect(sp.X - w / 2.0, sp.Y - h / 2.0, w, h);
                if (screenRect.IntersectsWith(mrect)) result.Add(marker);
            }
            catch (Exception ex) { _log?.Error($"GetMarkersInRect 실패 '{marker.Title}': {ex.Message}"); }
        }
        return result;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        //_log?.Info("=== GMapCustomControl.OnMouseLeftButtonDown 시작 ===");
        //_log?.Info($"편집 모드: {IsEditMode}");

        var mousePos = e.GetPosition(this);
        var geoPos = FromLocalToLatLng((int)mousePos.X, (int)mousePos.Y);

        //_log?.Info($"마우스 위치: 화면({mousePos.X:F2}, {mousePos.Y:F2}) -> 지리({geoPos.Lat:F6}, {geoPos.Lng:F6})");

        // [FP-1] base 호출 전 처리: base.OnMouseLeftButtonDown이 GMap.NET 내부 _core.MouseDown을
        // 기록하여 팬을 Armed 상태로 만든다. 타겟 조준·라인 드로잉·이미지 편집이 이벤트를 소비할 경우
        // base를 호출하지 않아 팬 Armed를 방지한다.

        // [Camera Aim] 타겟 조준 모드 — 라인드로잉과 동급 위치, base 호출 전 가로채기
        // (팬 Armed·마커 히트테스트·이미지 편집·더블클릭 500ms 윈도우 모두 차단)
        if (IsTargetAimMode)
        {
            TargetAimClicked?.Invoke(geoPos, mousePos);
            e.Handled = true;
            return;
        }

        // [심볼 배치] 추가 버튼으로 진입한 배치 모드 — 클릭 위치에 심볼 추가(타겟조준과 동급, base 전 가로채기).
        if (IsSymbolPlacementMode)
        {
            SymbolPlacementClicked?.Invoke(geoPos, mousePos);
            e.Handled = true;
            return;
        }

        // [홈 배치] 홈 설정 버튼으로 진입한 배치 모드 — 클릭 위치를 홈으로(심볼배치와 동급). (FR-H2)
        if (IsHomePlacementMode)
        {
            HomePlacementClicked?.Invoke(geoPos, mousePos);
            e.Handled = true;
            return;
        }

        // [앵커 그리기] 사이트 고정 영역 드래그 시작 — 러버밴드 마퀴 재사용(base 전 가로채기 = 팬 미Armed). (FR-B3)
        if (IsAnchorDrawMode)
        {
            _rubberForAnchor = true;
            _isRubberBanding = true;
            _rubberStart = mousePos;
            _rubberCurrent = mousePos;
            ShowRubberBand();
            UpdateRubberBand();
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // [측정] 길이/넓이 측정 — base 전 가로채기(팬 미Armed). 더블클릭=완료, 단일=점 추가.
        if (IsMeasuring)
        {
            if (e.ClickCount >= 2) _measureController!.Finish();
            else _measureController!.AddPoint(geoPos);
            e.Handled = true;
            return;
        }

        if (IsLineDrawing)
        {
            OnMapClicked?.Invoke(geoPos, mousePos);
            e.Handled = true;
            return;
        }

        // [Rubber-band] 편집 모드 Shift+좌드래그 = 영역 다중선택 시작. base 전 가로채기 = 팬 미Armed(불변식#5). (FR-MS-01)
        if (IsEditMode && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            AdornerManager?.DeselectAllMarkers(this);   // 그룹 진입 = 기존 단일 adorner 억제(FR-MS-08)
            RubberBandStarted?.Invoke();
            _isRubberBanding = true;
            _rubberStart = mousePos;
            _rubberCurrent = mousePos;
            ShowRubberBand();       // 마퀴는 어도너(이미지·마커 위)로 렌더 — 오버레이 이미지에 안 가려짐
            UpdateRubberBand();
            CaptureMouse();
            e.Handled = true;
            return;
        }

        if (IsEditMode)
        {
            //_log?.Info("편집 모드에서 처리 시작");

            if (HandleImageEdit(mousePos, geoPos, e))
            {
                _log?.Info("이미지 편집 처리 완료 — base 호출 없이 팬 Armed 방지");
                return;
            }
            //_log?.Info("이미지 편집 해당 없음");
        }

        // 이미지/라인 편집 소비 없음 → base 호출하여 팬 및 기타 처리 위임
        base.OnMouseLeftButtonDown(e);

        //_log?.Info("클릭된 객체 검색 시작");
        var clickedImage = GetImageAtScreen(mousePos);
        var clickedMarker = GetMarkerAtScreen(mousePos);

        //_log?.Info($"검색 결과 - 이미지: {clickedImage?.Title ?? "없음"}, 마커: {clickedMarker?.Title ?? "없음"}");

        if (clickedMarker != null)
        {
            // [Ctrl+클릭] Shift 없이 Ctrl+클릭 = 그룹 선택 토글(단일선택/더블클릭 대신).
            // base 이후 동일 clickedMarker 사용 = 일반 클릭이 찾는 마커와 항상 동일(히트 신뢰).
            if (IsEditMode && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                MarkerToggleRequested?.Invoke(clickedMarker);
                return;
            }
            // 더블클릭 감지(컨트롤 레벨 — 자식 Shape에 의존하지 않아 편집/일반 모드 공통 동작)
            var nowClick = DateTime.Now;
            if (ReferenceEquals(clickedMarker, _lastClickedMarkerForDbl)
                && (nowClick - _lastMarkerClickTime).TotalMilliseconds <= MarkerDoubleClickIntervalMs)
            {
                _lastMarkerClickTime = DateTime.MinValue;
                _lastClickedMarkerForDbl = null;
                //_log?.Info($"마커 더블클릭 이벤트 발생: {clickedMarker.Title}");
                OnMarkerDoubleClicked?.Invoke(clickedMarker);
            }
            else
            {
                _lastMarkerClickTime = nowClick;
                _lastClickedMarkerForDbl = clickedMarker;
                //_log?.Info($"마커 클릭 이벤트 발생: {clickedMarker.Title}");
                OnMarkerClicked?.Invoke(clickedMarker);
            }
        }
        else if (clickedImage != null)
        {
            _log?.Info($"이미지 클릭 이벤트 발생: {clickedImage.Title}");
            OnImageClicked?.Invoke(clickedImage);
        }
        else
        {
            //_log?.Info("빈 공간 클릭 이벤트 발생");
            OnMapClicked?.Invoke(geoPos, mousePos);
        }

        //_log?.Info("=== GMapCustomControl.OnMouseLeftButtonDown 완료 ===");
    }

    /// <summary>
    /// 마우스 이동
    /// </summary>
    /// <summary>마지막 마우스 화면좌표(붙여넣기 기준점 — Ctrl+V at cursor). early-return 이전 무조건 갱신.</summary>
    private Point _lastMouseScreen = new Point(double.NaN, double.NaN);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _lastMouseScreen = e.GetPosition(this);   // 커서 추적(붙여넣기 위치) — early-return 이전 무조건

        // [측정] 미리보기 커서 갱신(라이브 리드아웃) — base 계속(좌표 표시 유지, 팬은 미Armed).
        if (IsMeasuring) _measureController?.UpdateMouse(e.GetPosition(this));

        // [Rubber-band] 마퀴 갱신 (base 미호출 = 팬 방지, FR-MS-01)
        if (_isRubberBanding)
        {
            _rubberCurrent = e.GetPosition(this);
            UpdateRubberBand();   // 어도너 마퀴 갱신(OnRender 아님 → 이미지·마커 위)
            return;
        }

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

    /// <summary>마지막 마우스 화면좌표 → 위경도. 커서가 맵 밖/미설정(NaN)이면 뷰 중앙 폴백(붙여넣기 기준점).
    /// FromLocalToLatLng는 WPF가 RenderTransform.Inverse를 e.GetPosition에 자동적용하므로 디지털줌 보정 불요.</summary>
    public GMap.NET.PointLatLng GetLastCursorLatLng()
    {
        double w = ActualWidth, h = ActualHeight;
        var p = _lastMouseScreen;
        bool inside = !double.IsNaN(p.X) && p.X >= 0 && p.Y >= 0 && p.X <= w && p.Y <= h;
        if (!inside) p = new Point(w / 2.0, h / 2.0);   // 맵 밖/미설정 → 뷰 중앙 폴백
        return FromLocalToLatLng((int)p.X, (int)p.Y);
    }

    /// <summary>
    /// 마우스 버튼 해제
    /// </summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        // [Rubber-band] 릴리스 → 사각형 내 마커 산출·통지 (FR-MS-02)
        if (_isRubberBanding)
        {
            _isRubberBanding = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;

            // [앵커 그리기] 릴리스 → 사각형 화면좌표를 NW/SE 지리좌표로 변환해 통지(마커선택과 분기). (FR-B3)
            //   FromLocalToLatLng은 WPF가 e.GetPosition에 RenderTransform.Inverse를 자동 적용하므로 디지털줌(17+)에서도 정확.
            if (_rubberForAnchor)
            {
                _rubberForAnchor = false;
                HideRubberBand();
                if (_rubberStart.HasValue && _rubberCurrent.HasValue)
                {
                    var ar = new Rect(_rubberStart.Value, _rubberCurrent.Value);   // 두 점의 바운딩박스(정규화)
                    if (ar.Width >= 4 && ar.Height >= 4)   // 최소 드래그(오클릭 방지)
                    {
                        IsAnchorDrawMode = false;                                    // 유효 드래그 → 모드 종료
                        // [Rotation R-28] 회전 시 화면 좌상≠지리 북서 — 4모서리를 전부 변환해 min/max로
                        // 정규화(HeightLat 음수·뒤틀린 저장 방지). 비회전에선 종전 2코너와 동일 결과.
                        var g1 = FromLocalToLatLng((int)ar.Left, (int)ar.Top);
                        var g2 = FromLocalToLatLng((int)ar.Right, (int)ar.Top);
                        var g3 = FromLocalToLatLng((int)ar.Right, (int)ar.Bottom);
                        var g4 = FromLocalToLatLng((int)ar.Left, (int)ar.Bottom);
                        double north = Math.Max(Math.Max(g1.Lat, g2.Lat), Math.Max(g3.Lat, g4.Lat));
                        double south = Math.Min(Math.Min(g1.Lat, g2.Lat), Math.Min(g3.Lat, g4.Lat));
                        double west = Math.Min(Math.Min(g1.Lng, g2.Lng), Math.Min(g3.Lng, g4.Lng));
                        double east = Math.Max(Math.Max(g1.Lng, g2.Lng), Math.Max(g3.Lng, g4.Lng));
                        var nw = new PointLatLng(north, west);                       // 정규화 북서
                        var se = new PointLatLng(south, east);                       // 정규화 남동
                        AnchorAreaDrawn?.Invoke(nw, se);                             // VM이 채우고 ExitAnchorDrawMode(커서 복원)
                    }
                    // 너무 작은 드래그(오클릭)면 모드 유지 → 재시도 또는 ESC 취소
                }
                _rubberStart = null; _rubberCurrent = null;
                return;
            }

            IReadOnlyList<IEditableMarker> hits = System.Array.Empty<IEditableMarker>();
            if (_rubberStart.HasValue && _rubberCurrent.HasValue)
            {
                var rect = new Rect(_rubberStart.Value, _rubberCurrent.Value);
                if (rect.Width >= 3 && rect.Height >= 3)   // 최소 드래그(오클릭 방지)
                    hits = GetMarkersInRect(rect);
            }
            _rubberStart = null; _rubberCurrent = null;
            HideRubberBand();   // 마퀴 어도너 제거
            MarkersRubberBandSelected?.Invoke(hits);
            return;
        }

        // [FP-3] 이미지 드래그 완료 시 ResetDragState() 먼저 실행 후 return.
        // 팬이 미Armed 상태(FP-1 효과)이므로 base의 GMap.NET EndDrag 처리가 불필요하고,
        // ReleaseMouseCapture()를 먼저 실행해야 WPF 이벤트 라우팅이 즉시 정상화된다.
        if (_isDragging && _isImageDrag)
        {
            // ★ FR-8 — ResetDragState가 _draggedImage를 null로 만들기 전에 캡처 후 편집완료 발화(DB 영속화)
            var edited = _draggedImage;
            var beforeBounds = _dragStartBounds;          // Undo before-state(D1)
            var beforeRot = _dragStartUserRotation;
            ResetDragState();
            if (edited != null) OnImageEditCompleted?.Invoke(edited, beforeBounds, beforeRot);
            _log?.Info("이미지 드래그 완료");
            e.Handled = true;
            return;
        }

        base.OnMouseLeftButtonUp(e);

        // [MapAnchor] 맵 팬 종료 복구(스턱 방지) — 맵 팬은 _isDragging(이미지 전용)을 세팅하지 않아 아래 이미지 블록을 안 탄다.
        //   드래그 중 중심이 inset 밖으로 오버슈트하면 벤더가 이후 모든 드래그를 Contains-스킵해 '스턱'되고(줌 변경 전까지 복구 불가),
        //   벤더 mouse-up 복원(LastLocationInBounds)은 스테일일 수 있다. 여기서 '현재 inset'으로 강제 클램프해 마우스 릴리즈 시 즉시 복구.
        if (!_isDragging && !_isClampingToBounds)
            ClampCenterToBounds(Position);

        if (_isDragging)
        {
            ResetDragState();
            // [임시 진단 제거] 드래그 종료 계측 로그
            //var elapsed = (DateTime.Now - _panStartTime).TotalMilliseconds;
            //_log?.Info($"[PAN] ===DRAG-END=== t={DateTime.Now:HH:mm:ss.fff} skippedFrames={_panSkipCount} elapsed={elapsed:F0}ms → TriggerSelectionChange once");

            // [MapAnchor] 드래그-팬 종료 정합 — 라이브 클램프가 매 프레임 경계를 유지하므로 보통 no-op.
            //   재클램프가 위치를 바꾸면(true) 그 Position 설정이 OnPositionChanged 재발화 → (IsDragging=false이므로)
            //   TriggerSelectionChange를 이미 수행하므로, 중복 렌더 방지를 위해 여기선 건너뛴다(H-2). 안 바뀌면 1회 갱신.
            if (!ClampCenterToBounds(Position))
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
        //_log?.Info($"GetMarkerAtScreen 호출: 화면위치({screenPosition.X:F2}, {screenPosition.Y:F2})");

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
                if (marker.IsLocked) continue;            // 잠긴 심볼은 클릭/선택 대상에서 제외(좌·우클릭 차단)
                if (!SetMarkerVisibility(marker)) continue;

                var markerScreenPos = FromLatLngToLocal(marker.Position);
                var markerScreenPoint = new Point(markerScreenPos.X, markerScreenPos.Y);
                var screenDistance = CalculateScreenDistance(screenPosition, markerScreenPoint);

                // 렌더된 화면 크기 — 이미지 마커는 OnRender 캐시(RenderedScreenWidth) 우선(ActualWidth 1사이클 지연 회피),
                // 그 외 마커는 Shape.ActualWidth/Height, 폴백 32px
                var shape = (marker as GMap.NET.WindowsPresentation.GMapMarker)?.Shape as FrameworkElement;
                double renderedW, renderedH;
                if (shape is GMapMarkerImageControl imgCtrl && imgCtrl.RenderedScreenWidth > 0)
                {
                    renderedW = imgCtrl.RenderedScreenWidth;
                    renderedH = imgCtrl.RenderedScreenHeight;
                }
                else
                {
                    renderedW = shape?.ActualWidth is > 0 ? shape.ActualWidth : 32.0;
                    renderedH = shape?.ActualHeight is > 0 ? shape.ActualHeight : 32.0;
                }

                // ★ AABB 히트테스트 (MarkerHitTest_AABB_Fix R-1) — 원형 반경(Math.Max(W,H)/2+8) 대신
                //   마커 Width×Height 사각형으로 판정. 마커 Offset=(-W/2,-H/2)이므로 markerScreenPoint가
                //   시각 중심 → 중심 기준 AABB가 정확. 원형은 라인/비정방형 심볼에서 빈 공간 오선택 발생.
                var halfW = renderedW / 2.0;
                var halfH = renderedH / 2.0;

                // ★ 회전 보정 — [Rotation FR-11 render/hit parity] 렌더가 쓰는 표시각과 '동일한'
                //   RotationMath.DisplayAngle(Bearing, θ, AppliesMapRotation)로 역회전한다(F-05:
                //   종전 raw marker.Bearing만 쓰면 지도 회전 시 보이는 모양과 클릭 영역이 어긋남).
                //   정점 재투영 계열(AppliesMapRotation=false)은 표시각에 θ가 없어 자동 제외(R-36).
                var testPos = screenPosition;
                bool shapeMapRotates = shape is GMapSymbols.IMapRotationAwareShape aware && aware.AppliesMapRotation;
                double hitMapBearing = Utils.RotationMath.NormalizeDeg(Bearing);
                double displayAngle = Utils.RotationMath.DisplayAngle(marker.Bearing, hitMapBearing, shapeMapRotates);
                if (Math.Abs(displayAngle) > 0.01)
                {
                    var rad = -displayAngle * Math.PI / 180.0;
                    var ox = screenPosition.X - markerScreenPoint.X;
                    var oy = screenPosition.Y - markerScreenPoint.Y;
                    testPos = new Point(
                        markerScreenPoint.X + ox * Math.Cos(rad) - oy * Math.Sin(rad),
                        markerScreenPoint.Y + ox * Math.Sin(rad) + oy * Math.Cos(rad));
                }
                var dx = Math.Abs(testPos.X - markerScreenPoint.X);
                var dy = Math.Abs(testPos.Y - markerScreenPoint.Y);

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
            //_log?.Info("클릭 위치에서 마커를 찾을 수 없음");
            return null;
        }

        // 우선순위: ZIndex 높은 순 → 면적 작은 순 → 거리 가까운 순
        var selected = candidates
            .OrderByDescending(c => c.zIndex)
            .ThenBy(c => c.area)
            .ThenBy(c => c.distance)
            .First();

        //_log?.Info($"GetMarkerAtScreen 선택: '{selected.marker.Title}' ZIndex={selected.zIndex} Area={selected.area:F0} Dist={selected.distance:F1}px (후보 {candidates.Count}개)");
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
                //_log?.Info($"마커 Adorner 등록: {marker.Title}");
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
                //_log?.Info($"마커 Adorner 해제: {marker.Title}");
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
            //_log?.Info($"마커 선택 시도: {marker.Title}");

            if (marker != null)
            {
                //_log?.Info($"마커 컨트롤 찾음: {marker.GetType().Name}");

                // 마커를 선택 상태로 설정
                marker.IsSelected = true;
                var markerControl = FindMarkerControlByMarker(marker);

                // AdornerManager를 통한 선택
                bool result = AdornerManager.SelectMarker(marker, markerControl, this);
                //_log?.Info($"AdornerManager.SelectMarker 결과: {result}");

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
            //_log?.Info($"마커 자체가 IMarkerControl 구현: {marker.GetType().Name}");
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
                foreach (var marker in Markers.OfType<IEditableMarker>())   // 불변식#8: 트레일/추적(비-IEditableMarker) 하드캐스트 크래시 방지
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

        // [Rotation FR-18] Ctrl+Shift+R = 회전 kill-switch 토글 — 툴바 버튼과 동일한
        // ToggleRotationFeature(단일 진실원) 경유. flag 기본값은 여전히 OFF(부팅 시 회전 불가).
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.R)
        {
            // [일관성] A모드 앵커 잠금 중엔 키보드도 버튼(비활성)과 동일하게 무시 — 버튼만 막고
            // 키보드로 우회되면 상태가 어긋난다(사용자 "버튼 풀림→앵커 꼬임" 보고 방어).
            if (IsRotationLockedByAnchor)
            {
                _log?.Info("[Rotation] 토글 무시 — 사이트 고정(A모드) 회전 잠금 중");
                e.Handled = true;
                return;
            }
            ToggleRotationFeature();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                // [Rotation FR-18 재개방] Ctrl+←/→ 회전 — kill-switch(기본 OFF) 게이트.
                // OFF: 종전 af0f29d 그대로 소비만(동작 변화 0). ON: RotateMap(∓5) → SSOT 경유.
                case Key.Left:
                    if (Utils.RotationFeature.IsEnabled) RotateMap(-5);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (Utils.RotationFeature.IsEnabled) RotateMap(5);
                    e.Handled = true;
                    break;
                case Key.R:
                    ResetRotation();    // 기존 회전 상태 0으로 복구(유지)
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

    #region Digital Zoom (MaxZoom 초과 소프트 확대 — 컨트롤 RenderTransform 방식)

    // 불변식: ScaleMode(Integer)·Zoom·_core.Zoom/ScaleX/Y는 절대 변경하지 않는다. RenderTransform만 변경.
    //   WPF가 e.GetPosition(this)에 RenderTransform.Inverse를 자동 적용하므로 히트테스트 수동 보정 금지(이중변환 버그).
    public const int DIGITAL_ZOOM_MAX = 3;
    // level 1 = 1.25× → 정수줌 사이 "중간 스텝"(예: z17 50m→40m). level 2/3 = 1.5/2.0 (MaxZoom 위 소프트 줌). (FR-A 줌 40m)
    private static readonly double[] DIGITAL_SCALE_TABLE = { 1.0, 1.25, 1.5, 2.0 };

    /// <summary>디지털 줌 레벨 변경 시 발화 (arg = 새 레벨 0~2). 축척바 동기화용.</summary>
    public event Action<int>? DigitalZoomLevelChanged;

    /// <summary>
    /// 현재 디지털 줌 레벨(0~2). DependencyProperty — 슬라이더/VM과 TwoWay 바인딩(변경 통지 내장).
    /// CLR 프로퍼티로 두면 바인딩이 변경 통지를 못 받으므로 DP 필수.
    /// </summary>
    public int DigitalZoomLevel
    {
        get => (int)GetValue(DigitalZoomLevelProperty);
        set => SetValue(DigitalZoomLevelProperty, value);
    }
    public static readonly DependencyProperty DigitalZoomLevelProperty =
        DependencyProperty.Register(nameof(DigitalZoomLevel), typeof(int), typeof(GMapCustomControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnDigitalZoomLevelChanged, CoerceDigitalZoomLevel));

    private static object CoerceDigitalZoomLevel(DependencyObject d, object baseValue)
        => Math.Clamp((int)baseValue, 0, DIGITAL_ZOOM_MAX);

    private static void OnDigitalZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (GMapCustomControl)d;
        c.ApplyDigitalZoomTransform();
        // [MapAnchor] 디지털 줌 변경 시 보이는 영역이 달라지므로 inset 라이브 재계산(FR-4/5).
        c.RecomputeAnchorViewportBounds();
        c.DigitalZoomLevelChanged?.Invoke((int)e.NewValue);
    }

    /// <summary>현재 디지털 줌 배율(1.0/1.5/2.0).</summary>
    public double DigitalZoomScale => DIGITAL_SCALE_TABLE[Math.Clamp(DigitalZoomLevel, 0, DIGITAL_ZOOM_MAX)];

    /// <summary>디지털 줌 레벨 증감(+1/-1). 코어스로 [0,2] 클램프. UI 스레드에서만 호출.</summary>
    public void StepDigitalZoom(int delta) => DigitalZoomLevel += delta;

    /// <summary>맵 전환·Provider 변경·홈 이동·MaxZoom 변동 시 디지털 줌 초기화(FR-10).</summary>
    public void ResetDigitalZoom() => DigitalZoomLevel = 0;

    /// <summary>디지털 배율을 컨트롤 RenderTransform(화면 중심 기준 ScaleTransform)으로 적용.</summary>
    private void ApplyDigitalZoomTransform()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;   // SE-1: 초기화/리사이즈 중 중심 어긋남 방어 (NFR-4)
        double scale = DigitalZoomScale;
        if (Math.Abs(scale - 1.0) < 0.001)
            RenderTransform = null;                          // 아이덴티티 복원
        else
            RenderTransform = new ScaleTransform(scale, scale, ActualWidth / 2.0, ActualHeight / 2.0);
        _log?.Info($"[DigitalZoom] level={DigitalZoomLevel}, scale={scale:F1}x");
        InvalidateVisual();
    }

    /// <summary>
    /// inner(논리/타일) 좌표 → outer(화면) 좌표. 디지털 줌 ScaleTransform(중심 cx,cy 기준)의 정방향 변환.
    /// ★ 카메라 팝업 경로(PropertyPanelCanvas — RenderTransform '밖' 형제 캔버스) 전용.
    ///   마커/격자/스냅(컨트롤 '안' — WPF가 e.GetPosition(this)에 RenderTransform.Inverse를 자동 적용)에는
    ///   절대 적용 금지: 이중보정 버그(불변식, 본 region 상단 주석 참조). scale=1(디지털줌 OFF)이면 항등.
    ///   ※ 이 수식을 바꾸면 tests/GMaps.Ui.Tests/DigitalZoomCoordinateTests.cs의 복제 수식도 동기화할 것(L-1).
    /// </summary>
    public Point InnerToOuter(Point p)
    {
        double s = DigitalZoomScale;
        if (ActualWidth <= 0 || ActualHeight <= 0 || Math.Abs(s - 1.0) < 0.001) return p;
        double cx = ActualWidth / 2.0, cy = ActualHeight / 2.0;
        return new Point(cx + (p.X - cx) * s, cy + (p.Y - cy) * s);
    }

    /// <summary>
    /// outer(화면) 좌표 → inner(논리/타일) 좌표. <see cref="InnerToOuter"/>의 역함수(드래그 저장 시 FromLocalToLatLng 입력용).
    /// ★ 팝업 경로 전용(위 가드 동일). scale=1이면 항등.
    /// </summary>
    public Point OuterToInner(Point p)
    {
        double s = DigitalZoomScale;
        if (ActualWidth <= 0 || ActualHeight <= 0 || Math.Abs(s - 1.0) < 0.001) return p;
        double cx = ActualWidth / 2.0, cy = ActualHeight / 2.0;
        return new Point(cx + (p.X - cx) / s, cy + (p.Y - cy) / s);
    }

    #endregion

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

        // [Rotation FR-18 재개방] Shift+휠 회전 — RotationFeature.IsEnabled(kill-switch, 기본 OFF) 게이트.
        // OFF: 종전 af0f29d 그대로 소비만(동작 변화 0 — 머지 안전). ON: RotateMap(±5°) →
        // SSOT(ApplyMapRotation) 경유라 정규화·앵커 게이트 자동 적용. Ctrl+R 리셋은 항상 유지.
        if (Keyboard.Modifiers == ModifierKeys.Shift)
        {
            if (Utils.RotationFeature.IsEnabled)
                RotateMap(e.Delta > 0 ? 5 : -5);
            e.Handled = true;   // 휠은 소비(줌으로 전파 금지)
            return;
        }

        bool zoomUp = InvertedMouseWheelZooming ? e.Delta < 0 : e.Delta > 0;

        // ─────────────────────────────────────────────────────────────────────────────
        //  줌 40m 세분화 (FR-A) — 정수줌 사이에 디지털 1.25× "중간 스텝" 삽입.
        //  래더(오름차순): (17,0)=50m → (17,1)=40m → (18,0)=30m → (18,1)=24m → (19,0)…
        //    · Zoom<MaxZoom: level 0↔1(1.25× 중간). 휠업 at level1 → 다음 정수. 휠다운 대칭(Z→Z-1+중간).
        //    · Zoom>=MaxZoom: 기존 above-max 소프트 줌(level 1/2/3 = 1.25/1.5/2.0).
        //  타일 Zoom/_core는 정수만 변경(불변식 유지), 중간은 RenderTransform. 스케일바("40m")·라벨("17+")은
        //  DigitalZoomScale로 자동 계산. reset+base는 한 핸들러 내 동기 실행이라 중간 프레임 렌더 없음(플리커 없음).
        // ─────────────────────────────────────────────────────────────────────────────
        if (zoomUp)
        {
            if (Zoom < MaxZoom)
            {
                if (DigitalZoomLevel == 0)              // 정수 → 40m 중간 스텝
                {
                    StepDigitalZoom(+1);                // → level 1 (1.25×)
                    e.Handled = true;
                    return;
                }
                ResetDigitalZoom();                     // 중간 → 다음 정수(아래 base 정수 줌인)
            }
            else                                         // ★ FR-1: MaxZoom 위 소프트 줌(기존)
            {
                if (DigitalZoomLevel < DIGITAL_ZOOM_MAX) StepDigitalZoom(+1);
                e.Handled = true;
                return;
            }
        }
        else // zoomDown
        {
            // ★ FR-2: 디지털(중간/소프트) 활성 중 휠다운 → 디지털 감소 우선
            if (DigitalZoomLevel > 0)
            {
                StepDigitalZoom(-1);
                e.Handled = true;
                return;
            }
            // ★ T2: MinZoom 하한 가드 — base는 Position 먼저 옮긴 뒤 Zoom 클램프 → 경계 중심점프 차단
            if (Zoom <= MinZoom)
            {
                e.Handled = true;
                return;
            }
            // 정수(level0) → 아래 정수의 40m 중간 스텝 (Z→Z-1 후 1.25× 부여): 오름차순과 대칭(18→17+→17)
            base.OnMouseWheel(e);                        // Zoom Z→Z-1 (정수 줌아웃)
            if (Zoom < MaxZoom) StepDigitalZoom(+1);     // 아래 정수의 중간 스텝 부여
            e.Handled = true;
            return;
        }

        base.OnMouseWheel(e);   // 정수 줌인(중간→다음 정수, 또는 MinZoom 근처 일반 줌인)
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

                foreach (var marker in Markers.OfType<IEditableMarker>())   // 불변식#8: 하드캐스트 크래시 방지
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

        // 핸들(리사이즈/회전/중심이동)을 클릭한 경우만 편집으로 소비.
        if (_resizeHandle != ResizeHandle.None)
        {
            StartImageDrag(selectedImage, mousePos, _resizeHandle);
            e.Handled = true;
            return true;
        }

        // ★ T4 — 이미지 '본체' 클릭/드래그는 편집으로 소비하지 않고 맵 팬으로 위임(false 반환).
        //   이미지 이동은 중심 이동(Move) 핸들로만 한다. (기존 본체 드래그=이동 폴백 제거)
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
        _dragStartBounds = image.ImageBounds;          // Undo before-state(D1) — 이동/크기/회전 공통
        _dragStartUserRotation = image.UserRotation;

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
                newBounds = SnapBoundsCenter(MoveBounds(curBounds, deltaX, deltaY)); // RC-4: 중심 격자 스냅
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
    /// 이미지 화면 사각형 — [FR-07 ProjectedQuad 동치] '회전 불변 중심 사각형'. (NFR-1 공통 헬퍼)
    /// 투영은 등각(회전=거리 보존)이므로 [불변 rect(정확한 중심 + 인접모서리 유클리드 W/H)] +
    /// [GetImageRotateTransform(EffectiveRotation=User−θ) 1회전] 합성이 곧 4모서리 투영 quad와
    /// 수학적 동치다. 렌더·히트(InverseRotateMouse)·핸들·드래그가 이 rect+회전을 공유하므로
    /// rect 산출만 교정하면 전 경로가 quad 정합(F-01 AABB 팽창·R-01 음수 crash 동시 해소).
    /// Bearing=0·θ=0에선 종전 TL/BR rect와 동일(비회전 회귀 0, NFR-04). 예외 시 Rect.Empty.
    /// </summary>
    private Rect GetImageScreenRect(GMapCustomImage image)
    {
        try
        {
            var b = image.ImageBounds;
            var tl = FromLatLngToLocal(b.LocationTopLeft);
            var br = FromLatLngToLocal(b.LocationRightBottom);
            var tr = FromLatLngToLocal(new PointLatLng(b.Lat, b.Lng + b.WidthLng));
            var bl = FromLatLngToLocal(new PointLatLng(b.Lat - b.HeightLat, b.Lng));
            double w = ScreenDist(tl, tr);   // 회전 불변 폭(항상 ≥0)
            double h = ScreenDist(tl, bl);   // 회전 불변 높이
            double cx = (tl.X + br.X) / 2.0; // 등각 투영: 대각 중점 = 지오 중심의 투영
            double cy = (tl.Y + br.Y) / 2.0;
            if (w < 1 || h < 1) return Rect.Empty;
            return new Rect(cx - w / 2.0, cy - h / 2.0, w, h);
        }
        catch (Exception ex)
        {
            _log?.Error($"GetImageScreenRect 실패(가드): {ex.Message}");
            return Rect.Empty;
        }
    }

    /// <summary>투영점 간 유클리드 거리 — 회전 불변 크기 산출 공용(FR-07).</summary>
    private static double ScreenDist(GPoint a, GPoint b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
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

        // ★ T4 — 중심 이동(Move) 핸들. 본체 드래그는 맵 팬, 이미지 이동은 이 중심 핸들로만 한다.
        var moveHandle = new Point(imageRect.Left + imageRect.Width / 2,
                                   imageRect.Top + imageRect.Height / 2);
        if (Math.Abs(local.X - moveHandle.X) <= tolerance &&
            Math.Abs(local.Y - moveHandle.Y) <= tolerance)
        {
            return ResizeHandle.Move;
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

        // [Rotation FR-15 후보B — V-08 pixel-diff 실측 채택] 회전 시: 타일을 '비절단 core-local'
        // (벤더 seam FromLatLngToCoreLocal)에 배치하고 회전 행렬을 딱 1회 Push — 베이스 타일
        // (DrawMap)과 동일 변환 합성이라 seam 0(전 각도·DPI 0-diff). 절단된 Canvas 좌표를
        // 역회전하는 후보A는 타일별 1px 계단으로 실측 탈락(R-44 이중회전도 금지).
        // ※ MapScaleTransform은 앱이 ScaleMode=Integer 고정이라 항상 null(internal이라 접근도 불가) —
        //   fractional 도입 시 벤더 seam 확장 필요(주석 계약).
        bool rotated = IsRotated;
        var rot = RotationMatrixValue;
        var rotInv = rot; if (rotated) rotInv.Invert();
        if (rotated) drawingContext.PushTransform(new MatrixTransform(rot));
        try
        {
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

                    double drawX = left, drawY = top;
                    if (rotated)
                    {
                        if (img.Tag is PointLatLng tileGeo)
                        {
                            // 정석: 타일 geo → 비절단 core-local (베이스 타일과 동일 격자)
                            var core = FromLatLngToCoreLocal(tileGeo);
                            drawX = core.X; drawY = core.Y;
                        }
                        else
                        {
                            // 과도기 폴백(Tag 미보유 stale 타일): 절단 좌표 역회전 — 다음 Refresh서 정상화
                            var back = rotInv.Transform(new Point(left, top));
                            drawX = back.X; drawY = back.Y;
                        }
                    }

                    var rect = new Rect(drawX, drawY, img.Width, img.Height);

                    if (opacity < 1.0)
                        drawingContext.PushOpacity(opacity);

                    drawingContext.DrawImage(img.Source, rect);

                    if (opacity < 1.0)
                        drawingContext.Pop();
                }
            }
        }
        finally
        {
            if (rotated) drawingContext.Pop();
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

        // ★ T4 — 중심 이동(Move) 핸들 (하늘색 원). 본체 드래그는 맵 팬, 이동은 이 핸들로만.
        var moveCenter = new Point(imageRect.Left + imageRect.Width / 2, imageRect.Top + imageRect.Height / 2);
        drawingContext.DrawEllipse(Brushes.DeepSkyBlue, handlePen, moveCenter, handleSize / 2.0 + 1, handleSize / 2.0 + 1);

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

    // 타겟 반경 오버레이 — 흰색 점선 외곽(채움 없음). 정적 frozen — 렌더마다 재할당 회피.
    private static readonly Pen _aimEdgePen = CreateDashedPen(Colors.White, 2d);
    private static readonly Pen _aimCrossPen = CreateFrozenPen(Color.FromArgb(220, 255, 255, 255), 1.5d);

    private static Pen CreateFrozenPen(Color c, double thickness)
    {
        var br = new SolidColorBrush(c);
        br.Freeze();
        var p = new Pen(br, thickness);
        p.Freeze();
        return p;
    }

    private static Pen CreateDashedPen(Color c, double thickness)
    {
        var br = new SolidColorBrush(c);
        br.Freeze();
        var p = new Pen(br, thickness) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
        p.Freeze();
        return p;
    }

    /// <summary>
    /// 카메라 "특정 위치 확인" 타겟 반경 원 + 중심 십자 그리기.
    /// 중심/반경은 매 렌더 시 현재 줌·팬 기준으로 지오→픽셀 재계산하므로 맵과 함께 정확히 이동/스케일.
    /// </summary>
    private void DrawAimRadius(DrawingContext drawingContext)
    {
        try
        {
            var center = AimOverlayCenter!.Value;
            var cPx = FromLatLngToLocal(center);
            double rPx = MetersToScreenPixels(AimOverlayRadiusMeters, center);
            if (rPx <= 0d || double.IsNaN(rPx) || double.IsInfinity(rPx)) return;   // 변환 실패 시 미표시

            var centerPt = new Point(cPx.X, cPx.Y);
            drawingContext.DrawEllipse(null, _aimEdgePen, centerPt, rPx, rPx);   // 채움 없음 — 흰 점선 외곽만

            const double cross = 7d;   // 중심 십자 반길이(px)
            drawingContext.DrawLine(_aimCrossPen, new Point(centerPt.X - cross, centerPt.Y), new Point(centerPt.X + cross, centerPt.Y));
            drawingContext.DrawLine(_aimCrossPen, new Point(centerPt.X, centerPt.Y - cross), new Point(centerPt.X, centerPt.Y + cross));
        }
        catch (Exception ex)
        {
            _log?.Error($"타겟 반경 렌더 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 미터 반경 → 현재 줌의 화면 픽셀 반경. 중심에서 동쪽으로 meters 이동한 지오점의 화면거리로 산출.
    /// (GMapMarkerPidsControl.ConvertMetersToPixels 와 동일 공식 — 컨트롤 자족 구현.)
    /// </summary>
    internal double MetersToScreenPixels(double meters, PointLatLng center)
    {
        const double earthRadius = 6_371_000d;
        if (Math.Abs(center.Lat) > 89.9d) return 0d;        // 극점 인근 방어(cos→0 발산 회피)
        double latRad = center.Lat * Math.PI / 180d;
        double denom = earthRadius * Math.Cos(latRad);
        if (denom <= 0d) return 0d;
        double deltaLng = meters / denom * 180d / Math.PI;
        var target = new PointLatLng(center.Lat, center.Lng + deltaLng);
        var cPx = FromLatLngToLocal(center);
        var tPx = FromLatLngToLocal(target);
        double dx = tPx.X - cPx.X;
        double dy = tPx.Y - cPx.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ── 카메라 반경 오버레이 adorner (Camera_Aim_Overlay_Animation) ──
    // AdornerManager 미사용 — 5분 정리타이머·per-marker 딕셔너리와 무관한 전용 소유(불변식 준수).
    private Adorners.AimOverlayAdorner? _aimOverlayAdorner;

    // ── 러버밴드 마퀴 어도너(맵 AdornerLayer, 이미지·마커 위 — OnRender 마퀴가 이미지마커에 가려지는 문제 해소) ──
    private Adorners.RubberBandAdorner? _rubberBandAdorner;

    /// <summary>러버밴드 마퀴 어도너 부착(러버밴드 시작 시).</summary>
    private void ShowRubberBand()
    {
        try
        {
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(this);
            if (layer == null) return;
            if (_rubberBandAdorner == null)
            {
                _rubberBandAdorner = new Adorners.RubberBandAdorner(this);
                layer.Add(_rubberBandAdorner);
            }
            SetImagesMultiSelectActive(true);   // 러버밴드(멀티셀렉트) 중 오버레이 이미지 호버 Edge 억제(B1)
        }
        catch (Exception ex) { _log?.Error($"러버밴드 오버레이 표시 실패: {ex.Message}"); }
    }

    /// <summary>러버밴드(멀티셀렉트) 활성 상태를 오버레이 이미지 컨트롤에 전파 — 활성 중 호버 Edge 억제(B1).
    /// 이미지는 멀티셀렉트 대상서 제외(M1)라 러버밴드 중 아무 반응도 없어야 함.</summary>
    private void SetImagesMultiSelectActive(bool active)
    {
        if (Markers == null) return;
        foreach (var m in Markers)
            if ((m as GMap.NET.WindowsPresentation.GMapMarker)?.Shape is GMapMarkerImageControl c)
                c.IsMultiSelectActive = active;
    }

    /// <summary>러버밴드 마퀴 사각형 갱신(드래그 중).</summary>
    private void UpdateRubberBand()
    {
        if (_rubberBandAdorner != null && _rubberStart.HasValue && _rubberCurrent.HasValue)
            _rubberBandAdorner.Update(new Rect(_rubberStart.Value, _rubberCurrent.Value));
    }

    /// <summary>러버밴드 마퀴 어도너 제거(종료/취소).</summary>
    private void HideRubberBand()
    {
        SetImagesMultiSelectActive(false);   // 러버밴드 종료/취소 → 이미지 호버 원복(B1). 종료·취소 전 경로가 이 메서드 경유.
        if (_rubberBandAdorner == null) return;
        try { System.Windows.Documents.AdornerLayer.GetAdornerLayer(this)?.Remove(_rubberBandAdorner); } catch { /* 레이어 정리 중 */ }
        _rubberBandAdorner = null;
    }

    /// <summary>반경 오버레이 adorner 부착 + 등장 애니 시작(IsTargetAimMode=true 시).</summary>
    private void ShowAimOverlay()
    {
        try
        {
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(this);
            if (layer == null) return;
            if (_aimOverlayAdorner == null)
            {
                _aimOverlayAdorner = new Adorners.AimOverlayAdorner(this, _log);
                layer.Add(_aimOverlayAdorner);
            }
            _aimOverlayAdorner.StartGrowIn();
        }
        catch (Exception ex) { _log?.Error($"aim 오버레이 표시 실패: {ex.Message}"); }
    }

    /// <summary>반경 오버레이 adorner 제거 + Dispose(구독해제, 누수방지). IsTargetAimMode=false 시.</summary>
    private void HideAimOverlay()
    {
        try
        {
            if (_aimOverlayAdorner == null) return;
            System.Windows.Documents.AdornerLayer.GetAdornerLayer(this)?.Remove(_aimOverlayAdorner);
            _aimOverlayAdorner.Dispose();
            _aimOverlayAdorner = null;
        }
        catch (Exception ex) { _log?.Error($"aim 오버레이 제거 실패: {ex.Message}"); }
    }

    /// <summary>유효 클릭 지점에 주황 리플 1회 재생. aim 모드 종료(단발) 이후에도 보이도록 전이(transient) adorner로 재생.</summary>
    public void TriggerAimRipple(PointLatLng geo)
    {
        try
        {
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(this);
            if (layer == null) return;
            var ripple = new Adorners.AimOverlayAdorner(this, _log);
            layer.Add(ripple);
            ripple.RippleCompleted += () =>
            {
                try { System.Windows.Documents.AdornerLayer.GetAdornerLayer(this)?.Remove(ripple); } catch { }
                ripple.Dispose();
            };
            ripple.TriggerRipple(geo);
        }
        catch (Exception ex) { _log?.Error($"aim 리플 실패: {ex.Message}"); }
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
        // [Rotation R-33 데드존 탈출] 스냅각 ≥ 2×스텝이면 절대격자 반올림이 항상 같은 배수로
        // 되돌아와 회전이 무반응이 되던 결함 — 스냅 결과가 제자리면 delta 방향 다음 배수로 전진.
        if (RotationSnapAngle > 0
            && Math.Abs(deltaAngle) > Utils.RotationMath.Epsilon
            && Math.Abs(newRotation - MapRotation) < Utils.RotationMath.Epsilon)
        {
            newRotation = MapRotation + Math.Sign(deltaAngle) * RotationSnapAngle;
        }
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

    // [FR-01 SSOT] DP 교정 재진입 가드 — ApplyMapRotation 안에서 MapRotation을 canonical로
    // 되돌릴 때 중첩 콜백을 무시한다(무한루프 방지). 모든 회전 경로(RotateMap/SetMapRotation/
    // ResetRotation/XAML 바인딩/직접 DP 세트)는 DP 콜백 → 이 메서드 하나로 수렴한다.
    private bool _rotationCoercing;

    /// <summary>
    /// 지도 회전 적용 — 회전 SSOT 단일 진입점(FR-01). 게이트(kill-switch FR-03 + 앵커 잠금 FR-02)
    /// → canonical [-180,180) 정규화 → DP 교정(MapRotation≠Bearing 불일치 방지, R-09) → Bearing 적용.
    /// 차단 시 DP를 직전 canonical(Bearing)로 되돌려 소비처(나침반·텍스트·스냅게이트·IsRotated)가
    /// 항상 정규화 값을 읽게 한다.
    /// </summary>
    private void ApplyMapRotation(double rotation)
    {
        if (_rotationCoercing) return;   // canonical 교정 중 중첩 콜백 무시
        try
        {
            // [V-06 옵션C] 앵커 잠금은 '회전 비허용 앵커'일 때만(B모드 앵커는 회전 통과)
            var applied = Utils.RotationMath.Decide(rotation, Utils.RotationFeature.IsEnabled,
                _anchorSiteRect != null && !_anchorAllowsRotation);
            if (applied == null)
            {
                // 차단(kill-switch OFF 또는 앵커 활성) — DP를 직전 canonical로 복원(no-op)
                _rotationCoercing = true;
                try { MapRotation = Bearing; } finally { _rotationCoercing = false; }
                _log?.Info($"지도 회전 차단(no-op): 요청 {rotation:F1}도 — feature={Utils.RotationFeature.IsEnabled}, anchor={_anchorSiteRect != null}");
                return;
            }

            double canonical = applied.Value;
            if (Math.Abs(canonical - rotation) > Utils.RotationMath.Epsilon)
            {
                // 요청값이 비정규(365도 등) — DP를 canonical로 교정(소비처 통일, R-09)
                _rotationCoercing = true;
                try { MapRotation = canonical; } finally { _rotationCoercing = false; }
            }

            if (Utils.RotationMath.AreClose(Bearing, canonical)) return;   // ε-가드: 중복 적용 스킵

            Bearing = (float)canonical;
            UpdateOverlaysAfterRotation();
            _log?.Info($"지도 회전 적용: {canonical:F1}도");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 적용 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 회전 후 오버레이 업데이트 — [FR-04/F-10] 파트별 예외 격리(종전 단일 try/catch 해체:
    /// 한 파트 실패가 뒤 파트를 중단시키지 않는다) + 뷰포트 snapshot 발행(FR-05 소비처 통지).
    /// </summary>
    private void UpdateOverlaysAfterRotation()
    {
        // [FR-16 / F-12] 마커 위치 재배치 루프 제거 — 벤더 Bearing setter가 이미
        // ForceUpdateOverlays()로 전 마커를 갱신한다(GMapControl.cs Bearing setter). 여기서
        // 또 돌면 회전 tick마다 중복 O(N). (맵 전환 Resync 경로의 위치는 Position/Zoom 이벤트가 담당.)

        // [FR-11] 심볼 표시각 중앙 배포 — 각 Shape에 canonical bearing 푸시(개별 구독 없음=누수 0).
        // point 심볼=−θ 합성, 정점 재투영 계열(AppliesMapRotation=false)은 내부에서 스킵(R-36).
        try
        {
            double canonicalBearing = Utils.RotationMath.NormalizeDeg(Bearing);
            foreach (GMapMarker marker in Markers)
                (marker.Shape as GMapSymbols.IMapRotationAwareShape)?.OnMapBearingChanged(canonicalBearing);
        }
        catch (Exception ex) { _log?.Error($"회전 후 심볼 표시각 배포 실패(격리): {ex.Message}"); }

        // 이미지 오버레이 회전 보정 (FR-7, NFR-6)
        // ★ Rotation(=UserRotation) 덮어쓰기 금지 — 사용자 편집 회전값 보존.
        //   맵 보정값만 MapCorrectionRotation에 반영하고, 렌더는 EffectiveRotation(합산)을 사용한다.
        try
        {
            foreach (var customImage in CustomImages)
                customImage.MapCorrectionRotation = -MapRotation;
        }
        catch (Exception ex) { _log?.Error($"회전 후 이미지 보정 실패(격리): {ex.Message}"); }

        // [V-06 옵션C/B모드] 회전 허용 앵커 활성 중 회전이 바뀌면 inset을 라이브 재계산 —
        // ViewArea(FR-08 4코너)가 회전 화면의 외접 bbox를 주므로 가두기 보장이 각도 무관 유지.
        try
        {
            if (_anchorSiteRect != null && _anchorAllowsRotation)
                RecomputeAnchorViewportBounds();
        }
        catch (Exception ex) { _log?.Error($"회전 후 앵커 inset 재계산 실패(격리): {ex.Message}"); }

        // 소비처(라벨·측정·조준·라인드로잉·그룹선택·FOV·라인·트레일·재생·팝업·오버레이맵) 통지 — FR-05
        QueueViewportSnapshot();

        try { InvalidateVisual(); }
        catch (Exception ex) { _log?.Error($"회전 후 무효화 실패(격리): {ex.Message}"); }
    }

    #region Viewport Snapshot (FR-04 — 회전 동기 계약)

    private ViewportSnapshotPublisher? _viewportPublisher;
    private bool _snapshotPublishQueued;

    private ViewportSnapshotPublisher ViewportPublisher => _viewportPublisher ??= new ViewportSnapshotPublisher(_log);

    /// <summary>현재 뷰포트 snapshot(발행 전이면 즉석 생성). 소비자는 구독 시 자동 replay되므로
    /// 통상 직접 조회 불요 — 초기화 순서가 특수한 소비자용.</summary>
    public MapViewportSnapshot CurrentViewportSnapshot
        => ViewportPublisher.Current ?? BuildViewportSnapshot();

    /// <summary>뷰포트 snapshot 구독 — 구독 즉시 현재 snapshot replay(동적 추가 소비자 정합).
    /// 소비자는 반드시 Dispose/Unloaded에서 UnsubscribeViewport(NFR-04 누수 방지).</summary>
    public void SubscribeViewport(System.Action<MapViewportSnapshot> handler) => ViewportPublisher.Subscribe(handler);

    public void UnsubscribeViewport(System.Action<MapViewportSnapshot> handler) => ViewportPublisher.Unsubscribe(handler);

    private MapViewportSnapshot BuildViewportSnapshot() => new(
        Position, Utils.RotationMath.NormalizeDeg(Bearing), Zoom,
        ActualWidth, ActualHeight, DigitalZoomScale, ViewportPublisher.CurrentRevision);

    /// <summary>snapshot 발행 예약 — WPF 프레임당 최종 1회 coalescing(FR-16: 연속 회전 입력이
    /// 프레임 내 여러 번 와도 발행은 마지막 상태로 1회). revision은 매 변경마다 증가.</summary>
    private void QueueViewportSnapshot()
    {
        ViewportPublisher.NextRevision();
        if (_snapshotPublishQueued) return;
        _snapshotPublishQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new System.Action(() =>
        {
            _snapshotPublishQueued = false;
            try { ViewportPublisher.Publish(BuildViewportSnapshot()); }
            catch (Exception ex) { _log?.Error($"Viewport snapshot 발행 실패: {ex.Message}"); }
        }));
    }

    /// <summary>맵 전환/재로드 후 회전 종속 상태 재적용(R-31) — 신규 오버레이의
    /// MapCorrectionRotation 재주입 + snapshot 재발행. MapViewModel.ChangeMapAsync가 호출.</summary>
    public void ResyncRotationDependents() => UpdateOverlaysAfterRotation();

    /// <summary>앵커(사이트 고정) 활성 여부.</summary>
    public bool IsAnchorActive => _anchorSiteRect != null;

    /// <summary>앵커에 의해 회전이 잠겼는가 — A모드(회전 비허용) 앵커 활성 시에만 true.
    /// B모드(AllowRotation) 앵커는 회전 허용이므로 false. 툴바 버튼 비활성 바인딩용(V-06 옵션C).</summary>
    public bool IsRotationLockedByAnchor => _anchorSiteRect != null && !_anchorAllowsRotation;

    /// <summary>회전 kill-switch 토글(FR-03/18 단일 진실원) — 키보드(Ctrl+Shift+R)와 툴바 버튼 공용.
    /// OFF 전환 = 즉시 정북 복귀(0은 게이트 무관 항상 허용 — 안전 복구 경로). 반환=새 상태.</summary>
    public bool ToggleRotationFeature()
    {
        Utils.RotationFeature.IsEnabled = !Utils.RotationFeature.IsEnabled;
        if (!Utils.RotationFeature.IsEnabled) ResetRotation();
        _log?.Info($"[Rotation] kill-switch 토글: {(Utils.RotationFeature.IsEnabled ? "ON — Shift+휠/Ctrl+←→ 회전 가능" : "OFF — 정북 복귀·입력 차단")}");
        return Utils.RotationFeature.IsEnabled;
    }

    #endregion

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
            // [R-26 / FR-05 동적추가 replay] 회전된 맵에 늦게 추가된 오버레이도 즉시 −θ 보정 —
            // 종전에는 UpdateOverlaysAfterRotation에서만 세팅돼 다음 회전 변경까지 desync였다.
            customImage.MapCorrectionRotation = -MapRotation;
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

            // 진행 중 러버밴드 취소 + 마퀴 어도너 제거(편집 종료 시 잔존 방지)
            if (_isRubberBanding) { _isRubberBanding = false; _rubberStart = null; _rubberCurrent = null; if (IsMouseCaptured) ReleaseMouseCapture(); }
            HideRubberBand();

            // 편집 모드 해제 시 모든 선택 해제
            foreach (var img in CustomImages) img.IsSelected = false;
            //foreach (var marker in CustomMarkers) marker.IsSelected = false;
            foreach (var marker in Markers.OfType<IEditableMarker>()) marker.IsSelected = false;   // 불변식#8: 하드캐스트 크래시 방지

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
        // [Rotation R-33] ToEven(기본) 대신 AwayFromZero — .5 경계에서 전진 보장
        return Math.Round(angle / RotationSnapAngle, MidpointRounding.AwayFromZero) * RotationSnapAngle;
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
    /// 오버레이 이미지(AABB)의 '중심'을 화면 픽셀 격자에 스냅한다. (RC-4 / FR-11)
    /// 마커 스냅과 동일한 ComputeOrigin/Snap 사용 → 보이는 격자선/교점에 흡착.
    /// 스냅 비활성이거나 맵 회전(MapRotation≠0) 시 원본 그대로 반환(FR-12).
    /// </summary>
    private RectLatLng SnapBoundsCenter(RectLatLng bounds)
    {
        if (!IsSnapToGridEnabled || Math.Abs(MapRotation) > 0.1)
            return bounds;

        // AABB 중심 (Lat=상단/최대, Lng=좌측/최소 → 중심은 -H/2, +W/2)
        double centerLat = bounds.Lat - bounds.HeightLat / 2.0;
        double centerLng = bounds.Lng + bounds.WidthLng / 2.0;

        var centerLocal = FromLatLngToLocal(new PointLatLng(centerLat, centerLng));
        var gridPx = SnapGridOverlayService.EffectiveGridPx(GridSizePx);
        // 맵 고정 격자: DrawGrid/마커스냅과 동일한 지오 앵커 기반 원점 (RC-7/FR-16)
        var (x0, y0) = SnapGridOverlayService.ComputeOrigin(this, gridPx);
        var (sx, sy, snapX, snapY) = SnapGridOverlayService.Snap(
            centerLocal.X, centerLocal.Y, gridPx, x0, y0);

        if (!snapX && !snapY)
            return bounds;

        var snappedCenter = FromLocalToLatLng((int)Math.Round(sx), (int)Math.Round(sy));
        // 중심 → 좌상단 재구성 (상단 = 중심 + H/2, 좌측 = 중심 - W/2)
        double newLat = snappedCenter.Lat + bounds.HeightLat / 2.0;
        double newLng = snappedCenter.Lng - bounds.WidthLng / 2.0;
        return new RectLatLng(newLat, newLng, bounds.WidthLng, bounds.HeightLat);
    }

    /// <summary>
    /// 비율 유지하며 이미지 크기 조정
    /// </summary>
    private RectLatLng ResizeBoundsWithRatio(RectLatLng bounds, double deltaX, double deltaY, ResizeHandle corner)
    {
        GPoint tlGP = FromLatLngToLocal(bounds.LocationTopLeft);
        GPoint brGP = FromLatLngToLocal(bounds.LocationRightBottom);
        // [Rotation FR-07 / R-25] 종전 대각 성분차(brGP−tlGP)는 회전 시 부호 반전 → `<=2` 조기
        // 반환으로 모서리 리사이즈가 무반응(no-op)이 되던 결함 — 크기는 회전 불변 유클리드 거리로
        // 산출(brGP는 후속 스케일 기준점 계산에만 사용).
        GPoint trGP = FromLatLngToLocal(new PointLatLng(bounds.Lat, bounds.Lng + bounds.WidthLng));
        GPoint blGP = FromLatLngToLocal(new PointLatLng(bounds.Lat - bounds.HeightLat, bounds.Lng));
        double curW = ScreenDist(tlGP, trGP);
        double curH = ScreenDist(tlGP, blGP);
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
    /// 두 점을 연결하는 선에 맞춰 회전 — [FR-01 v1 비활성] 회전 SSOT 정비 전까지 다중 진입점
    /// 축소(PRD GMap_Rotation_Full_Sync §3.4). 스크린 좌표 기반 각도 산출이 회전 상태에서
    /// 자기참조(현재 회전 포함 투영)라 검증 전 사용 금지. 필요 시 후속 FR로 재설계.
    /// </summary>
    public void AlignToLine(PointLatLng point1, PointLatLng point2)
    {
        _log?.Warning($"AlignToLine 비활성(v1) — 회전 SSOT 정비 전 다중 진입점 차단(FR-01)");
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
    // [임시 진단 제거] 팬 계측용 필드 (LogOverlayDesyncDiag 전용 — 위 블록주석과 함께 해제)
    //private bool _prevDragging;
    //private int _panSkipCount;
    //private DateTime _panStartTime;

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
    private GMap.NET.RectLatLng _dragStartBounds;      // Undo before-state(D1) — 이미지 편집 시작 시 Bounds
    private double _dragStartUserRotation;             // Undo before-state(D1) — 이미지 편집 시작 시 UserRotation
    private const double ROTATE_HANDLE_DISTANCE = 30; // 상단 중앙에서 위쪽 오프셋(px)
    #endregion
}
