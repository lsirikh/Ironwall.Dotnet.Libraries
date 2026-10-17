using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.NVR_emstone.Camera
{
    public class CameraPresetDto
    {
        /// <summary>
        /// 프리셋 번호
        /// </summary>
        [JsonProperty("presetNum")]
        public string PresetNum { get; set; } = string.Empty;

        /// <summary>
        /// 프리셋 이름
        /// </summary>
        [JsonProperty("presetName")]
        public string PresetName { get; set; } = string.Empty;
    }
}
