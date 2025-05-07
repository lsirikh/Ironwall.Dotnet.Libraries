using Ironwall.Dotnet.Framework.Helpers;
using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Vms;
using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Mappers.Vms
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/3/2024 4:00:05 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsSensorMapper : BaseModel, IVmsSensorMapper
    {
        #region - Ctors -
        public VmsSensorMapper()
        {

        }

        public VmsSensorMapper(int id, int groupNumber, BaseDeviceModel device, EnumTrueFalse status) : base(id)
        {
            GroupNumber = groupNumber;
            Device = device.Id;
            Status = EnumHelper.GetStatusType(status);
        }

        public VmsSensorMapper(IVmsSensorModel model) : base(model)
        {
            GroupNumber = model.GroupNumber;
            Device = model.Device.Id;
            Status = EnumHelper.GetStatusType(model.Status);
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
        public int GroupNumber { get; set; }
        public int Device { get; set; }
        public bool Status { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}