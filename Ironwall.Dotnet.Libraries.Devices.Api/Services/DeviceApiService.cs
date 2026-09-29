using Newtonsoft.Json;
using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Devices.Api.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Api.Models;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : Device API Service Implementation (GOP RESTful API 연동)
   Created By   : GHLee
   Created On   : 11/10/2025 6:00:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP_Restful_Api_연동설계.md 기반 Device & Event API 호출 서비스 구현
                  - IApiService (HTTP Client Wrapper)를 사용한 GOP RESTful API 호출
                  - RESTful API 명명 가이드 컨벤션 사용 (Get/Create 패턴)
                  - ResponseHelper를 통한 HttpResponseMessage to ApiResponse 변환
                  - 모든 예외는 ApiResponse 에러 상태로 반환하여 호출측에서 안전하게 처리
****************************************************************************/

/// <summary>
/// Device API 서비스 구현체
/// <para>IApiService를 사용하여 GOP RESTful API를 호출하고 결과를 DTO로 변환합니다.</para>
/// <para>RESTful API 명명 가이드(Get/Create/Patch/Update/Delete)를 따릅니다.</para>
/// </summary>
public class DeviceApiService : IDeviceApiService
{
    #region - Ctors -
    /// <summary>
    /// DeviceApiService 생성자
    /// </summary>
    /// <param name="log">로그 서비스</param>
    /// <param name="apiService">HTTP API 클라이언트 서비스</param>
    /// <param name="setupModel">API 설정 모델</param>
    /// <param name="contractProbe">
    /// 서버 계약 세대 프로브(FR-09). <c>null</c>(미등록)이면 <see cref="EnumServerContract.V6_3"/> 로 간주한다 —
    /// 운영이 6.3.2 이므로 틀렸을 때 손해가 가장 작은 쪽이다.
    /// </param>
    public DeviceApiService(
        ILogService? log,
        IApiService apiService,
        ApiSetupModel setupModel,
        IServerContractProbe? contractProbe = null)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
        _contractProbe = contractProbe;
    }
    #endregion

    #region - 서버 계약 세대 분기 (FR-09) -
    /// <summary>현재 서버 계약 세대. 프로브가 없거나 미확보면 <see cref="EnumServerContract.V6_3"/>.</summary>
    private EnumServerContract Contract => _contractProbe?.Contract ?? EnumServerContract.V6_3;

    /// <summary>
    /// 축(axis) 계약(7.0 <b>이상</b>)인가.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>동치 비교(<c>== V7_0</c>)를 쓰면 안 된다</b> — 8.0 서버에서 조용히 6.3 경로로 떨어져
    /// <c>type_device</c> 를 보내고 즉시 422 다. 열거값은 <c>V6_3=0 &lt; V7_0=1 &lt; V8_0=2</c> 로
    /// 순서가 보장돼 있어 <c>&gt;=</c> 비교가 안전하다.
    /// </remarks>
    private bool IsAxisContract => Contract >= EnumServerContract.V7_0;

    /// <summary>부대 편제 축(<c>unit_id</c>)을 실을 수 있는 계약인가. 8.0 이상에서만 <c>true</c>.</summary>
    private bool IsUnitScopedContract => Contract >= EnumServerContract.V8_0;

    /// <summary>
    /// 쓰기 직전에 DTO 의 직렬화 계약을 현재 서버 세대로 맞춘다(POST·PATCH·PUT 공통 진입점).
    /// </summary>
    /// <remarks>
    /// <para>DTO 를 버전마다 복제하지 않고 <c>ShouldSerializeXxx()</c> 조건 직렬화로 한 DTO 가 두 계약을
    /// 모두 표현하게 했다. 여기서 켜는 스위치가 <b>그 유일한 분기점</b>이다.</para>
    /// <para><c>unit_id</c> 는 8.0 미만에서는 값 자체를 지운다 — 6.3·7.0 쓰기 스키마에 없는 키라
    /// 실리면 즉시 422 다(7.0 은 <c>additionalProperties:false</c>).</para>
    /// </remarks>
    /// <summary>
    /// 서버 <c>limit</c> 상한(<b>100</b>) — 스웨거 <c>maximum: 100</c> 실측(6.3.2 · 8.0.1 공통).
    /// </summary>
    private const int LIMIT_MAX = 100;

    /// <summary>
    /// 페이지 크기를 서버 허용 범위(<c>1..100</c>)로 접는다.
    /// <para>🔴 <b>실측 근거</b>(2026-09-18) — <c>limit=200</c> 을 그대로 흘리면 서버가 <c>422</c> 로 거절하고
    /// 목록이 <b>빈 응답</b>으로 보인다("데이터가 없다"로 오진되는 침묵 실패). 상한을 아는 쪽에서 접는다.</para>
    /// </summary>
    private int ClampLimit(int limit, string caller)
    {
        if (limit >= 1 && limit <= LIMIT_MAX) return limit;
        var clamped = limit < 1 ? 1 : LIMIT_MAX;
        _log?.Warning($"[{caller}] limit={limit} 은 서버 허용범위(1..{LIMIT_MAX}) 밖이라 {clamped} 로 접었습니다.");
        return clamped;
    }

    private T ShapeWrite<T>(T dto) where T : BaseDeviceDto
    {
        if (dto == null) return dto!;
        dto.UseAxisWrite = IsAxisContract;
        if (!IsUnitScopedContract) dto.UnitId = null;
        if (IsAxisContract) LinkVersionToFirmware(dto);
        return dto;
    }

    /// <summary>
    /// 기존 장비 수정의 전송 — <b>축 계약(7.0+)이면 <c>PATCH</c>, 6.3 이면 종전대로 <c>PUT</c></b>.
    /// 7 카테고리의 <c>Update*Async</c> 가 전부 이 한 곳을 지난다.
    /// </summary>
    /// <remarks>
    /// <para><b>왜 축 계약에서 PUT 을 쓰지 않는가</b> — 서버의 <c>PUT</c> 은 본문에 실린 축 문서를 <b>통째 교체</b>한다.
    /// 그런데 우리 DTO 의 축은 서버에서 받은 원본이 아니라 <b>평면 필드에서 재조립한 부분 집합</b>이다
    /// (<c>connection</c> 은 IP·포트·계정·프로토콜뿐, <c>hardware_spec</c> 은 스칼라뿐). 8.0.1 실측(2026-09-19):</para>
    /// <list type="bullet">
    /// <item>재조립 <c>connection</c> 을 PUT → <c>type</c> 이 <c>IP_DIRECT</c> 로 초기화 · <c>channel</c>·<c>parent_device_id</c> 소실(200 · 경고 없음).</item>
    /// <item><c>hardware_spec</c> 스칼라만 PUT → <c>components[]</c> 와 나머지 스칼라 전부 소실 — 부품이 지워지면 그 관측·설정도 함께 지워진다.</item>
    /// <item>같은 본문을 PATCH → 축은 <b>객체 병합</b>이라 위 값이 전부 보존되고 보낸 필드만 갱신된다.</item>
    /// </list>
    /// <para>즉 8.0 서버에서는 패널의 저장 버튼 한 번이 접속 방식과 부품 선언을 조용히 지우고 있었다.
    /// 6.3 은 축이 없어 PUT 이 안전하고, 그 서버의 PATCH 지원 범위를 가정하지 않기 위해 그대로 둔다(무회귀).</para>
    /// <para>PATCH 도 <c>components</c> <b>배열</b>은 통째 교체한다 — 그쪽은
    /// <see cref="HardwareSpecDto.AllowComponentsWrite"/> 게이트가 막는다.</para>
    /// </remarks>
    private Task<System.Net.Http.HttpResponseMessage> WriteExistingDeviceAsync<T>(string url, T dto) where T : BaseDeviceDto
        => IsAxisContract
            ? _apiService.PatchRequestAsync(url, dto)
            : _apiService.PutRequestAsync(url, dto);

    /// <summary>
    /// 축 모드 쓰기에서 <c>version</c> 을 <c>hardware_spec.firmware</c> 로 잇는다(§5 머리 이관표, 쓰기 방향).
    /// </summary>
    /// <remarks>
    /// <para><b>읽기는 이미 이어져 있었다</b> — <c>BaseDeviceDto.HardwareSpecCore</c> setter 가
    /// 응답의 <c>firmware</c> 를 <c>Version</c> 으로 역투영한다. 반대 방향이 비어 있어서
    /// 축 모드에서 <c>version</c> 은 <c>ShouldSerializeVersion() =&gt; !UseAxisWrite</c> 로 <b>드롭만</b> 됐고,
    /// 사용자가 펌웨어를 고쳐도 서버에 전달되지 않았다.</para>
    ///
    /// <para><b>없는 축을 만들지 않는다</b> — <c>hardware_spec</c> 이 <c>null</c> 이면 그대로 둔다.
    /// 여기서 새 객체를 만들면 <c>PUT</c> 이 서버의 <c>hardware_spec</c>(특히 <c>components[]</c> 부품 선언)을
    /// <b>통째 교체</b>해 형상 선언이 사라진다 — 문 위치 부품이 사라지면 문 상태를 영영 못 읽는다.
    /// 그래서 <b>기존 축이 있을 때만</b> 채운다.</para>
    ///
    /// <para><b>기존 값을 덮지 않는다</b> — <c>firmware</c> 에 이미 값이 있으면 그것이 서버 정본이다
    /// (읽기 역투영 때문에 <c>Version</c> 과 같을 뿐이다).</para>
    ///
    /// <para><b>길이 초과는 채우지 않는다</b> — 축 <c>firmware</c> 는 50자 상한이라 넘기면 422 로
    /// <b>저장 전체가 실패</b>한다. 잘라 보내면 사용자가 모르는 값이 저장되므로, 경고만 남기고 생략한다.</para>
    /// </remarks>
    private void LinkVersionToFirmware(BaseDeviceDto dto)
    {
        var version = dto.Version;
        if (string.IsNullOrWhiteSpace(version)) return;

        var spec = ResolveHardwareSpec(dto);
        if (spec == null) return;                                  // 없는 축을 만들지 않는다
        if (!string.IsNullOrWhiteSpace(spec.Firmware)) return;     // 서버 정본을 덮지 않는다

        var trimmed = version.Trim();
        if (trimmed.Length > HardwareSpecDto.AXIS_FIRMWARE_MAX_LENGTH)
        {
            _log?.Warning(
                $"[{nameof(DeviceApiService)}] version({trimmed.Length}자) 이 hardware_spec.firmware 상한" +
                $"({HardwareSpecDto.AXIS_FIRMWARE_MAX_LENGTH})을 넘어 전송을 생략한다 — 422 로 저장 전체가 실패하는 것을 피한다.");
            return;
        }

        spec.Firmware = trimmed;
    }

    /// <summary>
    /// 장비 DTO 의 <c>hardware_spec</c> 을 꺼낸다. 배후 저장소가 <c>protected</c> 라 파생 타입으로 갈라 읽는다.
    /// </summary>
    /// <remarks>모르는 타입이면 <c>null</c> — 추측으로 리플렉션하지 않는다(새 카테고리가 생기면 여기 추가).</remarks>
    private static HardwareSpecDto? ResolveHardwareSpec(BaseDeviceDto dto) => dto switch
    {
        CameraDeviceDto camera => camera.HardwareSpec,
        ControllerDeviceDto controller => controller.HardwareSpec,
        SensorDeviceDto sensor => sensor.HardwareSpec,
        SpeakerDeviceDto speaker => speaker.HardwareSpec,
        EnclosureDeviceDto enclosure => enclosure.HardwareSpec,
        GateDeviceDto gate => gate.HardwareSpec,
        LampDeviceDto lamp => lamp.HardwareSpec,
        _ => null,
    };
    #endregion

    #region - 조회 쿼리 공통 -
    /// <summary>
    /// 7.0 이상의 응답 프로필 파라미터(<c>?view=</c>·<c>?include=</c>)를 쿼리에 붙인다.
    /// </summary>
    /// <remarks>
    /// <para><b>버전 판단은 여기서 하지 않는다</b> — 호출부(<c>Devices.Ui</c> 의 <c>DeviceQueryPolicy</c>)가
    /// 계약 세대를 보고 값을 넣거나 <c>null</c> 로 둔다. API 계층이 또 분기하면 결정 지점이 둘이 된다.</para>
    /// <para><c>null</c>·공백이면 <b>키 자체를 붙이지 않는다</b> — 서버 어휘가 닫힌 집합으로 가는 중이라
    /// 빈 문자열은 422 위험이다.</para>
    /// </remarks>
    private static void AddViewInclude(Dictionary<string, string> parameters, string? view, string? include)
    {
        if (!string.IsNullOrWhiteSpace(view)) parameters["view"] = view!.Trim();
        if (!string.IsNullOrWhiteSpace(include)) parameters["include"] = include!.Trim();
    }

    /// <summary>
    /// 7.0 에서 신설된 목록 필터(종류축·<c>protocol</c>·<c>group_id</c>·<c>server_id</c> 등)를 붙인다.
    /// </summary>
    /// <remarks>
    /// <para><c>null</c>·공백이면 <b>키를 붙이지 않는다</b>.</para>
    /// <para>6.3 계약에서는 <b>보내지 않고 경고만 남긴다</b> — 6.3 은 선언하지 않은 쿼리를
    /// <b>조용히 무시</b>하므로 그냥 보내면 "걸렀는데 전건이 온다"는 침묵 실패가 된다.
    /// 필터가 안 걸린 결과를 걸린 것처럼 쓰는 쪽이 더 위험해 로그로 표면화한다.</para>
    /// </remarks>
    /// <summary>
    /// <b>운영 6.3.2 에도 존재하는</b> 필터를 붙인다 — 게이트 없이 전 판본 전송.
    /// <para>⚠ <see cref="AddAxisFilter(Dictionary{string,string},string,string?,string)"/> 로 보내면
    /// <b>6.3 에서 멀쩡한 필터를 잃는다</b>(경고만 남기고 생략된다). 실측(2026-09-18, 배포 스웨거 대조):</para>
    /// <list type="bullet">
    ///   <item><c>group_id</c> — 6.3.2 의 <b>controllers · sensors · cameras</b> 에 존재</item>
    ///   <item><c>server_id</c> — 6.3.2 의 <b>speakers</b> 에 존재</item>
    /// </list>
    /// <para>그 밖의 조합(예: enclosures 의 <c>group_id</c>)은 6.3.2 에 <b>없으므로</b>
    /// <see cref="AddAxisFilter(Dictionary{string,string},string,int?,string)"/> 를 쓴다.</para>
    /// </summary>
    private void AddLegacySafeFilter(Dictionary<string, string> parameters, string key, int? value, string caller)
    {
        if (!value.HasValue) return;
        parameters[key] = value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void AddAxisFilter(Dictionary<string, string> parameters, string key, string? value, string caller)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!IsAxisContract)
        {
            _log?.Warning($"[{caller}] contract={Contract} 에는 '{key}' 필터가 없다(미지 쿼리는 조용히 무시된다) — 전송 생략. value=\"{value}\"");
            return;
        }
        parameters[key] = value.Trim();
    }

    /// <inheritdoc cref="AddAxisFilter(Dictionary{string,string},string,string?,string)"/>
    private void AddAxisFilter(Dictionary<string, string> parameters, string key, int? value, string caller)
    {
        if (!value.HasValue) return;
        AddAxisFilter(parameters, key, value.Value.ToString(), caller);
    }

    /// <summary>
    /// 부대 편제 축(<c>unit_id</c>·<c>include_descendants</c>)을 붙인다 — <b>8.0 이상에서만</b>.
    /// </summary>
    /// <remarks>
    /// <para>⚠ 이 게이트가 <b>이 파일에서 가장 중요한 침묵 실패 방지선</b>이다. 6.3·7.0 은 미지 쿼리를
    /// 조용히 무시하므로 게이트 없이 보내면 <b>"부대로 걸렀는데 전건이 온다"</b>. 화면에는 다른 부대 장비가
    /// 섞여 나오는데 오류는 하나도 없어, 운영에서 사람이 오판하기 딱 좋은 형태다.</para>
    /// <para><c>include_descendants</c> 는 <c>unit_id</c> 와 <b>함께만</b> 의미가 있다 —
    /// 단독으로 주면 보내지 않고 경고한다.</para>
    /// </remarks>
    private void AddUnitScope(Dictionary<string, string> parameters, int? unitId, bool? includeDescendants, string caller)
    {
        if (!unitId.HasValue && !includeDescendants.HasValue) return;

        if (!IsUnitScopedContract)
        {
            _log?.Warning(
                $"[{caller}] contract={Contract} 에는 unit_id·include_descendants 가 없다 — 전송 생략. " +
                $"(6.3·7.0 은 미지 쿼리를 조용히 무시해 '부대로 걸렀는데 전건' 이 된다)");
            return;
        }

        if (!unitId.HasValue)
        {
            _log?.Warning($"[{caller}] include_descendants 는 unit_id 와 함께만 유효하다 — 단독 지정이라 전송 생략.");
            return;
        }

        parameters["unit_id"] = unitId.Value.ToString();
        if (includeDescendants.HasValue)
            parameters["include_descendants"] = includeDescendants.Value ? "true" : "false";
    }

    /// <summary>
    /// 이 계약에 존재하지 않는 축 경로 호출을 <b>네트워크에 나가기 전에</b> 차단한다.
    /// </summary>
    /// <remarks>
    /// 6.3 은 404, 7.0+ 는 경로가 있다. 실제로 호출하면 404 가 <c>NOT_FOUND</c> 로 매핑돼
    /// "그 장비가 없다"와 구분되지 않는다 — 원인을 사람에게 보여주려고 전용 코드로 돌려준다.
    /// </remarks>
    private ApiResponse<T> AxisEndpointUnavailable<T>(string what, string replacement)
    {
        var message = $"{what} 은(는) 현재 서버 계약({Contract})에 존재하지 않습니다. 대체: {replacement}";
        _log?.Error($"[{nameof(DeviceApiService)}] ENDPOINT_UNAVAILABLE {message}");
        return ApiResponse<T>.CreateError("ENDPOINT_UNAVAILABLE", message, replacement);
    }

    /// <inheritdoc cref="AxisEndpointUnavailable{T}(string,string)"/>
    private ApiListResponse<T> AxisEndpointUnavailableList<T>(string what, string replacement)
    {
        var message = $"{what} 은(는) 현재 서버 계약({Contract})에 존재하지 않습니다. 대체: {replacement}";
        _log?.Error($"[{nameof(DeviceApiService)}] ENDPOINT_UNAVAILABLE {message}");
        return ApiListResponse<T>.CreateError("ENDPOINT_UNAVAILABLE", message, replacement);
    }
    #endregion

    #region - Implementation of Interface -
    /// <summary>
    /// 서비스 초기화 및 시작
    /// <para>IApiService를 초기화하고 BaseUrl 설정을 로깅합니다</para>
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(DeviceApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 서비스 중지
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(DeviceApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - Controller Device API -
    /// <summary>
    /// GOP API를 통해 Controller 목록을 조회합니다
    /// <para>GET /devices/controllers 엔드포인트를 호출</para>
    /// </summary>    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값 false)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<ControllerDeviceDto>> GetControllersAsync(        string? status = null,
        bool includeSensors = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeController = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            if (includeSensors) parameters.Add("include_sensors", "true");
            parameters.Add("page", page.ToString());
            parameters.Add("limit", ClampLimit(limit, nameof(GetControllersAsync)).ToString());

            AddViewInclude(parameters, view, include);
            AddAxisFilter(parameters, "type_controller", typeController, nameof(GetControllersAsync));
            AddLegacySafeFilter(parameters, "group_id", groupId, nameof(GetControllersAsync));
            AddAxisFilter(parameters, "server_id", serverId, nameof(GetControllersAsync));
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetControllersAsync));

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/controllers", parameters);
            return await response.ToApiListResponseAsync<ControllerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetControllersAsync)}] Error: {ex.Message}");
            return ApiListResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get controllers", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Controller를 조회합니다
    /// <para>GET /devices/controllers/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">Controller ID</param>
    /// <param name="includeSensors">연결된 센서 포함 여부 (선택, 기본값 false)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> GetControllerByIdAsync(
        int id,
        bool includeSensors = false,
        CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeSensors) parameters.Add("include_sensors", "true");

            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/controllers/{id}", parameters);
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetControllerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get controller {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Controller를 생성합니다
    /// <para>POST /devices/controllers 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="dto">생성할 Controller 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> CreateControllerAsync(
        ControllerDeviceDto dto,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/controllers", ShapeWrite(dto));
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create controller", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Controller의 일부 속성을 수정합니다(부분 업데이트).
    /// <para>PATCH /devices/controllers/{id} 엔드포인트를 호출</para>
    /// <para>DTO에서 null이 아닌 속성만 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Controller ID</param>
    /// <param name="dto">수정할 속성 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> PatchControllerAsync(
        int id,
        ControllerDeviceDto dto,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/controllers/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch controller {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Controller의 전체 정보를 수정합니다(전체 업데이트).
    /// <para>PUT /devices/controllers/{id} 엔드포인트를 호출</para>
    /// <para>DTO의 모든 속성을 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Controller ID</param>
    /// <param name="dto">수정할 전체 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Controller DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ControllerDeviceDto>> UpdateControllerAsync(
        int id,
        ControllerDeviceDto dto,
        CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/controllers/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<ControllerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<ControllerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update controller {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Controller를 삭제합니다
    /// <para>DELETE /devices/controllers/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Controller ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteControllerAsync(
        int id,
        CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/controllers/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteControllerAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete controller {id}", ex.Message);
        }
    }
    #endregion

    #region - Sensor Device API -
    /// <summary>
    /// GOP API를 통해 Sensor 목록을 조회합니다
    /// <para>GET /devices/sensors 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="controllerId">Controller ID 필터 (선택)</param>    /// <param name="typeDevice">디바이스 타입 필터 (PIR, Laser, Fence, IoController, Cable, Contact, Underground 등) (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="includeController">연결된 제어기 포함 여부 (선택, 기본값 false)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<SensorDeviceDto>> GetSensorsAsync(
        int? controllerId = null,        string? typeDevice = null,
        string? status = null,
        bool includeController = false,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeSensor = null,
        int? groupId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (controllerId.HasValue) parameters.Add("controller_id", controllerId.Value.ToString());            if (!string.IsNullOrEmpty(typeDevice)) parameters.Add("type_device", typeDevice);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            if (includeController) parameters.Add("include_controller", "true");
            parameters.Add("page", page.ToString());
            parameters.Add("limit", ClampLimit(limit, nameof(GetSensorsAsync)).ToString());

            AddViewInclude(parameters, view, include);
            AddAxisFilter(parameters, "type_sensor", typeSensor, nameof(GetSensorsAsync));
            AddLegacySafeFilter(parameters, "group_id", groupId, nameof(GetSensorsAsync));
            // ⚠ 센서에는 server_id 를 싣지 않는다 — 8.0 에서 센서의 서버 축이 사라져 deprecated(422)이고
            //   상위 축은 controller_id 다. 그래서 파라미터 자체를 만들지 않았다.
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetSensorsAsync));

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/sensors", parameters);
            return await response.ToApiListResponseAsync<SensorDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSensorsAsync)}] Error: {ex.Message}");
            return ApiListResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get sensors", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Sensor를 조회합니다
    /// <para>GET /devices/sensors/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">Sensor ID</param>
    /// <param name="includeController">연결된 제어기 포함 여부 (선택, 기본값 false)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> GetSensorByIdAsync(
        int id,
        bool includeController = false,
        CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeController) parameters.Add("include_controller", "true");

            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/sensors/{id}", parameters);
            return await response.ToApiResponseAsync<SensorDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSensorByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get sensor {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Sensor를 생성합니다
    /// <para>POST /devices/sensors 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="dto">생성할 Sensor 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> CreateSensorAsync(SensorDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/sensors", ShapeWrite(dto));
            return await response.ToApiResponseAsync<SensorDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create sensor", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Sensor의 일부 속성을 수정합니다(부분 업데이트).
    /// <para>PATCH /devices/sensors/{id} 엔드포인트를 호출</para>
    /// <para>DTO에서 null이 아닌 속성만 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Sensor ID</param>
    /// <param name="dto">수정할 속성 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> PatchSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/sensors/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<SensorDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch sensor {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Sensor의 전체 정보를 수정합니다(전체 업데이트).
    /// <para>PUT /devices/sensors/{id} 엔드포인트를 호출</para>
    /// <para>DTO의 모든 속성을 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Sensor ID</param>
    /// <param name="dto">수정할 전체 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Sensor DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<SensorDeviceDto>> UpdateSensorAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/sensors/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<SensorDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<SensorDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update sensor {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Sensor를 삭제합니다
    /// <para>DELETE /devices/sensors/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Sensor ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteSensorAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/sensors/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteSensorAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete sensor {id}", ex.Message);
        }
    }
    #endregion

    #region - Camera Device API -
    /// <summary>
    /// GOP API를 통해 Camera 목록을 조회합니다
    /// <para>GET /devices/cameras 엔드포인트를 호출</para>
    /// </summary>    /// <param name="mode">카메라 모드 필터 (Fixed, PTZ 등) (선택)</param>
    /// <param name="category">카메라 분류 필터 (선택)</param>
    /// <param name="status">상태 필터 (ACTIVATED, ERROR, DEACTIVATED) (선택)</param>
    /// <param name="page">페이지 번호 (기본값 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<CameraDeviceDto>> GetCamerasAsync(        string? mode = null,
        string? category = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeCamera = null,
        string? protocol = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();            if (!string.IsNullOrEmpty(mode)) parameters.Add("mode", mode);
            if (!string.IsNullOrEmpty(category)) parameters.Add("category", category);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", ClampLimit(limit, nameof(GetCamerasAsync)).ToString());

            AddViewInclude(parameters, view, include);
            AddAxisFilter(parameters, "type_camera", typeCamera, nameof(GetCamerasAsync));   // 옛 category
            AddAxisFilter(parameters, "protocol", protocol, nameof(GetCamerasAsync));        // 옛 mode
            AddLegacySafeFilter(parameters, "group_id", groupId, nameof(GetCamerasAsync));
            AddAxisFilter(parameters, "server_id", serverId, nameof(GetCamerasAsync));
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetCamerasAsync));

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras", parameters);
            return await response.ToApiListResponseAsync<CameraDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCamerasAsync)}] Error: {ex.Message}");
            return ApiListResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get cameras", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Camera를 조회합니다
    /// <para>GET /devices/cameras/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">Camera ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> GetCameraByIdAsync(int id, CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{id}", parameters);
            return await response.ToApiResponseAsync<CameraDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCameraByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Camera를 생성합니다
    /// <para>POST /devices/cameras 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="dto">생성할 Camera 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> CreateCameraAsync(CameraDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/cameras", ShapeWrite(dto));
            return await response.ToApiResponseAsync<CameraDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create camera", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Camera의 일부 속성을 수정합니다(부분 업데이트).
    /// <para>PATCH /devices/cameras/{id} 엔드포인트를 호출</para>
    /// <para>DTO에서 null이 아닌 속성만 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Camera ID</param>
    /// <param name="dto">수정할 속성 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> PatchCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/cameras/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<CameraDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// 장비 위치(geolocation)만 부분 수정 — PATCH /devices/{kind}/{id} 에 {"geolocation": {...}} 만 전송.
    /// 좌표 외 필드(이름/IP/비번/hardware_spec 등)는 전송하지 않아 보존된다. (Symbol_Apply_DeviceLocation)
    /// </summary>
    public async Task<ApiResponse<object>> PatchGeolocationAsync(string deviceKindPath, int id, GeolocationDto geolocation, CancellationToken token = default)
    {
        try
        {
            var body = new { geolocation };
            var url = $"{_setupModel.Url}/devices/{deviceKindPath}/{id}";
            _log?.Info($"[PatchGeolocationAsync] PATCH {url} body={Newtonsoft.Json.JsonConvert.SerializeObject(body)}");
            var response = await _apiService.PatchRequestAsync(url, body);
            _log?.Info($"[PatchGeolocationAsync] 응답 status={(int)response.StatusCode} {response.StatusCode}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchGeolocationAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to patch geolocation {deviceKindPath}/{id}", ex.Message);
        }
    }

    /// <summary>
    /// 카메라 hardware_spec만 PATCH — MaxDetectionRange 포함 전체 HardwareSpec 전송, 그 외 카메라 필드(비번 등)는 미전송 보존.
    /// (Camera_PTZ_AimLocation / Symbol_Apply_DeviceLocation — full PUT의 H1 소거 위험 회피)
    /// </summary>
    public async Task<ApiResponse<object>> PatchHardwareSpecAsync(int id, HardwareSpecDto hardwareSpec, CancellationToken token = default)
    {
        try
        {
            // FR-09: 7.0 이상에서는 hardware_spec 키가 갈렸다(name·location·hardware·device_id 제거,
            // device_id→serial · hardware→hardware_rev). 단독 전송이라 여기서 직접 계약을 맞춘다.
            if (hardwareSpec != null) hardwareSpec.UseAxisWrite = IsAxisContract;
            var body = new { hardware_spec = hardwareSpec };
            var url = $"{_setupModel.Url}/devices/cameras/{id}";
            _log?.Info($"[PatchHardwareSpecAsync] PATCH {url} body={Newtonsoft.Json.JsonConvert.SerializeObject(body)}");
            var response = await _apiService.PatchRequestAsync(url, body);
            _log?.Info($"[PatchHardwareSpecAsync] 응답 status={(int)response.StatusCode} {response.StatusCode}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchHardwareSpecAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to patch hardware_spec cameras/{id}", ex.Message);
        }
    }

    /// <summary>축 값 부분 수정 본문에 실을 수 있는 최상위 키 — 이 밖은 보내기 전에 막는다.</summary>
    private static readonly HashSet<string> AxisPatchTopKeys = new(StringComparer.Ordinal)
    {
        "connection", "hardware_spec", "device_config", "unit_id",
    };

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.PatchDeviceAxesAsync" path="/summary"/>
    /// </summary>
    public async Task<ApiResponse<object>> PatchDeviceAxesAsync(
        string deviceTypePath,
        int deviceId,
        Newtonsoft.Json.Linq.JObject body,
        CancellationToken token = default)
    {
        if (!DeviceTypePaths.IsValid(deviceTypePath))
            return InvalidDeviceTypePath<object>(nameof(PatchDeviceAxesAsync), deviceTypePath);

        if (deviceId <= 0)
            return ApiResponse<object>.CreateError("VALIDATION_ERROR", "아직 서버에 없는 장비입니다.");

        if (!IsAxisContract)
        {
            return AxisEndpointUnavailable<object>(
                $"PATCH /api/devices/{deviceTypePath}/{{id}} (축 값 부분 수정)",
                "6.3 에는 접속 · 형상 · 설정 축이 없습니다 — 장비 본문의 평면 필드를 사용하십시오.");
        }

        var shaped = (Newtonsoft.Json.Linq.JObject?)body?.DeepClone() ?? new Newtonsoft.Json.Linq.JObject();

        // 8.0 미만에는 unit_id 가 쓰기 스키마에 없다 — 실리면 422 라 확실히 뺀다(ShapeWrite 와 같은 규칙).
        if (!IsUnitScopedContract) shaped.Remove("unit_id");
        // 8.0+ 에서 명시적 "unit_id": null 은 422 다(서버 회신 2026-09-28 Q-1 — 무소속 장비는 존재할 수 없다). 병합 PATCH 에서
        // 키가 없으면 지금 부대가 그대로이므로 키째 뺀다 — 호출부가 어떤 경로로 null 을 실어도 서버까지 가지 않는 마지막 관문.
        else if (shaped.TryGetValue("unit_id", out var unitToken) && unitToken.Type == Newtonsoft.Json.Linq.JTokenType.Null)
        {
            shaped.Remove("unit_id");
            _log?.Warning($"[{nameof(PatchDeviceAxesAsync)}] 명시적 unit_id=null 을 뺐습니다 — {deviceTypePath}/{deviceId}");
        }

        var unknown = shaped.Properties().Select(p => p.Name).Where(name => !AxisPatchTopKeys.Contains(name)).ToList();
        string? blocked = unknown.Count > 0 ? $"축 값 부분 수정에 실을 수 없는 키입니다: {string.Join(", ", unknown)}"
            : shaped.SelectToken("hardware_spec.components") != null ? "부품 배열은 PATCH 에서도 통째로 바뀝니다 — 부품 구성은 조립기로만 보냅니다."
            : shaped.Count == 0 ? "보낼 값이 없습니다."
            : null;
        if (blocked != null)
        {
            _log?.Error($"[{nameof(PatchDeviceAxesAsync)}] 차단 — {blocked}");
            return ApiResponse<object>.CreateError("VALIDATION_ERROR", blocked);
        }

        try
        {
            var url = $"{_setupModel.Url}/devices/{deviceTypePath.Trim().ToLowerInvariant()}/{deviceId}";
            _log?.Info($"[{nameof(PatchDeviceAxesAsync)}] PATCH {url} keys={string.Join(",", shaped.Properties().Select(p => p.Name))}");
            var response = await _apiService.PatchRequestAsync(url, shaped);
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchDeviceAxesAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to patch axes {deviceTypePath}/{deviceId}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Camera의 전체 정보를 수정합니다(전체 업데이트).
    /// <para>PUT /devices/cameras/{id} 엔드포인트를 호출</para>
    /// <para>DTO의 모든 속성을 업데이트됩니다</para>
    /// </summary>
    /// <param name="id">Camera ID</param>
    /// <param name="dto">수정할 전체 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Camera DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<CameraDeviceDto>> UpdateCameraAsync(int id, CameraDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/cameras/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<CameraDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update camera {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 Camera를 삭제합니다
    /// <para>DELETE /devices/cameras/{id} 엔드포인트를 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Camera ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteCameraAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/cameras/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete camera {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraSettingDto>> GetCameraSettingAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/settings");
            return await response.ToApiResponseAsync<CameraSettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetCameraSettingAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraSettingDto>.CreateError("INTERNAL_ERROR", $"Failed to get camera setting {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraSettingDto>> PatchCameraSettingAsync(int cameraId, CameraSettingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/settings", dto);
            return await response.ToApiResponseAsync<CameraSettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchCameraSettingAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraSettingDto>.CreateError("INTERNAL_ERROR", $"Failed to patch camera setting {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraSettingDto>> UpdateCameraSettingAsync(int cameraId, CameraSettingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/settings", dto);
            return await response.ToApiResponseAsync<CameraSettingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateCameraSettingAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraSettingDto>.CreateError("INTERNAL_ERROR", $"Failed to update camera setting {cameraId}", ex.Message);
        }
    }
    #endregion

    #region - Camera Preset API -

    public async Task<ApiResponse<PresetListDataDto>> GetPresetsAsync(int cameraId, bool includeRois = false, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeRois) parameters.Add("include_rois", "true");
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets", parameters);
            return await response.ToApiResponseAsync<PresetListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPresetsAsync)}] Error: {ex.Message}");
            return ApiResponse<PresetListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to get presets for camera {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> CreatePresetAsync(int cameraId, CameraPresetDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets", dto);
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreatePresetAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to create preset for camera {cameraId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> GetPresetByIdAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}");
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPresetByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to get preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> PatchPresetAsync(int cameraId, int presetId, CameraPresetDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}", dto);
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchPresetAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to patch preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<CameraPresetDto>> UpdatePresetAsync(int cameraId, int presetId, CameraPresetDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}", dto);
            return await response.ToApiResponseAsync<CameraPresetDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdatePresetAsync)}] Error: {ex.Message}");
            return ApiResponse<CameraPresetDto>.CreateError("INTERNAL_ERROR", $"Failed to update preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeletePresetAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/cameras/{cameraId}/presets/{presetId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeletePresetAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete preset {presetId}", ex.Message);
        }
    }

    #endregion

    #region - ROI API -

    public async Task<ApiResponse<RoiListDataDto>> GetRoisAsync(int presetId, bool includePoints = false, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (includePoints) parameters.Add("include_points", "true");
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois", parameters);
            return await response.ToApiResponseAsync<RoiListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetRoisAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to get ROIs for preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> CreateRoiAsync(int presetId, RoiDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois", dto);
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to create ROI for preset {presetId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> GetRoiByIdAsync(int presetId, int roiId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}");
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetRoiByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to get ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> PatchRoiAsync(int presetId, int roiId, RoiDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}", dto);
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to patch ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<RoiDto>> UpdateRoiAsync(int presetId, int roiId, RoiDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}", dto);
            return await response.ToApiResponseAsync<RoiDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<RoiDto>.CreateError("INTERNAL_ERROR", $"Failed to update ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteRoiAsync(int presetId, int roiId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/presets/{presetId}/rois/{roiId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteRoiAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete ROI {roiId}", ex.Message);
        }
    }

    #endregion

    #region - Point API -

    public async Task<ApiResponse<PointListDataDto>> GetPointsAsync(int roiId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/rois/{roiId}/points");
            return await response.ToApiResponseAsync<PointListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetPointsAsync)}] Error: {ex.Message}");
            return ApiResponse<PointListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to get points for ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<XyPointDto>> CreatePointAsync(int roiId, XyPointDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/rois/{roiId}/points", dto);
            return await response.ToApiResponseAsync<XyPointDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreatePointAsync)}] Error: {ex.Message}");
            return ApiResponse<XyPointDto>.CreateError("INTERNAL_ERROR", $"Failed to create point for ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<PointListDataDto>> ReplacePointsAsync(int roiId, XyPointBulkDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/rois/{roiId}/points", dto);
            return await response.ToApiResponseAsync<PointListDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(ReplacePointsAsync)}] Error: {ex.Message}");
            return ApiResponse<PointListDataDto>.CreateError("INTERNAL_ERROR", $"Failed to replace points for ROI {roiId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeletePointAsync(int roiId, int pointId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/rois/{roiId}/points/{pointId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeletePointAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete point {pointId}", ex.Message);
        }
    }

    #endregion

    #region - Speaker Device API -
    public async Task<ApiListResponse<SpeakerDeviceDto>> GetSpeakersAsync(        string? speakerType = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? speakerRole = null,
        string? typeSpeaker = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();            if (!string.IsNullOrEmpty(speakerType)) parameters.Add("speaker_type", speakerType);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", ClampLimit(limit, nameof(GetSpeakersAsync)).ToString());

            AddViewInclude(parameters, view, include);
            AddAxisFilter(parameters, "speaker_role", speakerRole, nameof(GetSpeakersAsync));   // 옛 speaker_type(역할)
            AddAxisFilter(parameters, "type_speaker", typeSpeaker, nameof(GetSpeakersAsync));   // 형상 — 역할과 다른 축
            AddAxisFilter(parameters, "group_id", groupId, nameof(GetSpeakersAsync));
            AddLegacySafeFilter(parameters, "server_id", serverId, nameof(GetSpeakersAsync));
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetSpeakersAsync));

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/speakers", parameters);
            return await response.ToApiListResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSpeakersAsync)}] Error: {ex.Message}");
            return ApiListResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get speakers", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> GetSpeakerByIdAsync(int id, CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/speakers/{id}", parameters);
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetSpeakerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get speaker {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> CreateSpeakerAsync(SpeakerDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/speakers", ShapeWrite(dto));
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create speaker", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> PatchSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/speakers/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch speaker {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<SpeakerDeviceDto>> UpdateSpeakerAsync(int id, SpeakerDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/speakers/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<SpeakerDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<SpeakerDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update speaker {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteSpeakerAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/speakers/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete speaker {id}", ex.Message);
        }
    }
    #endregion

    #region - Enclosure Device API -
    public async Task<ApiListResponse<EnclosureDeviceDto>> GetEnclosuresAsync(        string? doorStatus = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeEnclosure = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();            if (!string.IsNullOrEmpty(doorStatus)) parameters.Add("door_status", doorStatus);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", ClampLimit(limit, nameof(GetEnclosuresAsync)).ToString());

            AddViewInclude(parameters, view, include);
            AddAxisFilter(parameters, "type_enclosure", typeEnclosure, nameof(GetEnclosuresAsync));
            AddAxisFilter(parameters, "group_id", groupId, nameof(GetEnclosuresAsync));
            AddAxisFilter(parameters, "server_id", serverId, nameof(GetEnclosuresAsync));
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetEnclosuresAsync));
            // door_status 의 7.0+ 대체는 이 목록이 아니라 GetDevicesByComponentAsync(DOOR_SENSOR) 다.

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/enclosures", parameters);
            return await response.ToApiListResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosuresAsync)}] Error: {ex.Message}");
            return ApiListResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get enclosures", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> GetEnclosureByIdAsync(int id, CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}", parameters);
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosureByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get enclosure {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> CreateEnclosureAsync(EnclosureDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/enclosures", ShapeWrite(dto));
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create enclosure", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> PatchEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch enclosure {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureDeviceDto>> UpdateEnclosureAsync(int id, EnclosureDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/enclosures/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<EnclosureDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update enclosure {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteEnclosureAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/enclosures/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteEnclosureAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete enclosure {id}", ex.Message);
        }
    }
    #endregion

    #region - Gate Device API (통문, 서버 v6.3) -
    public async Task<ApiListResponse<GateDeviceDto>> GetGatesAsync(
        string? gateStatus = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeGate = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(gateStatus)) parameters.Add("gate_status", gateStatus);
            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());     // 서버 ge=1 — 0 은 VALIDATION_ERROR(2026-09-08 실측)
            parameters.Add("limit", ClampLimit(limit, nameof(GetGatesAsync)).ToString());
            AddAxisFilter(parameters, "type_gate", typeGate, nameof(GetGatesAsync));
            AddAxisFilter(parameters, "group_id", groupId, nameof(GetGatesAsync));
            AddAxisFilter(parameters, "server_id", serverId, nameof(GetGatesAsync));
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetGatesAsync));
            // gate_status 의 7.0+ 대체는 이 목록이 아니라 GetDevicesByComponentAsync(DOOR_ACTUATOR) 다.

            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/gates", parameters);
            return await response.ToApiListResponseAsync<GateDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetGatesAsync)}] Error: {ex.Message}");
            return ApiListResponse<GateDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get gates", ex.Message);
        }
    }

    public async Task<ApiResponse<GateDeviceDto>> GetGateByIdAsync(int id, CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/gates/{id}", parameters);
            return await response.ToApiResponseAsync<GateDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetGateByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<GateDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get gate {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<GateDeviceDto>> PatchGateAsync(int id, GateDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/gates/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<GateDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchGateAsync)}] Error: {ex.Message}");
            return ApiResponse<GateDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch gate {id}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<GateDeviceDto>> CreateGateAsync(GateDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/gates", ShapeWrite(dto));
            return await response.ToApiResponseAsync<GateDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateGateAsync)}] Error: {ex.Message}");
            return ApiResponse<GateDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create gate", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<GateDeviceDto>> UpdateGateAsync(int id, GateDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/gates/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<GateDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateGateAsync)}] Error: {ex.Message}");
            return ApiResponse<GateDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update gate {id}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<bool>> DeleteGateAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/gates/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteGateAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete gate {id}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public Task<ApiResponse<GateDeviceDto>> ControlGateAsync(int id, string doorCommand, CancellationToken token = default)
        => Task.FromResult(DoorControlRemoved<GateDeviceDto>(nameof(ControlGateAsync), "gates", id, "GATE_DOOR_SET"));

    /// <inheritdoc/>
    public Task<ApiResponse<EnclosureDeviceDto>> ControlEnclosureAsync(int id, string doorCommand, CancellationToken token = default)
        => Task.FromResult(DoorControlRemoved<EnclosureDeviceDto>(nameof(ControlEnclosureAsync), "enclosures", id, "ENCLOSURE_DOOR_SET"));

    /// <summary>
    /// 개폐 REST 경로 무력화 — <b>호출 전에 차단</b>한다.
    /// <para>운영 6.3.2 는 <c>/devices/gates*</c> 리소스가 없어 404, 개발 7.0.1·8.0.1 은
    /// <c>/control</c> 두 개가 모두 410 <c>ENDPOINT_REMOVED</c> 묘비(서버 <c>gates.py</c>·<c>enclosures.py</c>
    /// <c>raise_endpoint_removed</c>)다. 그대로 호출하면 사용자에게 엉뚱한 서버 오류가 보이므로
    /// 왕복을 만들지 않고 사유를 그대로 돌려준다.</para>
    /// <para>정본 채널은 NATS <c>GATE_DOOR_SET</c>·<c>ENCLOSURE_DOOR_SET</c>
    /// (subject <c>{domain}.{부대ID}.all.gate-door</c> / <c>.all.enclosure-door</c>, 발신 Central/GIS) —
    /// GIS 는 <c>GMaps.Ui</c> 의 <c>IDoorControlService</c> 로 발행한다(브로커 연동설계 v1.6 §7).</para>
    /// </summary>
    private ApiResponse<T> DoorControlRemoved<T>(string caller, string resource, int id, string natsCommand) where T : class
    {
        var message = $"'POST /api/devices/{resource}/{{id}}/control' 는 서버에서 제거됐습니다 — NATS {natsCommand} 로 발행하십시오.";
        _log?.Warning($"[{caller}] 차단 — {resource}/{id}: {message}");
        return ApiResponse<T>.CreateError("ENDPOINT_REMOVED", message,
            "REST 개폐 경로는 운영 404 / 개발 410 입니다. 개폐 명령은 클라 → 구동 담당 매니저 NATS 직행입니다.");
    }
    #endregion

    #region - Lamp Device API -
    public async Task<ApiListResponse<LampDeviceDto>> GetLampsAsync(        string? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default,
        string? view = null,
        string? include = null,
        string? typeLamp = null,
        int? groupId = null,
        int? serverId = null,
        int? unitId = null,
        bool? includeDescendants = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();            if (!string.IsNullOrEmpty(status)) parameters.Add("status", status);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", ClampLimit(limit, nameof(GetLampsAsync)).ToString());

            AddViewInclude(parameters, view, include);
            AddAxisFilter(parameters, "type_lamp", typeLamp, nameof(GetLampsAsync));
            AddAxisFilter(parameters, "group_id", groupId, nameof(GetLampsAsync));
            AddAxisFilter(parameters, "server_id", serverId, nameof(GetLampsAsync));
            AddUnitScope(parameters, unitId, includeDescendants, nameof(GetLampsAsync));

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/lamps", parameters);
            return await response.ToApiListResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetLampsAsync)}] Error: {ex.Message}");
            return ApiListResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get lamps", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> GetLampByIdAsync(int id, CancellationToken token = default,
        string? view = null,
        string? include = null)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            AddViewInclude(parameters, view, include);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/lamps/{id}", parameters);
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetLampByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to get lamp {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> CreateLampAsync(LampDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/lamps", ShapeWrite(dto));
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateLampAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to create lamp", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> PatchLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/lamps/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchLampAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to patch lamp {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<LampDeviceDto>> UpdateLampAsync(int id, LampDeviceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await WriteExistingDeviceAsync($"{_setupModel.Url}/devices/lamps/{id}", ShapeWrite(dto));
            return await response.ToApiResponseAsync<LampDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateLampAsync)}] Error: {ex.Message}");
            return ApiResponse<LampDeviceDto>.CreateError("INTERNAL_ERROR", $"Failed to update lamp {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteLampAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/lamps/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteLampAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete lamp {id}", ex.Message);
        }
    }
    #endregion

    #region - 부품 상태 일괄 조회 · 보고 · 설정 축 · 카탈로그 (A-devices D-3 · D-30~32) -
    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.GetDevicesByComponentAsync" path="/summary"/>
    /// </summary>
    /// <remarks>
    /// <para>나가기 전 검문 두 가지 — ① <c>component</c>/<c>component_type</c> 중 <b>정확히 하나</b>,
    /// ② 계약 <c>&gt;= V7_0</c>. 둘 다 서버가 각각 422·404 로 답하지만, 그 답이
    /// "필터 값이 틀렸다"·"장비가 없다"와 구분되지 않아 진단이 막힌다.</para>
    /// <para><b>page·limit 를 붙이지 않는다</b> — 8.0.1 스웨거에 이 경로의 페이징 쿼리가 <b>선언되어 있지 않다</b>
    /// (응답 봉투에는 <c>pagination</c> 키가 있다). 선언 없는 쿼리를 보내면 FastAPI 가 조용히 버리므로
    /// 페이징을 임의로 만들지 않고 서버 기본 동작을 그대로 쓴다. 필요해지면 스웨거를 다시 확인한 뒤 추가한다.</para>
    /// </remarks>
    public async Task<ApiListResponse<ComponentStateRowDto>> GetDevicesByComponentAsync(
        string? componentType = null,
        string? component = null,
        string? state = null,
        string? health = null,
        string? deviceType = null,
        CancellationToken token = default)
    {
        var hasType = !string.IsNullOrWhiteSpace(componentType);
        var hasKey = !string.IsNullOrWhiteSpace(component);

        if (hasType == hasKey)
        {
            var reason = hasType
                ? "component 와 component_type 을 함께 보낼 수 없습니다(서버 422)."
                : "component 또는 component_type 중 하나는 반드시 필요합니다(서버 422).";
            _log?.Error($"[{nameof(GetDevicesByComponentAsync)}] 차단 — {reason}");
            return ApiListResponse<ComponentStateRowDto>.CreateError("VALIDATION_ERROR", reason,
                "정확히 하나만 지정하십시오 — 유형으로 찾으려면 component_type(DOOR_SENSOR 등), 부품 key 로 찾으려면 component(door 등).");
        }

        if (!IsAxisContract)
        {
            return AxisEndpointUnavailableList<ComponentStateRowDto>(
                "GET /api/devices/by-component",
                "6.3 에서는 GET /api/devices/enclosures?door_status= · GET /api/devices/gates?gate_status= 를 사용하십시오.");
        }

        if (!string.IsNullOrWhiteSpace(deviceType) && !DeviceTypePaths.IsValid(deviceType))
        {
            var reason = $"device_type=\"{deviceType}\" 은 허용 어휘가 아닙니다(서버 404). 허용: {string.Join(" · ", DeviceTypePaths.All)}";
            _log?.Error($"[{nameof(GetDevicesByComponentAsync)}] 차단 — {reason}");
            return ApiListResponse<ComponentStateRowDto>.CreateError("VALIDATION_ERROR", reason, "URL 복수형만 허용됩니다(enclosure 가 아니라 enclosures).");
        }

        try
        {
            var parameters = new Dictionary<string, string>();
            if (hasType) parameters["component_type"] = componentType!.Trim();
            // ⚠ component(key) 는 대소문자·공백을 그대로 비교한다 — Trim 하지 않는다.
            if (hasKey) parameters["component"] = component!;
            if (!string.IsNullOrWhiteSpace(state)) parameters["state"] = state!.Trim();
            if (!string.IsNullOrWhiteSpace(health)) parameters["health"] = health!.Trim();
            if (!string.IsNullOrWhiteSpace(deviceType)) parameters["device_type"] = deviceType!.Trim();

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/by-component", parameters);
            return await response.ToApiListResponseAsync<ComponentStateRowDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDevicesByComponentAsync)}] Error: {ex.Message}");
            return ApiListResponse<ComponentStateRowDto>.CreateError("INTERNAL_ERROR", "Failed to get devices by component", ex.Message);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.PatchComponentStatusAsync" path="/summary"/>
    /// </summary>
    public async Task<ApiResponse<DeviceStatusWriteDataDto>> PatchComponentStatusAsync(
        string deviceTypePath,
        int deviceId,
        IDictionary<string, ComponentStatusDto> components,
        CancellationToken token = default)
    {
        if (!DeviceTypePaths.IsValid(deviceTypePath))
        {
            return InvalidDeviceTypePath<DeviceStatusWriteDataDto>(nameof(PatchComponentStatusAsync), deviceTypePath);
        }

        if (components == null || components.Count == 0)
        {
            const string reason = "보고할 부품이 없습니다 — component-status 는 항목 1개 이상이 필수입니다(서버 minProperties=1).";
            _log?.Error($"[{nameof(PatchComponentStatusAsync)}] 차단 — {reason}");
            return ApiResponse<DeviceStatusWriteDataDto>.CreateError("VALIDATION_ERROR", reason, null);
        }

        // observed_at·health 는 항목마다 필수다(ST-1). 빠뜨리면 서버 422 인데, 응답만 보면
        // 어느 부품이 문제인지 알기 어려워 여기서 부품 key 를 짚어 돌려준다.
        foreach (var pair in components)
        {
            var value = pair.Value;
            if (value == null
                || string.IsNullOrWhiteSpace(value.ObservedAt)
                || string.IsNullOrWhiteSpace(value.Health))
            {
                var reason = $"부품 '{pair.Key}' 보고에 observed_at·health 가 모두 필요합니다(항목마다 필수 — ST-1).";
                _log?.Error($"[{nameof(PatchComponentStatusAsync)}] 차단 — {reason}");
                return ApiResponse<DeviceStatusWriteDataDto>.CreateError("VALIDATION_ERROR", reason,
                    "observed_at 은 오프셋 포함 ISO 8601, health 는 OK·DEGRADED·FAULT·UNKNOWN 입니다.");
            }
        }

        if (!IsAxisContract)
        {
            return AxisEndpointUnavailable<DeviceStatusWriteDataDto>(
                $"PATCH /api/devices/{deviceTypePath}/{{id}}/component-status",
                "6.3 에는 부품 상태 축이 없습니다(문 위치가 장비 스칼라 필드였습니다).");
        }

        try
        {
            var path = deviceTypePath.Trim().ToLowerInvariant();
            var response = await _apiService.PatchRequestAsync(
                $"{_setupModel.Url}/devices/{path}/{deviceId}/component-status", components);
            return await response.ToApiResponseAsync<DeviceStatusWriteDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchComponentStatusAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceStatusWriteDataDto>.CreateError(
                "INTERNAL_ERROR", $"Failed to report component status for {deviceTypePath}/{deviceId}", ex.Message);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.GetDeviceConfigAsync" path="/summary"/>
    /// </summary>
    public async Task<ApiResponse<DeviceConfigWriteDataDto>> GetDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        CancellationToken token = default)
    {
        if (!DeviceTypePaths.IsValid(deviceTypePath))
            return InvalidDeviceTypePath<DeviceConfigWriteDataDto>(nameof(GetDeviceConfigAsync), deviceTypePath);

        if (!IsAxisContract)
        {
            return AxisEndpointUnavailable<DeviceConfigWriteDataDto>(
                $"GET /api/devices/{deviceTypePath}/{{id}}/config",
                "6.3 에서는 장비 본문의 threshold_config·heater_enabled·fan_enabled·is_record(카메라는 /settings)를 사용하십시오.");
        }

        try
        {
            var path = deviceTypePath.Trim().ToLowerInvariant();
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/{path}/{deviceId}/config");
            return await response.ToApiResponseAsync<DeviceConfigWriteDataDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceConfigAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceConfigWriteDataDto>.CreateError(
                "INTERNAL_ERROR", $"Failed to get device config for {deviceTypePath}/{deviceId}", ex.Message);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.PatchDeviceConfigAsync" path="/summary"/>
    /// </summary>
    public Task<ApiResponse<DeviceConfigWriteDataDto>> PatchDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        DeviceConfigAxisDto config,
        CancellationToken token = default)
        => WriteDeviceConfigAsync(deviceTypePath, deviceId, config, replace: false, nameof(PatchDeviceConfigAsync));

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.UpdateDeviceConfigAsync" path="/summary"/>
    /// </summary>
    public Task<ApiResponse<DeviceConfigWriteDataDto>> UpdateDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        DeviceConfigAxisDto config,
        CancellationToken token = default)
        => WriteDeviceConfigAsync(deviceTypePath, deviceId, config, replace: true, nameof(UpdateDeviceConfigAsync));

    /// <summary>PATCH·PUT <c>/config</c> 공통 경로 — 검문과 오류 문구를 한 곳에 둔다.</summary>
    private async Task<ApiResponse<DeviceConfigWriteDataDto>> WriteDeviceConfigAsync(
        string deviceTypePath,
        int deviceId,
        DeviceConfigAxisDto config,
        bool replace,
        string caller)
    {
        if (!DeviceTypePaths.IsValid(deviceTypePath))
            return InvalidDeviceTypePath<DeviceConfigWriteDataDto>(caller, deviceTypePath);

        if (config == null)
        {
            const string reason = "device_config 본문이 null 입니다.";
            _log?.Error($"[{caller}] 차단 — {reason}");
            return ApiResponse<DeviceConfigWriteDataDto>.CreateError("VALIDATION_ERROR", reason, null);
        }

        if (!IsAxisContract)
        {
            return AxisEndpointUnavailable<DeviceConfigWriteDataDto>(
                $"{(replace ? "PUT" : "PATCH")} /api/devices/{deviceTypePath}/{{id}}/config",
                "6.3 에는 device_config 축이 없습니다 — 장비 본문의 평면 필드를 사용하십시오.");
        }

        // 빈 본문은 PATCH 에서 아무 일도 하지 않고, PUT 에서는 축을 전부 지운다 —
        // 후자는 사고라서 나가기 전에 막는다(임계치·모드·부품 의도가 한 번에 사라진다).
        if (config.IsEmpty && replace)
        {
            const string reason = "PUT /config 에 빈 본문을 보내면 thresholds·modes·component_overrides 가 모두 삭제됩니다 — 차단했습니다.";
            _log?.Error($"[{caller}] 차단 — {reason}");
            return ApiResponse<DeviceConfigWriteDataDto>.CreateError("VALIDATION_ERROR", reason,
                "한 값만 고치려면 PatchDeviceConfigAsync 를, 통째 교체가 의도라면 보존할 섹션을 GetDeviceConfigAsync 로 받아 함께 실으십시오.");
        }

        try
        {
            var path = deviceTypePath.Trim().ToLowerInvariant();
            var endpoint = $"{_setupModel.Url}/devices/{path}/{deviceId}/config";

            var response = replace
                ? await _apiService.PutRequestAsync(endpoint, config)
                : await _apiService.PatchRequestAsync(endpoint, config);

            var result = await response.ToApiResponseAsync<DeviceConfigWriteDataDto>();

            // UNMATCHED_THRESHOLD 처럼 "거부하지 않은 경고" 는 봉투에만 실려 조용히 사라진다 — 로그로 표면화한다.
            if (result.Warnings != null)
            {
                foreach (var warning in result.Warnings)
                    _log?.Warning($"[{caller}] 서버 경고 {warning.Code} ({warning.Field}): {warning.Message}");
            }
            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"[{caller}] Error: {ex.Message}");
            return ApiResponse<DeviceConfigWriteDataDto>.CreateError(
                "INTERNAL_ERROR", $"Failed to write device config for {deviceTypePath}/{deviceId}", ex.Message);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.GetDeviceSpecCatalogAsync" path="/summary"/>
    /// </summary>
    public async Task<ApiResponse<DeviceSpecCatalogDto>> GetDeviceSpecCatalogAsync(
        bool includeInactive = false,
        CancellationToken token = default)
    {
        if (!IsAxisContract)
        {
            return AxisEndpointUnavailable<DeviceSpecCatalogDto>(
                "GET /api/devices/spec",
                "6.3 에는 어휘 카탈로그가 없습니다 — 코드 상수(EnumDeviceType 등)가 정본입니다.");
        }

        try
        {
            var parameters = new Dictionary<string, string>();
            // 기본값(false)일 때는 키를 붙이지 않는다 — 서버 기본과 같고, 쿼리를 짧게 유지한다.
            if (includeInactive) parameters["include_inactive"] = "true";

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/spec", parameters);
            return await response.ToApiResponseAsync<DeviceSpecCatalogDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceSpecCatalogAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceSpecCatalogDto>.CreateError("INTERNAL_ERROR", "Failed to get device spec catalog", ex.Message);
        }
    }

    /// <summary>
    /// <inheritdoc cref="IDeviceApiService.GetDeviceTypeSpecAsync" path="/summary"/>
    /// </summary>
    public async Task<ApiResponse<DeviceTypeSpecDto>> GetDeviceTypeSpecAsync(
        string deviceTypePath,
        bool includeInactive = false,
        CancellationToken token = default)
    {
        if (!DeviceTypePaths.IsValid(deviceTypePath))
            return InvalidDeviceTypePath<DeviceTypeSpecDto>(nameof(GetDeviceTypeSpecAsync), deviceTypePath);

        if (!IsAxisContract)
        {
            return AxisEndpointUnavailable<DeviceTypeSpecDto>(
                $"GET /api/devices/{deviceTypePath}/spec",
                "6.3 에는 어휘 카탈로그가 없습니다 — 코드 상수가 정본입니다.");
        }

        try
        {
            var parameters = new Dictionary<string, string>();
            if (includeInactive) parameters["include_inactive"] = "true";

            var path = deviceTypePath.Trim().ToLowerInvariant();
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/{path}/spec", parameters);
            return await response.ToApiResponseAsync<DeviceTypeSpecDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceTypeSpecAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceTypeSpecDto>.CreateError(
                "INTERNAL_ERROR", $"Failed to get device type spec for {deviceTypePath}", ex.Message);
        }
    }

    /// <summary>
    /// 축 경로의 <c>{device_type}</c> 세그먼트가 허용 어휘(URL 복수형 7종)가 아닐 때의 공통 오류.
    /// </summary>
    /// <remarks>
    /// 서버는 404 로 답하는데, 그 404 가 "장비가 없다"와 구분되지 않는다 — 특히 응답의
    /// <c>category_device</c>(<b>단수</b>)를 그대로 끼워 넣은 실수가 이 형태로 나타난다.
    /// </remarks>
    private ApiResponse<T> InvalidDeviceTypePath<T>(string caller, string? deviceTypePath)
    {
        var reason = $"device_type=\"{deviceTypePath}\" 은 허용 어휘가 아닙니다(서버 404). 허용: {string.Join(" · ", DeviceTypePaths.All)}";
        _log?.Error($"[{caller}] 차단 — {reason}");
        return ApiResponse<T>.CreateError("VALIDATION_ERROR", reason,
            "URL 복수형만 허용됩니다. 응답의 category_device 는 단수(enclosure)라 DeviceTypePaths.FromCategory 로 바꿔야 합니다.");
    }
    #endregion

    #region - Enclosure Metrics API (§5.5.9~12) -
    public async Task<EnclosureMetricSaveResponseDto> CreateEnclosureMetricAsync(
        int enclosureId, EnclosureMetricDto dto, CancellationToken token = default)
    {
        try
        {
            // FR-09: 7.0 이상 EnclosureMetricCreate 는 additionalProperties=false 이고
            // id·created_at·updated_at·enclosure_id 가 properties 에 없다(함체 id 는 경로가 정한다).
            dto?.ApplyWriteContract(IsAxisContract);
            var response = await _apiService.PostRequestAsync(
                $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics", dto);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<EnclosureMetricSaveResponseDto>(content);
            return result ?? new EnclosureMetricSaveResponseDto { Success = false, Message = "Parse error" };
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateEnclosureMetricAsync)}] Error: {ex.Message}");
            return new EnclosureMetricSaveResponseDto { Success = false, Message = ex.Message };
        }
    }

    public async Task<ApiListResponse<EnclosureMetricDto>> GetEnclosureMetricsAsync(
        int enclosureId, string? startTime = null, string? endTime = null,
        int limit = 100, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["limit"] = limit.ToString()
            };
            if (!string.IsNullOrEmpty(startTime)) parameters["start_time"] = startTime;
            if (!string.IsNullOrEmpty(endTime)) parameters["end_time"] = endTime;

            var response = await _apiService.GetRequestAsync(
                $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics", parameters);
            return await response.ToApiListResponseAsync<EnclosureMetricDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosureMetricsAsync)}] Error: {ex.Message}");
            return ApiListResponse<EnclosureMetricDto>.CreateError("INTERNAL_ERROR", "Failed to get enclosure metrics", ex.Message);
        }
    }

    public async Task<ApiResponse<EnclosureMetricDto>> GetEnclosureMetricLatestAsync(
        int enclosureId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync(
                $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics/latest");
            return await response.ToApiResponseAsync<EnclosureMetricDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEnclosureMetricLatestAsync)}] Error: {ex.Message}");
            return ApiResponse<EnclosureMetricDto>.CreateError("INTERNAL_ERROR", "Failed to get latest enclosure metric", ex.Message);
        }
    }

    public async Task<ApiResponse<MetricDeleteResultDto>> DeleteEnclosureMetricsAsync(
        int enclosureId, string? beforeDate = null, CancellationToken token = default)
    {
        try
        {
            var endpoint = $"{_setupModel.Url}/devices/enclosures/{enclosureId}/metrics";
            if (!string.IsNullOrEmpty(beforeDate))
                endpoint += $"?before_date={Uri.EscapeDataString(beforeDate)}";   // aware ISO(+09:00)의 '+' 손상 방지(%2B 인코딩)

            var response = await _apiService.DeleteRequestAsync(endpoint);
            return await response.ToApiResponseAsync<MetricDeleteResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteEnclosureMetricsAsync)}] Error: {ex.Message}");
            return ApiResponse<MetricDeleteResultDto>.CreateError("INTERNAL_ERROR", "Failed to delete enclosure metrics", ex.Message);
        }
    }
    #endregion

    #region - DeviceGroup CRUD -

    public async Task<ApiListResponse<DeviceGroupDto>> GetDeviceGroupsAsync(
        string? name = null, int page = 1, int limit = 20, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            };
            if (!string.IsNullOrEmpty(name))
                parameters["name"] = name;

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/groups", parameters);
            return await response.ToApiListResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceGroupsAsync)}] Error: {ex.Message}");
            return ApiListResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", "Failed to get device groups", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> GetDeviceGroupByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/devices/groups/{id}");
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDeviceGroupByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", $"Failed to get device group {id}", ex.Message);
        }
    }

    // 그룹 쓰기(POST · PATCH · PUT)는 응답 DTO 가 아니라 DeviceGroupWriteDto(name · description · unit_id)를 보낸다 —
    //   서버 스키마가 extra="forbid" 라 id · device_count · created_at 이 실리면 422 UNKNOWN_FIELD 다(라이브 실측 2026-09-24).
    //   unit_id 는 8.0 부터의 키라 그 미만에서는 지운다(장비 쓰기의 ShapeWrite 와 같은 규칙).
    private DeviceGroupWriteDto GroupWriteBody(DeviceGroupDto dto)
    {
        var body = DeviceGroupWriteDto.From(dto);
        if (!IsUnitScopedContract) body.UnitId = null;
        return body;
    }

    public async Task<ApiResponse<DeviceGroupDto>> CreateDeviceGroupAsync(DeviceGroupDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/groups", GroupWriteBody(dto));
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", "Failed to create device group", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> PatchDeviceGroupAsync(int id, DeviceGroupDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/devices/groups/{id}", GroupWriteBody(dto));
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", $"Failed to patch device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupDto>> UpdateDeviceGroupAsync(int id, DeviceGroupDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/devices/groups/{id}", GroupWriteBody(dto));
            return await response.ToApiResponseAsync<DeviceGroupDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupDto>.CreateError("INTERNAL_ERROR", $"Failed to update device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> DeleteDeviceGroupAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/groups/{id}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteDeviceGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", $"Failed to delete device group {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<DeviceGroupAssignResultDto>> AssignDevicesToGroupAsync(
        int groupId, DeviceGroupAssignRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/devices/groups/{groupId}/devices", dto);
            return await response.ToApiResponseAsync<DeviceGroupAssignResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AssignDevicesToGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupAssignResultDto>.CreateError("INTERNAL_ERROR", "Failed to assign devices to group", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> RemoveDeviceFromGroupAsync(int groupId, int deviceId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/groups/{groupId}/devices/{deviceId}");
            return await response.ToApiResponseAsync<object>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(RemoveDeviceFromGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<object>.CreateError("INTERNAL_ERROR", "Failed to remove device from group", ex.Message);
        }
    }

    // v4.3: 일괄 제거 (body-DELETE) — DeviceGroupAssignRequestDto.device_ids 재사용
    public async Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveDevicesFromGroupAsync(
        int groupId, DeviceGroupAssignRequestDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/devices/groups/{groupId}/devices", dto);
            return await response.ToApiResponseAsync<DeviceGroupBulkRemoveResultDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(RemoveDevicesFromGroupAsync)}] Error: {ex.Message}");
            return ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("INTERNAL_ERROR", "Failed to bulk-remove devices from group", ex.Message);
        }
    }

    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;

    /// <summary>서버 계약 세대 프로브(FR-09). <c>null</c> 이면 6.3 으로 간주한다.</summary>
    private readonly IServerContractProbe? _contractProbe;
    #endregion
}
