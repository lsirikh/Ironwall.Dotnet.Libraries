using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
/****************************************************************************
   Purpose      : Device query assembly policy (server contract branching)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// v7.0 에서 <b>410 묘비</b>가 된 장비·서버 경로(FR-11).
/// </summary>
/// <remarks>
/// 실측(2026-09-18, 배포 Swagger 7.0.1): 아래 경로는 <c>200</c> 선언이 사라지고
/// <c>410 ENDPOINT_REMOVED</c> 만 남았다. 6.3 운영 서버에서는 <b>정상 동작</b>하므로
/// 경로 자체를 지우지 않고 <see cref="DeviceQueryPolicy.IsEndpointAvailable"/> 로 게이트만 건다.
/// </remarks>
public enum EnumDeviceLegacyEndpoint
{
    /// <summary><c>GET·PATCH·PUT /api/devices/cameras/{id}/settings</c> — 대체: <c>/api/devices/{category}/{id}/config</c>.</summary>
    CameraSettings = 0,

    /// <summary><c>POST /api/devices/gates/{id}/control</c> — 대체: NATS <c>GATE_DOOR_SET</c>.</summary>
    GateDoorControl = 1,

    /// <summary><c>POST /api/devices/enclosures/{id}/control</c> — 대체: NATS <c>ENCLOSURE_DOOR_SET</c>.</summary>
    EnclosureDoorControl = 2,

    /// <summary><c>GET·PATCH·PUT /api/servers/{id}/proxy-settings</c> — 대체: <c>/api/servers/{id}/config</c> 의 <c>modes</c>.</summary>
    ServerProxySettings = 3,
}

/// <summary>
/// 장비·서버 조회 쿼리를 <b>서버 계약 세대별로 조립</b>하는 단일 분기점(FR-10 · FR-11).
/// </summary>
/// <remarks>
/// <para><b>왜 한 곳인가</b> — 종전에는 호출부마다 <c>includeSensors: true</c> 같은 리터럴이 흩어져 있어
/// 서버가 판을 갈 때마다 같은 실수가 반복됐다. 이 클래스가 <b>유일한 판단 지점</b>이고,
/// 호출부는 결정을 내리지 않고 값만 받아 쓴다.</para>
///
/// <para><b>안전 방향</b> — <see cref="IServerContractProbe"/> 가 없거나 미확보면
/// <see cref="EnumServerContract.V6_3"/> 으로 간주한다. 즉 <b>현행 운영(6.3.2) 동작이 기본값</b>이고
/// 7.0 경로는 프로브가 7.0 을 실제로 관측했을 때만 켜진다(운영 무회귀 우선).</para>
///
/// <para><b>실측 근거(2026-09-18)</b> — 원격 운영 = 6.3.2 · 로컬 개발 = 7.0.1 · 명세 v8.0 = 미배포.
/// 7.0.1 에서 <c>?include_sensors=true</c>·<c>?include_controller=true</c> 는
/// <c>422 REMOVED_FIELD</c>(<c>moved_to=include=sensors</c>)이고 <c>?include=sensors</c> 는 200 이다.
/// 또 7.0.1 의 <c>view</c> 기본값이 <c>basic</c> 이라 <c>device_config</c>·<c>device_status</c>·
/// <c>hardware_spec</c> 이 <b>키째 오지 않는다</b>(422 도 null 도 아니라 조용한 데이터 손실).</para>
/// </remarks>
public sealed class DeviceQueryPolicy
{
    #region - Ctors -
    /// <param name="probe">서버 계약 프로브. <c>null</c> 이면 <see cref="EnumServerContract.V6_3"/> 로 간주한다.</param>
    /// <param name="log">진단 로그(선택).</param>
    public DeviceQueryPolicy(IServerContractProbe? probe = null, ILogService? log = null)
    {
        _probe = probe;
        _log = log;
    }
    #endregion

    #region - Properties -
    /// <summary>현재 서버 계약 세대. 프로브가 없거나 미확보면 <see cref="EnumServerContract.V6_3"/>.</summary>
    public EnumServerContract Contract => _probe?.Contract ?? EnumServerContract.V6_3;

    /// <summary>6.3 계약(현행 운영)인가. <c>true</c> 면 <b>모든 기존 동작을 그대로 유지</b>한다.</summary>
    /// <remarks>
    /// ⚠ <b>동치 비교 금지</b> — 반드시 <c>&lt;=</c>/<c>&gt;=</c> 같은 <b>순서 비교</b>로만 판정한다.
    /// 세대는 계속 올라간다(실측 2026-09-18: 로컬이 하루 안에 7.0.1 → 8.0.1).
    /// <c>== V7_0</c> 같은 동치 비교를 쓰면 8.0 서버가 <b>조용히 레거시로 떨어져</b>
    /// 제거된 <c>include_sensors</c> 를 다시 보내고 422 → 제어기·센서 전량 0건이 된다.
    /// </remarks>
    public bool IsLegacyContract => Contract <= EnumServerContract.V6_3;

    /// <summary>7.0 <b>이상</b>(축 전환판 — 7.0 · 8.0 · 그 이후 전부 포함)인가.</summary>
    public bool IsAxisContract => Contract >= EnumServerContract.V7_0;

    /// <summary>
    /// 제어기 목록·단건에 레거시 <c>?include_sensors=true</c> 를 실을 수 있는가(FR-10).
    /// 7.0 에서는 <b>제거된 키</b>라 실으면 즉시 422 → 제어기 전량 0건.
    /// </summary>
    public bool CanUseIncludeSensorsFlag => IsLegacyContract;

    /// <summary>
    /// 센서 목록·단건에 레거시 <c>?include_controller=true</c> 를 실을 수 있는가(FR-10).
    /// 7.0 에서는 제거된 키(→ <c>?include=controller</c>).
    /// </summary>
    public bool CanUseIncludeControllerFlag => IsLegacyContract;

    /// <summary>
    /// 장비·서버 조회에 붙일 <c>view</c> 값. 7.0 이상에서만 <c>"full"</c>,
    /// 6.3 에서는 <c>null</c>(파라미터 자체가 없다 — 붙이면 무시 또는 422 위험).
    /// </summary>
    /// <remarks>
    /// <para><b>배선 완료(2026-09-18)</b> — <c>DeviceProviderService</c> 의 목록 8경로(제어기·센서·카메라·스피커·함체·
    /// 통문·경광등·서버)와 장비 패널 6곳의 <c>FetchXxxAsync</c> 가 <c>view: _queryPolicy.View</c> 를 넘긴다.
    /// Api 계층(<c>AddViewInclude</c>)이 <see cref="string.IsNullOrWhiteSpace"/> 로 걸러 6.3 에서는
    /// 쿼리에 아무것도 붙지 않는다(운영 무회귀).</para>
    ///
    /// <para>⚠ <b>목록에만 붙인다 — 단건에는 붙이지 않는다.</b> 실측(8.0.1 라이브 GET, 2026-09-18):
    /// 목록 7경로 + <c>/api/servers</c> 는 기본이 <c>meta.view=basic</c>·<c>sections=['connection']</c> 이고
    /// <c>?view=full</c> 을 붙여야 <c>['components','connection','device_config','device_status','hardware_spec']</c>
    /// (서버는 <c>['connection','server_config']</c>)가 실린다. 반면 <b>단건 8경로는 기본이 이미
    /// <c>meta.view=full</c></b> 이라 붙일 필요가 없다. 목록 기본이 <c>basic</c> 인 채로 두면
    /// <c>device_config</c>·<c>device_status</c>·<c>hardware_spec</c> 이 <b>키째</b> 오지 않아
    /// 3D 하우징·부품상태·임계치·FOV 가 "설정 안 됨"으로 보인다(422 도 null 도 아닌 조용한 손실).</para>
    /// </remarks>
    public string? View => IsAxisContract ? VIEW_FULL : null;
    #endregion

    #region - Processes -
    /// <summary>
    /// 7.0 의 <c>?include=</c> CSV 토큰을 조립한다. 6.3 에서는 <c>null</c>(파라미터 미지원).
    /// </summary>
    /// <param name="tokens"><c>sensors</c>·<c>controller</c>·<c>rois</c>·<c>points</c>·<c>connection</c> 등.</param>
    /// <returns>CSV 문자열 또는 <c>null</c>(붙이지 않음).</returns>
    public string? BuildInclude(params string?[]? tokens)
    {
        if (!IsAxisContract || tokens == null) return null;

        var picked = new List<string>(tokens.Length);
        foreach (var t in tokens)
        {
            if (string.IsNullOrWhiteSpace(t)) continue;      // (F-18) 빈 문자열·공백은 드롭
            var trimmed = t.Trim();
            if (!picked.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                picked.Add(trimmed);
        }
        return picked.Count == 0 ? null : string.Join(",", picked);
    }

    /// <summary>
    /// 해당 쿼리 필터 키를 현재 계약에서 보낼 수 있는가(FR-11).
    /// 7.0 에서 제거된 7종은 <c>false</c>, 6.3 에서는 전부 <c>true</c>.
    /// </summary>
    public bool IsFilterSupported(string? filterKey)
    {
        if (string.IsNullOrWhiteSpace(filterKey)) return false;
        if (IsLegacyContract) return true;
        return !RemovedFiltersV7.ContainsKey(filterKey.Trim());
    }

    /// <summary>
    /// 필터 값을 계약에 맞게 정규화한다 — <b>호출부는 반환값을 그대로 넘기면 된다</b>.
    /// </summary>
    /// <returns>
    /// 보낼 수 있으면 트림된 값(빈 값·공백은 <c>null</c>), 7.0 에서 제거된 키면 <c>null</c>(= 안 보냄).
    /// </returns>
    /// <remarks>
    /// 제거된 키를 드롭할 때 <c>moved_to</c> 안내를 로그로 남긴다 — 서버가 이사 경로를 알려주는데
    /// 우리가 버리면 진단이 "알 수 없는 오류"로 끝난다(F-08).
    /// </remarks>
    public string? SanitizeFilter(string filterKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;    // (F-18) IsNullOrWhiteSpace 로 통일

        if (!IsLegacyContract
            && !string.IsNullOrWhiteSpace(filterKey)
            && RemovedFiltersV7.TryGetValue(filterKey.Trim(), out var movedTo))
        {
            _log?.Warning(
                $"{nameof(DeviceQueryPolicy)}: 쿼리 필터 '{filterKey}' 는 서버 계약 {Contract} 에서 제거됨 — 전송 생략 " +
                $"(moved_to: {movedTo})");
            return null;
        }
        return value.Trim();
    }

    /// <summary>
    /// 해당 레거시 엔드포인트를 현재 계약에서 호출해도 되는가(FR-11 묘비 가드).
    /// 6.3 이면 <c>true</c>(현행 유지), 7.0 이상이면 <c>false</c>(410).
    /// </summary>
    public bool IsEndpointAvailable(EnumDeviceLegacyEndpoint endpoint)
        => IsLegacyContract || !TombstonedV7.ContainsKey(endpoint);

    /// <summary>묘비 엔드포인트의 대체 경로 안내(로그·개발자 경보용). 사용 가능하면 <c>null</c>.</summary>
    public string? DescribeRemoval(EnumDeviceLegacyEndpoint endpoint)
        => IsEndpointAvailable(endpoint)
            ? null
            : $"{endpoint} 는 서버 계약 {Contract} 에서 제거됨(410 ENDPOINT_REMOVED) — 대체: {TombstonedV7[endpoint]}";

    /// <summary>
    /// 묘비 엔드포인트 호출 시도를 기록하고 호출 가능 여부를 돌려준다(호출부 1행 가드용).
    /// </summary>
    public bool TryUseEndpoint(EnumDeviceLegacyEndpoint endpoint, string caller)
    {
        if (IsEndpointAvailable(endpoint)) return true;
        _log?.Warning($"{caller}: {DescribeRemoval(endpoint)}");
        return false;
    }

    /// <summary>
    /// 컨테이너에서 정책을 얻는다 — 수동 <c>new</c> 로 만들어지는 VM/다이얼로그용.
    /// 해석 실패 시 <b>6.3 기본값 정책</b>을 돌려주므로 호출부는 null 검사가 필요 없다.
    /// </summary>
    public static DeviceQueryPolicy Resolve()
    {
        try
        {
            var all = IoC.GetAllInstances(typeof(DeviceQueryPolicy));
            if (all != null)
            {
                foreach (var item in all)
                    if (item is DeviceQueryPolicy policy) return policy;
            }
        }
        catch
        {
            // 컨테이너 미구성(디자인 타임·단위테스트) → 아래 폴백
        }
        return Legacy;
    }
    #endregion

    #region - Attributes -
    /// <summary>7.0 의 <c>view</c> 최대 프로필 — 이 값일 때만 <c>device_config</c>·<c>device_status</c>·<c>hardware_spec</c> 이 실린다.</summary>
    public const string VIEW_FULL = "full";

    /// <summary>7.0 <c>?include=</c> 토큰 — 제어기의 센서 목록.</summary>
    public const string INCLUDE_SENSORS = "sensors";

    /// <summary>7.0 <c>?include=</c> 토큰 — 센서의 제어기 객체.</summary>
    public const string INCLUDE_CONTROLLER = "controller";

    /// <summary>7.0 <c>?include=</c> 토큰 — 카메라 프리셋의 ROI.</summary>
    public const string INCLUDE_ROIS = "rois";

    /// <summary>7.0 <c>?include=</c> 토큰 — ROI 점 목록.</summary>
    public const string INCLUDE_POINTS = "points";

    /// <summary>
    /// 7.0 에서 제거돼 <c>422 REMOVED_FIELD</c> 가 되는 쿼리 필터 → 서버가 알려주는 <c>moved_to</c>(FR-11, 실측).
    /// </summary>
    /// <remarks>
    /// <b>대체 이름 실측(8.0.1 Swagger, 2026-09-18)</b> — 제거된 파라미터는 스펙에 <c>deprecated:true</c> 로
    /// 남아 있고 설명에 대체 이름이 적혀 있다. <c>type_device</c> 는 <b>카테고리마다 이름이 다르다</b>:
    /// 제어기 <c>type_controller</c> · 센서 <c>type_sensor</c> · 카메라 <c>type_camera</c> ·
    /// 스피커 <c>type_speaker</c> · 함체 <c>type_enclosure</c> · 통문 <c>type_gate</c> · 경광등 <c>type_lamp</c>.
    /// <para>⚠ <b>현재 이 대체 이름들을 전달할 Api 통로가 없다</b> — <c>IDeviceApiService</c> 의 목록 시그니처는
    /// 여전히 옛 이름(<c>typeDevice</c>·<c>mode</c>·<c>category</c>·<c>speakerType</c>·<c>doorStatus</c>·
    /// <c>gateStatus</c>)만 받는다. 그래서 7.0+ 에서 <b>해당 필터 기능 자체가 없다</b>(422 는
    /// <see cref="SanitizeFilter"/> 드롭으로 막혀 있다). Devices.Ui 호출부는 현재 이 필터를 <b>한 곳도 쓰지 않아</b>
    /// 실사용 영향은 없지만, 필터 UI 를 붙이려면 Api 계층에 새 이름 파라미터 신설이 선행돼야 한다.</para>
    /// <para>또 8.0.1 에서 센서의 <c>server_device_id</c> 축이 사라졌다 — <c>/api/devices/sensors?server_id=</c> 는
    /// <c>deprecated</c>(보내면 422)이고 상위는 <c>?controller_id=</c> 다. 센서 목록에 <c>server_id</c> 를 싣지 말 것.</para>
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> RemovedFiltersV7 =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type_device"]   = "type_controller / type_sensor / type_camera / type_speaker / type_enclosure / type_gate / type_lamp (카테고리별로 이름이 다르다)",
            ["mode"]          = "protocol (connection.protocol)",
            ["category"]      = "type_camera",
            ["speaker_type"]  = "speaker_role (형상 필터는 별도 키 type_speaker)",
            ["door_status"]   = "GET /api/devices/by-component?component_type=DOOR_SENSOR&state=OPEN",
            ["gate_status"]   = "GET /api/devices/by-component?component_type=DOOR_ACTUATOR&state=OPEN",
            ["category_id"]   = "category_server",
            ["include_presets"]    = "include=presets",
            ["include_server"]     = "include=server",
            ["include_sensors"]    = "include=sensors",
            ["include_controller"] = "include=controller",
            ["include_rois"]       = "include=rois",
            ["include_points"]     = "include=points",
        };

    /// <summary>7.0 에서 <c>410 ENDPOINT_REMOVED</c> 가 된 경로 → 대체 안내(FR-11, 실측).</summary>
    public static readonly IReadOnlyDictionary<EnumDeviceLegacyEndpoint, string> TombstonedV7 =
        new Dictionary<EnumDeviceLegacyEndpoint, string>
        {
            [EnumDeviceLegacyEndpoint.CameraSettings]
                = "GET/PATCH/PUT /api/devices/{category}/{id}/config (device_config 축)",
            [EnumDeviceLegacyEndpoint.GateDoorControl]
                = "NATS GATE_DOOR_SET (보고는 PATCH /api/devices/gates/{id}/component-status)",
            [EnumDeviceLegacyEndpoint.EnclosureDoorControl]
                = "NATS ENCLOSURE_DOOR_SET (보고는 PATCH /api/devices/enclosures/{id}/component-status)",
            [EnumDeviceLegacyEndpoint.ServerProxySettings]
                = "GET/PATCH /api/servers/{id}/config 의 server_config.modes",
        };

    /// <summary>프로브 없는 폴백 정책(6.3 = 현행 운영 동작).</summary>
    private static readonly DeviceQueryPolicy Legacy = new();

    private readonly IServerContractProbe? _probe;
    private readonly ILogService? _log;
    #endregion
}
