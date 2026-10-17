using Autofac;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Modules;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/25/2025 10:31:05 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapDbModule : Module
{
    #region - Ctors -
    public GMapDbModule(IGMapSetupModel gMapSetup, IMariaDbSetupModel gMapDbSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _gMapSetupModel = gMapSetup;
        _gMapDbSetupModel = gMapDbSetup;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            builder.RegisterModule(new GMapModule(_gMapSetupModel, _log, _count)); // 4

            var setupModel = new GMapDbSetupModel(_gMapDbSetupModel);
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<GMapDbService>().As<IGMapDbService>().As<IService>()
                .SingleInstance().WithMetadata("Order", _count+5);
            builder.RegisterType<GMapDbSymbolService>().As<IGMapDbSymbolService>().As<IService>()
                .SingleInstance().WithMetadata("Order", _count+6);
            // 결선 펜스 구성(fence-wiring-editor FR-11 · FR-16) — IService 가 아니다(부팅 경로 밖 · 표는 결선 창이 처음 쓸 때 만든다).
            //   장비 콘솔(Devices.Ui)의 결선 창이 Lazy<IFenceLayoutStore> 로 받는다 — 이 모듈이 없으면 로컬 저장 칸만 숨는다.
            builder.RegisterType<FenceLayoutStore>().As<Ironwall.Dotnet.Monitoring.Models.Fences.IFenceLayoutStore>()
                .SingleInstance();
            // 센서 설치 위치 · 속성 프리셋(sensor-mount-preset D1 · D5) — 같은 로컬 DB · 부팅 경로 밖. 결선 창 · 센서 모델 창이 Lazy 로 받는다.
            //   저장소는 appsettings "SensorPresets": { "Source": "GisDb" | "GopApi" } 로 고른다(없으면 GisDb). GopApi 는 자리만이라 읽기 전용
            //   (회사 기본 7종을 보이고 편집 단추가 잠긴다 · W1). 쓰임 셈(결선 레이아웃 JSON)은 늘 GIS DB 에서 읽는다.
            builder.RegisterType<GisDbSensorPresetRepository>()
                .AsSelf()
                .As<Ironwall.Dotnet.Monitoring.Models.Fences.Presets.ISensorPresetUsageSource>()
                .SingleInstance();
            builder.Register(c =>
                {
                    var context = c.Resolve<IComponentContext>();
                    return Ironwall.Dotnet.Monitoring.Models.Fences.Presets.SensorPresetRepositoryFactory.Create(
                        Ironwall.Dotnet.Monitoring.Models.Fences.Presets.SensorPresetSettings.Current,
                        () => context.Resolve<GisDbSensorPresetRepository>());
                })
                .As<Ironwall.Dotnet.Monitoring.Models.Fences.Presets.ISensorPresetRepository>()
                .SingleInstance();
        }
        catch
        {
            throw;
        }
    }
    #endregion
    #region - Overrides -
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
    private IGMapSetupModel _gMapSetupModel;
    private IMariaDbSetupModel _gMapDbSetupModel;
    private int _count;
    #endregion
}