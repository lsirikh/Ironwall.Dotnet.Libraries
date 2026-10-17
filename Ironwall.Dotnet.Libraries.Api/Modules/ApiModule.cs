using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Net;
using System.Net.Http;

namespace Ironwall.Dotnet.Libraries.Api.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/5/2025 12:15:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ApiModule : Module
{
    #region - Ctors -
    public ApiModule(ILogService? log, IApiSetupModel setup, string name = "default", int count=100,
                     Func<IComponentContext, DelegatingHandler?>? authHandlerFactory = null)
    {
        _log = log;
        _setup = setup;
        _name = name;
        _count = count;
        _authHandlerFactory = authHandlerFactory;   // 로그인 게이팅(FR-BR): Device/Event에 Bearer 부착용 (null=기존 동작)
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(_setup).Named<ApiSetupModel>(_name).SingleInstance();
        builder.Register(build => new ApiService(_log, build.ResolveNamed<ApiSetupModel>(_name), _authHandlerFactory?.Invoke(build)))
            .Named<IApiService>(_name).SingleInstance().WithMetadata("Order", _count);

        // FR-08: 서버 계약 세대 프로브. setup(BaseAddress) 1:1 이라 Named 가 정본이고,
        //   편의상 기본 등록도 함께 둔다. PreserveExistingDefaults 로 "먼저 등록된 ApiModule"이 기본을
        //   갖게 해 결정적으로 만든다(Aligo 처럼 GOP 가 아닌 setup 이 기본을 덮는 사고 방지).
        //   ⚠ 이 모듈이 받은 _setup 인스턴스를 그대로 잡는다 — 이름 해석에 의존하지 않는다.
        builder.Register(_ => new ServerContractProbe(_log, _setup))
            .Named<IServerContractProbe>(_name)
            .As<IServerContractProbe>()
            .SingleInstance()
            .PreserveExistingDefaults();

        // FR-08 활성화 경로: 프로브는 "누가 ResolveAsync 를 부르느냐"가 없으면 캐시가 영원히 비어
        //   Contract 가 V6_3 폴백에 고정된다(= 버전 분기 전체가 죽은 코드). 그 호출부가 이 서비스다.
        //   레포 관용구(IService + Order)에 올라타므로 ParentBootstrapper.Start() 가 자동으로 기동한다
        //   — 메인 솔루션 수정 불필요. Order 는 음수(BOOT_ORDER)로 모든 API 소비자보다 앞에 둔다.
        //   ⚠ ApiModule 은 도메인별로 여러 번 등록된다(DeviceApi·EventApi·ReportApi…) → IfNotRegistered 로 1개만 살린다.
        //     (설령 중복돼도 서비스 내부 멱등 가드 + 프로브 ResolveAsync 멱등으로 중복 왕복은 없다.)
        builder.Register(c => new ServerContractBootService(_log, c.Resolve<IEnumerable<IServerContractProbe>>()))
            .AsSelf()
            .As<IService>()
            .SingleInstance()
            .WithMetadata("Order", ServerContractBootService.BOOT_ORDER)
            .IfNotRegistered(typeof(ServerContractBootService));

        _log?.Info($"{nameof(ApiModule)} is trying to create a single {nameof(ApiService)} instance.");
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
    private readonly ILogService? _log;
    private readonly IApiSetupModel _setup;
    private readonly string _name;
    private readonly int _count;
    private readonly Func<IComponentContext, DelegatingHandler?>? _authHandlerFactory;
    #endregion
}