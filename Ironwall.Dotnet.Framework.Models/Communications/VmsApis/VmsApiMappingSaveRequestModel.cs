using Ironwall.Dotnet.Framework.Models.Vms;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 10:17:49 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiMappingSaveRequestModel : BaseMessageModel, IVmsApiMappingSaveRequestModel
    {
        public VmsApiMappingSaveRequestModel()
        {
        }


        public VmsApiMappingSaveRequestModel(List<IVmsMappingModel> list)
            : base(EnumCmdType.API_MAPPING_SAVE_REQUEST)
        {
            Body = list.OfType<VmsMappingModel>().ToList();
        }

        [JsonProperty("body", Order = 2)]
        public List<VmsMappingModel>? Body { get; set; }
    }
}