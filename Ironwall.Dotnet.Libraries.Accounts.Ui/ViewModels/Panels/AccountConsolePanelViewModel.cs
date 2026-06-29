using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 계정·권한 관리 콘솔 — 큰 카드 + 상단 수평 탭(사용자/권한/세션/감사)
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 작은 팝업/SetupPanel 좁은탭 → 전용 큰 콘솔로 통합(사용자 피드백: 컬럼 잘림).
                  4개 콘텐츠 VM을 보유 + 활성화(조회 로드) 위임. 탭 노출은 역할 게이팅(CanSee*).
                  디자인: Tactical Command 토큰(시안헤더+다크카드). 내 정보(MyPage)는 콘솔 제외(독립 유지).
                  GOP 모드 전용(IAccountApiService 의존 패널 보유) — AccountUiModule !_useDbAuth 등록.
****************************************************************************/
public class AccountConsolePanelViewModel : BasePanelViewModel
{
    #region - Ctors -
    public AccountConsolePanelViewModel(IEventAggregator eventAggregator
                                       , ILogService log
                                       , IPermissionService permission
                                       , AccountManagerPanelViewModel accountManager
                                       , PermissionMatrixPanelViewModel permissionMatrix
                                       , UserSessionPanelViewModel userSession
                                       , AuditLogPanelViewModel auditLog
                                       , AccountSetupPanelViewModel accountSetup)
        : base(eventAggregator, log)
    {
        _permission = permission;
        AccountManagerPanelViewModel = accountManager;
        PermissionMatrixPanelViewModel = permissionMatrix;
        UserSessionPanelViewModel = userSession;
        AuditLogPanelViewModel = auditLog;
        AccountSetupPanelViewModel = accountSetup;
        _permission.PermissionsChanged += OnPermissionsChanged;
    }
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        // 각 탭 콘텐츠 VM 활성화 → OnActivate에서 서버 조회 로드
        await ScreenExtensions.TryActivateAsync(AccountManagerPanelViewModel, cancellationToken);
        await ScreenExtensions.TryActivateAsync(PermissionMatrixPanelViewModel, cancellationToken);
        await ScreenExtensions.TryActivateAsync(UserSessionPanelViewModel, cancellationToken);
        await ScreenExtensions.TryActivateAsync(AuditLogPanelViewModel, cancellationToken);
        await ScreenExtensions.TryActivateAsync(AccountSetupPanelViewModel, cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await ScreenExtensions.TryDeactivateAsync(AccountManagerPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(PermissionMatrixPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(UserSessionPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(AuditLogPanelViewModel, close, cancellationToken);
        await ScreenExtensions.TryDeactivateAsync(AccountSetupPanelViewModel, close, cancellationToken);
        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion
    #region - Binding Methods -
    public async Task ClickClose()
        => await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePanelMessageModel());

    private void OnPermissionsChanged()
    {
        NotifyOfPropertyChange(nameof(CanSeeAccounts));
        NotifyOfPropertyChange(nameof(CanSeePermission));
        NotifyOfPropertyChange(nameof(CanSeeSession));
        NotifyOfPropertyChange(nameof(CanSeeSessionConfig));
        NotifyOfPropertyChange(nameof(CanSeeAudit));
    }
    #endregion
    #region - Properties -
    /// <summary>탭 콘텐츠 VM — View의 ContentControl(x:Name 컨벤션) 호스팅.</summary>
    public AccountManagerPanelViewModel AccountManagerPanelViewModel { get; }
    public PermissionMatrixPanelViewModel PermissionMatrixPanelViewModel { get; }
    public UserSessionPanelViewModel UserSessionPanelViewModel { get; }
    public AuditLogPanelViewModel AuditLogPanelViewModel { get; }
    public AccountSetupPanelViewModel AccountSetupPanelViewModel { get; }

    /// <summary>탭 노출 게이팅 — 사용자/권한/세션/세션설정=ADMIN, 감사=ADMIN|MAINTAINER.</summary>
    public bool CanSeeAccounts => _permission.IsAdmin;
    public bool CanSeePermission => _permission.IsAdmin;
    public bool CanSeeSession => _permission.IsAdmin;
    public bool CanSeeSessionConfig => _permission.IsAdmin;
    public bool CanSeeAudit => _permission.CanAccessAuditLogs();
    #endregion
    #region - Attributes -
    private readonly IPermissionService _permission;
    #endregion
}
