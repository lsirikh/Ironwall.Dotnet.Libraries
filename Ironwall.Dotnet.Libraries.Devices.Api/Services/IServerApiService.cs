using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : Server API Service Interface (GOP RESTful API §8.2~8.3, §8.6)
   Created By   : Claude
   Created On   : 2026-02-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : Server Category, Server Instance, Server Metrics API 호출 서비스
                  - /api/servers/categories (§8.2)
                  - /api/servers (§8.3)
                  - /api/servers/{id}/metrics (§8.6)
****************************************************************************/

/// <summary>
/// Server API 서비스 인터페이스
/// </summary>
public interface IServerApiService : IService
{
    // ────────────────────────── Server Category CRUD (§8.2) ──────────────────────────

    Task<ApiListResponse<CategoryDto>> GetCategoriesAsync(
        int page = 1,
        int limit = 20,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDetailDto>> GetCategoryByIdAsync(
        int id,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDto>> CreateCategoryAsync(
        CategoryDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDto>> PatchCategoryAsync(
        int id,
        CategoryDto dto,
        CancellationToken token = default);

    Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(
        int id,
        CategoryDto dto,
        CancellationToken token = default);

    Task<ApiResponse<object>> DeleteCategoryAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Server Instance CRUD (§8.3) ──────────────────────────

    /// <summary>
    /// 서버 목록 조회.
    /// <para>⚠ <paramref name="categoryId"/> 는 <b>6.3 계약 전용</b>이다. 7.0 은 <c>?category_id=</c> 를
    /// 422 로 거부하고 문자열 판별자 <c>?category_server=&lt;EnumServerType&gt;</c> 를 요구하는데
    /// 정수 id 로는 그 값을 만들 수 없다 — 그래서 7.0 계약에서는 <b>필터를 생략</b>하고 전체를 돌려준다
    /// (조회 전체가 422 로 죽는 것보다 나은 실패 모드). 호출부가 걸러 쓰거나 별도 경로가 필요하다 — A-devices S-06.</para>
    /// <para><paramref name="status"/> 는 닫힌 어휘(<c>NORMAL</c>·<c>WARNING</c>·<c>ERROR</c>[·<c>UNKNOWN</c> 7.0])다.
    /// 어휘 밖 값은 422 를 피해 생략된다.</para>
    /// </summary>
    /// <param name="view">
    /// 표현 프로필 — <c>basic</c> · <c>full</c>. <b>목록의 서버 기본값은 <c>basic</c></b> 이고 그때
    /// <c>server_config</c> 는 <b>키째 오지 않으며</b> <c>connection</c> 은 <c>credentials</c> 만 빠진다.
    /// 즉 값이 없는 것과 섹션을 안 받은 것이 구분되지 않아 임계치·계정이 "설정 안 됨"으로 보인다(A-devices S-05 · S-27).
    /// <para>이 계층은 <b>통로만 연다</b> — 무엇을 보낼지는 호출부가 결정한다(버전 분기 없음).
    /// API 7.0 부터 존재하는 파라미터이고 6.3 에는 없다.</para>
    /// </param>
    /// <param name="include">
    /// 추가 섹션 — <c>connection</c> · <c>server_config</c> (쉼표 구분 또는 반복). 비우면 붙이지 않는다.
    /// </param>
    /// <param name="categoryServer">
    /// 7.0 문자열 판별자 필터 <c>?category_server=&lt;EnumServerType&gt;</c> — <b>제거된 <paramref name="categoryId"/> 의 대체</b>.
    /// <para>정수 id 로는 이 값을 만들 수 없어 종전에는 7.0 에서 카테고리 필터 기능 자체가 없었다.
    /// 이 파라미터가 그 통로다. 모르는 값은 서버 422 이므로 어휘는
    /// <c>GET /api/servers/categories</c> 의 판별자에서 가져온다.</para>
    /// <para>6.3 계약에서는 이 파라미터가 <b>전송되지 않는다</b>(6.3 에 없는 키 — 미지 쿼리는 조용히 무시되어
    /// "걸렀는데 전건" 이 된다). 6.3 에서는 <paramref name="categoryId"/> 를 쓴다.</para>
    /// </param>
    /// <param name="unitId">소속 부대 id 필터 <c>?unit_id=</c> — <b>8.0 이상에서만 전송</b>된다.</param>
    /// <param name="includeDescendants">
    /// 예하 부대까지 포함 <c>?include_descendants=</c> — <paramref name="unitId"/> 와 <b>함께만</b> 유효하고
    /// 8.0 이상에서만 전송된다.
    /// </param>
    /// <remarks>
    /// 새 파라미터 3종을 <c>CancellationToken</c> <b>뒤</b>에 둔 것은 의도다 — 앞에 끼우면
    /// <c>token</c> 을 위치 인자로 넘기던 기존 호출부가 깨진다. 이름 인자로 넘길 것.
    /// </remarks>
    Task<ApiListResponse<ServerDto>> GetServersAsync(
        int? categoryId = null,
        string? status = null,
        int page = 1,
        int limit = 20,
        string? view = null,
        string? include = null,
        CancellationToken token = default,
        string? categoryServer = null,
        int? unitId = null,
        bool? includeDescendants = null);

    /// <summary>
    /// 서버 단건 조회. <b>단건의 서버 기본 프로필은 <c>full</c></b> 이다(목록과 반대 — 목록은 <c>basic</c>).
    /// </summary>
    /// <param name="view">표현 프로필 — <c>basic</c> · <c>full</c>. 비우면 붙이지 않는다(서버 기본 <c>full</c>).</param>
    /// <param name="include">추가 섹션 — <c>connection</c> · <c>server_config</c>. 비우면 붙이지 않는다.</param>
    Task<ApiResponse<ServerDto>> GetServerByIdAsync(
        int id,
        string? view = null,
        string? include = null,
        CancellationToken token = default);

    /// <summary>
    /// 서버 등록. <b>6.3 계약에서만</b> 평면 <see cref="ServerDto"/> 를 그대로 본문으로 보낸다.
    /// <para>⚠ <b>7.0+ 축 계약에서는 호출하지 않는다</b>(D-22) — <c>category_id</c>·<c>ip_address</c>·<c>port</c>·
    /// <c>hostname</c>·<c>threshold_config</c> 는 축 계약의 <c>extra="forbid"</c> 에 걸려 전부 422 다
    /// (<c>app/schemas/server.py:59-66,328-339</c>). 네트워크에 나가기 전에 <c>AXIS_SHAPE_REQUIRED</c> 오류로
    /// 막고 <c>Devices.Api.Servers.IServerAxisApiService</c>(서버 콘솔이 실제로 쓰는 판본 인식 통로)를 안내한다.</para>
    /// </summary>
    Task<ApiResponse<ServerDto>> CreateServerAsync(
        ServerDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 서버 부분 수정. <b>6.3 계약에서만</b> 평면 <see cref="ServerDto"/> 를 그대로 본문으로 보낸다.
    /// 7.0+ 에서는 <see cref="CreateServerAsync"/> 와 같은 이유로 <c>AXIS_SHAPE_REQUIRED</c> 를 돌려준다(D-22).
    /// </summary>
    Task<ApiResponse<ServerDto>> PatchServerAsync(
        int id,
        ServerDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 서버 전체 교체. <b>6.3 계약에서만</b> 평면 <see cref="ServerDto"/> 를 그대로 본문으로 보낸다.
    /// 7.0+ 에서는 <see cref="CreateServerAsync"/> 와 같은 이유로 <c>AXIS_SHAPE_REQUIRED</c> 를 돌려준다(D-22).
    /// </summary>
    Task<ApiResponse<ServerDto>> UpdateServerAsync(
        int id,
        ServerDto dto,
        CancellationToken token = default);

    Task<ApiResponse<object>> DeleteServerAsync(
        int id,
        CancellationToken token = default);

    // ────────────────────────── Server Metrics (§8.6) ──────────────────────────

    Task<ApiResponse<ServerMetricDto>> CreateServerMetricAsync(
        int serverId,
        ServerMetricDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 서버 계측 이력 조회. <para>⚠ 서버 쿼리 키는 <c>start_time</c>/<c>end_time</c> 이다(6.3.2·7.0.1 동일).
    /// 파라미터 이름(<c>startDate</c>/<c>endDate</c>)은 기존 호출부 호환을 위해 유지하지만
    /// 값은 <b>aware ISO8601</b>(오프셋 포함) 시각이다. 종전에는 <c>start_date</c>/<c>end_date</c> 로 보내
    /// FastAPI 가 조용히 버렸고 기간 필터가 전혀 걸리지 않았다 — A-devices S-13.</para>
    /// </summary>
    Task<ApiListResponse<ServerMetricDto>> GetServerMetricsAsync(
        int serverId,
        string? startDate = null,
        string? endDate = null,
        int limit = 100,
        CancellationToken token = default);

    Task<ApiResponse<ServerMetricLatestDto>> GetServerMetricLatestAsync(
        int serverId,
        CancellationToken token = default);

    /// <summary>
    /// 서버 계측 시계열을 <b>보존기간(일)</b> 기준으로 삭제한다
    /// (<c>DELETE /api/servers/{id}/metrics?older_than_days=N</c>).
    /// <para>서버 파라미터는 <c>older_than_days</c>(정수, 최소 1) 하나뿐이다 —
    /// 6.3.2(운영)·7.0.1·8.0.1 <b>세 판본 Swagger 실측 전부 동일</b>이라 판본 분기가 필요 없다.</para>
    /// <para>⚠ <c>before_date</c> 는 <b>어느 판본에도 존재하지 않는다.</b> FastAPI 가 미선언 쿼리를
    /// 조용히 버리므로 422 도 없이 서버 기본값(30일) 삭제가 실행됐다 — A-devices S-14(P0 데이터 손상).</para>
    /// </summary>
    /// <param name="olderThanDays">이 일수보다 오래된 계측을 삭제한다. <b>1 이상 필수</b>(기본값을 두지 않는 것이 의도다).</param>
    Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(
        int serverId,
        int olderThanDays,
        CancellationToken token = default);

    /// <summary>
    /// <b>사용 금지</b> — 호출하면 네트워크에 나가지 않고 오류를 돌려준다.
    /// <see cref="DeleteServerMetricsAsync(int, int, CancellationToken)"/> 를 쓴다.
    /// </summary>
    [Obsolete("before_date 는 서버에 없는 파라미터다. DeleteServerMetricsAsync(serverId, olderThanDays) 를 사용하라. " +
              "이 오버로드는 호출을 거부한다(조용한 30일 삭제 방지).", error: false)]
    Task<ApiResponse<MetricDeleteResultDto>> DeleteServerMetricsAsync(
        int serverId,
        string? beforeDate = null,
        CancellationToken token = default);

    // ────────────────────────── Server Status Report (§8.3 · API 7.0+) ──────────────────────────

    /// <summary>
    /// 서버 매니저가 관측한 상태를 보고한다(<c>PATCH /api/servers/{id}/status</c>, 권한 <c>servers:control</c>).
    /// <para><b>API 7.0 전용</b> — 6.3.2 에는 경로가 없다. 6.3 계약에서는 호출하지 않고
    /// <c>ENDPOINT_REMOVED</c> 오류를 돌려준다.</para>
    /// <para><c>status</c> 는 관측 필드라 <c>PATCH /api/servers/{id}</c> 본문으로는 쓸 수 없다(422 OBSERVED_FIELD).
    /// 이 경로가 상태를 갱신할 유일한 입구다 — A-devices S-17.</para>
    /// </summary>
    /// <param name="status"><c>NORMAL</c> · <c>WARNING</c> · <c>ERROR</c>. <c>UNKNOWN</c> 보고는 서버가 422.</param>
    /// <param name="observedAt">관측 시각(오프셋 필수). 생략 시 서버가 수신 시각으로 채우고 경고를 싣는다.</param>
    Task<ApiResponse<ServerStatusReportDto>> ReportServerStatusAsync(
        int serverId,
        string status,
        DateTimeOffset? observedAt = null,
        CancellationToken token = default);

    // ────────────────────────── Proxy Settings (§8.8) ──────────────────────────

    /// <summary>
    /// 프록시 설정 조회. <b>계약별로 존재 여부가 다르다</b>(실측):
    /// 6.3.2 = 200 정상 · 7.0.1 = <c>410 ENDPOINT_REMOVED</c> 묘비.
    /// 7.0 계약에서는 호출하지 않고 대체 경로(<c>GET /api/servers/{id}/config</c> 의 <c>server_config.modes</c>)를
    /// 안내하는 오류를 돌려준다 — A-devices S-15.
    /// </summary>
    Task<ApiResponse<ProxySettingDto>> GetProxySettingsAsync(
        int serverId,
        CancellationToken token = default);
}
