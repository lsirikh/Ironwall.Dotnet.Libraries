using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wpf.Pids.Proxy.Master.DTO.Integrations
{
    public class CameraEventPresetDto
    {
        /// <summary>
        /// 카메라 번호
        /// </summary>
        [JsonProperty("cam_id")]
        public int CamId { get; set; }

        /// <summary>
        /// RTSP URLs
        /// </summary>
        [JsonProperty("urls")]
        public EventUrlsDto Urls { get; set; } = new EventUrlsDto();

        /// <summary>
        /// 카메라 종류 (EnumCameraType: "NONE", "FIXED", "PTZ")
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; } = "NONE";

        /// <summary>
        /// 프리셋 번호
        /// </summary>
        [JsonProperty("preset_id")]
        public string PresetId { get; set; } = "";

        /// <summary>
        /// 프리셋 이동 시간(초)
        /// </summary>
        [JsonProperty("preset_time")]
        public int MovePresetTime { get; set; }

        /// <summary>
        /// 복귀 프리셋 번호
        /// </summary>
        [JsonProperty("home_preset")]
        public int HomePreset { get; set; }

        /// <summary>
        /// 복귀 프리셋 이동 시간(초)
        /// </summary>
        [JsonProperty("home_time")]
        public int MoveHomeTime { get; set; }
    }
}
