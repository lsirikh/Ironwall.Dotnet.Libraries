using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/26/2023 2:35:31 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MapFileLoadResponseModel : ResponseModel, IMapFileLoadResponseModel
    {
        #region - Ctors -
        public MapFileLoadResponseModel()
        {
            Command = EnumCmdType.MAP_FILE_LOAD_RESPONSE;
        }

        public MapFileLoadResponseModel(bool success, string content, List<MapModel> maps)
            : base(success, content)
        {
            Command = EnumCmdType.MAP_FILE_LOAD_RESPONSE;
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
        [JsonProperty("maps", Order = 4)]
        public List<MapModel>? Maps { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
