using Autofac;
using Ironwall.Dotnet.Framework.Services;                 // TokenGenerator
using Ironwall.Dotnet.Libraries.Accounts.Db.Modules;      // AccountDbModule
using Ironwall.Dotnet.Libraries.Accounts.Api.Modules;     // AccountApiModule (GOP useDbAuth=false)
using Ironwall.Dotnet.Libraries.Accounts.Gateways;        // IAuthGateway / IUserDirectoryGateway / IProfileGateway
using Ironwall.Dotnet.Libraries.Api.Models;               // IApiSetupModel (GOP 서버 설정)
using Ironwall.Dotnet.Libraries.Accounts.Models;          // IAccountSetupModel, AccountSetupModel
using Ironwall.Dotnet.Libraries.Accounts.Modules;         // AccountModule
using Ironwall.Dotnet.Libraries.Accounts.Ui.Gateways;     // DbAccountGateway
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;     // ISessionConfigService / IProfileImageService
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;   // AccountViewModel / LoginViewModel / RegisterViewModel
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;  // 다이얼로그 VM
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;   // 패널 VM
using Ironwall.Dotnet.Libraries.Base.Models;              // IMariaDbSetupModel
using Ironwall.Dotnet.Libraries.Base.Services;            // ILogService
using Ironwall.Dotnet.Monitoring.Models.Accounts;         // AccountModel (C-1 전용 모델)

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Modules;

/****************************************************************************
   Purpose      : Accounts.Ui DI 등록 (도메인 모듈 일원화 + Gateway seam + 서비스)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 앱 Bootstrapper 의 AccountModule + AccountDbModule 2줄 등록을 이 모듈 1줄로 대체(§9.5).
                  useDbAuth(=AuthMode.Direct) 일 때 DbAccountGateway 등록. 후속 GOP-00은
                  useDbAuth=false + ApiAccountGateway 등록으로 전환(VM 재편집 0).
****************************************************************************/
public class AccountUiModule : Module
{
    #region - Ctors -
    /// <param name="accountSetup">IsSession/SessionExpiration 보유(SetupModel 구현).</param>
    /// <param name="dbSetup">MariaDB 연결정보(SetupModel 구현). useDbAuth=false 시 미사용.</param>
    /// <param name="log">선택적 로깅.</param>
    /// <param name="count">IService Order 시작값(기존 AccountDb Order=10 유지).</param>
    /// <param name="useDbAuth">AuthMode.Direct(=true): DbAccountGateway 등록. GOP-00에서 false로 전환.</param>
    /// <param name="apiSetup">GOP 서버 설정(useDbAuth=false 시 필수). AccountApiModule 에 주입.</param>
    public AccountUiModule(IAccountSetupModel accountSetup,
                           IMariaDbSetupModel? dbSetup = default,
                           ILogService? log = default,
                           int count = default,
                           bool useDbAuth = true,
                           IApiSetupModel? apiSetup = default)
    {
        _accountSetup = accountSetup;
        _dbSetup = dbSetup;
        _log = log;
        _count = count;
        _useDbAuth = useDbAuth;
        _apiSetup = apiSetup;
    }
    #endregion

    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            // 1) 하위 도메인 모듈 체인 (등록 책임을 UI 모듈로 일원화)
            //    ⚠️ Bootstrapper 의 동일 RegisterModule 2줄은 반드시 제거 (§9.5)
            builder.RegisterModule(new AccountModule(_accountSetup, _log, _count++));
            if (_useDbAuth)
                builder.RegisterModule(new AccountDbModule(_dbSetup, _log, _count++));

            // 2) AccountSetupModel 을 IAccountSetupModel 로 노출 (AccountModule은 AsSelf만 등록)
            builder.Register(c => c.Resolve<AccountSetupModel>())
                   .As<IAccountSetupModel>().SingleInstance();

            // 3) TokenGenerator (세션 단일 소스, 앱이 이미 등록했으면 무시)
            builder.RegisterType<TokenGenerator>()
                   .AsSelf().SingleInstance().IfNotRegistered(typeof(TokenGenerator));

            // 4) Account.Ui 서비스
            builder.RegisterType<SessionConfigService>()
                   .As<ISessionConfigService>().SingleInstance();
            builder.RegisterType<ProfileImageService>()
                   .As<IProfileImageService>().SingleInstance()
                   .IfNotRegistered(typeof(IProfileImageService));   // 앱이 커스텀 주입 시 우선

            // 5) Gateway seam — Direct: DbAccountGateway / GOP: ApiAccountGateway (AccountApiModule)
            if (_useDbAuth)
            {
                builder.RegisterType<DbAccountGateway>()
                       .As<IAuthGateway>()
                       .As<IUserDirectoryGateway>()
                       .As<IProfileGateway>()
                       .SingleInstance();
            }
            else
            {
                // GOP-00: ApiAccountGateway(3 인터페이스)+IAccountApiService+TokenStorage+Permission+BearerAuthHandler 일괄 등록.
                //         VM 은 IAuthGateway 등에만 의존하므로 재편집 0 (§2.4.1).
                if (_apiSetup is null)
                    throw new InvalidOperationException(
                        "useDbAuth=false(GOP 모드)인데 apiSetup(IApiSetupModel — GOP 서버 URL)이 주입되지 않았습니다.");
                builder.RegisterModule(new AccountApiModule(_log, _apiSetup, name: "Account", count: _count++));
            }

            // 6) 상태 VM (Phase 2 이관 완료) — 패널/다이얼로그 VM은 Phase 3에서 활성화
            builder.RegisterType<AccountViewModel>().SingleInstance();
            builder.RegisterType<LoginViewModel>().SingleInstance();
            builder.Register(c => new RegisterViewModel(
                        c.Resolve<Caliburn.Micro.IEventAggregator>(),
                        c.Resolve<ILogService>(),
                        new AccountModel()))                  // ⚠️ C-1: 전용 모델 주입(공유 싱글톤 격리)
                   .AsSelf().InstancePerDependency();
            builder.RegisterType<LoginPanelViewModel>().SingleInstance();
            builder.RegisterType<LogoutPanelViewModel>().SingleInstance();
            builder.RegisterType<AccountManagerPanelViewModel>().SingleInstance();
            builder.RegisterType<MyPagePanelViewModel>().SingleInstance();
            builder.RegisterType<AccountSetupPanelViewModel>().SingleInstance();
            builder.RegisterType<RegisterDialogViewModel>().SingleInstance();
            builder.RegisterType<EditorDialogViewModel>().SingleInstance();
            builder.RegisterType<DeleteAccountDialogViewModel>().SingleInstance();
            builder.RegisterType<ResetPassDialogViewModel>().SingleInstance();

            // GOP 모드 전용 읽기 패널 — IAccountApiService(그룹/세션/감사 읽기 API) 직접 주입.
            // DbAuth 모드엔 IAccountApiService 미등록(AccountApiModule 미로드)이라 조건부 등록(resolve 깨짐 방지).
            if (!_useDbAuth)
            {
                builder.RegisterType<PermissionMatrixPanelViewModel>().SingleInstance();
                builder.RegisterType<UserSessionPanelViewModel>().SingleInstance();
                builder.RegisterType<AuditLogPanelViewModel>().SingleInstance();
            }
        }
        catch
        {
            throw;
        }
    }
    #endregion

    #region - Attributes -
    private readonly IAccountSetupModel _accountSetup;
    private readonly IMariaDbSetupModel? _dbSetup;
    private readonly ILogService? _log;
    private int _count;
    private readonly bool _useDbAuth;
    private readonly IApiSetupModel? _apiSetup;
    #endregion
}
