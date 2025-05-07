using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountIdCheckResponse
        : ResponseModel
    {
        public AccountIdCheckResponse()
        {
            Command = EnumCmdType.USER_ACCOUNT_ID_CHECK_RESPONSE;
        }

        public AccountIdCheckResponse(bool success, string message)
        {
            Command = EnumCmdType.USER_ACCOUNT_ID_CHECK_RESPONSE;
            Success = success;
            Message = message;
        }
    }
}
