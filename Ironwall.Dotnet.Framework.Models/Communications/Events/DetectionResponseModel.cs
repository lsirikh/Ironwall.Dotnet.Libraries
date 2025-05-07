using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class DetectionResponseModel 
        : ResponseModel, IDetectionResponseModel
    {
        public DetectionResponseModel()
        {
            Command = EnumCmdType.EVENT_DETECTION_RESPONSE;
        }

        public DetectionResponseModel(bool success, string msg, IDetectionRequestModel model = null)
            : base(success, msg)
        {
            Command = EnumCmdType.EVENT_DETECTION_RESPONSE;
            
            RequestModel = model is DetectionRequestModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(DetectionRequestModel)} 타입이어야 합니다.");
        }

        [JsonProperty("request_model", Order = 4)]
        public DetectionRequestModel? RequestModel { get; set; }

    }
}
