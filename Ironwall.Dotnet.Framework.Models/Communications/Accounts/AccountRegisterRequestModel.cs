
using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountRegisterRequestModel : BaseMessageModel, IAccountRegisterRequestModel
    {
        public AccountRegisterRequestModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_ADD_REQUEST;
        }

        public AccountRegisterRequestModel(IUserModel model)
        {
            Command = EnumCmdType.USER_ACCOUNT_ADD_REQUEST;

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

        public AccountRegisterRequestModel(
            int id,
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
            Command = EnumCmdType.USER_ACCOUNT_ADD_REQUEST;

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

        [JsonProperty("user_id", Order = 1)]
        public string IdUser { get; set; } = string.Empty;

        [JsonProperty("user_pass", Order = 2)]
        public string Password { get; set; } = string.Empty;

        [JsonProperty("name", Order = 3)]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("emp_number", Order = 4)]
        public string? EmployeeNumber { get; set; }

        [JsonProperty("birth", Order = 5)]
        public string? Birth { get; set; }

        [JsonProperty("phone", Order = 6)]
        public string? Phone { get; set; }

        [JsonProperty("address", Order = 7)]
        public string? Address { get; set; }

        [JsonProperty("email", Order = 8)]
        public string? EMail { get; set; }

        [JsonProperty("image", Order = 9)]
        public string? Image { get; set; }

        [JsonProperty("position", Order = 10)]
        public string? Position { get; set; }

        [JsonProperty("department", Order = 11)]
        public string? Department { get; set; }

        [JsonProperty("company", Order = 12)]
        public string? Company { get; set; }

        [JsonProperty("level", Order = 13)]
        public EnumAccountLevel Level { get; set; }

        [JsonProperty("used", Order = 14)]
        public bool Used { get; set; }

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

        public void Insert(
            int id,
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
    }
}
