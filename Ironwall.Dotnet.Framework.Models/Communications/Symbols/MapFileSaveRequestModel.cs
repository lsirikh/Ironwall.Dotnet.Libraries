using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 10/10/2023 8:44:54 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MapFileSaveRequestModel : UserSessionBaseRequestModel, IMapFileSaveRequestModel
    {
        #region - Ctors -
        public MapFileSaveRequestModel()
        {
            Command = EnumCmdType.MAP_FILE_SAVE_REQUEST;
        }

        public MapFileSaveRequestModel(ILoginSessionModel model, List<MapModel> maps)
            : base(model)
        {
            Command = EnumCmdType.MAP_FILE_SAVE_REQUEST;
            Maps = maps;
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
        [JsonProperty("maps", Order = 5)]
        public List<MapModel>? Maps { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
