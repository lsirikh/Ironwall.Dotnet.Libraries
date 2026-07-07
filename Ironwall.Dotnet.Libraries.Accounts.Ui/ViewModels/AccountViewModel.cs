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

    /// <summary>GOP 5단계 역할(구분). 편집 ComboBox 바인딩 대상. Level(게이팅용)도 동기 갱신(드리프트 방지, W3).</summary>
    public EnumUserRole Role
    {
        get => Model.Role;
        set
        {
            Model.Role = value;
            Model.Level = RoleMappingHelper.ToLevel(value);   // 구분(Role)↔체크박스 게이팅(Level) 동기화
            NotifyOfPropertyChange(() => Role);
            NotifyOfPropertyChange(() => Level);
        }
    }

    /// <summary>role 드롭다운 항목 — v5.4 서버 Role 축소(ADMIN/USER 2종). 레거시 5등급(VIEWER/OPERATOR/MAINTAINER/GUEST)은 서버 미발행이라 배제(생성 시 422 방지).</summary>
    public System.Collections.Generic.IReadOnlyList<EnumUserRole> AvailableRoles { get; }
        = new[] { EnumUserRole.ADMIN, EnumUserRole.USER };

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

    /// <summary>계정 잠금 상태(서버 is_locked) — 목록 🔒 표시·잠금해제 버튼 가시성 바인딩. 표시 전용(읽기).</summary>
    public bool IsLocked
    {
        get => Model.IsLocked;
        set
        {
            Model.IsLocked = value;
            NotifyOfPropertyChange(() => IsLocked);
        }
    }

    /// <summary>잠금 사유(서버 lock_reason) — 툴팁 표시용. 표시 전용(읽기).</summary>
    public string? LockReason
    {
        get => Model.LockReason;
        set
        {
            Model.LockReason = value;
            NotifyOfPropertyChange(() => LockReason);
        }
    }
    #endregion
}
