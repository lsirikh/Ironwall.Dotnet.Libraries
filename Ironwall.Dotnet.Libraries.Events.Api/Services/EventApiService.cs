using Ironwall.Dotnet.Libraries.Messages.Defines.Commons;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Events.Api.Services;
/****************************************************************************
   Purpose      : Event API Service Implementation (GOP RESTful API 연동)
   Created By   : GHLee
   Created On   : 11/11/2025 12:00:00 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : GOP_Restful_Api_연동설계.md 기반 Event API 호출 서비스 구현
                  - IApiService (HTTP Client Wrapper)를 활용한 GOP RESTful API 호출
                  - RESTful API 표준 네이밍 컨벤션 사용 (Get/Create 패턴)
                  - ResponseHelper를 통한 HttpResponseMessage → ApiResponse 변환
                  - 모든 예외를 ApiResponse 에러 형태로 반환하여 호출자가 안전하게 처리
****************************************************************************/

/// <summary>
/// Event API 서비스 구현체
/// <para>IApiService를 활용하여 GOP RESTful API를 호출하고 결과를 DTO로 변환합니다.</para>
/// <para>RESTful API 표준 네이밍 (Get/Create/Patch/Update/Delete)을 따릅니다.</para>
/// </summary>
public class EventApiService : IEventApiService
{
    #region - Ctors -
    /// <summary>
    /// EventApiService 생성자
    /// </summary>
    /// <param name="log">로그 서비스</param>
    /// <param name="apiService">HTTP API 클라이언트 서비스</param>
    /// <param name="setupModel">Event API 설정 모델</param>
    /// <param name="contractProbe">
    /// 서버 계약 세대 프로브. <c>null</c>(미등록)이면 <see cref="EnumServerContract.V6_3"/> 로 간주한다 —
    /// 운영이 6.3.2 이므로 틀렸을 때 손해가 가장 작은 쪽이다.
    /// <para>⚠ <b>동치 비교(==) 금지, 항상 &gt;= 로 판정</b>한다. 판본은 앞으로도 올라간다.</para>
    /// </param>
    public EventApiService(
        ILogService log,
        IApiService apiService,
        ApiSetupModel setupModel,
        IServerContractProbe? contractProbe = null)
    {
        _log = log;
        _apiService = apiService;
        _setupModel = setupModel;
        _contractProbe = contractProbe;
    }

    /// <summary>현재 서버 계약 세대. 프로브가 없거나 미확보면 <see cref="EnumServerContract.V6_3"/>.</summary>
    private EnumServerContract Contract => _contractProbe?.Contract ?? EnumServerContract.V6_3;

    /// <summary>운영 이벤트 카테고리(<c>/api/events/operations</c>)가 있는 판본인가 — 7.0 이상.</summary>
    private bool HasOperationEvents => Contract >= EnumServerContract.V7_0;

    /// <summary>부대 편제 축(<c>unit_id</c>·<c>include_descendants</c>)이 있는 판본인가 — 8.0 이상.</summary>
    private bool IsUnitScopedContract => Contract >= EnumServerContract.V8_0;

    /// <summary>
    /// 탐지 목록에 <c>type_event</c> 쿼리가 있는 판본인가 — <b>8.0 이상</b>(F-22).
    /// <para>⚠ 부대 축(<see cref="IsUnitScopedContract"/>)과 판정값이 같지만 <b>근거가 다른 축</b>이다 —
    /// 서버가 둘 중 하나만 옮기면 같이 틀리므로 이름을 분리해 둔다.</para>
    /// </summary>
    private bool HasEventTypeFilter => Contract >= EnumServerContract.V8_0;
    #endregion

