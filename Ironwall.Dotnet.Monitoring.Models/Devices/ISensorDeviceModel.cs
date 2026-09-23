namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface ISensorDeviceModel : IBaseDeviceModel
{
    IControllerDeviceModel? Controller { get; set; }

    /// <summary>접속 IP — 서버 7.0+ <c>connection.ip_address</c>(D-21). IP 기반 센서만 쓴다.</summary>
    string? IpAddress { get; set; }

    /// <summary>접속 포트 — 서버 7.0+ <c>connection.ip_port</c>(D-21).</summary>
    int? IpPort { get; set; }

    /// <summary>RS485 버스 주소(D13) — 접점 채널이 아니라 그 제어기 버스 안에서의 노드 주소다.</summary>
    int? Channel { get; set; }
}