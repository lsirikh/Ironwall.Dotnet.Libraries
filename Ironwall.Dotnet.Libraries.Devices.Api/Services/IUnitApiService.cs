using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : 부대 편제 API 서비스 인터페이스 (GOP RESTful API §11-A, /api/units)
   Created By   : Claude
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : 부대 편제 3경로 · 7 엔드포인트
                  - GET    /api/units            목록
                  - POST   /api/units            생성
                  - GET    /api/units/graph      관계도
                  - GET    /api/units/{id}       단건(+include)
                  - PATCH  /api/units/{id}       부분 수정
                  - PUT    /api/units/{id}       전체 교체
                  - DELETE /api/units/{id}       삭제
****************************************************************************/

/// <summary>
/// 부대 편제(<c>/api/units</c>) API 서비스.
/// </summary>
/// <remarks>
/// <para>🔴 <b>계약 게이트</b> — 이 표면은 <b>서버 8.0 이상에만 존재한다</b>(실측 2026-09-18:
/// 로컬 개발 8.0.1 에 3경로 실재 · 원격 운영 6.3.2 에 0건). 6.3·7.0 서버에서 호출하면 404 다.
/// 모든 메서드가 <c>Contract &gt;= EnumServerContract.V8_0</c> 을 먼저 확인하고,
/// 아니면 <b>네트워크에 나가기 전에</b> <c>ENDPOINT_REMOVED</c> 로 실패한다 —
/// 404 를 "알 수 없는 오류"로 표시해 사용자를 재시도 루프에 빠뜨리지 않는다.</para>
/// <para>권한은 <c>units:view</c> / <c>units:edit</c> / <c>units:delete</c> 3종이다(<c>control</c> 은 없다).
/// 기존 그룹 백필은 <c>devices:view</c> 를 가진 그룹에 <c>units:view</c> 만 켰으므로,
/// 쓰기 경로는 403 을 정상 경로로 다뤄야 한다.</para>
/// </remarks>
public interface IUnitApiService : IService
{
    // ────────────────────────── 읽기 (units:view) ──────────────────────────

    /// <summary>
    /// 부대 목록. 정렬은 서버가 <b>제대 순위 → 코드</b> 순으로 준다.
    /// </summary>
    /// <param name="page">1 이상. 기본 1.</param>
    /// <param name="limit">1~100. 서버 기본값은 20.</param>
    /// <param name="echelon">제대 필터. <c>null</c> 이면 전체.</param>
    /// <param name="parentId">이 부대의 <b>직속 자식만</b>. <c>null</c> 이면 전체.</param>
    /// <param name="isEnable">운용 여부 필터. <c>null</c> 이면 전체.</param>
    /// <remarks>
    /// 트리·관계도를 그릴 목적이면 <see cref="GetUnitGraphAsync"/> 가 낫다 —
    /// 목록은 페이지네이션이 걸려 있어 부분만 오고, 간선을 주지 않는다.
    /// </remarks>
    Task<ApiListResponse<UnitListDto>> GetUnitsAsync(
        int page = 1,
        int limit = 20,
        EnumUnitEchelon? echelon = null,
        int? parentId = null,
        bool? isEnable = null,
        CancellationToken token = default);

    /// <summary>
    /// 부대 단건(full) + 선택 섹션.
    /// </summary>
    /// <param name="unitId">부대 id. 없으면 404.</param>
    /// <param name="include">
    /// 쉼표 구분 — <c>parent</c>·<c>children</c>·<c>ancestors</c>·<c>adjacent</c>.
    /// <b>모르는 값은 서버가 422</b> 로 거절하므로 보내기 전에 지역 검사한다.
    /// </param>
    Task<ApiResponse<UnitDetailDto>> GetUnitAsync(
        int unitId,
        string? include = null,
        CancellationToken token = default);

    /// <summary>
    /// 부대 관계도 — <b>노드 + 계층 간선 + 인접 간선</b>을 한 번에 준다.
    /// </summary>
    /// <param name="rootId">이 부대의 서브트리만. <c>null</c> 이면 전체.</param>
    /// <param name="depth">
    /// 펼칠 깊이. 뿌리를 0 으로 세어 <c>1</c> 이면 직속 자식까지. <c>null</c> 이면 끝까지.
    /// <b>1 이상</b>이어야 한다(0·음수는 422).
    /// <c>rootId</c> 없이도 듣는다 — 그때는 뿌리 부대마다 같은 깊이를 적용해 합친다.
    /// </param>
    /// <remarks>
    /// ⚠ 인접 상대가 서브트리 밖이면 <b>노드로는 들어오지만 그 부대의 상위는 펼치지 않는다</b> —
    /// <c>ParentId</c> 가 <c>nodes</c> 에 없는 id 를 가리킬 수 있다.
    /// <c>UnitGraphDto.DisplayRoots</c> 로 처리한다.
    /// </remarks>
    Task<ApiResponse<UnitGraphDto>> GetUnitGraphAsync(
        int? rootId = null,
        int? depth = null,
        CancellationToken token = default);

    // ────────────────────────── 쓰기 (units:edit / units:delete) ──────────────────────────

    /// <summary>
    /// 부대 생성(201). <b>코드를 정할 수 있는 유일한 지점</b>이며, 정한 코드는 불변이다.
    /// </summary>
    /// <remarks>
    /// 코드 형식·<c>global</c> 예약은 전송 전에 지역 검사한다 — 오타는 되돌릴 수 없고
    /// 그 부대의 NATS subject 토큰까지 그 오타로 고정된다.
    /// <para>422 사유(서버): 코드 형식 위반 · <c>global</c> · 코드 중복 · 상위 부대 미존재 ·
    /// 상위 제대가 하위 이하 · 인접 상대 미존재 · 자기 자신 인접.</para>
    /// </remarks>
    Task<ApiResponse<UnitDto>> CreateUnitAsync(
        UnitCreateDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 부대 부분 수정(RFC 7396) — <b>보낸 필드만</b> 바뀐다. 부분 수정은 항상 이쪽이다.
    /// </summary>
    Task<ApiResponse<UnitDto>> PatchUnitAsync(
        int unitId,
        UnitUpdateDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 부대 전체 교체.
    /// </summary>
    /// <remarks>
    /// 🔴 <c>dto.AdjacentUnitIds</c> 가 <c>null</c> 이면 <b>호출하지 않고 거절한다</b> —
    /// 생략하면 서버가 인접을 전삭제하기 때문이다. 현재 값을 실으려면
    /// <c>UnitReplaceDto.FromCurrent(단건 조회 결과)</c> 를 쓰고, 정말 비울 의도라면 빈 목록을 명시한다.
    /// <para>🔴 <c>dto.ParentId</c> 가 <c>null</c> 이고 <c>dto.IsRootIntended</c> 가 아니면 <b>호출하지 않고 거절한다</b> —
    /// 생략하면 서버가 그 부대를 루트로 옮기고 관계도 배치 행까지 지운다(서버 v8.0.4 회신 ⑦). 부분 수정은 <see cref="PatchUnitAsync"/> 다.</para>
    /// </remarks>
    Task<ApiResponse<UnitDto>> ReplaceUnitAsync(
        int unitId,
        UnitReplaceDto dto,
        CancellationToken token = default);

    /// <summary>
    /// 부대 삭제. <b>매달린 것이 있으면 409</b> 이고, 정책상 퇴역은 삭제가 아니라 <c>is_enable=false</c> 다.
    /// </summary>
    Task<ApiResponse<UnitDeleteResultDto>> DeleteUnitAsync(
        int unitId,
        CancellationToken token = default);
}
