using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Camera_SPG
{
    public class PTZDto: BaseDto
    {
        /// <summary>
        /// 카메라 번호
        /// </summary>
        [JsonProperty("id_camera", Order = 2)]
        public int CameraId { get; set; }

        /// <summary>
        /// 카메라 이름
        /// </summary>
        [JsonProperty("name_camera", Order = 2)]
        public string CameraName { get; set; } = string.Empty;

        /// <summary>
        /// 팬 각도
        /// </summary>
        [JsonProperty("p", Order = 3)]
        public float P { get; set; }

        /// <summary>
        /// 틸트 각도
        /// </summary>
        [JsonProperty("t", Order = 4)]
        public float T { get; set; }


        /// <summary>
        /// 줌 배율
        /// </summary>
        [JsonProperty("z", Order = 5)]
        public float Z { get; set; }
    }
}
