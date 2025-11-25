using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations
{
    public class SensorEventMapping: EventMappingDto
    {
        /// <summary>
        /// 제어기 번호
        /// </summary>
        [JsonProperty("controller", Order = 9)]
        public int Controller { get; set; }

        /// <summary>
        /// 센서 번호
        /// </summary>
        [JsonProperty("sensor", Order = 10)]
        public int Sensor { get; set; }

        /// <summary>
        /// 신호 크기
        /// </summary>
        [JsonProperty("signal", Order = 11)]
        public int Signal { get; set; }

        /// <summary>
        /// 그룹 번호
        /// </summary>
        [JsonProperty("middleware_group", Order = 12)]
        public int MiddlewareGroup { get; set; }

        /// <summary>
        /// 디바이스 타입 (EnumDeviceType: "Multi", "Fence", "Underground", "Contact", "PIR", etc.)
        /// </summary>
        [JsonProperty("type_device", Order = 13)]
        public string TypeDevice { get; set; } = string.Empty;
    }
}
