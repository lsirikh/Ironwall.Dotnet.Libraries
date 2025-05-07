using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Accounts
{
    public class AccountIdCheckResponseModel 
        : ResponseModel
    {
        public AccountIdCheckResponseModel()
        {
            Command = EnumCmdType.USER_ACCOUNT_ID_CHECK_RESPONSE;
        }

        public AccountIdCheckResponseModel(bool success, string message)
            : base(success, message)
        {
            Command = EnumCmdType.USER_ACCOUNT_ID_CHECK_RESPONSE;
        }
    }
}
