using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public abstract class LoginBaseModel : AccountBaseModel, ILoginBaseModel
{
    public LoginBaseModel()
    {

    }
    public LoginBaseModel(string userId, DateTime timeCreated) : base()
    {
        UserId = userId;
        TimeCreated = timeCreated;
    }

    public LoginBaseModel(ILoginBaseModel model)
        : base(model.Id)
    {
        UserId = model.UserId;
        TimeCreated = model.TimeCreated;
    }

    public LoginBaseModel(int id, string userId, DateTime timeCreated) : base(id)
    {
        UserId = userId;
        TimeCreated = timeCreated;
    }

    [JsonProperty("username", Order = 1)]
    public string UserId { get; set; } = default!;
    [JsonProperty("time", Order = 20)]
    public DateTime TimeCreated { get; set; }
}
