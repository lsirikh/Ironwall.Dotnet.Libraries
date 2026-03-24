using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
public class TrackingTargetDto : BaseDto
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
    /// 타겟 타입
    /// </summary>
    [JsonProperty("type_target", Order = 2)]
    public string TypeTarget { get; set; } = string.Empty;

    /// <summary>
    /// 위도
    /// </summary>
    [JsonProperty("latitude", Order = 3)]
    public double Latitude { get; set; }

    /// <summary>
    /// 경도
    /// </summary>
    [JsonProperty("longitude", Order = 4)]
    public double Longitude { get; set; }

    /// <summary>
    /// 고도 (편의 속성)
    /// </summary>
    [JsonProperty("altitude", Order = 5)]
    public double Altitude { get; set; }

}