    #region - Implementation of Interface -
    /// <summary>
    /// 서비스 초기화 및 시작
    /// <para>IApiService를 초기화하고 BaseUrl 설정을 로깅합니다.</para>
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task ExecuteAsync(CancellationToken token = default)
    {
        _apiService.Initialize();
        _log?.Info($"[{nameof(EventApiService)}] Initialized with BaseUrl: {_setupModel.Url}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 서비스 중지
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>완료된 Task</returns>
    public Task StopAsync(CancellationToken token = default)
    {
        _log?.Info($"[{nameof(EventApiService)}] Stopping service...");
        return Task.CompletedTask;
    }
    #endregion

    #region - Detection Event API -
    /// <summary>
    /// GOP API를 통해 Detection Event 목록을 조회합니다.
    /// <para>GET /events/detections 엔드포인트 호출</para>
    /// <para>침입 감지 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식, 예: 2024-01-01T00:00:00.000Z) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="status">상태 필터 (선택) — 서버 <c>action_reported</c>(bool) 로 매핑된다.</param>
    /// <param name="result">탐지 결과 필터(<c>EnumDetectionType</c> 어휘). 6.3.2·8.0.1 양쪽 지원.</param>
    /// <param name="typeEvent">이벤트 종류 필터(<c>EnumEventType</c> 어휘). <b>서버 8.0 이상 전용</b>.</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Detection Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<DetectionEventDto>> GetDetectionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        string? status = null,
        string? result = null,
        string? typeEvent = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        // F-22: 닫힌 어휘 파라미터는 **전송 전에** 검증한다 — 서버가 어휘 밖 값에 422 를 내므로
        //   왕복을 만들 이유가 없고, 실패 사유를 여기서 남겨야 원인이 드러난다.
        if (!TryNormalizeVocabulary<EnumDetectionType>(result, nameof(result), out var resultQuery, out var resultError))
            return ApiListResponse<DetectionEventDto>.CreateError("INVALID_ARGUMENT", resultError!, $"result={result}");
        if (!TryNormalizeVocabulary<EnumEventType>(typeEvent, nameof(typeEvent), out var typeEventQuery, out var typeEventError))
            return ApiListResponse<DetectionEventDto>.CreateError("INVALID_ARGUMENT", typeEventError!, $"type_event={typeEvent}");
        // ⚠ `type_event` 는 8.0 신설이다. 6.3.2 로 보내면 FastAPI 가 **조용히 무시**해
        //   "필터가 걸린 줄 알았는데 전건" 이라는 최악의 침묵 실패가 된다.
        //   unit_id 선례(경고 후 미전송)와 달리 **여기서는 요청 자체를 실패**시킨다 —
        //   호출부가 이 필터를 명시했다면 전건 응답은 정답이 아니기 때문이다.
        if (typeEventQuery != null && !HasEventTypeFilter)
        {
            _log?.Warning($"[{nameof(GetDetectionEventsAsync)}] type_event 는 서버 8.0 이상 전용 — 현재 {Contract} 라 요청을 보내지 않음(전건 오인 방지)");
            return ApiListResponse<DetectionEventDto>.CreateError(
                "NOT_SUPPORTED",
                "이벤트 종류(type_event) 필터는 서버 8.0 이상에서만 제공됩니다.",
                $"contract={Contract}, type_event={typeEventQuery}");
        }
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            // PRD v2.1 서버 계약: 장치 필터 = device_id 단일(구 controller/sensor/type_device 제거 — detections.py:213).
            // 구명 "sensor"는 FastAPI가 조용히 무시해 전체 데이터가 반환되던 실버그(2026-08-06 그룹 탐지 이력 B2/B3 근원).
            if (sensor.HasValue) parameters.Add("device_id", sensor.Value.ToString());
            // F-22: `status` 인자를 서버 계약명 `action_reported` 로 **매핑**한다(종전에는 받고 버렸다).
            //   근거 — 이 도메인에서 이벤트의 "status" 는 `EnumTrueFalse` 로 표현되는 **조치보고 여부**다
            //   (`DetectionEventModel.Status`). 서버는 `action_reported: Optional[bool]` 로 받고
            //   **6.3.2·8.0.1 양쪽 모두** 이 쿼리를 지원한다(스웨거 실측) — 판본 분기가 필요 없다.
            //   ⚠ bool 로 확정되지 않는 값은 **보내지 않는다**(서버가 422 를 낸다).
            if (TryToServerBool(status, out var actionReportedFlag))
                parameters.Add("action_reported", actionReportedFlag);
            else if (!string.IsNullOrWhiteSpace(status))
                _log?.Warning($"[{nameof(GetDetectionEventsAsync)}] status 값을 action_reported(bool)로 해석할 수 없어 전송하지 않음");
            // F-22: 탐지 결과 필터. **6.3.2·8.0.1 양쪽 지원**(스웨거 실측) — 판본 게이트 없음.
            //   ⚠ 빈 값은 붙이지 않는다(서버 어휘가 닫히는 중이라 빈 문자열은 422).
            if (resultQuery != null) parameters.Add("result", resultQuery);
            // F-22: 이벤트 종류 필터 — 위에서 8.0 게이트를 통과한 경우에만 값이 존재한다.
            if (typeEventQuery != null) parameters.Add("type_event", typeEventQuery);
            // controller: 서버에 대응 파라미터가 없다(v7.0 에서 장치 필터가 device_id 단일로 통합됐다).
            //   죽은 파라미터는 보내지 않는다 — FastAPI 가 조용히 무시해 "필터가 걸린 줄 알았는데 전건"이 된다.
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/detections", parameters);
            return await response.ToApiListResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get detection events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Detection Event를 조회합니다.
    /// <para>GET /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Detection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> GetDetectionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/detections/{id}");
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get detection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Detection Event를 생성합니다.
    /// <para>POST /events/detections 엔드포인트 호출</para>
    /// <para>외부 시스템에서 감지 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Detection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> CreateDetectionEventAsync(DetectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            // ⚠ REST 본문에서만 응답 전용 `device` 를 뺀다(F-01).
            //    ShouldSerialize 로 영구히 끄면 **NATS 발행 본문까지** 깨진다 —
            //    ACTION_REPORT 가 같은 DTO 를 태우고 GIS.md 는 from_event.device 를 요구한다.
            //    같은 객체가 이후 발행에 재사용될 수 있으므로 finally 로 반드시 되돌린다.
            if (dto != null) dto.SuppressDeviceOnRestWrite = true;
            try
            {
                var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/detections", dto);
                return MapMissingDevice(await response.ToApiResponseAsync<DetectionEventDto>(), dto?.DeviceId ?? 0);
            }
            finally { if (dto != null) dto.SuppressDeviceOnRestWrite = false; }
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create detection event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Detection Event ID</param>
    /// <param name="dto">수정할 Detection Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> PatchDetectionEventAsync(int id, DetectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/detections/{id}", dto);
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch detection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Detection Event ID</param>
    /// <param name="dto">전체 Detection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Detection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<DetectionEventDto>> UpdateDetectionEventAsync(int id, DetectionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/detections/{id}", dto);
            return await response.ToApiResponseAsync<DetectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update detection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Detection Event를 삭제합니다.
    /// <para>DELETE /events/detections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Detection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteDetectionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/detections/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteDetectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete detection event {id}", ex.Message);
        }
    }
    #endregion

