using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Devices
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/14/2024 3:07:40 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DeviceMaxIdResponseModel : ResponseModel
    {
        #region - Ctors -
        DeviceMaxIdResponseModel()
        {
            Command = EnumCmdType.DEVICE_MAX_ID_RESPONSE;
        }

        DeviceMaxIdResponseModel(bool success, string content, IDeviceDetailModel detail)
            : base(EnumCmdType.DEVICE_MAX_ID_RESPONSE, success, content)
        {
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
        [JsonProperty("max_id", Order = 4)]
        public int MaxId { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}