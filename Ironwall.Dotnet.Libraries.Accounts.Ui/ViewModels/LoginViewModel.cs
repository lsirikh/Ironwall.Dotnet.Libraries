using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
/****************************************************************************
   Purpose      : 로그인 상태 VM (Token/IsLogin/세션) — 라이브러리 이관(전략 A)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
   Notes        : 시그니처 보존(IsLogin/Token/Logout) — GOP-00 연동 시 Role/Permissions 추가만.
****************************************************************************/
public class LoginViewModel : AccountViewModel
{
    #region - Ctors -
    public LoginViewModel(IEventAggregator eventAggregator
                        , ILogService log
                        , IAccountModel model)
                        : base(eventAggregator, log, model)
    {
    }
    #endregion
    #region - Processes -
    public bool Logout()
    {
        try
        {
            Clear();
            Token = string.Empty;
            IsLogin = false;
            LoginTime = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            return false;
        }
    }
    #endregion
    #region - Properties -
    public string? Token
    {
        get { return _token; }
        set
        {
            _token = value;
            NotifyOfPropertyChange(() => Token);
        }
    }
    public bool IsLogin
    {
        get { return _isLogin; }
        set
        {
            _isLogin = value;
            NotifyOfPropertyChange(() => IsLogin);
        }
    }
    public string? LoginTime
    {
        get { return _loginTime; }
        set
        {
            _loginTime = value;
            NotifyOfPropertyChange(() => LoginTime);
        }
    }

    public bool IsSubMenuOpened
    {
        get { return _isSubMenuOpened; }
        set
        {
            _isSubMenuOpened = value;
            NotifyOfPropertyChange(() => IsSubMenuOpened);
        }
    }

    public bool IsIdSaved
    {
        get { return _isIdSaved; }
        set
        {
            _isIdSaved = value;
            NotifyOfPropertyChange(() => IsIdSaved);
        }
    }
    #endregion
    #region - Attributes -
    private string? _token;
    private bool _isLogin;
    private string? _loginTime;
    private bool _isSubMenuOpened = true;
    private bool _isIdSaved;
    #endregion
}