    #region - Malfunction Event API -
    /// <summary>
    /// GOP API를 통해 Malfunction Event 목록을 조회합니다.
    /// <para>GET /events/malfunctions 엔드포인트 호출</para>
    /// <para>장애 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="reason">장애 사유 필터(<c>EnumFaultType</c> 어휘). 6.3.2·8.0.1 양쪽 지원.</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<MalfunctionEventDto>> GetMalfunctionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        string? reason = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        // F-22: 닫힌 어휘(`EnumFaultType`)를 전송 전에 검증한다 — 어휘 밖이면 왕복 없이 실패.
        if (!TryNormalizeVocabulary<EnumFaultType>(reason, nameof(reason), out var reasonQuery, out var reasonError))
            return ApiListResponse<MalfunctionEventDto>.CreateError("INVALID_ARGUMENT", reasonError!, $"reason={reason}");
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            // PRD v2.1 서버 계약: 장치 필터 = device_id 단일(malfunctions.py:208) — detections 동일 계열 잠복 함정 정리(버그헌트 E4)
            if (sensor.HasValue) parameters.Add("device_id", sensor.Value.ToString());
            // F-22: 장애 사유 필터. **6.3.2·8.0.1 양쪽 지원**(스웨거 실측) — 판본 게이트 없음.
            //   ⚠ 빈 값은 붙이지 않는다(서버 422 방지).
            if (reasonQuery != null) parameters.Add("reason", reasonQuery);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/malfunctions", parameters);
            return await response.ToApiListResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMalfunctionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get malfunction events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Malfunction Event를 조회합니다.
    /// <para>GET /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Malfunction Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> GetMalfunctionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}");
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMalfunctionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get malfunction event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Malfunction Event를 생성합니다.
    /// <para>POST /events/malfunctions 엔드포인트 호출</para>
    /// <para>외부 시스템에서 장애 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Malfunction Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> CreateMalfunctionEventAsync(MalfunctionEventDto dto, CancellationToken token = default)
    {
        try
        {
            // ⚠ REST 본문에서만 응답 전용 `device` 를 뺀다(F-01).
            //    ShouldSerialize 로 영구히 끄면 **NATS 발행 본문까지** 깨진다 —
            //    ACTION_REPORT 가 같은 DTO 를 태우고 GIS.md 는 from_event.device 를 요구한다.
            //    같은 객체가 이후 발행에 재사용될 수 있으므로 finally 로 반드시 되돌린다.
            if (dto != null) dto.SuppressDeviceOnRestWrite = true;
            try
            {
                var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/malfunctions", dto);
                return MapMissingDevice(await response.ToApiResponseAsync<MalfunctionEventDto>(), dto?.DeviceId ?? 0);
            }
            finally { if (dto != null) dto.SuppressDeviceOnRestWrite = false; }
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create malfunction event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Malfunction Event ID</param>
    /// <param name="dto">수정할 Malfunction Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> PatchMalfunctionEventAsync(int id, MalfunctionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}", dto);
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch malfunction event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Malfunction Event ID</param>
    /// <param name="dto">전체 Malfunction Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Malfunction Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<MalfunctionEventDto>> UpdateMalfunctionEventAsync(int id, MalfunctionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}", dto);
            return await response.ToApiResponseAsync<MalfunctionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<MalfunctionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update malfunction event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Malfunction Event를 삭제합니다.
    /// <para>DELETE /events/malfunctions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Malfunction Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteMalfunctionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/malfunctions/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMalfunctionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete malfunction event {id}", ex.Message);
        }
    }
    #endregion

