using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public abstract class AccountBaseModel : IAccountBaseModel
{
    public AccountBaseModel()
    {
    }

    public AccountBaseModel(int id)
    {
        Id = id;
    }

    [JsonProperty("id", Order = 0)]
    public int Id { get; set; }
}
