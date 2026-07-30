using Autofac;
using System;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Modules;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Ptz;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Tracking.Api.Modules;
using Ironwall.Dotnet.Libraries.Tracking.Api.Services;
using Ironwall.Dotnet.Libraries.SystemResources.Modules;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/23/2025 2:22:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapUiModule: Module
{
    #region - Ctors -
    public GMapUiModule(IGMapSetupModel gMapSetup, IMariaDbSetupModel gMapDbSetup, IMainControlWebSetupModel webSetup, ILogService? log = default, int count = default, IApiSetupModel? trackingApiSetup = null)
    {
        _log = log;
        _count = count;
        _gMapSetup = gMapSetup;
        _gMapDbSetup = gMapDbSetup;
        _webSetup = webSetup;
        _trackingApiSetup = trackingApiSetup;   // Playback API 모드 base URL(=GOP ApiSetupModel). null이면 selector가 Local 폴백.
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterModule(new GMapDbModule(_gMapSetup, _gMapDbSetup, _log, _count)); // 4

        builder.RegisterInstance(_webSetup).As<IMainControlWebSetupModel>().SingleInstance();
        builder.RegisterType<DeviceDetailUrlService>().As<IDeviceDetailUrlService>().SingleInstance();
        // GIS→매니저 제어 REQ 공통 실행기(v1.5.2 §6.4 — REQ+RSP 확인, 기본 5s) — Broadcast/Aim/Lamp 공유
        builder.RegisterType<Services.Brokers.BrokerRequestClient>().As<Services.Brokers.IBrokerRequestClient>().SingleInstance();
        builder.RegisterType<BroadcastControlService>().As<IBroadcastControlService>().SingleInstance();
        // 카메라 "특정 위치 확인" 회전요청 NATS 발행(GIS→nvr_manager REQ) — Camera_PTZ_AimLocation
        builder.RegisterType<CameraAimControlService>().As<ICameraAimControlService>().SingleInstance();
        // 경광등 제어 4종 REQ 발행(LAMP_CLEAR/OFF/COLOR_SET/BUZZER_SET) — UI 미배선, 로직 계층 선행(PRD FR-07)
        builder.RegisterType<LampControlService>().As<ILampControlService>().SingleInstance();

        builder.RegisterType<MarkerFactory>().SingleInstance();
        builder.RegisterType<PropertyPanelFactory>().SingleInstance();
        builder.RegisterType<GMapControl>().SingleInstance();
        builder.RegisterType<GMapCustomControl>().SingleInstance();
        builder.RegisterType<MapViewModel>().SingleInstance();
        // Undo/Redo (Map_Edit_Undo_Redo) — 편집 사후기록/스택 서비스. MapViewModel에 IEditRecorder/IUndoService 주입.
        builder.RegisterType<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.UndoService>()
               .As<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.IUndoService>().SingleInstance();
        builder.RegisterType<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.EditRecorder>()
               .As<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.IEditRecorder>().SingleInstance();
        // PTZ 제어(CameraPopup_PTZ_Control) — IOnvifService(OnvifServiceModule)·ILogService 의존, 메인 Bootstrapper에서 OnvifServiceModule 등록 필요
        builder.RegisterType<PtzController>().As<IPtzController>().SingleInstance();
        // Tracking GIS 오버레이(FR-15) — IClock + 설정모델 + 단일 진입점 매니저(라이브러리 자족, EXT-01 없이 동작)
        builder.RegisterType<SystemClock>().As<IClock>().SingleInstance();
        // 시스템 리소스 모니터(SystemResources) — 툴바 우측 CPU/GPU/RAM. 위 IClock 등록 뒤라 IfNotRegistered가 중복 없이 스킵.
        builder.RegisterModule(new SystemResourceModule());
        // ⚠ 복사생성자 TrackingSetupModel(ITrackingSetupModel) 를 Autofac이 greedy 선택 → 자기참조 순환.
        //    appsettings(AppSettings.Tracking)에서 동기 로드한 단일 인스턴스 등록 → 저장값 복원(재시작 유지) + 순환 회피.
        builder.RegisterInstance(MapSettingsHelper.LoadTrackingSettings(_log))
               .As<ITrackingSetupModel>().SingleInstance();
        builder.RegisterType<TrackingOverlayManager>().AsSelf().As<ITrackingOverlayManager>().SingleInstance();
        // 추적 좌표 로컬 DB 영속(P4) — write(ITrackPointWriter)만. read seam은 selector가 토글.
        builder.RegisterType<TrackPointStore>().AsSelf()
               .As<ITrackPointWriter>().SingleInstance();
        // 데이터소스 토글(로컬 DB ↔ 서버 API) — API 모드는 trackingApiSetup 주입 시에만 활성.
        if (_trackingApiSetup != null)
        {
            // 앱 God-Model(IApiSetupModel)을 concrete ApiSetupModel로 복사(타임아웃 30s).
            // TrackingApiModule/ApiService/TrackingApiService는 concrete ApiSetupModel을 사용한다.
            var trackingApi = new ApiSetupModel(_trackingApiSetup) { Timeout = 30 };
            builder.RegisterModule(new TrackingApiModule(_log, trackingApi, "TrackingApi", _count));
            builder.Register(ctx => new TrackPointApiReader(ctx.Resolve<ITrackingApiService>(), _log))
                   .AsSelf().SingleInstance();
        }
        // ITrackPointReader = selector(FetchAsync마다 DataSource 읽어 라이브 분기). api 미주입 시 Local 폴백.
        builder.Register(ctx => new TrackDataSourceSelector(
                   ctx.Resolve<TrackPointStore>(),
                   _trackingApiSetup != null ? ctx.Resolve<TrackPointApiReader>() : null,
                   ctx.Resolve<ITrackingSetupModel>(),
                   _log))
               .As<ITrackPointReader>().SingleInstance();
        // Playback(P5) — 엔진·격리 오버레이·콘솔 VM (로컬 DB 조회 재생)
        builder.RegisterType<PlaybackEngine>().SingleInstance();
        builder.RegisterType<PlaybackOverlayManager>().SingleInstance();
        builder.RegisterType<PlaybackViewModel>().SingleInstance();
        // 추적 설정 패널(P3-04) — 싱글톤 ITrackingSetupModel 직접 조정(즉시 적용) + 저장
        builder.RegisterType<TrackingSetupViewModel>().SingleInstance();
        builder.RegisterType<TileGenerationService>().SingleInstance();
        builder.RegisterType<CustomMapService>().SingleInstance();
        builder.RegisterType<CustomMapOverlayService>().SingleInstance();
        builder.RegisterType<LruTileCache>().SingleInstance();
        builder.RegisterType<ImageOverlayService>().SingleInstance();
        builder.RegisterType<ImageFileService>().As<IImageFileService>().SingleInstance();
        //builder.RegisterType<MGRSGridOverlayService>().SingleInstance();
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private ILogService? _log;
    private int _count;
    private IGMapSetupModel _gMapSetup;
    private IMariaDbSetupModel _gMapDbSetup;
    private IMainControlWebSetupModel _webSetup;
    private IApiSetupModel? _trackingApiSetup;   // 앱 God-Model(SetupModel:IApiSetupModel) 그대로 수용
    #endregion
}