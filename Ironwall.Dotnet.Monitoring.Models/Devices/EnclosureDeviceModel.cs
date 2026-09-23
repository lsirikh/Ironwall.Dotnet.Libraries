using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class EnclosureDeviceModel : BaseDeviceModel, IEnclosureDeviceModel
{
    public EnclosureDeviceModel()
    {
        DeviceType = EnumDeviceType.Enclosure;
    }

    public EnclosureDeviceModel(IEnclosureDeviceModel model) : base(model)
    {
        DoorStatus = model.DoorStatus;
        ThresholdConfig = model.ThresholdConfig as EnclosureThresholdConfigModel;
        HeaterEnabled = model.HeaterEnabled;
        FanEnabled = model.FanEnabled;
        IpAddress = model.IpAddress;
        IpPort = model.IpPort;
    }

    [JsonProperty("door_status", Order = 7)]
    public string DoorStatus { get; set; } = "CLOSED";

    [JsonProperty("threshold_config", Order = 8)]
    public EnclosureThresholdConfigModel? ThresholdConfig { get; set; }

    IEnclosureThresholdConfigModel? IEnclosureDeviceModel.ThresholdConfig
    {
        get => ThresholdConfig;
        set => ThresholdConfig = value as EnclosureThresholdConfigModel;
    }

    [JsonProperty("heater_enabled", Order = 9)]
    public bool HeaterEnabled { get; set; }

    [JsonProperty("fan_enabled", Order = 10)]
    public bool FanEnabled { get; set; }

    /// <summary>접속 IP(D-21) — 서버 7.0+ <c>connection.ip_address</c>. 6.3 에는 자리가 없었다.</summary>
    [JsonProperty("ip_address", Order = 11)]
    public string? IpAddress { get; set; }

    /// <summary>접속 포트(D-21) — 서버 7.0+ <c>connection.ip_port</c>.</summary>
    [JsonProperty("ip_port", Order = 12)]
    public int? IpPort { get; set; }
}
