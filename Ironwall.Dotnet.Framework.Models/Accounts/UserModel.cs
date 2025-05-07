using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public class UserModel : UserBaseModel, IUserModel
{
    public UserModel()
    {
    }

    public UserModel(IUserModel model) : base(model)
    {
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

    public UserModel(int id,
                    string userId,
                    string password,
                    string name,
                    string empnum,
                    string birth,
                    string phone,
                    string address,
                    string email,
                    string image,
                    string position,
                    string department,
                    string company,
                    EnumAccountLevel level,
                    bool used)
                    : base(id, userId, password, level, name, used)
    {
        EmployeeNumber = empnum;
        Birth = birth;
        Phone = phone;
        Address = address;
        EMail = email;
        Image = image;
        Position = position;
        Department = department;
        Company = company;
    }

    #region - Implementations for IAccountModel -
    public string EmployeeNumber { get; set; } = string.Empty; //6
    public string Birth { get; set; } = string.Empty;          //7
    public string Phone { get; set; } = string.Empty;          //8
    public string Address { get; set; } = string.Empty;        //9
    public string EMail { get; set; } = string.Empty;          //10
    public string Image { get; set; } = string.Empty;          //11
    public string Position { get; set; } = string.Empty;       //12
    public string Department { get; set; } = string.Empty;     //13
    public string Company { get; set; } = string.Empty;        //14
    #endregion

    public void Insert(int id,
                        string userId,
                        string password,
                        string name,
                        string empnum,
                        string birth,
                        string phone,
                        string address,
                        string email,
                        string image,
                        string position,
                        string department,
                        string company,
                        EnumAccountLevel level,
                        bool used)
    {
        Id = id;
        IdUser = userId;
        Password = password;
        Name = name;
        EmployeeNumber = empnum;
        Birth = birth;
        Phone = phone;
        Address = address;
        EMail = email;
        Image = image;
        Position = position;
        Department = department;
        Company = company;
        Level = level;
        Used = used;
    }

    public void Insert(IUserModel model)
    {
        Id = model.Id;
        IdUser = model.IdUser;
        Password = model.Password;
        Name = model.Name;
        EmployeeNumber = model.EmployeeNumber;
        Birth = model.Birth;
        Phone = model.Phone;
        Address = model.Address;
        EMail = model.EMail;
        Image = model.Image;
        Position = model.Position;
        Department = model.Department;
        Company = model.Company;
        Level = model.Level;
        Used = model.Used;
    }
}
