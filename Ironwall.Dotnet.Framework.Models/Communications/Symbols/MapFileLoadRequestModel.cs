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
        Created On   : 5/26/2023 2:31:59 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MapFileLoadRequestModel : UserSessionBaseRequestModel, IMapFileLoadRequestModel
    {
        #region - Ctors -
        public MapFileLoadRequestModel()
        {
            Command = EnumCmdType.MAP_FILE_LOAD_REQUEST;
        }

        public MapFileLoadRequestModel(ILoginSessionModel model) : base(model)
        {
            Command = EnumCmdType.MAP_FILE_LOAD_REQUEST;
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
