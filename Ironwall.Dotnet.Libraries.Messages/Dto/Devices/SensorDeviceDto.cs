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

    /// <summary>7.0+ <c>hardware_spec</c> — 센서는 <c>max_detection_range</c> 를 쓸 수 있는 두 카테고리 중 하나다.</summary>
    [JsonProperty("hardware_spec", Order = 31, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    public bool ShouldSerializeHardwareSpec() => ShouldSerializeHardwareSpecCore();

    /// <summary>응답 전용 — 7.0 <c>SensorCreate</c> properties 에 없어 실으면 422(읽기는 <c>?include=controller</c>).</summary>
    public bool ShouldSerializeController() => !UseAxisWrite && Controller != null;
    #endregion
}
