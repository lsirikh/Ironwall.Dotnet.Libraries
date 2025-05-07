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
       Created On   : 7/5/2024 10:17:05 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiMappingListResponseModel : ResponseModel, IVmsApiMappingListResponseModel
    {
        public VmsApiMappingListResponseModel()
        {

        }

        public VmsApiMappingListResponseModel(bool success, string msg, List<IVmsMappingModel> list)
            : base(EnumCmdType.API_MAPPING_LIST_RESPONSE, success, msg)
        {
            Body = list.OfType<VmsMappingModel>().ToList();
        }

        [JsonProperty("body", Order = 4)]
        public List<VmsMappingModel>? Body { get; set; }
    }
}