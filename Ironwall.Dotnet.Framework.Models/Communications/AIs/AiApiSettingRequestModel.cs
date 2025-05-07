using Ironwall.Dotnet.Framework.Models.Ais;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.AIs
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 1/14/2025 6:55:56 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class AiApiSettingRequestModel : BaseMessageModel, IAiApiSettingRequestModel
    {
        #region - Ctors -
        public AiApiSettingRequestModel()
            : base(EnumCmdType.AI_API_SETTING_REQUEST)
        {
        }

        public AiApiSettingRequestModel(INetworkSettingModel model)
            : base(EnumCmdType.AI_API_SETTING_REQUEST)
        {
            SettingModel = (NetworkSettingModel)model;
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
        [JsonProperty("ai_api_setting", Order = 1)]
        public NetworkSettingModel SettingModel { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}