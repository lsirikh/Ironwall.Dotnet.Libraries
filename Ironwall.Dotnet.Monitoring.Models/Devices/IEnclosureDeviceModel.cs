namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public interface IEnclosureDeviceModel : IBaseDeviceModel
{
    string DoorStatus { get; set; }
    IEnclosureThresholdConfigModel? ThresholdConfig { get; set; }
    bool HeaterEnabled { get; set; }
    bool FanEnabled { get; set; }

    /// <summary>접속 IP — 서버 7.0+ <c>connection.ip_address</c>(D-21). 함체도 IP_DIRECT 접속을 가질 수 있다.</summary>
    string? IpAddress { get; set; }

    /// <summary>접속 포트 — 서버 7.0+ <c>connection.ip_port</c>(D-21).</summary>
    int? IpPort { get; set; }
}
