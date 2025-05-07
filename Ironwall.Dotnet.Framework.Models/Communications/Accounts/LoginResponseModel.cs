using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class LoginResponseModel : ResponseModel, ILoginResponseModel
    {
        public LoginResponseModel()
        {
            Command = EnumCmdType.LOGIN_RESPONSE;
        }

        public LoginResponseModel(bool success, string msg, LoginResultModel result)
            : base(success, msg)
        {
            Command = EnumCmdType.LOGIN_RESPONSE;
            Results = result;
        }

        [JsonProperty("results", Order = 9)]
        public LoginResultModel Results { get; set; }

        public void Insert(bool success, string msg, LoginResultModel result)
        {
            Success = success;
            Message = msg;
            Results = result;
        }
    }
}
