using Ironwall.Dotnet.Libraries.Messages.Dto.Units;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Services;
/****************************************************************************
   Purpose      : 부대 관계도 공유 배치 API — GET/PATCH /api/units/layout (서버 요청 S-1, FR-10 · FR-50 · FR-52)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 관계도의 <b>공유 배치 문서</b>(조직에 하나)를 읽고 쓴다.
/// </summary>
/// <remarks>
/// <para><b>새 인터페이스</b>다 — <see cref="IUnitApiService"/> 를 넓히지 않는다(NFR-10: 그 가짜 · 목이 전부 깨진다).</para>
/// <para><b>결과는 판별 공용체</b>다 — 404 · 412 · 타임아웃을 예외로 흘리지 않는다. 부르는 쪽(관계도 뷰모델 · 포트)은
/// <c>switch</c> 한 번으로 지원 판정(FR-50) · 충돌(FR-52) · 실패 복구(FR-34)를 가른다.</para>
/// <para>계약은 <b>제안형</b>이다(PRD §3.4) — 2026-09-28 현재 어느 서버 판에도 이 경로가 없다(8.0.x 는 422, 운영 6.3.2 는 부대 표면 0건).
/// S-1 이 들어오면 IMPL-50 이 키 이름 · 412 본문 · ETag 형식을 실측으로 맞춘다.</para>
/// <para>스레드: 어느 스레드에서 불러도 된다. 구현은 <c>ConfigureAwait(false)</c> — 결과를 UI 에 쓰려면 호출부가 돌아온다.</para>
/// </remarks>
public interface IUnitLayoutApiService
{
    /// <summary>
    /// 공유 배치 문서를 읽는다 — 창을 열 때 1회(지원 판정 겸) · 알림 뒤 재조회 · 412 뒤 최신 받기.
    /// </summary>
    Task<UnitLayoutReadResult> GetAsync(CancellationToken token = default);

    /// <summary>
    /// 일괄 쓰기 1회 — <c>If-Match: "<paramref name="ifMatchVersion"/>"</c> 를 싣는다.
    /// </summary>
    /// <param name="ifMatchVersion">마지막으로 받은 문서 버전.</param>
    /// <param name="patch">본문. 한 번의 끌기는 끈 부대 한 항목이다.</param>
    Task<UnitLayoutWriteResult> PatchAsync(long ifMatchVersion, UnitLayoutPatchDto patch, CancellationToken token = default);
}

/// <summary>읽기 실패의 종류 — 문구 · 다시 시도 여부를 가른다(SIM-P019~P028).</summary>
public enum UnitLayoutFailureKind
{
    /// <summary>401 — 세션 만료. 재로그인 안내.</summary>
    Unauthorized,

    /// <summary>403 — 권한 없음(<c>units:view</c>/<c>units:edit</c>).</summary>
    Forbidden,

    /// <summary>504 — 시간 초과(요청이 서버에 닿았을 수 있다 — 쓰기면 다시 읽어 확인).</summary>
    Timeout,

    /// <summary>503 — 서버에 닿지 못함.</summary>
    Unreachable,

    /// <summary>그 밖의 5xx.</summary>
    Server,

    /// <summary>200 인데 본문을 읽지 못함.</summary>
    Parse,

    /// <summary>428 — <c>If-Match</c> 가 없었다(서버가 거절 — 클라 결함 신호).</summary>
    PreconditionRequired,

    /// <summary>전송 계층이 요청 헤더를 실을 수 없다(<c>IApiHeaderRequestService</c> 없음) — 조건 없는 쓰기를 보내지 않았다.</summary>
    NoConditionalTransport,

    /// <summary>그 밖(지원 판정과 무관한 422 · 모르는 상태 등).</summary>
    Other,
}

/// <summary><see cref="IUnitLayoutApiService.GetAsync"/> 의 결과.</summary>
public abstract record UnitLayoutReadResult
{
    private UnitLayoutReadResult() { }

    /// <summary>200 — 서버가 공유 배치를 지원한다.</summary>
    /// <param name="Document">문서.</param>
    /// <param name="ETagVersion">헤더 <c>ETag</c> 에서 읽은 버전(없거나 못 읽으면 <c>null</c> — 본문 <c>version</c> 이 정본).</param>
    public sealed record Supported(UnitLayoutDocumentDto Document, long? ETagVersion) : UnitLayoutReadResult;

    /// <summary>
    /// 이 서버는 공유 배치를 지원하지 않는다 — 세션 전용 모드(FR-51). 404 · 405 · <c>ENDPOINT_REMOVED</c>(410) ·
    /// <b>422 + <c>path.unit_id</c></b>(라우트 가림, 8.0.x — V-06) · 계약 8.0 미만.
    /// </summary>
    public sealed record Unsupported(int StatusCode, string Reason) : UnitLayoutReadResult;

    /// <summary>읽지 못했다 — 자동 배치로 그리고 <b>쓰기 금지</b> · [다시 시도](FR-50).</summary>
    public sealed record ReadFailed(UnitLayoutFailureKind Kind, int StatusCode, string Reason) : UnitLayoutReadResult;
}

/// <summary><see cref="IUnitLayoutApiService.PatchAsync"/> 의 결과.</summary>
public abstract record UnitLayoutWriteResult
{
    private UnitLayoutWriteResult() { }

    /// <summary>200 — 새 문서 전체(버전 +1).</summary>
    public sealed record Ok(UnitLayoutDocumentDto Document, long? ETagVersion) : UnitLayoutWriteResult;

    /// <summary>
    /// 412 <c>VERSION_CONFLICT</c> — 그 사이 누가 썼다. 쓰지 않았다.
    /// <paramref name="CurrentVersion"/> 은 서버가 알려 준 현재 버전(<c>error.details.current_version</c>, 없으면 <c>null</c>).
    /// 부르는 쪽은 GET 으로 최신을 받아 <c>UnitMapLayoutSync.Resolve</c> 로 판정한다(FR-52).
    /// </summary>
    public sealed record Conflict(long? CurrentVersion) : UnitLayoutWriteResult;

    /// <summary>422 — 서버 규칙 위반(없는 부대 · 판 불일치 · 모르는 키 등). 전체 롤백 — 부분 적용 없음. 재시도 유도 금지.</summary>
    public sealed record Rejected(string Reason) : UnitLayoutWriteResult;

    /// <summary>배치를 쓸 수 없다 — 계약 8.0 미만(네트워크 전). 경로가 사라진 것은 하위 갈래 <see cref="EndpointGone"/>.</summary>
    public record Unsupported(int StatusCode, string Reason) : UnitLayoutWriteResult;

    /// <summary>
    /// 404 · 405 · 410 · <c>ENDPOINT_REMOVED</c> — 지원 중이던 배치 경로가 사라졌다 → 세션 전용 전환(SIM-F059 · TEST-65).
    /// <see cref="Unsupported"/> 의 하위라 옛 소비자(<c>case Unsupported</c>)도 그대로 받는다.
    /// </summary>
    public sealed record EndpointGone(int StatusCode, string Reason) : Unsupported(StatusCode, Reason);

    /// <summary>그 밖의 실패(401 · 403 · 409 · 5xx · 503 · 전송 능력 없음). 전용 하위 갈래: <see cref="PreconditionRequired"/> · <see cref="Unknown"/>.</summary>
    public record Failed(UnitLayoutFailureKind Kind, int StatusCode, string Reason) : UnitLayoutWriteResult;

    /// <summary>428 — <c>If-Match</c> 가 없었다(클라 결함 신호). VM 은 배치를 다시 읽는다. <see cref="Failed"/> 의 하위.</summary>
    public sealed record PreconditionRequired(int StatusCode, string Reason)
        : Failed(UnitLayoutFailureKind.PreconditionRequired, StatusCode, Reason);

    /// <summary>504 · 시간 초과 — 쓰기가 <b>반영됐을 수 있다</b>. VM 은 다시 읽어 결과를 확정한다(SIM-F065). <see cref="Failed"/> 의 하위(Kind = Timeout).</summary>
    public sealed record Unknown(int StatusCode, string Reason)
        : Failed(UnitLayoutFailureKind.Timeout, StatusCode, Reason);
}
