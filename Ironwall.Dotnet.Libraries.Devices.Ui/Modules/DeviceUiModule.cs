using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Modules;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.OnvifSolution.Modules;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/28/2025 3:49:06 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceUiModule : Module
{
    #region - Ctors -
    public DeviceUiModule( IApiSetupModel apiSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _apiSetup = apiSetup;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            builder.RegisterModule(new DeviceModule(_log, _count++));
            //builder.RegisterModule(new DeviceDbModule(_log, _apiSetup, _count++));
            builder.RegisterModule(new DeviceApiModule(_log, new ApiSetupModel(_apiSetup), count: _count++));
            // 디바이스 위치 저장 게이트웨이(Symbol_Apply_DeviceLocation) — 맵 심볼 현재위치를 디바이스 API로 저장.
            // IDeviceApiService(위 DeviceApiModule 등록)에 의존. GMaps.Ui가 lazy 해석.
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Services.DeviceLocationGateway>()
                   .As<Ironwall.Dotnet.Monitoring.Models.Devices.IDeviceLocationGateway>().SingleInstance();
            // FR-10 · FR-11: 장비 조회 쿼리를 서버 계약 세대별로 조립하는 단일 분기점.
            // IServerContractProbe 는 ResolveOptional — 미등록(구성 이전 단계)이면 null → 정책이 6.3(현행 운영)으로 동작한다.
            builder.Register(c => new DeviceQueryPolicy(
                        c.ResolveOptional<IServerContractProbe>(),
                        c.ResolveOptional<ILogService>()))
                   .As<DeviceQueryPolicy>()
                   .SingleInstance();

            // 장비 어휘 카탈로그(GET /api/devices/spec) 캐시 — 종류축 콤보·부품 어휘의 단일 원천(device-console-v8 FR-05).
            // IService 가 아니다: 부팅 때 읽지 않고 장비 창이 처음 필요로 할 때 1회 읽는다(로그인 전에는 토큰이 없다).
            // 6.3 이면 정책이 막아 서버를 부르지 않는다. SYNC_CATALOG 수신부(호스트 NatsBrokerService)가 RefreshAsync 를 부른다.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogService(
                        c.Resolve<Ironwall.Dotnet.Libraries.Devices.Api.Services.IDeviceApiService>(),
                        c.Resolve<DeviceQueryPolicy>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.IComponentCatalog>()   // 같은 캐시가 부품 팔레트도 댄다(조립기)
                   .SingleInstance();

            // 서버 8.0 부대 편제(unit_id) — GroupNats(부대 코드) → unit_id(정수) 해석 1회 + 캐시.
            // 의존 3종 모두 ResolveOptional: 프로브 미등록이면 V6_3 으로 간주되어 해석·전송이 전부 꺼진다(운영 6.3.2 무회귀).
            // INatsSetupModel 미등록(DB/오프라인 모드)이면 해석할 코드가 없어 경고 1회 후 unit_id 생략.
            builder.Register(c => new UnitScopeService(
                        c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Api.Services.IUnitApiService>(),
                        c.ResolveOptional<Ironwall.Dotnet.Libraries.Nats.Models.INatsSetupModel>(),
                        c.ResolveOptional<IServerContractProbe>(),
                        c.ResolveOptional<ILogService>()))
                   // Order 는 DeviceProviderService 와 같은 값을 쓴다(_count 를 증가시키지 않는다) —
                   // 기존 서비스들의 Order 값을 한 칸씩 밀면 다른 모듈 서비스와의 상대 순서가 바뀐다.
                   // ParentBootstrapper 의 OrderBy 는 안정 정렬이라 같은 값이면 등록 순서가 유지돼
                   // 이 서비스가 DeviceProviderService 보다 먼저 시작한다(장비 적재 전에 부대 해석).
                   .As<IUnitScopeService>().As<IService>()
                   .SingleInstance().WithMetadata("Order", _count);

            builder.RegisterType<DeviceProviderService>().As<IDeviceProviderService>().As<IService>()
                .SingleInstance().WithMetadata("Order", _count);
            // 조립기 · 프리셋 · 프리셋으로 등록 창을 여는 입구. 창 뷰모델은 싱글턴이 아니다 — 열 때마다 새로 만든다.
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.AssemblyLauncher>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.IAssemblyLauncher>().SingleInstance();
            // 셋업 · 결선 창(N-04) — 장비 콘솔이 Lazy 로만 잡는다(못 만들어도 콘솔은 열린다).
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.WiringLauncher>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.IWiringLauncher>().SingleInstance();
            builder.RegisterType<DeviceDashboardViewModel>().SingleInstance();
            builder.RegisterType<DeviceTabControlViewModel>().SingleInstance();
            builder.RegisterType<ControllerDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<SensorDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<CameraDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<SpeakerDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<EnclosureDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<LampDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<GateDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<DeviceGroupPanelViewModel>().SingleInstance();
            builder.RegisterType<ControllerDeviceViewModel>().SingleInstance();
            builder.RegisterType<SensorDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<CameraDeviceViewModel>().SingleInstance();

            // N-05 dialogs — 장비 배정 창(T4 · L 720)을 여는 입구. 창 뷰모델은 열 때마다 새로 만든다.
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs.DeviceAssignLauncher>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs.IDeviceAssignLauncher>().SingleInstance();
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
    //private IMariaDbSetupModel _apiSetup;
    private IApiSetupModel _apiSetup;
    private int _count;
    #endregion
}