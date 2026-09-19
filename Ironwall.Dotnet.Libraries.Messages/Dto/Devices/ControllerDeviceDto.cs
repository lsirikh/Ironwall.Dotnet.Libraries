using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Controller 디바이스 DTO
/// </summary>
public class ControllerDeviceDto : BaseDeviceDto
{
    public ControllerDeviceDto()
    {
        TypeDevice = "Controller";
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
    /// 연결된 센서 목록 (선택적, include_sensors=true 시)
    /// </summary>
    [JsonProperty("sensors", Order = 13)]
    public List<SensorDeviceDto>? Sensors { get; set; }

    #region - 7.0 축(axis) 투영 (FR-09) -
    /// <summary>
    /// 7.0 필수 <c>type_controller</c>(<c>EnumControllerType</c> = Controller·SmartController·IoController).
    /// 6.3 <c>type_device</c> 가 이미 같은 어휘라 그대로 쓴다.
    /// </summary>
    /// <remarks>
    /// <para>세 값 밖이면 <b>보내지 않는다</b>(종전 <c>Controller</c> 낙착을 철회 — 2026-09-18).
    /// 제어기 DTO 에 센서 종류가 들어와 있다면 그것은 매핑 결함이고, 임의로 <c>Controller</c> 로
    /// 바꿔 보내면 <b>장비 종류가 조용히 달라진 채 저장</b>된다. 필수 키가 비면 서버가 422 로
    /// 표면화하므로 결함이 드러난다(<c>type_camera</c>·<c>type_sensor</c> 와 같은 원칙).</para>
    /// <para>서버 확정 사실: <c>SmartController</c> 는 실데이터 0건이지만 <b>유령값이 아니다</b>(미배치일 뿐),
    /// <c>IoController</c> 는 제어기의 한 종류가 맞다(PM 확정 2026-09-09). <c>MainController</c> 는
    /// 명세 예시에만 있던 <b>무효값</b>이라 어휘에 없다.</para>
    /// <para>setter 는 7.0+ 응답 역투영 — 응답에 <c>type_device</c> 가 없으므로(D-24)
    /// 이 되돌림이 없으면 제어기 종류가 생성자 기본값 <c>"Controller"</c> 로 굳는다.</para>
    /// </remarks>
    [JsonProperty("type_controller", Order = 30, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeControllerAxis
    {
        get => DeviceAxisWrite.PickOrFallback(TypeDevice, AXIS_CONTROLLER_TYPES, null);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) TypeDevice = v; }
    }

    public bool ShouldSerializeTypeControllerAxis() => UseAxisWrite && TypeControllerAxis != null;

    /// <summary>7.0 필수 <c>connection</c> — 6.3 평면 <c>ip_address</c>·<c>ip_port</c> 의 새 자리.</summary>
    /// <remarks>setter 는 7.0+ 응답 역투영(D-24) — 없으면 제어기 IP·포트가 빈 껍데기가 된다.</remarks>
    [JsonProperty("connection", Order = 31, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get => DeviceAxisWrite.BuildIpConnection(IpAddress, IpPort);
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            if (DeviceAxisWrite.NullIfEmpty(value.IpAddress) is { } ip) IpAddress = ip;
            if (value.IpPort is > 0) IpPort = value.IpPort.Value;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite;

    /// <summary>7.0+ <c>hardware_spec</c> — 6.3 쓰기 스키마에 없어 축 모드에서만 전송한다(펌웨어 읽기 경로).</summary>
    [JsonProperty("hardware_spec", Order = 32, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    // ── 7.0 에서 제거된 평면 키 ──
    public bool ShouldSerializeIpAddress() => !UseAxisWrite;
    public bool ShouldSerializeIpPort() => !UseAxisWrite;

    /// <summary>응답 전용 — 7.0 <c>ControllerCreate</c> properties 에 없어 실으면 422(읽기는 <c>?include=sensors</c>).</summary>
    public bool ShouldSerializeSensors() => !UseAxisWrite && Sensors != null;
    #endregion

    #region - Attributes -
    /// <summary>7.0+ <c>EnumControllerType</c> 유효값 전부(배포 Swagger 8.0.1 실측).</summary>
    private static readonly string?[] AXIS_CONTROLLER_TYPES = { "Controller", "SmartController", "IoController" };
    #endregion
}
