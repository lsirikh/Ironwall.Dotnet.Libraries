using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 10:16:49 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiMappingListRequestModel : BaseMessageModel, IVmsApiMappingListRequestModel
    {
        public VmsApiMappingListRequestModel()
            : base(EnumCmdType.API_MAPPING_LIST_REQUEST)
        {
        }
    }
}