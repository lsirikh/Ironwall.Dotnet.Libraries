using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Servers;

public class ServerModel : BaseModel, IServerModel
{
    public ServerModel() { }

    public ServerModel(IServerModel model) : base(model)
    {
        CategoryId = model.CategoryId;
        if (model is ServerModel server) CategoryServer = server.CategoryServer;
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

    /// <summary>
    /// 7.0+ 서버 유형 판별자(<c>category_server</c> — 예: <c>SPEAKER_API</c> · <c>VMS</c>). 표시 · 판정용이라 직렬화하지 않는다.
    /// 6.3 응답에는 없어 <c>null</c> 이다 — 그때는 유형으로 거르지 못한다(장비 콘솔의 관리 서버 선택지 · 기본값 판정).
    /// </summary>
    [JsonIgnore]
    public string? CategoryServer { get; set; }

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
