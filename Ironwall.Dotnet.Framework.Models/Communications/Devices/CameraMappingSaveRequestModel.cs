using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 8/4/2023 10:45:47 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraMappingSaveRequestModel : UserSessionBaseRequestModel, ICameraMappingSaveRequestModel
    {

        #region - Ctors -
        public CameraMappingSaveRequestModel()
        {
            Command = EnumCmdType.CAMERA_MAPPING_SAVE_REQUEST;
        }

        public CameraMappingSaveRequestModel(ILoginSessionModel model, List<ICameraMappingModel> mappings, EnumCmdType cmd = EnumCmdType.CAMERA_MAPPING_SAVE_REQUEST)
         : base(model, cmd)
        {
            Body = mappings.OfType<CameraMappingModel>().ToList();
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
        [JsonProperty("body", Order = 5)]
        public List<CameraMappingModel> Body { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