    #region - Connection Event API -
    /// <summary>
    /// GOP API를 통해 Connection Event 목록을 조회합니다.
    /// <para>GET /events/connections 엔드포인트 호출</para>
    /// <para>디바이스 연결 상태 변경 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="controller">Controller ID 필터 (선택)</param>
    /// <param name="sensor">Sensor ID 필터 (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Connection Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<ConnectionEventDto>> GetConnectionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? controller = null,
        int? sensor = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            // PRD v2.1 서버 계약: 장치 필터 = device_id 단일 — detections 동일 계열 잠복 함정 정리(버그헌트 E4)
            if (sensor.HasValue) parameters.Add("device_id", sensor.Value.ToString());
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/connections", parameters);
            return await response.ToApiListResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetConnectionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get connection events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Connection Event를 생성합니다.
    /// <para>POST /events/connections 엔드포인트 호출</para>
    /// <para>외부 시스템에서 연결 상태 변경 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Connection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> CreateConnectionEventAsync(ConnectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            // ⚠ REST 본문에서만 응답 전용 `device` 를 뺀다(F-01).
            //    ShouldSerialize 로 영구히 끄면 **NATS 발행 본문까지** 깨진다 —
            //    ACTION_REPORT 가 같은 DTO 를 태우고 GIS.md 는 from_event.device 를 요구한다.
            //    같은 객체가 이후 발행에 재사용될 수 있으므로 finally 로 반드시 되돌린다.
            if (dto != null) dto.SuppressDeviceOnRestWrite = true;
            try
            {
                var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/connections", dto);
                return MapMissingDevice(await response.ToApiResponseAsync<ConnectionEventDto>(), dto?.DeviceId ?? 0);
            }
            finally { if (dto != null) dto.SuppressDeviceOnRestWrite = false; }
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create connection event", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Connection Event를 조회합니다.
    /// <para>GET /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Connection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> GetConnectionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/connections/{id}");
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetConnectionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get connection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Connection Event ID</param>
    /// <param name="dto">수정할 Connection Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> PatchConnectionEventAsync(int id, ConnectionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/connections/{id}", dto);
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch connection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Connection Event ID</param>
    /// <param name="dto">전체 Connection Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Connection Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ConnectionEventDto>> UpdateConnectionEventAsync(int id, ConnectionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/connections/{id}", dto);
            return await response.ToApiResponseAsync<ConnectionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ConnectionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update connection event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Connection Event를 삭제합니다.
    /// <para>DELETE /events/connections/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Connection Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteConnectionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/connections/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteConnectionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete connection event {id}", ex.Message);
        }
    }
    #endregion

    #region - Action Event API -
    /// <summary>
    /// GOP API를 통해 Action Event 목록을 조회합니다.
    /// <para>GET /events/actions 엔드포인트 호출</para>
    /// <para>사용자 동작 이벤트를 조회합니다.</para>
    /// </summary>
    /// <param name="startDate">시작 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="endDate">종료 일시 (ISO 8601 형식) (선택)</param>
    /// <param name="page">페이지 번호 (기본값: 1)</param>
    /// <param name="limit">페이지당 항목 수 (기본값: 20)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Action Event DTO 목록을 포함한 API 응답</returns>
    public async Task<ApiListResponse<ActionEventDto>> GetActionEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/actions", parameters);
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetActionEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", "Failed to get action events", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 새로운 Action Event를 생성합니다.
    /// <para>POST /events/actions 엔드포인트 호출</para>
    /// <para>외부 시스템에서 사용자 동작 이벤트를 GOP로 전송할 때 사용합니다.</para>
    /// </summary>
    /// <param name="dto">생성할 Action Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>생성된 Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> CreateActionEventAsync(ActionEventCreateDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/events/actions", dto);
            var result = await response.ToApiResponseAsync<ActionEventDto>();
            // 201 의 data 원문을 따로 든다 — NATS ACTION_REPORT body 가 이것을 그대로 싣는다(브로커 명세 §6.4, WP-1 ⑳).
            if (result.Success) result.RawData = await ReadRawDataAsync(response);
            return result;
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", "Failed to create action event", ex.Message);
        }
    }

    /// <summary>응답 봉투의 <c>data</c> 원문. 못 읽으면 <c>null</c>(부르는 쪽이 조립으로 폴백한다).</summary>
    private async Task<Newtonsoft.Json.Linq.JToken?> ReadRawDataAsync(System.Net.Http.HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            // 시각 문자열을 DateTime 으로 바꾸지 않는다 — 기본 파싱(DateParseHandling.DateTime)은 다시 쓸 때 마이크로초 끝 0 을 잘라
            //   ACTION_REPORT body 가 서버 data 와 글자 단위로 달라졌다(.341750 → .34175, 헤디드 r18-e1 EVT-E2E-049). 원문은 원문대로.
            using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(content))
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
            };
            return Newtonsoft.Json.Linq.JToken.ReadFrom(reader) is Newtonsoft.Json.Linq.JObject envelope
                   && envelope["data"] is Newtonsoft.Json.Linq.JObject data ? data : null;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(CreateActionEventAsync)}] 응답 data 원문을 읽지 못함 — ACTION_REPORT 는 조립으로 폴백: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// GOP API를 통해 특정 ID의 Action Event를 조회합니다.
    /// <para>GET /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">Action Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> GetActionEventByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/actions/{id}");
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetActionEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get action event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event의 일부 속성을 수정합니다.
    /// <para>PATCH /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Action Event ID</param>
    /// <param name="dto">수정할 Action Event 정보 DTO (부분 업데이트)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> PatchActionEventAsync(int id, ActionEventDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/events/actions/{id}", dto);
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to patch action event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event의 전체 데이터를 교체합니다.
    /// <para>PUT /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">수정할 Action Event ID</param>
    /// <param name="dto">전체 Action Event 정보 DTO</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>수정된 Action Event DTO를 포함한 API 응답</returns>
    public async Task<ApiResponse<ActionEventDto>> UpdateActionEventAsync(int id, ActionEventReplaceDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/events/actions/{id}", dto);
            return await response.ToApiResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to update action event {id}", ex.Message);
        }
    }

    /// <summary>
    /// GOP API를 통해 Action Event를 삭제합니다.
    /// <para>DELETE /events/actions/{id} 엔드포인트 호출</para>
    /// </summary>
    /// <param name="id">삭제할 Action Event ID</param>
    /// <param name="token">취소 토큰 (선택)</param>
    /// <returns>삭제 성공 여부를 포함한 API 응답</returns>
    public async Task<ApiResponse<bool>> DeleteActionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/events/actions/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteActionEventAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete action event {id}", ex.Message);
        }
    }
    #endregion

    #region - Detection/Malfunction Action 조회 -
    // v4.6: ActionEvent 1:N — 경로 /actions(복수) + 배열(ApiListResponse). 미존재 시 data:[]
    public async Task<ApiListResponse<ActionEventDto>> GetDetectionActionsAsync(int detectionId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/detections/{detectionId}/actions");
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionActionsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get actions of detection {detectionId}", ex.Message);
        }
    }

    public async Task<ApiListResponse<ActionEventDto>> GetMalfunctionActionsAsync(int malfunctionId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/malfunctions/{malfunctionId}/actions");
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMalfunctionActionsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get actions of malfunction {malfunctionId}", ex.Message);
        }
    }
    #endregion

    #region - Detection Log -
    public async Task<ApiListResponse<DetectionLogDto>> GetDetectionLogsAsync(
        string? startDate = null,
        string? endDate = null,
        int page = 1,
        int limit = 20,
        int? deviceId = null,
        bool? actionReported = null,
        string? result = null,
        int? unitId = null,
        bool? includeDescendants = null,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            // F-18: 서버가 **6.3.2 부터** 받는 필터 3종 — 종전에는 전건을 받아 클라에서 후필터했다.
            if (deviceId.HasValue) parameters.Add("device_id", deviceId.Value.ToString());
            // ⚠ 소문자 bool 로 보낸다(서버 Optional[bool] — 다른 표기는 422).
            if (actionReported.HasValue) parameters.Add("action_reported", actionReported.Value ? "true" : "false");
            if (!string.IsNullOrWhiteSpace(result)) parameters.Add("result", result);
            // ⚠ 부대 축은 **8.0 이상에서만 전송**한다. 6.3.2·7.0.1 에는 이 쿼리가 없어
            //    조용히 무시되고 "부대로 걸렀는데 전건" 이라는 최악의 침묵 실패가 된다.
            if (IsUnitScopedContract)
            {
                if (unitId.HasValue) parameters.Add("unit_id", unitId.Value.ToString());
                if (includeDescendants.HasValue) parameters.Add("include_descendants", includeDescendants.Value ? "true" : "false");
            }
            else if (unitId.HasValue || includeDescendants.HasValue)
            {
                _log?.Warning($"[{nameof(GetDetectionLogsAsync)}] unit_id/include_descendants 는 서버 8.0 이상 전용 — 현재 {Contract} 라 전송하지 않음");
            }
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/detection-logs", parameters);
            // F-19: 이 엔드포인트만 actions[] 를 함께 준다 — DetectionEventDto 로 받으면 조용히 버렸다.
            return await response.ToApiListResponseAsync<DetectionLogDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionLogsAsync)}] Error: {ex.Message}");
            return ApiListResponse<DetectionLogDto>.CreateError("INTERNAL_ERROR", "Failed to get detection logs", ex.Message);
        }
    }

    public async Task<ApiResponse<DetectionLogDto>> GetDetectionLogByIdAsync(int eventId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/detection-logs/{eventId}");
            return await response.ToApiResponseAsync<DetectionLogDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetDetectionLogByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<DetectionLogDto>.CreateError("INTERNAL_ERROR", $"Failed to get detection log {eventId}", ex.Message);
        }
    }

    /// <summary>
    /// 서버가 받는 소문자 bool 문자열로 변환한다(<c>"true"</c>/<c>"false"</c>).
    /// <para>느슨한 입력("True"/"1")은 받되 <b>bool 로 확정되지 않으면 전송하지 않는다</b> —
    /// 서버 <c>Optional[bool]</c> 쿼리는 다른 값에 422 를 낸다.</para>
    /// </summary>
    private static bool TryToServerBool(string? text, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        if (bool.TryParse(t, out var b)) { value = b ? "true" : "false"; return true; }
        if (t == "1") { value = "true"; return true; }
        if (t == "0") { value = "false"; return true; }
        return false;
    }

    /// <summary>
    /// 닫힌 어휘(서버 enum) 쿼리 값을 **전송 전에** 검증하고 서버 표기(정본 enum 이름)로 정규화한다(F-22).
    /// </summary>
    /// <typeparam name="TEnum">서버 어휘와 이름이 1:1 대응하는 클라 enum.</typeparam>
    /// <param name="text">호출부가 준 원시 값. <c>null</c>/공백이면 "필터 없음" 으로 통과시키고 <paramref name="query"/> 는 <c>null</c> 이 된다.</param>
    /// <param name="parameterName">실패 메시지에 쓸 파라미터 이름.</param>
    /// <param name="query">전송할 값(정본 enum 이름). 필터가 없으면 <c>null</c>.</param>
    /// <param name="error">어휘 밖일 때의 한글 실패 사유.</param>
    /// <returns>전송 가능하면 <c>true</c>. 어휘 밖이면 <c>false</c> — 호출부는 <b>왕복 없이</b> 실패시켜야 한다.</returns>
    /// <remarks>
    /// ⚠ 대소문자만 다른 입력은 받아 **정본 표기로 교정**해 보낸다(서버는 정확한 표기만 받는다).
    /// 숫자 문자열(<c>"5"</c>)은 받지 않는다 — 서버가 문자열 어휘로만 주고받으므로 숫자를 조용히
    /// enum 으로 승격시키면 호출부의 오타가 "성공한 다른 필터" 로 위장한다.
    /// </remarks>
    private bool TryNormalizeVocabulary<TEnum>(
        string? text, string parameterName, out string? query, out string? error)
        where TEnum : struct, Enum
    {
        query = null;
        error = null;
        // 빈 값은 쿼리에 아예 붙이지 않는다 — 서버가 닫힌 어휘로 가는 중이라 빈 값은 422 다.
        if (string.IsNullOrWhiteSpace(text)) return true;

        var candidate = text.Trim();
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (!string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)) continue;
            query = name;   // 정본 표기로 교정해 전송
            return true;
        }

        error = $"'{parameterName}' 값 '{candidate}' 은 서버 어휘({typeof(TEnum).Name}) 밖입니다.";
        _log?.Error($"[{nameof(TryNormalizeVocabulary)}] {error} 허용값: {string.Join(", ", Enum.GetNames<TEnum>())}");
        return false;
    }
    #endregion

    #region - Operation Event (조회 전용) -
    /// <inheritdoc/>
    public async Task<ApiListResponse<OperationEventDto>> GetOperationEventsAsync(
        string? startDate = null,
        string? endDate = null,
        int? deviceId = null,
        string? reason = null,
        string? severity = null,
        bool? actionReported = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        if (!HasOperationEvents)
            return ApiListResponse<OperationEventDto>.CreateError(
                "NOT_SUPPORTED",
                "운영 이벤트는 서버 7.0 이상에서만 제공됩니다.",
                $"contract={Contract}");
        try
        {
            var parameters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(startDate)) parameters.Add("start_date", startDate);
            if (!string.IsNullOrEmpty(endDate)) parameters.Add("end_date", endDate);
            if (deviceId.HasValue) parameters.Add("device_id", deviceId.Value.ToString());
            if (!string.IsNullOrWhiteSpace(reason)) parameters.Add("reason", reason);
            if (!string.IsNullOrWhiteSpace(severity)) parameters.Add("severity", severity);
            if (actionReported.HasValue) parameters.Add("action_reported", actionReported.Value ? "true" : "false");
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/operations", parameters);
            return await response.ToApiListResponseAsync<OperationEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetOperationEventsAsync)}] Error: {ex.Message}");
            return ApiListResponse<OperationEventDto>.CreateError("INTERNAL_ERROR", "Failed to get operation events", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ApiResponse<OperationEventDto>> GetOperationEventByIdAsync(int id, CancellationToken token = default)
    {
        if (!HasOperationEvents)
            return ApiResponse<OperationEventDto>.CreateError(
                "NOT_SUPPORTED", "운영 이벤트는 서버 7.0 이상에서만 제공됩니다.", $"contract={Contract}");
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/operations/{id}");
            return await response.ToApiResponseAsync<OperationEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetOperationEventByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<OperationEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get operation event {id}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ApiListResponse<ActionEventDto>> GetOperationActionsAsync(int operationId, CancellationToken token = default)
    {
        if (!HasOperationEvents)
            return ApiListResponse<ActionEventDto>.CreateError(
                "NOT_SUPPORTED", "운영 이벤트는 서버 7.0 이상에서만 제공됩니다.", $"contract={Contract}");
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/operations/{operationId}/actions");
            return await response.ToApiListResponseAsync<ActionEventDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetOperationActionsAsync)}] Error: {ex.Message}");
            return ApiListResponse<ActionEventDto>.CreateError("INTERNAL_ERROR", $"Failed to get actions of operation {operationId}", ex.Message);
        }
    }
    #endregion

    #region - Event Mapping 중복(409) 처리 -
    /// <summary>
    /// 이벤트매핑 구성 쓰기의 <b>409 CONFLICT</b> 를 사용자 문구로 바꾼다.
    /// <para><b>왜</b> — 서버 8.0.1 은 카메라·스피커·경광등 단건 <c>POST</c>/<c>PATCH</c>/<c>PUT</c> 에
    /// 중복 가드(409)를 넣었다(6.3.2 에는 <c>409</c> 선언이 없어 201 로 중복 행이 생겼다).
    /// 실패로는 잡히지만(<c>IsSuccessStatusCode</c>) 운영자에게는 서버 영문 원문
    /// ("Camera 7 is already mapped to event mapping 3 (config id 12)...")이 그대로 노출됐다.</para>
    /// <para><b>무회귀</b> — 409 가 아니면 응답을 손대지 않는다. 6.3.2 에서는 이 분기가 절대 타지 않는다.</para>
    /// <para>서버 원문은 <c>Error.Details</c> 에 보존해 진단을 잃지 않는다.</para>
    /// </summary>
    private static ApiResponse<T> MapMappingDuplicate<T>(ApiResponse<T> response, string deviceLabel)
    {
        if (response.StatusCode != 409) return response;

        var origin = response.Error?.Message;
        response.Error ??= new ApiError();
        response.Error.Code = "CONFLICT";
        response.Error.Message =
            $"이미 이 이벤트매핑에 연동된 {deviceLabel}입니다. 새로 추가하는 대신 기존 구성을 편집하십시오.";
        if (!string.IsNullOrWhiteSpace(origin) && string.IsNullOrWhiteSpace(response.Error.Details))
            response.Error.Details = origin;   // 서버 원문(중복 config id 포함) 보존 — 진단용
        return response;
    }

    /// <summary>
    /// 이벤트 쓰기의 <b>400 BAD REQUEST</b>(= 서버에 없는 <c>device_id</c>)를 사용자 문구로 바꾼다. (F-21)
    /// <para><b>왜</b> — 이 경로들의 400 은 사유가 하나다: <c>"Device with id {id} not found"</c>
    /// (본문 검증 실패는 422 로 나온다). 종전에는 이 영문 원문이 운영자에게 그대로 노출됐고,
    /// 더 나쁜 것은 <b>재시도 가능한 실패로 오인</b>된 점이다 — 장비가 서버에서 지워진 상태라
    /// 몇 번을 보내도 400 이다.</para>
    /// <para><b>무회귀</b> — 400 이 아니면 응답을 손대지 않는다. 서버 원문은 <c>Error.Details</c> 에 보존한다.</para>
    /// </summary>
    private static ApiResponse<T> MapMissingDevice<T>(ApiResponse<T> response, int deviceId)
    {
        if (response.StatusCode != 400) return response;

        var origin = response.Error?.Message;
        response.Error ??= new ApiError();
        response.Error.Code = "DEVICE_NOT_FOUND";
        response.Error.Message = deviceId > 0
            ? $"서버에 없는 장비입니다(device_id={deviceId}). 재시도해도 같은 결과이니 장비 목록을 다시 동기화한 뒤 진행하십시오."
            : "서버에 없는 장비입니다. 재시도해도 같은 결과이니 장비 목록을 다시 동기화한 뒤 진행하십시오.";
        if (!string.IsNullOrWhiteSpace(origin) && string.IsNullOrWhiteSpace(response.Error.Details))
            response.Error.Details = origin;
        return response;
    }
    #endregion

    #region - Event Mapping CRUD -
    public async Task<ApiListResponse<EventMappingDto>> GetEventMappingsAsync(
        int? deviceGroupId = null,
        bool? status = null,
        int page = 1,
        int limit = 20,
        CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>();
            if (deviceGroupId.HasValue) parameters.Add("device_group_id", deviceGroupId.Value.ToString());
            if (status.HasValue) parameters.Add("status", status.Value.ToString().ToLower());
            parameters.Add("page", page.ToString());
            parameters.Add("limit", limit.ToString());

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings", parameters);
            return await response.ToApiListResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventMappingsAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", "Failed to get event mappings", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> GetEventMappingByIdAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}");
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventMappingByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", $"Failed to get event mapping {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> CreateEventMappingAsync(EventMappingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings", dto);
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", "Failed to create event mapping", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> PatchEventMappingAsync(int id, EventMappingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}", dto);
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", $"Failed to patch event mapping {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingDto>> UpdateEventMappingAsync(int id, EventMappingDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}", dto);
            return await response.ToApiResponseAsync<EventMappingDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingDto>.CreateError("INTERNAL_ERROR", $"Failed to update event mapping {id}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteEventMappingAsync(int id, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{id}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteEventMappingAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete event mapping {id}", ex.Message);
        }
    }
    #endregion

    #region - Mapping Camera CRUD -
    public async Task<ApiListResponse<EventMappingCameraDto>> GetMappingCamerasAsync(int mappingId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras");
            return await response.ToApiItemsListResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingCamerasAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping cameras {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> GetMappingCameraByIdAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}");
            return await response.ToApiResponseAsync<EventMappingCameraDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingCameraByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping camera {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> CreateMappingCameraAsync(int mappingId, EventMappingCameraDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingCameraDto>(), "카메라");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to create mapping camera {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> PatchMappingCameraAsync(int mappingId, int configId, EventMappingCameraDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingCameraDto>(), "카메라");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to patch mapping camera {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingCameraDto>> UpdateMappingCameraAsync(int mappingId, int configId, EventMappingCameraDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingCameraDto>(), "카메라");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingCameraDto>.CreateError("INTERNAL_ERROR", $"Failed to update mapping camera {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteMappingCameraAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/cameras/{configId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMappingCameraAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete mapping camera {mappingId}/{configId}", ex.Message);
        }
    }
    #endregion

    #region - Mapping Speaker CRUD -
    public async Task<ApiListResponse<EventMappingSpeakerDto>> GetMappingSpeakersAsync(int mappingId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers");
            return await response.ToApiItemsListResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingSpeakersAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping speakers {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> GetMappingSpeakerByIdAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}");
            return await response.ToApiResponseAsync<EventMappingSpeakerDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingSpeakerByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> CreateMappingSpeakerAsync(int mappingId, EventMappingSpeakerDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingSpeakerDto>(), "스피커");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to create mapping speaker {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> PatchMappingSpeakerAsync(int mappingId, int configId, EventMappingSpeakerDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingSpeakerDto>(), "스피커");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to patch mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingSpeakerDto>> UpdateMappingSpeakerAsync(int mappingId, int configId, EventMappingSpeakerDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingSpeakerDto>(), "스피커");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingSpeakerDto>.CreateError("INTERNAL_ERROR", $"Failed to update mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteMappingSpeakerAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/speakers/{configId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMappingSpeakerAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete mapping speaker {mappingId}/{configId}", ex.Message);
        }
    }
    #endregion

    #region - Mapping Lamp CRUD -
    public async Task<ApiListResponse<EventMappingLampDto>> GetMappingLampsAsync(int mappingId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps");
            return await response.ToApiItemsListResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingLampsAsync)}] Error: {ex.Message}");
            return ApiListResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping lamps {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> GetMappingLampByIdAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}");
            return await response.ToApiResponseAsync<EventMappingLampDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetMappingLampByIdAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to get mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> CreateMappingLampAsync(int mappingId, EventMappingLampDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PostRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingLampDto>(), "경광등");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(CreateMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to create mapping lamp {mappingId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> PatchMappingLampAsync(int mappingId, int configId, EventMappingLampDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PatchRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingLampDto>(), "경광등");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(PatchMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to patch mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<EventMappingLampDto>> UpdateMappingLampAsync(int mappingId, int configId, EventMappingLampDto dto, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.PutRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}", dto);
            return MapMappingDuplicate(await response.ToApiResponseAsync<EventMappingLampDto>(), "경광등");
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(UpdateMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<EventMappingLampDto>.CreateError("INTERNAL_ERROR", $"Failed to update mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteMappingLampAsync(int mappingId, int configId, CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.DeleteRequestAsync($"{_setupModel.Url}/integrations/event-mappings/{mappingId}/lamps/{configId}");
            return await response.ToApiResponseAsync<bool>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(DeleteMappingLampAsync)}] Error: {ex.Message}");
            return ApiResponse<bool>.CreateError("INTERNAL_ERROR", $"Failed to delete mapping lamp {mappingId}/{configId}", ex.Message);
        }
    }
    #endregion

    #region - Event Statistics (§6.7) -

    public async Task<ApiResponse<EventDashboardDto>> GetEventStatisticsDashboardAsync(
        string startDate, string endDate, string? interval = "hour", CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };
            if (!string.IsNullOrEmpty(interval)) parameters.Add("interval", interval);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/dashboard", parameters);
            return await response.ToApiResponseAsync<EventDashboardDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsDashboardAsync)}] Error: {ex.Message}");
            return ApiResponse<EventDashboardDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics dashboard", ex.Message);
        }
    }

    public async Task<ApiResponse<EventTrendDto>> GetEventStatisticsTrendAsync(
        string startDate, string endDate, string? interval = "hour", CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };
            if (!string.IsNullOrEmpty(interval)) parameters.Add("interval", interval);

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/trend", parameters);
            return await response.ToApiResponseAsync<EventTrendDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsTrendAsync)}] Error: {ex.Message}");
            return ApiResponse<EventTrendDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics trend", ex.Message);
        }
    }

    public async Task<ApiResponse<EventSummaryDto>> GetEventStatisticsSummaryAsync(
        string startDate, string endDate, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/summary", parameters);
            return await response.ToApiResponseAsync<EventSummaryDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsSummaryAsync)}] Error: {ex.Message}");
            return ApiResponse<EventSummaryDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics summary", ex.Message);
        }
    }

    public async Task<ApiResponse<EventByDeviceDto>> GetEventStatisticsByDeviceAsync(
        string startDate, string endDate, CancellationToken token = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "start_date", startDate },
                { "end_date", endDate }
            };

            var response = await _apiService.GetRequestAsync($"{_setupModel.Url}/events/statistics/by-device", parameters);
            return await response.ToApiResponseAsync<EventByDeviceDto>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(GetEventStatisticsByDeviceAsync)}] Error: {ex.Message}");
            return ApiResponse<EventByDeviceDto>.CreateError("INTERNAL_ERROR", "Failed to get event statistics by device", ex.Message);
        }
    }

    #endregion

    #region - Attributes -
    private readonly ILogService _log;
    private readonly IApiService _apiService;
    private readonly ApiSetupModel _setupModel;
    private readonly IServerContractProbe? _contractProbe;
    #endregion
}
