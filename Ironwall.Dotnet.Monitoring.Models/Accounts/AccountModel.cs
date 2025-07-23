using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Accounts;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/8/2025 9:06:13 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class AccountModel : IAccountModel
{
    #region - Implementations for IUserModel -
    public int Id { get; set; }                                 //Not Counted
    public string Username { get; set; } = string.Empty;        //1
    public string Password { get; set; } = string.Empty;        //2
    public string Name { get; set; } = string.Empty;            //3
    public EnumLevelType Level { get; set; }                        //4
    public EnumUsedType Used { get; set; }                      //5
    #endregion
    #region - Implementations for IAccountModel -
    public string? EmployeeNumber { get; set; }                    //6
    public DateTime? Birth { get; set; }                           //7
    public string? Phone { get; set; }                          //8
    public string? Address { get; set; }                          //9
    public string? EMail { get; set; }                          //10
    public string? Image { get; set; }                         //11
    public string? Position { get; set; }                        //12
    public string? Department { get; set; }                    //13
    public string? Company { get; set; }                      //14
    #endregion


    /// <summary>
    /// 전달된 <paramref name="model"/> 의 값을 현재 인스턴스(this)에 복사한다.
    /// (null 체크와 참조 동일성 검사 포함)
    /// </summary>
    public void Update(IAccountModel model)
    {
        if (model is null)
            throw new ArgumentNullException(nameof(model));

        // 같은 객체라면 굳이 복사할 필요 없음
        if (ReferenceEquals(this, model))
            return;

        // ─── 공통(Interface) 영역 ───
        Id = model.Id;
        Username = model.Username;
        Password = model.Password;
        Name = model.Name;
        Level = model.Level;
        Used = model.Used;

        // IAccountModel 고유 영역
        EmployeeNumber = model.EmployeeNumber;
        Birth = model.Birth;
        Phone = model.Phone;
        Address = model.Address;
        EMail = model.EMail;
        Image = model.Image;
        Position = model.Position;
        Department = model.Department;
        Company = model.Company;
    }


    /// <summary>
    /// 모든 필드를 초기 상태로 되돌린다.
    /// </summary>
    public void Clear()
    {
        // ▩ 기본값 또는 null 로 리셋 ▩
        Id = 0;                   // 필요 없다면 주석 처리
        Username = string.Empty;
        Password = string.Empty;
        Name = string.Empty;
        Level = default;             // Enum 기본값 (0)
        Used = default;

        EmployeeNumber = null;
        Birth = null;
        Phone = null;
        Address = null;
        EMail = null;
        Image = null;
        Position = null;
        Department = null;
        Company = null;
    }
}
