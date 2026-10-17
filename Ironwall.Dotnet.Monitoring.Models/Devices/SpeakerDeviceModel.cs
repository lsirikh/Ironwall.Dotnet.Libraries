using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class SpeakerDeviceModel : BaseDeviceModel, ISpeakerDeviceModel
{
    public SpeakerDeviceModel()
    {
        DeviceType = EnumDeviceType.IpSpeaker;
    }

    public SpeakerDeviceModel(ISpeakerDeviceModel model) : base(model)
    {
        SpeakerType = model.SpeakerType;
        Description = model.Description;
        Server = model.Server;
        IpAddress = model.IpAddress;
        IpPort = model.IpPort;
    }

    [JsonProperty("speaker_type", Order = 7)]
    public string SpeakerType { get; set; } = "NORMAL";

    [JsonProperty("description", Order = 8)]
    public string? Description { get; set; }

    [JsonIgnore]
    public IServerModel? Server { get; set; }

    /// <summary>접속 IP(D-21) — 서버 7.0+ <c>connection.ip_address</c>. 6.3 에는 자리가 없었다.</summary>
    [JsonProperty("ip_address", Order = 9)]
    public string? IpAddress { get; set; }

    /// <summary>접속 포트(D-21) — 서버 7.0+ <c>connection.ip_port</c>.</summary>
    [JsonProperty("ip_port", Order = 10)]
    public int? IpPort { get; set; }
}
