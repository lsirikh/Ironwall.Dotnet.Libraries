using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.NVR_emstone.Camera
{
    public class CameraDto:BaseDto
    {
        /// <summary>
        /// 카메라 이름
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 이름
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 주소
        /// </summary>
        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 위치
        /// </summary>
        [JsonProperty("location")]
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// 네트워크 카메라 소스 ID
        /// </summary>
        [JsonProperty("source")]
        public int Source { get; set; }

        /// <summary>
        /// 네트워크 카메라 소스 채널 ID
        /// </summary>
        [JsonProperty("channel")]
        public int Channel { get; set; }

        /// <summary>
        /// 카메라 연결 여부
        /// </summary>
        [JsonProperty("connected")]
        public bool IsConnected { get; set; }

        /// <summary>
        /// 카메라 신호 유효 여부
        /// </summary>
        [JsonProperty("has_signal")]
        public bool HasSignal { get; set; }

        /// <summary>
        /// PTZ 기능 사용 가능 여부
        /// </summary>
        [JsonProperty("has_ptz")]
        public bool HasPtz { get; set; }

        /// <summary>
        /// 녹화 작동 여부
        /// </summary>
        [JsonProperty("recording")]
        public bool IsRecording { get; set; }

        /// <summary>
        /// 강제 녹화 작동 여부
        /// </summary>
        [JsonProperty("force_recording")]
        public bool IsForceRecording { get; set; }

        /// <summary>
        /// PTZ 프리셋 리스트
        /// </summary>
        [JsonProperty("ptz_presets")]
        public List<Dictionary<string, Dictionary<string,string>>> PtzPresets { get; set; } = new List<Dictionary<string, Dictionary<string,string>>>();

        /// <summary>
        /// PTZ 투어 설정 리스트
        /// </summary>
        [JsonProperty("ptz_tours")]
        public List<Dictionary<string, List<CameraPTZTourDto>>> PtzTours { get; set; } = new List<Dictionary<string, List<CameraPTZTourDto>>>();

        /// <summary>
        /// 스트리밍 활성화 여부
        /// </summary>
        [JsonProperty("streaming")]
        public bool IsStreaming { get; set; }

        /// <summary>
        /// 카메라 HTTP URL (스냅샷/뷰어 등)
        /// </summary>
        [JsonProperty("http_url")]
        public string HttpUrl { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 비고
        /// </summary>
        [JsonProperty("note")]
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 OSD 설정 객체
        /// </summary>
        [JsonProperty("osd")]
        public CameraOSDDto OsdSettings { get; set; } = new CameraOSDDto();

        /// <summary>
        /// PTZ 타입 (AUTO / NONE / PTZ / ZOOM)
        /// </summary>
        [JsonProperty("ptz_type")]
        public string PtzType { get; set; } = "NONE";

        /// <summary>
        /// 카메라 설치 목적
        /// </summary>
        [JsonProperty("purpose")]
        public string Purpose { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 외형 (BULLET / DOME / BOX / PTZ)
        /// </summary>
        [JsonProperty("shape")]
        public string Shape { get; set; } = string.Empty;

        /// <summary>
        /// 카메라 왜곡 보정 (Dewarping)
        /// </summary>
        [JsonProperty("dewap")] // JSON 키는 원본 유지 (오타 dewap)
        public string Dewarp { get; set; } = string.Empty;
    }
}
