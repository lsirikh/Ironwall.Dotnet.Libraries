using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using System;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
/****************************************************************************
   Purpose      : 계정 상태 VM (IAccountModel 래핑) — 라이브러리 이관(전략 A)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class AccountViewModel : BaseCustomViewModel<IAccountModel>
{
    #region - Ctors -
    public AccountViewModel(IEventAggregator eventAggregator
                            , ILogService log
                            , IAccountModel model)
                            : base(model, eventAggregator, log)
    {
    }
    #endregion
    #region - Overrides -
    public override void Dispose()
    {
        Model.Clear();
        GC.Collect();
    }
    #endregion
    #region - Processes -
    public bool Clear()
    {
        try
        {
            Model.Clear();
            Refresh();
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    public bool Insert(IAccountModel model)
    {
        try
        {
            Model.Update(model);
            Refresh();
            return true;
        }
        catch (System.Exception)
        {
            return false;
        }
    }
    #endregion
    #region - Properties -
    public string Username
    {
        get => Model.Username;
        set
        {
            Model.Username = value;
            NotifyOfPropertyChange(() => Username);
        }
    }

    public string Password
    {
        get => Model.Password;
        set
        {
            Model.Password = value;
            NotifyOfPropertyChange(() => Password);
        }
    }

    public string Name
    {
        get => Model.Name;
        set
        {
            Model.Name = value;
            NotifyOfPropertyChange(() => Name);
        }
    }

    public EnumLevelType Level
    {
        get => Model.Level;
        set
        {
            Model.Level = value;
            NotifyOfPropertyChange(() => Level);
        }
    }

    /// <summary>GOP 5단계 역할(구분). 편집 ComboBox 바인딩 대상.</summary>
    public EnumUserRole Role
    {
        get => Model.Role;
        set
        {
            Model.Role = value;
            NotifyOfPropertyChange(() => Role);
        }
    }

    public EnumUsedType Used
    {
        get => Model.Used;
        set
        {
            Model.Used = value;
            NotifyOfPropertyChange(() => Used);
        }
    }

    public string? EmployeeNumber
    {
        get => Model.EmployeeNumber;
        set
        {
            Model.EmployeeNumber = value;
            NotifyOfPropertyChange(() => EmployeeNumber);
        }
    }

    public DateTime? Birth
    {
        get => Model.Birth;
        set
        {
            Model.Birth = value;
            NotifyOfPropertyChange(() => Birth);
        }
    }

    public string? Phone
    {
        get => Model.Phone;
        set
        {
            Model.Phone = value;
            NotifyOfPropertyChange(() => Phone);
        }
    }

    public string? Address
    {
        get => Model.Address;
        set
        {
            Model.Address = value;
            NotifyOfPropertyChange(() => Address);
        }
    }

    public string? EMail
    {
        get => Model.EMail;
        set
        {
            Model.EMail = value;
            NotifyOfPropertyChange(() => EMail);
        }
    }

    public string? Image
    {
        get => Model.Image;
        set
        {
            Model.Image = value;
            NotifyOfPropertyChange(() => Image);
        }
    }

    public string? Position
    {
        get => Model.Position;
        set
        {
            Model.Position = value;
            NotifyOfPropertyChange(() => Position);
        }
    }

    public string? Department
    {
        get => Model.Department;
        set
        {
            Model.Department = value;
            NotifyOfPropertyChange(() => Department);
        }
    }

    public string? Company
    {
        get => Model.Company;
        set
        {
            Model.Company = value;
            NotifyOfPropertyChange(() => Company);
        }
    }
    #endregion
}
