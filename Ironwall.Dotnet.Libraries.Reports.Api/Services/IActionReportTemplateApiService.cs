using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Reports.Api.Services;

/// <summary>
/// 조치보고 문구 기본값(lookup) API — <c>/api/events/action-report-templates</c> <b>7경로</b> 래핑.
/// <para><b>인가가 읽기/쓰기로 갈린다</b>(명세 §6.4.8): 읽기(목록·단건) = <c>events:view</c> ·
/// 쓰기(POST·PATCH·PUT·reorder) = <c>action_report_templates:edit</c> · 삭제 = <c>action_report_templates:delete</c>.
/// <c>action_report_templates</c> 는 <b>전용 권한 모듈</b>이라(권한 16종 중 하나) 그룹 권한 화면이 그 키를
/// 저장하지 못하면 비-ADMIN 은 문구 관리에서 영구 403 이다 — 그 건은 Accounts 도메인(권한 매트릭스)에서 다룬다.</para>
/// <para><b>이 목록에 페이지네이션이 없다</b>(<c>pagination: null</c>) — 전량이 한 번에 오고 정렬은
/// <c>display_order</c> 오름차순(동률 id)이다. 드래그 정렬 UI 의 "전량 로딩" 전제가 서버 쪽에서 충족된다.</para>
/// <para><b>서버 지원 여부</b>: 이 라우터는 운영 6.3.2 에 <b>없다</b>(openapi 실측 0경로). 목록 조회가
/// 404 로 떨어지면 <see cref="IsSupported"/> 가 <c>false</c> 로 확정되고 오류 코드 <c>NOT_SUPPORTED</c> 를 돌려준다 —
/// 호출부는 그때 문구 관리 UI 를 감춘다(없는 기능을 오류로 보이게 하지 않는다).</para>
/// </summary>
public interface IActionReportTemplateApiService : IService
{
    /// <summary>
    /// 서버가 이 API 를 제공하는가. <c>null</c> = 아직 모른다(1회도 조회 안 함) ·
    /// <c>false</c> = 목록이 404(구 서버라 라우터 없음) · <c>true</c> = 정상 응답 1회 이상.
    /// </summary>
    bool? IsSupported { get; }

    /// <summary>목록(GET) — <c>display_order</c> 오름차순 전량. 페이지네이션 없음. <c>events:view</c>.</summary>
    Task<ApiListResponse<ActionReportTemplateDto>> GetTemplatesAsync(CancellationToken token = default);

    /// <summary>단건(GET /{id}) — <c>events:view</c>. 없는 id 는 404.</summary>
    Task<ApiResponse<ActionReportTemplateDto>> GetTemplateByIdAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 추가(POST, 201) — <c>action_report_templates:edit</c>.
    /// <c>content</c> 는 1~500자이고 <b>서버 UNIQUE</b> 라 중복이면 <b>409</b> 다(스웨거는 409 를 선언하지 않는다 —
    /// 선언을 믿지 말고 처리한다).
    /// </summary>
    Task<ApiResponse<ActionReportTemplateDto>> CreateTemplateAsync(ActionReportTemplateCreateDto dto, CancellationToken token = default);

    /// <summary>부분 수정(PATCH /{id}) — 보낸 필드만 반영. 409·404 동일.</summary>
    Task<ApiResponse<ActionReportTemplateDto>> UpdateTemplateAsync(int id, ActionReportTemplateUpdateDto dto, CancellationToken token = default);

    /// <summary>전체 교체(PUT /{id}) — 두 필드 모두 필수. 409·404 동일.</summary>
    Task<ApiResponse<ActionReportTemplateDto>> ReplaceTemplateAsync(int id, ActionReportTemplateReplaceDto dto, CancellationToken token = default);

    /// <summary>
    /// 삭제(DELETE /{id}, 하드) — <c>action_report_templates:delete</c>.
    /// 이미 기록된 조치보고(<c>action_events</c>)에는 <b>영향이 없다</b>(FK 없음).
    /// </summary>
    Task<ApiResponse<object>> DeleteTemplateAsync(int id, CancellationToken token = default);

    /// <summary>
    /// 드래그 정렬 일괄 반영(POST /reorder) — <b>한 트랜잭션 + NATS 알림 1건</b>.
    /// <para>응답은 재정렬 <b>후 전체 목록</b>이므로 호출부는 재조회 없이 그대로 갈아끼운다.
    /// 요청 id 중 하나라도 없으면 <b>아무것도 바뀌지 않고 404</b> — 화면 순서를 되돌려야 한다.</para>
    /// <para>행마다 PATCH 를 반복하지 말 것 — 드래그 1회가 <c>|i−j|+1</c> 건이 되고 알림도 그만큼 나간다.</para>
    /// </summary>
    Task<ApiListResponse<ActionReportTemplateDto>> ReorderTemplatesAsync(IEnumerable<ActionReportTemplateReorderItemDto> items, CancellationToken token = default);
}
