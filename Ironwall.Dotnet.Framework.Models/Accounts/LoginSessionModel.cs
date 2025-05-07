using Ironwall.Dotnet.Framework.Models.Communications.Accounts;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public class LoginSessionModel : LoginBaseModel, ILoginSessionModel
{
    public LoginSessionModel()
    {
    }

    public LoginSessionModel(ILoginResultModel model)
                            : base(model.Details.Id, model.Details.IdUser, model.TimeCreated)
    {
        UserPass = model.Details.Password;
        Token = model.Token;
        TimeExpired = model.TimeExpired;
    }


    public LoginSessionModel(int id,
                            string userId, 
                            string userPass, 
                            string token,
                            DateTime timeCreated,
                            DateTime timeExpired)
                            : base(id, userId, timeCreated)
    {
        UserPass = userPass;
        Token = token;
        TimeExpired = timeExpired;
    }
    [JsonProperty("password", Order = 2)]
    public string UserPass { get; set; } = default!;
    [JsonProperty("level", Order = 2)]
    public string Token { get; set; } = default!;
    [JsonProperty("level", Order = 2)]
    public DateTime TimeExpired { get; set; }
}
