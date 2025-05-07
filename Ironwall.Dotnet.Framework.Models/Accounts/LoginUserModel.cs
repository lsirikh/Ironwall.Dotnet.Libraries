using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Accounts;

public class LoginUserModel : LoginBaseModel, ILoginUserModel
{
    public LoginUserModel()
    {
    }

    public LoginUserModel(ILoginUserModel model) : base(model)
    {
        UserLevel = model.UserLevel;
        ClientId = model.ClientId;
        Mode = model.Mode;
    }

    public LoginUserModel(string userId, EnumAccountLevel userLevel, int clientId, int mode, DateTime timeCreated)
        : base(userId, timeCreated)
    {
        UserLevel = userLevel;
        ClientId = clientId;
        Mode = mode;
    }

    [JsonProperty("level", Order = 2)]
    public EnumAccountLevel UserLevel { get; set; }

    [JsonProperty("client_id", Order = 3)]
    public int ClientId { get; set; }

    [JsonProperty("mode", Order = 4)]
    public int Mode { get; set; }

    public void Insert(string userId, EnumAccountLevel userLevel, int clientId, int mode, DateTime timeCreated)
    {
        UserId = userId;
        UserLevel = userLevel;
        ClientId = clientId;
        Mode = mode;
        TimeCreated = timeCreated;
    }

    public override string ToString()
    {
        return $"UserId : {UserId}, UserLevel : {UserLevel}, ClientId : {ClientId}, Mode : {Mode}, TimeCreated : {TimeCreated}";
    }
}
