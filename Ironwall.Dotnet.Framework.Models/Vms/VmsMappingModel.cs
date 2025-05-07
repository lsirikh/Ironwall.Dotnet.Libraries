using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Vms
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/11/2024 4:43:37 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsMappingModel : BaseModel, IVmsMappingModel
    {
        #region - Ctors -
        public VmsMappingModel()
        {
        }

        public VmsMappingModel(int id, int groupNumber, int eventId, EnumTrueFalse status) : base(id)
        {
            GroupNumber = groupNumber;
            EventId = eventId;
            Status = status;
        }

        public VmsMappingModel(IVmsMappingModel model) : base(model)
        {
            GroupNumber = model.GroupNumber;
            EventId = model.EventId;
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
        [JsonProperty("event_id", Order = 3)]
        public int EventId { get; set; }
        [JsonProperty("status", Order = 4)]
        public EnumTrueFalse Status { get; set; } = EnumTrueFalse.True;
        #endregion
        #region - Attributes -
        #endregion
    }
}