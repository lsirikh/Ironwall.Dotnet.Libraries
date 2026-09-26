using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Sensor 디바이스 DTO
/// </summary>
public class SensorDeviceDto : BaseDeviceDto
{
    /// <summary>
    /// 소속 Controller ID
    /// <para>기반 <see cref="BaseDeviceDto.ControllerId"/>(<c>int?</c>)를 의도적으로 숨긴다 —
    /// 센서는 <c>controller_id</c> 가 필수(6.3·7.0 공통)라 nullable 이 아니다.
    /// <c>new</c> 는 CS0108 경고 제거용이며 직렬화 동작은 종전과 같다(Newtonsoft 는 숨겨진 기반 멤버를 제외한다).</para>
    /// </summary>
    [JsonProperty("controller_id", Order = 14)]
    public new int ControllerId { get; set; }

    /// <summary>
    /// <c>controller_id</c> 는 <b>0 이면 싣지 않는다</b>.
    /// <para>🔴 <b>실측 근거</b>(2026-09-18, 로컬 8.0.1) — 부분 수정용으로 희소 DTO
    /// (<c>new SensorDeviceDto { Id = id, NameDevice = "..." }</c>)를 <c>PATCH</c> 에 넘기면
    /// 값형 기본값 <c>0</c> 이 그대로 실려 서버가 <c>404 NOT_FOUND "Controller with id 0 not found"</c> 를
    /// 돌려준다. 소속 컨트롤러 0 은 어느 판본에서도 유효하지 않으므로(생성 시에도 404)
    /// 드롭이 항상 안전하다 — 전체 교체(<c>PUT</c>)에서는 실제 값이 있어 영향이 없다.</para>
    /// </summary>
    public bool ShouldSerializeControllerId() => ControllerId > 0;

    /// <summary>
    /// 소속 Controller (선택적, include_controller=true 시)
    /// </summary>
    [JsonProperty("controller", Order = 15)]
    public ControllerDeviceDto? Controller { get; set; }

