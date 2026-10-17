using Ironwall.Dotnet.Monitoring.Models.Servers;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ISpeakerDeviceModel : IBaseDeviceModel
{
    string SpeakerType { get; set; }
    string? Description { get; set; }
    IServerModel? Server { get; set; }

    /// <summary>접속 IP — 서버 7.0+ <c>connection.ip_address</c>(D-21). 방송서버 경유(<c>server_id</c>)와 별개다.</summary>
    string? IpAddress { get; set; }

    /// <summary>접속 포트 — 서버 7.0+ <c>connection.ip_port</c>(D-21).</summary>
    int? IpPort { get; set; }
}
