using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:55:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SensorDeviceModel : BaseDeviceModel, ISensorDeviceModel
{
    public SensorDeviceModel()
    {
        Controller = new ControllerDeviceModel();
    }

    [JsonProperty("controller", Order = 6)]
    [JsonConverter(typeof(DeviceModelConverter))]
    public IControllerDeviceModel Controller { get; set; }

    /// <summary>접속 IP(D-21) — 서버 7.0+ <c>connection.ip_address</c>. IP 기반 센서만 쓴다.</summary>
    [JsonProperty("ip_address", Order = 7)]
    public string? IpAddress { get; set; }

    /// <summary>접속 포트(D-21) — 서버 7.0+ <c>connection.ip_port</c>.</summary>
    [JsonProperty("ip_port", Order = 8)]
    public int? IpPort { get; set; }

    /// <summary>
    /// RS485 버스 주소(D13) — 접점 채널이 아니라 그 제어기 버스 안에서의 노드 주소다.
    /// <see cref="IpAddress"/> 가 비어 있을 때만 <c>SensorDeviceDto.ConnectionAxis</c> 가 이 값으로 RS485 를 싣는다.
    /// </summary>
    [JsonProperty("channel", Order = 9)]
    public int? Channel { get; set; }
}