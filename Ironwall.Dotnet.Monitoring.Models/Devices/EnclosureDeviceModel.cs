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
        HeaterComponentKey = model.HeaterComponentKey;
        FanComponentKey = model.FanComponentKey;
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

    /// <summary>히터 부품의 서버 선언 key — 읽기 전용 캐시(<see cref="IEnclosureDeviceModel.HeaterComponentKey"/> 참조).
    /// 로컬 저장·IPC 용도가 아니라 매 읽기마다 새로 채워지므로 직렬화하지 않는다.</summary>
    [JsonIgnore]
    public string? HeaterComponentKey { get; set; }

    /// <summary>팬 부품의 서버 선언 key — <see cref="HeaterComponentKey"/> 와 같은 계약.</summary>
    [JsonIgnore]
    public string? FanComponentKey { get; set; }
}
