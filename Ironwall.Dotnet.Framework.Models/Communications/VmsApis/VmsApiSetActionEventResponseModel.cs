using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:49:05 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiSetActionEventResponseModel : ResponseModel, IVmsApiSetActionEventResponseModel
    {
        public VmsApiSetActionEventResponseModel()
        {
        }

        public VmsApiSetActionEventResponseModel(bool success, string msg)
            : base(EnumCmdType.API_ACT_EVENT_RESPONSE, success, msg)
        {
        }
    }
}