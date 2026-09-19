using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

/// <summary>
/// Camera 디바이스 DTO
/// <para>서버 6.3(평면 필드)과 7.0(축 필드)을 <b>같은 DTO 한 개</b>로 표현한다 —
/// <see cref="BaseDeviceDto.UseAxisWrite"/> 가 <c>true</c> 면 평면 키를 끄고 축 키를 켠다.</para>
/// </summary>
public class CameraDeviceDto : BaseDeviceDto
{
    public CameraDeviceDto()
    {
        TypeDevice = "IpCamera";
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
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 사용자 비밀번호
    /// </summary>
    [JsonProperty("user_password", Order = 14)]
    public string UserPassword { get; set; } = string.Empty;

    /// <summary>
    /// RTSP URI (레거시, urls.rtsp로 대체 — 서버 v2.3에서 제거됨)
    /// </summary>
    [JsonProperty("rtsp_uri", Order = 15, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string? RtspUri { get; set; }

    /// <summary>
    /// RTSP 포트 (레거시, urls.rtsp에 포함 — 서버 v2.3에서 제거됨)
    /// </summary>
    [JsonProperty("rtsp_port", Order = 16, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int? RtspPort { get; set; }

    /// <summary>
    /// 카메라 모드 (EnumCameraMode: "NONE", "ONVIF", "EMSTONE_API", "INNODEP_API", "ETC")
    /// </summary>
    [JsonProperty("mode", Order = 17)]
    public string Mode { get; set; } = "NONE";

    /// <summary>
    /// 카메라 종류 (EnumCameraType: "NONE", "FIXED", "PTZ", "FISHEYES", "THERMAL")
    /// </summary>
    [JsonProperty("category", Order = 18)]
    public string Category { get; set; } = "NONE";

    /// <summary>
    /// 카메라 URL 묶음 (신규)
    /// </summary>
    [JsonProperty("urls", Order = 19, NullValueHandling = NullValueHandling.Ignore)]
    public CameraUrlsDto? Urls { get; set; }

    /// <summary>
    /// 녹화 여부 (신규)
    /// </summary>
    [JsonProperty("is_record", Order = 20, NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsRecord { get; set; }

    /// <summary>
    /// 하드웨어 스펙 (신규) — 7.0 에서도 같은 이름이고 <b>키 구성만</b> 갈렸다(<see cref="HardwareSpecDto"/>).
    /// </summary>
    [JsonProperty("hardware_spec", Order = 21, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HardwareSpecDto? HardwareSpec
    {
        get => HardwareSpecCore;
        set => HardwareSpecCore = value;
    }

    #region - 7.0 축(axis) 쓰기 투영 (FR-09) -
    /// <summary>
    /// 7.0 필수 <c>type_camera</c>(<c>EnumCameraType</c> = FIXED·PTZ·SPEED_DOME) — 6.3 <c>category</c> 에서 유도.
    /// 세 값 밖(<c>NONE</c>·<c>FISHEYES</c>·<c>THERMAL</c>·미설정)이면 <b>보내지 않는다</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>왜 임의 폴백(FIXED)을 쓰지 않는가 — 명세로 확정(2026-09-18).</b></para>
    /// <list type="bullet">
    /// <item><b>운영 6.3.2 서버의 <c>EnumCameraType</c> 은 <c>[NONE, FIXED, PTZ]</c> 뿐이다</b>(실측).
    ///   <c>FISHEYES</c>·<c>THERMAL</c> 은 <b>서버에 존재한 적이 없는 클라 전용 값</b>이라
    ///   저장된 실데이터가 0건이다(서버 DB enum 도 그 둘을 가진 적이 없다 —
    ///   <c>v88_device_enum_cleanup.sql</c> 이 <c>NONE</c> 만 제거하고 나머지를 단순 캐스팅했다).</item>
    /// <item><c>NONE</c> 은 8.0 에서 제거됐고(FR-19/D-P, 실데이터 0건) 서버 주석이
    ///   "클라 <c>ParseCameraType</c> 의 <c>NONE</c> 폴백은 <b>클라 내부 값</b>이라 서버가 빼도 무영향"
    ///   이라고 못 박았다 — 즉 <c>NONE</c> 은 서버에 보낼 값이 아니다.</item>
    /// <item>명세는 어안·열화상의 새 자리를 <b>다른 축</b>으로 지정한다 —
    ///   <c>hardware_spec.components[]</c> 의 <c>OPTICAL_LENS</c>(<c>spec.shape="FISHEYE"</c>) ·
    ///   <c>THERMAL_CAMERA</c>·<c>EO_CAMERA</c>. <c>type_camera</c> 는 <b>구동 방식 배타축</b>이고
    ///   <c>Unknown</c> 값이 <b>없다</b>. "열상 PTZ 가 실제로 존재한다"는 것이 두 축을 나눈 이유다.</item>
    /// </list>
    /// <para>그러므로 <c>THERMAL</c> 을 <c>FIXED</c> 로 보내면 <b>PTZ 열상 카메라가 고정형으로 굳는다</b> —
    /// 카메라 종류가 조용히 바뀌는 최악의 결과다. 대응값이 없다는 사실을 드러내는 쪽을 택해
    /// 값을 비우고, <c>CameraCreate.required</c> 위반으로 <b>서버가 422 로 표면화</b>하게 둔다
    /// (운용자가 구동 방식을 고르면 <c>Category</c> 에 유효값이 들어와 그대로 통과한다).
    /// 렌즈·영상방식 정보는 <c>hardware_spec.components[]</c> 로 별도 보존한다 — 그 조립은
    /// 장비 모델을 아는 매핑 계층 몫이고(<c>PUT</c> 이 <c>components</c> 를 통째 교체하므로
    /// 기존 선언을 모르는 DTO 가 자동 생성하면 다른 부품이 지워진다), 이 DTO 는 담을 자리만 제공한다.</para>
    /// </remarks>
    [JsonProperty("type_camera", Order = 30, NullValueHandling = NullValueHandling.Ignore)]
    public string? TypeCameraAxis
    {
        get => DeviceAxisWrite.PickOrFallback(Category, AXIS_CAMERA_TYPES, null);
        set { if (DeviceAxisWrite.NullIfEmpty(value) is { } v) Category = v; }
    }

    public bool ShouldSerializeTypeCameraAxis() => UseAxisWrite && TypeCameraAxis != null;

    /// <summary>
    /// 7.0 필수 <c>connection</c> — 6.3 평면 <c>ip_address</c>·<c>ip_port</c>·<c>user_name</c>·
    /// <c>user_password</c>·<c>mode</c>·<c>urls</c> 를 한 객체로 모은다.
    /// <c>protocol</c> 은 <c>CameraConnectionAxis.required</c> 라 비면 <c>"NONE"</c>(유효 열거값)으로 채운다.
    /// </summary>
    /// <remarks>
    /// setter 는 <b>7.0+ 응답 역투영</b>이다(A-devices D-24) — 응답에는 평면 키가 없으므로
    /// 이 되돌림이 없으면 카메라 IP·포트·계정·프로토콜·링크가 전부 빈 껍데기가 된다.
    /// 되돌려 두면 기존 호출부(평면 필드를 읽는 코드)는 <b>한 줄도 고치지 않아도</b> 동작한다.
    /// </remarks>
    [JsonProperty("connection", Order = 31, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public ConnectionAxisDto? ConnectionAxis
    {
        get => DeviceAxisWrite.BuildIpConnection(
            IpAddress, IpPort, UserName, UserPassword,
            DeviceAxisWrite.NullIfEmpty(Mode) ?? CAMERA_PROTOCOL_FALLBACK,
            Urls);
        set
        {
            ReceivedConnection = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            if (DeviceAxisWrite.NullIfEmpty(value.IpAddress) is { } ip) IpAddress = ip;
            if (value.IpPort is > 0) IpPort = value.IpPort.Value;
            if (DeviceAxisWrite.NullIfEmpty(value.Credentials?.UserName) is { } u) UserName = u;
            if (DeviceAxisWrite.NullIfEmpty(value.Credentials?.UserPassword) is { } p) UserPassword = p;
            if (DeviceAxisWrite.NullIfEmpty(value.Protocol) is { } proto) Mode = proto;
            if (DeviceAxisWrite.AsUrls<CameraUrlsDto>(value.Urls) is { } urls) Urls = urls;
        }
    }

    public bool ShouldSerializeConnectionAxis() => UseAxisWrite;

    /// <summary>7.0 <c>device_config.modes.is_record</c> — 6.3 평면 <c>is_record</c> 의 새 자리.</summary>
    /// <remarks>
    /// <para>모드 묶음은 <b>카메라만</b> 가진다(다른 카테고리는 422). 서버 어휘는
    /// <c>weather_mode</c>·<c>camera_mode</c>·<c>day_night_mode</c>·<c>focus_mode</c>·<c>iris_mode</c>·
    /// <c>palette</c>·<c>is_record</c> 다 — 6.3 에서 우리가 가진 대응 필드는 <c>is_record</c> 하나뿐이고,
    /// 나머지는 <c>CameraSettingDto</c>(410 으로 제거된 <c>/settings</c>)에 있었다.</para>
    /// <para>setter 는 응답 역투영(<c>modes.is_record</c> → <see cref="IsRecord"/>)이고,
    /// <b>받은 모드 묶음 전체를 <see cref="DeviceConfigModes"/> 에 보존</b>한다 —
    /// 쓰기에서 <c>is_record</c> 만 다시 실으면 <c>PUT</c> 이 나머지 모드를 지우기 때문이다.</para>
    /// </remarks>
    [JsonProperty("device_config", Order = 32, NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public DeviceConfigAxisDto? DeviceConfigAxis
    {
        get
        {
            DeviceConfigAxisDto? computed;
            if (!IsRecord.HasValue && DeviceConfigModes == null)
            {
                computed = null;
            }
            else
            {
                var modes = DeviceConfigModes != null ? (JObject)DeviceConfigModes.DeepClone() : new JObject();
                if (IsRecord.HasValue) modes["is_record"] = IsRecord.Value;
                computed = modes.Count == 0 ? null : new DeviceConfigAxisDto { Modes = modes };
            }

            // N-02 §3 공통 통로 — 호출자가 채운 조각(예: heater override, PTZ 한랭지 프리셋)을 병합한다.
            // AllowDeviceConfigWrite 가 꺼져 있으면(기본) computed 그대로 — 오늘 본문과 바이트 단위 동일.
            return ComposeDeviceConfigAxis(computed);
        }
        set
        {
            ReceivedDeviceConfig = value;   // raw capture for read mapping (device-console-v8 FR-03)
            if (value == null) return;
            DeviceConfigModes = value.Modes;
            var isRecord = value.Modes?["is_record"];
            if (isRecord != null && isRecord.Type == JTokenType.Boolean) IsRecord = isRecord.Value<bool>();
        }
    }

    public bool ShouldSerializeDeviceConfigAxis() => UseAxisWrite && DeviceConfigAxis != null;

    /// <summary>
    /// 7.0+ 응답에서 받은 <c>device_config.modes</c> 원본 — 우리가 6.3 평면 필드로 표현하지 못하는
    /// 모드(<c>day_night_mode</c> 등)를 <b>쓰기에서 잃지 않기 위해</b> 보관한다.
    /// </summary>
    [JsonIgnore]
    public JObject? DeviceConfigModes { get; set; }

    // ── 7.0 에서 제거된 평면 키 (축 모드에서만 끈다 — 6.3 본문은 불변) ──
    public bool ShouldSerializeIpAddress() => !UseAxisWrite;
    public bool ShouldSerializeIpPort() => !UseAxisWrite;
    public bool ShouldSerializeUserName() => !UseAxisWrite;
    public bool ShouldSerializeUserPassword() => !UseAxisWrite;
    public bool ShouldSerializeRtspUri() => !UseAxisWrite;
    public bool ShouldSerializeRtspPort() => !UseAxisWrite;
    public bool ShouldSerializeMode() => !UseAxisWrite;
    public bool ShouldSerializeCategory() => !UseAxisWrite;
    public bool ShouldSerializeUrls() => !UseAxisWrite && Urls != null;
    public bool ShouldSerializeIsRecord() => !UseAxisWrite && IsRecord.HasValue;

    /// <summary>
    /// <c>hardware_spec</c> 도 7.0 에서 키가 갈렸다(<c>name</c>·<c>location</c>·<c>hardware</c>·
    /// <c>device_id</c> 제거). 직렬화 직전에 자식 DTO 에 같은 계약 세대를 전파한다.
    /// </summary>
    public bool ShouldSerializeHardwareSpec()
    {
        if (HardwareSpec != null) HardwareSpec.UseAxisWrite = UseAxisWrite;
        return HardwareSpec != null;
    }
    #endregion

    #region - Attributes -
    /// <summary>
    /// 7.0+ <c>EnumCameraType</c> 유효값 <b>전부</b>(배포 Swagger 8.0.1 실측 — 이 셋뿐이다).
    /// 이 밖의 값은 <c>type_camera</c> 로 보내지 않는다(위 <see cref="TypeCameraAxis"/> remarks).
    /// </summary>
    private static readonly string?[] AXIS_CAMERA_TYPES = { "FIXED", "PTZ", "SPEED_DOME" };

    /// <summary><c>CameraConnectionAxis.protocol</c> 필수 — <c>EnumCameraMode</c> 의 중립값.</summary>
    private const string CAMERA_PROTOCOL_FALLBACK = "NONE";
    #endregion
}
