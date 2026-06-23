using Autofac;
using Ironwall.Dotnet.Framework.Services;                 // TokenGenerator
using Ironwall.Dotnet.Libraries.Accounts.Db.Modules;      // AccountDbModule
using Ironwall.Dotnet.Libraries.Accounts.Gateways;        // IAuthGateway / IUserDirectoryGateway / IProfileGateway
using Ironwall.Dotnet.Libraries.Accounts.Models;          // IAccountSetupModel, AccountSetupModel
using Ironwall.Dotnet.Libraries.Accounts.Modules;         // AccountModule
using Ironwall.Dotnet.Libraries.Accounts.Ui.Gateways;     // DbAccountGateway
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;     // ISessionConfigService / IProfileImageService
using Ironwall.Dotnet.Libraries.Base.Models;              // IMariaDbSetupModel
using Ironwall.Dotnet.Libraries.Base.Services;            // ILogService

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
    public AccountUiModule(IAccountSetupModel accountSetup,
                           IMariaDbSetupModel? dbSetup = default,
                           ILogService? log = default,
                           int count = default,
                           bool useDbAuth = true)
    {
        _accountSetup = accountSetup;
        _dbSetup = dbSetup;
        _log = log;
        _count = count;
        _useDbAuth = useDbAuth;
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

            // 5) Gateway seam — AuthMode.Direct: DbAccountGateway (GOP-00에서 ApiAccountGateway로 교체)
            if (_useDbAuth)
            {
                builder.RegisterType<DbAccountGateway>()
                       .As<IAuthGateway>()
                       .As<IUserDirectoryGateway>()
                       .As<IProfileGateway>()
                       .SingleInstance();
            }

            // 6) ViewModel 등록 — Phase 2/3 이관 후 활성화 (현재 VM 미생성)
            // builder.RegisterType<AccountViewModel>().SingleInstance();
            // builder.RegisterType<LoginViewModel>().SingleInstance();
            // builder.Register(c => new RegisterViewModel(
            //         c.Resolve<Caliburn.Micro.IEventAggregator>(),
            //         c.ResolveOptional<ILogService>(),
            //         new Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel()))
            //        .AsSelf().InstancePerDependency();   // ⚠️ C-1: 전용 모델 주입
            // builder.RegisterType<LoginPanelViewModel>().SingleInstance();
            // builder.RegisterType<AccountManagerPanelViewModel>().SingleInstance();
            // builder.RegisterType<MyPagePanelViewModel>().SingleInstance();
            // builder.RegisterType<AccountSetupPanelViewModel>().SingleInstance();
            // builder.RegisterType<RegisterDialogViewModel>().SingleInstance();
            // builder.RegisterType<EditorDialogViewModel>().SingleInstance();
            // builder.RegisterType<DeleteAccountDialogViewModel>().SingleInstance();
            // builder.RegisterType<ResetPasswordDialogViewModel>().SingleInstance();
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
    #endregion
}
