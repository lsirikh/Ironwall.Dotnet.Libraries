using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public abstract class UserBaseModel : AccountBaseModel, IUserBaseModel
{
    public UserBaseModel()
    {
    }

    public UserBaseModel(IUserBaseModel model) : base(model.Id)
    {
        IdUser = model.IdUser;
        Password = model.Password;
        Level = model.Level;
        Name = model.Name;
        Used = model.Used;
    }

    public UserBaseModel(int id, string idUser, string pass, EnumAccountLevel level, string name, bool used)  : base(id) 
    {
        IdUser = idUser;
        Password = pass;
        Level = level;
        Name = name;
        Used = used;
    }

    [JsonProperty("username", Order = 1)]
    public string IdUser { get; set; } = default!;
    [JsonProperty("password", Order = 2)]
    public string Password { get; set; } = default!;
    [JsonProperty("level", Order = 3)]
    public EnumAccountLevel Level { get; set; } = EnumAccountLevel.USER;
    [JsonProperty("name", Order = 4)]
    public string Name { get; set; } = default!;
    [JsonProperty("used", Order = 5)]
    public bool Used { get; set; }
}
