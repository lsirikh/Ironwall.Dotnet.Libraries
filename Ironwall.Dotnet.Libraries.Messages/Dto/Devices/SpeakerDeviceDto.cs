using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Speaker(방송 장비) 디바이스 DTO (§5.4)
/// </summary>
public class SpeakerDeviceDto : BaseDeviceDto
{
    public SpeakerDeviceDto()
    {
        TypeDevice = "IpSpeaker";
    }

    /// <summary>
    /// 스피커 타입 (EnumSpeakerType: NORMAL, ADMIN, MONITOR, DEV)
    /// </summary>
    [JsonProperty("speaker_type", Order = 11)]
    public string SpeakerType { get; set; } = "NORMAL";

    /// <summary>
    /// 장비 설명
    /// </summary>
    [JsonProperty("description", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// 방송서버 참조 (read-only, nested) — 응답 역직렬화 전용. 쓰기는 <see cref="ServerId"/> 사용.
    /// </summary>
    [JsonProperty("server", Order = 13)]
    public ServerDto? Server { get; set; }

    /// <summary>
    /// 방송서버 ID (write) — SpeakerCreate/Update.server_id. 해제 미지원이라 null이면 직렬화 생략.
    /// </summary>
    [JsonProperty("server_id", Order = 14, NullValueHandling = NullValueHandling.Ignore)]
    public int? ServerId { get; set; }

    /// <summary>요청 본문에 nested server를 직렬화하지 않음(서버는 server_id를 기대 + 민감정보 누출 방지).</summary>
    public bool ShouldSerializeServer() => false;

    #region - 7.0 축(axis) 쓰기 투영 (FR-09) -
    /// <summary>
    /// 7.0 <c>speaker_role</c>(<c>EnumSpeakerRole</c> = NORMAL·ADMIN·MONITOR·DEV) —
    /// 6.3 <c>speaker_type</c> 의 <b>개명</b>이다. 두 열거값 집합이 완전히 같아 1:1 로 옮긴다.
    /// </summary>
    /// <remarks>
    /// 7.0 은 스피커를 2축으로 쪼갰다 — 역할(<c>speaker_role</c>)과 형상(<c>type_speaker</c>:
    /// Horn·Pillar·Unknown). 6.3 에는 형상 축이 <b>아예 없어</b> 유도할 근거가 없으므로
    /// <c>type_speaker</c> 는 보내지 않는다(서버 기본값 <c>Unknown</c>).
    /// </remarks>
    [JsonProperty("speaker_role", Order = 30, NullValueHandling = NullValueHandling.Ignore)]
    public string? SpeakerRoleAxis
    {
        get => DeviceAxisWrite.NullIfEmpty(SpeakerType);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) SpeakerType = v; }
    }

    public bool ShouldSerializeSpeakerRoleAxis() => UseAxisWrite;

    /// <summary>
    /// 축 전용 배후 저장소 — 6.3 스피커 쓰기 스키마에는 <c>ip_address</c>·<c>ip_port</c> 자리가 없었다
    /// (D-21: 서버 스키마 <c>app/schemas/device.py:718</c>는 스피커도 <c>connection: Optional[ConnectionAxis]</c>
    /// 를 받는다 — 예시는 <c>{"type":"IP_DIRECT","ip_address":..,"ip_port":80}</c>). <c>[JsonIgnore]</c> 라
    /// 6.3 본문 바이트는 늘지 않는다 — <see cref="ConnectionAxis"/> 를 통해서만 나간다.
    /// </summary>
    [JsonIgnore]
    public string? IpAddress { get; set; }

    [JsonIgnore]
    public int? IpPort { get; set; }

    /// <summary>
    /// 7.0 <c>connection</c> — 스피커도 필수가 아니다(<c>SpeakerCreate.connection: Optional[ConnectionAxis]</c>).
    /// <c>server_id</c>(방송서버 경유)로도 운용되므로, IP 가 없으면 <c>null</c> 을 돌려줘 키를 뺀다.
    /// </summary>
    /// <remarks>setter 는 7.0+ 응답 역투영(D-24) — 없으면 스피커 IP·포트가 화면에서 사라진다.</remarks>
    [JsonProperty("connection", Order = 29, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get
        {
            var ip = DeviceAxisWrite.NullIfEmpty(IpAddress);
            var port = DeviceAxisWrite.PortOrNull(IpPort ?? 0);
            return ip == null && port == null ? null : DeviceAxisWrite.BuildIpConnection(IpAddress, IpPort ?? 0, storedType: PreservedConnectionType);
        }
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            if (DeviceAxisWrite.NullIfEmpty(value.IpAddress) is { } ip) IpAddress = ip;
            if (value.IpPort is > 0) IpPort = value.IpPort.Value;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite && ConnectionAxis != null;

    /// <summary>
    /// 7.0 <c>type_speaker</c>(<c>EnumSpeakerShape</c> = Horn·Pillar·Unknown) — <b>하우징 형상</b> 축.
    /// </summary>
    /// <remarks>
    /// 6.3 에는 형상 축이 <b>아예 없어</b> 유도할 근거가 없다(서버도 "백필 불가 — DB 에 형상 정보가 없다").
    /// 그래서 값이 없으면 보내지 않고 서버 기본값 <c>Unknown</c> 에 맡긴다. 응답은 필수 키라 항상 채워지고,
    /// 운용자가 입력하면 이 속성으로 실어 보낸다. "현장 미확인"은 <c>null</c> 이 아니라 <c>Unknown</c> 이다.
    /// </remarks>
    [JsonProperty("type_speaker", Order = 31, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeSpeaker { get; set; }

    public bool ShouldSerializeTypeSpeaker() => UseAxisWrite && TypeSpeaker != null;

    /// <summary>7.0+ <c>hardware_spec</c> — 6.3 쓰기 스키마에 없어 축 모드에서만 전송한다(펌웨어 읽기 경로).</summary>
    [JsonProperty("hardware_spec", Order = 32, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    /// <summary>
    /// 7.0 <c>device_config</c> — 스피커도 계산해서 채울 축이 없다. <see cref="BaseDeviceDto.DeviceConfigWrite"/>
    /// (호출자가 채운 조각)만 실린다(N-02 §3 공통 통로). 플래그가 꺼져 있으면(기본) 절대 나가지 않는다.
    /// </summary>
    [JsonProperty("device_config", Order = 33, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DeviceConfigAxisDto? DeviceConfigAxis
    {
        get => ComposeDeviceConfigAxis(null);
        set => ReceivedDeviceConfig = value;   // raw capture for read mapping (device-console-v8 FR-03)
    }

    public bool ShouldSerializeDeviceConfigAxis() => UseAxisWrite && AllowDeviceConfigWrite && DeviceConfigAxis != null;

    /// <summary>7.0 에서 <c>speaker_role</c> 로 이관된 키(<c>_legacy.py:99</c>) — 축 모드에선 미전송.</summary>
    public bool ShouldSerializeSpeakerType() => !UseAxisWrite;
    #endregion
}
