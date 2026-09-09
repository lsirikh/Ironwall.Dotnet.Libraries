using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// 통문·함체 문 개폐 <b>명령</b> 요청 body — 서버 `GateControl` / `EnclosureControl` 대응.
/// <para><c>POST /api/devices/gates/{id}/control</c> · <c>POST /api/devices/enclosures/{id}/control</c></para>
/// <para><b>이 요청은 상태를 바꾸지 않는다.</b> 서버는 명령을 NATS(<c>GATE_DOOR_SET</c> /
/// <c>ENCLOSURE_DOOR_SET</c>)로 전파만 하고, 실제 개폐는 담당 매니저가 <c>PATCH /{id}/status</c> 로
/// 보고할 때 성립한다(operation-event PRD v1.5 FR-15). 응답의 <c>gate_status</c>/<c>door_status</c> 는
/// 명령 <b>이전</b> 값이므로 이것으로 화면 상태를 갱신하면 안 된다.</para>
/// </summary>
public class DoorControlRequestDto
{
    /// <summary>열림 명령 값(서버 `EnumDoorCommand`).</summary>
    public const string Open = "OPEN";

    /// <summary>닫힘 명령 값(서버 `EnumDoorCommand`).</summary>
    public const string Close = "CLOSE";

    public DoorControlRequestDto() { }

    public DoorControlRequestDto(string doorCommand) => DoorCommand = doorCommand;

    /// <summary>문 개폐 명령 — <see cref="Open"/> 또는 <see cref="Close"/>.</summary>
    [JsonProperty("door_command")]
    public string DoorCommand { get; set; } = Close;
}
