using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.NVR_emstone.Camera
{
    public class CameraOSDDto
    {
        /// <summary>
        /// OSD 텍스트 내용
        /// </summary>
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
        /// <summary>
        /// Font 크기
        /// </summary>
        [JsonProperty("size")]
        public int Size { get; set; }
        /// <summary>
        /// Font 색상 (예: "#FFFFFF" 형식)
        /// </summary>
        [JsonProperty("color")]
        public string Color { get; set; } = string.Empty;
        /// <summary>
        /// Font 위치 (예: "top-left","top-center" ,"top-right", "bottom-left","bottom-center" ,"bottom-right")
        /// </summary>
        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;
        /// <summary>
        /// 날자 형식
        /// </summary>
        [JsonProperty("date_format")]
        public string DataFormat { get; set; } = string.Empty;
        /// <summary>
        /// 시간 형식
        /// </summary>
        [JsonProperty("time_format")]
        public string TimeFormat { get; set; } = string.Empty;
    }
}
