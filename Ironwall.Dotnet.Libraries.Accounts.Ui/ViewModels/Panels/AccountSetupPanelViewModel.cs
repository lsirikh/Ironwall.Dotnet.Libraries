using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 계정/세션 설정 패널 — 세션 토글/만료를 ISessionConfigService로 위임
   Created By   : GHLee
   Company      : Sensorway Co., Ltd.
   Notes        : 앱 BaseSetupViewModel(탭 메타) 결합을 끊고 세션 코어만 라이브러리로 추출(§8).
                  탭 호스팅은 앱 잔류(점진안, OQ-3).
****************************************************************************/
public class AccountSetupPanelViewModel : BasePanelViewModel
{
    #region - Ctors -
    public AccountSetupPanelViewModel(IEventAggregator eventAggregator, ILogService log,
        ISessionConfigService session, ViewModels.LoginViewModel login) : base(eventAggregator, log)
    {
        _session = session;
        _login = login;
        _isSession = session.IsSession;
        _sessionExpiration = session.SessionExpiration;
    }
    #endregion
    #region - Properties -
    private bool _isSession;
    public bool IsSession
    {
        get => _isSession;
        set
        {
            if (_isSession == value) return;
            _isSession = value;
            NotifyOfPropertyChange(() => IsSession);
            _session.ApplySession(_isSession, _sessionExpiration);
        }
    }

    private int _sessionExpiration;
    public int SessionExpiration
    {
        get => _sessionExpiration;
        set
        {
            if (_sessionExpiration == value) return;
            _sessionExpiration = value;
            NotifyOfPropertyChange(() => SessionExpiration);
            _session.ApplySession(_isSession, _sessionExpiration);
        }
    }

    public ViewModels.LoginViewModel LoginViewModel => _login;   // XAML 권한(레벨) 바인딩용
    #endregion
    #region - Attributes -
    private readonly ISessionConfigService _session;
    private readonly ViewModels.LoginViewModel _login;
    #endregion
}
