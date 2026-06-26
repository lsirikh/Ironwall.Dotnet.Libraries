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
    public GMapUiModule(IGMapSetupModel gMapSetup, IMariaDbSetupModel gMapDbSetup, IMainControlWebSetupModel webSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _count = count;
        _gMapSetup = gMapSetup;
        _gMapDbSetup = gMapDbSetup;
        _webSetup = webSetup;
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
        builder.RegisterType<BroadcastControlService>().As<IBroadcastControlService>().SingleInstance();

        builder.RegisterType<MarkerFactory>().SingleInstance();
        builder.RegisterType<PropertyPanelFactory>().SingleInstance();
        builder.RegisterType<GMapControl>().SingleInstance();
        builder.RegisterType<GMapCustomControl>().SingleInstance();
        builder.RegisterType<MapViewModel>().SingleInstance();
        // PTZ 제어(CameraPopup_PTZ_Control) — IOnvifService(OnvifServiceModule)·ILogService 의존, 메인 Bootstrapper에서 OnvifServiceModule 등록 필요
        builder.RegisterType<PtzController>().As<IPtzController>().SingleInstance();
        // Tracking GIS 오버레이(FR-15) — IClock + 설정모델 + 단일 진입점 매니저(라이브러리 자족, EXT-01 없이 동작)
        builder.RegisterType<SystemClock>().As<IClock>().SingleInstance();
        // ⚠ 복사생성자 TrackingSetupModel(ITrackingSetupModel) 를 Autofac이 greedy 선택 → 자기참조 순환.
        //    명시적 기본생성자 팩토리로 등록(순환 회피).
        builder.Register(_ => new TrackingSetupModel()).As<ITrackingSetupModel>().SingleInstance();
        builder.RegisterType<TrackingOverlayManager>().AsSelf().As<ITrackingOverlayManager>().SingleInstance();
        // 추적 좌표 로컬 DB 영속(P4) — write(ITrackPointWriter) + Playback read(ITrackPointReader seam, 서버전환 시 교체)
        builder.RegisterType<TrackPointStore>().AsSelf()
               .As<ITrackPointWriter>().As<ITrackPointReader>().SingleInstance();
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
    #endregion
}