using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations
{
    public class CameraEventMappingDto: EventMappingDto
    {
        /// <summary>
        /// 카메라 번호
        /// </summary>
        [JsonProperty("camId", Order = 9)]
        public int[] CamId { get; set; } = Array.Empty<int>();

        /// <summary>
        /// 프리셋 번호
        /// </summary>
        [JsonProperty("presetId", Order = 10)]
        public string[] PresetId { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 프리셋 이동 시간(초)
        /// </summary>
        [JsonProperty("movePresetTime", Order = 11)]
        public int[] MovePresetTime { get; set; } = Array.Empty<int>();

        /// <summary>
        /// 복귀 프리셋 번호
        /// </summary>
        [JsonProperty("homePreset", Order = 12)]
        public int[] HomePreset { get; set; } = Array.Empty<int>();

        /// <summary>
        /// 복귀 프리셋 이동 시간(초)
        /// </summary>
        [JsonProperty("moveHomeTime", Order = 13)]
        public int[] MoveHomeTime { get; set; } = Array.Empty<int>();
    }
}
