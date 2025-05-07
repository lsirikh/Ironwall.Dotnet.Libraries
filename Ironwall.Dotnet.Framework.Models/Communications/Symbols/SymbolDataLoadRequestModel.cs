using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public class SymbolDataLoadRequestModel
        : UserSessionBaseRequestModel, ISymbolDataLoadRequestModel
    {
        public SymbolDataLoadRequestModel()
        {
            Command = EnumCmdType.SYMBOL_DATA_LOAD_REQUEST;
        }

        public SymbolDataLoadRequestModel(ILoginSessionModel model)
            : base(model)
        {
            Command = EnumCmdType.SYMBOL_DATA_LOAD_REQUEST;
        }
    }
}
