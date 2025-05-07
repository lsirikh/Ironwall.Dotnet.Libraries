using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using Ironwall.Redis.Message.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public class DetectionRequestModel : BaseMessageModel, IDetectionRequestModel
    {
        public DetectionRequestModel()
        {
            Command = EnumCmdType.EVENT_DETECTION_REQUEST;
        }

        /// <summary>
        /// Event Model로 부터 Request Model을 생성
        /// </summary>
        /// <param name="model">Detection Event Model</param>
        public DetectionRequestModel(IDetectionEventModel model) 
            : base(EnumCmdType.EVENT_DETECTION_REQUEST)
        {
            Body = model is DetectionEventModel eventModel ? eventModel : throw new InvalidCastException($"model은 {typeof(DetectionEventModel)} 타입이어야 합니다.");
        }

        [JsonProperty("detail", Order = 6)]
        public DetectionEventModel? Body { get; set; }
    }
}
