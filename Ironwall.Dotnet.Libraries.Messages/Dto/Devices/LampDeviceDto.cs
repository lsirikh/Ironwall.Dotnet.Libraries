using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Lamp(경고등) 디바이스 DTO (§5.11)
/// </summary>
public class LampDeviceDto : BaseDeviceDto
{
    public LampDeviceDto()
    {
        TypeDevice = "Lamp";
    }

    /// <summary>
    /// IP 주소
    /// </summary>
    [JsonProperty("ip_address", Order = 11)]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// IP 포트
    /// </summary>
    [JsonProperty("ip_port", Order = 12)]
    public int IpPort { get; set; }

    /// <summary>
    /// 사용자 이름
    /// </summary>
    [JsonProperty("user_name", Order = 13)]
    public string? UserName { get; set; }

    /// <summary>
    /// 사용자 비밀번호
    /// </summary>
    [JsonProperty("user_password", Order = 14)]
    public string? UserPassword { get; set; }

    /// <summary>
    /// 장비 설명
    /// </summary>
    [JsonProperty("description", Order = 15, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    #region - 7.0 축(axis) 쓰기 투영 (FR-09) -
    /// <summary>
    /// 7.0 필수 <c>connection</c> — 6.3 평면 <c>ip_address</c>·<c>ip_port</c>·<c>user_name</c>·
    /// <c>user_password</c> 의 새 자리(<c>LampCreate.required=[number_device,name_device,connection]</c>).
    /// </summary>
    /// <remarks>
    /// setter 는 <b>7.0+ 응답 역투영</b>이다(A-devices D-24) — 응답에 평면 키가 없으므로
    /// 이 되돌림이 없으면 경광등 IP·포트·계정이 빈 껍데기가 된다.
    /// </remarks>
    [JsonProperty("connection", Order = 30, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get => DeviceAxisWrite.BuildIpConnection(IpAddress, IpPort, UserName, UserPassword, storedType: PreservedConnectionType);
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            if (DeviceAxisWrite.NullIfEmpty(value.IpAddress) is { } ip) IpAddress = ip;
            if (value.IpPort is > 0) IpPort = value.IpPort.Value;
            if (DeviceAxisWrite.NullIfEmpty(value.Credentials?.UserName) is { } u) UserName = u;
            if (DeviceAxisWrite.NullIfEmpty(value.Credentials?.UserPassword) is { } p) UserPassword = p;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite;

    /// <summary>
    /// 7.0 <c>type_lamp</c>(<c>EnumLampType</c> = Beacon·Strobe·LedBar·Unknown) — <b>발광 방식</b> 축.
    /// </summary>
    /// <remarks>
    /// 6.3 에 대응 필드가 없어(경광등 고유 컬럼이 색·경보음·점등 동작 enum 3종뿐) 모르면 보내지 않고
    /// 서버 기본값 <c>Unknown</c> 에 맡긴다. 부저 유무는 여기가 아니라
    /// <c>hardware_spec.components</c> 의 <c>BUZZER</c> 보유로 표현한다(중복 금지).
    /// </remarks>
    [JsonProperty("type_lamp", Order = 31, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeLamp { get; set; }

    public bool ShouldSerializeTypeLamp() => UseAxisWrite && TypeLamp != null;

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
    /// 7.0 <c>device_config</c> — 경광등도 계산해서 채울 축이 없다(부저 유무는
    /// <c>hardware_spec.components</c> 의 몫). <see cref="BaseDeviceDto.DeviceConfigWrite"/>(호출자가
    /// 채운 조각 — 예 <c>component_overrides.buzzer</c>)만 실린다(N-02 §3 공통 통로). 플래그가 꺼져
    /// 있으면(기본) 절대 나가지 않는다.
    /// </summary>
    [JsonProperty("device_config", Order = 33, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DeviceConfigAxisDto? DeviceConfigAxis
    {
        get => ComposeDeviceConfigAxis(null);
        set => ReceivedDeviceConfig = value;   // raw capture for read mapping (device-console-v8 FR-03)
    }

    public bool ShouldSerializeDeviceConfigAxis() => UseAxisWrite && AllowDeviceConfigWrite && DeviceConfigAxis != null;

    // ── 7.0 에서 제거된 평면 키 ──
    public bool ShouldSerializeIpAddress() => !UseAxisWrite;
    public bool ShouldSerializeIpPort() => !UseAxisWrite;
    public bool ShouldSerializeUserName() => !UseAxisWrite;
    public bool ShouldSerializeUserPassword() => !UseAxisWrite;
    #endregion
}