    /// <summary>
    /// 장비 설명 — 서버 8.0.1 은 7 카테고리 공통으로 저장한다(하네스가 함체에 raw PATCH 로 실측 확인,
    /// device-assembly-preset 왕복 하네스). <c>null</c> 이면 생략한다("값 없음" ≠ "지워라" — <c>NullValueHandling.Ignore</c>).
    /// </summary>
    [JsonProperty("description", Order = 16, NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    #region - 7.0 축(axis) 투영 (FR-09) -
    /// <summary>
    /// 7.0 필수 <c>type_sensor</c>(<c>EnumSensorType</c>) — 6.3 <c>type_device</c> 를 <b>그대로</b> 보낸다.
    /// </summary>
    /// <remarks>
    /// <para><b>명세로 확정(2026-09-18) — 임의 매핑을 하지 않는다.</b> 7.0+ <c>EnumSensorType</c> 은
    /// <c>Multi</c>·<c>Fence</c>·<c>Underground</c>·<c>Contact</c>·<c>PIR</c>·<c>Laser</c>·<c>Radar</c>·
    /// <c>OpticalCable</c>·<c>SmartSensor</c>·<c>SmartSensor2</c>·<c>SmartCompound</c>·
    /// <c>SmartMultisensor2</c> 12값이고 <b>"<c>EnumDeviceType</c> 의 센서값을 그대로 승계"</b>한다
    /// (name == value). 즉 <b>이름이 같은 값은 변환이 필요 없다</b>.</para>
    /// <list type="bullet">
    /// <item><c>Cable</c>·<c>Fence_Group</c> — 서버가 <b>"실데이터 0건 + PM 확정으로 승계하지 않는다"</b>
    ///   (<c>EnumSensorType</c> 설명, FR-19)고 명시했고 DB enum 에서도 제거됐다
    ///   (<c>v88_device_enum_cleanup.sql:61-65</c> — 새 <c>enumdevicetype</c> 에 두 값이 없다).
    ///   그러므로 <c>Cable</c>→<c>OpticalCable</c> 같은 대응은 <b>명세가 부정한 매핑</b>이다.</item>
    /// <item><c>IoController</c> — 센서 종류가 아니라 <b><c>EnumControllerType</c> 값</b>이다(제어기 종류).
    ///   센서 DTO 에 이 값이 있다면 축이 뒤섞인 데이터 결함이므로 고쳐 보내야 할 자리가 아니다.</item>
    /// <item><c>NONE</c> — 6.3 <c>EnumDeviceType</c> 에는 있었지만 7.0 에서 제거됐다(같은 <c>v88</c>).</item>
    /// </list>
    /// <para>그래서 값을 <b>보정하지 않고 그대로</b> 보내 서버가 <c>type_sensor: Input should be 'Multi', …</c>
    /// 로 <b>422 명시 거부</b>하게 둔다 — 값을 비우면 "필수 누락"이 되어 <b>어떤 값이 문제였는지</b>
    /// 진단 정보가 사라지기 때문이다(그래서 카메라와 달리 여기서는 미전송이 아니라 원값 전송이다).</para>
    /// <para>setter 는 7.0+ 응답 역투영(D-24) — 응답에 <c>type_device</c> 가 없으므로
    /// 이 되돌림이 없으면 센서 종류가 빈 문자열로 굳는다.</para>
    /// </remarks>
    [JsonProperty("type_sensor", Order = 30, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeSensorAxis
    {
        get => DeviceAxisWrite.NullIfEmpty(TypeDevice);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) TypeDevice = v; }
    }

    public bool ShouldSerializeTypeSensorAxis() => UseAxisWrite;

    /// <summary>
    /// 축 전용 배후 저장소 — 센서의 접속은 <b>둘로 갈린다</b>(PM 확정: "뭐는 IP 가 없는 센서 RS485,
    /// 어떤 건 IP 기반"). <see cref="Channel"/> 이 있으면 RS485 버스 주소, <see cref="IpAddress"/> 가 있으면
    /// IP_DIRECT — 서버 <c>SensorConnectionAxis</c>(<c>app/schemas/device.py:132</c>)는 <c>ConnectionAxis</c> 를
    /// 그대로 물려받되 <c>parent_device_id</c> 만 거부한다(D13 — 상위는 <c>controller_id</c> 한 곳).
    /// 이 DTO 는 <c>ParentDeviceId</c> 를 애초에 채우지 않으므로 그 거부에 저절로 순응한다.
    /// </summary>
    [JsonIgnore]
    public string? IpAddress { get; set; }

    [JsonIgnore]
    public int? IpPort { get; set; }

    /// <summary>RS485 버스 주소(D13) — 접점 채널이 아니라 그 제어기 버스 안에서의 노드 주소다.</summary>
    [JsonIgnore]
    public int? Channel { get; set; }

    /// <summary>
    /// 7.0 <c>connection</c> — 필수가 아니다(<c>SensorCreate.connection: Optional[SensorConnectionAxis]</c>).
    /// IP 가 있으면 IP 축(저장된 방식, 없으면 IP_DIRECT)을 우선하고, 없고 <see cref="Channel"/> 만 있으면
    /// 저장된 채널 방식(<c>CONTROLLER_CONTACT</c> 등), 없으면 RS485 를 싣는다(<see cref="ChannelConnectionType"/>).
    /// 둘 다 없으면 <c>null</c> 로 키를 뺀다.
    /// </summary>
    /// <remarks>setter 는 7.0+ 응답 역투영(D-24) — 없으면 센서 접속 정보가 화면에서 사라진다.</remarks>
    [JsonProperty("connection", Order = 29, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get
        {
            var ip = DeviceAxisWrite.NullIfEmpty(IpAddress);
            if (ip != null) return DeviceAxisWrite.BuildIpConnection(IpAddress, IpPort ?? 0, storedType: PreservedConnectionType);
            if (Channel.HasValue) return new ConnectionAxisDto { Type = ChannelConnectionType(PreservedConnectionType), Channel = Channel };
            return null;
        }
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            if (DeviceAxisWrite.NullIfEmpty(value.IpAddress) is { } ip) IpAddress = ip;
            if (value.IpPort is > 0) IpPort = value.IpPort.Value;
            if (value.Channel.HasValue) Channel = value.Channel;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite && ConnectionAxis != null;

    /// <summary>
    /// IP 없이 채널만 있는 센서의 결선 방식 — 저장된 방식이 있으면 그것, 없으면(새 장비) <c>RS485</c>.
    /// </summary>
    /// <remarks>
    /// 채널은 RS485 버스 주소이기도 하고 제어기 접점 번호이기도 하다(<c>ConnectionAxis.channel</c> — "접점 채널 번호 / RS485 버스 주소").
    /// 예전에는 채널만 있으면 무조건 <c>RS485</c> 를 실어, PATCH(객체 병합)가 저장된 <c>CONTROLLER_CONTACT</c> 를 덮었다
    /// (라이브 실측 2026-09-26: 접점 결선 센서의 이름만 고쳐 저장 → 서버 <c>RS485</c>). IP 계열 방식(<c>IP_DIRECT</c>·<c>IP_CONVERTER</c>)은
    /// IP 가 없는 이 가지에서는 뜻이 없으므로 이어받지 않는다.
    /// </remarks>
    internal static string ChannelConnectionType(string? storedType)
    {
        var stored = DeviceAxisWrite.NullIfEmpty(storedType);
        if (stored == null
            || stored == EnumConnectionTypeNames.IpDirect
            || stored == EnumConnectionTypeNames.IpConverter)
            return EnumConnectionTypeNames.Rs485;
        return stored;
    }

    /// <summary>7.0+ <c>hardware_spec</c> — 센서는 <c>max_detection_range</c> 를 쓸 수 있는 두 카테고리 중 하나다.</summary>
    [JsonProperty("hardware_spec", Order = 31, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    /// <summary>
    /// 7.0 <c>device_config</c> — 센서도 계산해서 채울 축이 없다. <see cref="BaseDeviceDto.DeviceConfigWrite"/>
    /// (호출자가 채운 조각)만 실린다(N-02 §3 공통 통로). 플래그가 꺼져 있으면(기본) 절대 나가지 않는다.
    /// </summary>
    [JsonProperty("device_config", Order = 32, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DeviceConfigAxisDto? DeviceConfigAxis
    {
        get => ComposeDeviceConfigAxis(null);
        set => ReceivedDeviceConfig = value;   // raw capture for read mapping (device-console-v8 FR-03)
    }

    public bool ShouldSerializeDeviceConfigAxis() => UseAxisWrite && AllowDeviceConfigWrite && DeviceConfigAxis != null;

    /// <summary>응답 전용 — 7.0 <c>SensorCreate</c> properties 에 없어 실으면 422(읽기는 <c>?include=controller</c>).</summary>
    public bool ShouldSerializeController() => !UseAxisWrite && Controller != null;
    #endregion
}
