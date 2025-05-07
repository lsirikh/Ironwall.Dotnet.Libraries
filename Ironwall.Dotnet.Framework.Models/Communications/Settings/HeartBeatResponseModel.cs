using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Settings
{
    public class HeartBeatResponseModel 
        : ResponseModel, IHeartBeatResponseModel
    {
        public HeartBeatResponseModel()
        {
            Command = EnumCmdType.HEART_BEAT_RESPONSE;
        }
        public HeartBeatResponseModel(bool success, string msg,string currentTime, string expiredTime)
            : base(success, msg)
        {
            Command = EnumCmdType.HEART_BEAT_RESPONSE;
            TimeCurrent = currentTime;
            TimeExpired = expiredTime;
        }

        [JsonProperty("current_time", Order = 4)]
        public string? TimeCurrent { get; set; } = string.Empty;

        [JsonProperty("expired_time", Order = 5)]
        public string? TimeExpired { get; set; } = string.Empty;
    }
}
