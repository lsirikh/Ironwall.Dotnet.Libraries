using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/18/2023 4:42:10 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolDataSaveResponseModel
        : ResponseModel, ISymbolDataSaveResponseModel
    {
        #region - Ctors -
        public SymbolDataSaveResponseModel()
        {
            Command = EnumCmdType.SYMBOL_DATA_SAVE_RESPONSE;
        }

        public SymbolDataSaveResponseModel(bool success, string content, ISymbolMoreDetailModel detail)
            : base(success, content)
        {
            Command = EnumCmdType.SYMBOL_DATA_SAVE_RESPONSE;
            Detail = ResponseFactory.Build<SymbolMoreDetailModel>(detail);
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
        [JsonProperty("detail", Order = 4)]
        public SymbolMoreDetailModel? Detail { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
