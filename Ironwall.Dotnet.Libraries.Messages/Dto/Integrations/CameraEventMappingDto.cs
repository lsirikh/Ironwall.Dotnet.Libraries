using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wpf.Pids.Proxy.Master.DTO.Integrations;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations
{
    public class CameraEventMappingDto: EventMappingDto
    {
        /// <summary>
        /// 카메라 번호
        /// </summary>
        [JsonProperty("camera_presets")]
        List<CameraEventPresetDto> CameraPresets = new List<CameraEventPresetDto>();
    }
}
