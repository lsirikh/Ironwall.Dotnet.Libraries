using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
/****************************************************************************
   Purpose      : 서버 API 7.0 '축(axis)' 쓰기 스키마 DTO (FR-09 장비 쓰기 버전 분기)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 7.0 <c>ConnectionAxis</c> — 종전 평면 필드(<c>ip_address</c>·<c>ip_port</c>·
/// <c>user_name</c>·<c>user_password</c>·<c>mode</c>·<c>urls</c>)가 이 객체 하나로 모였다.
/// </summary>
/// <remarks>
/// <para>실측(2026-09-18, 배포 Swagger 7.0.1): <c>ConnectionAxis.additionalProperties=false</c> 라
/// 여기 없는 키를 넣으면 즉시 422 다. 카메라만 파생 스키마(<c>CameraConnectionAxis</c>)를 쓰고
/// <c>protocol</c> 이 <b>필수</b>(<c>EnumCameraMode</c>)다.</para>
/// <para><b>type 주의</b> — 배포 Swagger 는 <c>default:"NONE"</c> 을 선언하지만 런타임은
/// <c>ip_address</c> 가 있으면 <c>NONE</c> 을 <c>IP_DIRECT</c> 로 <b>덮어쓴다</b>
/// (A-devices D-29). 그래서 우리는 IP 가 있으면 <c>IP_DIRECT</c> 를 <b>명시</b>하고
/// <c>NONE</c> 의 보존을 전제하지 않는다.</para>
/// <para>전 필드 <c>NullValueHandling.Ignore</c> — 공통 직렬화 설정(<c>ApiService._jsonSettings</c>)에
/// <c>NullValueHandling</c> 이 없어서 그냥 두면 <c>null</c> 이 전송되고 <c>additionalProperties:false</c>
/// 검증에서 값 제약(예: <c>ip_port</c> minimum 1)에 걸린다.</para>
/// </remarks>
public class ConnectionAxisDto
{
    /// <summary>축 스키마 버전(서버 기본 1).</summary>
    [JsonProperty("schema", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public int? Schema { get; set; } = 1;

    /// <summary>결선 방식 — <see cref="EnumConnectionTypeNames"/> 중 하나. 미상이면 생략(서버 기본값 위임).</summary>
    [JsonProperty("type", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? Type { get; set; }

    [JsonProperty("ip_address", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public string? IpAddress { get; set; }

    /// <summary>포트 1~65535. 0·미설정은 생략(서버 minimum 1 위반 방지).</summary>
    [JsonProperty("ip_port", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public int? IpPort { get; set; }

    [JsonProperty("credentials", Order = 5, NullValueHandling = NullValueHandling.Ignore)]
    public ConnectionCredentialsDto? Credentials { get; set; }

    /// <summary>접점·컨버터 결선의 상위 장비 id(통문 등).</summary>
    [JsonProperty("parent_device_id", Order = 6, NullValueHandling = NullValueHandling.Ignore)]
    public int? ParentDeviceId { get; set; }

    /// <summary>접점 채널 번호.</summary>
    [JsonProperty("channel", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public int? Channel { get; set; }

    /// <summary>카메라는 <c>EnumCameraMode</c>(필수), 그 밖은 자유 문자열.</summary>
    [JsonProperty("protocol", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public string? Protocol { get; set; }

    /// <summary>
    /// 링크 묶음(<c>homepage</c>·<c>management</c>·<c>image</c>·<c>onvif</c>·<c>streams</c>·<c>snapshot</c>).
    /// 서버 <c>ConnectionUrls.additionalProperties=true</c> 라 기존 <see cref="CameraUrlsDto"/>·
    /// <see cref="JObject"/> 를 그대로 실을 수 있다.
    /// </summary>
    [JsonProperty("urls", Order = 9, NullValueHandling = NullValueHandling.Ignore)]
    public object? Urls { get; set; }
}

/// <summary>서버 7.0 <c>ConnectionCredentials</c> — 종전 평면 <c>user_name</c>·<c>user_password</c>.</summary>
public class ConnectionCredentialsDto
{
    [JsonProperty("user_name", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public string? UserName { get; set; }

    [JsonProperty("user_password", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public string? UserPassword { get; set; }

    /// <summary>둘 다 비어 있으면 <c>null</c> — 빈 객체를 보내 서버 저장값을 지우지 않는다.</summary>
    public static ConnectionCredentialsDto? Create(string? userName, string? userPassword)
    {
        var u = DeviceAxisWrite.NullIfEmpty(userName);
        var p = DeviceAxisWrite.NullIfEmpty(userPassword);
        if (u == null && p == null) return null;
        return new ConnectionCredentialsDto { UserName = u, UserPassword = p };
    }
}

/// <summary>
/// 서버 7.0 <c>DeviceConfigAxis</c> — 종전 <c>threshold_config</c>·<c>heater_enabled</c>·
/// <c>fan_enabled</c>·<c>is_record</c> 가 여기로 이관됐다.
/// </summary>
/// <remarks>
/// <para>최상위는 <c>schema</c>·<c>modes</c>·<c>thresholds</c>·<c>component_overrides</c> <b>뿐</b>이고
/// <c>additionalProperties=false</c> 다(서버 CF-1).</para>
/// <para><c>thresholds</c> 의 키 어휘는 <b>확정됐다</b> — 카탈로그 <c>metric_key</c> 6종이고
/// 설정 가능한 경계는 <b>7개</b>뿐이다. 변환은 <see cref="DeviceThresholdAxis"/> 가 한다.</para>
/// <para><c>PATCH</c> 는 RFC 7396 이라 값이 <c>null</c> 인 키가 <b>삭제</b>이고, 생성·<c>PUT</c> 에서 보낸
/// <c>null</c> 은 저장하지 않는다. 반대로 <c>PUT</c> 에서 <b>보낸 축은 통째 교체</b>라
/// 일부만 실으면 나머지가 사라진다(명세 §5.5.5) — 그래서 각 장비 DTO 는 받은 섹션을 보존해 다시 싣는다.</para>
/// </remarks>
public class DeviceConfigAxisDto
{
    [JsonProperty("schema", Order = 1, NullValueHandling = NullValueHandling.Ignore)]
    public int? Schema { get; set; } = 1;

    /// <summary>
    /// 메트릭별 임계치 <c>{metric: {high, low}}</c> — 키는 카탈로그 <c>metric_key</c>
    /// (<c>temperature</c>·<c>humidity</c>·<c>current</c>·<c>voltage</c>·<c>vibration</c>·<c>ups_battery_level</c>).
    /// 6.3 평면 <c>threshold_config</c> 와의 변환은 <see cref="DeviceThresholdAxis"/>.
    /// </summary>
    [JsonProperty("thresholds", Order = 2, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Thresholds { get; set; }

    /// <summary>운용 모드 묶음 — <b>카메라만</b> 가진다(다른 카테고리는 422). <c>is_record</c> 가 여기로 들어간다.</summary>
    [JsonProperty("modes", Order = 3, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? Modes { get; set; }

    /// <summary>
    /// 부품별 운용 의도 — 키는 그 장비가 <b>선언한 부품 key</b>(선언 없으면 422).
    /// 함체 <c>heater</c>·<c>fan</c> 의 <c>enabled</c>, 경광등은 <c>color</c>.
    /// </summary>
    [JsonProperty("component_overrides", Order = 4, NullValueHandling = NullValueHandling.Ignore)]
    public JObject? ComponentOverrides { get; set; }

    /// <summary>실을 내용이 하나도 없으면 <c>true</c> — 이때는 아예 보내지 않는다.</summary>
    [JsonIgnore]
    public bool IsEmpty => Thresholds == null && Modes == null && ComponentOverrides == null;
}

/// <summary>서버 7.0 <c>EnumConnectionType</c> 값(문자열 상수 — Enums 어셈블리 참조 없이 쓰기 위함).</summary>
public static class EnumConnectionTypeNames
{
    public const string IpDirect = "IP_DIRECT";
    public const string IpConverter = "IP_CONVERTER";
    public const string ControllerContact = "CONTROLLER_CONTACT";
    public const string Rs485 = "RS485";
    public const string EnclosureContact = "ENCLOSURE_CONTACT";
    public const string ServerManaged = "SERVER_MANAGED";
    public const string None = "NONE";
}

/// <summary>장비 DTO 의 7.0 축 쓰기 투영에서 공용으로 쓰는 변환 도우미.</summary>
internal static class DeviceAxisWrite
{
    /// <summary>빈 문자열·공백은 <c>null</c> — 7.0 은 빈 문자열을 422 로 거부한다(<c>raise_empty_strings</c>).</summary>
    public static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>0 이하 포트는 <c>null</c>(서버 <c>ip_port</c> minimum 1).</summary>
    public static int? PortOrNull(int port) => port > 0 && port <= 65535 ? port : null;

    /// <summary>IP 계열 장비의 <c>connection</c> 축을 만든다(카메라·제어기·경광등 공용).</summary>
    public static ConnectionAxisDto BuildIpConnection(
        string? ipAddress,
        int ipPort,
        string? userName = null,
        string? userPassword = null,
        string? protocol = null,
        object? urls = null)
    {
        var ip = NullIfEmpty(ipAddress);
        return new ConnectionAxisDto
        {
            // IP 가 있으면 IP_DIRECT 를 명시한다(서버 런타임이 어차피 그렇게 덮어쓴다 — D-29).
            Type = ip != null ? EnumConnectionTypeNames.IpDirect : null,
            IpAddress = ip,
            IpPort = PortOrNull(ipPort),
            Credentials = ConnectionCredentialsDto.Create(userName, userPassword),
            Protocol = NullIfEmpty(protocol),
            Urls = urls,
        };
    }

    /// <summary>값이 유효 집합에 있으면 그대로, 없으면 <paramref name="fallback"/>.</summary>
    /// <remarks>
    /// <paramref name="fallback"/> 에 <c>null</c> 을 주면 <b>"모르면 안 보낸다"</b> 가 된다 —
    /// 종류축(<c>type_camera</c>·<c>type_sensor</c> 등)에서 임의 값으로 낙착시키면
    /// 장비 종류가 조용히 바뀌므로 그쪽을 기본 전략으로 쓴다.
    /// </remarks>
    public static string? PickOrFallback(string? value, string?[] allowed, string? fallback)
    {
        var v = NullIfEmpty(value);
        if (v == null) return fallback;
        foreach (var a in allowed)
        {
            if (string.Equals(a, v, StringComparison.Ordinal)) return v;
        }
        return fallback;
    }

    /// <summary>
    /// <c>connection.urls</c> 를 원하는 타입으로 되돌린다 — 역직렬화 결과는 <see cref="JObject"/> 이고
    /// 우리가 실은 값은 원래 타입 그대로다.
    /// </summary>
    public static T? AsUrls<T>(object? urls) where T : class
    {
        switch (urls)
        {
            case null: return null;
            case T typed: return typed;
            case JObject jo: return jo.ToObject<T>();
            default: return null;
        }
    }

    /// <summary><c>connection.urls</c> 를 <see cref="JObject"/> 로 되돌린다(통문·함체는 JSONB 그대로 쓴다).</summary>
    public static JObject? AsJObject(object? value)
        => value switch
        {
            null => null,
            JObject jo => jo,
            _ => JObject.FromObject(value),
        };

    /// <summary>
    /// <paramref name="computed"/> 위에 <paramref name="carrier"/> 의 각 키를 <b>그대로 덮어쓴다</b>(carrier 우선).
    /// </summary>
    /// <remarks>
    /// 값이 <see cref="JTokenType.Null"/> 인 키도 그대로 옮겨진다 — 부모 프로퍼티의
    /// <c>NullValueHandling.Ignore</c> 는 <c>JObject</c> 전체가 <c>null</c> 일 때만 적용되고 그 안의
    /// 자식 키 값에는 내려오지 않으므로, "이 부품 override 를 지운다"는 의도(명시적 <c>null</c>)가
    /// 직렬화 결과에 <c>"key": null</c> 로 그대로 살아남는다(N-02 §3, <see cref="BaseDeviceDto.DeviceConfigWrite"/>).
    /// </remarks>
    public static JObject? MergeJObjectOverride(JObject? computed, JObject? carrier)
    {
        if (carrier == null || carrier.Count == 0) return computed;

        var merged = computed != null ? (JObject)computed.DeepClone() : new JObject();
        foreach (var prop in carrier.Properties())
            merged[prop.Name] = prop.Value;

        return merged.Count == 0 ? null : merged;
    }

    /// <summary>
    /// 파생 DTO 가 계산한 <c>device_config</c> 축(<paramref name="computed"/>)과 호출자가 채운
    /// <see cref="BaseDeviceDto.DeviceConfigWrite"/> 조각(<paramref name="carrier"/>)을 하나로 합친다.
    /// </summary>
    /// <remarks>
    /// <c>thresholds</c> · <c>modes</c> · <c>component_overrides</c> 각각을
    /// <see cref="MergeJObjectOverride"/> 로 독립 병합한다(<paramref name="carrier"/> 우선). 셋 다 비면
    /// <c>null</c> 을 돌려준다 — 빈 <c>device_config: {}</c> 를 보내지 않기 위해서다.
    /// </remarks>
    public static DeviceConfigAxisDto? MergeDeviceConfigAxis(DeviceConfigAxisDto? computed, DeviceConfigAxisDto? carrier)
    {
        if (carrier == null || carrier.IsEmpty) return computed;

        var thresholds = MergeJObjectOverride(computed?.Thresholds, carrier.Thresholds);
        var modes = MergeJObjectOverride(computed?.Modes, carrier.Modes);
        var overrides = MergeJObjectOverride(computed?.ComponentOverrides, carrier.ComponentOverrides);

        if (thresholds == null && modes == null && overrides == null) return null;

        return new DeviceConfigAxisDto
        {
            Thresholds = thresholds,
            Modes = modes,
            ComponentOverrides = overrides,
        };
    }
}
