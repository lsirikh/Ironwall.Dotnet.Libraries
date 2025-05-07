using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using Sensorway.Events.Base.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/5/2024 9:48:09 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiGetEventListResponseModel : ResponseModel, IVmsApiGetEventListResponseModel
    {
        public VmsApiGetEventListResponseModel()
        {

        }

        public VmsApiGetEventListResponseModel(bool success, string msg, List<Sensorway.Events.Base.Models.IEventModel> list)
            : base(EnumCmdType.API_LIST_EVENT_RESPONSE, success, msg)
        {
            Body = list.OfType<Sensorway.Events.Base.Models.EventModel>().ToList();
        }

        [JsonProperty("body", Order = 4)]
        public List<Sensorway.Events.Base.Models.EventModel>? Body { get; set; }
    }
}