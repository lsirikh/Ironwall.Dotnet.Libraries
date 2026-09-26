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
        // 값을 옮기면 "알려짐"이 켜지므로, 원본이 "설정 없음"이었으면 그 사실을 다시 옮긴다.
        if (model is EnclosureDeviceModel source)
        {
            HeaterEnabledKnown = source.HeaterEnabledKnown;
            FanEnabledKnown = source.FanEnabledKnown;
        }
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

    /// <summary>히터 동작 의도. 값을 쓰면(사람이 켜거나 끄면) <see cref="HeaterEnabledKnown"/> 이 켜진다.</summary>
    [JsonProperty("heater_enabled", Order = 9)]
    public bool HeaterEnabled
    {
        get => _heaterEnabled;
        set { _heaterEnabled = value; HeaterEnabledKnown = true; }
    }

    /// <summary>팬 동작 의도. 값을 쓰면(사람이 켜거나 끄면) <see cref="FanEnabledKnown"/> 이 켜진다.</summary>
    [JsonProperty("fan_enabled", Order = 10)]
    public bool FanEnabled
    {
        get => _fanEnabled;
        set { _fanEnabled = value; FanEnabledKnown = true; }
    }

    /// <summary>
    /// <see cref="HeaterEnabled"/> 가 실제 의도인가 — <c>false</c> 면 서버에 이 부품의 <c>enabled</c> 설정이 없고
    /// 아무도 만지지 않았다("설정 없음"). 이때 <see cref="HeaterEnabled"/> 의 <c>false</c> 는 기본값일 뿐이라
    /// 저장 본문에 싣지 않는다.
    /// </summary>
    /// <remarks>
    /// 7.0+ 는 히터 · 팬 설정을 <c>device_config.component_overrides.{key}.enabled</c> 로 두고, 없으면 "무동작 · 설정 없음"이다.
    /// <c>bool</c> 하나로는 그 상태를 못 적어, 아무것도 고치지 않은 저장이 설정 없는 팬에 <c>enabled:false</c> 를 지어냈다
    /// (라이브 하네스 dl.4 / asm.R4b3, 2026-09-26). 기본값은 <c>true</c> — 새로 만든 모델과 6.3(평면 bool) 은 늘 값이 있다.
    /// 로컬 저장 · IPC 로 나가지 않는다(읽을 때마다 새로 정해진다).
    /// </remarks>
    [JsonIgnore]
    public bool HeaterEnabledKnown { get; set; } = true;

    /// <summary><see cref="FanEnabled"/> 가 실제 의도인가 — <see cref="HeaterEnabledKnown"/> 과 같은 계약.</summary>
    [JsonIgnore]
    public bool FanEnabledKnown { get; set; } = true;

    private bool _heaterEnabled;
    private bool _fanEnabled;

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
