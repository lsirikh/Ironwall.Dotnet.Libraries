using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public class ServerModel : BaseModel, IServerModel
{
    public ServerModel() { }

    public ServerModel(IServerModel model) : base(model)
    {
        CategoryId = model.CategoryId;
        Name = model.Name;
        Status = model.Status;
        IpAddress = model.IpAddress;
        Port = model.Port;
        Hostname = model.Hostname;
        UserName = model.UserName;
        UserPassword = model.UserPassword;
        ThresholdConfig = model.ThresholdConfig as ServerThresholdConfigModel;
    }

    [JsonProperty("category_id", Order = 2)]
    public int CategoryId { get; set; }

    [JsonProperty("name", Order = 3)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("status", Order = 4)]
    public string Status { get; set; } = "NORMAL";

    [JsonProperty("ip_address", Order = 5)]
    public string IpAddress { get; set; } = string.Empty;

    [JsonProperty("port", Order = 6)]
    public int Port { get; set; }

    [JsonProperty("hostname", Order = 7)]
    public string? Hostname { get; set; }

    [JsonProperty("user_name", Order = 8)]
    public string? UserName { get; set; }

    [JsonProperty("user_password", Order = 9)]
    public string? UserPassword { get; set; }

    [JsonProperty("threshold_config", Order = 10)]
    public ServerThresholdConfigModel? ThresholdConfig { get; set; }

    IServerThresholdConfigModel? IServerModel.ThresholdConfig
    {
        get => ThresholdConfig;
        set => ThresholdConfig = value as ServerThresholdConfigModel;
    }
}
