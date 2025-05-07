using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class LoginRequestModel 
        : BaseMessageModel, ILoginRequestModel
    {
        public LoginRequestModel()
        {
            Command = EnumCmdType.LOGIN_REQUEST;
        }

        public LoginRequestModel(string userId, string userPass, bool isForceLogin)
        {
            Command = EnumCmdType.LOGIN_REQUEST;

            UserId = userId;
            UserPass = userPass;
            IsForceLogin = isForceLogin;
        }

        public LoginRequestModel(IUserModel model, bool isForceLogin)
        {
            Command = EnumCmdType.LOGIN_REQUEST;
            
            UserId = model.IdUser;
            UserPass = model.Password;
            IsForceLogin = isForceLogin;
        }

        [JsonProperty("user_id", Order = 1)]
        public string UserId { get; set; }
        [JsonProperty("user_pass", Order = 2)]
        public string UserPass { get; set; }
        [JsonProperty("force_login", Order = 3)]
        public bool IsForceLogin { get; set; }

        public void Insert(string userId, string userPass, bool isForceLogin)
        {
            UserId = userId;
            UserPass = userPass;
            IsForceLogin = isForceLogin;
        }
    }
}
