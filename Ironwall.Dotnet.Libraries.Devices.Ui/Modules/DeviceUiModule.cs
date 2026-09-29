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
    /// <summary>장비 API 클라이언트의 등록 이름(DeviceApiModule 기본값과 같다) — 이름 없이 IApiService 를 찾으면 호스트에서 실패한다.</summary>
    public const string DeviceApiName = "DeviceApi";

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
            builder.RegisterModule(new DeviceApiModule(_log, new ApiSetupModel(_apiSetup), DeviceApiName, count: _count++));
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
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Services.IUnitTopologyCache>()   // SYNC_UNIT 수신 시 호스트가 무효화한다
                   .SingleInstance().WithMetadata("Order", _count);

            builder.RegisterType<DeviceProviderService>().As<IDeviceProviderService>().As<IService>()
                .SingleInstance().WithMetadata("Order", _count);
            // 조립기 · 프리셋 · 프리셋으로 등록 창을 여는 입구. 창 뷰모델은 싱글턴이 아니다 — 열 때마다 새로 만든다.
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.AssemblyLauncher>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.IAssemblyLauncher>().SingleInstance();
            // N-11 units console — 부대 편제 콘솔(서버 8.0 이상에만 존재한다).
            // 세 등록 모두 ResolveOptional 로 의존을 받는다: 프로브·부대 API 가 없으면 IsAvailable=false 가 되어
            // 창 입구 자체가 나오지 않는다(운영 6.3.2 무회귀). 콘솔 뷰모델은 싱글턴이 아니라 열 때마다 새로 만든다.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.UnitGraphApiAdapter(
                        c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Api.Services.IUnitApiService>(),
                        c.ResolveOptional<IServerContractProbe>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.IUnitGraphApi>()
                   .SingleInstance();
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.UnitDeviceApiAdapter(
                        c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Api.Services.IDeviceApiService>(),
                        c.ResolveOptional<IServerContractProbe>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.IUnitDeviceApi>()
                   .SingleInstance();
            // 부대 관계도 배치 문서(S-1 GET/PATCH /api/units/layout) — 관계도 포트. 미지원 서버면 관계도가 세션 전용으로 동작한다(FR-50).
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.UnitLayoutApiAdapter(
                        c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Api.Services.IUnitLayoutApiService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.IUnitLayoutApi>()
                   .SingleInstance();
            // 부대 콘솔 입구 — 비모달 한 벌(FR-43). 지도의 [관계도에서 보기](OpenUnitConsoleRequest)를 들어야 하므로
            // 호스트가 해석하지 않아도 만들어 둔다(AutoActivate — V-13: 해석되지 않은 싱글턴은 구독자 목록에 없다).
            // 로그아웃 신호(ISessionLifecycle — GOP 모드만 등록)를 들어 콘솔을 닫는다.
            builder.Register(c =>
                   {
                       // 관계도 배치 "마지막 변경 나"(REVIEW-01 MEDIUM-6) — 로그인 계정 표시 이름(user.name = 서버 updated_by.name). 읽는 순간의 값.
                       var permissions = c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.IPermissionService>();
                       return new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.UnitConsoleLauncher(
                           c.Resolve<Caliburn.Micro.IWindowManager>(),
                           c.Resolve<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.IUnitGraphApi>(),
                           c.Resolve<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.IUnitDeviceApi>(),
                           c.ResolveOptional<Ironwall.Dotnet.Libraries.Nats.Models.INatsSetupModel>(),
                           c.ResolveOptional<ILogService>(),
                           c.ResolveOptional<Caliburn.Micro.IEventAggregator>(),   // 떠 있는 동안 SYNC_UNIT 를 듣는다 · 지도 요청을 듣는다
                           c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.IUnitLayoutApi>(),
                           c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ISessionLifecycle>(),
                           // 셸 종료 관문(U-27) — 호스트가 등록하면 떠 있는 콘솔을 올려 셸이 끝나기 전에 가드를 묻게 한다.
                           guardedWindows: c.ResolveOptional<Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.IGuardedWindowRegistry>(),
                           // 실시간 꼬리표: 라이브러리에 NATS 연결 상태 공개 서비스가 없어 런처 기본(알림 통로 부재 = 꺼짐)을 쓴다.
                           operatorName: permissions is null ? null : () => permissions.Name);
                   })
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.IUnitConsoleLauncher>()
                   .SingleInstance()
                   .AutoActivate();
            // D-14: unit_id → 부대 이름 읽기 전용 사전. IUnitGraphApi 위에서 id→이름을 1회 적재해 캐시한다 —
            // 장비 목록 "소속 부대" 열 · 상세 "부대" 칸이 원값 id 대신 이름을 보이는 유일한 경로.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Services.UnitNameDirectory(
                        c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.IUnitGraphApi>(),
                        c.ResolveOptional<IServerContractProbe>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Services.UnitNameDirectory>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Services.IUnitTopologyCache>()   // SYNC_UNIT 수신 시 호스트가 무효화한다
                   .SingleInstance();
            // 부대 관계도 FR-44 — 지도 · 상세 창이 읽는 부대 사전(이름 · 경로 · 소속 줄). 등록은 IUnitDirectory 하나뿐이다
            // (구체 타입으로 풀지 않는다 — 지도 쪽은 Devices 의 인터페이스만 안다). 편제 적재는 위 UnitNameDirectory 한 벌을 같이 쓴다.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.UnitDirectory(
                        c.Resolve<Ironwall.Dotnet.Libraries.Devices.Ui.Services.UnitNameDirectory>(),
                        c.ResolveOptional<Caliburn.Micro.IEventAggregator>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Units.IUnitDirectory>()
                   .SingleInstance();


            // N-12 server monitor — 서버 모니터 콘솔(레일 · 목록 + 지표 띠 · 상세).
            // 판본(6.3 평면 ↔ 7.0/8.0 축)을 아는 통로는 Devices.Api 의 새 인터페이스다 — 기존 IServerApiService 는 그대로 둔다.
            // ★ IApiService 는 ApiModule 이 <b>이름으로만</b> 등록한다("DeviceApi" 등) — 이름 없이 Resolve 하면 호스트에서
            //   "Cannot resolve parameter IApiService" 로 서버 모니터 콘솔이 열리지 않는다(이벤트 맵핑 워크벤치가 같은 결함으로
            //   실창에서 숨겨졌던 것과 같은 원인). DeviceApiModule(기본 이름 "DeviceApi")이 로그인 토큰을 붙이는 그 클라이언트를 쓴다.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Api.Servers.ServerAxisApiService(
                        c.ResolveNamed<Ironwall.Dotnet.Libraries.Api.Services.IApiService>(DeviceApiName),
                        new ApiSetupModel(_apiSetup),
                        c.ResolveOptionalNamed<IServerContractProbe>(DeviceApiName) ?? c.ResolveOptional<IServerContractProbe>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Api.Servers.IServerAxisApiService>()
                   .SingleInstance();
            // IUnitApiService · IUnitScopeService 는 Lazy 로만 잡는다: 8.0 미만이면 서비스가 부대를 아예 묻지 않는다.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers.ServerConsoleService(
                        c.Resolve<Ironwall.Dotnet.Libraries.Devices.Api.Servers.IServerAxisApiService>(),
                        c.Resolve<Ironwall.Dotnet.Libraries.Devices.Api.Services.IServerApiService>(),
                        c.Resolve<DeviceQueryPolicy>(),
                        c.ResolveOptional<Lazy<Ironwall.Dotnet.Libraries.Devices.Api.Services.IUnitApiService>>(),
                        c.ResolveOptional<Lazy<IUnitScopeService>>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers.IServerConsoleService>()
                   .SingleInstance();
            // 시계는 호스트가 이미 등록했으면 그것을, 아니면 여기서 채운다(서버 모니터의 "마지막 변화" 가 쓴다).
            builder.RegisterType<Ironwall.Dotnet.Libraries.Base.Services.SystemClock>()
                   .As<Ironwall.Dotnet.Libraries.Base.Services.IClock>()
                   .SingleInstance().PreserveExistingDefaults();
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers.ServerConsoleDialogs>()
                   .As<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers.IServerConsoleDialogs>().SingleInstance();
            builder.RegisterType<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers.ServerMonitorViewModel>().SingleInstance();

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