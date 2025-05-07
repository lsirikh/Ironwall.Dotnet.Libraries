using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Enums;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/15/2023 5:50:32 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolInfoRequestModel 
        : UserSessionBaseRequestModel, ISymbolInfoRequestModel
    {
        #region - Ctors -
        public SymbolInfoRequestModel()
        {
            Command = EnumCmdType.SYMBOL_DATA_INFO_REQUEST;
        }

        public SymbolInfoRequestModel(ILoginSessionModel model) : base(model)
        {
            Command = EnumCmdType.SYMBOL_DATA_INFO_REQUEST;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        #endregion
    }
}
