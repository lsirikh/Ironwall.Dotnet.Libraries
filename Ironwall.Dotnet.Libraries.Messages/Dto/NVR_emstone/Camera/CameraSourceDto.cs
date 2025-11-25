using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.NVR_emstone.Camera
{
    public class CameraSourceDto: BaseDto
    {
        /// <summary>
        /// 카메라 이름
        /// </summary>
        [JsonProperty("name", Order = 2)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 주소
        /// </summary>
        [JsonProperty("address", Order = 3)]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 mac 주소
        /// </summary>
        [JsonProperty("mac", Order = 4)]
        public string Mac { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 위치
        /// </summary>
        [JsonProperty("location", Order = 5)]
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 채널 리스트
        /// </summary>
        [JsonProperty("channels", Order = 6)]
        public List<Dictionary<string, int>> Channels { get; set; } = new List<Dictionary<string, int>>();

    }
}
