using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.NVR_emstone.Camera
{
    public class CameraPTZTourDto
    {
        /// <summary>
        /// 프리셋 이름
        /// </summary>
        [JsonProperty("preset")]
        public int Preset { get; set; }
        /// <summary>
        /// 지속 시간
        /// </summary>
        [JsonProperty("duration")]
        public int Duration { get; set; }
        /// <summary>
        /// 이동 속도
        /// </summary>
        [JsonProperty("speed")]
        public int Speed { get; set; }
    }
}
