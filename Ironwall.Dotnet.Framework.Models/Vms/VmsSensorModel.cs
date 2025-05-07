using Ironwall.Dotnet.Framework.Models.Communications.Helpers;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Vms
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2024 4:03:53 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsSensorModel : BaseModel, IVmsSensorModel
    {
        #region - Ctors -
        public VmsSensorModel()
        {

        }

        public VmsSensorModel(int id, int groupNumver, IBaseDeviceModel device, EnumTrueFalse status) : base(id)
        {
            GroupNumber = groupNumver;
            Device = device as BaseDeviceModel;
            Status = status;
        }

        public VmsSensorModel(IVmsSensorModel model) : base(model)
        {
            GroupNumber= model.GroupNumber;
            Device = model.Device;
            Status = model.Status;
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
        [JsonProperty("group_number", Order = 2)]
        public int GroupNumber { get; set; }
        [JsonProperty("device", Order = 3)]
        [JsonConverter(typeof(DeviceModelConverter))] // JsonConverter 추가
        public BaseDeviceModel Device { get; set; }
        [JsonProperty("status", Order = 4)]
        public EnumTrueFalse Status { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}