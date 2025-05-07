using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Framework.Models.Communications.Settings
{
    public class HeartBeatRequestModel
        : BaseMessageModel, IHeartBeatRequestModel
    {
        public HeartBeatRequestModel()
        {
            Command = EnumCmdType.HEART_BEAT_REQUEST;
        }

        public HeartBeatRequestModel(string ipAddress, int port)
        {
            Command = EnumCmdType.HEART_BEAT_REQUEST;
            IpAddress = ipAddress;
            Port = port;
        }

        [JsonProperty("ipAddress", Order = 1)]
        public string? IpAddress { get; set; } = string.Empty;
        [JsonProperty("port", Order = 2)]
        public int Port { get; set; }
    }
}
