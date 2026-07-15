using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;   // IPermissionService (FR-EN-06 권한 게이팅)
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using GMap.NET;
using GMap.NET.MapProviders;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using System.IO;
using System.Threading;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Libraries.Enums;
using GMap.NET.MapProviders.Custom;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Ptz;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Libraries.Streaming.Models;
using System.Windows.Controls;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using CoordinateSharp;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Org.BouncyCastle.Crypto.Macs;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using System.Windows;
using Google.Protobuf.WellKnownTypes;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Newtonsoft.Json.Linq;
using System.Windows.Data;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapMilitary;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapRoi;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System.Collections.ObjectModel;
using System.Windows.Threading;


namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : GIS 지도 제어 및 편집 기능을 제공하는 주요 ViewModel 
   Created By   : GHLee                                                
   Created On   : 7/22/2025 2:59:21 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public partial class MapViewModel : BasePanelViewModel,
                            IHandle<AllDevicesLoadedMessage>,
                            IHandle<CallDeleteMapRoiProcessMessageModel>,
                            IHandle<CallDeleteMapLayerProcessMessageModel>,
                            IHandle<CallDeleteGroupSymbolsProcessMessageModel>,
                            IHandle<CallDeleteSelectedProcessMessageModel>,
                            IHandle<ZOrderChangeRequestedEvent>
                            //, IHandle<PropertyPanelCloseRequestedEvent>
                            //, IHandle<MarkerPropertyChangedEventArgs>
{
    #region - 상수 정의 -
    public const int ZOOM_MAX = 19;
    public const int ZOOM_MIN = 6;
    public const double DEFAULT_ZOOM = 15d;
    public const int SENSOR_COVERAGE = 200;
    public const int MaxTimeDifference = 60 * 5;
    public ICoordinateModel DEFAULT_LOCATION = new CoordinateModel(37.648425, 126.904284);
    #endregion

    #region - 생성자 -
    /// <summary>
    /// MapViewModel 생성자 - 의존성 주입을 통한 초기화
    /// </summary>
    public MapViewModel(ILogService log
                        , IEventAggregator eventAggregator
                        , GMapSetupModel setupModel
                        , MapProvider mapProvider
                        , CustomMapService customMapService
                        , IGMapDbSymbolService gMapDbSymbolService
                        , SymbolProvider symbolProvider
                        , GeometricSymbolProvider geoSymbolProvider
                        , DeviceProvider deviceProvider
                        , ImageOverlayService imageOverlayService
                        , MarkerFactory markerFactory
                        , PropertyPanelFactory propertyPanelFactory
                        , SymbolEventManager symbolEventManager
                        , IDeviceDetailUrlService deviceDetailUrlService
                        , IBroadcastControlService broadcastControlService
                        , ICameraAimControlService cameraAimControlService
                        , ITrackingSetupModel trackingSetupModel
                        , IGMapDbService gMapDbService
                        , CustomMapOverlayService customMapOverlayService
                        , IImageFileService imageFileService
                        , Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.IEditRecorder editRecorder
                        , Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.IUndoService undoService
                        , Ironwall.Dotnet.Libraries.SystemResources.Services.ISystemResourceMonitor resourceMonitor
                        , TrackingOverlayManager? trackingOverlay = null
                        , PlaybackViewModel? playbackVm = null
                        , TrackingSetupViewModel? trackingSetupVm = null
                        ) : base(eventAggregator, log)
    {
        _cts = new CancellationTokenSource();
        _mapProvider = mapProvider;
        _gMapDbSymbolService = gMapDbSymbolService;
        _gMapDbService = gMapDbService;
        _symbolProvider = symbolProvider;
        _setupModel = setupModel;
        _customMapService = customMapService;
        _imageOverlayService = imageOverlayService;
        _markerFactory = markerFactory;
        _propertyPanelFactory = propertyPanelFactory;
        _symbolEventManager = symbolEventManager;
        _deviceDetailUrlService = deviceDetailUrlService;
        _broadcastControlService = broadcastControlService;
        _cameraAimControlService = cameraAimControlService;
        _trackingSetupModel = trackingSetupModel;
        _customMapOverlayService = customMapOverlayService;
        _imageFileService = imageFileService;
        _editRecorder = editRecorder;
        _undoService = undoService;
        _resourceMonitor = resourceMonitor;
        _trackingOverlay = trackingOverlay;
        _playbackVm = playbackVm;
        _trackingSetupVm = trackingSetupVm;
        DeviceProvider = deviceProvider;
        InitializeCommands();
        InitializeUndoRedo();
    }
    #endregion

    #region - 라이프사이클 오버라이드 -
    /// <summary>
    /// 뷰가 연결될 때 MainMap 컨트롤 설정 및 회전 속성 동기화
    /// </summary>
    protected override void OnViewAttached(object view, object context)
    {
        base.OnViewAttached(view, context);
        if (view is MapView mapView && mapView.MainMap != null)
        {
            MainMap = mapView.MainMap;
            _log?.Info($"MainMap 참조 설정 완료: {MainMap.GetHashCode()}");

            // Tracking GIS 오버레이 매니저를 지도에 연결(TTL 스윕 기동). NATS 핸들러가 이 인스턴스로 마커 반영
            _trackingOverlay?.Attach(MainMap);
            _playbackVm?.AttachMap(MainMap);   // Playback(P5) 재생 오버레이도 같은 지도에 연결

            // Adorner 시스템 통합
            SetupAdornerIntegration();

            // LineDrawingService 초기화 추가!
            InitializeLineDrawingService();


            // 회전 속성 동기화
            SyncRotationProperties();

            // 오버레이 맵 Canvas — GMapCustomControl.OnRender에서 DrawingContext로 렌더링
            // base.OnRender(타일) → RenderOverlayMapTiles(오버레이) → 심볼(ItemsPresenter)
            var overlayCanvas = new Canvas { IsHitTestVisible = false };
            MainMap.OverlayMapCanvas = overlayCanvas;
            _customMapOverlayService.Initialize(overlayCanvas);

            _log?.Info("MapViewModel과 뷰 연결 완료");
        }
    }

    /// <summary>
    /// ViewModel 활성화 시 비동기 초기화 작업 수행
    /// </summary>
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.OnActivateAsync(cancellationToken);

            // ★ 디바이스-심볼 재매칭 신뢰성(재시작·재로그인 연결 끊김 근본수정): EventAggregator 구독을 활성화 맨 앞으로 이동.
            //   심볼은 로컬DB에서 로그인 무관하게 일찍 로드되나, 디바이스는 로그인 후 늦게 fetch됨(로그인 게이팅).
            //   구독이 뒤(구 위치)에 있으면 그 사이 발행되는 AllDevicesLoadedMessage를 놓치거나(레이스),
            //   중간 await 예외로 구독 자체가 스킵돼 세션 내내 재매칭이 안 되던 결함. → 맨 앞 구독으로 어떤
            //   로드 순서·중간 예외에도 재매칭 신호를 확실히 수신(재로그인 재fetch도 동일 커버). 재매칭은 멱등(304 Dispose+재구성).
            _eventAggregator.SubscribeOnPublishedThread(this);

            // FR-EN-11: 역할강등/세션변경 시 PTZ 권한 재평가 구독(진행 중 이동 취소·팝업 비활성)
            SubscribePtzPermission();

            // 툴바 우측 CPU/GPU/RAM 사용률 표시 시작(UI DispatcherTimer로 Sample 구동)
            StartResourceMonitor();

            // 1. 저장된 커스텀 맵들 로드
            await _customMapService.LoadCustomMapsAsync();

            // 2. 지도 설정 (초기 로드 — MBTiles center로 이동)
            await MapConfigureAsync(isInitialLoad: true);

            // 3. 심볼 설정
            await SymbolConfigureAsync();

            // 4. 이미지 오버레이 설정 (Phase 28)
            await ImageConfigureAsync();

            // 4.5. 기존 CustomMap → MapLayers 마이그레이션 + 오버레이 복원
            await SeedAndRestoreOverlayMapsAsync();

            // 4.5.1. OverlayImage → MapLayers Seed
            await SeedOverlayImageLayersAsync();

            // 4.5.2. 저장된 레이어 가시성 복원 — 트리 빌드 즉시, 마커 복원은 ApplicationIdle 후
            // ① 트리 빌드: _layerTreeNodes 확보 (DB 조회, 즉시 실행)
            await LoadLayersFromDbAsync();
            // ② 마커 복원: 모든 렌더링 완료 후 적용 → startup flash 방지 + UpdateMarkersVisibilityByZoom 경쟁 없음
            await System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(
                RestoreLayerVisibility,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            // 4.6. 오버레이 첫 렌더링 — 맵 로드 완료 후 실행
            if (_customMapOverlayService.ActiveOverlays.Any())
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new System.Action(() =>
                    {
                        if (MainMap?.ViewArea.WidthLng > 0)
                        {
                            _log?.Info($"[Overlay] 초기 렌더링 트리거 — ViewArea={MainMap.ViewArea}");
                            _customMapOverlayService.RefreshVisibleTiles(MainMap);
                        }
                        else
                        {
                            _log?.Info("[Overlay] ViewArea 아직 미유효 — OnTileLoadComplete 대기");
                            MainMap.OnTileLoadComplete += OnFirstTileLoadForOverlay;
                        }
                    }));
            }

            // 5. ComboBox 초기 선택 알림
            NotifyOfPropertyChange(nameof(AvailableMaps));
            NotifyOfPropertyChange(nameof(SelectedMapItem));

            // 빈 타일 버그 해결됨 — MBTilesMapProvider MinZoom/MaxZoom shadowing 제거 (2026-03-24)
            // (구독은 활성화 맨 앞으로 이동 — 위 SubscribeOnPublishedThread 참조. 재매칭 신호 유실 방지)
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
    }



    /// <summary>
    /// ViewModel 비활성화 시 리소스 정리
    /// </summary>
    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        try
        {
            UnsubscribePtzPermission();   // FR-EN-11 PTZ 권한 재평가 구독 해제
            StopResourceMonitor();          // 시스템 리소스 타이머 정지(모든 경로 — close 무관, 이중구독/leak 방지)

            // (줌 디바운스 타이머 제거됨 — OnMapZoomChanged에서 직접 RefreshVisibleTiles 호출)

            // Adorner 시스템 정리
            CleanupAdornerIntegration();

            // 열린 카메라 RTSP 팝업 정리(Hub Lease/스트림 해제) — 누수 방지(H-1)
            if (_cameraPopups != null)
            {
                foreach (var popup in _cameraPopups.ToList())
                    await CloseCameraPopupAsync(popup);
            }

            if (close)
            {
                // GMap.NET CacheEngine은 IsBackground=false 포그라운드 스레드 → 미호출 시 프로세스 영구 잔존
                GMap.NET.GMaps.Instance.CancelTileCaching();

                // WPF 디스패처가 살아있는 동안 오버레이 타일 로드 취소 + Canvas 정리
                // Autofac 컨테이너 해제(Task.Run 백그라운드)까지 기다리지 않고 즉시 처리하여
                // Dispatcher.Invoke 셧다운 교착 방지
                _customMapOverlayService?.Dispose();

                // AdornerManagerService 내부 5분 주기 TrimMemory 타이머 정지
                // 미호출 시 앱 종료 후에도 백그라운드 스레드에서 계속 발화함
                MainMap?.AdornerManager?.Dispose();
            }

            // 모든 커스텀 맵 비활성화
            _customMapService.DeactivateAllCustomMaps();

            await base.OnDeactivateAsync(close, cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Error($"MapViewModel 비활성화 실패: {ex.Message}");
        }
    }

    private Task InitializeDeviceSymbolIntegration()
    {
        try
        {
            var devices = DeviceProvider.ToList();
            var symbols = MainMap?.Markers.ToList();
            _symbolEventManager.Dispose();
            var registeredCount = 0;
            foreach (var device in devices)
            {
                // 1차: DeviceType 완전 일치
                var symbol = symbols?.OfType<GMapPidsMarker>()
                    .FirstOrDefault(s => s.LinkedDeviceId == device.Id
                    && s.DeviceType == device.DeviceType);

                // 2차 fallback: DeviceType 불일치(DB 심볼 타입 ≠ NATS 실제 타입) — ID만으로 매칭
                // 예) DB 심볼=SmartSensor(11), NATS 장비=SmartSensor2(12)
                if (symbol == null)
                {
                    symbol = symbols?.OfType<GMapPidsMarker>()
                        .FirstOrDefault(s => s.LinkedDeviceId == device.Id);
                    if (symbol != null)
                        _log?.Warning($"장비-심볼 매핑 fallback: Device({device.Id},{device.DeviceType}) → Symbol.DeviceType={symbol.DeviceType}");
                }

                if (symbol != null)
                {
                    // ★ 런타임 LinkedDevice 객체 재바인딩 — 카메라/제어기 재시작 연결끊김 근본수정(조사 결함1).
                    //   심볼 로드(MarkerFactory) 시점엔 로그인-게이팅으로 DeviceProvider가 비어 LinkedDevice=null이 됨.
                    //   여기(디바이스 로드 후 매칭 성공 지점)에서 실제 device 객체를 다시 붙여야 속성창 콤보·PTZ·
                    //   홈페이지 메뉴·'현재위치 적용' 등 객체 의존 기능이 복구됨. LinkedDeviceId는 이미 DB에서 정상 로드.
                    //   (그룹은 LinkedDeviceGroup 정수 링크라 객체 해석이 불필요 → 이 문제 없음)
                    symbol.LinkedDevice = device;
                    _symbolEventManager.RegisterDeviceSymbol(device, symbol.Model);
                    registeredCount++;
                }

                // 복수 그룹 지원: 각 DeviceGroup에 대해 그룹 심볼 매핑
                if (device.DeviceGroups != null)
                {
                    foreach (var groupId in device.DeviceGroups)
                    {
                        var groupSymbol = symbols?.OfType<GMapPidsGroupMarker>()
                            .FirstOrDefault(s => s.LinkedDeviceGroup == groupId);
                        if (groupSymbol != null)
                        {
                            _symbolEventManager.RegisterGroupSymbol(groupId, device, groupSymbol.Model);
                            //_log?.Info($"그룹-심볼 매핑: DeviceGroup({groupId}) <-> {groupSymbol.Title}");
                        }
                    }
                }
            }

            _log?.Info($"심볼 등록 완료: 개별 {registeredCount}건 / 전체 {devices.Count}건 (FenceGroup 전용: {devices.Count - registeredCount}건)");
        }
        catch (Exception ex)
        {
            _log?.Error($"장비-심볼 매핑 실패: {ex.Message}");
        }

        // 시작(로그인 후 최종 로드) 시 undo 이력 초기화 — 시드 오버레이/심볼 등록 등 부팅 중 기록된 항목이
        // 사용자 편집으로 오인돼 Undo되는 사고 방지(예: 이미지 오버레이 소실). 재로그인마다 깨끗한 상태.
        ClearUndoStack();

        return Task.CompletedTask;
    }

    /// <summary>
    /// 전체 Device 로딩 완료 시 Device-Symbol 매핑을 재실행합니다.
    /// 지도 활성화 시점보다 Device 로딩이 늦게 완료되는 경우를 대비합니다.
    /// </summary>
    public async Task HandleAsync(AllDevicesLoadedMessage message, CancellationToken cancellationToken)
    {
        await InitializeDeviceSymbolIntegration();
        EnsureUniqueZOrder();
    }

    private void InitializeLineDrawingService()
    {
        if (MainMap == null) return;

        _lineDrawingService = MainMap.LineDrawingService;

        if (_lineDrawingService != null)
        {
            // 이벤트 구독
            _lineDrawingService.StateChanged += OnLineDrawingStateChanged;
            _lineDrawingService.PointAdded += OnLinePointAdded;
            _lineDrawingService.LineCompleted += OnLineCompleted;
            _lineDrawingService.DrawingCancelled += OnLineDrawingCancelled;

            _log?.Info("LineDrawingService 이벤트 구독 완료");
        }
        else
        {
            _log?.Warning("LineDrawingService가 null입니다");
        }
    }
    #endregion

    #region - Adorner 시스템 통합 -
    /// <summary>
    /// Adorner 시스템 통합 설정
    /// </summary>
    private void SetupAdornerIntegration()
    {
        if (MainMap?.AdornerManager == null)
        {
            _log?.Error("MainMap 또는 AdornerManager가 null입니다!");
            return;
        }

        try
        {
            _log?.Info("Adorner 시스템 통합 시작");
            // GMapCustomControl 이벤트 구독
            MainMap.OnMarkerClicked += OnMapMarkerClicked;
            MainMap.OnMarkerRightClicked += OnMapMarkerRightClicked;
            MainMap.OnMarkerDoubleClicked += OnMapMarkerDoubleClicked;   // RTSP 카메라 팝업
            MainMap.Markers.CollectionChanged += Markers_CollectionChangedForCameraPopups;  // FR-13 심볼 제거 시 팝업 닫기
            MainMap.OnImageClicked += OnMapImageClicked;
            MainMap.OnImageRightClicked += OnMapImageRightClicked;       // FR-9 이미지 우클릭 메뉴
            MainMap.OnImageEditCompleted += OnMapImageEditCompleted;     // FR-8 편집 완료 DB 영속화
            MainMap.DigitalZoomLevelChanged += OnMapDigitalZoomLevelChanged; // 디지털 줌 → 축척바 갱신
            MainMap.OnMapClicked += OnMapClicked;
            MainMap.TargetAimClicked += OnTargetAimClicked;                  // 카메라 특정위치 확인 — 좌클릭 좌표 수신
            MainMap.SymbolPlacementClicked += OnSymbolPlacementClicked;      // 추가 버튼 배치 모드 — 좌클릭 위치에 심볼 추가(#4)
            MainMap.PreviewKeyDown += OnMapPreviewKeyDownForAim;             // ESC 취소

            // 그룹(러버밴드) 다중선택 (GMap_RubberBand_MultiSelect) — AdornerManager 밖 전용 서비스
            _groupSelection = new GroupSelectionService(MainMap, _log);
            MainMap.RubberBandStarted += OnRubberBandStarted;
            MainMap.MarkersRubberBandSelected += OnMarkersRubberBandSelected;
            MainMap.MarkerToggleRequested += OnMapMarkerToggleRequested;   // Ctrl+클릭 토글
            MainMap.PreviewKeyDown += OnMapPreviewKeyDownForGroup;           // Del=그룹 삭제
            EnsureGroupKeyWindowHook();                                       // 방향키 등은 윈도우서도 후킹(포커스 무관, #7)
            _groupSelection.GroupMoveCompleted += OnGroupMoveCompleted;
            _groupSelection.GroupDeleteRequested += OnGroupDeleteRequested;
            _groupSelection.GroupLockRequested += OnGroupLockRequested;
            _groupSelection.GroupVisibilityRequested += OnGroupVisibilityRequested;
            _groupSelection.GroupZOrderRequested += OnGroupZOrderRequested;

            // 심볼 라벨 분리 오버레이 (Symbol_Label_Decouple) — AdornerManager 밖 전용 서비스
            _labelService = new LabelAdornerService(MainMap, _log);
            _labelService.LabelOffsetChanged += OnLabelOffsetChanged;   // 라벨 드래그 → 오프셋 DB 영속(FR-LB-05)
            MainMap.Markers.CollectionChanged += Markers_CollectionChangedForLabels;
            _labelService.Sync(MainMap.Markers);   // 이미 로드된 마커 부착

            _log?.Info("GMapCustomControl 이벤트 구독 완료");
            // AdornerManager 이벤트 구독
            MainMap.MarkerEditStarted += OnMarkerEditStarted;
            MainMap.MarkerEditCompleted += OnMarkerEditCompleted;
            MainMap.MarkerEditCancelled += OnMarkerEditCancelled;

            _log?.Info("AdornerManager 이벤트 구독 완료");
            MainMap.AdornerCreated += OnAdornerCreated;
            MainMap.AdornerRemoved += OnAdornerRemoved;

            // 다중 선택 모드 설정 (기본값: 단일 선택)
            MainMap.SetMultiSelectMode(false);

            _log?.Info("Adorner 시스템 통합 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 시스템 통합 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Adorner 시스템 정리
    /// </summary>
    private void CleanupAdornerIntegration()
    {
        if (MainMap == null) return;

        try
        {
            // 타겟 조준 모드가 켜진 채 뷰가 비활성화되면 전역 커서(Cursors.Cross)·오버레이가 잔존 → 강제 종료(H1)
            ExitTargetAimMode();

            // 이벤트 구독 해제
            MainMap.OnMarkerClicked -= OnMapMarkerClicked;
            MainMap.OnMarkerRightClicked -= OnMapMarkerRightClicked;
            MainMap.OnMarkerDoubleClicked -= OnMapMarkerDoubleClicked;
            MainMap.Markers.CollectionChanged -= Markers_CollectionChangedForCameraPopups;
            MainMap.OnImageClicked -= OnMapImageClicked;
            MainMap.OnImageRightClicked -= OnMapImageRightClicked;
            MainMap.OnImageEditCompleted -= OnMapImageEditCompleted;
            MainMap.DigitalZoomLevelChanged -= OnMapDigitalZoomLevelChanged;
            MainMap.OnMapClicked -= OnMapClicked;
            MainMap.TargetAimClicked -= OnTargetAimClicked;
            MainMap.SymbolPlacementClicked -= OnSymbolPlacementClicked;
            MainMap.PreviewKeyDown -= OnMapPreviewKeyDownForAim;
            if (_aimEscWindow != null) { _aimEscWindow.PreviewKeyDown -= OnMapPreviewKeyDownForAim; _aimEscWindow = null; }

            // 그룹(러버밴드) 다중선택 정리
            MainMap.RubberBandStarted -= OnRubberBandStarted;
            MainMap.MarkersRubberBandSelected -= OnMarkersRubberBandSelected;
            MainMap.MarkerToggleRequested -= OnMapMarkerToggleRequested;
            if (_groupKeyWindow != null) { _groupKeyWindow.PreviewKeyDown -= OnMapPreviewKeyDownForGroup; _groupKeyWindow = null; }
            MainMap.PreviewKeyDown -= OnMapPreviewKeyDownForGroup;
            if (_groupSelection != null)
            {
                _groupSelection.GroupMoveCompleted -= OnGroupMoveCompleted;
                _groupSelection.GroupDeleteRequested -= OnGroupDeleteRequested;
                _groupSelection.GroupLockRequested -= OnGroupLockRequested;
                _groupSelection.GroupVisibilityRequested -= OnGroupVisibilityRequested;
                _groupSelection.GroupZOrderRequested -= OnGroupZOrderRequested;
                _groupSelection.Dispose();
                _groupSelection = null;
            }

            // 심볼 라벨 분리 오버레이 정리
            MainMap.Markers.CollectionChanged -= Markers_CollectionChangedForLabels;
            if (_labelService != null) _labelService.LabelOffsetChanged -= OnLabelOffsetChanged;
            _labelService?.Dispose();
            _labelService = null;

            MainMap.MarkerEditStarted -= OnMarkerEditStarted;
            MainMap.MarkerEditCompleted -= OnMarkerEditCompleted;
            MainMap.MarkerEditCancelled -= OnMarkerEditCancelled;
            MainMap.AdornerCreated -= OnAdornerCreated;
            MainMap.AdornerRemoved -= OnAdornerRemoved;

            // 모든 선택 해제
            MainMap?.DeselectAllMarkers();

            _log?.Info("Adorner 시스템 정리 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"Adorner 시스템 정리 실패: {ex.Message}");
        }
    }

    // 심볼 라벨 분리 오버레이 서비스 (Symbol_Label_Decouple Phase 2) — AdornerManager 밖 소유.
    private Ironwall.Dotnet.Libraries.GMaps.Ui.Services.LabelAdornerService? _labelService;
    /// <summary>마커 추가/제거 시 라벨 adorner 동기화(신규 부착·제거 detach).</summary>
    private void Markers_CollectionChangedForLabels(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => _labelService?.Sync(MainMap?.Markers);

    /// <summary>라벨 드래그 완료 → 심볼 모델 DB 영속(LabelOffsetX/Y 포함). RBAC 게이트(FR-LB-05).</summary>
    private async void OnLabelOffsetChanged(IEditableMarker marker, double beforeX, double beforeY)
    {
        if (marker == null || !CanEditMap()) return;
        try { await DbUpdateProcess(marker); _editRecorder?.RecordLabelOffset(marker, beforeX, beforeY); }   // before=드래그 시작 오프셋(선택 무관 정확)
        catch (Exception ex) { _log?.Error($"라벨 오프셋 영속 실패: {ex.Message}"); }
    }
    #endregion

    #region - 카메라 특정위치 확인 (타겟 조준 모드, Camera_PTZ_AimLocation) -

    private string _aimStatusMessage = string.Empty;
    /// <summary>타겟 조준 모드 상태 배너 텍스트.</summary>
    public string AimStatusMessage
    {
        get => _aimStatusMessage;
        set { _aimStatusMessage = value; NotifyOfPropertyChange(nameof(AimStatusMessage)); }
    }

    private bool _isAimStatusVisible;
    /// <summary>타겟 조준 모드 상태 배너 표시 여부.</summary>
    public bool IsAimStatusVisible
    {
        get => _isAimStatusVisible;
        set { _isAimStatusVisible = value; NotifyOfPropertyChange(nameof(IsAimStatusVisible)); }
    }

    /// <summary>타겟 조준 모드 진입 — 컨텍스트 메뉴 "특정 위치 확인" 클릭(동기, async 없음 — async void 함정 회피).</summary>
    private void EnterTargetAimMode(GMapPidsMarker marker)
    {
        try
        {
            if (MainMap == null || marker == null) return;
            var cam = marker.LinkedDevice as ICameraDeviceModel;
            if (cam == null) { SetAimStatus("연결된 카메라가 없어 '특정 위치 확인'을 사용할 수 없습니다.", autoHide: true); return; }

            // 중심 = 심볼 위치(FOV 부채꼴과 동일 중심). NaN/범위초과 + 미설정(0,0) 거부
            double centerLat = marker.Latitude, centerLng = marker.Longitude;
            if (!Services.Tracking.TrackingMath.IsValidLatLng(centerLat, centerLng)
                || (Math.Abs(centerLat) < 1e-6 && Math.Abs(centerLng) < 1e-6))
            {
                SetAimStatus("카메라 설치 좌표가 없어 '특정 위치 확인'을 사용할 수 없습니다.", autoHide: true);
                return;
            }

            // 모드 상호배제 — 라인드로잉/편집 강제 종료(한 번에 하나의 맵 인터랙션 모드)
            CancelConflictingModesForAim();

            _aimCamera = cam;
            _aimCenter = new PointLatLng(centerLat, centerLng);   // 심볼 위치 = 원·히트테스트·메시지 공통 중심(중심 불일치 방지)
            // 반경 우선순위: ①카메라 최대탐지거리(HardwareSpec.MaxDetectionRange) → ②심볼 탐지범위(DetectionRange) → ③글로벌 폴백
            _aimRadiusMeters = Services.Tracking.CameraAimMath.ResolveAimRadius(
                cam.HardwareSpec?.MaxDetectionRange,
                marker.DetectionRange,
                _trackingSetupModel?.CameraAimRadiusMeters ?? 30d);
            unchecked { _aimGeneration++; }

            MainMap.AimOverlayCenter = _aimCenter;
            MainMap.AimOverlayRadiusMeters = _aimRadiusMeters;
            MainMap.IsTargetAimMode = true;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
            EnsureAimEscWindowHook();        // ESC: 윈도우 레벨 후킹(맵이 포커스를 못 받아도 수신)
            MainMap.Focus();                 // 보조 — 맵 포커스 확보 시도
            MainMap.InvalidateVisual();

            SetAimStatus($"특정 위치 확인 — 탐지범위 {_aimRadiusMeters:F0}m 안을 클릭하세요. (ESC·영역 밖 클릭 = 취소)");
            _log?.Info($"[CameraAim] 타겟 모드 진입 cam={cam.Id} R={_aimRadiusMeters:F0}m (DetectionRange)");
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraAim] 모드 진입 실패: {ex.Message}");
            ExitTargetAimMode();
        }
    }

    /// <summary>타겟 조준 모드 종료(취소/완료 공통) — 커서·오버레이·세션 정리.</summary>
    private void ExitTargetAimMode()
    {
        try
        {
            unchecked { _aimGeneration++; }
            _aimCamera = null;
            if (MainMap != null)
            {
                MainMap.IsTargetAimMode = false;
                MainMap.AimOverlayCenter = null;
                MainMap.AimOverlayRadiusMeters = 0d;
                MainMap.InvalidateVisual();
            }
            if (System.Windows.Input.Mouse.OverrideCursor == System.Windows.Input.Cursors.Cross)
                System.Windows.Input.Mouse.OverrideCursor = null;
            IsAimStatusVisible = false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraAim] 모드 종료 실패: {ex.Message}");
        }
    }

    // ─────────────── 심볼 배치 모드(#4 — 추가 버튼 → 클릭으로 배치) ───────────────
    private EnumMarkerCategory _placeCategory;
    private object? _placeType;
    private string? _placeTitle;

    /// <summary>추가 버튼 → 배치 모드 진입(타겟조준 패턴). 커서 십자 + 다음 좌클릭 위치에 심볼 추가. 편집모드는 유지.</summary>
    private void EnterSymbolPlacementMode(EnumMarkerCategory category, object type, string title)
    {
        if (MainMap == null || type == null) return;
        // 충돌 모드 정리(타겟조준·라인드로잉). 배치는 편집모드와 공존(편집모드 종료 안 함).
        if (MainMap.IsTargetAimMode) ExitTargetAimMode();
        if (MainMap.IsLineDrawing) _ = MainMap.LineDrawingService?.CancelDrawingAsync();
        _placeCategory = category; _placeType = type; _placeTitle = title;
        MainMap.IsSymbolPlacementMode = true;
        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Cross;
        EnsureAimEscWindowHook();   // ESC 취소(윈도우 후킹 재사용)
        MainMap.Focus();
        SetAimStatus($"클릭한 위치에 '{title}' 배치 — (ESC = 취소)");
    }

    /// <summary>배치 모드 종료(취소/완료 공통) — 커서·상태·대기정보 정리.</summary>
    private void ExitSymbolPlacementMode()
    {
        _placeType = null; _placeTitle = null;
        if (MainMap != null) MainMap.IsSymbolPlacementMode = false;
        if (System.Windows.Input.Mouse.OverrideCursor == System.Windows.Input.Cursors.Cross)
            System.Windows.Input.Mouse.OverrideCursor = null;
        IsAimStatusVisible = false;
    }

    /// <summary>배치 모드 좌클릭 수신 — 클릭 위치에 대기 심볼 추가 후 모드 종료(단발).</summary>
    private async void OnSymbolPlacementClicked(PointLatLng geo, Point screen)
    {
        if (MainMap == null || !MainMap.IsSymbolPlacementMode) return;
        var category = _placeCategory; var type = _placeType; var title = _placeTitle ?? GetSymbolTitle();
        ExitSymbolPlacementMode();   // 단발 — 먼저 종료(재진입/중복 방지)
        if (type == null) return;
        try
        {
            switch (category)
            {
                case EnumMarkerCategory.BASIC_SHAPES:
                    if (type is string bt) await AddBasicShapeMarker(geo, bt, title);
                    break;
                case EnumMarkerCategory.GEOMETRICS:
                    if (type is EnumShapeType gt) await AddGeometricMarker(geo, gt, title);
                    break;
                case EnumMarkerCategory.PIDS_EQUIPMENT:
                    if (type is EnumDeviceType dt) await AddPidsMarker(geo, dt, title);
                    break;
                case EnumMarkerCategory.INFRASTRUCTURE:
                    if (type is string it) await AddInfraMarker(geo, it, title);
                    break;
            }
        }
        catch (Exception ex) { _log?.Error($"심볼 배치 실패: {ex.Message}"); }
    }

    /// <summary>타겟 모드 진입 시 충돌 모드(라인드로잉) 종료. (편집 모드 좌클릭은 타겟 분기가 선점)</summary>
    private void CancelConflictingModesForAim()
    {
        try
        {
            if (MainMap?.IsLineDrawing == true)
                _ = MainMap.LineDrawingService?.CancelDrawingAsync();
            // 편집 모드 종료 — 세터가 SetEditMode(false)+ClearAllSelections 수행, 편집 단축키/어도너 상호배제(M5)
            if (IsEditModeEnabled)
                IsEditModeEnabled = false;
        }
        catch (Exception ex) { _log?.Warning($"[CameraAim] 충돌 모드 종료 경고: {ex.Message}"); }
    }

    /// <summary>타겟 모드 좌클릭 수신 — 반경 판정 → 이내=발행 / 밖=취소(사용자 결정).</summary>
    private void OnTargetAimClicked(PointLatLng geo, Point screen)
    {
        if (MainMap == null || !MainMap.IsTargetAimMode) return;     // 방어
        var cam = _aimCamera;
        if (cam == null) { ExitTargetAimMode(); return; }

        // 중심 = 진입 시 스냅샷(_aimCenter, 심볼 위치) — 그려진 원과 동일 기준으로 판정(cam 디바이스 좌표 불일치 방지)
        bool inside = Services.Tracking.CameraAimMath
            .IsWithinRadius(_aimCenter.Lat, _aimCenter.Lng, geo.Lat, geo.Lng, _aimRadiusMeters);

        if (!inside)
        {
            _log?.Info($"[CameraAim] 반경({_aimRadiusMeters:F0}m) 밖 클릭 → 취소");
            ExitTargetAimMode();
            SetAimStatus("반경 밖을 클릭하여 취소했습니다.", autoHide: true);
            return;
        }

        var body = Services.Tracking.CameraAimRequestBuilder
            .Build(cam.Id, _aimCenter.Lat, _aimCenter.Lng, geo.Lat, geo.Lng, CurrentRequestedBy());
        if (body == null)
        {
            ExitTargetAimMode();
            SetAimStatus("좌표가 유효하지 않아 요청을 보낼 수 없습니다.", autoHide: true);
            return;
        }

        // 유효 클릭 지점 주황 리플(확대+페이드) — 전이 adorner라 아래 즉시 종료 후에도 재생(FR-AIM-03)
        MainMap?.TriggerAimRipple(geo);

        // 모드 즉시 종료(단발성) → 종료 후 세대 캡처. 발행 완료 시 그 사이 새 세션 진입했으면 안내 생략(M3)
        ExitTargetAimMode();
        _ = HandleTargetAimPublishAsync(body, _aimGeneration);
    }

    /// <summary>회전요청 발행(내부 예외 격리) — async void 함정 회피용 별도 async 메서드. gen=발행 시점 세대(stale 안내 방지).</summary>
    private async Task HandleTargetAimPublishAsync(CameraAimLocationBodyDto body, int gen)
    {
        try
        {
            await _cameraAimControlService.PublishAimAsync(body, _cts?.Token ?? CancellationToken.None)
                                          .ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                if (gen == _aimGeneration)   // 그 사이 새 타겟 세션이 시작됐으면 새 안내 보존
                    SetAimStatus($"카메라 {body.CameraId} → 회전요청 전송 ({body.DistanceM:F0}m, {body.BearingDeg:F0}°).", autoHide: true);
            });
        }
        catch (Exception ex)
        {
            _log?.Error($"[CameraAim] 발행 처리 실패: {ex.Message}");
            await OnUiAsync(() =>
            {
                if (gen == _aimGeneration)
                    SetAimStatus("회전요청 전송에 실패했습니다.", autoHide: true);
            });
        }
    }

    /// <summary>ESC 취소를 위해 윈도우 PreviewKeyDown을 1회 후킹(맵 컨트롤이 키보드 포커스를 못 받는 경우 대비). cleanup에서 해제.</summary>
    private void EnsureAimEscWindowHook()
    {
        try
        {
            if (_aimEscWindow != null || MainMap == null) return;
            _aimEscWindow = System.Windows.Window.GetWindow(MainMap);
            if (_aimEscWindow != null)
                _aimEscWindow.PreviewKeyDown += OnMapPreviewKeyDownForAim;
        }
        catch (Exception ex) { _log?.Warning($"[CameraAim] ESC 윈도우 후킹 경고: {ex.Message}"); }
    }

    private System.Windows.Window? _groupKeyWindow;

    /// <summary>그룹 단축키(방향키/Del/Ctrl+Z)를 윈도우 PreviewKeyDown에도 후킹 — 맵이 키보드 포커스를 못 받거나
    /// 포커스가 메뉴/다른 심볼로 이동해도 방향키 격자이동이 동작하도록(#7). 방향키는 WPF 포커스 이동키라 맵 단독 후킹은 불안정.</summary>
    private void EnsureGroupKeyWindowHook()
    {
        try
        {
            if (_groupKeyWindow != null || MainMap == null) return;
            void Hook()
            {
                if (_groupKeyWindow != null) return;
                var w = System.Windows.Window.GetWindow(MainMap);
                if (w != null) { _groupKeyWindow = w; _groupKeyWindow.PreviewKeyDown += OnMapPreviewKeyDownForGroup; }
            }
            Hook();
            if (_groupKeyWindow == null) MainMap.Loaded += (_, __) => Hook();
        }
        catch (Exception ex) { _log?.Warning($"[그룹키] 윈도우 후킹 경고: {ex.Message}"); }
    }

    /// <summary>ESC = 타겟 모드 취소.</summary>
    private void OnMapPreviewKeyDownForAim(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        if (MainMap?.IsTargetAimMode ?? false)
        {
            ExitTargetAimMode();
            SetAimStatus("취소했습니다.", autoHide: true);
            e.Handled = true;
        }
        else if (MainMap?.IsSymbolPlacementMode ?? false)   // 배치 모드도 ESC 취소(#4)
        {
            ExitSymbolPlacementMode();
            SetAimStatus("배치를 취소했습니다.", autoHide: true);
            e.Handled = true;
        }
        else if (MainMap?.IsHomePlacementMode ?? false)   // 홈 배치 모드 ESC 취소 (FR-H2)
        {
            ExitHomePlacementMode();
            SetAimStatus("홈 설정을 취소했습니다.", autoHide: true);
            e.Handled = true;
        }
        else if (MainMap?.IsAnchorDrawMode ?? false)   // 앵커 영역 그리기 ESC 취소 (FR-B3)
        {
            ExitAnchorDrawMode();
            SetAimStatus("영역 그리기를 취소했습니다.", autoHide: true);
            e.Handled = true;
        }
    }

    /// <summary>타겟 모드 상태 안내(상단 배너 + 로그). autoHide=true면 2.5초 후 숨김(세대 유지 시).</summary>
    private void SetAimStatus(string message, bool autoHide = false)
    {
        AimStatusMessage = message;
        IsAimStatusVisible = true;
        _log?.Info($"[CameraAim] {message}");
        if (!autoHide) return;

        var gen = _aimGeneration;
        _ = Task.Delay(2500).ContinueWith(_ =>
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (gen == _aimGeneration) IsAimStatusVisible = false;
                });
            }
            catch { /* 종료 중 Dispatcher 없음 — 무시 */ }
        });
    }

    #endregion

    #region - 그룹(러버밴드) 다중선택 (GMap_RubberBand_MultiSelect FR-MS-03~09) -
    private GroupSelectionService? _groupSelection;

    /// <summary>현재 그룹(러버밴드) 선택집합 — 그룹 이동/삭제/잠금 대상.</summary>
    public IReadOnlyList<IEditableMarker> SelectedMarkers => _groupSelection?.Selection ?? System.Array.Empty<IEditableMarker>();

    /// <summary>Shift+드래그 시작 — 단일 선택 중이었으면 그룹에 흡수(추가선택 유지) 후 단일 해제.</summary>
    private void OnRubberBandStarted()
    {
        if (SelectedMarker != null && !SelectedMarker.IsDisposed && !(_groupSelection?.HasSelection ?? false))
            _groupSelection?.SetSelection(new System.Collections.Generic.List<IEditableMarker> { SelectedMarker });
        SelectedMarker = null;
    }

    /// <summary>Shift+드래그 릴리스 — 사각형 내 마커를 기존 선택에 추가/토글 병합(겹치는 것만 토글, 나머지 유지). 빈 드래그면 유지.</summary>
    private void OnMarkersRubberBandSelected(IReadOnlyList<IEditableMarker> hits)
    {
        var ids = CurrentSelectionIds();
        if (hits != null)
            foreach (var h in hits)
                if (h != null && !h.IsDisposed && h.Id > 0)
                { var k = (h is GMapSymbols.GMapImageMarker, h.Id); if (!ids.Add(k)) ids.Remove(k); }   // 겹치면 해제(토글), 아니면 추가(타입인지)
        ApplyGroupSelectionByIds(ids);
        if (ids.Count > 0)
            SetAimStatus($"{ids.Count}개 선택 — 드래그=이동·Del=삭제 · Ctrl+클릭/Shift+드래그=추가·해제", autoHide: true);
    }

    /// <summary>Ctrl+클릭 — 해당 심볼/이미지마커를 그룹 선택에 토글(추가/해제, 나머지 유지).</summary>
    private void OnMapMarkerToggleRequested(IEditableMarker marker)
    {
        if (marker == null || marker.IsDisposed || marker.Id <= 0 || !IsEditModeEnabled) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        var ids = CurrentSelectionIds();
        var key = (marker is GMapSymbols.GMapImageMarker, marker.Id);
        if (!ids.Add(key)) ids.Remove(key);   // 이미 선택→해제, 아니면 추가(나머지 유지, 타입인지)
        ApplyGroupSelectionByIds(ids);
        SetAimStatus(ids.Count > 0 ? $"{ids.Count}개 선택(Ctrl+클릭·Shift+드래그로 추가·해제)" : "선택 해제", autoHide: true);
    }

    /// <summary>현재 선택 집합을 (타입,Id)로 수집(그룹 ∪ 단일). 이미지↔심볼 같은 Id 충돌 시 함께 잡히지 않게 타입 구분.
    /// (타입,Id) 기준이라 리로드로 인스턴스가 바뀌어도 안전.</summary>
    private System.Collections.Generic.HashSet<(bool isImage, int id)> CurrentSelectionIds()
    {
        var ids = new System.Collections.Generic.HashSet<(bool isImage, int id)>();
        if (_groupSelection?.Selection != null)
            foreach (var m in _groupSelection.Selection)
                if (m != null && m.Id > 0) ids.Add((m is GMapSymbols.GMapImageMarker, m.Id));
        if (SelectedMarker != null && SelectedMarker.Id > 0) ids.Add((SelectedMarker is GMapSymbols.GMapImageMarker, SelectedMarker.Id));
        return ids;
    }

    /// <summary>(타입,Id) 집합을 라이브 마커로 해석해 그룹 선택 적용 — 단일 adorner/패널 정리 후 세팅(빈 집합=전체 해제).</summary>
    private void ApplyGroupSelectionByIds(System.Collections.Generic.ICollection<(bool isImage, int id)> ids)
    {
        MainMap?.DeselectAllMarkers();   // 단일 편집 adorner 정리(그룹으로 전환)
        SelectedMarker = null;
        HidePropertyPanel();
        System.Collections.Generic.List<IEditableMarker>? live = null;
        if (ids != null && ids.Count > 0 && MainMap?.Markers != null)
        {
            var idset = new System.Collections.Generic.HashSet<(bool isImage, int id)>(ids);
            live = MainMap.Markers.OfType<IEditableMarker>()
                .Where(m => m != null && !m.IsDisposed && idset.Contains((m is GMapSymbols.GMapImageMarker, m.Id))).ToList();   // 타입인지 — 같은 Id 반대타입 미포함
        }
        _groupSelection?.SetSelection(live != null && live.Count > 0 ? live : null);
        NotifyOfPropertyChange(nameof(SelectedMarkers));
        NotifyOfPropertyChange(nameof(HasSelectedItem));   // 그룹선택 변경 → 쓰레기통·선택취소 버튼 활성 갱신
        ShowGroupPropertyPanelIfNeeded();   // 그룹 ≥2 → 공통 속성창(전체 반영). (기능 ②)
    }

    /// <summary>Del = 그룹 삭제(그룹 활성 시).</summary>
    private async void OnMapPreviewKeyDownForGroup(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // 텍스트/콤보 입력 중이면 단축키(Del/방향키/Ctrl+C·V 등) 가로채지 않음 — 윈도우 레벨 후킹이 광범위하므로(#7, RISK-02).
        var focused = System.Windows.Input.Keyboard.FocusedElement;
        if (focused is System.Windows.Controls.Primitives.TextBoxBase
            || e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase
            || focused is System.Windows.Controls.ComboBox
            || focused is System.Windows.Controls.PasswordBox) return;

        // Delete = 선택 심볼/이미지 삭제(단일·그룹) — 단일 진입점 ExecuteDeleteSelected(확인팝업 경유)로 통일.
        //   그룹 선택 시 내부에서 ExecuteGroupDelete로 위임. 단일 Delete 키 경로는 원래 부재(어도너 스텁=no-op)였어 신규 배선(FR-05).
        if (e.Key == System.Windows.Input.Key.Delete
            && ((_groupSelection?.HasSelection ?? false) || SelectedMarker != null || SelectedImage != null))
        {
            e.Handled = true;
            ExecuteDeleteSelected(null);
            return;
        }

        // Ctrl+C / Ctrl+V = 심볼 복사 / 붙여넣기(마우스 커서 위치). 맵 활성·편집모드 시에만(윈도우 광역 후킹 격리, FR-06).
        var ctrlC = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
        if (ctrlC && IsMapShortcutContextActive())
        {
            if (e.Key == System.Windows.Input.Key.C) { e.Handled = true; CopySelectionToBuffer(); return; }
            if (e.Key == System.Windows.Input.Key.V) { e.Handled = true; PasteFromBufferAsync(); return; }
        }

        // 방향키 이동 — 스냅 ON=격자 한 칸, 스냅 OFF=1px(Shift+방향키=5px). 선택된 심볼/이미지 대상.
        // 선택 없으면 미처리 → 기본 동작(맵 패닝) 유지.
        if (e.Key is System.Windows.Input.Key.Left or System.Windows.Input.Key.Right
            or System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)
        {
            if (await TryMoveSelectionByGridAsync(e.Key)) e.Handled = true;
            return;
        }

        // Undo/Redo 단축키 (FR-11) — 편집모드∧권한 시에만(Command CanExecute). Ctrl+Z=Undo, Ctrl+Y·Ctrl+Shift+Z=Redo.
        var ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
        var shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
        if (ctrl && e.Key == System.Windows.Input.Key.Z && !shift)
        {
            if (UndoCommand?.CanExecute(null) == true) { e.Handled = true; UndoCommand.Execute(null); }
        }
        else if (ctrl && (e.Key == System.Windows.Input.Key.Y || (e.Key == System.Windows.Input.Key.Z && shift)))
        {
            if (RedoCommand?.CanExecute(null) == true) { e.Handled = true; RedoCommand.Execute(null); }
        }
    }

    /// <summary>방향키로 선택 심볼/이미지 이동. 스냅 ON=격자 교점 한 칸, 스냅 OFF=방향키 1px·Shift+방향키 5px.
    /// 그룹 선택 우선(전체 이동, 잠금 제외), 없으면 단일 선택. DB 영속 + Undo 기록(그룹=1 매크로). 편집모드·권한 게이트.</summary>
    private async System.Threading.Tasks.Task<bool> TryMoveSelectionByGridAsync(System.Windows.Input.Key key)
    {
        if (MainMap == null || !IsEditModeEnabled) return false;   // 스냅 ON=격자, OFF=픽셀(1/5) 둘 다 처리

        // 대상 수집(잠금·dispose 제외). 그룹 우선, 없으면 단일.
        var targets = (_groupSelection?.HasSelection ?? false)
            ? _groupSelection!.Selection.Where(m => m != null && !m.IsDisposed && !m.IsLocked).ToList()
            : (SelectedMarker != null && !SelectedMarker.IsDisposed && !SelectedMarker.IsLocked
                ? new System.Collections.Generic.List<GMapSymbols.IEditableMarker> { SelectedMarker }
                : new System.Collections.Generic.List<GMapSymbols.IEditableMarker>());
        if (targets.Count == 0) return false;

        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return true; }   // 처리됨(권한없음 안내) — 패닝 방지

        int sx = key == System.Windows.Input.Key.Left ? -1 : key == System.Windows.Input.Key.Right ? 1 : 0;
        int sy = key == System.Windows.Input.Key.Up ? -1 : key == System.Windows.Input.Key.Down ? 1 : 0;
        if (sx == 0 && sy == 0) return false;

        // 스냅 ON: 격자 교점 스냅. 스냅 OFF: 방향키=1px, Shift+방향키=5px.
        bool snap = MainMap.IsSnapToGridEnabled;
        double stepPx = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0 ? 5.0 : 1.0;
        double gridPx = 0d, x0 = 0d, y0 = 0d;
        if (snap)
        {
            gridPx = SnapGridOverlayService.EffectiveGridPx(MainMap.GridSizePx);
            (x0, y0) = SnapGridOverlayService.ComputeOrigin(MainMap, gridPx);
        }

        // 이동 + 영속(그룹 이동과 동일 패턴). before는 이동 전 위치(Undo용).
        var moves = new System.Collections.Generic.List<(GMapSymbols.IEditableMarker marker, GMap.NET.PointLatLng before)>();
        foreach (var m in targets)
        {
            var before = m.Position;
            var p = MainMap.FromLatLngToLocal(m.Position);   // 화면 픽셀(RenderOffset 포함)
            GMap.NET.PointLatLng np;
            if (snap)
            {
                // 가장 가까운 격자 교점 인덱스 + 한 칸 → 항상 교점에 안착(시각=스냅 단일원천 재사용).
                double ix = System.Math.Round((p.X - x0) / gridPx) + sx;
                double iy = System.Math.Round((p.Y - y0) / gridPx) + sy;
                np = MainMap.FromLocalToLatLng((int)System.Math.Round(x0 + ix * gridPx), (int)System.Math.Round(y0 + iy * gridPx));
            }
            else
            {
                // 스냅 OFF: 현재 화면 위치에서 방향×스텝(px)만큼 이동.
                np = MainMap.FromLocalToLatLng((int)System.Math.Round(p.X + sx * stepPx), (int)System.Math.Round(p.Y + sy * stepPx));
            }
            m.UpdateLocation(np);
            moves.Add((m, before));
            try { await DbUpdateProcess(m); } catch (System.Exception ex) { _log?.Error($"방향키 이동 영속 실패: {ex.Message}"); }
        }

        // Undo 기록 — 단일=직접, 다중=1 매크로.
        if (moves.Count == 1)
            _editRecorder?.RecordPositionChange(moves[0].marker, moves[0].before);
        else
            using (_editRecorder?.BeginBatch($"격자 이동 {moves.Count}개"))
                foreach (var mv in moves) _editRecorder?.RecordPositionChange(mv.marker, mv.before);

        if (_groupSelection?.HasSelection ?? false) _groupSelection.RefreshAdorner();
        MainMap.InvalidateVisual();
        return true;
    }

    /// <summary>그룹 이동 완료 → 멤버별 DB 영속(잠금 멤버 스킵, FR-MS-05/08). RBAC 게이트.</summary>
    private async void OnGroupMoveCompleted(System.Collections.Generic.IReadOnlyList<(GMapSymbols.IEditableMarker marker, GMap.NET.PointLatLng before)> moves)
    {
        if (_groupSelection == null) return;
        if (!CanEditMap()) { SetAimStatus("편집 권한이 없습니다.", true); return; }
        int moved = 0, skipped = 0;
        foreach (var m in _groupSelection.Selection.ToList())
        {
            if (m == null || m.IsDisposed) continue;
            if (m.IsLocked) { skipped++; continue; }
            try { await DbUpdateProcess(m); moved++; } catch (Exception ex) { _log?.Error($"그룹 이동 영속 실패: {ex.Message}"); }
        }
        // Undo 기록 — 멤버별 위치변경을 1 매크로로(그룹 이동)
        if (moves != null && moves.Count > 0)
            using (_editRecorder?.BeginBatch($"그룹 이동 {moves.Count}개"))
                foreach (var mv in moves)
                    _editRecorder?.RecordPositionChange(mv.marker, mv.before);
        _groupSelection.RefreshAdorner();
        SetAimStatus(skipped > 0 ? $"{moved}개 이동 저장(잠금 {skipped}개 제외)" : $"{moved}개 이동 저장", true);
    }

    private async void OnGroupDeleteRequested() => await ExecuteGroupDelete();
    private async void OnGroupLockRequested(bool locked) => await ExecuteGroupLock(locked);

    // 그룹 삭제 확인 대기 스냅샷 — 확인 팝업 발행 시 대상 마커를 담고, Handle에서 소비.
    private System.Collections.Generic.List<IEditableMarker>? _pendingGroupDelete;

    /// <summary>그룹 삭제 요청 — 파괴적 작업이라 표준 확인 팝업(EventAggregator) 발행. 확인 시
    /// <see cref="HandleAsync(CallDeleteGroupSymbolsProcessMessageModel, CancellationToken)"/> 가 실제 삭제 수행.
    /// 잠긴 멤버 스킵(FR-MS-08), CanEditMap 게이트.</summary>
    private async Task ExecuteGroupDelete()
    {
        if (_groupSelection == null || !_groupSelection.HasSelection) return;
        if (!CanEditMap()) { SetAimStatus("삭제 권한이 없습니다.", true); return; }
        var targets = _groupSelection.Selection.Where(m => m != null && !m.IsDisposed && !m.IsLocked).ToList();
        if (targets.Count == 0) { SetAimStatus("삭제할 항목이 없습니다(잠금 제외).", true); return; }

        _pendingGroupDelete = targets;
        // raw MessageBox 금지 — 프로젝트 표준 확인 팝업 패턴(ROI 삭제와 동일). 확인 콜백 = CallDeleteGroupSymbolsProcessMessageModel.
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "그룹 삭제",
            Explain = $"선택한 {targets.Count}개 심볼을 삭제하시겠습니까?",
            MessageModel = new CallDeleteGroupSymbolsProcessMessageModel()
        });
    }

    #region - Group Delete IHandle -

    /// <summary>그룹 삭제 확인 콜백 — 사용자가 확인 팝업에서 "확인" 시 발행됨. 실제 다중 삭제 수행(Undo 1 매크로).</summary>
    public async Task HandleAsync(CallDeleteGroupSymbolsProcessMessageModel message, CancellationToken cancellationToken)
    {
        var targets = _pendingGroupDelete;
        _pendingGroupDelete = null;
        try
        {
            // 확인 팝업 → 진행 팝업으로 전환(확인 창 닫힘). ROI 삭제와 동일 패턴.
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

            if (targets == null || targets.Count == 0) return;
            if (!CanEditMap()) { SetAimStatus("삭제 권한이 없습니다.", true); return; }

            int del = 0, skipped = 0;
            using (_editRecorder?.BeginBatch("그룹 삭제"))
            {
                foreach (var m in targets)
                {
                    if (m == null || m.IsDisposed) continue;
                    if (m.IsLocked) { skipped++; continue; }
                    try
                    {
                        var snap = _editRecorder?.CaptureForDelete(m);   // 삭제 전 스냅샷(Undo용)
                        MainMap?.DeselectMarker(m);
                        if (m is GMapMarker gm) MainMap?.Markers?.Remove(gm);
                        await DbDeleteProcess(m);
                        var s = _symbolProvider.FirstOrDefault(x => x.Id == m.Id);   // 캐시 동기(감사 P0, provider.Remove 미머지)
                        if (s != null) _symbolProvider.Remove(s);
                        m.Dispose();
                        _editRecorder?.RecordDelete(snap);   // Undo 기록(배치 멤버)
                        del++;
                    }
                    catch (Exception ex) { _log?.Error($"그룹 삭제 실패: {ex.Message}"); }
                }
            }
            _groupSelection?.Clear();
            NotifyOfPropertyChange(nameof(SelectedMarkers));
            HidePropertyPanel();
            // 레이어 패널 트리 동기화 — 그룹(다중) 삭제 경로도 누락돼 있어 스테일 노드가 남던 버그 보강.
            if (del > 0) await LoadLayersFromDbAsync();
            MainMap?.InvalidateVisual();
            SetAimStatus(skipped > 0 ? $"{del}개 삭제(잠금 {skipped}개 제외)" : $"{del}개 삭제", true);
        }
        catch (Exception ex) { _log?.Error($"그룹 삭제 처리 실패: {ex.Message}"); }
        finally
        {
            // 확인/진행 팝업 닫기 — 콜백 발행 후 자동으로 닫히지 않으므로 모든 경로에서 명시적 Close 필수(083ac4d 누락 수정).
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    #endregion

    /// <summary>그룹 잠금/해제 — 전체 적용(FR-MS-06). marker.IsLocked 모델 연동 후 DB 영속.</summary>
    private async Task ExecuteGroupLock(bool locked)
    {
        if (_groupSelection == null || !_groupSelection.HasSelection) return;
        if (!CanEditMap()) { SetAimStatus("잠금 변경 권한이 없습니다.", true); return; }
        int n = 0;
        var lockChanges = new System.Collections.Generic.List<(IEditableMarker m, bool before)>();
        foreach (var m in _groupSelection.Selection.ToList())
        {
            if (m == null || m.IsDisposed) continue;
            var before = m.IsLocked;
            try { m.IsLocked = locked; await DbUpdateProcess(m); if (before != locked) lockChanges.Add((m, before)); n++; }
            catch (Exception ex) { _log?.Error($"그룹 잠금 변경 실패: {ex.Message}"); }
        }
        // Undo 기록 — 그룹 잠금/해제를 1 매크로로(개별 잠금과 동일 RecordLock). 누락 시 Ctrl+Z 무반응이었음(버그 B).
        if (lockChanges.Count > 0)
            using (_editRecorder?.BeginBatch(locked ? "그룹 잠금" : "그룹 잠금 해제"))
                foreach (var c in lockChanges) _editRecorder?.RecordLock(c.m, c.before, locked);
        SyncLayerNodesFromMarkers(_groupSelection.Selection);   // D: 레이어 트리 노드 잠금 상태 동기화
        _groupSelection.RefreshAdorner();
        NotifyOfPropertyChange(nameof(SelectedMarkers));
        SetAimStatus($"{n}개 {(locked ? "잠금" : "잠금 해제")}", true);
    }

    private void OnGroupVisibilityRequested(bool show) => ExecuteGroupVisibility(show);
    private async void OnGroupZOrderRequested(bool toFront) => await ExecuteGroupZOrder(toFront);

    /// <summary>그룹 표시/숨김 — 멤버 가시성 런타임 토글(ShowShape/IsLayerEnabled/IsVisible). DB영속=v2. CanEditMap 게이트.</summary>
    private void ExecuteGroupVisibility(bool show)
    {
        if (_groupSelection == null || !_groupSelection.HasSelection) return;
        if (!CanEditMap()) { SetAimStatus("편집 권한이 없습니다.", true); return; }
        int n = 0;
        using (_editRecorder?.BeginBatch(show ? "그룹 표시" : "그룹 숨김"))   // AREA 1: 그룹 가시성 1 undo 단위
        {
            foreach (var m in _groupSelection.Selection.ToList())
            {
                if (m == null || m.IsDisposed) continue;
                var beforeShow = m.ShowShape;
                m.IsLayerEnabled = show;
                m.ShowShape = show;
                m.IsVisible = show && MainMap != null && MainMap.Zoom >= m.Zoom;   // 유효 가시성 = 토글 AND 줌
                _editRecorder?.RecordVisibility(m, beforeShow, show);
                n++;
            }
        }
        SyncLayerNodesFromMarkers(_groupSelection.Selection);   // D: 레이어 트리 노드 체크(가시성) 동기화
        MainMap?.InvalidateVisual();
        _groupSelection.RefreshAdorner();
        SetAimStatus($"{n}개 {(show ? "표시" : "숨김")}", true);
    }

    /// <summary>그룹 잠금/가시성 변경 후 레이어 트리 심볼 리프 노드 동기화(D — 마커→노드, 피드백 없음). Id 매칭, InitIsLocked/SetCheckedSilently.</summary>
    private void SyncLayerNodesFromMarkers(IReadOnlyList<IEditableMarker> markers)
    {
        if (_layerTreeNodes == null || markers == null || markers.Count == 0) return;
        var byId = new Dictionary<int, IEditableMarker>();
        foreach (var mm in markers) if (mm != null && !mm.IsDisposed && mm.Id > 0) byId[mm.Id] = mm;
        if (byId.Count == 0) return;
        foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes).Where(n => n.IsSymbolLeaf && n.Symbol != null))
            if (byId.TryGetValue(leaf.Symbol!.Id, out var mk))
            {
                leaf.InitIsLocked(mk.IsLocked);          // 잠금 아이콘/헤더 갱신(LockChanged 미발화, desync 방지)
                leaf.SetCheckedSilently(mk.ShowShape);   // 가시성 체크 갱신(CheckChanged 미발화)
            }
    }

    /// <summary>그룹 Z순서 — 선택 멤버를 심볼 밴드 최상단(toFront)/최하단으로 일괄. BatchUpdateZOrderAsync 영속. CanEditMap 게이트.</summary>
    private async Task ExecuteGroupZOrder(bool toFront)
    {
        if (_groupSelection == null || !_groupSelection.HasSelection || MainMap?.Markers == null) return;
        if (!CanEditMap()) { SetAimStatus("편집 권한이 없습니다.", true); return; }
        try
        {
            var selected = _groupSelection.Selection.Where(m => m is GMapMarker && !m.IsDisposed).ToList();
            if (selected.Count == 0) return;
            var selIds = new HashSet<int>(selected.Select(m => m.Id));
            var zBefore = IsApplyingUndo ? null : selected.Where(m => m.Id > 0)
                .Select(m => (isImage: m is GMapSymbols.GMapImageMarker, id: m.Id, zOrder: m.ZOrder)).ToList();   // FIX 8 — 순서변경 undo 기록용(변경 전, isImage 보존 D1)

            // 심볼 밴드(비-이미지, 선택 외) 현재 ZIndex 수집 → 최상단/최하단 기준
            var bandZ = MainMap.Markers.OfType<GMapMarker>()
                .Where(g => g.Shape is UIElement && g is IEditableMarker em && em is not IImageEditableMarker && !selIds.Contains(em.Id))
                .Select(g => System.Windows.Controls.Panel.GetZIndex(g.Shape as UIElement))
                .ToList();
            int baseZ = bandZ.Count > 0 ? (toFront ? bandZ.Max() + 1 : bandZ.Min() - selected.Count) : 1000;

            var changes = new List<(bool isImage, int id, int zOrder)>();
            for (int i = 0; i < selected.Count; i++)
            {
                var m = selected[i];
                int newZ = baseZ + i;
                if (m is GMapMarker gm && gm.Shape is UIElement shape)
                {
                    ApplyMarkerZOrderLocal(gm, shape, newZ);
                    if (m.Id > 0) changes.Add((m is GMapSymbols.GMapImageMarker, m.Id, newZ));   // isImage 보존(D1, 그룹밴드는 심볼전용이라 사실상 false)
                }
            }
            if (changes.Count > 0) await _gMapDbSymbolService.BatchUpdateZOrderAsync(changes.Select(c => (c.id, c.zOrder)).ToList());
            if (zBefore != null && changes.Count > 0) _editRecorder?.RecordZOrder(zBefore, changes);   // FIX 8
            MainMap.InvalidateVisual();
            _groupSelection.RefreshAdorner();
            SetAimStatus($"{changes.Count}개 {(toFront ? "맨 위로" : "맨 아래로")}", true);
        }
        catch (Exception ex) { _log?.Error($"그룹 Z순서 변경 실패: {ex.Message}"); }
    }
    #endregion

    #region - GMapCustomControl 이벤트 핸들러 -
    /// <summary>
    /// 지도 마커 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapMarkerClicked(IEditableMarker marker)
    {
        try
        {
            // 단일 클릭(Shift 없음) = 그룹 선택 해제 후 단일선택 폴백(공존, FR-MS-08)
            if (_groupSelection?.HasSelection ?? false)
            {
                _groupSelection.Clear();
                NotifyOfPropertyChange(nameof(SelectedMarkers));
            }
            if (marker?.IsLocked == true) return;   // 잠긴 심볼은 편집모드 ON에서도 클릭/선택 차단
            //_log?.Info($"=== 마커 클릭 시작 ===");
            //_log?.Info($"클릭 전 - {GetMarkerInfo(marker)}");
            //_log?.Info($"OnMapMarkerClicked 호출됨: {marker.Title}, 편집모드: {IsEditModeEnabled}");

            if (IsEditModeEnabled)
            {
                //_log?.Info($"편집 모드에서 마커 선택 시도");
                SelectMarkerForEditing(marker);
                
            }
            //else
            //{
            //    _log?.Info($"일반 모드에서 마커 클릭: {marker.Title}");
            //    _log?.Info($"UpdateSelectedMarker 호출 전 - {GetMarkerInfo(marker)}");
            //    UpdateSelectedMarker(marker);
            //    _log?.Info($"UpdateSelectedMarker 호출 후 - {GetMarkerInfo(marker)}");
            //}

            //_log?.Info($"클릭 완료 후 - {GetMarkerInfo(marker)}");
            //_log?.Info($"=== 마커 클릭 종료 ===");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 마커 우클릭 이벤트 핸들러 — 컨텍스트 메뉴 표시
    /// </summary>
    private void OnMapMarkerRightClicked(IEditableMarker marker)
    {
        try
        {
            ShowMarkerContextMenu(marker, new Point());
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 우클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 카메라 심볼 더블클릭 → RTSP 스트리밍 팝업 오픈(IpCamera 한정).
    /// 카메라 모델의 RTSP URL(Urls.RtspSub→RtspMain→Ip)을 어댑터로 변환해 팝업에 전달.
    /// (P5에서 ObservableCollection 팝업 오픈/위치복원에 연결)
    /// </summary>
    private void OnMapMarkerDoubleClicked(IEditableMarker marker)
    {
        try
        {
            // 카메라(IpCamera)만 대상
            if (marker is not IPidsEditableMarker pidsMarker
                || pidsMarker.DeviceType != EnumDeviceType.IpCamera)
                return;

            // 카메라 팝업 연동 OFF면 더블클릭 무시 (EventSetupView "카메라 팝업 연동")
            var setup = ResolveStreamingSetup();
            if (setup != null && !setup.IsCameraPopupUsed)
            {
                _log?.Info("[CameraPopup] 카메라 팝업 연동 OFF — 더블클릭 무시");
                return;
            }

            var cameraModel = pidsMarker.LinkedDevice as ICameraDeviceModel;
            if (cameraModel == null)
            {
                _log?.Warning($"[CameraPopup] 카메라 모델 없음(LinkedDevice null): {marker.Title}");
                return;
            }

            var connInfo = CameraConnectionAdapter.ToConnectionInfo(cameraModel, preferSub: true);
            if (connInfo == null)
            {
                _log?.Warning($"[CameraPopup] RTSP URL 없음(영상 없음): {marker.Title} — 카메라 상세보기 > URLs 탭에 rtsp:// 입력 필요");
                return;
            }

            // 실제 접속 URL = 카메라 설정(Urls.RtspSub/RtspMain) 원본. VLC엔 원본 그대로, 로그만 자격증명 마스킹.
            _log?.Info($"[CameraPopup] 카메라 {cameraModel.Id}({marker.Title}) RTSP 접속 URL(설정값)={MaskRtspCredentials(connInfo.GetFullUrl())}");

            _ = OpenCameraStreamPopupAsync(cameraModel.Id, marker.Title, connInfo, marker);
        }
        catch (Exception ex)
        {
            _log?.Error($"카메라 더블클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>로그 출력용 RTSP URL 자격증명 마스킹(rtsp://user:pass@host → rtsp://***@host). 재생 URL은 원본 유지.</summary>
    private static string MaskRtspCredentials(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "(빈 URL)";
        var scheme = url.IndexOf("://", StringComparison.Ordinal);
        var at = url.IndexOf('@');
        if (scheme > 0 && at > scheme)
            return string.Concat(url.AsSpan(0, scheme + 3), "***@", url.AsSpan(at + 1));
        return url;
    }

    #region - Camera RTSP Stream Popup (맵 위 이동식 영상 팝업) -

    private const double CameraPopupInitialOffsetRight = 100;   // 심볼 중점 기준 오른쪽
    private const double CameraPopupInitialOffsetUp = 100;      // 심볼 중점 기준 위

    private ObservableCollection<CameraStreamPopupViewModel>? _cameraPopups;
    private ICameraPopupPositionStore? _cameraPopupPositionStore;
    private ISharedCameraStreamHub? _cameraStreamHub;
    private bool _cameraStreamHubResolved;
    private IStreamingSetupModel? _streamingSetup;
    private bool _streamingSetupResolved;
    private readonly Dictionary<CameraStreamPopupViewModel, System.Windows.Threading.DispatcherTimer> _popupAutoCloseTimers = new();

    // PTZ 제어(CameraPopup_PTZ_Control) — IPtzController는 OnvifServiceModule 등록 시에만 IoC lazy 해석
    private IPtzController? _ptzController;
    private bool _ptzControllerResolved;
    private const double PtzDragSensitivity = 2.0;   // 드래그 픽셀↔이동량 감도(Phase 0 실카메라 GetNode 응답으로 튜닝)

    private CameraStreamPopupViewModel? _selectedCameraPopup;

    /// <summary>단일 선택된 팝업(상호배타). 좌클릭 시 설정 + 맨앞 이동. (FR-SEL-01)</summary>
    public CameraStreamPopupViewModel? SelectedCameraPopup
    {
        get => _selectedCameraPopup;
        set
        {
            if (ReferenceEquals(_selectedCameraPopup, value)) return;
            if (_selectedCameraPopup != null) _selectedCameraPopup.IsSelected = false;   // 이전 해제(원자)
            _selectedCameraPopup = value;
            if (_selectedCameraPopup != null) _selectedCameraPopup.IsSelected = true;     // 신규 활성
            NotifyOfPropertyChange(nameof(SelectedCameraPopup));
        }
    }

    /// <summary>맵 위에 열린 카메라 RTSP 팝업 목록(MapView PropertyPanelCanvas ItemsControl 바인딩).</summary>
    public ObservableCollection<CameraStreamPopupViewModel> CameraPopups
        => _cameraPopups ??= new ObservableCollection<CameraStreamPopupViewModel>();

    private ICameraPopupPositionStore CameraPopupPositionStore
        => _cameraPopupPositionStore ??= new CameraPopupPositionStore(_gMapDbService, _log);

    private IPtzPresetStore? _ptzPresetStore;
    private IPtzPresetStore PtzPresetStore => _ptzPresetStore ??= new PtzPresetStore(_gMapDbService, _log);

    /// <summary>ISharedCameraStreamHub는 메인솔루션 StreamingModule 등록 시에만 존재 — IoC lazy 획득.</summary>
    private ISharedCameraStreamHub? ResolveHub()
    {
        if (_cameraStreamHubResolved) return _cameraStreamHub;
        _cameraStreamHubResolved = true;
        try { _cameraStreamHub = IoC.Get<ISharedCameraStreamHub>(); }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] StreamingHub 미등록(영상 팝업 비활성): {ex.Message}");
            _cameraStreamHub = null;
        }
        return _cameraStreamHub;
    }

    /// <summary>IStreamingSetupModel(라이브 SetupModel) — 메인솔루션 StreamingModule 등록 시에만 존재. IoC lazy.</summary>
    private IStreamingSetupModel? ResolveStreamingSetup()
    {
        if (_streamingSetupResolved) return _streamingSetup;
        _streamingSetupResolved = true;
        try { _streamingSetup = IoC.Get<IStreamingSetupModel>(); }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] StreamingSetup 미등록(게이팅/자동해제 기본동작): {ex.Message}");
            _streamingSetup = null;
        }
        return _streamingSetup;
    }

    /// <summary>IPtzController는 메인솔루션 OnvifServiceModule 등록 시에만 해석 — IoC lazy(미등록 시 PTZ 비활성). (FR-PTZCTL-01)</summary>
    private IPtzController? ResolvePtzController()
    {
        if (_ptzControllerResolved) return _ptzController;
        _ptzControllerResolved = true;
        try { _ptzController = IoC.Get<IPtzController>(); }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] PtzController 미등록(PTZ 비활성): {ex.Message}");
            _ptzController = null;
        }
        return _ptzController;
    }

    // ── 디바이스 위치 저장 게이트웨이(Symbol_Apply_DeviceLocation) ── DeviceUiModule 등록 시에만 해석.
    private IDeviceLocationGateway? _deviceLocationGateway;
    private bool _deviceLocationGatewayResolved;
    private IDeviceLocationGateway? ResolveDeviceLocationGateway()
    {
        if (_deviceLocationGatewayResolved) return _deviceLocationGateway;
        _deviceLocationGatewayResolved = true;
        try { _deviceLocationGateway = IoC.Get<IDeviceLocationGateway>(); }
        catch (Exception ex)
        {
            _log?.Warning($"[DeviceLocation] 게이트웨이 미등록(현재위치 적용 비활성): {ex.Message}");
            _deviceLocationGateway = null;
        }
        return _deviceLocationGateway;
    }

    /// <summary>"현재위치 적용" 클릭 — 동기 위임(async void 회피), 내부 async에서 예외 격리.</summary>
    private void OnDeviceLocationApplyRequested(object? sender, EventArgs e) => _ = HandleApplyDeviceLocationAsync();

    /// <summary>심볼 현재 위치(+방위)를 연결 디바이스 Model/API에 저장. 진행바는 버튼 내부(Begin은 클릭 시, End는 여기 finally).</summary>
    private async Task HandleApplyDeviceLocationAsync()
    {
        _log?.Info("[DeviceLocation] '현재위치 적용' 이벤트 수신 — 핸들러 진입");
        bool ok = false;
        try
        {
            if (SelectedMarker is not GMapPidsMarker pids)
            {
                _log?.Warning("[DeviceLocation] 중단 — 선택 마커가 GMapPidsMarker 아님");
                return;
            }

            var dev = pids.LinkedDevice;
            if (dev == null) { _log?.Warning("[DeviceLocation] 중단 — LinkedDevice=null"); SetAimStatus("연결된 디바이스가 없어 저장할 수 없습니다.", autoHide: true); return; }
            if (dev.Id <= 0) { _log?.Warning($"[DeviceLocation] 중단 — 유효하지 않은 device.Id={dev.Id}"); SetAimStatus("디바이스 ID가 유효하지 않습니다.", autoHide: true); return; }

            double lat = pids.Latitude, lng = pids.Longitude;
            if (!Services.Tracking.TrackingMath.IsValidLatLng(lat, lng))
            {
                _log?.Warning($"[DeviceLocation] 중단 — 무효 좌표 ({lat},{lng})");
                SetAimStatus("심볼 좌표가 유효하지 않습니다.", autoHide: true);
                return;
            }

            var gateway = ResolveDeviceLocationGateway();
            if (gateway == null) { _log?.Warning("[DeviceLocation] 중단 — IDeviceLocationGateway 미등록(DeviceUiModule 미로드?)"); SetAimStatus("디바이스 저장 기능을 사용할 수 없습니다(미등록).", autoHide: true); return; }

            double? heading = pids.BaseBearing;   // 심볼 방위(0~360) → 디바이스 Heading
            _log?.Info($"[DeviceLocation] 저장 시도 — device='{dev.DeviceName}' id={dev.Id} type={dev.GetType().Name} → ({lat:F6},{lng:F6}) heading={heading:F0}");
            var ct = _cts?.Token ?? CancellationToken.None;
            ok = await gateway.ApplyLocationAsync(dev, lat, lng, heading, ct);
            _log?.Info($"[DeviceLocation] 저장 결과 — ok={ok} (device id={dev.Id})");
            SetAimStatus(ok
                ? $"디바이스 '{dev.DeviceName}' 위치 저장됨 ({lat:F6}, {lng:F6})."
                : "디바이스 위치 저장에 실패했습니다.", autoHide: true);
        }
        catch (Exception ex)
        {
            _log?.Error($"[DeviceLocation] 적용 실패: {ex.Message}");
            SetAimStatus("디바이스 위치 저장 중 오류가 발생했습니다.", autoHide: true);
        }
        finally
        {
            PropertyPanel?.EndDeviceLocationApply(ok);   // 버튼 내부 진행바 → 정상 복원(성공/실패/예외 공통)
        }
    }

    // ── 권한 게이팅(FR-EN-06) ── IPermissionService도 IPtzController처럼 IoC lazy 해석.
    //    GMaps.Ui→Accounts.Api 참조 추가됨(순환 없음). 미등록(DB Auth/오프라인/테스트) 시 null → 전체허용 폴백(V-EN-11).
    private IPermissionService? _permissionService;
    private bool _permissionResolved;
    private IPermissionService? ResolvePermissionService()
    {
        if (_permissionResolved) return _permissionService;
        try
        {
            _permissionService = IoC.Get<IPermissionService>();
            _permissionResolved = _permissionService != null;   // 성공 시에만 캐시 확정 — 실패 시 재시도 허용(영구 null캐시=영구 fail-open 방지)
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopup] PermissionService 미해석(권한 게이팅 전체허용 폴백): {ex.Message}");
            _permissionService = null;   // _permissionResolved 미설정 → 다음 호출 재시도
        }
        return _permissionService;
    }

    /// <summary>카메라 제어 권한(cam:control). 권한엔진 미등록/미로그인 시 true(전체허용 폴백). 모듈명 "cameras" 고정. (FR-EN-06)</summary>
    private bool CanControlCamera() => ResolvePermissionService()?.CanControl("cameras") ?? true;

    /// <summary>방송 발행 권한(broadcast:control). 권한엔진 미등록/미로그인 시 true(전체허용 폴백). 모듈명 "broadcast" 고정. (FR-EN-07)</summary>
    private bool CanBroadcast() => ResolvePermissionService()?.CanControl("broadcast") ?? true;

    /// <summary>상황도 편집 권한(map:edit). 권한엔진 미등록/미로그인 시 true(전체허용 폴백). 모듈명 "map" 고정. (FR-EN-08)</summary>
    private bool CanEditMap() => ResolvePermissionService()?.CanEdit("map") ?? true;

    // ── T5: verb→버튼 시각 게이팅(비활성) 바인딩용 공개 프로퍼티. 표시모델=disable(권한없음→회색). 액션차단 가드는 safety net 유지. ──
    /// <summary>상황도 편집 버튼 활성 여부(map:edit). PermissionsChanged 시 재평가(OnPtzPermissionsChanged).</summary>
    public bool CanMapEdit => CanEditMap();
    /// <summary>방송 버튼 활성 여부(broadcast:control).</summary>
    public bool CanMapBroadcast => CanBroadcast();

    /// <summary>맵 편집 권한 부재 안내 팝업(FR-EN-08). 게이트 차단 시 사용자에게 가시적 통지(조용한 무동작 방지).</summary>
    private void ShowNoMapEditPermissionInfo()
    {
        try
        {
            _ = _eventAggregator?.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "권한 없음",
                Explain = "맵 편집 권한이 없습니다.\n관리자에게 권한을 요청하세요."
            });
        }
        catch (Exception ex) { _log?.Warning($"[FR-EN-08] 권한 안내 팝업 실패: {ex.Message}"); }
    }

    // FR-EN-11: 역할강등 즉시 재평가 — 권한 상실 시 진행 중 PTZ 이동(연속이동) 취소 + 열린 팝업 PTZ 비활성.
    private bool _ptzPermSubscribed;
    private void SubscribePtzPermission()
    {
        if (_ptzPermSubscribed) return;
        var perm = ResolvePermissionService();
        if (perm == null) return;
        perm.PermissionsChanged += OnPtzPermissionsChanged;
        var sl = ResolveSessionLifecycle();
        if (sl != null) sl.ForceLogoutRequested += OnForceLogoutRequested;   // FR-FL-07: 강제 로그아웃 → GIS PTZ정지·스트림 해제
        _ptzPermSubscribed = true;
    }
    private void UnsubscribePtzPermission()
    {
        if (!_ptzPermSubscribed) return;
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged -= OnPtzPermissionsChanged;
        var sl = ResolveSessionLifecycle();
        if (sl != null) sl.ForceLogoutRequested -= OnForceLogoutRequested;
        _ptzPermSubscribed = false;
    }
    /// <summary>PermissionsChanged 콜백(NATS 배경스레드 가능). 권한 상실 시 진행 중 제스처 취소 + 팝업 IsPtzCapable 비활성.</summary>
    private void OnPtzPermissionsChanged()
    {
        var canCtrl = CanControlCamera();
        if (!canCtrl)
            foreach (var cts in _ptzGestureCts.Values) { try { cts.Cancel(); } catch { /* 이미 종료 */ } }
        _ = OnUiAsync(() =>
        {
            NotifyOfPropertyChange(nameof(CanMapEdit));        // T5: 편집/방송 버튼 시각 비활성 재평가(권한 상실/획득)
            NotifyOfPropertyChange(nameof(CanMapBroadcast));
            if (!CanEditMap() && IsEditModeEnabled) IsEditModeEnabled = false;   // T6 2회차(H-17/19): 맵편집권한 상실(역할강등) 시 편집모드 강제 종료 — 버튼 비활성과 상태 정합
            if (_cameraPopups == null) return;
            foreach (var vm in _cameraPopups) vm.IsPtzCapable = vm.IsPtzCapable && canCtrl;   // 권한 상실→비활성(복구는 팝업 재오픈)
        });
    }

    // FR-FL-07: 강제 로그아웃(세션 revoke/401/수동) 전파 — 진행 중 PTZ 능동제어 정지 + 카메라 팝업(스트림) 해제.
    //   ISessionLifecycle 도 IoC lazy(미등록 시 null=비활성). 셸의 가림막/로그인 화면 전환은 별도 구독자(메인 솔루션) 책임.
    private ISessionLifecycle? _sessionLifecycle;
    private bool _sessionLifecycleResolved;
    private ISessionLifecycle? ResolveSessionLifecycle()
    {
        if (_sessionLifecycleResolved) return _sessionLifecycle;
        _sessionLifecycleResolved = true;
        try { _sessionLifecycle = IoC.Get<ISessionLifecycle>(); }
        catch (Exception ex) { _log?.Warning($"[ForceLogout] SessionLifecycle 미해석(GIS 강제로그아웃 정리 비활성): {ex.Message}"); _sessionLifecycle = null; }
        return _sessionLifecycle;
    }

    // requested_by = 로그인 계정 식별자(login_id). 서버 JWT sub=login_id 이므로 ITokenStorageService.UserId 가 곧 login_id.
    //   ITokenStorageService 도 IoC lazy(미등록/미로그인 시 null → Windows 사용자명 폴백).
    private ITokenStorageService? _tokenStorage;
    private bool _tokenStorageResolved;
    private ITokenStorageService? ResolveTokenStorage()
    {
        if (_tokenStorageResolved) return _tokenStorage;
        _tokenStorageResolved = true;
        try { _tokenStorage = IoC.Get<ITokenStorageService>(); }
        catch (Exception ex) { _log?.Warning($"[CameraAim] TokenStorage 미해석(requested_by 폴백): {ex.Message}"); _tokenStorage = null; }
        return _tokenStorage;
    }
    /// <summary>회전요청 requested_by — 로그인 계정 표시이름(user.name) 우선 → login_id(JWT sub) → Windows 사용자명 폴백.</summary>
    private string CurrentRequestedBy()
    {
        var name = ResolvePermissionService()?.Name;          // 표시이름(로그인 응답 user.name) 우선
        if (!string.IsNullOrWhiteSpace(name)) return name;
        var loginId = ResolveTokenStorage()?.UserId;          // JWT sub=login_id 폴백
        return string.IsNullOrWhiteSpace(loginId) ? Environment.UserName : loginId;
    }

    /// <summary>강제 로그아웃 콜백(배경스레드 가능). 진행 중 PTZ 제스처 즉시 취소 + 열린 카메라 팝업 닫기(스트림 해제).</summary>
    private void OnForceLogoutRequested(EnumRevokeReason reason)
    {
        _log?.Info($"[ForceLogout] GIS 정리 (reason={reason}) — PTZ 정지 + 팝업 해제");
        foreach (var cts in _ptzGestureCts.Values) { try { cts.Cancel(); } catch { /* 이미 종료 */ } }
        _ = OnUiAsync(() =>
        {
            if (IsEditModeEnabled) IsEditModeEnabled = false;   // T6 2회차(G-14): 강제 로그아웃 시 편집모드 강제 종료 — 버튼 비활성과 상태 정합(개별 액션은 CanEditMap 백스톱)
            if (_cameraPopups == null) return;
            foreach (var vm in _cameraPopups.ToList()) _ = CloseCameraPopupAsync(vm);   // 순회 중 Remove 재진입 방어, 비동기 해제
        });
    }

    /// <summary>팝업 오픈 시 ONVIF PTZ 준비(InitializeFull + GetNode space) → IsPtzCapable 설정(게이팅 진실원). (FR-GATE-01)</summary>
    private async Task EnsurePtzReadyAsync(CameraStreamPopupViewModel vm, ICameraDeviceModel cam)
    {
        var ptz = ResolvePtzController();
        if (ptz == null)
        {
            // 무음 실패 방지(진단) — 메인 솔루션 Bootstrapper에 OnvifServiceModule 등록 + 재빌드 필요.
            _log?.Warning($"[CameraPopup] PTZ 비활성 — IPtzController 미해석 cam={vm.CameraId}. 메인 OnvifServiceModule 등록(EXT-01) + 앱 재빌드/재시작 확인.");
            return;
        }
        await OnUiAsync(() => vm.IsPtzLoading = true).ConfigureAwait(false);   // "PTZ 준비 중…" 표시(수 초 소요)
        try
        {
            var conn = new ConnectionModel
            {
                IpAddress = cam.IpAddress,
                PortOnvif = cam.IpPort > 0 ? cam.IpPort : 80,   // ONVIF device_service 포트(기본 80, Phase 0 확인)
                Username = cam.UserName,
                Password = cam.UserPassword,
            };
            _log?.Info($"[CameraPopup] PTZ 준비 시도 cam={vm.CameraId} {cam.IpAddress}:{conn.PortOnvif}");
            var ok = await ptz.EnsureReadyAsync(vm.CameraId, conn).ConfigureAwait(false);
            await OnUiAsync(() => { vm.IsPtzCapable = ok && CanControlCamera(); vm.IsImagingCapable = ptz.IsImagingCapable(vm.CameraId); vm.IsPtzLoading = false; }).ConfigureAwait(false);
            _log?.Info($"[CameraPopup] PTZ 준비 결과 cam={vm.CameraId} capable={ok} (false면 비PTZ 카메라거나 ONVIF 포트/계정 확인)");
        }
        catch (Exception ex)
        {
            await OnUiAsync(() => vm.IsPtzLoading = false).ConfigureAwait(false);
            _log?.Warning($"[CameraPopup] PTZ 준비 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}");
        }
    }

    // ── ContinuousMove 펄스 상수(RelativeMove 미지원 카메라 대응) ──
    //    팬/틸트·줌 속도 크기는 팝업 VM(PanTiltSpeed/ZoomSpeed, [0.1,1.0])이 사용자 조절값으로 보유 — PTZ 탭 슬라이더/텍스트박스.
    private const int PtzDragMaxDurationMs = 700;   // 드래그 1회 이동 최대 시간(드래그 길이 비례)
    private const int PtzDragMinDurationMs = 120;   // 짧은 드래그 최소 펄스 — sub-perceptible 무동작(dead-zone) 방지
    private const int PtzZoomPulseMs = 250;         // 휠 1노치 줌 펄스 시간

    // PTZ 제스처(드래그/줌) 취소용 — 새 제스처가 직전 것을 취소(Last-Write-Wins, 큐 적체 방지).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Threading.CancellationTokenSource> _ptzGestureCts = new();

    /// <summary>새 PTZ 제스처 시작 — 같은 카메라의 직전 제스처(드래그/줌 대기) 취소. 반환 토큰을 대기에 사용.</summary>
    private System.Threading.CancellationToken BeginPtzGesture(int cameraId)
    {
        var cts = new System.Threading.CancellationTokenSource();
        // 원자적 교체(F-06): TryRemove+인덱서 2단계 비원자 경합(직전 CTS 미취소/잘못된 토큰 저장) 방지.
        // 직전 CTS는 update 콜백에서 취소·Dispose. add/update 모두 사전 생성한 동일 cts 반환 → 토큰 일관성 보장.
        _ptzGestureCts.AddOrUpdate(cameraId,
            _ => cts,
            (_, old) => { try { old.Cancel(); old.Dispose(); } catch { /* 이미 종료 */ } return cts; });
        return cts.Token;
    }

    private void OnCameraPopupPtzDragRequested(object? sender, PtzDragEventArgs e)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandlePtzDragAsync(vm, e);   // cam:control 게이팅 (FR-EN-06)
    }

    /// <summary>좌버튼 드래그 릴리즈 → 드래그 방향으로 ContinuousMove, 길이 비례 시간 후 Stop. FOV(부채꼴)는 NVR→NATS가 갱신. (FR-DRAG-03)</summary>
    private async Task HandlePtzDragAsync(CameraStreamPopupViewModel vm, PtzDragEventArgs e)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        var len = Math.Sqrt(e.Dx * e.Dx + e.Dy * e.Dy);
        if (len < 1) return;
        var ct = BeginPtzGesture(vm.CameraId);   // 직전 제스처 취소(Last-Write-Wins)
        try
        {
            // 최대 드래그 길이(이 이상이면 mag=1.0 포화). 0.4→0.65로 늘려 더 길게 끌어야 최대 — 미세조절 폭↑.
            var maxLen = Math.Max(60.0, Math.Min(e.ImageWidth, e.ImageHeight) * 0.65);
            var mag = Math.Min(1.0, len / maxLen);
            // 드래그 길이 비례 이동량: 속도·시간 둘 다 mag로 스케일 → 짧은 드래그=느리고 짧게, 긴 드래그=빠르고 길게(차이 뚜렷).
            // 속도엔 하한(0.3)을 둬 아주 짧은 드래그도 죽지 않게(카메라 가감속에 묻히는 것 방지). 이전엔 ÷len(단위벡터)라 길이 무관이었음.
            var velFactor = vm.PanTiltSpeed * (0.3 + 0.7 * mag);
            var panVel = (e.Dx / len) * velFactor;
            var tiltVel = -(e.Dy / len) * velFactor;   // 화면 아래로 드래그 → 틸트 다운
            if (!await ptz.ContinuousMoveAsync(vm.CameraId, panVel, tiltVel, 0, ct).ConfigureAwait(false)) return;
            await Task.Delay(Math.Max(PtzDragMinDurationMs, (int)(mag * PtzDragMaxDurationMs)), ct).ConfigureAwait(false);
            await ptz.StopAsync(vm.CameraId, ct).ConfigureAwait(false);
            // FOV는 NVR→NATS(CameraPtzNatsSyncService) 경로가 갱신 — ONVIF 직접 갱신 안 함(스케일 불일치).
        }
        catch (OperationCanceledException) { /* 새 제스처가 인계 — 이전 Stop 생략 */ }
        catch (Exception ex) { _log?.Error($"[CameraPopup] PTZ 이동 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    /// <summary>방향 패드 누름 → 직전 제스처 취소 후 해당 방향 ContinuousMove(뗄 때까지 계속). 뗌은 PtzStop. (FR-UI-02)</summary>
    private void OnCameraPopupPtzNudge(object? sender, PtzNudgeEventArgs e)
    {
        if (sender is not CameraStreamPopupViewModel vm) return;
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        if (!CanControlCamera()) return;   // cam:control 게이팅 (FR-EN-06)
        // FR-PTR-01: 취소토큰 전달 → 새 제스처가 직전 대기명령을 LWW 취소(Gate 큐 누적 제거). 드래그/휠과 동일.
        var ct = BeginPtzGesture(vm.CameraId);
        _ = ptz.ContinuousMoveAsync(vm.CameraId, e.Dx * vm.PanTiltSpeed, -e.Dy * vm.PanTiltSpeed, 0, ct);
    }

    private void OnCameraPopupPtzStop(object? sender, EventArgs e)
    {
        if (sender is not CameraStreamPopupViewModel vm) return;
        var ptz = ResolvePtzController();
        if (ptz != null) _ = ptz.StopAsync(vm.CameraId);   // FOV는 NATS가 갱신
    }

    private void OnCameraPopupPtzZoom(object? sender, int direction)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandlePtzZoomAsync(vm, direction);   // cam:control (FR-EN-06)
    }

    /// <summary>영상 휠 → 줌 방향 ContinuousMove 펄스 후 Stop. FOV는 NVR→NATS가 갱신. (FR-PTZCTL-03)</summary>
    private async Task HandlePtzZoomAsync(CameraStreamPopupViewModel vm, int direction)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        var ct = BeginPtzGesture(vm.CameraId);   // 직전 제스처 취소
        try
        {
            if (!await ptz.ContinuousMoveAsync(vm.CameraId, 0, 0, direction * vm.ZoomSpeed, ct).ConfigureAwait(false)) return;
            await Task.Delay(PtzZoomPulseMs, ct).ConfigureAwait(false);
            await ptz.StopAsync(vm.CameraId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* 새 제스처가 인계 */ }
        catch (Exception ex) { _log?.Error($"[CameraPopup] PTZ 줌 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    /// <summary>줌 버튼 누름 → 방향 연속 줌 시작(뗄 때까지 계속). 뗌은 OnCameraPopupPtzStop(StopAsync). (FR-PH-01)</summary>
    private void OnCameraPopupZoomHold(object? sender, int direction)
    {
        if (sender is not CameraStreamPopupViewModel vm) return;
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        if (!CanControlCamera()) return;   // cam:control 게이팅 (FR-EN-06) — 다른 PTZ 핸들러와 동일
        // FR-PTR-01: 취소토큰 전달 → 직전 인플라이트 펄스/대기명령 LWW 취소(큐 누적 제거).
        var ct = BeginPtzGesture(vm.CameraId);
        _ = ptz.ContinuousMoveAsync(vm.CameraId, 0, 0, direction * vm.ZoomSpeed, ct);   // 연속 — 뗄 때 StopAsync
    }

    /// <summary>포커스 버튼 누름 → 연속 포커스 시작(near/far). 뗌/캡처분실/닫기는 OnCameraPopupFocusStop. direction +1=far/-1=near. (FR-PH-02)</summary>
    private void OnCameraPopupFocusHold(object? sender, int direction)
    {
        if (sender is not CameraStreamPopupViewModel vm) return;
        var ptz = ResolvePtzController();
        if (ptz != null && CanControlCamera()) _ = ptz.StartFocusAsync(vm.CameraId, direction);   // cam:imaging→잠정 cam:control (FR-EN-06)
    }

    /// <summary>포커스 정지(뗌/캡처분실) → ImagingClient Stop. PTZ StopAsync와 별개 모터 경로. 정지는 항상 허용(권한 무관). (FR-PH-02/03)</summary>
    private void OnCameraPopupFocusStop(object? sender, EventArgs e)
    {
        if (sender is not CameraStreamPopupViewModel vm) return;
        var ptz = ResolvePtzController();
        if (ptz != null) _ = ptz.StopFocusAsync(vm.CameraId);
    }

    /*──────────────── 프리셋(로컬 DB) 핸들러 ────────────────*/

    private void OnCameraPopupPresetsReload(object? sender, EventArgs e)
    {
        if (sender is CameraStreamPopupViewModel vm) _ = LoadPresetsAsync(vm);
    }

    private async Task LoadPresetsAsync(CameraStreamPopupViewModel vm)
    {
        try
        {
            var list = await PtzPresetStore.GetPresetsAsync(vm.CameraId).ConfigureAwait(false);
            await OnUiAsync(() => vm.SetPresets(list)).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 프리셋 로드 실패 cam={vm.CameraId}: {ex.Message}"); }
    }

    private void OnCameraPopupPresetGoto(object? sender, IPtzPresetModel preset)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandlePresetGotoAsync(vm, preset);   // cam:control (FR-EN-06)
    }

    /// <summary>프리셋 이동 = AbsoluteMove(저장 좌표). FOV(부채꼴)는 NVR→NATS가 갱신. (FR-PRESET-02)</summary>
    private async Task HandlePresetGotoAsync(CameraStreamPopupViewModel vm, IPtzPresetModel preset)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        try
        {
            await ptz.AbsoluteMoveAsync(vm.CameraId, preset.Pan, preset.Tilt, preset.Zoom).ConfigureAwait(false);
            // FOV는 NVR→NATS(CameraPtzNatsSyncService) 경로가 갱신 — ONVIF 직접 갱신 안 함.
        }
        catch (Exception ex) { _log?.Error($"[CameraPopup] 프리셋 이동 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    private void OnCameraPopupPresetSave(object? sender, string name)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandlePresetSaveAsync(vm, name);   // cam:control (FR-EN-06)
    }

    /// <summary>프리셋 저장 = 현재 위치(GetStatus) 읽어 DB Upsert. 위치 못 읽으면 중단(쓰레기 좌표 금지). (FR-PRESET-03)</summary>
    private async Task HandlePresetSaveAsync(CameraStreamPopupViewModel vm, string name)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        try
        {
            var pos = await ptz.GetStatusAsync(vm.CameraId).ConfigureAwait(false);
            if (pos == null) { _log?.Warning($"[CameraPopup] 프리셋 저장 중단 — 현재 위치 읽기 실패 cam={vm.CameraId}"); return; }

            var model = new PtzPresetModel
            {
                CameraId = vm.CameraId,
                PresetName = name,
                Pan = pos.Pan,
                Tilt = pos.Tilt,
                Zoom = pos.Zoom,
                PanTiltSpace = pos.PanTiltSpace,
                ZoomSpace = pos.ZoomSpace,
            };
            await PtzPresetStore.SaveAsync(model).ConfigureAwait(false);
            await LoadPresetsAsync(vm).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Error($"[CameraPopup] 프리셋 저장 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    private void OnCameraPopupPresetDelete(object? sender, IPtzPresetModel preset)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandlePresetDeleteAsync(vm, preset);   // cam:control (FR-EN-06)
    }

    private async Task HandlePresetDeleteAsync(CameraStreamPopupViewModel vm, IPtzPresetModel preset)
    {
        try
        {
            await PtzPresetStore.DeleteAsync(preset.Id).ConfigureAwait(false);
            await LoadPresetsAsync(vm).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Error($"[CameraPopup] 프리셋 삭제 실패 id={preset.Id}: {ex.Message}"); }
    }

    private void OnCameraPopupPresetHome(object? sender, IPtzPresetModel preset)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandlePresetHomeAsync(vm, preset);   // cam:control (FR-EN-06)
    }

    private async Task HandlePresetHomeAsync(CameraStreamPopupViewModel vm, IPtzPresetModel preset)
    {
        try
        {
            await PtzPresetStore.SetHomeAsync(vm.CameraId, preset.Id).ConfigureAwait(false);
            await LoadPresetsAsync(vm).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Error($"[CameraPopup] Home 설정 실패 id={preset.Id}: {ex.Message}"); }
    }

    /*──────────────── 영상 옵션(주야간/포커스) 핸들러 ────────────────*/

    private void OnCameraPopupOptionsReload(object? sender, EventArgs e)
    {
        if (sender is CameraStreamPopupViewModel vm) _ = LoadImagingAsync(vm);
    }

    /// <summary>옵션 탭 진입 → 영상 옵션(주야간/포커스) 조회·반영. (FR-OPT-01/02/03)</summary>
    private async Task LoadImagingAsync(CameraStreamPopupViewModel vm)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        try
        {
            var capable = ptz.IsImagingCapable(vm.CameraId);
            var st = capable ? await ptz.GetImagingAsync(vm.CameraId).ConfigureAwait(false) : null;
            await OnUiAsync(() =>
            {
                vm.IsImagingCapable = capable && st != null;
                if (st != null) vm.SetImagingState(st.IrCutFilter, st.AutoFocus);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Warning($"[CameraPopup] 영상 옵션 로드 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    private void OnCameraPopupIrCutFilter(object? sender, string mode)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandleIrCutFilterAsync(vm, mode);   // cam:imaging→잠정 cam:control (OQ-PG-04 전, FR-EN-06)
    }

    private async Task HandleIrCutFilterAsync(CameraStreamPopupViewModel vm, string mode)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        try
        {
            if (await ptz.SetIrCutFilterAsync(vm.CameraId, mode).ConfigureAwait(false))
                await LoadImagingAsync(vm).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Error($"[CameraPopup] 주야간 설정 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    private void OnCameraPopupAutoFocus(object? sender, bool auto)
    {
        if (sender is CameraStreamPopupViewModel vm && CanControlCamera()) _ = HandleAutoFocusAsync(vm, auto);   // cam:imaging→잠정 cam:control (OQ-PG-04 전, FR-EN-06)
    }

    private async Task HandleAutoFocusAsync(CameraStreamPopupViewModel vm, bool auto)
    {
        var ptz = ResolvePtzController();
        if (ptz == null) return;
        try
        {
            if (await ptz.SetAutoFocusAsync(vm.CameraId, auto).ConfigureAwait(false))
                await LoadImagingAsync(vm).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Error($"[CameraPopup] 포커스 설정 실패 cam={vm.CameraId}: {MaskRtspCredentials(ex.Message)}"); }
    }

    /// <summary>UI 스레드 마샬링(백그라운드 ONVIF 호출 후 VM/심볼 갱신용).</summary>
    private static Task OnUiAsync(System.Action action)
    {
        var disp = System.Windows.Application.Current?.Dispatcher;
        if (disp == null || disp.CheckAccess()) { action(); return Task.CompletedTask; }
        return disp.InvokeAsync(action).Task;
    }

    private void OnCameraPopupSelectRequested(object? sender, EventArgs e)
    {
        if (sender is not CameraStreamPopupViewModel vm) return;
        SelectedCameraPopup = vm;
        BringCameraPopupToFront(vm);
    }

    private int _popupZCounter;   // Panel.ZIndex 증가 카운터(팝업 간 상대 z-order)

    /// <summary>팝업을 최상위로 — Panel.ZIndex 증가. 컬렉션 Move 금지(ItemsControl 컨테이너 재생성=RTSP 영상 끊김 방지). (FR-SEL-02)</summary>
    private void BringCameraPopupToFront(CameraStreamPopupViewModel vm)
    {
        vm.ZIndex = ++_popupZCounter;
    }

    /// <summary>자동해제 타이머 시작/리셋 — IsAutoDiscard ON일 때 TimeoutSeconds 후 팝업 자동 닫힘.</summary>
    private void StartOrResetAutoCloseTimer(CameraStreamPopupViewModel vm)
    {
        var setup = ResolveStreamingSetup();
        if (setup == null || !setup.IsAutoDiscard || setup.TimeoutSeconds <= 0)
        {
            StopAutoCloseTimer(vm);   // 자동해제 OFF면 기존 타이머 제거
            return;
        }

        if (!_popupAutoCloseTimers.TryGetValue(vm, out var timer))
        {
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += (s, e) =>
            {
                StopAutoCloseTimer(vm);
                _ = CloseCameraPopupAsync(vm);   // 타임아웃 만료 → 팝업 닫힘(+Hub Lease 해제)
            };
            _popupAutoCloseTimers[vm] = timer;
        }
        timer.Stop();
        timer.Interval = TimeSpan.FromSeconds(setup.TimeoutSeconds);
        timer.Start();   // 리셋(상호작용 시 재시작)
    }

    private void StopAutoCloseTimer(CameraStreamPopupViewModel vm)
    {
        if (_popupAutoCloseTimers.TryGetValue(vm, out var timer))
        {
            timer.Stop();
            _popupAutoCloseTimers.Remove(vm);
        }
    }

    private async Task OpenCameraStreamPopupAsync(int cameraId, string? title, RtspConnectionInfo connInfo, IEditableMarker marker)
    {
        try
        {
            if (MainMap == null) return;

            // 중복 더블클릭 → 기존 팝업 포커스(맨 앞으로)
            var existing = CameraPopups.FirstOrDefault(p => p.CameraId == cameraId);
            if (existing != null)
            {
                BringCameraPopupToFront(existing);      // Move 금지 — ZIndex로 최상위(영상 유지)
                SelectedCameraPopup = existing;
                StartOrResetAutoCloseTimer(existing);   // 재더블클릭 = 상호작용 → 카운트 리셋
                return;
            }

            var hub = ResolveHub();
            if (hub == null) return;

            // 위치: 저장된 AnchorGeo 우선, 없으면 카메라 심볼 우상단(중점+오른쪽100/위100에 팝업 좌하단)
            PointLatLng anchorGeo;
            double left, top;
            var saved = await CameraPopupPositionStore.TryGetPositionAsync(cameraId);
            if (saved != null)
            {
                anchorGeo = new PointLatLng(saved.Latitude, saved.Longitude);
                // inner(타일) → outer(화면) 보정 — 팝업은 디지털줌 RenderTransform 밖 캔버스에 산다(InnerToOuter는 scale=1이면 항등)
                var g = MainMap.FromLatLngToLocal(anchorGeo);
                var sp = MainMap.InnerToOuter(new Point(g.X, g.Y));
                left = sp.X; top = sp.Y;
            }
            else
            {
                var g = MainMap.FromLatLngToLocal(marker.Position);
                var c = MainMap.InnerToOuter(new Point(g.X, g.Y));   // 심볼의 화면(outer) 위치 기준 우상단 오프셋
                left = c.X + CameraPopupInitialOffsetRight;
                top = c.Y - CameraPopupInitialOffsetUp - CameraStreamPopupViewModel.DefaultHeight;
                // outer(화면) → inner → 위경도 앵커
                var ip = MainMap.OuterToInner(new Point(left, top));
                anchorGeo = MainMap.FromLocalToLatLng((int)ip.X, (int)ip.Y);
            }

            // 연결선(Leader Line) 끝점1 = 카메라 심볼 중점(화면 outer 좌표)
            var cg = MainMap.FromLatLngToLocal(marker.Position);
            var camScreen = MainMap.InnerToOuter(new Point(cg.X, cg.Y));
            var vm = new CameraStreamPopupViewModel(cameraId, title, connInfo, anchorGeo, hub)
            {
                CanvasLeft = left,
                CanvasTop = top,
                CameraGeo = marker.Position,
                CameraScreenX = camScreen.X,
                CameraScreenY = camScreen.Y,
            };
            vm.CloseRequested += OnCameraPopupCloseRequested;
            vm.DragCompleted += OnCameraPopupDragCompleted;
            vm.PtzDragRequested += OnCameraPopupPtzDragRequested;   // 좌버튼 PTZ 드래그 → IPtzController
            vm.PtzZoomRequested += OnCameraPopupPtzZoom;            // 휠 → 상대 줌(펄스)
            vm.ZoomHoldRequested += OnCameraPopupZoomHold;          // 줌 +/- 누름 → 연속 줌(뗌=PtzStop)
            vm.FocusHoldRequested += OnCameraPopupFocusHold;        // 포커스 +/- 누름 → 연속 포커스
            vm.FocusStopRequested += OnCameraPopupFocusStop;        // 포커스 뗌/캡처분실 → ImagingClient Stop
            vm.SelectRequested += OnCameraPopupSelectRequested;     // 좌클릭 → 선택+맨앞
            vm.PtzNudgeRequested += OnCameraPopupPtzNudge;          // PTZ 탭 방향 패드
            vm.PtzStopRequested += OnCameraPopupPtzStop;
            vm.PresetsReloadRequested += OnCameraPopupPresetsReload; // 프리셋 탭 로드/이동/저장/삭제/Home
            vm.PresetGotoRequested += OnCameraPopupPresetGoto;
            vm.PresetSaveRequested += OnCameraPopupPresetSave;
            vm.PresetDeleteRequested += OnCameraPopupPresetDelete;
            vm.PresetHomeRequested += OnCameraPopupPresetHome;
            vm.OptionsReloadRequested += OnCameraPopupOptionsReload;  // 옵션 탭 주야간/포커스
            vm.IrCutFilterRequested += OnCameraPopupIrCutFilter;
            vm.AutoFocusRequested += OnCameraPopupAutoFocus;
            CameraPopups.Add(vm);
            SelectedCameraPopup = vm;          // 오픈 시 자동 선택(단일)
            BringCameraPopupToFront(vm);       // 새 팝업 최상위(ZIndex)
            StartOrResetAutoCloseTimer(vm);   // 자동해제 타이머 시작(IsAutoDiscard ON 시)

            // ONVIF PTZ 준비(비동기) → IsPtzCapable 설정(PTZ 카메라만 우버튼 활성)
            if ((marker as IPidsEditableMarker)?.LinkedDevice is ICameraDeviceModel camModel)
                _ = EnsurePtzReadyAsync(vm, camModel);
        }
        catch (Exception ex)
        {
            _log?.Error($"카메라 팝업 오픈 실패(CameraId={cameraId}): {ex.Message}");
        }
    }

    private async void OnCameraPopupCloseRequested(object? sender, EventArgs e)
    {
        if (sender is CameraStreamPopupViewModel vm) await CloseCameraPopupAsync(vm);
    }

    private async void OnCameraPopupDragCompleted(object? sender, EventArgs e)
    {
        if (sender is not CameraStreamPopupViewModel vm || MainMap == null) return;
        try
        {
            // 드래그 종료 위치(팝업 좌상단, 화면 outer) → inner 역보정 → 위경도 앵커 갱신 + DB 저장(다중 클라 공유)
            var ip = MainMap.OuterToInner(new Point(vm.CanvasLeft, vm.CanvasTop));
            var geo = MainMap.FromLocalToLatLng((int)ip.X, (int)ip.Y);
            vm.AnchorGeo = geo;
            StartOrResetAutoCloseTimer(vm);   // 드래그 = 상호작용 → 카운트 리셋
            await CameraPopupPositionStore.SavePositionAsync(vm.CameraId, geo);
        }
        catch (Exception ex) { _log?.Error($"카메라 팝업 위치 저장 실패: {ex.Message}"); }
    }

    private async Task CloseCameraPopupAsync(CameraStreamPopupViewModel vm)
    {
        try
        {
            StopAutoCloseTimer(vm);    // 자동해제 타이머 정지
            vm.CloseRequested -= OnCameraPopupCloseRequested;
            vm.DragCompleted -= OnCameraPopupDragCompleted;
            vm.PtzDragRequested -= OnCameraPopupPtzDragRequested;
            vm.PtzZoomRequested -= OnCameraPopupPtzZoom;
            vm.ZoomHoldRequested -= OnCameraPopupZoomHold;
            vm.FocusHoldRequested -= OnCameraPopupFocusHold;
            vm.FocusStopRequested -= OnCameraPopupFocusStop;
            vm.SelectRequested -= OnCameraPopupSelectRequested;
            vm.PtzNudgeRequested -= OnCameraPopupPtzNudge;
            vm.PtzStopRequested -= OnCameraPopupPtzStop;
            vm.PresetsReloadRequested -= OnCameraPopupPresetsReload;
            vm.PresetGotoRequested -= OnCameraPopupPresetGoto;
            vm.PresetSaveRequested -= OnCameraPopupPresetSave;
            vm.PresetDeleteRequested -= OnCameraPopupPresetDelete;
            vm.PresetHomeRequested -= OnCameraPopupPresetHome;
            vm.OptionsReloadRequested -= OnCameraPopupOptionsReload;
            vm.IrCutFilterRequested -= OnCameraPopupIrCutFilter;
            vm.AutoFocusRequested -= OnCameraPopupAutoFocus;
            if (ReferenceEquals(_selectedCameraPopup, vm)) SelectedCameraPopup = null;   // dangling 방지(FR-SEL-04)
            // 인스턴스 유지: 팝업 닫기 시 ONVIF/PTZ 인스턴스는 Release하지 않고 워밍 유지(재오픈 즉시). 이동만 정지.
            // Release는 카메라 심볼/모델 삭제 시에만(Markers_CollectionChangedForCameraPopups).
            var ptzOnClose = ResolvePtzController();
            if (ptzOnClose != null)
            {
                _ = ptzOnClose.StopAsync(vm.CameraId);        // PTZ(팬틸트+줌) 정지
                _ = ptzOnClose.StopFocusAsync(vm.CameraId);   // 포커스 hold 중 닫기 → ImagingClient 모터 정지(F-03, 가드 내장)
            }
            if (_ptzGestureCts.TryRemove(vm.CameraId, out var gcts)) { try { gcts.Cancel(); } catch { } gcts.Dispose(); }
            CameraPopups.Remove(vm);
            await vm.DisposeAsync();   // Hub Lease 해제(C-03)
        }
        catch (Exception ex) { _log?.Error($"카메라 팝업 닫기 실패: {ex.Message}"); }
    }

    /// <summary>팬/줌 시 모든 팝업을 AnchorGeo 기준으로 재배치(Geo 추종 — 자동숨김/클램프 없음).</summary>
    private void RefreshCameraPopupPositions()
    {
        if (MainMap == null || _cameraPopups == null || _cameraPopups.Count == 0) return;
        foreach (var vm in _cameraPopups.ToList())   // 순회 중 Close(Remove) 재진입 방어
        {
            // 카메라 심볼 화면점(연결선 끝점1) 갱신 — 팬/줌 추종 + 디지털줌 outer 보정(InnerToOuter는 scale=1이면 항등)
            var cs = MainMap.FromLatLngToLocal(vm.CameraGeo);
            var cso = MainMap.InnerToOuter(new Point(cs.X, cs.Y));
            vm.CameraScreenX = cso.X;
            vm.CameraScreenY = cso.Y;

            var sp = MainMap.FromLatLngToLocal(vm.AnchorGeo);
            var spo = MainMap.InnerToOuter(new Point(sp.X, sp.Y));
            vm.CanvasLeft = spo.X;
            vm.CanvasTop = spo.Y;
        }
    }

    /// <summary>FR-13: 카메라 심볼이 맵에서 제거(레이어 off·삭제)되면 해당 팝업 자동 닫기+Dispose.</summary>
    private void Markers_CollectionChangedForCameraPopups(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        try
        {
            if (_cameraPopups == null || _cameraPopups.Count == 0) return;

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                foreach (var old in e.OldItems.OfType<GMapPidsMarker>())
                {
                    // 심볼/모델 삭제 = ONVIF 인스턴스 정리(워밍 해제). 팝업이 열려 있으면 닫기.
                    ResolvePtzController()?.Release(old.LinkedDeviceId);
                    var vm = _cameraPopups.FirstOrDefault(p => p.CameraId == old.LinkedDeviceId);
                    if (vm != null) _ = CloseCameraPopupAsync(vm);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                foreach (var vm in _cameraPopups.ToList()) _ = CloseCameraPopupAsync(vm);
            }
        }
        catch (Exception ex) { _log?.Error($"카메라 팝업 심볼제거 처리 실패: {ex.Message}"); }
    }

    #endregion

    /// <summary>
    /// 지도 이미지 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapImageClicked(GMapCustomImage image)
    {
        try
        {
            _log?.Info($"이미지 클릭됨: {image.Title}");

            if (IsEditModeEnabled)
            {
                // 편집 모드에서는 이미지를 선택
                SelectImageForEditing(image);
            }
            else
            {
                // 일반 모드에서는 단순 선택만
                UpdateSelectedImage(image);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 이미지 편집(이동/리사이즈/회전) 완료 핸들러 — UserRotation/Bounds DB 영속화 (FR-8, NFR-6)
    /// </summary>
    private async void OnMapImageEditCompleted(GMapCustomImage image, GMap.NET.RectLatLng beforeBounds, double beforeRotation)
    {
        try
        {
            if (image == null) return;
            if (!CanEditMap()) { _log?.Warning("[RBAC] 맵 편집 권한 없음 — 이미지 편집 영속 차단(백스톱)"); return; }

            // GMapCustomImage.Model은 ImageBounds(Deconstruct)·UserRotation(_model.Rotation)이 동기화된 상태.
            // MapCorrectionRotation은 런타임 전용이라 모델에 반영되지 않음 → DB엔 UserRotation만 저장됨 (NFR-6).
            _log?.Info($"[IMG-EDIT-DONE] Title={image.Title}, UserRotation={image.UserRotation:F1}, Bounds={image.ImageBounds}");

            if (image.Id > 0)
            {
                await _gMapDbSymbolService.UpdateImageAsync(image.Model);
            }
            else
            {
                _log?.Warning($"[IMG-EDIT-DONE] 영속 경로 없음(Id<=0) — DB 저장 생략: {image.Title}");
            }
            // Undo 기록(D1) — 핸들 편집(이동/크기/회전). IsApplyingUndo 중이면 recorder가 무시.
            if (image.Id > 0)
                _editRecorder?.RecordCustomImageEdit(image.Id, beforeBounds, beforeRotation, image.ImageBounds, image.UserRotation);
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 편집 완료 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 이미지 우클릭 핸들러 — 회전 초기화/프리셋 컨텍스트 메뉴 (FR-9)
    /// </summary>
    private void OnMapImageRightClicked(GMapCustomImage image)
    {
        try
        {
            ShowImageContextMenu(image, new Point());
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 우클릭 처리 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 이미지 우클릭 컨텍스트 메뉴 — 회전 초기화/프리셋(0/90/180/270°). (FR-9, NFR-7)
    /// 다이얼로그 금지 컨벤션에 따라 자유 각도 입력 대신 프리셋 제공.
    /// </summary>
    public void ShowImageContextMenu(GMapCustomImage image, Point screenPosition)
    {
        try
        {
            if (image == null) return;

            var menu = new ContextMenu();

            void AddRotateItem(string header, double angle, MaterialDesignThemes.Wpf.PackIconKind icon)
            {
                var item = new MenuItem
                {
                    Header = header,
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = icon, Width = 16, Height = 16 }
                };
                item.Click += async (s, e) =>
                {
                    try
                    {
                        if (!CanEditMap()) { _log?.Warning("[RBAC] 맵 편집 권한 없음 — 이미지 회전 차단"); ShowNoMapEditPermissionInfo(); return; }
                        var beforeRot = image.UserRotation;   // Undo용 이전 회전(AREA 2)
                        image.UserRotation = angle;     // EffectiveRotation 자동 갱신
                        MainMap?.InvalidateVisual();
                        if (image.Id > 0)
                            await _gMapDbSymbolService.UpdateImageAsync(image.Model);  // UserRotation만 영속 (NFR-6)
                        if (image.Id > 0) _editRecorder?.RecordCustomImageRotation(image.Id, beforeRot, angle);   // Undo 기록
                    }
                    catch (Exception ex) { _log?.Error($"이미지 회전 설정 저장 실패: {ex.Message}"); }
                };
                menu.Items.Add(item);
            }

            AddRotateItem("회전 초기화 (0°)", 0, MaterialDesignThemes.Wpf.PackIconKind.RotateRight);
            AddRotateItem("90° 회전", 90, MaterialDesignThemes.Wpf.PackIconKind.RotateRight);
            AddRotateItem("180° 회전", 180, MaterialDesignThemes.Wpf.PackIconKind.RotateRight);
            AddRotateItem("270° 회전", 270, MaterialDesignThemes.Wpf.PackIconKind.RotateRight);

            if (menu.Items.Count > 0)
            {
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 컨텍스트 메뉴 표시 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지도 빈 공간 클릭 이벤트 핸들러
    /// </summary>
    private void OnMapClicked(PointLatLng geoPos, Point screenPos)
    {
        try
        {
            ClickedCurrentPosition = geoPos;
            //_log?.Info($"지도 클릭: ({geoPos.Lat:F6}, {geoPos.Lng:F6})");

            // 편집 모드에서 빈 공간 클릭 시 모든 선택 해제
            if (IsEditModeEnabled)
            {
                ClearAllSelections();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 클릭 처리 실패: {ex.Message}");
        }
    }
    #endregion

    #region - Adorner 이벤트 핸들러 -
    /// <summary>
    /// 마커 편집 시작 이벤트 핸들러
    /// </summary>
    private void OnMarkerEditStarted(object? sender, MarkerEditStartedEventArgs e)
    {
        _log?.Info($"마커 편집 시작: {e.Marker.Title}, 핸들: {e.Handle}");

        // UI 상태 업데이트
        IsMarkerEditing = true;
    }

    /// <summary>
    /// 마커 편집 완료 이벤트 핸들러
    /// </summary>
    private async void OnMarkerEditCompleted(object? sender, MarkerEditCompletedEventArgs e)
    {
        _log?.Info($"마커 편집 완료: {e.Marker.Title}");
        _log?.Info($"변경사항: {e.GetChangesSummary()}");

        if (!CanEditMap()) { _log?.Warning("[RBAC] 맵 편집 권한 없음 — 마커 편집 영속 차단(백스톱)"); return; }
        await DbUpdateProcess(e.Marker);
        // Line/Area 리사이즈(점 스케일)는 스냅샷 점 커맨드로 — TransformCommand는 점 미복원 + line은 HasChanges=false라
        // early-return되어 undo 미기록+즉시영속 파괴적(§5-C R-07). 어도너가 line resize일 때만 OriginalLinePoints 세팅.
        if (e.Marker is GMapSymbols.ILineEditableMarker && e.OriginalLinePoints != null)
            _editRecorder?.RecordLineGeometry(e.Marker, e.OriginalLinePoints, e.OriginalPosition);
        else
            _editRecorder?.RecordTransform(e);   // Undo 기록(이동/크기/회전 — 비-line 또는 line 이동/회전)

        // UI 상태 업데이트
        IsMarkerEditing = false;

        // 선택된 마커 속성들 갱신
        //if (SelectedMarker?.Id == e.Marker.Id)
        //{
        //    NotifyOfPropertyChange(nameof(SelectedMarkerBearing));
        //    NotifyOfPropertyChange(nameof(SelectedMarkerWidth));
        //    NotifyOfPropertyChange(nameof(SelectedMarkerHeight));
        //}
    }

    /// <summary>
    /// 마커 편집 취소 이벤트 핸들러
    /// </summary>
    private void OnMarkerEditCancelled(object? sender, MarkerEditCancelledEventArgs e)
    {
        _log?.Info($"마커 편집 취소: {e.Marker.Title}, 이유: {e.Reason}");

        // UI 상태 복원
        IsMarkerEditing = false;
    }

    /// <summary>
    /// Adorner 생성 이벤트 핸들러
    /// </summary>
    private void OnAdornerCreated(object? sender, AdornerLifecycleEventArgs e)
    {
        //_log?.Info($"Adorner 생성됨: {e.Marker.Title}");
        AdornerCount++;
    }

    /// <summary>
    /// Adorner 제거 이벤트 핸들러
    /// </summary>
    private void OnAdornerRemoved(object? sender, AdornerLifecycleEventArgs e)
    {
        //_log?.Info($"Adorner 제거됨: {e.Marker.Title}");
        AdornerCount = Math.Max(0, AdornerCount - 1);
    }
    #endregion

    #region - 선택 관리 메서드 -
    /// <summary>
    /// 편집을 위한 마커 선택
    /// </summary>
    private bool _isSelectingMarker;   // T4-B: 본체 클릭이 Shape+GMapCustomControl 양쪽에서 OnMarkerClicked 이중 발화 → 재진입 방지
    private async void SelectMarkerForEditing(IEditableMarker marker)
    {
        // ★ 재진입 가드 — 동일 클릭의 2차 발화가 DbUpdateProcess를 중복 트리거하지 않게 차단.
        //   1차(Shape.TriggerMarkerClicked)가 올바른 마커를 선택하고, 2차(GetMarkerAtScreen)는 여기서 무시된다.
        if (_isSelectingMarker) return;
        _isSelectingMarker = true;
        try
        {
            //_log?.Info($"편집을 위한 마커 선택 시작: {marker.Title}");

            if(SelectedMarker != null && CanEditMap() && !IsApplyingUndo)
                await DbUpdateProcess(SelectedMarker);

            _editRecorder?.CaptureSelectionBaseline(marker);   // 라벨/속성 before용 baseline

            // 이전 선택 해제
            ClearAllSelections();
            //_log?.Info("이전 선택 해제 완료");

            if (MainMap == null) return;


            // 새 마커 선택 및 Adorner 생성
            //_log?.Info($"MainMap.SelectMarker 호출 중...");
            bool success = MainMap.SelectMarker(marker);
            //_log?.Info($"MainMap.SelectMarker 결과: {success}");

            if (success)
            {
                UpdateSelectedMarker(marker);
                ShowPropertyPanel();
                //_log?.Info($"마커 편집 모드 활성화 완료: {marker.Title}");
            }
            else
            {
                _log?.Warning($"마커 선택 실패: {marker.Title}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 편집 선택 실패: {ex.Message}");
        }
        finally
        {
            _isSelectingMarker = false;
        }
    }

    /// <summary>
    /// 편집을 위한 이미지 선택
    /// </summary>
    private void SelectImageForEditing(GMapCustomImage image)
    {
        try
        {
            // 이전 선택 해제
            ClearAllSelections();

            // 새 이미지 선택
            UpdateSelectedImage(image);
            image.IsSelected = true;

            _log?.Info($"이미지 편집 모드 활성화: {image.Title}");
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 편집 선택 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 선택된 마커 업데이트
    /// </summary>
    private void UpdateSelectedMarker(IEditableMarker marker)
    {

        //_log?.Info($"UpdateSelectedMarker 시작(marker) - {GetMarkerInfo(marker)}");

        SelectedMarker = marker;
        //_log?.Info($"SelectedMarker 설정 후(Selectedmarker) - {GetMarkerInfo(SelectedMarker)}");


        SelectedImage = null; // 이미지 선택 해제

        NotifyOfPropertyChange(nameof(CanEditMarker));
        //_log?.Info($"UpdateSelectedMarker 완료 - {GetMarkerInfo(marker)}");
    }

    /// <summary>
    /// 선택된 이미지 업데이트
    /// </summary>
    private void UpdateSelectedImage(GMapCustomImage image)
    {
        SelectedImage = image;
        SelectedMarker = null; // 마커 선택 해제
    }

    /// <summary>
    /// 모든 선택 해제
    /// </summary>
    private void ClearAllSelections()
    {
        try
        {
            // Adorner 모든 해제
            MainMap?.DeselectAllMarkers();

            // 그룹(러버밴드) 다중선택 해제 — 빈공간 클릭·편집모드 종료 시 함께 취소(M2/M3)
            _groupSelection?.Clear();

            // 이미지 선택 해제
            if (MainMap?.CustomImages != null)
            {
                foreach (var img in MainMap.CustomImages)
                {
                    img.IsSelected = false;
                }
            }



            // ViewModel 속성 초기화
            SelectedMarker = null;
            SelectedImage = null;

            HidePropertyPanel();
            NotifyOfPropertyChange(nameof(HasSelectedItem));   // 전체 해제 → 버튼 비활성 갱신

            //_log?.Info("모든 선택 해제 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 해제 실패: {ex.Message}");
        }
    }
    #endregion

    #region - 명령어 초기화 -
    /// <summary>
    /// 모든 RelayCommand 초기화
    /// </summary>
    private void InitializeCommands()
    {
        InitializeFileCommands();
        InitializeMapCommands();
        InitializeNavigationCommands();
        InitializeEditCommands();
        InitializeRotationCommands();
        InitializeMarkerEditCommands();
        InitializeAdornerCommands();
        InitializeLineAdornerCommands();
    }

   

    /// <summary>
    /// 파일 관련 명령어 초기화
    /// </summary>
    private void InitializeFileCommands()
    {
        LoadMapImageCommand = new RelayCommand(ExecuteLoadMapImage, CanExecuteLoadImageMap);
        LoadImageOverlayCommand = new RelayCommand(ExecuteLoadImageOverlay, CanExecuteLoadImageOverlay);
        CreateCustomMapCommand = new RelayCommand(ExecuteCreateCustomMap, CanExecuteCreateCustomMap);
        ExitApplicationCommand = new RelayCommand(ExecuteExitApplication, CanExecuteExitApplication);
    }

    /// <summary>
    /// 지도 표시 관련 명령어 초기화
    /// </summary>
    private void InitializeMapCommands()
    {
        ToggleWGS84Command = new RelayCommand(ExecuteToggleWGS84Command, CanExecuteToggleWGS84Command);
        ToggleMGRSCommand = new RelayCommand(ExecuteToggleMGRSCommand, CanExecuteToggleMGRSCommand);
        ToggleSnapToGridCommand = new RelayCommand(_ => IsSnapToGridEnabled = !IsSnapToGridEnabled);
        ToggleUTMCommand = new RelayCommand(ExecuteToggleUTMCommand, CanExecuteToggleUTMCommand);
    }

    /// <summary>
    /// 네비게이션 관련 명령어 초기화
    /// </summary>
    private void InitializeNavigationCommands()
    {
        MoveHomeLocationCommand = new RelayCommand(ExecuteMoveHomeLocation, CanExecuteMoveHomeLocation);
        SetHomeLocationCommand = new RelayCommand(ExecuteSetHomeLocation, CanExecuteSetHomeLocation);
        ShowMapRoiPanelCommand = new RelayCommand(_ => ShowMapRoiPanel());
        ZoomInCommand = new RelayCommand(_ =>
        {
            if (MainMap == null) return;
            if (MainMap.Zoom < ZoomMax) MainMap.Zoom++;
            else MainMap.StepDigitalZoom(+1);   // MaxZoom 초과 → 디지털 줌(상한 도달 시 no-op)
        });
        ZoomOutCommand = new RelayCommand(_ =>
        {
            if (MainMap == null) return;
            if (MainMap.DigitalZoomLevel > 0) MainMap.StepDigitalZoom(-1);   // 디지털 우선 감소
            else if (MainMap.Zoom > ZoomMin) MainMap.Zoom--;
        });
        ShowLayerPanelCommand = new RelayCommand(_ => ShowLayerPanel());
        TogglePlaybackPanelCommand = new RelayCommand(_ => TogglePlaybackPanel());
        ToggleTrackingSettingsPanelCommand = new RelayCommand(_ => ToggleTrackingSettingsPanel());
    }

    #region - Tracking Playback(P5) -
    public RelayCommand? TogglePlaybackPanelCommand { get; private set; }

    public GMapControls.PlaybackConsoleControl? PlaybackPanel
    {
        get => _playbackPanel;
        private set { _playbackPanel = value; NotifyOfPropertyChange(nameof(PlaybackPanel)); }
    }

    public bool IsPlaybackPanelVisible
    {
        get => _isPlaybackPanelVisible;
        set { _isPlaybackPanelVisible = value; NotifyOfPropertyChange(nameof(IsPlaybackPanelVisible)); }
    }

    /// <summary>맵 툴바 재생 버튼 — 콘솔 열기/닫기.</summary>
    public void TogglePlaybackPanel()
    {
        if (_playbackVm == null) { _log?.Warning("Playback VM 미주입"); return; }
        if (IsPlaybackPanelVisible) { _playbackVm.Close(); return; }   // Close→CloseRequested→숨김

        if (PlaybackPanel == null)
        {
            PlaybackPanel = new GMapControls.PlaybackConsoleControl { DataContext = _playbackVm };
            _playbackVm.CloseRequested += () => IsPlaybackPanelVisible = false;
            _playbackVm.FocusRequested += (lat, lng) => { if (MainMap != null) MainMap.Position = new PointLatLng(lat, lng); };
            if (MainMap != null) _playbackVm.AttachMap(MainMap);
        }
        IsPlaybackPanelVisible = true;
    }
    #endregion

    #region - Tracking 설정(P3-04) -
    public RelayCommand? ToggleTrackingSettingsPanelCommand { get; private set; }

    public GMapControls.TrackingSettingsControl? TrackingSettingsPanel
    {
        get => _trackingSettingsPanel;
        private set { _trackingSettingsPanel = value; NotifyOfPropertyChange(nameof(TrackingSettingsPanel)); }
    }

    public bool IsTrackingSettingsPanelVisible
    {
        get => _isTrackingSettingsPanelVisible;
        set { _isTrackingSettingsPanelVisible = value; NotifyOfPropertyChange(nameof(IsTrackingSettingsPanelVisible)); }
    }

    /// <summary>맵 툴바 설정 버튼 — 추적 설정 패널 열기/닫기.</summary>
    public void ToggleTrackingSettingsPanel()
    {
        if (_trackingSetupVm == null) { _log?.Warning("TrackingSetup VM 미주입"); return; }
        if (IsTrackingSettingsPanelVisible) { IsTrackingSettingsPanelVisible = false; return; }

        if (TrackingSettingsPanel == null)
        {
            TrackingSettingsPanel = new GMapControls.TrackingSettingsControl { DataContext = _trackingSetupVm };
            _trackingSetupVm.CloseRequested += () => IsTrackingSettingsPanelVisible = false;
        }
        IsTrackingSettingsPanelVisible = true;
    }
    #endregion

    /// <summary>
    /// 편집 관련 명령어 초기화
    /// </summary>
    private void InitializeEditCommands()
    {
        ClearSelectionCommand = new RelayCommand(ExecuteClearSelection, CanExecuteClearSelection);
        DeleteSelectedCommand = new RelayCommand(ExecuteDeleteSelected, CanExecuteDeleteSelected);
        ToggleEditModeCommand = new RelayCommand(ExecuteToggleEditMode, CanExecuteToggleEditMode); // 새로 추가

    }

    /// <summary>
    /// 회전 관련 명령어 초기화
    /// </summary>
    private void InitializeRotationCommands()
    {
        RotateCommand = new RelayCommand(ExecuteRotate, CanExecuteRotate);
        FineRotateCommand = new RelayCommand(ExecuteFineRotate, CanExecuteFineRotate);
        ResetRotationCommand = new RelayCommand(ExecuteResetRotation, CanExecuteResetRotation);
    }

    /// <summary>
    /// 마커 편집 관련 명령어 초기화
    /// </summary>
    private void InitializeMarkerEditCommands()
    {
        AddSelectedSymbolCommand = new RelayCommand(ExecuteAddSelectedSymbol, CanExecuteAddSelectedSymbol);
        DuplicateMarkerCommand = new RelayCommand(ExecuteDuplicateMarker, CanExecuteDuplicateMarker);
        SnapMarkerToGridCommand = new RelayCommand(ExecuteSnapMarkerToGrid, CanExecuteSnapMarkerToGrid);
        ResetMarkerRotationCommand = new RelayCommand(ExecuteResetMarkerRotation, CanExecuteResetMarkerRotation);
        ResetMarkerSizeCommand = new RelayCommand(ExecuteResetMarkerSize, CanExecuteResetMarkerSize);
    }



    /// <summary>
    /// Adorner 관련 명령어 초기화
    /// </summary>
    private void InitializeAdornerCommands()
    {
        ToggleMultiSelectCommand = new RelayCommand(ExecuteToggleMultiSelect, CanExecuteToggleMultiSelect);
        CancelAllEditingCommand = new RelayCommand(ExecuteCancelAllEditing, CanExecuteCancelAllEditing);
    }

    /// <summary>
    /// Line Adorner 관련 명령어 초기화
    /// </summary>
    private void InitializeLineAdornerCommands()
    {
        StartLineDrawingCommand = new AsyncRelayCommand(ExecuteStartLineDrawing);
        CompleteLineDrawingCommand = new AsyncRelayCommand(ExecuteCompleteLineDrawing, CanExecuteCompleteLineDrawing);
        CancelLineDrawingCommand = new AsyncRelayCommand(ExecuteCancelLineDrawing, CanExecuteCancelLineDrawing);
        UndoLastPointCommand = new RelayCommand(ExecuteUndoLastPoint, CanExecuteUndoLastPoint);
    }

    #endregion

    #region - 파일 명령어 구현 -

    #region Image Overlay Command (Phase 29)

    /// <summary>
    /// 이미지 오버레이 로드 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteLoadImageOverlay(object arg) => MainMap != null;

    /// <summary>
    /// 이미지 오버레이 로드 실행 - 이미지 파일을 GMapImageMarker로 추가
    /// </summary>
    /// <remarks>
    /// Phase 30: 줌 레벨 기반 ImageBounds 계산
    /// - 하드코딩된 0.01° 대신 현재 줌 레벨에서 이미지 픽셀 크기에 맞는 degree 계산
    /// - FromLocalToLatLng()를 사용하여 화면 픽셀 → 지리 좌표 변환
    /// </remarks>
    private async void ExecuteLoadImageOverlay(object obj)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 이미지 오버레이 추가 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _log?.Info("이미지 오버레이 불러오기 시작");

            // 파일 다이얼로그 열기
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "이미지 파일 선택",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files (*.*)|*.*",
                DefaultExt = ".png",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var originalPath = openFileDialog.FileName;
                // 이미지를 Images/Overlays/에 복사하고 상대 경로 획득
                var filePath = _imageFileService.CopyImageToLocal(originalPath);
                var title = System.IO.Path.GetFileNameWithoutExtension(originalPath);
                // 항상 현재 지도 중심점 기준으로 배치
                var currentPosition = MainMap.Position;

                // 이미지 실제 크기 로드 (절대 경로로 변환하여 사용)
                var absolutePath = _imageFileService.GetAbsolutePath(filePath);
                double imageWidth = 200;  // 기본값
                double imageHeight = 200; // 기본값
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(absolutePath);
                    bitmap.EndInit();
                    imageWidth = bitmap.PixelWidth;
                    imageHeight = bitmap.PixelHeight;
                    _log?.Info($"이미지 원본 크기: {imageWidth}x{imageHeight}");
                }
                catch (Exception ex)
                {
                    _log?.Warning($"이미지 크기 로드 실패, 기본값 사용: {ex.Message}");
                }

                // 현재 줌 레벨에서 원본 픽셀 크기 → degree 변환
                var centerScreen = MainMap.FromLatLngToLocal(currentPosition);
                _log?.Info($"[DEBUG-LOAD] 지도중심={currentPosition.Lat:F6},{currentPosition.Lng:F6} → 화면좌표=({centerScreen.X},{centerScreen.Y}), Zoom={MainMap.Zoom}");

                var topLeftScreen = new GMap.NET.GPoint(
                    (long)(centerScreen.X - imageWidth / 2),
                    (long)(centerScreen.Y - imageHeight / 2));
                var bottomRightScreen = new GMap.NET.GPoint(
                    (long)(centerScreen.X + imageWidth / 2),
                    (long)(centerScreen.Y + imageHeight / 2));
                _log?.Info($"[DEBUG-LOAD] imageW={imageWidth}, imageH={imageHeight} → topLeftScreen=({topLeftScreen.X},{topLeftScreen.Y}), bottomRightScreen=({bottomRightScreen.X},{bottomRightScreen.Y})");

                var topLeftGeo = MainMap.FromLocalToLatLng((int)topLeftScreen.X, (int)topLeftScreen.Y);
                var bottomRightGeo = MainMap.FromLocalToLatLng((int)bottomRightScreen.X, (int)bottomRightScreen.Y);
                _log?.Info($"[DEBUG-LOAD] → Geo: TopLeft=({topLeftGeo.Lat:F6},{topLeftGeo.Lng:F6}), BottomRight=({bottomRightGeo.Lat:F6},{bottomRightGeo.Lng:F6})");

                // ImageModel 생성
                var imageModel = new Ironwall.Dotnet.Monitoring.Models.Symbols.ImageModel
                {
                    Title = title,
                    FilePath = filePath,
                    Latitude = currentPosition.Lat,
                    Longitude = currentPosition.Lng,
                    Opacity = 0.8,
                    Rotation = 0.0,
                    Width = imageWidth,
                    Height = imageHeight,
                    Left = topLeftGeo.Lng,
                    Right = bottomRightGeo.Lng,
                    Top = topLeftGeo.Lat,
                    Bottom = bottomRightGeo.Lat
                };
                _log?.Info($"[DEBUG-LOAD] ImageModel: W={imageModel.Width},H={imageModel.Height}, Bounds=L:{imageModel.Left:F6},T:{imageModel.Top:F6},R:{imageModel.Right:F6},B:{imageModel.Bottom:F6}");

                // MarkerFactory로 마커 생성
                var marker = _markerFactory.CreateImageMarker(imageModel);

                if (marker != null)
                {
                    // 지도에 마커 추가
                    MainMap.Markers.Add(marker);
                    // EditMode 상태에 맞게 IsHitTestVisible 동기화
                    if (marker is GMapMarker gm && gm.Shape is UIElement shape)
                        shape.IsHitTestVisible = IsEditModeEnabled;

                    // DB에 저장
                    var savedId = await DbSaveProcess(marker);
                    if (savedId > 0)
                    {
                        imageModel.Id = savedId;
                        _log?.Info($"이미지 오버레이 추가 및 DB 저장 완료: {title} (Id={savedId})");

                        // MapLayers에 OverlayImage 레코드 INSERT (레이어 패널 연동)
                        var nextZOrder = await _gMapDbService.GetNextZOrderAsync("OverlayImage");
                        await _gMapDbService.InsertMapLayerAsync(new MapLayerModel
                        {
                            Name = title,
                            LayerType = "OverlayImage",
                            Category = "OverlayImage",
                            IsVisible = true,
                            Opacity = imageModel.Opacity,
                            ZOrder = nextZOrder,
                            FilePath = filePath,
                        });
                        await LoadLayersFromDbAsync();
                    }
                    else
                    {
                        _log?.Warning($"이미지 오버레이 추가됨, DB 저장 실패: {title}");
                    }

                    // 뷰 갱신
                    MainMap.InvalidateVisual();
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 불러오기 실패: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// 이미지 맵 로드 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteLoadImageMap(object arg) => true;

    /// <summary>
    /// 이미지 맵 로드 실행 - TIF/일반 이미지를 오버레이로 추가
    /// </summary>
    private async void ExecuteLoadMapImage(object obj)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 맵파일 추가 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _log?.Info("커스텀 맵 불러오기 시작");

            // 파일 다이얼로그 열기
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "TIF 파일 선택",
                Filter = "TIF Files (*.tif;*.tiff)|*.tif;*.tiff|All Files (*.*)|*.*",
                DefaultExt = ".tif",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var filePath = openFileDialog.FileName;
                var mapName = System.IO.Path.GetFileNameWithoutExtension(filePath);

                // 파일 확장자에 따라 적절한 메서드 호출
                GMapCustomImage image = null;
                var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                var currentPosition = ClickedCurrentPosition.IsEmpty ? MainMap.CenterPosition : ClickedCurrentPosition;

                if (extension == ".tif" || extension == ".tiff")
                {
                    image = await _imageOverlayService.CreateTifOverlayAsync(
                        filePath, currentPosition, MainMap, mapName);
                }
                else
                {
                    image = await _imageOverlayService.CreateImageOverlayAsync(
                        filePath, currentPosition, MainMap, mapName);
                }

                if (image != null)
                {
                    // 이미지가 표시되도록 ShowShape 확인
                    image.Visibility = true;

                    // GMapCustomControl에 이미지 추가
                    MainMap.AddImageOverlay(image);

                    // 뷰 갱신 강제
                    MainMap.InvalidateVisual();

                    _log?.Info($"이미지 오버레이 추가 완료: {mapName}");
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 불러오기 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 커스텀 맵 생성 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteCreateCustomMap(object arg) => SelectedImage != null;

    /// <summary>
    /// 커스텀 맵 생성 실행 — 등록 임베디드 패널 표시
    /// </summary>
    private void ExecuteCreateCustomMap(object obj)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 커스텀 맵 등록 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            if (SelectedImage == null) return;

            var imageFilePath = SelectedImage.FilePath;
            if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
            {
                _log?.Error($"이미지 파일을 찾을 수 없습니다: {imageFilePath}");
                return;
            }

            var fileInfo = new FileInfo(imageFilePath);

            // 등록 패널 생성 (Register Phase)
            var panel = new MapRegistrationControl
            {
                Phase = RegistrationPhase.Register,
                FileName = Path.GetFileName(imageFilePath),
                FileSize = FormatFileSize(fileInfo.Length),
                ImageResolution = $"{SelectedImage.Width} x {SelectedImage.Height} px",
                LayerName = SelectedImage.Title ?? "새 오버레이 맵",
                MinZoom = 10,
                MaxZoom = (int)Zoom,
            };

            // "등록" 이벤트 구독
            panel.RegisterRequested += async (s, args) =>
            {
                panel.Phase = RegistrationPhase.Progress;
                var cts = new CancellationTokenSource();
                panel.CancelRequested += (_, __) => cts.Cancel();

                var startTime = DateTime.Now;
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                timer.Tick += (_, __) =>
                {
                    var elapsed = DateTime.Now - startTime;
                    panel.ElapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                };
                timer.Start();

                var progress = new Progress<TileConversionProgress>(p =>
                {
                    panel.ProgressPercentage = p.ProgressPercentage;
                    panel.CurrentZoom = p.CurrentZoomLevel;
                    panel.ProcessedTiles = p.ProcessedTiles;
                    panel.TotalTiles = p.TotalTiles;
                });

                try
                {
                    var imageBounds = SelectedImage.ImageBounds;
                    var geoOptions = CreateGeoOptionsFromImageBounds(imageBounds, args.LayerName);
                    geoOptions.MinZoom = args.MinZoom;
                    geoOptions.MaxZoom = args.MaxZoom;

                    var customMap = await _customMapService.ProcessTifFileAsync(
                        imageFilePath, args.LayerName, geoOptions, progress);

                    timer.Stop();
                    var elapsed = DateTime.Now - startTime;
                    panel.ElapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                    panel.TotalTiles = customMap.TotalTileCount;

                    // 오버레이로 활성화
                    _customMapOverlayService.ActivateOverlay(customMap, MainMap);

                    // 원본 TIF 이미지 오버레이 제거
                    if (SelectedImage != null)
                    {
                        MainMap.RemoveImageOverlay(SelectedImage);
                        SelectedImage = null;
                    }

                    // MapLayers DB에 등록
                    var overlayMapZOrder = await _gMapDbService.GetNextZOrderAsync("OverlayMap");
                    await _gMapDbService.InsertMapLayerAsync(new MapLayerModel
                    {
                        Name = args.LayerName,
                        LayerType = "OverlayMap",
                        Category = "OverlayMap",
                        IsVisible = true,
                        Opacity = 1.0,
                        ZOrder = overlayMapZOrder,
                        MapId = customMap.Id,
                    });

                    // 레이어 패널 트리 갱신
                    await LoadLayersFromDbAsync();

                    panel.Phase = RegistrationPhase.Complete;
                }
                catch (OperationCanceledException)
                {
                    timer.Stop();
                    HideMapRegistrationPanel();
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    panel.ErrorMessage = ex.Message;
                    panel.Phase = RegistrationPhase.Error;
                }
            };

            // "취소"/"닫기" 이벤트 구독
            panel.CancelRequested += (s, e) => HideMapRegistrationPanel();
            panel.CloseRequested += (s, e) => HideMapRegistrationPanel();

            // 패널 표시
            MapRegistrationPanel = panel;
            IsMapRegistrationPanelVisible = true;
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 등록 패널 생성 실패: {ex.Message}");
        }
    }

    private void HideMapRegistrationPanel()
    {
        IsMapRegistrationPanelVisible = false;
        MapRegistrationPanel = null;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

    /// <summary>
    /// 앱 시작 시 기존 CustomMap → MapLayers Seed + 오버레이 자동 복원
    ///
    /// ■ 호출 시점:
    ///   OnActivateAsync → [2.1] LoadCustomMapsAsync 완료 후 → [4.5] 이 메서드 호출
    ///
    /// ■ 전제 조건:
    ///   - _customMapService.LoadCustomMapsAsync() 완료 → CustomMapProvider에 CustomMap 로드됨
    ///   - MainMap != null (OnViewAttached에서 설정됨)
    ///   - _customMapOverlayService.Initialize(Canvas) 완료 (OnViewAttached에서 설정됨)
    ///
    /// ■ 역할 2가지:
    ///   [Seed]    CustomMaps 테이블에는 있지만 MapLayers에 OverlayMap 레코드가 없는 경우 자동 INSERT
    ///   [Restore] MapLayers(LayerType='OverlayMap') → CustomMap 매칭 → ActivateOverlay 호출
    ///
    /// ■ ActivateOverlay 내부 동작:
    ///   1. CustomMapService.ActivateCustomMap → FileBasedCustomMapProvider 생성 (타일 폴더 연결)
    ///   2. Canvas 생성 → _overlayCanvas에 추가
    ///   3. RefreshVisibleTilesForState → 현재 뷰포트에 해당하는 타일 로드 + Canvas에 Image 배치
    ///      ※ 이 시점에 MainMap.ViewArea가 아직 (0,0)이면 타일 렌더링은 지연됨
    ///         → 맵 로드 완료 후 OnMapZoomChanged/OnPositionChanged에서 RefreshVisibleTiles 재호출
    ///
    /// ■ 주의:
    ///   - 이 메서드는 OnActivateAsync에서 GMapControl_Loaded 이전에 실행될 수 있음
    ///   - 따라서 ViewArea가 유효하지 않을 수 있고, 첫 타일 렌더링은 지연될 수 있음
    ///   - OnActivateAsync에서 이 메서드 이후 Dispatcher.BeginInvoke로 초기 렌더링 예약
    /// </summary>
    /// <summary>
    /// Images DB → MapLayers Seed (OverlayImage 레코드 자동 생성)
    /// Images 테이블에 있지만 MapLayers에 OverlayImage 레코드가 없으면 INSERT
    /// </summary>
    private async Task SeedOverlayImageLayersAsync()
    {
        try
        {
            var images = await _gMapDbSymbolService.FetchImagesAsync();
            if (images == null || !images.Any()) return;

            var layers = await _gMapDbService.FetchMapLayersAsync();
            var existingFilePaths = layers?
                .Where(l => l.LayerType == "OverlayImage")
                .Select(l => l.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var image in images)
            {
                if (string.IsNullOrEmpty(image.FilePath)) continue;
                if (existingFilePaths.Contains(image.FilePath)) continue;

                var imgSeedZOrder = await _gMapDbService.GetNextZOrderAsync("OverlayImage");
                await _gMapDbService.InsertMapLayerAsync(new MapLayerModel
                {
                    Name = image.Title ?? System.IO.Path.GetFileName(image.FilePath),
                    LayerType = "OverlayImage",
                    Category = "OverlayImage",
                    IsVisible = image.Visibility,
                    Opacity = image.Opacity,
                    ZOrder = imgSeedZOrder,
                    FilePath = image.FilePath,
                });
                _log?.Info($"[OverlayImage] MapLayers Seed: {image.Title} ({image.FilePath})");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[OverlayImage] Seed 실패: {ex.Message}");
        }
    }

    private async Task SeedAndRestoreOverlayMapsAsync()
    {
        try
        {
            if (MainMap == null) return;

            // ──────────────────────────────────────────────────────
            // [데이터 수집] CustomMapService에서 메모리에 로드된 CustomMap 목록
            // LoadCustomMapsAsync()가 OnActivateAsync 초반에 이미 실행됨
            // 이 목록은 DB CustomMaps 테이블 기반 (타일 폴더 유효한 것만)
            // ──────────────────────────────────────────────────────
            var customMaps = _customMapService.LoadedCustomMaps.ToList();

            // ──────────────────────────────────────────────────────
            // [데이터 수집] MapLayers 테이블 전체 조회 (1회)
            // LayerType: 'Symbol', 'OverlayMap', 'OverlayImage' 등
            // ──────────────────────────────────────────────────────
            var layers = await _gMapDbService.FetchMapLayersAsync();

            _log?.Info($"[Overlay] Seed+Restore 시작 — CustomMaps {customMaps.Count}건, MapLayers {layers?.Count ?? 0}건");

            // ═══════════════════════════════════════════════════════
            // [Phase 1: Seed] CustomMap이 있는데 MapLayers에 없으면 INSERT
            //
            // 왜 필요한가?
            //   이전 버전에서 등록된 CustomMap은 Maps/CustomMaps 테이블에만 있고
            //   MapLayers 테이블에 OverlayMap 레코드가 없음 (이번 세션에서 추가된 기능)
            //   → 마이그레이션: 기존 CustomMap에 대한 MapLayers 레코드 자동 생성
            // ═══════════════════════════════════════════════════════
            var existingMapIds = layers?
                .Where(l => l.LayerType == "OverlayMap" && l.MapId.HasValue)
                .Select(l => l.MapId!.Value)
                .ToHashSet() ?? new HashSet<int>();

            bool seeded = false;
            foreach (var customMap in customMaps)
            {
                // 이미 MapLayers에 해당 CustomMap의 레코드가 있으면 스킵
                if (existingMapIds.Contains(customMap.Id)) continue;

                // MapLayers에 OverlayMap 레코드 INSERT
                var mapSeedZOrder = await _gMapDbService.GetNextZOrderAsync("OverlayMap");
                await _gMapDbService.InsertMapLayerAsync(new MapLayerModel
                {
                    Name = customMap.Name ?? $"오버레이 맵 {customMap.Id}",
                    LayerType = "OverlayMap",
                    Category = "OverlayMap",
                    IsVisible = true,
                    Opacity = 1.0,
                    ZOrder = mapSeedZOrder,
                    MapId = customMap.Id,
                });
                seeded = true;
                _log?.Info($"[Overlay] Seed: {customMap.Name} (MapId={customMap.Id})");
            }

            // Seed로 새 레코드가 추가됐으면 다시 조회 (INSERT된 레이어 포함)
            if (seeded)
                layers = await _gMapDbService.FetchMapLayersAsync();

            // ═══════════════════════════════════════════════════════
            // [Phase 2: Restore] MapLayers에서 OverlayMap 레이어 → 오버레이 활성화
            //
            // 각 OverlayMap 레이어에 대해:
            //   1. MapId로 CustomMap 매칭
            //   2. ActivateOverlay → FileBasedCustomMapProvider 생성 + Canvas 등록
            //   3. DB에 저장된 IsVisible/Opacity 적용
            // ═══════════════════════════════════════════════════════
            var overlayLayers = layers?.Where(l => l.LayerType == "OverlayMap").ToList()
                ?? new List<IMapLayerModel>();

            _log?.Info($"[Overlay] 복원 대상: {overlayLayers.Count}건");

            foreach (var layer in overlayLayers)
            {
                _log?.Info($"[Overlay] 매칭: layer.MapId={layer.MapId}, layer.Name={layer.Name}");

                // MapLayers.MapId로 CustomMap 찾기
                var customMap = customMaps.FirstOrDefault(m => m.Id == layer.MapId);

                if (customMap == null)
                {
                    // CustomMap이 삭제됐거나 타일 폴더가 없어서 LoadCustomMapsAsync에서 제외된 경우
                    _log?.Warning($"[Overlay] 매칭 실패 — CustomMap Id={layer.MapId} 없음");
                    continue;
                }

                // ──────────────────────────────────────────────────
                // ActivateOverlay 내부:
                //   1. CustomMapService.ActivateCustomMap(customMap)
                //      → FileBasedCustomMapProvider 생성 (타일 폴더: D:/Tiles/xxx)
                //      → provider.GetTileImage(GPoint, zoom)으로 PNG 로드 가능
                //   2. Canvas 생성 (이 CustomMap 전용, Opacity/Visibility 개별 제어)
                //   3. _overlayCanvas.Children.Add(canvas)
                //   4. RefreshVisibleTilesForState(state, mainMap)
                //      → ViewArea 유효 시: 교차 영역 타일 계산 → LoadTileImage → Canvas에 Image 배치
                //      → ViewArea 미유효 시 (맵 미로드): 스킵 → 이후 이벤트에서 렌더링
                // ──────────────────────────────────────────────────
                _customMapOverlayService.ActivateOverlay(customMap, MainMap);

                // DB에 저장된 Visibility/Opacity 적용
                _customMapOverlayService.SetVisibility(customMap.Id, layer.IsVisible);
                _customMapOverlayService.SetOpacity(customMap.Id, layer.Opacity);

                _log?.Info($"[Overlay] 복원 완료: {layer.Name}, Visible={layer.IsVisible}, Opacity={layer.Opacity}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[Overlay] Seed+Restore 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 애플리케이션 종료 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteExitApplication(object arg) => true;

    /// <summary>
    /// 애플리케이션 종료 실행
    /// </summary>
    private void ExecuteExitApplication(object obj)
    {
        // TODO: 애플리케이션 종료 로직 구현 필요
    }
    #endregion

    #region - 지도 표시 명령어 구현 -
    /// <summary>
    /// WGS84 좌표계 토글 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteToggleWGS84Command(object arg) => true;

    /// <summary>
    /// WGS84 좌표계 표시 토글 실행
    /// </summary>
    private void ExecuteToggleWGS84Command(object obj)
    {
        IsShowWSG84 = !IsShowWSG84; // 개선: 직접 토글로 변경
    }

    /// <summary>
    /// MGRS 좌표계 토글 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteToggleMGRSCommand(object arg) => true;

    /// <summary>
    /// MGRS 좌표계 표시 토글 실행
    /// </summary>
    private void ExecuteToggleMGRSCommand(object obj)
    {
        IsShowMGRSGrid = IsShowMGRS;
    }

    /// <summary>
    /// UTM 좌표계 토글 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteToggleUTMCommand(object arg) => true;

    /// <summary>
    /// UTM 좌표계 표시 토글 실행
    /// </summary>
    private void ExecuteToggleUTMCommand(object obj)
    {
    }
    #endregion

    #region - 네비게이션 명령어 구현 -
    /// <summary>
    /// 홈 위치로 이동 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteMoveHomeLocation(object arg) => HomePosition?.IsAvailable == true;

    /// <summary>
    /// 홈 위치로 이동 실행
    /// </summary>
    private void ExecuteMoveHomeLocation(object obj)
    {
        GoToHomePosition();
    }

    /// <summary>
    /// 홈 위치 설정 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteSetHomeLocation(object arg) => HomePosition != null;   // FR-H2: 과녁 설정은 항상 가능(최초 홈 설정 허용)

    /// <summary>
    /// 홈 위치 설정 실행 — 과녁(크로스헤어) 클릭으로 홈 지정 (FR-H2, 기존 "마지막 클릭 위치" 방식 대체)
    /// </summary>
    private void ExecuteSetHomeLocation(object obj)
    {
        EnterHomePlacementMode();
    }
    #endregion

    #region - 편집 명령어 구현 -

    /// <summary>
    /// 선택 해제 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteClearSelection(object arg) => HasSelectedItem;

    /// <summary>
    /// 선택 해제 실행
    /// </summary>
    private void ExecuteClearSelection(object obj)
    {
        ClearAllSelections();
    }

    /// <summary>
    /// 선택된 항목 삭제 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteDeleteSelected(object arg) => HasSelectedItem && IsEditModeEnabled;

    /// <summary>
    /// 선택된 항목 삭제 실행
    /// </summary>
    // 단일 삭제 확인 대기 대상(확인 팝업 왕복 중 SelectedMarker 변경 대비 캡처).
    private IEditableMarker? _pendingDeleteMarker;
    private GMapCustomImage? _pendingDeleteImage;

    private async void ExecuteDeleteSelected(object obj)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 선택 삭제 차단"); ShowNoMapEditPermissionInfo(); return; }
        // 멀티셀렉트(그룹) 상태면 그룹 삭제로 위임 — 상단 메뉴 삭제 버튼도 다중삭제·확인창 적용(A).
        if (_groupSelection?.HasSelection ?? false) { await ExecuteGroupDelete(); return; }
        if (SelectedImage == null && SelectedMarker == null) return;

        // ★ 확인 없이 즉시 삭제하던 경로 차단 — Delete 키 오입력 한 번에 오버레이 이미지(PNG 파일+DB)가
        //   영구 삭제(Undo 불가)되는 데이터 손실 방지. 그룹 삭제와 동일한 표준 확인 팝업 패턴.
        _pendingDeleteImage = SelectedImage;
        _pendingDeleteMarker = SelectedMarker;
        bool irreversibleImage = SelectedImage != null || SelectedMarker is GMapSymbols.GMapImageMarker;
        string name = SelectedMarker?.Title ?? SelectedImage?.Title ?? "선택 항목";
        string explain = irreversibleImage
            ? $"'{name}' 오버레이 이미지를 삭제하시겠습니까?\n※ 이미지 파일이 영구 삭제되며 되돌리기(Undo)가 불가합니다."
            : $"'{name}'을(를) 삭제하시겠습니까?";
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "삭제 확인",
            Explain = explain,
            MessageModel = new CallDeleteSelectedProcessMessageModel()
        });
    }

    /// <summary>단일 삭제 확인 콜백 — 확인 팝업에서 "확인" 시 실제 삭제 수행. 이미지=PNG+DB 영구삭제(Undo 불가),
    /// 심볼=스냅샷 기록(Undo 가능). raw MessageBox 금지 — 그룹 삭제와 동일 EventAggregator 패턴.</summary>
    public async Task HandleAsync(CallDeleteSelectedProcessMessageModel message, CancellationToken cancellationToken)
    {
        var image = _pendingDeleteImage; var marker = _pendingDeleteMarker;
        _pendingDeleteImage = null; _pendingDeleteMarker = null;
        try
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
            if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 선택 삭제 차단(확인 후)"); return; }
            if (image != null)
            {
                var deletedFilePath = image.FilePath;
                MainMap.RemoveImageOverlay(image);
                if (SelectedImage == image) SelectedImage = null;
                _log?.Info("선택된 이미지 삭제 완료");

                // MapLayers에서 OverlayImage 레코드 삭제 (레이어 패널 연동)
                if (!string.IsNullOrEmpty(deletedFilePath))
                {
                    try
                    {
                        var layers = await _gMapDbService.FetchMapLayersAsync();
                        var layer = layers?.FirstOrDefault(l =>
                            l.LayerType == "OverlayImage" &&
                            string.Equals(l.FilePath, deletedFilePath, StringComparison.OrdinalIgnoreCase));
                        if (layer != null)
                        {
                            await _gMapDbService.DeleteMapLayerAsync(layer.Id);
                            await LoadLayersFromDbAsync();
                            _log?.Info($"[OverlayImage] MapLayers 삭제: {layer.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _log?.Error($"[OverlayImage] MapLayers 삭제 실패: {ex.Message}");
                    }
                }
            }

            if (marker != null)
            {
                var markerTitle = marker.Title ?? "Unknown";
                var markerId = marker.Id;
                var delSnapshot = _editRecorder?.CaptureForDelete(marker);   // 삭제 전 스냅샷(Undo용, 이미지는 null=Undo 불가)

                _log?.Info($"마커 삭제 시작: {markerTitle} (ID: {markerId})");

                try
                {
                    // 1. Adorner 먼저 제거
                    MainMap?.DeselectMarker(marker);

                    // 2. GMap.NET 마커 컬렉션에서 제거
                    if (marker is GMapMarker gMapMarker)
                    {
                        MainMap?.Markers?.Remove(gMapMarker);
                    }

                    // 4. DB에서 삭제
                    var dbResult = await DbDeleteProcess(marker);
                    if (dbResult)
                        _log?.Info($"마커({markerId}) DB 삭제 성공");
                    else
                        _log?.Warning($"마커({markerId}) DB 삭제 실패");

                    // 5. PropertyPanel 정리
                    HidePropertyPanel();

                    // 6. 마커 리소스 정리
                    try
                    {
                        marker.Dispose();
                    }
                    catch (Exception disposeEx)
                    {
                        _log?.Warning($"마커 Dispose 실패: {disposeEx.Message}");
                    }

                    // 7. SelectedMarker null로 설정(현재 선택이 삭제 대상일 때만)
                    if (SelectedMarker == marker) SelectedMarker = null;

                    _log?.Info($"마커 '{markerTitle}' 삭제 완료");
                    _editRecorder?.RecordDelete(delSnapshot);   // Undo 기록(삭제, 이미지는 no-op)

                }
                catch (Exception markerEx)
                {
                    _log?.Error($"마커 삭제 중 오류: {markerEx.Message}");
                    if (SelectedMarker == marker) SelectedMarker = null;
                }

                // 레이어 패널 트리 동기화 — 삭제된 심볼을 _symbolProvider 캐시에서 직접 제거 후 리빌드.
                // ★ v2.6엔 DeleteXxxAsync의 provider.Remove(7394b7b)가 미머지 → 여기서 제거 안 하면 리빌드가
                //   스테일 스냅샷을 다시 읽어 노드가 부활함(감사 P0). Id로 매칭 제거.
                var deletedSym = _symbolProvider.FirstOrDefault(s => s.Id == markerId);
                if (deletedSym != null) _symbolProvider.Remove(deletedSym);
                await LoadLayersFromDbAsync();
            }
            // 8. 화면 갱신
            MainMap?.InvalidateVisual();

            _log?.Info("선택 항목 삭제 처리 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"선택 항목 삭제 실패: {ex.Message}");
        }
        finally
        {
            // 확인/진행 팝업 닫기 — 그룹 삭제와 동일(콜백 발행 후 자동으로 닫히지 않으므로 명시 Close 필수).
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    /// <summary>
    /// 편집 모드 토글 명령어
    /// </summary>
    // 편집모드 진입은 맵 편집 권한 필요(RBAC). 이미 진입 중이면 해제는 항상 허용. → 버튼 자동 비활성(WPF Command).
    // 이 게이트가 어도너 드래그/이동/회전/크기·선택·속성영속 경로를 한 번에 차단(심층방어 1차).
    private bool CanExecuteToggleEditMode(object arg) => IsEditModeEnabled || CanEditMap();
    private async void ExecuteToggleEditMode(object obj)
    {
        IsEditModeEnabled = !IsEditModeEnabled;
        if (!IsEditModeEnabled)
        {
            IsSnapToGridEnabled = false;
            await InitializeDeviceSymbolIntegration();
        }
    }

    /// <summary>
    /// 다중 선택 모드 토글 명령어
    /// </summary>
    private bool CanExecuteToggleMultiSelect(object arg) => IsEditModeEnabled;
    private void ExecuteToggleMultiSelect(object obj)
    {
        try
        {
            IsMultiSelectEnabled = !IsMultiSelectEnabled;
            MainMap?.SetMultiSelectMode(IsMultiSelectEnabled);
            _log?.Info($"다중 선택 모드: {(IsMultiSelectEnabled ? "활성화" : "비활성화")}");
        }
        catch (Exception ex)
        {
            _log?.Error($"다중 선택 모드 토글 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 모든 편집 취소 명령어
    /// </summary>
    private bool CanExecuteCancelAllEditing(object arg) => IsEditModeEnabled && AdornerCount > 0;
    private void ExecuteCancelAllEditing(object obj)
    {
        try
        {
            MainMap?.AdornerManager?.CancelAllEditing(MainMap);
            _log?.Info("모든 편집 취소 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"모든 편집 취소 실패: {ex.Message}");
        }
    }

   
    #endregion

    #region - 회전 명령어 구현 -
    /// <summary>
    /// 회전 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteRotate(object arg) => true;

    /// <summary>
    /// 회전 실행 - 절대각도로 회전
    /// </summary>
    private void ExecuteRotate(object obj)
    {
        try
        {
            if (obj is string angleStr && double.TryParse(angleStr, out double angle))
            {
                MainMap.SetMapRotation(angle);
                _log?.Info($"지도 회전: {angle}도");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 미세 회전 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteFineRotate(object arg) => true;

    /// <summary>
    /// 미세 회전 실행 - 상대각도로 회전
    /// </summary>
    private void ExecuteFineRotate(object obj)
    {
        try
        {
            if (obj is string deltaStr && double.TryParse(deltaStr, out double delta))
            {
                MainMap.RotateMap(delta);
                _log?.Info($"지도 미세 회전: {delta:+0.0;-0.0}도");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 미세 회전 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 회전 초기화 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteResetRotation(object arg) => true;

    /// <summary>
    /// 회전 초기화 실행
    /// </summary>
    private void ExecuteResetRotation(object obj)
    {
        try
        {
            MainMap.ResetRotation();
            _log?.Info("지도 회전 초기화");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 회전 초기화 실패: {ex.Message}");
        }
    }
    #endregion

    #region - 마커 편집 명령어 구현 -
    /// <summary>
    /// 선택된 심볼 추가 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteAddSelectedSymbol(object arg) => CanAddSymbol;

    /// <summary>
    /// 선택된 심볼 추가 실행
    /// </summary>
    private async void ExecuteAddSelectedSymbol(object obj)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 심볼 추가 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            var position = ClickedCurrentPosition.IsEmpty ? MainMap!.CenterPosition : ClickedCurrentPosition;
            var symbolTitle = GetSymbolTitle();

            switch (SelectedMarkerCategory)
            {
                // 점 심볼(#4) — 즉시 추가 대신 배치 모드 진입(클릭으로 위치 지정).
                case EnumMarkerCategory.BASIC_SHAPES:
                    if (SelectedSymbolType is string basicType)
                        EnterSymbolPlacementMode(SelectedMarkerCategory, basicType, symbolTitle);
                    break;

                case EnumMarkerCategory.GEOMETRICS:
                    if (SelectedSymbolType is EnumShapeType shapeType)
                        EnterSymbolPlacementMode(SelectedMarkerCategory, shapeType, symbolTitle);
                    break;

                case EnumMarkerCategory.VEHICLES:
                    //await AddVehicleMarker(position, SelectedSymbolType.ToString(), symbolTitle);
                    break;

                case EnumMarkerCategory.MILITARY_SYMBOLS:
                    ShowMilitarySymbolRegisterPanel();   // 군대부호 = 등록 패널(배치모드 아님)
                    break;

                case EnumMarkerCategory.PIDS_EQUIPMENT:
                    if (System.Enum.TryParse<EnumDeviceType>(SelectedSymbolType.ToString(), out var deviceType))
                        EnterSymbolPlacementMode(SelectedMarkerCategory, deviceType, symbolTitle);
                    break;

                case EnumMarkerCategory.AREA_BOUNDARY:
                    if (SelectedSymbolType is string areaType)
                        await AddAreaBoundaryMarker(position, areaType, symbolTitle);   // 라인/영역 = 기존 드로잉 유지
                    break;

                case EnumMarkerCategory.INFRASTRUCTURE:
                    if (SelectedSymbolType is string infraType)
                        EnterSymbolPlacementMode(SelectedMarkerCategory, infraType, symbolTitle);
                    break;

             
            }

            _log?.Info($"심볼 추가 완료: {SelectedMarkerCategory} - {SelectedSymbolType}");
        }
        catch (Exception ex)
        {
            _log?.Error($"심볼 추가 실패: {ex.Message}");
        }
    }

    


    /// <summary>
    /// 심볼 제목 생성
    /// </summary>
    private string GetSymbolTitle()
    {
        var categoryName = SymbolTypeHelper.CategoryDisplayNames.GetValueOrDefault(SelectedMarkerCategory, SelectedMarkerCategory.ToString());

        var typeName = SelectedSymbolType switch
        {
            EnumShapeType shapeType => SymbolTypeHelper.ShapeTypeDisplayNames.GetValueOrDefault(shapeType, shapeType.ToString()),
            string stringType => SymbolTypeHelper.SymbolTypeDisplayNames.GetValueOrDefault(stringType, stringType),
            _ => SelectedSymbolType?.ToString() ?? "Unknown"
        };

        return $"{typeName}";
    }

    // 각 카테고리별 마커 추가 메서드들
    private async Task AddBasicShapeMarker(PointLatLng position, string shapeType, string title)
    {
        // 기본 마커 생성 (기존 AddCustomMarker 사용)
        await AddCustomMarker(position, title);
    }

    /// <summary>
    /// 마커 복제 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteDuplicateMarker(object arg) => SelectedMarker != null;

    /// <summary>
    /// 마커 복제 실행
    /// </summary>
    private void ExecuteDuplicateMarker(object obj)
    {
        try
        {
            DuplicateSelectedMarker();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 복제 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 격자 스냅 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteSnapMarkerToGrid(object arg) => SelectedMarker != null && IsEditModeEnabled;

    /// <summary>
    /// 마커 격자 스냅 실행
    /// </summary>
    private void ExecuteSnapMarkerToGrid(object obj)
    {
        try
        {
            if (SelectedMarker != null)
            {
                SnapMarkerToGrid(SelectedMarker);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 스냅 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 회전 초기화 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteResetMarkerRotation(object arg) => SelectedMarker != null && IsEditModeEnabled;

    /// <summary>
    /// 마커 회전 초기화 실행
    /// </summary>
    private void ExecuteResetMarkerRotation(object obj)
    {
        try
        {
            if (SelectedMarker != null)
            {
                UpdateMarkerRotation(SelectedMarker, 0);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 회전 초기화 실행 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 크기 초기화 명령어 실행 가능 여부
    /// </summary>
    private bool CanExecuteResetMarkerSize(object arg) => SelectedMarker != null && IsEditModeEnabled;

    /// <summary>
    /// 마커 크기 초기화 실행
    /// </summary>
    private void ExecuteResetMarkerSize(object obj)
    {
        try
        {
            if (SelectedMarker != null)
            {
                UpdateMarkerSize(SelectedMarker, 32, 32); // 기본 크기로 초기화
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 크기 초기화 실행 실패: {ex.Message}");
        }
    }


    
    #endregion

    #region - 지도 구성 및 설정 -
    /// <summary>
    /// 비동기 지도 설정 - 선택된 맵 타입에 따라 구성
    /// </summary>
    private async Task MapConfigureAsync(bool isInitialLoad = false)
    {
        try
        {
            // MBTiles 기본 맵 등록 (최초 1회 — Datas/ 폴더 스캔)
            await SeedMBTilesMapsAsync();

            if (_mapProvider.Any())
            {
                var mapName = _setupModel.MapName;
                if (!string.IsNullOrEmpty(mapName))
                    SelectedMap = _mapProvider.Where(entity => entity.Name == mapName).FirstOrDefault();

                // 설정된 맵이 없으면 MBTiles 맵 중 첫 번째 선택 (폴백)
                if (SelectedMap == null)
                    SelectedMap = _mapProvider.OfType<DefinedMapModel>()
                        .FirstOrDefault(m => m.Vendor == EnumMapVendor.MBTiles);

                // 그래도 없으면 아무 맵이나
                if (SelectedMap == null)
                    SelectedMap = _mapProvider.FirstOrDefault();
            }

            if (SelectedMap == null) return;

            if (SelectedMap is DefinedMapModel definedMap)
            {
                await ConfigureDefinedMapAsync(definedMap, isInitialLoad);
            }
            else if (SelectedMap is CustomMapModel customMap)
            {
                await ConfigureCustomMapAsync(customMap);
            }

            // 공통 지도 설정 (초기 로드 시 HomePosition으로 이동)
            ConfigureCommonMapSettings(isInitialLoad);

            // 콤보박스 바인딩 갱신
            NotifyOfPropertyChange(nameof(SelectedMapItem));

            _log?.Info($"지도 설정 완료: {SelectedMap.Name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 설정 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 기존 제공자 지도 설정 (Google, Bing, OpenStreetMap 등)
    /// </summary>
    private async Task ConfigureDefinedMapAsync(DefinedMapModel definedMap, bool isInitialLoad = false)
    {
        try
        {
            switch (definedMap.Vendor)
            {
                case EnumMapVendor.MBTiles:
                    if (isInitialLoad)
                        InitializeMBTilesMap(definedMap);
                    else
                        SwitchMBTilesMap(definedMap);
                    return; // 온라인 모드 설정 불필요

                case EnumMapVendor.Google:
                    GoogleMapProvider.Instance.ApiKey = definedMap.ApiKey;
                    ConfigureGoogleMap(definedMap.Style);
                    break;
                case EnumMapVendor.Microsoft:
                    BingMapProvider.Instance.ClientKey = definedMap.ApiKey;
                    ConfigureBingMap(definedMap.Style);
                    break;
                case EnumMapVendor.OpenStreetMap:
                    OpenStreetMapProvider.Instance.YoursClientName = definedMap.ApiKey;
                    MainMap.MapProvider = GMapProviders.OpenStreetMap;
                    break;
                default:
                    MainMap.MapProvider = GMapProviders.OpenStreetMap;
                    break;
            }

            // 온라인 지도 모드 설정
            if (MainMap.Manager.Mode == AccessMode.CacheOnly)
                await GetMapDataAsync();
            else
                MainMap.Manager.Mode = AccessMode.ServerAndCache;

            _log?.Info($"기존 제공자 지도 설정 완료: {definedMap.Vendor} {definedMap.Style}");
        }
        catch (Exception ex)
        {
            _log?.Error($"기존 제공자 지도 설정 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 커스텀 지도 설정 (사용자 정의 타일 맵)
    /// </summary>
    private Task ConfigureCustomMapAsync(CustomMapModel customMap)
    {
        try
        {
            if (MainMap == null) return Task.CompletedTask;

            _log?.Info($"커스텀 지도 오버레이 설정: {customMap.Name}");

            // 오버레이로 활성화 (베이스맵 유지, MainMap.MapProvider 변경 안 함)
            _customMapOverlayService.ActivateOverlay(customMap, MainMap);

            _log?.Info($"커스텀 지도 오버레이 완료: {customMap.Name}, 타일 수: {customMap.TotalTileCount}");
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 지도 오버레이 실패: {customMap.Name}, 오류: {ex.Message}");
            throw;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Datas/ 폴더의 .mbtiles 파일을 스캔하여 DB에 DefinedMap으로 자동 등록.
    /// MBTiles 메타데이터(bounds, zoom)를 읽어 정확한 값으로 Insert.
    /// </summary>
    private async Task SeedMBTilesMapsAsync()
    {
        try
        {
            // Datas/ 폴더 스캔
            var datasPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datas");
            if (!System.IO.Directory.Exists(datasPath))
            {
                _log?.Info($"Datas 폴더 없음: {datasPath}");
                return;
            }

            var mbtilesFiles = System.IO.Directory.GetFiles(datasPath, "*.mbtiles");
            if (mbtilesFiles.Length == 0)
            {
                _log?.Info("Datas 폴더에 .mbtiles 파일 없음");
                return;
            }

            // DB에서 기존 MBTiles 맵 목록 조회
            var existing = await _gMapDbService.FetchDefinedMapsAsync();
            var existingMBTiles = existing?
                .Where(m => m.Vendor == EnumMapVendor.MBTiles)
                .ToList() ?? new List<IDefinedMapModel>();

            var folderFileNames = mbtilesFiles
                .Select(f => System.IO.Path.GetFileName(f))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. 폴더에 파일이 없는 DB 엔트리 삭제 (고아 정리) — 엔트리별 격리(한 건 실패가 전체 중단 금지)
            foreach (var dbMap in existingMBTiles)
            {
                try
                {
                    if (!folderFileNames.Contains(dbMap.ServiceUrl))
                    {
                        await _gMapDbService.DeleteDefinedMapAsync(new DefinedMapModel { Id = dbMap.Id });
                        var toRemove = _mapProvider.FirstOrDefault(m => m.Name == dbMap.Name);
                        if (toRemove != null) _mapProvider.Remove(toRemove);
                        var toRemoveDef = _definedMapProvider.FirstOrDefault(m => m.Name == dbMap.Name);
                        if (toRemoveDef != null) _definedMapProvider.Remove(toRemoveDef);
                        _log?.Info($"MBTiles DB 엔트리 삭제 (파일 없음): {dbMap.ServiceUrl}");
                    }
                }
                catch (Exception exOrphan)
                {
                    _log?.Error($"MBTiles 고아정리 실패(이 엔트리만 스킵): {dbMap?.ServiceUrl} — {exOrphan.Message}");
                }
            }

            var provider = MBTilesMapProvider.Instance;

            foreach (var filePath in mbtilesFiles)
            {
                var fileName = System.IO.Path.GetFileName(filePath);

                // 파일별 격리 — 한 파일에서 예외(NRE 등)가 나도 나머지 기본지도(위성/일반) 시드는 계속.
                // RC: DB 초기화 후 위성 파일 처리 중 NRE로 루프 전체가 중단 → 위성지도 유실 회귀(2026-07-03). 재발 차단.
                try
                {
                var fileInfo = new System.IO.FileInfo(filePath);

                // 2. 이미 DB에 있는 파일 → 변경 감지
                var dbEntry = existingMBTiles.FirstOrDefault(
                    m => string.Equals(m.ServiceUrl, fileName, StringComparison.OrdinalIgnoreCase));

                if (dbEntry != null)
                {
                    // 파일 수정일이 DB보다 새로우면 메타데이터 업데이트
                    if (fileInfo.LastWriteTime > dbEntry.UpdatedAt)
                    {
                        if (provider.Open(filePath) && provider.Bounds != null && provider.Bounds.Length == 2)
                        {
                            await _gMapDbService.UpdateDefinedMapMetadataAsync(
                                dbEntry.Id,
                                provider.Bounds[1].Lat, provider.Bounds[0].Lat,
                                provider.Bounds[0].Lng, provider.Bounds[1].Lng,
                                provider.MinZoom, provider.MaxZoom ?? 18);
                            _log?.Info($"MBTiles 메타데이터 갱신: {fileName}");
                        }
                    }
                    continue; // 이미 등록된 파일은 스킵
                }

                // 3. 새 파일 → DB에 등록
                if (!provider.Open(filePath))
                {
                    _log?.Error($"MBTiles 열기 실패: {fileName}");
                    continue;
                }

                // 파일명으로 Style 결정
                var isSatellite = fileName.Contains("satellite", StringComparison.OrdinalIgnoreCase);
                var style = isSatellite ? EnumMapStyle.Satellite : EnumMapStyle.Normal;
                var category = isSatellite ? EnumMapCategory.Satellite : EnumMapCategory.Standard;
                var displayName = isSatellite ? "위성지도" : "일반지도";

                var model = new DefinedMapModel
                {
                    Name = displayName,
                    Description = $"MBTiles 오프라인 지도 ({fileName})",
                    Category = category,
                    DataType = EnumMapData.Raster,
                    CoordinateSystem = "WGS84",
                    EpsgCode = "EPSG:3857",
                    MinZoomLevel = provider.MinZoom >= 0 ? provider.MinZoom : 10,
                    MaxZoomLevel = provider.MaxZoom ?? 18,
                    TileSize = 256,
                    Status = EnumMapStatus.Active,
                    CreatedBy = "System",
                    GMapProviderName = "MBTilesMapProvider",
                    ProviderGuid = provider.Id.ToString(),
                    Vendor = EnumMapVendor.MBTiles,
                    Style = style,
                    RequiresApiKey = false,
                    ServiceUrl = fileName,
                };

                // Bounds 설정
                if (provider.Bounds != null && provider.Bounds.Length == 2)
                {
                    model.MinLatitude = provider.Bounds[1].Lat;
                    model.MaxLatitude = provider.Bounds[0].Lat;
                    model.MinLongitude = provider.Bounds[0].Lng;
                    model.MaxLongitude = provider.Bounds[1].Lng;
                }

                int id = await _gMapDbService.InsertDefinedMapAsync(model);
                model.Id = id;

                // Provider 목록에 즉시 추가
                _mapProvider.Add(model);
                _definedMapProvider.Add(model);

                _log?.Info($"MBTiles 맵 등록: {displayName} ({fileName}), Id={id}, " +
                           $"Zoom={model.MinZoomLevel}~{model.MaxZoomLevel}, " +
                           $"Bounds=[{model.MinLatitude:F4}~{model.MaxLatitude:F4}, {model.MinLongitude:F4}~{model.MaxLongitude:F4}]");
                }
                catch (Exception exFile)
                {
                    // 이 파일만 스킵 — 다른 기본지도(위성/일반)는 계속 시드해 항상 존재하도록 보장.
                    _log?.Error($"MBTiles 시드 실패(이 파일만 스킵): {fileName} — {exFile.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"MBTiles 맵 Seed 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 초기 로드 전용 — MBTiles Open + Provider 설정만
    /// ReloadMap 호출 안 함 (GMapControl_Loaded → OnMapOpen이 타일 자동 로드)
    /// Position/Zoom 설정 안 함 (ConfigureCommonMapSettings에서 HomePosition 적용)
    /// </summary>
    private void InitializeMBTilesMap(DefinedMapModel definedMap)
    {
        if (MainMap == null || string.IsNullOrEmpty(definedMap.ServiceUrl)) return;

        var mbtilesPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Datas", definedMap.ServiceUrl);

        if (!System.IO.File.Exists(mbtilesPath))
        {
            _log?.Error($"[MapInit] MBTiles 파일 없음: {mbtilesPath}");
            return;
        }

        // 1. MBTiles 열기
        var provider = MBTilesMapProvider.Instance;
        if (!provider.Open(mbtilesPath))
        {
            _log?.Error($"[MapInit] MBTiles 열기 실패: {mbtilesPath}");
            return;
        }

        // 빈 타일(커버리지 밖) 영역에 깔끔/모던 기본 타일 표시 — UI 스레드 1회 생성·캐시(멱등)
        provider.DefaultTileBytes = DefaultTileImageFactory.GetBytes();

        // 2. Provider 설정 (EmptyProvider 불필요 — 최초이므로 참조 변경 자연 발생)
        MainMap.MapProvider = provider;
        MainMap.Manager.Mode = AccessMode.ServerOnly;

        // 3. Zoom 범위만 설정 (Position/Zoom은 ConfigureCommonMapSettings에서 HomePosition 적용)
        if (provider.MinZoom >= 0) ZoomMin = provider.MinZoom;   // 래퍼 경유 → 슬라이더 Minimum 통지 (T1)
        if (provider.MaxZoom.HasValue) ZoomMax = provider.MaxZoom.Value;   // 래퍼 경유 → 슬라이더 Maximum 통지 (T1)

        // ReloadMap 호출하지 않음 — IsStarted=false 상태 (폼 미로드)
        // GMapControl_Loaded → OnMapOpen에서 타일 자동 로드됨

        _log?.Info($"[MapInit] 초기화 완료: {definedMap.Name} ({definedMap.ServiceUrl}), " +
                   $"Zoom={provider.MinZoom}~{provider.MaxZoom}");
    }

    /// <summary>
    /// 맵 전환 전용 — EmptyProvider → CacheClear → Open → Provider → ReloadMap → Position 복원
    /// IsStarted=true 보장 (폼 이미 로드됨)
    /// </summary>
    private void SwitchMBTilesMap(DefinedMapModel definedMap)
    {
        if (MainMap == null || string.IsNullOrEmpty(definedMap.ServiceUrl)) return;

        var mbtilesPath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Datas", definedMap.ServiceUrl);

        if (!System.IO.File.Exists(mbtilesPath))
        {
            _log?.Error($"[MapSwitch] MBTiles 파일 없음: {mbtilesPath}");
            return;
        }

        // 0. 현재 위치/줌 저장
        var savedPosition = MainMap.Position;
        var savedZoom = MainMap.Zoom;

        _log?.Info($"[MapSwitch] 전환 시작: {MainMap.MapProvider?.Name ?? "null"} → {definedMap.ServiceUrl}");

        // 1. EmptyProvider 전환 (싱글턴 참조 변경 강제)
        MainMap.MapProvider = GMapProviders.EmptyProvider;

        // 2. 메모리 타일 캐시 초기화
        GMap.NET.GMaps.Instance.MemoryCache.Clear();

        // 3. 새 MBTiles 열기 (Open 내부에서 이전 source Close)
        var provider = MBTilesMapProvider.Instance;
        if (!provider.Open(mbtilesPath))
        {
            _log?.Error($"[MapSwitch] MBTiles 열기 실패: {mbtilesPath}");
            return;
        }

        // 빈 타일(커버리지 밖) 영역에 깔끔/모던 기본 타일 표시 — 캐시라 멱등(이미 설정돼도 무해)
        provider.DefaultTileBytes = DefaultTileImageFactory.GetBytes();

        // 4. Provider 설정
        MainMap.MapProvider = provider;
        MainMap.Manager.Mode = AccessMode.ServerOnly;

        // 5. Zoom 범위 설정
        if (provider.MinZoom >= 0) ZoomMin = provider.MinZoom;   // 래퍼 경유 → 슬라이더 Minimum 통지 (T1)
        if (provider.MaxZoom.HasValue) ZoomMax = provider.MaxZoom.Value;   // 래퍼 경유 → 슬라이더 Maximum 통지 (T1)

        // 6. 위치/줌 복원
        MainMap.Position = savedPosition;
        MainMap.Zoom = savedZoom;

        // 7. 강제 리로드 (IsStarted=true 보장)
        MainMap.ReloadMap();

        _log?.Info($"[MapSwitch] 전환 완료: {definedMap.Name} ({definedMap.ServiceUrl}), " +
                   $"Zoom={provider.MinZoom}~{provider.MaxZoom}, Position={MainMap.Position}");
    }

    /// <summary>
    /// 공통 지도 설정 - 위치, 줌, 이벤트 핸들러 등
    /// </summary>
    private void ConfigureCommonMapSettings(bool isInitialLoad = false)
    {
        if (MainMap == null || SelectedMap == null) return;

        // MinZoom/MaxZoom는 항상 DB 값으로 설정 (래퍼 경유 → 슬라이더 통지, T1)
        ZoomMin = SelectedMap.MinZoomLevel;
        ZoomMax = SelectedMap.MaxZoomLevel;

        if (isInitialLoad)
        {
            // 초기 로드: HomePosition으로 이동
            MainMap.Position = _setupModel.HomePosition?.PointLatLng ?? new PointLatLng(37.648425, 126.904284);
            MainMap.Zoom = _setupModel.HomePosition?.Zoom ?? DEFAULT_ZOOM;
        }
        // 맵 전환: Position/Zoom은 SwitchMBTilesMap에서 이미 복원됨 → 건드리지 않음

        MainMap.ShowCenter = false;
        MainMap.MultiTouchEnabled = false;

        // 이벤트 핸들러 해제 (누적 방지) → 재구독
        MainMap.OnPositionChanged -= MainMap_OnCurrentPositionChanged;
        MainMap.MouseMove -= MainMap_MouseMove;
        MainMap.MouseLeftButtonDown -= MainMap_MouseLeftButtonDown;
        MainMap.OnMapZoomChanged -= MainMap_OnMapZoomChanged;
        MainMap.SizeChanged -= MainMap_SizeChanged;

        MainMap.OnPositionChanged += MainMap_OnCurrentPositionChanged;
        MainMap.MouseMove += MainMap_MouseMove;
        MainMap.MouseLeftButtonDown += MainMap_MouseLeftButtonDown;
        MainMap.OnMapZoomChanged += MainMap_OnMapZoomChanged;
        MainMap.SizeChanged += MainMap_SizeChanged;

        MainMap.ShowCenter = true;
        MainMap_OnMapZoomChanged();

        ApplyMapAnchor();   // [MapAnchor] 사이트 고정: BoundsOfMap/MinZoom 적용 (증분2 · 앵커 비활성 시 해제)

        SetInitialHomePosition();
    }

    /// <summary>
    /// Google 지도 스타일별 Provider 설정을 별도 메서드로 분리
    /// </summary>
    private void ConfigureGoogleMap(EnumMapStyle style)
    {
        MainMap.MapProvider = style switch
        {
            EnumMapStyle.Normal => GMapProviders.GoogleMap,
            EnumMapStyle.Satellite => GMapProviders.GoogleSatelliteMap,
            EnumMapStyle.Hybrid => GMapProviders.GoogleHybridMap,
            EnumMapStyle.Terrain => GMapProviders.GoogleTerrainMap,
            _ => GMapProviders.GoogleMap
        };
    }

    /// <summary>
    /// Bing  지도 스타일별 Provider 설정
    /// </summary>
    private void ConfigureBingMap(EnumMapStyle style)
    {
        MainMap.MapProvider = style switch
        {
            EnumMapStyle.Normal => GMapProviders.BingMap,
            EnumMapStyle.Satellite => GMapProviders.BingSatelliteMap,
            EnumMapStyle.Hybrid => GMapProviders.BingHybridMap,
            _ => GMapProviders.BingMap
        };
    }

    private async Task SymbolConfigureAsync()
    {
        try
        {
            _log?.Info("SymbolConfigureAsync를 이용하여 심볼 불러오고 초기화.");

            // 우선순위에 따라 정렬된 심볼 목록 생성
            var sortedSymbols = _symbolProvider
                .OrderBy(item => GetSymbolPriority(item))
                .ThenBy(item => item is PidsSymbolModel pids ? (int)pids.DeviceType : 0)
                .ThenBy(item => item is PidsSymbolModel pids ? pids.LinkedDeviceId : 0)
                .ToList();

            foreach (var item in sortedSymbols)
            {
                AddMarkerFromSymbol(item, isExistingMarker: true);
            }

            _log?.Info($"심볼 추가 완료 - 총 {sortedSymbols.Count}개");

            await InitializeDeviceSymbolIntegration();
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <summary>
    /// 심볼 추가 우선순위 결정
    /// </summary>
    private int GetSymbolPriority(ISymbolModel symbol)
    {
        return symbol switch
        {
            PidsSymbolModel pids when pids.DeviceType == EnumDeviceType.IpCamera => 3, // 가장 늦게
            PidsSymbolModel => 2, // 두 번째
            _ => 1 // 가장 먼저
        };
    }

    /// <summary>
    /// DB에서 이미지 오버레이 로드 및 지도에 표시 (Phase 28)
    /// </summary>
    /// <remarks>
    /// PRD: PRD_ImageOverlay_Feature.md - 4.5.2 MapViewModel 확장 설계
    /// - OnActivateAsync에서 호출
    /// - DB에서 Images 테이블 조회
    /// - MarkerFactory로 GMapImageMarker 생성
    /// - MainMap.Markers에 추가
    /// </remarks>
    private async Task ImageConfigureAsync()
    {
        try
        {
            _log?.Info("ImageConfigureAsync - DB에서 이미지 오버레이 로드 시작");

            // 1. DB에서 이미지 목록 조회
            var images = await _gMapDbSymbolService.FetchImagesAsync();

            if (images == null || images.Count == 0)
            {
                _log?.Info("ImageConfigureAsync - 로드할 이미지 없음");
                return;
            }

            // 2. 각 이미지를 지도에 추가
            int successCount = 0;
            foreach (var imageModel in images)
            {
                try
                {
                    AddImageMarkerFromModel(imageModel);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _log?.Error($"이미지 마커 추가 실패 (Id={imageModel.Id}): {ex.Message}");
                }
            }

            _log?.Info($"ImageConfigureAsync 완료 - {successCount}/{images.Count}개 이미지 로드됨");
        }
        catch (Exception ex)
        {
            _log?.Error($"ImageConfigureAsync 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// ImageModel에서 마커 생성 및 지도에 추가 (Phase 28)
    /// </summary>
    /// <param name="imageModel">이미지 모델</param>
    /// <remarks>
    /// PRD: PRD_ImageOverlay_Feature.md - 4.5.2 MapViewModel 확장 설계
    /// </remarks>
    private void AddImageMarkerFromModel(IImageModel imageModel)
    {
        try
        {
            _log?.Info($"이미지 마커 생성 시작: {imageModel.Title} (Id={imageModel.Id})");

            // 1. MarkerFactory로 마커 생성
            var marker = _markerFactory.CreateImageMarker(imageModel);

            // 2. 지도에 추가
            if (MainMap != null && marker != null)
            {
                MainMap.Markers.Add(marker);
                // EditMode 상태에 맞게 IsHitTestVisible 동기화
                if (marker is GMapMarker gm && gm.Shape is UIElement shapeEl)
                {
                    shapeEl.IsHitTestVisible = IsEditModeEnabled;
                    // Panel.ZIndex 초기 동기화 (RestoreLayerVisibility 전까지 ZIndex=0 방지)
                    System.Windows.Controls.Panel.SetZIndex(shapeEl, marker.ZIndex);
                }
                _log?.Info($"이미지 마커 추가 완료: {imageModel.Title}");
                if (marker is GMapSymbols.IEditableMarker iem && iem.Id > 0) _editRecorder?.RecordAdd(iem);   // Undo 기록(이미지 마커 추가)
            }
            else
            {
                _log?.Warning($"MainMap 또는 마커가 null입니다: MainMap={MainMap != null}, marker={marker != null}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 마커 추가 실패 (Id={imageModel.Id}): {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - 지도 전환 및 관리 -
    /// <summary>
    /// 지도 변경 (런타임에서) - 다른 맵으로 동적 전환
    /// </summary>
    public async Task SwitchToMapAsync(IMapModel targetMap)
    {
        try
        {
            if (targetMap == null || targetMap.Id == SelectedMap?.Id)
                return;

            _log?.Info($"지도 변경: {SelectedMap?.Name} -> {targetMap.Name}");

            // 이전 커스텀 맵 비활성화
            if (SelectedMap is CustomMapModel && CurrentCustomMapProvider != null)
            {
                _customMapService.DeactivateCustomMap(SelectedMap.Id);
                CurrentCustomMapProvider = null;
            }

            SelectedMap = targetMap;
            // 이름 기반 검색
            SelectedMap = _mapProvider.Where(entity => entity.Name == targetMap.Name)
                                    .Where(entity => entity.Id == targetMap.Id).FirstOrDefault() ?? throw new NullReferenceException("There is no map you choose.");

            _setupModel.MapName = SelectedMap.Name;
            _setupModel.MapType = SelectedMap.ProviderType.ToString();
            switch (SelectedMap.ProviderType)
            {
                case EnumMapProvider.Defined:
                    _setupModel.MapMode = "ServerAndCache";
                    break;
                case EnumMapProvider.Custom:
                    _setupModel.MapMode = "ServerAndCache";
                    break;
                default:
                    break;
            }

            await MapConfigureAsync();

            _log?.Info($"지도 변경 완료: {targetMap.Name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 변경 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 커스텀 맵으로 직접 변경
    /// </summary>
    public async Task SwitchToCustomMapAsync(CustomMapModel customMap)
    {
        try
        {
            await SwitchToMapAsync(customMap);
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 변경 실패: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - 홈 위치 관리 -
    /// <summary>
    /// 초기 홈 위치 설정
    /// </summary>
    private void SetInitialHomePosition()
    {
        HomePosition = new HomePositionModel();
        if (_setupModel.HomePosition == null || _setupModel.HomePosition.Position == null) return;
        var position = _setupModel.HomePosition.Position;
        HomePosition.Position = position;
        HomePosition.Zoom = _setupModel.HomePosition.Zoom;
        HomePosition.IsAvailable = _setupModel.HomePosition?.IsAvailable ?? false;
        ClickedCurrentPosition = new PointLatLng(position.Latitude, position.Longitude);
        MoveHomeLocationCommand?.RaiseCanExecuteChanged();
        SetHomeLocationCommand?.RaiseCanExecuteChanged();

        _log?.Info($"HomePosition정보가 (Lat:{HomePosition.Position.Latitude}, Lng:{HomePosition.Position.Longitude}, Alt:{HomePosition.Position.Altitude}, Zoom:{HomePosition.Zoom})으로 설정되었습니다.");
    }

    /// <summary>
    /// 홈 위치 설정 - 현재 클릭된 위치를 홈으로 저장
    /// </summary>
    public async void SetHomePosition()
    {
        if (HomePosition == null) return;
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 홈 위치 저장 차단"); ShowNoMapEditPermissionInfo(); return; }

        HomePosition.Position = new CoordinateModel(latitude: ClickedCurrentPosition.Lat, longitude: ClickedCurrentPosition.Lng, altitude: 0);
        HomePosition.Zoom = Zoom;
        HomePosition.IsAvailable = true;
        MoveHomeLocationCommand?.RaiseCanExecuteChanged();
        SetHomeLocationCommand?.RaiseCanExecuteChanged();
        _log?.Info($"The home position is set to (Position: ({HomePosition.Position.Latitude}, {HomePosition.Position.Longitude}), Zoom: {HomePosition.Zoom}).");
        await MapSettingsHelper.SaveHomePositionAsync(HomePosition, _log);
    }

    /// <summary>
    /// 홈 위치로 이동
    /// </summary>
    public void GoToHomePosition()
    {
        if (HomePosition == null || HomePosition.Position == null) return;

        MainMap.Position = new PointLatLng(HomePosition.Position.Latitude, HomePosition.Position.Longitude);
        MainMap.Zoom = HomePosition.Zoom;
        _log?.Info($"Moved to home position.");
    }

    /// <summary>
    /// 홈 위치 해제
    /// </summary>
    public async void ClearHomePosition()
    {
        if (HomePosition == null || HomePosition.Position == null) return;

        MainMap.Position = new PointLatLng(HomePosition.Position.Latitude, HomePosition.Position.Longitude);
        HomePosition.Zoom = DEFAULT_ZOOM;
        HomePosition.IsAvailable = false;
        MoveHomeLocationCommand?.RaiseCanExecuteChanged();
        SetHomeLocationCommand?.RaiseCanExecuteChanged();
        _log?.Info($"Home position has been released..");

        // JSON에 저장
        await MapSettingsHelper.SaveHomePositionAsync(HomePosition);
    }
    #endregion

    #region - 마커 관리 및 편집 -
    /// <summary>
    /// 마커 추가 (테스트용) - 지정된 위치에 새 마커 생성
    /// </summary>
    public async Task AddCustomMarker(PointLatLng position, string title = "CustomMarker")
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — CustomMarker 추가 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            // 1. SymbolModel 생성
            var symbolModel = new SymbolModel
            {
                Title = title,
                TitleSize = 12,
                Latitude = position.Lat,
                Longitude = position.Lng,
                Zoom = Zoom,
                Width = 50,
                Height = 75,
                Bearing = 0,
                Category = EnumMarkerCategory.BASIC_SHAPES,
                ShowShape = true,
                ShowTitle = false,
                OperationState = EnumOperationState.ACTIVATED
            };

            var symbolId = await _gMapDbSymbolService.InsertSymbolAsync(symbolModel);
            var savedSymbol = await _gMapDbSymbolService.FetchSymbolAsync(symbolId);
            if (savedSymbol == null) throw new NullReferenceException($"SymbolId({symbolId})를 이용하여 FetchSymbolAsync 수행을 실패 했습니다.");
            AddMarkerFromSymbol(savedSymbol);

            // 강제 새로고침
            MainMap?.InvalidateVisual();

            //_log?.Info($"마커 추가 완료: {title} at ({position.Lat:F6}, {position.Lng:F6})");
            _log?.Info($"현재 총 마커 수: {MainMap?.Markers.Count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"테스트 마커 추가 실패: {ex.Message}");
        }
    }

    public async Task AddGeometricMarker(PointLatLng position, EnumShapeType shapeType, string title = "GeometricMarker")
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — GeometricMarker 추가 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            // 1. SymbolModel 생성
            var symbolModel = new GeometricSymbolModel
            {
                Title = title,
                TitleSize = 12,
                Latitude = position.Lat,
                Longitude = position.Lng,
                Zoom = Zoom,
                Width = 50,
                Height = 50,
                Bearing = 0,
                Category = EnumMarkerCategory.GEOMETRICS,
                ShowShape = true,
                ShowTitle = false,
                OperationState = EnumOperationState.ACTIVATED,
                Opacity = 0.7,
                ShapeType = shapeType
            };

            var symbolId = await _gMapDbSymbolService.InsertGeometrySymbolAsync(symbolModel);
            var savedSymbol = await _gMapDbSymbolService.FetchGeometrySymbolAsync(symbolId);
            if (savedSymbol == null) throw new NullReferenceException($"SymbolId({symbolId})를 이용하여 FetchGeometrySymbolAsync 수행을 실패 했습니다.");
            AddMarkerFromSymbol(savedSymbol);

            // 강제 새로고침
            MainMap?.InvalidateVisual();

            //_log?.Info($"마커 추가 완료: {title} at ({position.Lat:F6}, {position.Lng:F6})");
            _log?.Info($"현재 총 마커 수: {MainMap?.Markers.Count}");
        }
        catch (Exception ex)
        {
            _log?.Error($"테스트 마커 추가 실패: {ex.Message}");
        }
    }

    private Task AddPidsMarker(PointLatLng position, EnumDeviceType deviceType, string title)
    {
        try
        {

            switch (deviceType)
            {
                case EnumDeviceType.NONE:
                    break;
                case EnumDeviceType.Controller:
                case EnumDeviceType.Multi:
                case EnumDeviceType.Fence:
                case EnumDeviceType.Underground:
                case EnumDeviceType.Contact:
                case EnumDeviceType.PIR:
                case EnumDeviceType.IoController:
                case EnumDeviceType.Laser:
                case EnumDeviceType.Cable:
                case EnumDeviceType.IpCamera:
                case EnumDeviceType.SmartSensor:
                case EnumDeviceType.SmartSensor2:
                case EnumDeviceType.SmartCompound:
                case EnumDeviceType.IpSpeaker:
                case EnumDeviceType.Radar:
                case EnumDeviceType.OpticalCable:
                    AddPidsSingleMarker(position, deviceType, title);
                    break;
                case EnumDeviceType.Fence_Group:
                    AddPidsGroupMarker(position, deviceType, title);
                    break;
                default:
                    break;
            }

            
        }
        catch (Exception ex)
        {
            _log?.Error($"테스트 마커 추가 실패: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    

    private async void AddPidsSingleMarker(PointLatLng position, EnumDeviceType deviceType, string title)
    {
        try
        {
            // 1. SymbolModel 생성
            var symbolModel = new PidsSymbolModel
            {
                Title = title,
                TitleSize = 12,
                Latitude = position.Lat,
                Longitude = position.Lng,
                Zoom = Zoom,
                Width = 50,
                Height = 50,
                Bearing = 0,
                Category = EnumMarkerCategory.PIDS_EQUIPMENT,
                ShowShape = true,
                ShowTitle = false,
                OperationState = EnumOperationState.ACTIVATED,
                LinkedDeviceId = 2,
                DeviceType = deviceType,
                FOVOpacity = 0.7,
                FOVColor = EnumColorType.Red,
                DetectionRange = 30,
                DetectionAngle = 80,
                DetectionBearing = 0,
                ShowFOV = false,
                EventStatus = EnumEventStatus.Normal
            };

            var symbolId = await _gMapDbSymbolService.InsertPidsSymbolAsync(symbolModel);
            var savedSymbol = await _gMapDbSymbolService.FetchPidsSymbolAsync(symbolId);
            AddMarkerFromSymbol(savedSymbol ?? symbolModel);   // 저장된(Id 부여) 모델 사용 — Id=0 버그 수정(Undo 추가기록 활성화)

            // 강제 새로고침
            MainMap?.InvalidateVisual();

            //_log?.Info($"마커 추가 완료: {title} at ({position.Lat:F6}, {position.Lng:F6})");
            _log?.Info($"현재 총 마커 수: {MainMap?.Markers.Count}");
        }
        catch (Exception ex)
        {
            _log.Error(ex.Message);
        }
    }

    private async void AddPidsGroupMarker(PointLatLng position, EnumDeviceType deviceType, string title)
    {
        try
        {

            // 라인 드로잉 파라미터 설정
            var parameters = new LineDrawingParameters
            {
                Title = title,
                Model = new PidsGroupSymbolModel(),
            };

            // 라인 드로잉 시작
            var result = await MainMap.StartLineDrawingAsync(parameters);

            if (result)
            {
                IsLineDrawing = true;
                LineDrawingStatus = "경계선 그리기: 첫 번째 포인트를 클릭하세요";

                _log?.Info("라인 드로잉 모드 시작됨");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"구역 경계선 마커 추가 실패: {ex.Message}");
        }
    }


    /// <summary>
    /// 구역 경계선 마커 추가 (라인 드로잉 모드 시작)
    /// </summary>
    private async Task AddAreaBoundaryMarker(PointLatLng position, string areaType, string title)
    {
        try
        {
            _log?.Info($"구역 경계선 마커 추가 시작: {areaType}");

            // 라인 드로잉 파라미터 설정
            var parameters = new LineDrawingParameters
            {
                Title = title,
                Model = new LineSymbolModel(),
            };

            // 라인 타입에 따른 설정
            switch (areaType.ToLower())
            {
                case "area":
                    parameters.Model.StrokeColor = EnumColorType.Blue;
                    parameters.Model.LinePattern = EnumLinePattern.Solid;
                    parameters.Model.IsClosedPath = true;
                    break;

                case "line":
                    parameters.Model.StrokeColor = EnumColorType.Yellow;
                    parameters.Model.LinePattern = EnumLinePattern.Dashed;
                    parameters.Model.IsClosedPath = false;
                    break;
            }

            // 라인 드로잉 시작
            var result = await MainMap.StartLineDrawingAsync(parameters);

            if (result)
            {
                IsLineDrawing = true;
                LineDrawingStatus = "경계선 그리기: 첫 번째 포인트를 클릭하세요";

                _log?.Info("라인 드로잉 모드 시작됨");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"구역 경계선 마커 추가 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 건물 심볼 추가하는 메소드
    /// </summary>
    /// <param name="position"></param>
    /// <param name="infraType"></param>
    /// <param name="symbolTitle"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private async Task AddInfraMarker(PointLatLng position, string infraType, string symbolTitle)
    {
        try
        {
            // 1. 인프라 타입에 따른 기본값 설정
            EnumBuildingType buildingType = EnumBuildingType.Factory;
            EnumBuildingUsage buildingUsage = EnumBuildingUsage.Office;
            int floorCount = 1;
            int basementFloorCount = 0;
            double buildingArea = 100.0;
            string title = string.Empty;
            // infraType에 따른 설정
            switch (infraType.ToLower())
            {
                case "factory":
                    buildingType = EnumBuildingType.Factory;
                    buildingUsage = EnumBuildingUsage.Office;
                    floorCount = 2;
                    basementFloorCount = 1;
                    buildingArea = 500.0;
                    title = $"공장_{DateTime.Now:HHmmss}";
                    break;

                default:
                    buildingType = EnumBuildingType.Factory;
                    buildingUsage = EnumBuildingUsage.Office;
                    floorCount = 3;
                    basementFloorCount = 0;
                    buildingArea = 300.0;
                    title = $"건물_{DateTime.Now:HHmmss}";
                    break;
            }

            // 2. InfraSymbolModel 생성
            var infraSymbol = new InfraSymbolModel
            {
                Title = title,
                TitleSize = 12,
                Latitude = position.Lat,
                Longitude = position.Lng,
                Zoom = Zoom,
                Width = 40,
                Height = 50,
                Bearing = 0,
                Category = EnumMarkerCategory.INFRASTRUCTURE,
                ShowShape = true,
                ShowTitle = false,
                OperationState = EnumOperationState.ACTIVATED,
                FillColor = EnumColorType.Brown,
                StrokeColor = EnumColorType.Gray,
                StrokeThickness = 2,

                // Infrastructure 전용 속성
                BuildingType = buildingType,
                BuildingUsage = buildingUsage,
                FloorCount = floorCount,
                BasementFloorCount = basementFloorCount,
                BuildingArea = buildingArea
            };

            // 3. DB에 저장
            var symbolId = await _gMapDbSymbolService.InsertInfraSymbolAsync(infraSymbol);

            // 4. 저장된 심볼 가져오기
            var savedSymbol = await _gMapDbSymbolService.FetchInfraSymbolAsync(symbolId);

            if (savedSymbol != null)
            {
                // 5. 마커로 변환하여 지도에 추가
                AddMarkerFromSymbol(savedSymbol);

                // 6. 화면 갱신
                MainMap?.InvalidateVisual();

                _log?.Info($"인프라 마커 추가 완료: {title}");
                _log?.Info($"  건물타입: {buildingType}, 용도: {buildingUsage}");
                _log?.Info($"  층수: B{basementFloorCount}/F{floorCount}, 면적: {buildingArea:N0}㎡");
                _log?.Info($"  위치: ({position.Lat:F6}, {position.Lng:F6})");
            }
            else
            {
                _log?.Error($"인프라 심볼 DB 저장 후 가져오기 실패");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"인프라 마커 추가 실패: {ex.Message}");
        }
    }

    // <summary>
    /// 심볼로부터 마커 추가
    /// </summary>
    /// <param name="deferLayerSync">true면 레이어 트리 리빌드(LoadLayersFromDbAsync)를 생략 — 배치 붙여넣기에서
    /// 항목마다 N회 리빌드하지 않고 호출측이 배치 완료 후 1회만 리빌드하도록(NFR-01). RecordAdd/provider 동기화는 유지.</param>
    /// <returns>생성·추가된 마커(붙여넣기 후 자동선택용). 실패 시 null.</returns>
    private IEditableMarker? AddMarkerFromSymbol(ISymbolModel symbolModel, bool isExistingMarker = false, bool deferLayerSync = false)
    {
        try
        {
            //_log?.Info($"마커 생성 시작: Type={symbolModel.GetType().Name}, Title={symbolModel.Title}");

            // 1. Factory로 마커 생성
            var marker = _markerFactory.CreateMarker(symbolModel);

            // 2. 지도에 추가
            AddMarkerToMap(marker, isExistingMarker);

            // 3. 사용자 추가 심볼을 _symbolProvider 캐시에 동기화 + 레이어 트리 갱신(맵→패널 싱크).
            //    부팅 복원(isExistingMarker=true)은 이미 provider에 있으므로 제외(중복 방지).
            //    provider는 부팅 스냅샷이라 세션 중 추가/삭제를 여기서 반영해야 트리가 일치함(감사 P0).
            if (!isExistingMarker)
            {
                if (!_symbolProvider.Any(s => ReferenceEquals(s, symbolModel)))
                    _symbolProvider.Add(symbolModel);
                if (!deferLayerSync)
                    _ = LoadLayersFromDbAsync();   // 패널 열려있으면 새 노드 반영(트리 리빌드, 내부 try/catch)
                if (marker is IEditableMarker em && em.Id > 0) _editRecorder?.RecordAdd(em);   // Undo 기록(추가). Id=0(AddPidsSingle 버그) 제외
            }

            //_log?.Info($"마커 추가 완료: {symbolModel.Title}");
            return marker as IEditableMarker;
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 추가 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 지도에 마커 추가 - 단순화
    /// </summary>
    private void AddMarkerToMap(IEditableMarker marker, bool isExistingMarker = false)
    {
        if (marker is not GMapMarker gMapMarker)
        {
            _log?.Error($"마커가 GMapMarker가 아닙니다: {marker.GetType().Name}");
            return;
        }

        // GMap에 추가
        MainMap?.Markers.Add(gMapMarker);
        // EditMode 상태에 맞게 IsHitTestVisible 동기화
        if (gMapMarker.Shape is UIElement shapeElement)
        {
            shapeElement.IsHitTestVisible = IsEditModeEnabled;

            if (isExistingMarker)
            {
                // DB 로드 마커: 생성자에서 이미 symbolModel.ZIndex로 ZIndex + Panel.ZIndex 설정됨 — 유지
                System.Windows.Controls.Panel.SetZIndex(shapeElement, gMapMarker.ZIndex);
            }
            else
            {
                // 신규 마커: 심볼 band(1000+) 내 최상위 ZIndex + 1 부여
                int maxZ = 1000 - 1; // 심볼 band floor (최소 1000)
                foreach (var m in MainMap.Markers)
                {
                    if (m is IEditableMarker and not IImageEditableMarker && m.Shape is UIElement s)
                        maxZ = Math.Max(maxZ, System.Windows.Controls.Panel.GetZIndex(s));
                }
                ApplyMarkerZOrder(gMapMarker, shapeElement, maxZ + 1);
            }
        }

        // Shape 확인 로그
        var shapeType = gMapMarker.Shape?.GetType().Name ?? "null";
        //_log?.Info($"마커 '{marker.Title}' 추가됨, Shape: {shapeType}");
    }

    /// <summary>
    /// 마커 위치 업데이트
    /// </summary>
    public void UpdateMarkerPosition(GMapCustomMarker marker, PointLatLng newPosition)
    {
        try
        {
            if (marker == null) return;

            marker.UpdateLocation(newPosition);
            _log?.Info($"마커 '{marker.Title}' 위치 업데이트: ({newPosition.Lat:F6}, {newPosition.Lng:F6})");

            // 화면 갱신
            //MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 위치 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 회전 업데이트
    /// </summary>
    public void UpdateMarkerRotation(IEditableMarker marker, double bearing)
    {
        try
        {
            if (marker == null) return;

            marker.Bearing = bearing;
            _log?.Info($"마커 '{marker.Title}' 회전 업데이트: {bearing:F1}도");

            // 화면 갱신
            //MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 회전 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 크기 업데이트
    /// </summary>
    public void UpdateMarkerSize(IEditableMarker marker, double width, double height)
    {
        try
        {
            if (marker == null) return;

            marker.Width = Math.Max(10, width);   // 최소 크기 보장
            marker.Height = Math.Max(10, height); // 최소 크기 보장

            _log?.Info($"마커 '{marker.Title}' 크기 업데이트: {width:F1}x{height:F1}");

            // 화면 갱신
            //MainMap.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 크기 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 선택된 마커 복제
    /// </summary>
    public async void DuplicateSelectedMarker()
    {
        try
        {
            if (SelectedMarker == null)
            {
                _log?.Warning("복제할 마커가 선택되지 않았습니다.");
                return;
            }
            if (!CanEditMap()) { _log?.Warning("[RBAC] 맵 편집 권한 없음 — 심볼 복제 차단"); ShowNoMapEditPermissionInfo(); return; }

            _log?.Info($"마커 복제 시작: {SelectedMarker.Title}");

            // 1. 복제할 위치 계산 (원본에서 약간 이동)
            var originalPos = SelectedMarker.Position;
            var newPos = new PointLatLng(originalPos.Lat + 0.0001, originalPos.Lng + 0.0001);

            // 2. 심볼 스냅샷 딥클론 → 복사 코어(CreateSymbolCopyAsync) 재사용. Duplicate(오프셋)/Paste(커서) 공유.
            //    이미지 마커는 복제 대상 아님(v1). PIDS Id 유실(`= pidsSymbol`)·`+1000` 실장비 충돌 버그는
            //    코어에서 근본 수정(미링크 0 + Fetch Id 사용).
            ISymbolModel? duplicatedSymbol = null;
            var dupSnap = Services.Undo.SymbolSnapshot.Capture(SelectedMarker);
            if (dupSnap == null || dupSnap.IsImage)
            {
                _log?.Warning("복제 실패 — 스냅샷 생성 불가(또는 이미지 마커는 복제 미지원).");
                return;
            }
            if (dupSnap.CloneModel() is ISymbolModel dupClone)
                duplicatedSymbol = await CreateSymbolCopyAsync(dupClone, originalPos, newPos, appendCopySuffix: true);

            // 3. 복제된 심볼로 마커 생성 및 지도에 추가
            if (duplicatedSymbol != null)
            {
                AddMarkerFromSymbol(duplicatedSymbol);

                // 강제 새로고침
                MainMap?.InvalidateVisual();

                _log?.Info($"마커 복제 완료: {duplicatedSymbol.Title} at ({newPos.Lat:F6}, {newPos.Lng:F6})");
                _log?.Info($"현재 총 마커 수: {MainMap?.Markers.Count}");
            }
            else
            {
                _log?.Error("복제된 심볼 생성 실패");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 복제 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 우클릭 메뉴 생성 — 모든 마커 타입 지원, PIDS 전용 항목은 타입 체크 후 추가
    /// </summary>
    public void ShowMarkerContextMenu(IEditableMarker marker, Point screenPosition)
    {
        try
        {
            if (marker == null) return;

            _log?.Info($"마커 컨텍스트 메뉴 표시: {marker.Title}");

            var menu = new ContextMenu();

            // ── PIDS 전용 메뉴 ──
            if (marker is IPidsEditableMarker pidsMarker)
            {
                var devName = pidsMarker.DeviceType switch
                {
                    EnumDeviceType.Controller  => "제어기",
                    EnumDeviceType.SmartSensor => "감지센서",
                    EnumDeviceType.IpCamera    => "감시카메라",
                    EnumDeviceType.IpSpeaker   => "스피커",
                    EnumDeviceType.Lamp        => "경광등",
                    EnumDeviceType.Enclosure   => "함체",
                    _                          => "장치",
                };
                var hasDevice = pidsMarker.LinkedDeviceId > 0;

                var webServerEnabled = _deviceDetailUrlService?.IsWebServerEnabled == true;
                var listUrl = _deviceDetailUrlService?.BuildUrl(pidsMarker.DeviceType, 0, null) ?? string.Empty;
                var listItem = new MenuItem
                {
                    Header = $"{devName}페이지",
                    IsEnabled = !string.IsNullOrEmpty(listUrl),
                    Visibility = (IsEditModeEnabled || webServerEnabled) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ViewList, Width = 16, Height = 16 }
                };
                listItem.Click += (s, e) => _deviceDetailUrlService.OpenInChrome(listUrl);
                menu.Items.Add(listItem);

                var detailItem = new MenuItem
                {
                    Header = $"{devName}상세",
                    IsEnabled = hasDevice,
                    Visibility = (IsEditModeEnabled || webServerEnabled) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.InformationOutline, Width = 16, Height = 16 }
                };
                detailItem.Click += (s, e) =>
                {
                    var url = _deviceDetailUrlService.BuildUrl(pidsMarker.DeviceType, pidsMarker.LinkedDeviceId, "detail");
                    _deviceDetailUrlService.OpenInChrome(url);
                };
                menu.Items.Add(detailItem);

                var editItem = new MenuItem
                {
                    Header = $"{devName}수정",
                    IsEnabled = hasDevice,
                    Visibility = (IsEditModeEnabled || webServerEnabled) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Pencil, Width = 16, Height = 16 }
                };
                editItem.Click += (s, e) =>
                {
                    var url = _deviceDetailUrlService.BuildUrl(pidsMarker.DeviceType, pidsMarker.LinkedDeviceId, "edit");
                    _deviceDetailUrlService.OpenInChrome(url);
                };
                menu.Items.Add(editItem);

                // 제어기 홈페이지 (Controller 전용)
                if (pidsMarker.DeviceType == EnumDeviceType.Controller)
                {
                    var ctrlItem = new MenuItem
                    {
                        Header = "제어기 홈페이지",
                        Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Web, Width = 16, Height = 16 }
                    };
                    var controllerModel = pidsMarker.LinkedDevice as IControllerDeviceModel;
                    ctrlItem.IsEnabled = controllerModel != null;
                    ctrlItem.Click += (s, e) =>
                    {
                        if (controllerModel != null)
                        {
                            var url = $"http://{controllerModel.IpAddress}:{controllerModel.Port}";
                            _deviceDetailUrlService.OpenInChrome(url);
                        }
                    };
                    menu.Items.Add(ctrlItem);
                }

                // 카메라 홈페이지 (IpCamera 전용)
                if (pidsMarker.DeviceType == EnumDeviceType.IpCamera)
                {
                    var camItem = new MenuItem
                    {
                        Header = "카메라 홈페이지",
                        Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Web, Width = 16, Height = 16 }
                    };
                    var cameraModel = pidsMarker.LinkedDevice as ICameraDeviceModel;
                    camItem.IsEnabled = cameraModel != null;
                    camItem.Click += (s, e) =>
                    {
                        if (cameraModel != null)
                        {
                            var url = $"http://{cameraModel.IpAddress}:{cameraModel.IpPort}";
                            _deviceDetailUrlService.OpenInChrome(url);
                        }
                    };
                    menu.Items.Add(camItem);

                    // 특정 위치 확인 (PTZ 카메라 전용) — 지도 클릭 좌표로 회전요청 NATS 발행
                    if (cameraModel != null
                        && cameraModel.Category == Ironwall.Dotnet.Libraries.Enums.EnumCameraType.PTZ)
                    {
                        var aimMarker = pidsMarker as GMapPidsMarker;   // concrete(런타임 PIDS 마커) — 반경=마커 DetectionRange
                        var aimItem = new MenuItem
                        {
                            Header = "특정 위치 확인",
                            Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.CrosshairsGps, Width = 16, Height = 16 }
                        };
                        // 동기 위임 — EnterTargetAimMode는 비동기 없음(async void 함정 회피)
                        aimItem.Click += (s, e) => { if (aimMarker != null) EnterTargetAimMode(aimMarker); };
                        menu.Items.Add(aimItem);
                    }
                }

                // 스피커 방송 제어 (IpSpeaker 전용)
                if (pidsMarker.DeviceType == EnumDeviceType.IpSpeaker)
                {
                    var isEnabled = pidsMarker.LinkedDeviceId > 0;

                    var playItem = new MenuItem
                    {
                        Header = "음원 실행",
                        IsEnabled = isEnabled,
                        Visibility = (IsEditModeEnabled || webServerEnabled) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                        Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Play, Width = 16, Height = 16 }
                    };
                    playItem.Click += (s, e) => ShowBroadcastPlayPanel(pidsMarker.LinkedDeviceId);
                    menu.Items.Add(playItem);

                    var ttsItem = new MenuItem
                    {
                        Header = "TTS 실행",
                        IsEnabled = isEnabled,
                        Visibility = (IsEditModeEnabled || webServerEnabled) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                        Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Microphone, Width = 16, Height = 16 }
                    };
                    ttsItem.Click += (s, e) => ShowTtsBroadcastPanel(pidsMarker.LinkedDeviceId);
                    menu.Items.Add(ttsItem);

                    var stopItem = new MenuItem
                    {
                        Header = "Stop",
                        IsEnabled = isEnabled,
                        Visibility = (IsEditModeEnabled || webServerEnabled) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                        Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Stop, Width = 16, Height = 16 }
                    };
                    stopItem.Click += async (s, e) =>
                    {
                        StopBroadcast(pidsMarker);
                        await _broadcastControlService.PublishStopAsync(pidsMarker.LinkedDeviceId);
                    };
                    menu.Items.Add(stopItem);
                }
            }

            // ── 레이어 순서 제어 (편집 모드에서만) ──
            if (IsEditModeEnabled)
            {
                if (menu.Items.Count > 0)
                    menu.Items.Add(new Separator());

                var moveTopItem = new MenuItem
                {
                    Header = "맨 위로",
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ArrowCollapseUp, Width = 16, Height = 16 }
                };
                moveTopItem.Click += (s, e) => { var zb = IsApplyingUndo ? null : CaptureZOrderPairs(); MoveMarkerToTop(marker); if (zb != null) RecordZOrderDiff(zb); };   // V4 undo 기록
                menu.Items.Add(moveTopItem);

                var moveUpItem = new MenuItem
                {
                    Header = "한 칸 위로",
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ArrowUp, Width = 16, Height = 16 }
                };
                moveUpItem.Click += (s, e) => { var zb = IsApplyingUndo ? null : CaptureZOrderPairs(); MoveMarkerUp(marker); if (zb != null) RecordZOrderDiff(zb); };   // V4 undo 기록
                menu.Items.Add(moveUpItem);

                var moveDownItem = new MenuItem
                {
                    Header = "한 칸 아래로",
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ArrowDown, Width = 16, Height = 16 }
                };
                moveDownItem.Click += (s, e) => { var zb = IsApplyingUndo ? null : CaptureZOrderPairs(); MoveMarkerDown(marker); if (zb != null) RecordZOrderDiff(zb); };   // V4 undo 기록
                menu.Items.Add(moveDownItem);

                var moveBottomItem = new MenuItem
                {
                    Header = "맨 아래로",
                    Icon = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.ArrowCollapseDown, Width = 16, Height = 16 }
                };
                moveBottomItem.Click += (s, e) => { var zb = IsApplyingUndo ? null : CaptureZOrderPairs(); MoveMarkerToBottom(marker); if (zb != null) RecordZOrderDiff(zb); };   // V4 undo 기록
                menu.Items.Add(moveBottomItem);
            }

            if (menu.Items.Count > 0)
            {
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 컨텍스트 메뉴 표시 실패: {ex.Message}");
        }
    }

    #region Marker ZOrder Control

    /// <summary>
    /// 앱 시작 시 모든 마커에 고유 ZIndex 할당 (이미지 band 0~999 / 심볼 band 1000+)
    /// 중복 ZIndex가 있으면 컬렉션 순서대로 순차 재할당 + Batch DB 저장
    /// </summary>
    private void EnsureUniqueZOrder()
    {
        if (MainMap?.Markers == null) return;
        EnsureUniqueZOrderForBand(isImage: true,  bandOffset: 0);
        EnsureUniqueZOrderForBand(isImage: false, bandOffset: 1000);
    }

    private void EnsureUniqueZOrderForBand(bool isImage, int bandOffset)
    {
        var markers = MainMap!.Markers
            .OfType<GMapMarker>()
            .Where(m => (m is IImageEditableMarker) == isImage
                        && m is IEditableMarker
                        && m.Shape is UIElement)
            .ToList();

        if (markers.Count == 0) return;

        var zValues = markers
            .Select(m => System.Windows.Controls.Panel.GetZIndex(m.Shape as UIElement))
            .ToList();

        if (zValues.Count == zValues.Distinct().Count())
        {
            _log?.Info($"[ZOrder] {(isImage ? "Image" : "Symbol")} band 고유값 확인 완료 — {markers.Count}개, 중복 없음");
            return;
        }

        _log?.Info($"[ZOrder] {(isImage ? "Image" : "Symbol")} band 중복 감지 — {markers.Count}개 재할당");
        var changes = new List<(int id, int zOrder)>();
        for (int i = 0; i < markers.Count; i++)
        {
            int newZ = bandOffset + i;
            var gMarker = markers[i];
            if (gMarker.Shape is UIElement shape)
            {
                ApplyMarkerZOrderLocal(gMarker, shape, newZ);
                if (gMarker is IEditableMarker em && em.Id > 0)
                    changes.Add((em.Id, newZ));
            }
        }

        if (isImage)
        {
            foreach (var (id, z) in changes)
            {
                var imgMarker = markers.OfType<IImageEditableMarker>()
                    .FirstOrDefault(m => m.Id == id);
                if (imgMarker != null && !string.IsNullOrEmpty(imgMarker.FilePath))
                {
                    var node = LayerTreeBuilder.Flatten(_layerTreeNodes)
                        .FirstOrDefault(n => string.Equals(n.Model?.FilePath, imgMarker.FilePath,
                            StringComparison.OrdinalIgnoreCase));
                    if (node?.Model != null)
                    {
                        node.Model.ZOrder = z;
                        _ = _gMapDbService.UpdateMapLayerAsync(node.Model);
                    }
                }
            }
        }
        else
        {
            if (changes.Count > 0)
                _ = _gMapDbSymbolService.BatchUpdateZOrderAsync(changes);
        }

        _log?.Info($"[ZOrder] {(isImage ? "Image" : "Symbol")} band 재할당 완료 ({bandOffset}~{bandOffset + markers.Count - 1}), DB {changes.Count}건");
        LogAllMarkerZOrder();
    }

    /// <summary>
    /// 심볼 마커를 한 칸 위로 이동 — 바로 위 마커와 ZIndex 스왑
    /// </summary>
    private void MoveMarkerUp(IEditableMarker marker)
    {
        if (marker is not GMapMarker gMarker || gMarker.Shape is not UIElement shape || MainMap == null) return;

        int currentZ = System.Windows.Controls.Panel.GetZIndex(shape);

        // 바로 위: 같은 band 내에서 가장 작은 ZIndex > currentZ 인 마커 찾기
        bool isImageMarker = gMarker is IImageEditableMarker;
        GMapMarker? target = null;
        int targetZ = int.MaxValue;
        foreach (var m in MainMap.Markers)
        {
            if (m == gMarker || m is not IEditableMarker || (m is IImageEditableMarker) != isImageMarker || m.Shape is not UIElement s) continue;
            int z = System.Windows.Controls.Panel.GetZIndex(s);
            if (z > currentZ && z < targetZ) { target = m; targetZ = z; }
        }

        if (target == null) return; // 이미 최상위

        // 스왑
        ApplyMarkerZOrder(gMarker, shape, targetZ);
        ApplyMarkerZOrder(target, target.Shape as UIElement, currentZ);
        _log?.Info($"[ZOrder] SwapUp: '{marker.Title}'({currentZ}→{targetZ}) ↔ '{((IEditableMarker)target).Title}'({targetZ}→{currentZ})");
        LogAllMarkerZOrder();
    }

    /// <summary>
    /// 심볼 마커를 한 칸 아래로 이동 — 바로 아래 마커와 ZIndex 스왑
    /// </summary>
    private void MoveMarkerDown(IEditableMarker marker)
    {
        if (marker is not GMapMarker gMarker || gMarker.Shape is not UIElement shape || MainMap == null) return;

        int currentZ = System.Windows.Controls.Panel.GetZIndex(shape);

        // 바로 아래: 같은 band 내에서 가장 큰 ZIndex < currentZ 인 마커 찾기
        bool isImageMarker = gMarker is IImageEditableMarker;
        GMapMarker? target = null;
        int targetZ = int.MinValue;
        foreach (var m in MainMap.Markers)
        {
            if (m == gMarker || m is not IEditableMarker || (m is IImageEditableMarker) != isImageMarker || m.Shape is not UIElement s) continue;
            int z = System.Windows.Controls.Panel.GetZIndex(s);
            if (z < currentZ && z > targetZ) { target = m; targetZ = z; }
        }

        if (target == null) return; // 이미 최하위

        // 스왑
        ApplyMarkerZOrder(gMarker, shape, targetZ);
        ApplyMarkerZOrder(target, target.Shape as UIElement, currentZ);
        _log?.Info($"[ZOrder] SwapDown: '{marker.Title}'({currentZ}→{targetZ}) ↔ '{((IEditableMarker)target).Title}'({targetZ}→{currentZ})");
        LogAllMarkerZOrder();
    }

    /// <summary>
    /// 심볼 마커를 맨 위로 이동 → 정규화 (0~n-1)
    /// </summary>
    private void MoveMarkerToTop(IEditableMarker marker)
    {
        if (marker is not GMapMarker gMarker || gMarker.Shape is not UIElement shape || MainMap == null) return;

        bool isImageMarkerTop = gMarker is IImageEditableMarker;
        int bandMin = isImageMarkerTop ? 0 : 1000;
        int maxZ = bandMin - 1;
        foreach (var m in MainMap.Markers)
        {
            if ((m is IImageEditableMarker) == isImageMarkerTop && m is IEditableMarker && m.Shape is UIElement s)
                maxZ = Math.Max(maxZ, System.Windows.Controls.Panel.GetZIndex(s));
        }
        var oldZ = System.Windows.Controls.Panel.GetZIndex(shape);
        ApplyMarkerZOrderLocal(gMarker, shape, maxZ + 1);
        _log?.Info($"[ZOrder] MoveToTop: '{marker.Title}' ZIndex={oldZ}→{maxZ + 1}");
        NormalizeAllZOrder();
    }

    /// <summary>
    /// 심볼 마커를 맨 아래로 이동 → 정규화 (0~n-1)
    /// </summary>
    private void MoveMarkerToBottom(IEditableMarker marker)
    {
        if (marker is not GMapMarker gMarker || gMarker.Shape is not UIElement shape || MainMap == null) return;

        bool isImageMarkerBottom = gMarker is IImageEditableMarker;
        int bandFloor = isImageMarkerBottom ? 0 : 1000;
        int minZ = int.MaxValue;
        foreach (var m in MainMap.Markers)
        {
            if ((m is IImageEditableMarker) == isImageMarkerBottom && m is IEditableMarker && m.Shape is UIElement s)
                minZ = Math.Min(minZ, System.Windows.Controls.Panel.GetZIndex(s));
        }
        if (minZ == int.MaxValue) minZ = bandFloor;
        var oldZ = System.Windows.Controls.Panel.GetZIndex(shape);
        int newBottomZ = Math.Max(minZ - 1, bandFloor);
        ApplyMarkerZOrderLocal(gMarker, shape, newBottomZ);
        _log?.Info($"[ZOrder] MoveToBottom: '{marker.Title}' ZIndex={oldZ}→{newBottomZ}");
        NormalizeAllZOrder();
    }

    /// <summary>
    /// 전체 마커를 현재 순서 기준으로 band별 재번호 + DB 저장
    /// 이미지 band: 0~n-1 / 심볼 band: 1000~(1000+m-1)
    /// </summary>
    private void NormalizeAllZOrder()
    {
        if (MainMap?.Markers == null) return;
        NormalizeZOrderBand(isImage: true,  bandOffset: 0);
        NormalizeZOrderBand(isImage: false, bandOffset: 1000);
    }

    private void NormalizeZOrderBand(bool isImage, int bandOffset)
    {
        var sorted = MainMap!.Markers
            .OfType<GMapMarker>()
            .Where(m => (m is IImageEditableMarker) == isImage
                        && m is IEditableMarker
                        && m.Shape is UIElement)
            .OrderBy(m => System.Windows.Controls.Panel.GetZIndex(m.Shape as UIElement))
            .ToList();

        if (sorted.Count == 0) return;

        var changes = new List<(int id, int zOrder)>();

        for (int i = 0; i < sorted.Count; i++)
        {
            int newZ = bandOffset + i;
            var gMarker = sorted[i];
            if (gMarker.Shape is not UIElement shape) continue;

            if (System.Windows.Controls.Panel.GetZIndex(shape) == newZ) continue;

            ApplyMarkerZOrderLocal(gMarker, shape, newZ);
            if (gMarker is IEditableMarker em && em.Id > 0)
                changes.Add((em.Id, newZ));
        }

        if (changes.Count == 0)
        {
            _log?.Info($"[ZOrder] {(isImage ? "Image" : "Symbol")} band 정규화 — 변경 없음");
            return;
        }

        if (isImage)
        {
            foreach (var (id, z) in changes)
            {
                var imgMarker = sorted.OfType<IImageEditableMarker>()
                    .FirstOrDefault(m => m.Id == id);
                if (imgMarker != null && !string.IsNullOrEmpty(imgMarker.FilePath))
                {
                    var node = LayerTreeBuilder.Flatten(_layerTreeNodes)
                        .FirstOrDefault(n => string.Equals(n.Model?.FilePath, imgMarker.FilePath,
                            StringComparison.OrdinalIgnoreCase));
                    if (node?.Model != null)
                    {
                        node.Model.ZOrder = z;
                        _ = _gMapDbService.UpdateMapLayerAsync(node.Model);
                    }
                }
            }
        }
        else
        {
            _ = _gMapDbSymbolService.BatchUpdateZOrderAsync(changes);
        }

        _log?.Info($"[ZOrder] {(isImage ? "Image" : "Symbol")} band 정규화 완료 ({bandOffset}~{bandOffset + sorted.Count - 1}), DB {changes.Count}건");
        LogAllMarkerZOrder();
    }

    public Task HandleAsync(ZOrderChangeRequestedEvent message, CancellationToken cancellationToken)
    {
        if (message?.Marker == null || MainMap == null) return Task.CompletedTask;
        var zBefore = IsApplyingUndo ? null : CaptureZOrderPairs();   // FIX 8 — 순서변경 undo 기록용 스냅샷
        switch (message.Direction)
        {
            case ZOrderDirection.Up:       MoveMarkerUp(message.Marker);       break;
            case ZOrderDirection.Down:     MoveMarkerDown(message.Marker);     break;
            case ZOrderDirection.ToTop:    MoveMarkerToTop(message.Marker);    break;
            case ZOrderDirection.ToBottom: MoveMarkerToBottom(message.Marker); break;
        }
        RefreshPropertyPanelZOrder();
        if (zBefore != null) RecordZOrderDiff(zBefore);
        return Task.CompletedTask;
    }

    /// <summary>현재 편집가능 마커들의 (Id,ZOrder) 스냅샷 — ZOrder undo 기록용(FIX 8).</summary>
    private System.Collections.Generic.List<(bool isImage, int id, int zOrder)> CaptureZOrderPairs()
        => MainMap?.Markers?.OfType<IEditableMarker>().Where(m => !m.IsDisposed && m.Id > 0)
               .Select(m => (isImage: m is GMapSymbols.GMapImageMarker, id: m.Id, zOrder: m.ZOrder)).ToList()
           ?? new System.Collections.Generic.List<(bool isImage, int id, int zOrder)>();

    /// <summary>ZOrder 변경 전 스냅샷 대비 변경분만 ZOrderBatchCommand로 Undo 기록(FIX 8).</summary>
    private void RecordZOrderDiff(System.Collections.Generic.List<(bool isImage, int id, int zOrder)> before)
    {
        if (before == null || _editRecorder == null) return;
        // 복합키(isImage,id) — 이미지(Images.Id)와 심볼(Symbols.Id)이 같은 숫자 Id로 한 Markers 컬렉션에 공존 →
        //   기존 ToDictionary(p=>p.id)가 중복키 크래시("동일 키 1")를 냈음. 밴드 구분키로 방지.
        var beforeMap = before.ToDictionary(p => (p.isImage, p.id), p => p.zOrder);
        var changedBefore = new System.Collections.Generic.List<(bool isImage, int id, int zOrder)>();
        var changedAfter = new System.Collections.Generic.List<(bool isImage, int id, int zOrder)>();
        foreach (var (isImage, id, z) in CaptureZOrderPairs())
            if (beforeMap.TryGetValue((isImage, id), out var oldZ) && oldZ != z)
            { changedBefore.Add((isImage, id, oldZ)); changedAfter.Add((isImage, id, z)); }   // isImage 보존(D1)
        if (changedAfter.Count > 0) _editRecorder.RecordZOrder(changedBefore, changedAfter);
    }

    private void RefreshPropertyPanelZOrder()
    {
        if (PropertyPanel == null || SelectedMarker == null || MainMap == null) return;

        bool isImageSelected = SelectedMarker is IImageEditableMarker;
        var bandMarkers = MainMap.Markers
            .OfType<GMapMarker>()
            .Where(m => (m is IImageEditableMarker) == isImageSelected
                        && m is IEditableMarker
                        && m.Shape is UIElement)
            .OrderBy(m => System.Windows.Controls.Panel.GetZIndex(m.Shape as UIElement))
            .ToList();

        int rank = bandMarkers.IndexOf(SelectedMarker as GMapMarker);
        PropertyPanel.MarkerZOrderDisplay = rank >= 0
            ? $"{rank + 1} / {bandMarkers.Count}"
            : "- / -";
    }

    /// <summary>
    /// ZIndex를 Shape + GMapMarker + Model에 적용 (DB 저장 없음 — Batch용)
    /// </summary>
    private void ApplyMarkerZOrderLocal(GMapMarker gMarker, UIElement shape, int newZ)
    {
        if (gMarker is IEditableMarker em)
            ((IEditableMarker)em).ZOrder = newZ;  // GMapMarker.ZIndex + _model.ZIndex + Panel.SetZIndex(shape) 일괄 처리
        else
        {
            System.Windows.Controls.Panel.SetZIndex(shape, newZ);
            gMarker.ZIndex = newZ;
        }
    }

    /// <summary>
    /// ZIndex를 Shape + GMapMarker + Model + DB 개별 저장 (스왑용)
    /// </summary>
    private void ApplyMarkerZOrder(GMapMarker gMarker, UIElement shape, int newZ)
    {
        ApplyMarkerZOrderLocal(gMarker, shape, newZ);
        if (gMarker is IEditableMarker editableMarker && editableMarker.Id > 0)
            _ = SaveMarkerZOrderAsync(editableMarker, newZ);
    }

    private async Task SaveMarkerZOrderAsync(IEditableMarker marker, int zOrder)
    {
        try
        {
            if (marker is IImageEditableMarker imageMarker)
            {
                var filePath = imageMarker.FilePath;
                var layerNode = string.IsNullOrEmpty(filePath) ? null
                    : LayerTreeBuilder.Flatten(_layerTreeNodes)
                        .FirstOrDefault(n => string.Equals(n.Model?.FilePath, filePath,
                            StringComparison.OrdinalIgnoreCase));
                if (layerNode?.Model != null)
                {
                    layerNode.Model.ZOrder = zOrder;
                    await _gMapDbService.UpdateMapLayerAsync(layerNode.Model);
                }
            }
            else
            {
                await _gMapDbSymbolService.UpdateSymbolZOrderAsync(marker.Id, zOrder);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[ZOrder] DB 저장 실패: Id={marker.Id} ZOrder={zOrder} — {ex.Message}");
        }
    }

    /// <summary>
    /// 모든 마커의 현재 ZOrder 상태를 로그로 덤프
    /// </summary>
    private void LogAllMarkerZOrder()
    {
        if (MainMap == null) return;
        _log?.Info("[ZOrder] ── 전체 마커 ZIndex 현황 ──");
        foreach (var m in MainMap.Markers)
        {
            if (m is IEditableMarker em)
            {
                var shapeType = m.Shape?.GetType().Name ?? "null";
                var z = m.Shape is UIElement s ? System.Windows.Controls.Panel.GetZIndex(s) : -1;
                var hitTest = m.Shape is UIElement ht ? ht.IsHitTestVisible : false;
                _log?.Info($"  [{z,4}] {em.Title,-20} Shape={shapeType,-35} HitTest={hitTest} Marker.ZIndex={m.ZIndex}");
            }
        }
        _log?.Info("[ZOrder] ── 끝 ──");
    }

    #endregion

    private async Task StartBroadcastTimer(IPidsEditableMarker marker, double seconds)
    {
        var speakerId = marker.LinkedDeviceId;
        if (_broadcastTimers.TryGetValue(speakerId, out var existing))
            existing.Cancel();

        var cts = new CancellationTokenSource();
        _broadcastTimers[speakerId] = cts;
        marker.IsBroadcasting = true;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                marker.IsBroadcasting = false;
                _broadcastTimers.Remove(speakerId);
            });
        }
    }

    private void StopBroadcast(IPidsEditableMarker marker)
    {
        var id = marker.LinkedDeviceId;
        if (_broadcastTimers.TryGetValue(id, out var cts))
        {
            cts.Cancel();
            _broadcastTimers.Remove(id);
        }
        marker.IsBroadcasting = false;
    }

    /// <summary>
    /// 마커 스냅 기능 (격자에 맞춤)
    /// </summary>
    public void SnapMarkerToGrid(IEditableMarker marker, double gridSize = 0.0001)
    {
        try
        {
            if (marker == null) return;

            var currentPos = marker.Position;
            var snappedLat = Math.Round(currentPos.Lat / gridSize) * gridSize;
            var snappedLng = Math.Round(currentPos.Lng / gridSize) * gridSize;

            var snappedPos = new PointLatLng(snappedLat, snappedLng);
            marker.UpdateLocation(snappedPos);

            _log?.Info($"마커 '{marker.Title}' 격자 스냅 완료: ({snappedLat:F6}, {snappedLng:F6})");

            MainMap?.InvalidateVisual();
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 격자 스냅 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 군사 심볼 등록창 표시
    /// </summary>
    private void ShowMilitarySymbolRegisterPanel()
    {
        if (IsMilitarySymbolRegisterVisible) return;

        _log?.Info("군사 심볼 등록창 표시");

        // 기존 패널이 있으면 정리
        HideMilitarySymbolRegisterPanel();

        // 새 등록창 생성
        MilitarySymbolRegisterPanel = new GMapMilitarySymbolRegisterControl();

        // 이벤트 구독
        MilitarySymbolRegisterPanel.MilitarySymbolRegisterRequested += OnMilitarySymbolRegisterRequested;
        MilitarySymbolRegisterPanel.CancelRequested += OnMilitarySymbolRegisterCancelled;

        IsMilitarySymbolRegisterVisible = true;
        _log?.Info("군사 심볼 등록창 표시 완료");
    }

    

    /// <summary>
    /// 군사 심볼 등록 요청 처리
    /// </summary>
    private async void OnMilitarySymbolRegisterRequested(object? sender, MilitarySymbolRegisterEventArgs e)
    {
        try
        {
            var militaryModel = e.MilitarySymbolModel;
            var position = ClickedCurrentPosition.IsEmpty ? MainMap!.CenterPosition : ClickedCurrentPosition;

            // 위치 설정
            militaryModel.Latitude = position.Lat;
            militaryModel.Longitude = position.Lng;
            militaryModel.Zoom = Zoom;

            _log?.Info($"군사 심볼 등록: {militaryModel.Title}");
            _log?.Info($"소속: {militaryModel.Affiliation}, 공중성: {militaryModel.BattleDimension}");
            _log?.Info($"부대타입: {militaryModel.UnitType}, 규모: {militaryModel.UnitSize}");

            // DB에 저장
            var symbolId = await _gMapDbSymbolService.InsertMilitarySymbolAsync(militaryModel);
            var savedSymbol = await _gMapDbSymbolService.FetchMilitarySymbolAsync(symbolId);

            if (savedSymbol != null)
            {
                // 지도에 마커 추가
                AddMarkerFromSymbol(savedSymbol);

                // 강제 새로고침
                MainMap?.InvalidateVisual();

                _log?.Info($"군사 심볼 추가 완료: {savedSymbol.Title}");
            }

            // 등록창 닫기
            HideMilitarySymbolRegisterPanel();
        }
        catch (Exception ex)
        {
            _log?.Error($"군사 심볼 등록 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 군사 심볼 등록 취소 처리
    /// </summary>
    private void OnMilitarySymbolRegisterCancelled(object? sender, EventArgs e)
    {
        _log?.Info("군사 심볼 등록 취소됨");
        HideMilitarySymbolRegisterPanel();
    }

    /// <summary>
    /// 군사 심볼 등록창 숨김
    /// </summary>
    private void HideMilitarySymbolRegisterPanel()
    {
        if (MilitarySymbolRegisterPanel != null)
        {
            // 이벤트 구독 해제
            MilitarySymbolRegisterPanel.MilitarySymbolRegisterRequested -= OnMilitarySymbolRegisterRequested;
            MilitarySymbolRegisterPanel.CancelRequested -= OnMilitarySymbolRegisterCancelled;

            MilitarySymbolRegisterPanel = null;
        }

        IsMilitarySymbolRegisterVisible = false;
        _log?.Info("군사 심볼 등록창 숨김 완료");
    }
    #endregion

    #region - 회전 속성 동기화 -
    /// <summary>
    /// 회전 관련 속성들을 MainMap과 동기화하는 메서드
    /// DependencyProperty는 자동으로 바인딩되므로 PropertyChanged 이벤트 불필요
    /// </summary>
    private void SyncRotationProperties()
    {
        if (MainMap != null)
        {
            // 단방향 초기값 설정만 수행
            UpdateRotationPropertiesFromMainMap();

            _log?.Info("회전 속성 초기화 완료");
        }
    }

    /// <summary>
    /// MainMap에서 현재 회전 상태를 읽어와서 ViewModel 속성 초기화
    /// </summary>
    private void UpdateRotationPropertiesFromMainMap()
    {
        if (MainMap == null) return;

        // DependencyProperty 값을 직접 읽어서 ViewModel 초기화
        _currentRotation = MainMap.MapRotation;
        _mapRotation = MainMap.MapRotation;
        _rotationSnapAngle = MainMap.RotationSnapAngle;
        _showRotationControl = MainMap.ShowRotationControl;

        // UI 업데이트 알림
        NotifyOfPropertyChange(nameof(CurrentRotation));
        NotifyOfPropertyChange(nameof(MapRotation));
        NotifyOfPropertyChange(nameof(RotationSnapAngle));
        NotifyOfPropertyChange(nameof(ShowRotationControl));
        NotifyOfPropertyChange(nameof(IsRotated));
    }
    #endregion

    #region Line Drawing Command Implementations

    /// <summary>
    /// 라인 드로잉 시작
    /// </summary>
    private async Task ExecuteStartLineDrawing()
    {
        try
        {
            _log?.Info("라인 드로잉 시작 명령 실행");

            // 선택된 심볼 타입에 따른 파라미터 설정
            var parameters = CreateLineDrawingParameters();

            // 라인 드로잉 시작
            var result = await MainMap.StartLineDrawingAsync(parameters);

            if (result)
            {
                IsLineDrawing = true;
                LineDrawingStatus = "첫 번째 포인트를 클릭하세요";

                // UI 업데이트
                UpdateCommandStates();
            }
            else
            {
                _log?.Warning("라인 드로잉 시작 실패");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"라인 드로잉 시작 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 라인 드로잉 완료
    /// </summary>
    private async Task ExecuteCompleteLineDrawing()
    {
        try
        {
            _log?.Info("라인 드로잉 완료 명령 실행");

            var result = await MainMap.CompleteLineDrawingAsync();

            if (result)
            {
                IsLineDrawing = false;
                LineDrawingStatus = string.Empty;

                _log?.Info("라인이 성공적으로 생성되었습니다.");
            }
            else
            {
                _log?.Info("최소 2개의 포인트가 필요합니다.");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"라인 드로잉 완료 오류: {ex.Message}");
            _log?.Error("라인 완료 중 오류가 발생했습니다.");
        }
    }

    /// <summary>
    /// 라인 드로잉 취소
    /// </summary>
    private async Task ExecuteCancelLineDrawing()
    {
        try
        {
            _log?.Info("라인 드로잉 취소 명령 실행");

            var result = await MainMap.CancelLineDrawingAsync();

            if (result)
            {
                IsLineDrawing = false;
                LineDrawingStatus = string.Empty;

                _log?.Info("라인 드로잉이 취소되었습니다.");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"라인 드로잉 취소 오류: {ex.Message}");
        }
    }

    /// <summary>
    /// 마지막 포인트 제거
    /// </summary>
    private void ExecuteUndoLastPoint()
    {
        try
        {
            _lineDrawingService?.UndoLastPoint();
            UpdateLineDrawingStatus();
        }
        catch (Exception ex)
        {
            _log?.Error($"마지막 포인트 제거 오류: {ex.Message}");
        }
    }

    private bool CanExecuteCompleteLineDrawing()
    {
        return IsLineDrawing && _lineDrawingService?.PointCount >= 2;
    }

    private bool CanExecuteCancelLineDrawing()
    {
        return IsLineDrawing;
    }

    private bool CanExecuteUndoLastPoint()
    {
        return IsLineDrawing && _lineDrawingService?.PointCount > 0;
    }

    /// <summary>
    /// 라인 드로잉 파라미터 생성 (새로 추가)
    /// </summary>
    private LineDrawingParameters CreateLineDrawingParameters()
    {

        var parameters = new LineDrawingParameters
        {
            Model = new LineSymbolModel(),
        };

        // 선택된 타입에 따른 세부 설정
        if (SelectedMarkerCategory == EnumMarkerCategory.AREA_BOUNDARY)
        {
            switch (SelectedSymbolType?.ToString()?.ToLower())
            {
                case "area":
                    parameters.Title = "구역";
                    parameters.Model.StrokeColor = EnumColorType.Blue;
                    parameters.Model.LinePattern = EnumLinePattern.Solid;
                    parameters.Model.IsClosedPath = true;
                    break;

                case "line":
                    parameters.Title = "경계선";
                    parameters.Model.StrokeColor = EnumColorType.Yellow;
                    parameters.Model.LinePattern = EnumLinePattern.Dashed;
                    parameters.Model.IsClosedPath = false;
                    break;
            }
        }

        return parameters;
    }
    #endregion

    #region Line Drawing Event Handlers

    /// <summary>
    /// 라인 드로잉 상태 변경
    /// </summary>
    private void OnLineDrawingStateChanged(object? sender, LineDrawingState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case LineDrawingState.FirstClick:
                    LineDrawingStatus = "첫 번째 포인트를 클릭하세요";
                    break;

                case LineDrawingState.Drawing:
                    UpdateLineDrawingStatus();
                    break;

                case LineDrawingState.Completed:
                    IsLineDrawing = false;
                    LineDrawingStatus = "라인 완성";
                    break;

                case LineDrawingState.Cancelled:
                    IsLineDrawing = false;
                    LineDrawingStatus = "라인 취소됨";
                    break;
            }

            UpdateCommandStates();
        });
    }

    /// <summary>
    /// 라인 포인트 추가
    /// </summary>
    private void OnLinePointAdded(object? sender, PointLatLng point)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            UpdateLineDrawingStatus();
            UpdateCommandStates();
        });
    }

    /// <summary>
    /// 라인 완성
    /// </summary>
    private void OnLineCompleted(object? sender, ILineEditableMarker lineMarker)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            try
            {
                _log?.Info($"라인 완성: {lineMarker.Title}");
                _log?.Info($"포인트 수: {lineMarker.LinePoints.Count}");
                _log?.Info($"총 거리: {lineMarker.TotalDistance:F1}m");

                // DB에 저장
                if (lineMarker is GMapLineMarker gMapLineMarker)
                {

                    //var savedId = await DbSaveProcess(gMapLineMarker);
                    var savedId = await _gMapDbSymbolService.InsertLineSymbolAsync(gMapLineMarker.Model);
                    var fetchedMarker = await _gMapDbSymbolService.FetchLineSymbolAsync(savedId);
                    if (savedId > 0)
                    {
                        _log?.Info($"라인 DB 저장 완료: ID={savedId}");
                    }
                    AddMarkerFromSymbol(fetchedMarker);
                }
                else if(lineMarker is GMapPidsGroupMarker gMapPidsGroupMarker)
                {

                    //var savedId = await DbSaveProcess(gMapLineMarker);
                    var savedId = await _gMapDbSymbolService.InsertPidsGroupSymbolAsync(gMapPidsGroupMarker.Model);
                    var fetchedMarker = await _gMapDbSymbolService.FetchPidsGroupSymbolAsync(savedId);
                    if (savedId > 0)
                    {
                        _log?.Info($"라인 DB 저장 완료: ID={savedId}");
                    }
                    AddMarkerFromSymbol(fetchedMarker);
                }

                // UI 상태 업데이트
                IsLineDrawing = false;
                LineDrawingStatus = string.Empty;
            }
            catch (Exception ex)
            {
                _log?.Error($"라인 완성 처리 오류: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 라인 드로잉 취소
    /// </summary>
    private void OnLineDrawingCancelled(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _log?.Info("라인 드로잉 취소됨");
            UpdateCommandStates();
        });
    }

    #endregion

    #region - 이벤트 핸들러 -
    /// <summary>
    /// 지도 위치 변경 이벤트 핸들러
    /// </summary>
    private void MainMap_OnCurrentPositionChanged(PointLatLng point)
    {
        // Position은 GMapControl 내부에서 이미 변경된 후 이벤트가 발화되므로 재설정 불필요.
        // RefreshVisibleTiles는 드래그 중에도 호출해야 함:
        //   → OverlayMapCanvas 타일은 절대 픽셀 좌표(Canvas.SetLeft/Top)로 배치되므로
        //     매 프레임 FromLatLngToLocal 재계산으로 갱신하지 않으면 base 타일과 어긋남.
        //   → TriggerSelectionChange 체인은 GMapCustomControl_OnPositionChanged에서 이미 차단됨.
        //_log?.Info($"[PAN][VM] RefreshVisibleTiles drag={MainMap?.IsDragging} t={DateTime.Now:HH:mm:ss.fff}");
        _customMapOverlayService?.RefreshVisibleTiles(MainMap);
        RefreshCameraPopupPositions();   // 카메라 팝업 Geo 추종(팬)
    }

    /// <summary>
    /// 마우스 이동 이벤트 핸들러 - 좌표 표시 업데이트
    /// </summary>
    private void MainMap_MouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(MainMap);
        var current = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);
        CurrentCoordinatePosition = new CoordinateModel(current.Lat, current.Lng, 0.0);

        // 위경도 → MGRS 변환
        var coordinate = new Coordinate(current.Lat, current.Lng);
        CurrentMGRS = coordinate.MGRS.ToString(); // "52S CG 13084 42135"
        CurrentUTM = coordinate.UTM.ToString();
    }

    /// <summary>
    /// 마우스 클릭 이벤트 핸들러 - 클릭 위치 저장 및 편집 모드 처리
    /// </summary>
    private void MainMap_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(MainMap);
        ClickedCurrentPosition = MainMap.FromLocalToLatLng((int)p.X, (int)p.Y);

        // 디버깅 로그 추가
        //_log?.Info($"마우스 클릭: 화면좌표({p.X:F2}, {p.Y:F2}) -> 지리좌표({ClickedCurrentPosition.Lat:F6}, {ClickedCurrentPosition.Lng:F6})");
    }

    /// <summary>
    /// 줌 변경 이벤트 핸들러 - 스케일바 업데이트
    /// </summary>
    /// <summary>
    /// 맵 타일 최초 로드 완료 시 오버레이 렌더링 (1회성)
    /// </summary>
    private void OnFirstTileLoadForOverlay(long elapsedMilliseconds)
    {
        MainMap.OnTileLoadComplete -= OnFirstTileLoadForOverlay;
        _log?.Info($"[Overlay] OnTileLoadComplete 수신 — 초기 렌더링 실행");
        _customMapOverlayService?.RefreshVisibleTiles(MainMap);
    }

    private void MainMap_OnMapZoomChanged()
    {
        // ★ FR-10: 실제 타일 줌이 바뀌면(맵 전환/홈 이동/줌아웃 등) 디지털 줌 초기화.
        //   디지털 줌은 _core.Zoom을 바꾸지 않으므로 디지털 인/아웃 자체로는 이 핸들러가 호출되지 않는다.
        MainMap?.ResetDigitalZoom();

        CreateScaleBar();
        ClearAllSelections();
        ReapplyLayerVisibilityForZoom();
        RefreshCameraPopupPositions();   // 카메라 팝업 Geo 추종(줌)

        // 줌 변경 시 스테일 타일 로딩 즉시 취소 + 새 CTS 교체 → 즉시 갱신
        if (_customMapOverlayService != null)
        {
            var newZoom = (int)MainMap.Zoom;
            foreach (var state in _customMapOverlayService.ActiveOverlays.Values)
            {
                state.ActiveLoadCts?.Cancel();
                state.ActiveLoadCts?.Dispose();
                state.ActiveLoadCts = new System.Threading.CancellationTokenSource();
                state.CurrentRenderZoom = newZoom;
            }
            // debounce 없이 즉시 갱신 — CTS 취소가 연속 줌의 중간 로드를 자동 차단함
            _customMapOverlayService.RefreshVisibleTiles(MainMap);
        }
    }

    /// <summary>
    /// 맵 컨트롤 크기 변경 이벤트 핸들러 (전체화면 전환, 윈도우 리사이즈)
    /// 뷰포트 확장 시 새 영역의 OverlayMap 타일 로드 트리거
    /// </summary>
    private void MainMap_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _customMapOverlayService?.RefreshVisibleTiles(MainMap);
        RefreshCameraPopupPositions();   // 리사이즈 시 ScaleTransform 중심(W/2,H/2) 변동 → 팝업 outer 재계산
    }

    /// <summary>
    /// 줌 변경 시 모든 레이어의 Visibility를 재평가.
    /// AND 조건: layerON && (currentZoom >= marker.Zoom)
    /// </summary>
    private void ReapplyLayerVisibilityForZoom()
    {
        if (_layerTreeNodes == null || _layerTreeNodes.Count == 0) return;

        foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes))
        {
            if (leaf.Model != null)
                ApplyLayerVisibility(leaf.Model);
        }
    }
    #endregion

    #region - UI 업데이트 및 유틸리티 -
    /// <summary>
    /// 줌 버튼 클릭 핸들러 - 줌 인
    /// </summary>
    public void OnClickZoomUp(object sender, EventArgs args)
    {
        if (ZoomMax > MainMap.Zoom)
            MainMap.Zoom++;
    }

    /// <summary>
    /// 줌 버튼 클릭 핸들러 - 줌 아웃
    /// </summary>
    public void OnClickZoomDown(object sender, EventArgs args)
    {
        if (ZoomMin < MainMap.Zoom)
            MainMap.Zoom--;
    }

    /// <summary>
    /// 스케일바 생성
    /// </summary>
    private void CreateScaleBar()
    {
        (var scaleX, var scale) = ScaleHelper.RelativeCreateScalebar(Zoom);

        // ★ C2/FR-12: 디지털 줌 시 바 픽셀 폭은 고정하고 거리 라벨 숫자만 ÷배율(같은 픽셀폭이 더 짧은 거리 표현).
        //   (이전 FR-9의 scaleX *= digScale 바 확대 방식 대체 — 사용자 피드백)
        double digScale = MainMap?.DigitalZoomScale ?? 1.0;
        scale = ScaleHelper.AdjustScaleLabel(scale, digScale);

        Scale = scale;
        ScalePoints = new PointCollection()
        {
            new Point(0.0, 0.0),
            new Point(0.0, 5.0),
            new Point(scaleX, 5.0),
            new Point(scaleX, 0.0),
        };
        NotifyOfPropertyChange(() => ScalePoints);
    }

    /// <summary>디지털 줌 레벨 변경 → 축척바 재계산 + 카메라 팝업 outer 재배치.
    /// (디지털줌은 _core.Zoom 불변이라 OnMapZoomChanged가 미발화 → 여기서 직접 Refresh, RC-2)</summary>
    private void OnMapDigitalZoomLevelChanged(int level)
    {
        CreateScaleBar();
        RefreshCameraPopupPositions();   // 디지털 인/아웃 시 팝업·연결선 outer 좌표 재계산
    }

    /// <summary>
    /// 객체 위치 검색 - 첫 번째 마커 위치로 이동
    /// </summary>
    public void SearchObjectPosition()
    {
        try
        {
            if (MainMap.Markers == null || !(MainMap.Markers.Count() > 0))
                return;

            MainMap.Position = (MainMap.Markers.FirstOrDefault() ?? throw new NullReferenceException($"GMap의 Markers Collection에 인스턴스가 하나도 없습니다.")).Position;
        }
        catch (NullReferenceException ex)
        {
            _log?.Error(ex.Message);
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
    }
    #endregion

    #region - 데이터 로딩 및 저장 -
    /// <summary>
    /// 캐시된 지도 데이터 비동기 로드
    /// </summary>
    public Task<bool> GetMapDataAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                Thread.Sleep(3000);
                DirectoryInfo di = new DirectoryInfo(System.Environment.CurrentDirectory);
                var dirs = di.GetDirectories();
                foreach (var folder in dirs)
                {
                    var folderName = "maps";
                    if (folder.Name.ToLower() == folderName)
                    {
                        _log?.Info($"Find the folder({folderName}) successfully!");
                        var fileName = "map.gmdb";
                        var file = folder.GetFiles().Where(t => t.Name == fileName).FirstOrDefault();
                        bool ret = false;
                        if (file != null)
                        {
                            _log?.Info($"Find the file({fileName}) successfully!");
                            ret = GMap.NET.GMaps.Instance.ImportFromGMDB(file.FullName);
                        }

                        if (ret)
                        {
                            _log?.Info($"Reload Map from cashed data : {file?.Name}");
                            MainMap.ReloadMap();
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _log?.Error($"Rasied Exception in {nameof(GetMapDataAsync)} :  {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// 현재 지도 설정을 JSON에 저장
    /// </summary>
    public async Task SaveCurrentMapSettingsAsync()
    {
        try
        {
            await MapSettingsHelper.SaveMapSettingsAsync(_setupModel);
            _log?.Info("현재 지도 설정 저장 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 설정 저장 실패: {ex.Message}");
        }
    }

    #endregion

    #region - 헬퍼 메서드 -
    
    /// <summary>
    /// 이미지 경계에서 지리참조 옵션 생성
    /// </summary>
    private TifProcessingOptions CreateGeoOptionsFromImageBounds(RectLatLng bounds, string mapName)
    {
        return new TifProcessingOptions
        {
            UseManualCoordinates = true,

            // 이미지 경계의 4개 모서리 좌표 사용
            ManualMinLatitude = bounds.LocationRightBottom.Lat,  // 남쪽 (하단)
            ManualMaxLatitude = bounds.LocationTopLeft.Lat,      // 북쪽 (상단)
            ManualMinLongitude = bounds.LocationTopLeft.Lng,     // 서쪽 (좌측)
            ManualMaxLongitude = bounds.LocationRightBottom.Lng, // 동쪽 (우측)

            MinZoom = 10,  // 적절한 최소 줌 레벨
            MaxZoom = 19,  // 적절한 최대 줌 레벨
            TileSize = 256 // 표준 타일 크기
        };
    }

    /// <summary>
    /// 커스텀 지도 생성 확인 대화상자
    /// </summary>
    private async Task<bool> ShowCustomMapConfirmationAsync(GMapCustomImage image, TifProcessingOptions options)
    {
        // TODO: 실제 UI 확인 대화상자 구현
        // 예시 정보를 로그로 표시
        _log?.Info("=== 커스텀 지도 생성 정보 ===");
        _log?.Info($"이미지: {image.Title}");
        _log?.Info($"좌표 범위:");
        _log?.Info($"  위도: {options.ManualMinLatitude:F6} ~ {options.ManualMaxLatitude:F6}");
        _log?.Info($"  경도: {options.ManualMinLongitude:F6} ~ {options.ManualMaxLongitude:F6}");
        _log?.Info($"줌 레벨: {options.MinZoom} ~ {options.MaxZoom}");

        // TODO: 실제 UI 구현 시 사용자 확인 받기
        // 지금은 자동으로 true 반환
        await Task.Delay(100); // UI 대화상자 시뮬레이션
        return true;
    }

    /// <summary>
    /// 진행률 리포터 생성
    /// </summary>
    private IProgress<TileConversionProgress> CreateProgressReporter()
    {
        return new Progress<TileConversionProgress>(progress =>
        {
            // 10% 단위로 로그 출력
            if (progress.ProgressPercentage % 10 < 0.1)
            {
                _log?.Info($"타일 생성 진행률: {progress.ProgressPercentage:F1}% " +
                          $"({progress.ProcessedTiles:N0}/{progress.TotalTiles:N0}) - {progress.Status}");
            }

            // TODO: UI 진행률 표시 (ProgressBar 등)
            // Application.Current?.Dispatcher?.Invoke(() => {
            //     // progressBar.Value = progress.ProgressPercentage;
            //     // statusText.Text = progress.Status;
            // });
        });
    }

    /// <summary>
    /// 생성된 커스텀 지도 적용 (삭제필요 - 사용되지 않는 메서드)
    /// </summary>
    private async Task ApplyGeneratedCustomMap(CustomMapModel customMap, GMapCustomImage originalImage)
    {
        try
        {
            _log?.Info("생성된 커스텀 지도를 지도에 적용 중...");

            // 1. 커스텀 맵 활성화
            var provider = _customMapService.ActivateCustomMap(customMap);

            // 2. 지도 전환
            await SwitchToCustomMapAsync(customMap);

            // 3. 지도 중심을 원본 이미지 위치로 이동
            var bounds = originalImage.ImageBounds;
            var centerLat = (bounds.LocationTopLeft.Lat + bounds.LocationRightBottom.Lat) / 2;
            var centerLng = (bounds.LocationTopLeft.Lng + bounds.LocationRightBottom.Lng) / 2;

            MainMap.Position = new PointLatLng(centerLat, centerLng);
            MainMap.Zoom = 15; // 적절한 줌 레벨

            _log?.Info(" 커스텀 지도 적용 완료!");
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 지도 적용 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 원본 이미지 제거 확인 (삭제필요 - 사용되지 않는 메서드)
    /// </summary>
    private async Task<bool> AskRemoveOriginalImageAsync()
    {
        // TODO: 실제 UI 확인 대화상자 구현
        await Task.Delay(100);
        return true; // 기본적으로 제거
    }

    /// <summary>
    /// 라인 드로잉 상태 텍스트 업데이트
    /// </summary>
    private void UpdateLineDrawingStatus()
    {
        if (_lineDrawingService == null) return;

        var pointCount = _lineDrawingService.PointCount;
        var distance = _lineDrawingService.TotalDistance;

        if (pointCount == 0)
        {
            LineDrawingStatus = "첫 번째 포인트를 클릭하세요";
        }
        else if (pointCount == 1)
        {
            LineDrawingStatus = "두 번째 포인트를 클릭하세요";
        }
        else
        {
            LineDrawingStatus = $"포인트: {pointCount}개, 거리: {distance:F1}m (ESC: 완료, Backspace: 취소)";
        }
    }

    /// <summary>
    /// 라인 명령 상태 업데이트
    /// </summary>
    private void UpdateCommandStates()
    {
        (CompleteLineDrawingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (CancelLineDrawingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (UndoLastPointCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
    #endregion

    #region -DB Related Logic 모음 -
    private async Task<int> DbSaveProcess(IEditableMarker marker)
    {
        switch (marker)
        {
            case GMapCustomMarker customMarker:
                // GMapCustomMarker 전용 로직
                return await _gMapDbSymbolService.InsertSymbolAsync(customMarker.Model);
            case GMapGeometricMarker geometricMarker:
                // GMapGeometricMarker 전용 로직
                return await _gMapDbSymbolService.InsertGeometrySymbolAsync(geometricMarker.Model);
            case GMapPidsMarker pidsMarker:
                // GMapPidsMarker 전용 로직
                return await _gMapDbSymbolService.InsertPidsSymbolAsync(pidsMarker.Model);
            case GMapMilitarySymbolMarker militaryMarker:
                // GMapMilitarySymbolMarker 전용 로직
                return await _gMapDbSymbolService.InsertMilitarySymbolAsync(militaryMarker.Model);
            case GMapLineMarker lineMarker:
                // GMapLineMarker 전용 로직
                return await _gMapDbSymbolService.InsertLineSymbolAsync(lineMarker.Model);
            case GMapInfraMarker infraMarker:
                // GMapInfraMarker 전용 로직
                return await _gMapDbSymbolService.InsertInfraSymbolAsync(infraMarker.Model);
            case GMapPidsGroupMarker pidsGroupMarker:
                // GMapPidsGroupMarker 전용 로직
                return await _gMapDbSymbolService.InsertPidsGroupSymbolAsync(pidsGroupMarker.Model);
            case GMapImageMarker imageMarker:
                // GMapImageMarker 전용 로직 (Phase 28)
                return await _gMapDbSymbolService.InsertImageAsync(imageMarker.ImageModel);
            default:
                // 공통 로직
                return 0;
        }
    }

    private async Task DbUpdateProcess(IEditableMarker marker)
    {
        switch (marker)
        {
            case GMapCustomMarker customMarker:
                // GMapCustomMarker 전용 로직
                await _gMapDbSymbolService.UpdateSymbolAsync(customMarker.Model);
                break;
            case GMapGeometricMarker geometricMarker:
                // GMapGeometricMarker 전용 로직
                await _gMapDbSymbolService.UpdateGeometrySymbolAsync(geometricMarker.Model);
                break;
            case GMapPidsMarker pidsMarker:
                // GMapPidsMarker 전용 로직
                await _gMapDbSymbolService.UpdatePidsSymbolAsync(pidsMarker.Model);
                break;
            case GMapMilitarySymbolMarker militaryMarker:
                // GMapMilitarySymbolMarker 전용 로직
                await _gMapDbSymbolService.UpdateMilitarySymbolAsync(militaryMarker.Model);
                break;
            case GMapLineMarker lineMarker:
                // GMapLineMarker 전용 로직
                await _gMapDbSymbolService.UpdateLineSymbolAsync(lineMarker.Model);
                break;
            case GMapInfraMarker infraMarker:
                // GMapInfraMarker 전용 로직
                await _gMapDbSymbolService.UpdateInfraSymbolAsync(infraMarker.Model);
                break;
            case GMapPidsGroupMarker pidsGroupMarker:
                // GMapPidsGroupMarker 전용 로직
                await _gMapDbSymbolService.UpdatePidsGroupSymbolAsync(pidsGroupMarker.Model);
                break;
            case GMapImageMarker imageMarker:
                //_log?.Info($"[DEBUG-DBUPDATE] ImageModel Id={imageMarker.ImageModel.Id}, W={imageMarker.ImageModel.Width},H={imageMarker.ImageModel.Height}, Bounds=L:{imageMarker.ImageModel.Left:F6},T:{imageMarker.ImageModel.Top:F6},R:{imageMarker.ImageModel.Right:F6},B:{imageMarker.ImageModel.Bottom:F6}");
                await _gMapDbSymbolService.UpdateImageAsync(imageMarker.ImageModel);
                break;
            default:
                // 공통 로직
                break;
        }
    }

    private async Task<bool> DbDeleteProcess(IEditableMarker marker)
    {
        switch (marker)
        {
            case GMapCustomMarker customMarker:
                // GMapCustomMarker 전용 로직
                return await _gMapDbSymbolService.DeleteSymbolAsync(customMarker.Model);
            case GMapGeometricMarker geometricMarker:
                // GMapGeometricMarker 전용 로직
                return await _gMapDbSymbolService.DeleteGeometrySymbolAsync(geometricMarker.Model);
            case GMapPidsMarker pidsMarker:
                // GMapPidsMarker 전용 로직
                return await _gMapDbSymbolService.DeletePidsSymbolAsync(pidsMarker.Model);
            case GMapMilitarySymbolMarker militaryMarker:
                // GMapMilitarySymbolMarker 전용 로직
                return await _gMapDbSymbolService.DeleteMilitarySymbolAsync(militaryMarker.Model);
            case GMapLineMarker lineMarker:
                // GMapLineMarker 전용 로직
                return await _gMapDbSymbolService.DeleteLineSymbolAsync(lineMarker.Model);
            case GMapInfraMarker infraMarker:
                // GMapInfraMarker 전용 로직
                return await _gMapDbSymbolService.DeleteInfraSymbolAsync(infraMarker.Model);
            case GMapPidsGroupMarker pidsGroupMarker:
                // GMapPidsGroupMarker 전용 로직
                return await _gMapDbSymbolService.DeletePidsGroupSymbolAsync(pidsGroupMarker.Model);
            case GMapImageMarker imageMarker:
                var imgDeleted = await _gMapDbSymbolService.DeleteImageAsync(imageMarker.ImageModel.Id);
                // 복사된 이미지 파일 정리
                if (imgDeleted && !string.IsNullOrEmpty(imageMarker.FilePath))
                {
                    _imageFileService.DeleteLocalImage(imageMarker.FilePath);
                }
                // MapLayers OverlayImage 레코드도 함께 삭제
                if (imgDeleted && !string.IsNullOrEmpty(imageMarker.FilePath))
                {
                    try
                    {
                        var layers = await _gMapDbService.FetchMapLayersAsync();
                        var layer = layers?.FirstOrDefault(l =>
                            l.LayerType == "OverlayImage" &&
                            string.Equals(l.FilePath, imageMarker.FilePath, StringComparison.OrdinalIgnoreCase));
                        if (layer != null)
                        {
                            await _gMapDbService.DeleteMapLayerAsync(layer.Id);
                            await LoadLayersFromDbAsync();
                            _log?.Info($"[OverlayImage] MapLayers 삭제: {layer.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _log?.Error($"[OverlayImage] MapLayers 삭제 실패: {ex.Message}");
                    }
                }
                return imgDeleted;
            default:
                // 공통 로직
                return false;
        }
    }
    #endregion

    #region - 줌 관련 속성 -
    /// <summary>
    /// 현재 줌 레벨
    /// </summary>
    public double Zoom
    {
        get { return MainMap.Zoom; }
        set
        {
            MainMap.Zoom = value;
            NotifyOfPropertyChange(nameof(Zoom));
        }
    }

    /// <summary>
    /// 최대 줌 레벨
    /// </summary>
    public int ZoomMax
    {
        get { return MainMap.MaxZoom; }
        set
        {
            MainMap.MaxZoom = value;
            MainMap?.ResetDigitalZoom();   // ★ FR-10: MaxZoom 변동(provider/맵 전환) 시 디지털 줌 초기화
            NotifyOfPropertyChange(nameof(ZoomMax));
        }
    }

    /// <summary>
    /// 최소 줌 레벨
    /// </summary>
    public int ZoomMin
    {
        get { return MainMap.MinZoom; }
        set
        {
            MainMap.MinZoom = value;
            NotifyOfPropertyChange(nameof(ZoomMin));
        }
    }
    #endregion

    #region - 좌표 및 위치 관련 속성 -
    /// <summary>
    /// 현재 마우스 커서 위치의 좌표
    /// </summary>
    public ICoordinateModel CurrentCoordinatePosition
    {
        get { return _currentPosition; }
        set
        {
            _currentPosition = value;
            NotifyOfPropertyChange(nameof(CurrentCoordinatePosition));
        }
    }

    /// <summary>
    /// 현재 위치의 MGRS 좌표
    /// </summary>
    public string? CurrentMGRS
    {
        get { return _currentMGRS; }
        set { _currentMGRS = value; NotifyOfPropertyChange(nameof(CurrentMGRS)); }
    }

    /// <summary>
    /// 현재 위치의 UTM 좌표
    /// </summary>
    public string? CurrentUTM
    {
        get { return _currentUTM; }
        set { _currentUTM = value; NotifyOfPropertyChange(nameof(CurrentUTM)); }
    }

    /// <summary>
    /// 현재 좌표를 PointLatLng로 반환
    /// </summary>
    public PointLatLng CurrentPointPosition => new PointLatLng(_currentPosition.Latitude, _currentPosition.Longitude);

    /// <summary>
    /// 마지막으로 클릭된 위치
    /// </summary>
    public PointLatLng ClickedCurrentPosition { get; set; }

    /// <summary>
    /// 홈 위치 정보
    /// </summary>
    public HomePositionModel? HomePosition { get; set; }
    #endregion

    #region - 표시 옵션 관련 속성 -
    /// <summary>
    /// WGS84 좌표계 표시 여부
    /// </summary>
    public bool IsShowWSG84
    {
        get { return _isShowWSG84; }
        set { _isShowWSG84 = value; NotifyOfPropertyChange(nameof(IsShowWSG84)); }
    }

    /// <summary>
    /// MGRS 좌표계 표시 여부
    /// </summary>
    public bool IsShowMGRS
    {
        get { return _isShowMGRS; }
        set { _isShowMGRS = value;
            NotifyOfPropertyChange(nameof(IsShowMGRS)); }
    }

    /// <summary>
    /// MGRS 그리드 표시 여부
    /// </summary>
    public bool IsShowMGRSGrid
    {
        get { return _isShowMGRSGrid; }
        set { _isShowMGRSGrid = value; NotifyOfPropertyChange(nameof(IsShowMGRSGrid)); }
    }

    /// <summary>
    /// 격자 스냅 활성화 여부
    /// </summary>
    public bool IsSnapToGridEnabled
    {
        get => _isSnapToGridEnabled;
        set { _isSnapToGridEnabled = value; NotifyOfPropertyChange(nameof(IsSnapToGridEnabled)); }
    }

    /// <summary>
    /// 격자 크기(px) — 슬라이더 범위 4~50
    /// </summary>
    public double GridSizePx
    {
        get => _gridSizePx;
        set
        {
            _gridSizePx = Math.Max(4, Math.Min(50, value));
            NotifyOfPropertyChange(nameof(GridSizePx));
        }
    }

    /// <summary>
    /// UTM 좌표계 표시 여부
    /// </summary>
    public bool IsShowUTM
    {
        get { return _isShowUTM; }
        set { _isShowUTM = value; NotifyOfPropertyChange(nameof(IsShowUTM)); }
    }

    /// <summary>
    /// 스케일바 텍스트
    /// </summary>
    public string? Scale
    {
        get { return _scale; }
        set
        {
            _scale = value;
            NotifyOfPropertyChange(nameof(Scale));
        }
    }

    /// <summary>
    /// 스케일바 그리기 점들
    /// </summary>
    public PointCollection? ScalePoints { get; set; }
    #endregion

    #region - 편집 모드 관련 속성 -
    /// <summary>
    /// 편집 모드 활성화 여부
    /// </summary>
    public bool IsEditModeEnabled
    {
        get => _isEditModeEnabled;
        set
        {
            if (_isEditModeEnabled != value)
            {
                _isEditModeEnabled = value;
                MainMap.SetEditMode(value);

                // 편집 모드 해제 시 모든 선택 해제 + 배치 모드 취소(#4)
                if (!value)
                {
                    ClearAllSelections();
                    ExitSymbolPlacementMode();
                }

                NotifyOfPropertyChange(nameof(IsEditModeEnabled));
                NotifyOfPropertyChange(nameof(CanEditMarker));
                NotifyOfPropertyChange(nameof(CanAddSymbol));   // 추가 버튼 활성 갱신(#3)
                RaiseUndoRedoState();   // Undo/Redo 버튼 갱신(C#2) — CanUndo/CanRedo가 IsEditModeEnabled 포함
                if (PropertyPanel != null)
                    PropertyPanel.IsEditModeEnabled = value;
                _log?.Info($"편집 모드: {(value ? "활성화" : "비활성화")}");
            }
        }
    }

    /// <summary>
    /// 다중 선택 모드 활성화 여부
    /// </summary>
    public bool IsMultiSelectEnabled
    {
        get => _isMultiSelectEnabled;
        set
        {
            _isMultiSelectEnabled = value;
            NotifyOfPropertyChange(nameof(IsMultiSelectEnabled));
        }
    }

    /// <summary>
    /// 마커 편집 중인지 여부
    /// </summary>
    public bool IsMarkerEditing
    {
        get => _isMarkerEditing;
        set
        {
            _isMarkerEditing = value;
            NotifyOfPropertyChange(nameof(IsMarkerEditing));
        }
    }

    /// <summary>
    /// 현재 활성 Adorner 개수
    /// </summary>
    public int AdornerCount
    {
        get => _adornerCount;
        set
        {
            _adornerCount = value;
            NotifyOfPropertyChange(nameof(AdornerCount));
            NotifyOfPropertyChange(nameof(HasActiveAdorners));
        }
    }

    /// <summary>
    /// 활성 Adorner가 있는지 여부
    /// </summary>
    public bool HasActiveAdorners => AdornerCount > 0;

    /// <summary>
    /// 선택된 이미지
    /// </summary>
    public GMapCustomImage? SelectedImage
    {
        get => _selectedImage;
        set
        {
            _selectedImage = value;
            NotifyOfPropertyChange(nameof(SelectedImage));
            NotifyOfPropertyChange(nameof(HasSelectedItem));
            NotifyOfPropertyChange(nameof(IsEditModeEnabled));
        }
    }

    /// <summary>
    /// 선택된 마커
    /// </summary>
    public IEditableMarker? SelectedMarker
    {
        get => _selectedMarker;
        set
        {

            _selectedMarker = value;
            NotifyOfPropertyChange(nameof(SelectedMarker));
            NotifyOfPropertyChange(nameof(HasSelectedItem));
            NotifyOfPropertyChange(nameof(IsEditModeEnabled));
        }
    }
    /// <summary>
    /// 선택된 항목이 있는지 여부
    /// </summary>
    public bool HasSelectedItem => (SelectedImage != null || SelectedMarker != null || (_groupSelection?.HasSelection ?? false)) && IsEditModeEnabled;
    #endregion

    #region - 회전 관련 속성 -
    /// <summary>
    /// 현재 회전 각도
    /// </summary>
    public double CurrentRotation
    {
        get => _currentRotation;
        set
        {
            _currentRotation = value;
            NotifyOfPropertyChange(nameof(CurrentRotation));
            NotifyOfPropertyChange(nameof(IsRotated));
        }
    }

    /// <summary>
    /// 지도 회전 각도
    /// </summary>
    public double MapRotation
    {
        get => _mapRotation;
        set
        {
            if (Math.Abs(_mapRotation - value) > 0.01) // 미세한 변화 무시
            {
                _mapRotation = value;

                // MainMap에 적용
                if (MainMap != null)
                {
                    MainMap.MapRotation = value;
                }

                CurrentRotation = value;
                NotifyOfPropertyChange(nameof(MapRotation));
            }
        }
    }

    /// <summary>
    /// 회전 스냅 각도
    /// </summary>
    public double RotationSnapAngle
    {
        get => _rotationSnapAngle;
        set
        {
            if (Math.Abs(_rotationSnapAngle - value) > 0.01)
            {
                _rotationSnapAngle = value;

                if (MainMap != null)
                {
                    MainMap.RotationSnapAngle = value;
                }

                NotifyOfPropertyChange(nameof(RotationSnapAngle));
            }
        }
    }

    /// <summary>
    /// 회전 컨트롤 표시 여부
    /// </summary>
    public bool ShowRotationControl
    {
        get => _showRotationControl;
        set
        {
            if (_showRotationControl != value)
            {
                _showRotationControl = value;

                if (MainMap != null)
                {
                    MainMap.ShowRotationControl = value;
                }

                NotifyOfPropertyChange(nameof(ShowRotationControl));
            }
        }
    }

    /// <summary>
    /// 회전 상태 여부
    /// </summary>
    public bool IsRotated => Math.Abs(CurrentRotation) > 0.1;
    #endregion

    #region - 선택된 마커 편집 속성 -
    /// <summary>
    /// 마커 편집 가능 여부
    /// </summary>
    public bool CanEditMarker => SelectedMarker != null && IsEditModeEnabled;
    #endregion

    #region - 컨트롤 및 서비스 참조 -
    /// <summary>
    /// 메인 지도 컨트롤
    /// </summary>
    public GMapCustomControl? MainMap { get; private set; }

    /// <summary>
    /// 현재 선택된 지도 모델
    /// </summary>
    public IMapModel? SelectedMap { get; private set; }

    /// <summary>
    /// 현재 활성화된 커스텀 맵 Provider
    /// </summary>
    public FileBasedCustomMapProvider? CurrentCustomMapProvider { get; private set; }
    #endregion

    #region - 명령어 속성 -
    // 파일 관련 명령어
    public RelayCommand? LoadMapImageCommand { get; private set; }
    public RelayCommand? LoadImageOverlayCommand { get; private set; }
    public RelayCommand? CreateCustomMapCommand { get; private set; }
    public RelayCommand? ExitApplicationCommand { get; private set; }

    // 지도 표시 관련 명령어
    public RelayCommand? ToggleWGS84Command { get; private set; }
    public RelayCommand? ToggleMGRSCommand { get; private set; }
    public RelayCommand? ToggleUTMCommand { get; private set; }
    public RelayCommand? ToggleSnapToGridCommand { get; private set; }

    // 네비게이션 관련 명령어
    public RelayCommand? MoveHomeLocationCommand { get; private set; }
    public RelayCommand? SetHomeLocationCommand { get; private set; }
    public RelayCommand? ShowMapRoiPanelCommand { get; private set; }
    public RelayCommand? ZoomInCommand { get; private set; }
    public RelayCommand? ZoomOutCommand { get; private set; }

    // 편집 관련 명령어
    public RelayCommand? ClearSelectionCommand { get; private set; }
    public RelayCommand? DeleteSelectedCommand { get; private set; }

    // 회전 관련 명령어
    public RelayCommand? RotateCommand { get; private set; }
    public RelayCommand? FineRotateCommand { get; private set; }
    public RelayCommand? ResetRotationCommand { get; private set; }
    public RelayCommand? AlignToMGRSCommand { get; private set; } // TODO: 구현 필요

    // 마커 편집 관련 명령어
    public RelayCommand? AddSelectedSymbolCommand { get; private set; }
    public RelayCommand? DuplicateMarkerCommand { get; private set; }
    public RelayCommand? SnapMarkerToGridCommand { get; private set; }
    public RelayCommand? ResetMarkerRotationCommand { get; private set; }
    public RelayCommand? ResetMarkerSizeCommand { get; private set; }

    public RelayCommand? ToggleEditModeCommand { get; private set; }
    public RelayCommand? ToggleMultiSelectCommand { get; private set; }
    public RelayCommand? CancelAllEditingCommand { get; private set; }
    public RelayCommand? LogAdornerStatsCommand { get; private set; }

    /// <summary>
    /// 군사 심볼 등록창 열기 명령어
    /// </summary>
    public RelayCommand? ShowMilitarySymbolRegisterCommand { get; private set; }


    /// <summary>
    /// 라인 드로잉 관련 명령어들
    /// </summary>
    public AsyncRelayCommand? StartLineDrawingCommand { get; private set; }
    public AsyncRelayCommand? CancelLineDrawingCommand { get; private set; }
    public AsyncRelayCommand? CompleteLineDrawingCommand { get; private set; }
    public RelayCommand? UndoLastPointCommand { get; private set; }
    #endregion

    #region Property Panel Methods
    private void ShowPropertyPanel()
    {
        //_log?.Info($"ShowPropertyPanel 시작 - SelectedMarker: {SelectedMarker?.Title}");

        if (SelectedMarker == null) return;

        // Property Panel 생성 전후로 마커 상태 로그
        //_log?.Info($"Property Panel 생성 전 - {GetMarkerInfo(SelectedMarker)}");

       
        // 기존 패널 정리
        HidePropertyPanel();

        PropertyPanel = _propertyPanelFactory.CreatePropertyPanel(SelectedMarker);

        if(PropertyPanel is GMapPropertyPidsControl pidsControlPanel)
        {
            _log?.Info($"PropertyPanel의 {pidsControlPanel?.LinkedDevice?.DeviceName}");
        }
        if (PropertyPanel != null)
        {
            // 이벤트 구독 추가
            PropertyPanel.CloseRequested += OnPropertyPanelCloseRequested;
            PropertyPanel.MarkerPropertyChanged += OnMarkerPropertyChanged;
            PropertyPanel.ZOrderChangeRequested += OnPropertyPanelZOrderChangeRequested;
            PropertyPanel.DeviceLocationApplyRequested += OnDeviceLocationApplyRequested;   // 현재위치 적용(Symbol_Apply_DeviceLocation)

            // 공통 속성 설정
            PropertyPanel.AvailableColors = AvailableColors;
            PropertyPanel.AvailableSizes = AvailableSize;
            PropertyPanel.IsDraggable = true;
            PropertyPanel.IsEditModeEnabled = IsEditModeEnabled;

            IsPropertyPanelVisible = true;
            RefreshPropertyPanelZOrder();
            //_log?.Info($"PropertyPanel 생성 완료: {PropertyPanel.GetType().Name}");
        }

        //_log?.Info($"Property Panel 생성 후 - {GetMarkerInfo(SelectedMarker)}");
        IsPropertyPanelVisible = true;
    }

    // ─────────── 기능 ②: 멀티셀렉트 공통 속성창 + 전체 반영 + 배치 Undo ───────────

    /// <summary>그룹 선택(≥2)이면 최소 공통 타입 속성창 표시. 변경은 OnMarkerPropertyChanged가 전체 반영. 1개 이하면 미표시. (기능 ②)</summary>
    private void ShowGroupPropertyPanelIfNeeded()
    {
        var sel = _groupSelection?.Selection;
        if (sel == null || sel.Count < 2) return;   // 단일/없음 → 그룹 공통창 미표시(단일 경로는 별개)
        var rep = sel.FirstOrDefault(m => m != null && !m.IsDisposed);
        if (rep == null) return;

        HidePropertyPanel();
        PropertyPanel = _propertyPanelFactory.CreateCommonPropertyPanel(sel);
        if (PropertyPanel == null) return;

        PropertyPanel.CloseRequested += OnPropertyPanelCloseRequested;
        PropertyPanel.MarkerPropertyChanged += OnMarkerPropertyChanged;
        PropertyPanel.ZOrderChangeRequested += OnPropertyPanelZOrderChangeRequested;
        PropertyPanel.AvailableColors = AvailableColors;
        PropertyPanel.AvailableSizes = AvailableSize;
        PropertyPanel.IsDraggable = true;
        PropertyPanel.IsEditModeEnabled = IsEditModeEnabled;
        IsPropertyPanelVisible = true;
        RefreshPropertyPanelZOrder();
        _log?.Info($"그룹 공통 속성창 표시 — {sel.Count}개, {PropertyPanel.GetType().Name}");
    }

    /// <summary>그룹 공통 속성 변경을 선택된 전 마커에 적용 + 1개 매크로 Undo. 대표는 패널이 이미 적용(DB+record만),
    /// 나머지는 ApplyProperty로 적용 후 record. (기능 ②-2)</summary>
    private async System.Threading.Tasks.Task ApplyGroupPropertyChangeAsync(
        System.Collections.Generic.IReadOnlyList<GMapSymbols.IEditableMarker> group, MarkerPropertyChangedEventArgs e)
    {
        var targets = group.Where(m => m != null && !m.IsDisposed && !m.IsLocked).ToList();
        if (targets.Count == 0) return;

        // Pending sentinel 방어 — 정상 경로에선 미발생(패널 초기화 중 이벤트 억제)이나 값 오염 백스톱.
        if (e.NewValue is double nd && double.IsNaN(nd)) return;
        if (e.NewValue is Ironwall.Dotnet.Libraries.Enums.EnumColorType nc && (int)nc < 0) return;

        bool titleChanged = false;
        using (_editRecorder?.BeginBatch($"그룹 속성 변경: {e.PropertyName} ({targets.Count}개)"))
        {
            foreach (var m in targets)
            {
                // 패널은 그룹 모드에서 대표에도 직접쓰기 안 함(직접쓰기 억제) → 전원 동일 경로:
                //   before=마커 실값(ReadProperty) → 적용(ApplyProperty) → DB → 배치 record. Pending(빈칸) 필드도 undo 무결.
                var before = Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.UndoableCommandBase.ReadProperty(m, e.PropertyName);
                Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.UndoableCommandBase.ApplyProperty(m, e.PropertyName, e.NewValue);
                try { await DbUpdateProcess(m); } catch (System.Exception ex) { _log?.Error($"그룹 속성 영속 실패: {ex.Message}"); }
                _editRecorder?.RecordPropertyChange(m, e.PropertyName, before, e.NewValue);   // 배치 합류(1 매크로)
                if (e.PropertyName == "Title") titleChanged = true;
                if (e.PropertyName is "Zoom" or "IsLayerEnabled") MainMap?.RefreshMarkerVisibility(m);
            }
        }
        if (titleChanged) _ = LoadLayersFromDbAsync();   // 레이어 트리 이름 동기(1회)
        _groupSelection?.RefreshAdorner();
        MainMap?.InvalidateVisual();
    }

    private async void OnMarkerPropertyChanged(object? sender, MarkerPropertyChangedEventArgs e)
    {
        if (IsApplyingUndo) return;   // Undo/Redo 재적용 중 바인딩 에코 → 중복 DbUpdate/트리리로드/이미지싱크 방지(FIX 2)
        if (IsEditModeEnabled && !_isMarkerEditing)
        {
            // 맵편집 권한 게이트(RBAC, 2안) — 디바이스 연결(LinkedDevice) 포함 속성 영속은 map:edit 필요.
            // 조용한 실패(연결된 듯 보이나 미저장 → 재시작 시 원복) 대신 명시 팝업 피드백.
            // (OPERATOR는 편집모드 진입 자체가 게이팅돼 여기 도달 전 차단됨 — 이 가드는 세션 중 역할강등 등 백스톱)
            if (!CanEditMap()) { _log?.Warning($"[RBAC] 맵 편집 권한 없음 — 속성 저장 차단: {e.PropertyName}"); ShowNoMapEditPermissionInfo(); return; }
            _log?.Info($"속성창 변경에 의한 마커 속성 변경: {e.PropertyName} = {e.NewValue}");

            // 그룹 선택(≥2) + 공통 속성창 변경 → 선택된 전 마커에 반영 + 배치 Undo(1 매크로). (기능 ②-2)
            var groupSelForProp = _groupSelection?.Selection;
            if (groupSelForProp != null && groupSelForProp.Count >= 2
                && Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.UndoableCommandBase.IsReplayableProperty(e.PropertyName))
            {
                await ApplyGroupPropertyChangeAsync(groupSelForProp, e);
                return;
            }

            await DbUpdateProcess(e.Marker);
            _editRecorder?.RecordPropertyChange(e.Marker, e.PropertyName, e.OldValue, e.NewValue);   // Undo 기록(coalescing)
            // "Visibility"는 IsReplayableProperty 미포함이라 RecordPropertyChange가 드롭 → 전용 VisibilityCommand로
            //   라우팅해 속성창 가시성 변경도 Undo 가능하게(D2). (레이어 트리 체크박스와 동일 경로 재사용)
            if (e.PropertyName == "Visibility" && e.OldValue is bool visBefore && e.NewValue is bool visAfter && visBefore != visAfter)
                _editRecorder?.RecordVisibility(e.Marker, visBefore, visAfter);

            // 심볼 Title(이름) 변경 → 레이어 트리 노드 이름 동기화(맵→패널). marker.Model이 _symbolProvider
            // 인스턴스와 공유되므로 리빌드가 새 이름을 반영(감사 P1). 이미지는 아래 SyncOverlayImageLayer가 처리.
            if (e.PropertyName == "Title" && e.Marker is not GMapSymbols.GMapImageMarker)
                _ = LoadLayersFromDbAsync();

            // LinkedDevice 변경 → _deviceSymbolLookup 즉시 재등록 (AllDevicesLoadedMessage 대기 불필요)
            if (e.PropertyName == "LinkedDevice"
                && e.Marker is GMapPidsMarker pidsMarker
                && pidsMarker.LinkedDevice != null)
            {
                _symbolEventManager.RegisterDeviceSymbol(pidsMarker.LinkedDevice, pidsMarker.Model);
            }

            // OverlayImage Title/Opacity/Visibility 변경 → MapLayers 동기화
            if (e.Marker is GMapSymbols.GMapImageMarker imgMarker
                && (e.PropertyName == "Title" || e.PropertyName == "Opacity" || e.PropertyName == "Visibility"))
            {
                await SyncOverlayImageLayer(imgMarker.FilePath, e.PropertyName, e.NewValue);
            }

            // 최소 줌(Zoom)·레이어토글(IsLayerEnabled) 변경 → 편집 마커의 유효 가시성 즉시 재계산 + 리렌더.
            // (기존 결함: 캐시된 IsVisible는 UpdateMarkersVisibilityByZoom(OnAreaChange=팬/줌)에서만 갱신 →
            //  최소줌 편집이 다음 팬/줌 전까지 미반영. 심볼·DB이미지마커 공통. 술어=SetMarkerVisibility 단일원천)
            if ((e.PropertyName == "Zoom" || e.PropertyName == "IsLayerEnabled")
                && e.Marker is GMapSymbols.IEditableMarker zoomMarker)
            {
                MainMap?.RefreshMarkerVisibility(zoomMarker);
            }
        }
    }

    private async Task SyncOverlayImageLayer(string? filePath, string propertyName, object? newValue)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (_isSyncingRename && propertyName == "Title") return;
        try
        {
            var layers = await _gMapDbService.FetchMapLayersAsync();
            var layer = layers?.FirstOrDefault(l =>
                l.LayerType == "OverlayImage" &&
                string.Equals(l.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (layer == null) return;

            switch (propertyName)
            {
                case "Title":
                    layer.Name = newValue?.ToString() ?? layer.Name;
                    break;
                case "Opacity":
                    if (newValue is double opacity) layer.Opacity = opacity;
                    break;
                case "Visibility":
                    if (newValue is bool visible) layer.IsVisible = visible;
                    break;
            }

            await _gMapDbService.UpdateMapLayerAsync(layer);
            await LoadLayersFromDbAsync();
        }
        catch (Exception ex)
        {
            _log?.Error($"[OverlayImage] MapLayers 동기화 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// MainMap.Markers에서 FilePath로 GMapImageMarker 검색
    /// (이미지는 CustomImages가 아닌 Markers 컬렉션에 저장됨)
    /// </summary>
    private GMapSymbols.GMapImageMarker? FindImageMarkerByFilePath(string filePath)
    {
        static string Normalize(string? p)
        {
            if (string.IsNullOrEmpty(p)) return string.Empty;
            try { return System.IO.Path.GetFullPath(p).TrimEnd('\\', '/'); }
            catch { return p.Replace('/', '\\').TrimEnd('\\', '/'); }
        }
        var target = Normalize(filePath);
        return MainMap?.Markers
            .OfType<GMapSymbols.GMapImageMarker>()
            .FirstOrDefault(m => string.Equals(Normalize(m.FilePath), target,
                StringComparison.OrdinalIgnoreCase));
    }

    private void OnPropertyPanelCloseRequested(object? sender, EventArgs e)
    {
        ClearAllSelections();
        HidePropertyPanel();
    }

    private void OnPropertyPanelZOrderChangeRequested(object? sender, ZOrderChangeRequestedEventArgs e)
    {
        if (SelectedMarker == null || MainMap == null) return;
        var zBefore = IsApplyingUndo ? null : CaptureZOrderPairs();   // V4 — 단일 순서변경 undo 기록(라이브 패널 경로)
        switch (e.Direction)
        {
            case ZOrderDirection.Up:       MoveMarkerUp(SelectedMarker);       break;
            case ZOrderDirection.Down:     MoveMarkerDown(SelectedMarker);     break;
            case ZOrderDirection.ToTop:    MoveMarkerToTop(SelectedMarker);    break;
            case ZOrderDirection.ToBottom: MoveMarkerToBottom(SelectedMarker); break;
        }
        RefreshPropertyPanelZOrder();
        if (zBefore != null) RecordZOrderDiff(zBefore);
    }

    private void HidePropertyPanel()
    {
        if (PropertyPanel != null)
        {
            // 이벤트 구독 해제
            PropertyPanel.CloseRequested -= OnPropertyPanelCloseRequested;
            PropertyPanel.MarkerPropertyChanged -= OnMarkerPropertyChanged;
            PropertyPanel.ZOrderChangeRequested -= OnPropertyPanelZOrderChangeRequested;
            PropertyPanel.DeviceLocationApplyRequested -= OnDeviceLocationApplyRequested;

            // 바인딩 정리
            PropertyPanel.ClearAllBindings();
            PropertyPanel = null;
        }

        IsPropertyPanelVisible = false;
    }

    //public Task HandleAsync(PropertyPanelCloseRequestedEvent message, CancellationToken cancellationToken)
    //{
    //    ClearAllSelections();
    //    HidePropertyPanel();
        
    //    return Task.CompletedTask;
    //}

    //public async Task HandleAsync(MarkerPropertyChangedEventArgs message, CancellationToken cancellationToken)
    //{
    //    if (IsEditModeEnabled && !_isMarkerEditing)
    //    {
    //        _log?.Info($"속성창 변경에 의한 마커 속성 변경: {message.PropertyName} = {message.NewValue}");
    //        // DB 업데이트
    //        await DbUpdateProcess(message.Marker);
    //    }
    //}

    private void UpdateMarkerControlProperty(IEditableMarker marker, string propertyName, object value)
    {
        if (marker is GMapMarker gMapMarker && gMapMarker.Shape is GMapMarkerCustomControl control)
        {
            switch (propertyName)
            {
                case nameof(marker.Width):
                    control.Width = (double)value;
                    break;
                case nameof(marker.Height):
                    control.Height = (double)value;
                    break;
                case nameof(marker.Title):
                    control.MarkerTitle = (string)value;
                    break;
            }
        }
    }

    public static string GetMarkerInfo(IEditableMarker marker)
    {
        if (marker == null) return "Marker: null";

        return $"Id:{marker.Id}, Title:'{marker.Title}', " +
               $"Position:({marker.Position.Lat:F6},{marker.Position.Lng:F6}), " +
               $"Size:({marker.Width:F1}x{marker.Height:F1}), " +
               $"Zoom:{marker.Zoom:F1}, Bearing:{marker.Bearing:F1}, " +
               $"Selected:{marker.IsSelected}, ShowShape:{marker.ShowShape}, ShowTitle:{marker.ShowTitle}, " +
               $"Fill:{marker.FillColor}, Stroke:{marker.StrokeColor}, StrokeThickness:{marker.StrokeThickness:F1}, " +
               $"State:{marker.OperationState}";
    }
    #endregion
    #region - 지도 변경 메서드 -
    /// <summary>
    /// 지도 변경 (ComboBox 선택 시)
    /// </summary>
    private async Task ChangeMapAsync(IMapModel targetMap)
    {
        if (targetMap == null) return;

        // Race condition 방지: 이미 전환 중이면 스킵
        if (!await _mapSwitchLock.WaitAsync(0))
        {
            _log?.Info($"지도 변경 스킵 (전환 진행 중): {targetMap.Name}");
            return;
        }

        ClearUndoStack();   // 맵 전환 → Undo 스택 비움(외부 상태 충돌 방지, FR-12)

        try
        {
            _log?.Info($"지도 변경 요청: {SelectedMap?.Name} → {targetMap.Name}");

            // 편집 모드 해제
            if (IsEditModeEnabled)
            {
                IsEditModeEnabled = false;
            }

            // setupModel 업데이트
            _setupModel.MapName = targetMap.Name;
            _setupModel.MapType = targetMap.ProviderType.ToString();

            await MapConfigureAsync();

            // UI 업데이트
            NotifyOfPropertyChange(nameof(SelectedMapItem));

            // 설정 저장
            await SaveCurrentMapSettingsAsync();

            _log?.Info($"지도 변경 완료: {targetMap.Name}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지도 변경 실패: {ex.Message}");
            NotifyOfPropertyChange(nameof(SelectedMapItem));
        }
        finally
        {
            _mapSwitchLock.Release();
        }
    }
    #endregion
    #region - 심볼 추가 관련 속성 -

    /// <summary>
    /// 사용 가능한 마커 카테고리 목록
    /// </summary>
    public EnumMarkerCategory[] AvailableMarkerCategories =>
        System.Enum.GetValues<EnumMarkerCategory>();

    /// <summary>
    /// 선택된 마커 카테고리
    /// </summary>
    public EnumMarkerCategory SelectedMarkerCategory
    {
        get => _selectedMarkerCategory;
        set
        {
            if (_selectedMarkerCategory != value)
            {
                _selectedMarkerCategory = value;
                NotifyOfPropertyChange(nameof(SelectedMarkerCategory));

                // 카테고리 변경 시 사용 가능한 심볼 타입 업데이트
                NotifyOfPropertyChange(nameof(AvailableSymbolTypes));

                // 첫 번째 타입으로 자동 선택
                if (AvailableSymbolTypes.Any())
                {
                    SelectedSymbolType = AvailableSymbolTypes.First();
                }
            }
        }
    }

    /// <summary>
    /// 선택된 카테고리에 따른 사용 가능한 심볼 타입들
    /// </summary>
    public IEnumerable<object> AvailableSymbolTypes
    {
        get
        {
            return SelectedMarkerCategory switch
            {
                EnumMarkerCategory.BASIC_SHAPES => new[] { "Pin" },
                EnumMarkerCategory.GEOMETRICS => System.Enum.GetValues<EnumShapeType>().Cast<object>(),
                EnumMarkerCategory.VEHICLES => new[] { "Car" },
                EnumMarkerCategory.MILITARY_SYMBOLS => new[] { "Register" },
                EnumMarkerCategory.PIDS_EQUIPMENT => new[] {"Controller","Multi", "Fence", "IpCamera", "SmartSensor", "IpSpeaker", "Fence_Group" },
                EnumMarkerCategory.AREA_BOUNDARY => new[] { "Area","Line" },
                EnumMarkerCategory.INFRASTRUCTURE => new[] { "Factory" },
                _ => Array.Empty<object>()
            };
        }
    }

    /// <summary>
    /// 심볼 추가 가능 여부
    /// </summary>
    public bool CanAddSymbol => SelectedSymbolType != null && IsEditModeEnabled;   // 편집모드에서만 추가(#3)
    public EnumColorType[] AvailableColors => ColorHelper.GetCommonColors();
    public double[] AvailableSize => Enumerable.Range(1, 20).Select(i => i * 0.5).ToArray();

    /// <summary>
    /// 선택된 심볼 타입
    /// </summary>
    public object SelectedSymbolType
    {
        get => _selectedSymbolType;
        set
        {
            _selectedSymbolType = value;
            NotifyOfPropertyChange(nameof(SelectedSymbolType));
            NotifyOfPropertyChange(nameof(CanAddSymbol));
        }
    }

    

    public bool IsPropertyPanelVisible
    {
        get => _isPropertyPanelVisible;
        set
        {
            _isPropertyPanelVisible = value;
            NotifyOfPropertyChange(nameof(IsPropertyPanelVisible));
        }
    }

    public GMapPropertyBaseControl? PropertyPanel
    {
        get => _propertyPanel;
        set
        {
            _propertyPanel = value;
            NotifyOfPropertyChange(nameof(PropertyPanel));
        }
    }

    public DeviceProvider DeviceProvider { get; }

    /// <summary>
    /// 군사 심볼 등록창 표시 여부
    /// </summary>
    public bool IsMilitarySymbolRegisterVisible
    {
        get => _isMilitarySymbolRegisterVisible;
        set
        {
            _isMilitarySymbolRegisterVisible = value;
            NotifyOfPropertyChange(nameof(IsMilitarySymbolRegisterVisible));
        }
    }

    /// <summary>
    /// 군사 심볼 등록 패널
    /// </summary>
    public GMapMilitarySymbolRegisterControl? MilitarySymbolRegisterPanel
    {
        get => _militarySymbolRegisterPanel;
        set
        {
            _militarySymbolRegisterPanel = value;
            NotifyOfPropertyChange(nameof(MilitarySymbolRegisterPanel));
        }
    }


    #endregion
    

    #region Line Drawing Properties

    /// <summary>
    /// 라인 드로잉 중 여부
    /// </summary>
    public bool IsLineDrawing
    {
        get => _isLineDrawing;
        set
        {
            _isLineDrawing = value;
            NotifyOfPropertyChange(nameof(IsLineDrawing));
        }
    }

    /// <summary>
    /// 라인 드로잉 상태 텍스트
    /// </summary>
    public string LineDrawingStatus
    {
        get => _lineDrawingStatus;
        set
        {
            _lineDrawingStatus = value;
            NotifyOfPropertyChange(nameof(LineDrawingStatus));
        }
    }

    #endregion
    #region Line Drawing Fields

    private LineDrawingService _lineDrawingService;
    private bool _isLineDrawing;
    private string _lineDrawingStatus;

    #endregion
    #region - 지도 선택 관련 속성 -
    /// <summary>
    /// 사용 가능한 지도 목록
    /// </summary>
    public IEnumerable<IMapModel> AvailableMaps => _mapProvider
        .Where(m => m is not CustomMapModel)
        .Where(m => m.Category == EnumMapCategory.Standard || m.Category == EnumMapCategory.Satellite);

    /// <summary>
    /// ComboBox에서 선택된 지도
    /// </summary>
    public IMapModel? SelectedMapItem
    {
        get => SelectedMap;
        set
        {
            if (value != null && value != SelectedMap)
            {
                _ = ChangeMapAsync(value);
            }
        }
    }
    #endregion
    #region - 필드 (Private Fields) -
    // 맵 전환 동시 실행 방지
    private readonly SemaphoreSlim _mapSwitchLock = new(1, 1);

    // 서비스 및 의존성
    private CancellationTokenSource _cts;
    private MapProvider _mapProvider;
    private DefinedMapProvider _definedMapProvider;
    private IGMapDbSymbolService _gMapDbSymbolService;
    private SymbolProvider _symbolProvider;
    private GMapSetupModel _setupModel;
    private CustomMapService _customMapService;
    private CustomMapOverlayService _customMapOverlayService;
    private ImageOverlayService _imageOverlayService;
    private IImageFileService _imageFileService;
    private readonly TrackingOverlayManager? _trackingOverlay;   // Tracking GIS 오버레이(FR-15)
    private readonly PlaybackViewModel? _playbackVm;             // Tracking Playback(P5) 콘솔 VM
    private GMapControls.PlaybackConsoleControl? _playbackPanel;
    private bool _isPlaybackPanelVisible;
    private readonly TrackingSetupViewModel? _trackingSetupVm;   // 추적 설정 패널 VM(P3-04)
    private GMapControls.TrackingSettingsControl? _trackingSettingsPanel;
    private bool _isTrackingSettingsPanelVisible;
    private MarkerFactory _markerFactory;

    private PropertyPanelFactory _propertyPanelFactory;
    private GMapPropertyBaseControl? _propertyPanel;
    //private GMapPropertyCustomControl? _customPropertyPanel;
    private bool _isPropertyPanelVisible;
    private SymbolEventManager _symbolEventManager;
    private IDeviceDetailUrlService _deviceDetailUrlService;
    private IBroadcastControlService _broadcastControlService;
    private readonly Dictionary<int, CancellationTokenSource> _broadcastTimers = new();

    // 카메라 "특정 위치 확인" 타겟 조준 모드 (Camera_PTZ_AimLocation)
    private readonly ICameraAimControlService _cameraAimControlService;
    private readonly ITrackingSetupModel _trackingSetupModel;
    private ICameraDeviceModel? _aimCamera;      // 현재 타겟 모드 대상 카메라(UI 스레드 전용)
    private double _aimRadiusMeters;             // 진입 시 반경 스냅샷(m)
    private int _aimGeneration;                  // stale-await 가드(취소 시 ++)
    private PointLatLng _aimCenter;              // 진입 시 중심 스냅샷(=심볼 위치). 원/히트테스트/메시지 모두 동일 중심 사용(중심 불일치 버그 방지)
    private System.Windows.Window? _aimEscWindow; // ESC 취소용 윈도우 레벨 후킹(맵 포커스 상실 무관)

    // UI 상태 필드
    private string? _scale;
    private ICoordinateModel _currentPosition = new CoordinateModel(37.648425, 126.904284);
    private string? _currentMGRS;
    private string? _currentUTM;
    private bool _isEditModeEnabled = false;

    // 표시 옵션 필드
    private bool _isShowWSG84 = true;
    private bool _isShowMGRS;
    private bool _isShowMGRSGrid;
    private bool _isShowUTM;
    private bool _isSnapToGridEnabled;
    private double _gridSizePx = 32.0;

    // 회전 관련 필드
    private double _currentRotation;
    private double _mapRotation;
    private double _rotationSnapAngle;
    private bool _showRotationControl = false;

    // Layer 가시성 적용 재진입 차단 (ApplyLayerVisibility/AggregateLeafCheckedFromMarkers 공유)
    private bool _isApplyingLayerVisibility;

    // Adorner관련 필드
    private bool _isMultiSelectEnabled = false;
    private bool _isMarkerEditing = false;
    private int _adornerCount = 0;

    // 선택 상태 필드
    private GMapCustomImage? _selectedImage;
    private IEditableMarker? _selectedMarker;

    // 심볼 선택 관련 필드
    private EnumMarkerCategory _selectedMarkerCategory = EnumMarkerCategory.GEOMETRICS;
    private object _selectedSymbolType = EnumShapeType.Circle;

    // 필드 추가
    private bool _isMilitarySymbolRegisterVisible;
    private GMapMilitarySymbolRegisterControl? _militarySymbolRegisterPanel;

    // ROI 관련 필드
    private IGMapDbService _gMapDbService;
    private bool _isMapRoiPanelVisible;
    private MapRoiControl? _mapRoiPanel;
    private ObservableCollection<IMapRoiModel> _roiItems = new();

    // 오버레이 맵 등록 패널
    private bool _isMapRegistrationPanelVisible;
    private MapRegistrationControl? _mapRegistrationPanel;

    // 지도 선택 관련 필드

    #endregion

    #region - 오버레이 맵 등록 Properties -

    public bool IsMapRegistrationPanelVisible
    {
        get => _isMapRegistrationPanelVisible;
        set
        {
            _isMapRegistrationPanelVisible = value;
            NotifyOfPropertyChange(nameof(IsMapRegistrationPanelVisible));
        }
    }

    public MapRegistrationControl? MapRegistrationPanel
    {
        get => _mapRegistrationPanel;
        set
        {
            _mapRegistrationPanel = value;
            NotifyOfPropertyChange(nameof(MapRegistrationPanel));
        }
    }

    #endregion

    #region - ROI 관심지역 Properties -

    public bool IsMapRoiPanelVisible
    {
        get => _isMapRoiPanelVisible;
        set
        {
            _isMapRoiPanelVisible = value;
            NotifyOfPropertyChange(nameof(IsMapRoiPanelVisible));
        }
    }

    public MapRoiControl? MapRoiPanel
    {
        get => _mapRoiPanel;
        set
        {
            _mapRoiPanel = value;
            NotifyOfPropertyChange(nameof(MapRoiPanel));
        }
    }

    #endregion

    #region - ROI 관심지역 Methods -

    public void ShowMapRoiPanel()
    {
        if (IsMapRoiPanelVisible) return;

        _log?.Info("관심지역 패널 표시");

        HideMapRoiPanel();

        MapRoiPanel = new MapRoiControl { Opacity = 0 };
        MapRoiPanel.RoiItems = _roiItems;
        MapRoiPanel.MoveRequested += OnRoiMoveRequested;
        MapRoiPanel.RegisterRequested += OnRoiRegisterRequested;
        MapRoiPanel.DeleteRequested += OnRoiDeleteRequested;
        MapRoiPanel.CloseRequested += OnRoiCloseRequested;
        MapRoiPanel.TitleEdited += OnRoiTitleEdited;

        IsMapRoiPanelVisible = true;

        // 위치 먼저 잡은 후 Opacity 1로 표시 (점프 방지)
        MapRoiPanel.Loaded += async (s, e) =>
        {
            await LoadMapRoisAsync();
            MapRoiPanel?.CenterInCanvas();
            if (MapRoiPanel != null) MapRoiPanel.Opacity = 1;
        };
    }

    public void HideMapRoiPanel()
    {
        if (MapRoiPanel != null)
        {
            MapRoiPanel.MoveRequested -= OnRoiMoveRequested;
            MapRoiPanel.RegisterRequested -= OnRoiRegisterRequested;
            MapRoiPanel.DeleteRequested -= OnRoiDeleteRequested;
            MapRoiPanel.CloseRequested -= OnRoiCloseRequested;
            MapRoiPanel.TitleEdited -= OnRoiTitleEdited;
            MapRoiPanel = null;
        }

        IsMapRoiPanelVisible = false;
    }

    private void OnRoiMoveRequested(object? sender, MapRoiEventArgs e)
    {
        _log?.Info($"관심지역 이동: {e.Roi.Title} → ({e.Roi.Latitude}, {e.Roi.Longitude}), Zoom={e.Roi.Zoom}");
        MainMap!.Position = new PointLatLng(e.Roi.Latitude, e.Roi.Longitude);
        MainMap.Zoom = e.Roi.Zoom;
    }

    private async void OnRoiRegisterRequested(object? sender, EventArgs e)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 관심지역 등록 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            var position = MainMap!.Position;
            var zoom = (int)MainMap.Zoom;

            // 간단한 Title 입력 (InputBox)
            var title = $"관심지역_{DateTime.Now:HHmmss}";

            var roi = new MapRoiModel
            {
                Title = title,
                Latitude = position.Lat,
                Longitude = position.Lng,
                Altitude = 0,
                Zoom = zoom,
                MapId = SelectedMap?.Id ?? 1
            };

            int id = await _gMapDbService.InsertMapRoiAsync(roi);
            roi.Id = id;
            _roiItems.Add(roi);

            _log?.Info($"관심지역 등록 완료: {title} (Id={id})");
        }
        catch (Exception ex)
        {
            _log?.Error($"관심지역 등록 실패: {ex.Message}");
        }
    }

    private async void OnRoiDeleteRequested(object? sender, MapRoiEventArgs e)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 관심지역 삭제 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingDeleteRoiId = e.Roi.Id;

            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "관심지역 삭제",
                Explain = $"'{e.Roi.Title}' 관심지역을 삭제하시겠습니까?",
                MessageModel = new CallDeleteMapRoiProcessMessageModel()
            });
        }
        catch (Exception ex)
        {
            _log?.Error($"관심지역 삭제 요청 실패: {ex.Message}");
        }
    }

    private void OnRoiCloseRequested(object? sender, EventArgs e)
    {
        _log?.Info("관심지역 패널 닫기");
        HideMapRoiPanel();
    }

    private async void OnRoiTitleEdited(object? sender, MapRoiTitleEditedEventArgs e)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 관심지역 이름 변경 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            bool updated = await _gMapDbService.UpdateMapRoiTitleAsync(e.Roi.Id, e.NewTitle);
            if (updated)
                _log?.Info($"관심지역 이름 변경 완료: Id={e.Roi.Id}, '{e.NewTitle}'");
        }
        catch (Exception ex)
        {
            _log?.Error($"관심지역 이름 변경 실패: {ex.Message}");
        }
    }

    public async Task LoadMapRoisAsync()
    {
        try
        {
            var mapId = SelectedMap?.Id ?? 1;
            var list = await _gMapDbService.FetchMapRoisAsync(mapId);
            _roiItems.Clear();
            if (list != null)
            {
                foreach (var roi in list)
                    _roiItems.Add(roi);
            }
            _log?.Info($"관심지역 {_roiItems.Count}건 로드 완료 (MapId={mapId})");
        }
        catch (Exception ex)
        {
            _log?.Error($"관심지역 로드 실패: {ex.Message}");
        }
    }

    private int _pendingDeleteRoiId;
    private IMapLayerModel? _pendingDeleteLayer;

    #endregion

    #region - Broadcast Panel Properties & Methods -

    private BroadcastPlayControl? _broadcastPlayPanel;
    private bool _isBroadcastPlayPanelVisible;
    private TtsBroadcastControl? _ttsBroadcastPanel;
    private bool _isTtsBroadcastPanelVisible;

    public BroadcastPlayControl? BroadcastPlayPanel
    {
        get => _broadcastPlayPanel;
        set { _broadcastPlayPanel = value; NotifyOfPropertyChange(nameof(BroadcastPlayPanel)); }
    }

    public bool IsBroadcastPlayPanelVisible
    {
        get => _isBroadcastPlayPanelVisible;
        set { _isBroadcastPlayPanelVisible = value; NotifyOfPropertyChange(nameof(IsBroadcastPlayPanelVisible)); }
    }

    public TtsBroadcastControl? TtsBroadcastPanel
    {
        get => _ttsBroadcastPanel;
        set { _ttsBroadcastPanel = value; NotifyOfPropertyChange(nameof(TtsBroadcastPanel)); }
    }

    public bool IsTtsBroadcastPanelVisible
    {
        get => _isTtsBroadcastPanelVisible;
        set { _isTtsBroadcastPanelVisible = value; NotifyOfPropertyChange(nameof(IsTtsBroadcastPanelVisible)); }
    }

    public void ShowBroadcastPlayPanel(int linkedDeviceId)
    {
        HideBroadcastPlayPanel();

        BroadcastPlayPanel = new BroadcastPlayControl { LinkedDeviceId = linkedDeviceId };
        BroadcastPlayPanel.SendRequested += OnBroadcastPlaySendRequested;
        BroadcastPlayPanel.CancelRequested += (s, e) => HideBroadcastPlayPanel();
        IsBroadcastPlayPanelVisible = true;
        BroadcastPlayPanel.Loaded += (s, e) => BroadcastPlayPanel?.CenterInCanvas();
    }

    public void HideBroadcastPlayPanel()
    {
        if (BroadcastPlayPanel != null)
        {
            BroadcastPlayPanel.SendRequested -= OnBroadcastPlaySendRequested;
            BroadcastPlayPanel = null;
        }
        IsBroadcastPlayPanelVisible = false;
    }

    private async void OnBroadcastPlaySendRequested(object? sender, BroadcastSendEventArgs e)
    {
        if (!CanBroadcast()) { _log?.Warning("[FR-EN-07] 방송 발행 권한 없음 — 음원 실행 차단"); return; }
        try
        {
            await _broadcastControlService.PublishPlayAsync(e.LinkedDeviceId, e.FileGroupId, e.Repeat);
            _log?.Info($"음원 실행: DeviceId={e.LinkedDeviceId}, FileGroup={e.FileGroupId}, Repeat={e.Repeat}");
            HideBroadcastPlayPanel();
        }
        catch (Exception ex)
        {
            _log?.Error($"음원 실행 실패: {ex.Message}");
        }
    }

    public void ShowTtsBroadcastPanel(int linkedDeviceId)
    {
        HideTtsBroadcastPanel();

        TtsBroadcastPanel = new TtsBroadcastControl { LinkedDeviceId = linkedDeviceId };
        TtsBroadcastPanel.SendRequested += OnTtsSendRequested;
        TtsBroadcastPanel.CancelRequested += (s, e) => HideTtsBroadcastPanel();
        IsTtsBroadcastPanelVisible = true;
        TtsBroadcastPanel.Loaded += (s, e) => TtsBroadcastPanel?.CenterInCanvas();
    }

    public void HideTtsBroadcastPanel()
    {
        if (TtsBroadcastPanel != null)
        {
            TtsBroadcastPanel.SendRequested -= OnTtsSendRequested;
            TtsBroadcastPanel = null;
        }
        IsTtsBroadcastPanelVisible = false;
    }

    private async void OnTtsSendRequested(object? sender, TtsSendEventArgs e)
    {
        if (!CanBroadcast()) { _log?.Warning("[FR-EN-07] 방송 발행 권한 없음 — TTS 실행 차단"); return; }
        try
        {
            await _broadcastControlService.PublishTtsAsync(e.LinkedDeviceId, e.Message);
            _log?.Info($"TTS 실행: DeviceId={e.LinkedDeviceId}, Message={e.Message}");
            HideTtsBroadcastPanel();
        }
        catch (Exception ex)
        {
            _log?.Error($"TTS 실행 실패: {ex.Message}");
        }
    }

    #endregion

    #region - ROI IHandle -

    public async Task HandleAsync(CallDeleteMapRoiProcessMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken); //내가 추가
            await Task.Delay(500, cancellationToken);//내가 추가

            if (_pendingDeleteRoiId <= 0) return;

            bool deleted = await _gMapDbService.DeleteMapRoiAsync(_pendingDeleteRoiId);
            if (deleted)
            {
                var target = _roiItems.FirstOrDefault(r => r.Id == _pendingDeleteRoiId);
                if (target != null)
                    _roiItems.Remove(target);

                _log?.Info($"관심지역 삭제 완료 (Id={_pendingDeleteRoiId})");
            }

            _pendingDeleteRoiId = 0;

            await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Error($"관심지역 삭제 실패: {ex.Message}");
            await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    #endregion

    #region - Layer Delete IHandle -

    public async Task HandleAsync(CallDeleteMapLayerProcessMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);
            await Task.Delay(500, cancellationToken);

            var layer = _pendingDeleteLayer;
            if (layer == null)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
                return;
            }

            _log?.Info($"[레이어 삭제] 시작: {layer.Name} (LayerType={layer.LayerType}, Id={layer.Id})");

            switch (layer.LayerType)
            {
                case "OverlayMap":
                    if (layer.MapId.HasValue)
                    {
                        _customMapOverlayService.DeactivateOverlay(layer.MapId.Value);
                        await _customMapService.DeleteCustomMapAsync(layer.MapId.Value, deleteFiles: true);
                    }
                    break;

                case "OverlayImage":
                    if (!string.IsNullOrEmpty(layer.FilePath))
                    {
                        var marker = FindImageMarkerByFilePath(layer.FilePath);
                        if (marker != null)
                        {
                            MainMap?.DeselectMarker(marker);
                            MainMap?.Markers?.Remove(marker);
                            await _gMapDbSymbolService.DeleteImageAsync(marker.ImageModel.Id);
                        }
                    }
                    break;
            }

            await _gMapDbService.DeleteMapLayerAsync(layer.Id);

            if (layer.LayerType == "OverlayMap")
                _customMapOverlayService?.RefreshVisibleTiles(MainMap);
            MainMap?.InvalidateVisual();

            await LoadLayersFromDbAsync();

            _pendingDeleteLayer = null;
            _log?.Info($"[레이어 삭제] 완료: {layer.Name}");

            await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
        catch (Exception ex)
        {
            _log?.Error($"레이어 삭제 실패: {ex.Message}");
            await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    #endregion

    #region - Layer Panel Properties & Methods -

    private LayerPanelControl? _layerPanel;
    private bool _isLayerPanelVisible;
    private ObservableCollection<LayerTreeNode> _layerTreeNodes = new();

    public LayerPanelControl? LayerPanel
    {
        get => _layerPanel;
        set { _layerPanel = value; NotifyOfPropertyChange(nameof(LayerPanel)); }
    }

    public bool IsLayerPanelVisible
    {
        get => _isLayerPanelVisible;
        set { _isLayerPanelVisible = value; NotifyOfPropertyChange(nameof(IsLayerPanelVisible)); }
    }

    public RelayCommand? ShowLayerPanelCommand { get; private set; }

    public void ShowLayerPanel()
    {
        if (IsLayerPanelVisible) return;

        HideLayerPanel();
        LayerPanel = new LayerPanelControl { TreeNodes = _layerTreeNodes, Opacity = 0 };
        LayerPanel.LayerVisibilityChanged += OnLayerVisibilityChanged;
        LayerPanel.LayerOpacityChanged += OnLayerOpacityChanged;
        LayerPanel.LayerDeleteRequested += OnLayerDeleteRequested;
        LayerPanel.LayerMoveUpRequested += OnLayerMoveUpRequested;
        LayerPanel.LayerMoveDownRequested += OnLayerMoveDownRequested;
        LayerPanel.LayerRenameRequested += OnLayerRenameRequested;
        LayerPanel.LayerNavigateRequested += OnLayerNavigateRequested;
        LayerPanel.SymbolVisibilityChanged += OnSymbolVisibilityChanged;   // FR-03 개별 심볼 토글
        LayerPanel.SymbolNavigateRequested += OnSymbolNavigateRequested;   // FR-04 개별 심볼 이동
        LayerPanel.SymbolRenameRequested += OnSymbolRenameRequested;       // 심볼 이름변경 싱크(FR-04)
        LayerPanel.SymbolLockChanged += OnSymbolLockChanged;               // 심볼 잠금(FR-03)
        LayerPanel.LayerLockChanged += OnLayerLockChanged;                 // 이미지 잠금(FR-03)
        LayerPanel.PanelSizeCommitted += OnPanelSizeCommitted;             // FR-05/06 리사이즈 크기(세션 기억)
        if (_lastPanelSize.HasValue)
            LayerPanel.SetPanelSize(_lastPanelSize.Value.Width, _lastPanelSize.Value.Height);
        LayerPanel.CloseRequested += (s, e) => HideLayerPanel();
        IsLayerPanelVisible = true;
        // 위치 먼저 잡은 후 Opacity 1로 표시 (점프 방지)
        LayerPanel.Loaded += async (s, e) =>
        {
            await LoadLayersFromDbAsync();
            LayerPanel?.CenterInCanvas();
            if (LayerPanel != null) LayerPanel.Opacity = 1;
        };
    }

    public void HideLayerPanel()
    {
        if (LayerPanel != null)
        {
            LayerPanel.LayerVisibilityChanged -= OnLayerVisibilityChanged;
            LayerPanel.LayerOpacityChanged -= OnLayerOpacityChanged;
            LayerPanel.LayerDeleteRequested -= OnLayerDeleteRequested;
            LayerPanel.LayerMoveUpRequested -= OnLayerMoveUpRequested;
            LayerPanel.LayerMoveDownRequested -= OnLayerMoveDownRequested;
            LayerPanel.LayerRenameRequested -= OnLayerRenameRequested;
            LayerPanel.LayerNavigateRequested -= OnLayerNavigateRequested;
            LayerPanel.SymbolVisibilityChanged -= OnSymbolVisibilityChanged;
            LayerPanel.SymbolNavigateRequested -= OnSymbolNavigateRequested;
            LayerPanel.SymbolRenameRequested -= OnSymbolRenameRequested;
            LayerPanel.SymbolLockChanged -= OnSymbolLockChanged;
            LayerPanel.LayerLockChanged -= OnLayerLockChanged;
            LayerPanel.PanelSizeCommitted -= OnPanelSizeCommitted;
            LayerPanel.UnsubscribeLeaves();   // leaf 구독·델리게이트 해제(닫힌 컨트롤 누수 방지)
            LayerPanel = null;
        }
        IsLayerPanelVisible = false;
    }

    #region - 개별 심볼 노드 핸들러 (FR-03/04/05) -

    private System.Windows.Size? _lastPanelSize;   // 세션 내 리사이즈 크기 기억(재오픈 복원). 세션 간 영속=v2.

    /// <summary>개별 심볼 가시성 토글 → 해당 마커(Id 일치)의 IsLayerEnabled/IsVisible/ShowShape 적용(런타임). DB 영속=v2.</summary>
    private void OnSymbolVisibilityChanged(object? sender, SymbolVisibilityChangedEventArgs e)
    {
        try
        {
            // Id는 Symbols 테이블 단일 PK(전 심볼 타입 공유·전역 유일) → marker.Id==symbol.Id 가 타입무관 유일 식별.
            var marker = MainMap?.Markers
                .OfType<GMapSymbols.IEditableMarker>()
                .FirstOrDefault(m => m.Id == e.Symbol.Id);
            if (marker == null) return;

            var beforeShow = marker.ShowShape;   // Undo용 이전 가시성(AREA 1)
            marker.IsLayerEnabled = e.IsVisible;
            marker.ShowShape = e.IsVisible;
            marker.IsVisible = e.IsVisible && MainMap!.Zoom >= marker.Zoom;   // 유효 가시성 = 토글 AND 줌
            MainMap?.InvalidateVisual();
            _editRecorder?.RecordVisibility(marker, beforeShow, e.IsVisible);   // Undo 기록(런타임 가시성, DB 미영속)
        }
        catch (Exception ex) { _log?.Error($"심볼 가시성 변경 실패: {ex.Message}"); }
    }

    /// <summary>개별 심볼 '중앙으로 이동' → 맵을 심볼 좌표로 팬(FR-04).</summary>
    private void OnSymbolNavigateRequested(object? sender, SymbolNavigateRequestedEventArgs e)
    {
        try
        {
            if (MainMap == null) return;
            MainMap.Position = new PointLatLng(e.Symbol.Latitude, e.Symbol.Longitude);
            _log?.Info($"심볼 이동: {e.Symbol.Title} ({e.Symbol.Latitude:F6},{e.Symbol.Longitude:F6})");
        }
        catch (Exception ex) { _log?.Error($"심볼 이동 실패: {ex.Message}"); }
    }

    /// <summary>개별 심볼 이름변경 → symbol.Title + DB 영속 + 마커/속성창 싱크(FR-04, Overlay Image 패턴).</summary>
    private async void OnSymbolRenameRequested(object? sender, SymbolRenameRequestedEventArgs e)
    {
        try
        {
            if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 심볼 이름 변경 차단"); ShowNoMapEditPermissionInfo(); return; }
            var newName = e.NewName?.Trim();
            if (string.IsNullOrEmpty(newName)) return;

            var oldName = e.Symbol.Title;   // Undo용 이전 이름
            e.Symbol.Title = newName;
            await _gMapDbSymbolService.UpdateSymbolAsync(e.Symbol);   // 공통 Symbols 행(Title) 영속 — 타입 무관

            // 타입인지 — 심볼 이름변경은 이미지가 아닌 마커만 대상. 같은 Id의 오버레이 이미지(제어기1↔안양발전소 Id=1 충돌)를
            // 잡아 이미지 Title/속성창을 심볼 이름으로 오염시키던 경로 차단.
            var marker = MainMap?.Markers
                .OfType<GMapSymbols.IEditableMarker>()
                .FirstOrDefault(m => m.Id == e.Symbol.Id && m is not GMapSymbols.GMapImageMarker);
            if (marker != null)
            {
                marker.Title = newName;
                if (PropertyPanel?.SelectedMarker == marker) PropertyPanel.MarkerTitle = newName;
                _editRecorder?.RecordRename(marker, oldName, newName);   // Undo 기록(트리 이름변경)
            }
            _log?.Info($"심볼 이름변경: Id={e.Symbol.Id} → {newName}");
        }
        catch (Exception ex) { _log?.Error($"심볼 이름변경 실패: {ex.Message}"); }
    }

    /// <summary>개별 심볼 잠금 토글 → 마커 IsLocked(즉시 클릭차단) + DB 영속(FR-03).</summary>
    private async void OnSymbolLockChanged(object? sender, SymbolLockChangedEventArgs e)
    {
        try
        {
            if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 심볼 잠금 변경 차단(UI 비활성 백스톱)"); return; }
            var marker = MainMap?.Markers
                .OfType<GMapSymbols.IEditableMarker>()
                .FirstOrDefault(m => m.Id == e.Symbol.Id);
            var oldLocked = marker?.IsLocked ?? e.Symbol.IsLocked;   // Undo용 이전 잠금상태
            if (marker != null) marker.IsLocked = e.IsLocked;

            e.Symbol.IsLocked = e.IsLocked;
            await _gMapDbSymbolService.UpdateSymbolAsync(e.Symbol);   // IsLocked 공통 Symbols 행 영속
            if (marker != null) _editRecorder?.RecordLock(marker, oldLocked, e.IsLocked);   // Undo 기록(트리 잠금)
            _log?.Info($"심볼 잠금 {(e.IsLocked ? "ON" : "OFF")}: {e.Symbol.Title}(Id={e.Symbol.Id})");
        }
        catch (Exception ex) { _log?.Error($"심볼 잠금 변경 실패: {ex.Message}"); }
    }

    /// <summary>Overlay 이미지 잠금 토글 → 이미지 마커 IsLocked + DB 영속(FR-03).</summary>
    private async void OnLayerLockChanged(object? sender, LayerLockChangedEventArgs e)
    {
        try
        {
            if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 이미지 잠금 변경 차단(UI 비활성 백스톱)"); return; }
            if (e.Layer.LayerType == "OverlayImage" && !string.IsNullOrEmpty(e.Layer.FilePath))
            {
                var marker = FindImageMarkerByFilePath(e.Layer.FilePath);
                if (marker != null)
                {
                    marker.IsLocked = e.IsLocked;
                    await _gMapDbSymbolService.UpdateImageAsync(marker.ImageModel);
                    // 트리에서 잠금 시 그룹 선택 점선박스 즉시 재렌더(ExecuteGroupLock과 달리 이 경로엔 누락됐던 갱신).
                    if (_groupSelection?.HasSelection ?? false) _groupSelection.RefreshAdorner();
                }
            }
            _log?.Info($"이미지 잠금 {(e.IsLocked ? "ON" : "OFF")}: {e.Layer.Name}");
        }
        catch (Exception ex) { _log?.Error($"이미지 잠금 변경 실패: {ex.Message}"); }
    }

    /// <summary>리사이즈 완료 크기를 세션 내 기억(재오픈 복원, FR-06). 세션 간 영속(MapSettings)=v2.</summary>
    private void OnPanelSizeCommitted(object? sender, System.Windows.Size e) => _lastPanelSize = e;

    #endregion

    private async Task LoadLayersFromDbAsync()
    {
        try
        {
            await _gMapDbService.SeedDefaultSymbolLayersAsync();
            var list = await _gMapDbService.FetchMapLayersAsync();

            // 리빌드 전 펼침(IsExpanded) 상태 캡처 — LayerTreeBuilder가 카테고리/그룹을 접힘으로 재생성하므로
            // 복원하지 않으면 추가/삭제/이름변경 때마다 사용자가 펼친 카테고리가 모두 접힘(UX 저하, 감사 C).
            var expandedNames = _layerTreeNodes == null
                ? new System.Collections.Generic.HashSet<string>()
                : LayerTreeBuilder.Flatten(_layerTreeNodes).Where(n => n.IsExpanded).Select(n => n.Name).ToHashSet();

            // 개별 심볼(_symbolProvider) 전달 → 카테고리 아래 개별 심볼 자식 노드 생성(FR-02)
            _layerTreeNodes = LayerTreeBuilder.Build(list ?? Enumerable.Empty<IMapLayerModel>(), _symbolProvider);

            // 오버레이 이미지 leaf 잠금 상태를 마커에서 초기화(H-2). 심볼 leaf는 CreateSymbolLeaf에서 처리됨.
            // InitIsLocked는 LockChanged를 발화하지 않아 DB 재기록/피드백 루프 없음.
            // 또한 맵 편집 권한을 각 leaf에 주입 → 권한 없으면 잠금 토글이 비활성(아이콘 변화·desync 차단, 사용자 피드백).
            var canEditMap = CanEditMap();
            foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes))
            {
                leaf.IsMapEditable = canEditMap;
                if (leaf.Symbol == null && leaf.Model?.LayerType == "OverlayImage" && !string.IsNullOrEmpty(leaf.Model.FilePath))
                {
                    var imgMarker = FindImageMarkerByFilePath(leaf.Model.FilePath);
                    if (imgMarker != null)
                    {
                        leaf.InitIsLocked(imgMarker.IsLocked);
                        // 레이어 체크 ↔ 실제 이미지 가시성 동기화(desync 수정). AggregateLeafCheckedFromMarkers는
                        // Symbol leaf만 재동기화 → 이미지 leaf는 DB MapLayer.IsVisible(로드값)에 고정돼,
                        // 이미지가 켜져 있어도 트리엔 꺼짐으로 표시됐음. 실제 마커 IsLayerEnabled 기준으로 맞춤.
                        leaf.SetCheckedSilently(imgMarker.IsLayerEnabled);
                    }
                }
            }

            // 펼침 상태 복원(이름 기준 매칭) — 리빌드로 접힌 카테고리/그룹/섹션을 사용자 상태로 되돌림.
            if (expandedNames.Count > 0)
                foreach (var node in LayerTreeBuilder.Flatten(_layerTreeNodes))
                    if (expandedNames.Contains(node.Name)) node.IsExpanded = true;

            if (LayerPanel != null)
                LayerPanel.TreeNodes = _layerTreeNodes;

            AggregateLeafCheckedFromMarkers();
            UpdateLayerItemCounts();

            var leafCount = LayerTreeBuilder.Flatten(_layerTreeNodes).Count();
            //_log?.Info($"레이어 트리 빌드 완료 ({leafCount}개 Leaf 노드)");
        }
        catch (Exception ex)
        {
            _log?.Error($"레이어 로드 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 저장된 레이어 가시성을 마커에 복원한다.
    /// UpdateMarkersVisibilityByZoom 덮어쓰기 방지를 위해 IsLayerEnabled도 함께 설정.
    /// ApplicationIdle 이후 호출되어야 함 (마커 렌더링 완료 후).
    /// </summary>
    private void RestoreLayerVisibility()
    {
        if (_layerTreeNodes == null || MainMap == null) return;

        foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes))
        {
            var model = leaf.Model;
            if (model == null) continue;

            if (model.LayerType == "OverlayImage" && !string.IsNullOrEmpty(model.FilePath))
            {
                var imgMarker = FindImageMarkerByFilePath(model.FilePath);
                if (imgMarker != null)
                {
                    imgMarker.IsLayerEnabled = model.IsVisible;
                    imgMarker.IsVisible = model.IsVisible;
                    if (imgMarker is IEditableMarker editableImg)
                        ((IEditableMarker)editableImg).ZOrder = model.ZOrder;
                    else
                        imgMarker.ZIndex = model.ZOrder;
                }
                else
                    _log?.Warning($"[Restore] OverlayImage 마커 미발견: {model.FilePath}");
            }
            else
            {
                ApplyLayerVisibility(model);
            }
        }
        MainMap?.InvalidateVisual();
        AggregateLeafCheckedFromMarkers();
    }

    private async void OnLayerVisibilityChanged(object? sender, LayerChangedEventArgs e)
    {
        try
        {
            if (e.Layer.LayerType == "OverlayMap" && e.Layer.MapId.HasValue && e.Layer.MapId.Value > 0)
            {
                // 아직 Activate 안 된 오버레이면 먼저 활성화
                if (e.IsVisible && !_customMapOverlayService.IsActive(e.Layer.MapId.Value))
                {
                    var customMap = _customMapService.LoadedCustomMaps
                        .FirstOrDefault(m => m.Id == e.Layer.MapId.Value);
                    if (customMap != null && MainMap != null)
                    {
                        _customMapOverlayService.ActivateOverlay(customMap, MainMap);
                        _log?.Info($"[Overlay] 레이어 체크 시 활성화: {e.Layer.Name} (MapId={e.Layer.MapId})");
                    }
                }
                _customMapOverlayService.SetVisibility(e.Layer.MapId.Value, e.IsVisible);
            }
            else if (e.Layer.LayerType == "OverlayImage" && !string.IsNullOrEmpty(e.Layer.FilePath))
            {
                var marker = FindImageMarkerByFilePath(e.Layer.FilePath);
                if (marker != null)
                {
                    marker.IsLayerEnabled = e.IsVisible;
                    marker.IsVisible = e.IsVisible;
                    MainMap?.InvalidateVisual();
                    await _gMapDbSymbolService.UpdateImageAsync(marker.ImageModel);
                }
                else
                {
                    _log?.Warning($"[OverlayImage] 마커 미발견: {e.Layer.FilePath}");
                }
            }
            else
            {
                ApplyLayerVisibility(e.Layer);
            }
            if (CanEditMap())   // FR-PG-11: 로컬 렌더는 허용, DB 영속만 권한 필요 (FR-EN-08)
                await _gMapDbService.UpdateMapLayerVisibilityAsync(e.Layer.Id, e.IsVisible);
            else
                _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 레이어 가시성 DB 저장 차단(로컬 렌더는 적용됨)");
            _log?.Info($"레이어 '{e.Layer.Name}' Visibility={e.IsVisible}");
        }
        catch (Exception ex) { _log?.Error($"레이어 Visibility 변경 실패: {ex.Message}"); }
    }

    private async void OnLayerOpacityChanged(object? sender, LayerOpacityChangedEventArgs e)
    {
        try
        {
            if (e.Layer.LayerType == "OverlayMap" && e.Layer.MapId.HasValue && e.Layer.MapId.Value > 0)
            {
                _customMapOverlayService.SetOpacity(e.Layer.MapId.Value, e.Opacity);
            }
            else if (e.Layer.LayerType == "OverlayImage" && !string.IsNullOrEmpty(e.Layer.FilePath))
            {
                var marker = FindImageMarkerByFilePath(e.Layer.FilePath);
                if (marker != null)
                {
                    marker.Opacity = e.Opacity;
                    MainMap.InvalidateVisual();
                }
            }
            if (CanEditMap())   // FR-PG-11: 로컬 렌더는 허용, DB 영속만 권한 필요 (FR-EN-08)
                await _gMapDbService.UpdateMapLayerOpacityAsync(e.Layer.Id, e.Opacity);
            else
                _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 레이어 투명도 DB 저장 차단(로컬 렌더는 적용됨)");
            _log?.Info($"레이어 '{e.Layer.Name}' Opacity={e.Opacity:F2}");
        }
        catch (Exception ex) { _log?.Error($"레이어 Opacity 변경 실패: {ex.Message}"); }
    }

    private async void OnLayerDeleteRequested(object? sender, LayerChangedEventArgs e)
    {
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 레이어 삭제 차단"); ShowNoMapEditPermissionInfo(); return; }
        try
        {
            var layer = e.Layer;
            _pendingDeleteLayer = layer;

            var title = layer.LayerType switch
            {
                "OverlayMap" => "오버레이 맵 삭제",
                "OverlayImage" => "오버레이 이미지 삭제",
                _ => "레이어 삭제"
            };

            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = title,
                Explain = $"'{layer.Name}'을(를) 삭제하시겠습니까?",
                MessageModel = new CallDeleteMapLayerProcessMessageModel()
            });
        }
        catch (Exception ex) { _log?.Error($"레이어 삭제 요청 실패: {ex.Message}"); }
    }

    private bool _isSyncingRename;

    private async void OnLayerRenameRequested(object? sender, Args.LayerRenameEventArgs e)
    {
        if (_isSyncingRename) return;
        if (!CanEditMap()) { _log?.Warning("[FR-EN-08] 맵 편집 권한 없음 — 레이어 이름 변경 차단"); ShowNoMapEditPermissionInfo(); return; }
        _isSyncingRename = true;
        try
        {
            var layer = e.Layer;
            var newName = e.NewName;
            var oldName = layer.Name;   // Undo용 이전 이름(AREA 3)
            _log?.Info($"[레이어 이름변경] {layer.Name} → {newName} (LayerType={layer.LayerType})");

            // 1. MapLayers DB 갱신
            layer.Name = newName;
            await _gMapDbService.UpdateMapLayerAsync(layer);
            _editRecorder?.RecordLayerChange("레이어 이름 변경",
                new[] { new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.LayerFields(layer.Id, oldName, null, null) },
                new[] { new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.LayerFields(layer.Id, newName, null, null) });

            // 2. OverlayImage인 경우 Images DB + Property Panel 동기화
            if (layer.LayerType == "OverlayImage" && !string.IsNullOrEmpty(layer.FilePath))
            {
                var marker = FindImageMarkerByFilePath(layer.FilePath);
                if (marker != null)
                {
                    marker.Title = newName;
                    await _gMapDbSymbolService.UpdateImageAsync(marker.ImageModel);

                    // Property Panel 열려있고 해당 마커 선택 중이면 동기화
                    if (PropertyPanel?.SelectedMarker == marker)
                        PropertyPanel.MarkerTitle = newName;
                }
            }
            // OverlayMap은 Property Panel 없음 → MapLayers.Name만 갱신 완료

            await LoadLayersFromDbAsync();
            _log?.Info($"[레이어 이름변경] 완료: {newName}");
        }
        catch (Exception ex) { _log?.Error($"레이어 이름변경 실패: {ex.Message}"); }
        finally { _isSyncingRename = false; }
    }

    private async void OnLayerMoveUpRequested(object? sender, LayerChangedEventArgs e)
    {
        try
        {
            await SwapZOrderWithSibling(e.Layer, -1); // 위 노드와 스왑
        }
        catch (Exception ex) { _log?.Error($"레이어 위로 이동 실패: {ex.Message}"); }
    }

    private async void OnLayerMoveDownRequested(object? sender, LayerChangedEventArgs e)
    {
        try
        {
            await SwapZOrderWithSibling(e.Layer, +1); // 아래 노드와 스왑
        }
        catch (Exception ex) { _log?.Error($"레이어 아래로 이동 실패: {ex.Message}"); }
    }

    private async Task SwapZOrderWithSibling(IMapLayerModel layer, int direction)
    {
        var layers = await _gMapDbService.FetchMapLayersAsync();
        var sametype = layers?.Where(l => l.LayerType == layer.LayerType).ToList();
        if (sametype == null) return;

        var idx = sametype.FindIndex(l => l.Id == layer.Id);
        var siblingIdx = idx + direction;
        if (idx < 0 || siblingIdx < 0 || siblingIdx >= sametype.Count) return;

        var sibling = sametype[siblingIdx];
        int oldLayerZ = layer.ZOrder, oldSiblingZ = sibling.ZOrder;   // Undo용 이전 순서(AREA 3)
        _log?.Info($"[레이어 순서] 스왑 시작: {layer.Name}(Z={layer.ZOrder}) ↔ {sibling.Name}(Z={sibling.ZOrder}), direction={direction}");

        if (layer.ZOrder != sibling.ZOrder)
        {
            // ZOrder가 다르면 단순 스왑
            (layer.ZOrder, sibling.ZOrder) = (sibling.ZOrder, layer.ZOrder);
        }
        else
        {
            // ZOrder 동일 (기존 데이터 ZOrder=0) — 위치 기반 강제 분리
            // ORDER BY ZOrder ASC이므로 낮은 값 = 위에 표시
            // layer가 siblingIdx 위치로, sibling이 idx 위치로
            layer.ZOrder = siblingIdx;
            sibling.ZOrder = idx;
        }

        _log?.Info($"[레이어 순서] 스왑 결과: {layer.Name}(Z={layer.ZOrder}) ↔ {sibling.Name}(Z={sibling.ZOrder})");

        await _gMapDbService.UpdateMapLayerAsync(layer);
        await _gMapDbService.UpdateMapLayerAsync(sibling);
        _editRecorder?.RecordLayerChange("레이어 순서 변경",
            new[] { new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.LayerFields(layer.Id, null, null, oldLayerZ), new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.LayerFields(sibling.Id, null, null, oldSiblingZ) },
            new[] { new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.LayerFields(layer.Id, null, null, layer.ZOrder), new Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands.LayerFields(sibling.Id, null, null, sibling.ZOrder) });

        // 맵 위 렌더링 순서 동기화
        SyncMapRenderingOrder(layer);
        SyncMapRenderingOrder(sibling);

        await LoadLayersFromDbAsync();
    }

    /// <summary>
    /// 맵 위 렌더링 순서를 DB ZOrder와 동기화
    /// </summary>
    private void SyncMapRenderingOrder(IMapLayerModel layer)
    {
        switch (layer.LayerType)
        {
            case "OverlayMap":
                if (layer.MapId.HasValue)
                    _customMapOverlayService.SetZOrder(layer.MapId.Value, layer.ZOrder);
                break;

            case "OverlayImage":
                if (!string.IsNullOrEmpty(layer.FilePath))
                {
                    var marker = FindImageMarkerByFilePath(layer.FilePath);
                    if (marker != null)
                    {
                        marker.ZIndex = layer.ZOrder;
                        // Edit 모드 OFF에서도 GMap.NET 마커 재정렬 트리거
                        MainMap?.InvalidateVisual();
                    }
                }
                break;
        }
    }

    private void OnLayerNavigateRequested(object? sender, LayerChangedEventArgs e)
    {
        if (MainMap == null) return;
        try
        {
            var layer = e.Layer;
            _log?.Info($"[레이어 이동] 시작: {layer.Name} (LayerType={layer.LayerType})");

            switch (layer.LayerType)
            {
                case "OverlayImage":
                    if (!string.IsNullOrEmpty(layer.FilePath))
                    {
                        var marker = FindImageMarkerByFilePath(layer.FilePath);
                        if (marker != null)
                            MainMap.Position = marker.Center;
                    }
                    break;

                case "OverlayMap":
                    if (layer.MapId.HasValue)
                    {
                        var customMap = _customMapService.LoadedCustomMaps
                            .FirstOrDefault(m => m.Id == layer.MapId.Value);
                        if (customMap?.MinLatitude != null && customMap.MaxLatitude != null
                            && customMap.MinLongitude != null && customMap.MaxLongitude != null)
                        {
                            var centerLat = (customMap.MinLatitude.Value + customMap.MaxLatitude.Value) / 2;
                            var centerLng = (customMap.MinLongitude.Value + customMap.MaxLongitude.Value) / 2;
                            MainMap.Position = new GMap.NET.PointLatLng(centerLat, centerLng);
                        }
                    }
                    break;
            }

            _log?.Info($"[레이어 이동] 완료: {layer.Name}");
        }
        catch (Exception ex) { _log?.Error($"레이어 이동 실패: {ex.Message}"); }
    }

    /// <summary>
    /// 레이어 Visibility를 맵 마커에 적용.
    /// 우선순위: 레이어 OFF → 무조건 숨김 > Zoom 범위 밖 → 숨김 > 표시
    /// </summary>
    private void ApplyLayerVisibility(IMapLayerModel layer)
    {
        if (layer.LayerType != "Symbol" || string.IsNullOrEmpty(layer.Category)) return;
        if (_isApplyingLayerVisibility) return;

        _isApplyingLayerVisibility = true;
        try
        {
            foreach (var marker in MainMap!.Markers)
            {
                if (!MatchMarkerToCategory(marker, layer.Category)) continue;
                if (marker is not GMapSymbols.IEditableMarker em) continue;

                em.IsLayerEnabled = layer.IsVisible;
                if (!layer.IsVisible)
                {
                    em.IsVisible = false;
                }
                else
                {
                    bool zoomOk = MainMap!.Zoom >= em.Zoom;
                    em.IsVisible = zoomOk;
                }
            }
            MainMap?.InvalidateVisual();
        }
        finally
        {
            _isApplyingLayerVisibility = false;
        }
    }

    /// <summary>
    /// 시작 집계: Layer ON인 Symbol Leaf 중 IsLayerEnabled 혼재 시 IsChecked를 Indeterminate(null)로 설정.
    /// ShowShape 대신 IsLayerEnabled 사용 — ShowShape는 마커 내부 아이콘 제어용이라
    /// PidsGroup처럼 ShowShape 기본값이 false인 마커에서 leaf가 잘못 OFF가 되는 버그 방지.
    /// _isApplyingLayerVisibility 가드 내에서만 호출 — CheckChanged → ApplyLayerVisibility 재진입 차단.
    /// </summary>
    private void AggregateLeafCheckedFromMarkers()
    {
        if (_layerTreeNodes == null || MainMap == null) return;

        _isApplyingLayerVisibility = true;
        try
        {
            foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes)
                .Where(n => n.NodeType == LayerNodeType.Leaf
                         && n.Model?.LayerType == "Symbol"
                         && n.Model.IsVisible))
            {
                var category = leaf.Model!.Category;
                if (string.IsNullOrEmpty(category)) continue;

                var markers = MainMap.Markers
                    .Where(m => MatchMarkerToCategory(m, category))
                    .OfType<GMapSymbols.IEditableMarker>()
                    .ToList();

                if (markers.Count == 0) continue;

                bool allEnabled = markers.All(m => m.IsLayerEnabled);
                bool noneEnabled = markers.All(m => !m.IsLayerEnabled);

                if (!allEnabled)
                    leaf.SetCheckedSilently(noneEnabled ? false : (bool?)null);
            }
        }
        finally
        {
            _isApplyingLayerVisibility = false;
        }
    }

    /// <summary>
    /// 각 Leaf 노드의 ItemCount를 맵 마커 개수 기준으로 업데이트
    /// </summary>
    private void UpdateLayerItemCounts()
    {
        if (_layerTreeNodes == null || MainMap == null) return;

        foreach (var node in _layerTreeNodes)
        {
            UpdateNodeCounts(node);
        }
    }

    private int UpdateNodeCounts(LayerTreeNode node)
    {
        // 카테고리 노드: 빌더가 설정한 개별 심볼 자식 수(ItemCount)를 그대로 사용.
        // (개별 심볼 Leaf의 Category=EnumMarkerCategory 이름이라 MatchMarkerToCategory 키와 불일치 → 재계산 시 0 회귀 방지)
        if (node.NodeType == LayerNodeType.Category)
            return node.ItemCount;

        if (node.NodeType == LayerNodeType.Leaf && node.Symbol == null && !string.IsNullOrEmpty(node.Category))
        {
            // 레거시 카테고리 단위 Leaf: 맵에서 해당 카테고리 마커 개수
            node.ItemCount = MainMap!.Markers.Count(m => MatchMarkerToCategory(m, node.Category));
            return node.ItemCount;
        }

        // Group/Section: 자식 합계 (Category 자식은 위에서 ItemCount 반환)
        int total = 0;
        foreach (var child in node.Children)
        {
            total += UpdateNodeCounts(child);
        }
        node.ItemCount = total;
        return total;
    }

    private static bool MatchMarkerToCategory(GMap.NET.WindowsPresentation.GMapMarker marker, string category)
    {
        return category switch
        {
            "PidsCamera" => marker is GMapSymbols.GMapPidsMarker pm && pm.DeviceType == Enums.EnumDeviceType.IpCamera,
            "PidsSensor" => marker is GMapSymbols.GMapPidsMarker ps && (ps.DeviceType == Enums.EnumDeviceType.Multi || ps.DeviceType == Enums.EnumDeviceType.SmartMultisensor2 || ps.DeviceType == Enums.EnumDeviceType.SmartSensor || ps.DeviceType == Enums.EnumDeviceType.SmartSensor2 || ps.DeviceType == Enums.EnumDeviceType.PIR || ps.DeviceType == Enums.EnumDeviceType.Fence || ps.DeviceType == Enums.EnumDeviceType.Underground || ps.DeviceType == Enums.EnumDeviceType.Contact || ps.DeviceType == Enums.EnumDeviceType.Laser || ps.DeviceType == Enums.EnumDeviceType.Cable || ps.DeviceType == Enums.EnumDeviceType.Radar || ps.DeviceType == Enums.EnumDeviceType.OpticalCable),
            "PidsSpeaker" => marker is GMapSymbols.GMapPidsMarker psp && psp.DeviceType == Enums.EnumDeviceType.IpSpeaker,
            "PidsController" => marker is GMapSymbols.GMapPidsMarker pc && pc.DeviceType == Enums.EnumDeviceType.Controller,
            "PidsLamp" => marker is GMapSymbols.GMapPidsMarker pl && pl.DeviceType == Enums.EnumDeviceType.Lamp,
            "PidsEnclosure" => marker is GMapSymbols.GMapPidsMarker pe && pe.DeviceType == Enums.EnumDeviceType.Enclosure,
            "Basic" => marker is GMapSymbols.GMapCustomMarker,
            "PidsGroup" => marker is GMapSymbols.GMapPidsGroupMarker,
            "Military" => marker is GMapSymbols.GMapMilitarySymbolMarker,
            "Geometric" => marker is GMapSymbols.GMapGeometricMarker,
            "Line" => marker is GMapSymbols.GMapLineMarker,
            "Infra" => marker is GMapSymbols.GMapInfraMarker,
            _ => false
        };
    }

    #endregion

}