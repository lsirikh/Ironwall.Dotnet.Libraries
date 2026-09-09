using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 통문(Gate) 장비 모델 — 함체(<see cref="EnclosureDeviceModel"/>) 대응물. 서버 v6.3 신설 타입.
/// <para><b>불변식</b>: <see cref="GateStatus"/> 는 개폐 <b>명령</b>으로 바뀌지 않는다.
/// `POST /api/devices/gates/{id}/control` 은 명령을 NATS 로 전파만 하고, 실제 상태는 담당 매니저가
/// `PATCH /{id}/status` 로 보고할 때 `OPERATION_EVENT` 와 함께 전이된다(operation-event PRD v1.5 FR-15).</para>
/// </summary>
public class GateDeviceModel : BaseDeviceModel, IGateDeviceModel
{
    public GateDeviceModel()
    {
        DeviceType = EnumDeviceType.Gate;
    }

    public GateDeviceModel(IGateDeviceModel model) : base(model)
    {
        GateStatus = model.GateStatus;
        UrlsJson = model.UrlsJson;
        LinkInfoJson = model.LinkInfoJson;
    }

    [JsonProperty("gate_status", Order = 7)]
    public string GateStatus { get; set; } = "CLOSED";

    [JsonProperty("urls", Order = 8)]
    public string? UrlsJson { get; set; }

    [JsonProperty("link_info", Order = 9)]
    public string? LinkInfoJson { get; set; }
}
