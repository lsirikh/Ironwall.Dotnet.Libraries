using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Nats.Models;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;
/****************************************************************************
   Purpose      : 현재 부대(unit) id 해석·캐시 구현 — 코드(GroupNats) → id 사전
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="IUnitScopeService"/> 구현 — <c>GroupNats</c>(부대 코드) → <c>unit_id</c>(정수) 해석 1회 + 캐시.
/// </summary>
/// <remarks>
/// <para><b>해석 경로</b> — <c>GET /api/units?limit=100</c> 를 페이지 순회하며 <c>code</c> 가
/// <see cref="UnitCode"/> 와 같은 부대를 찾는다. 8.0.1 스웨거에 <b>코드 필터 파라미터가 없어</b>
/// (page·limit·echelon·parent_id·is_enable 뿐) 목록 매칭이 유일한 방법이다.</para>
///
/// <para><b>퇴역 부대도 받는다</b> — <c>is_enable</c> 필터를 걸지 않는다. 서버 정책상 퇴역은 삭제가 아니라
/// <c>is_enable=false</c> 이고, 우리 부대가 퇴역 표시돼도 <b>이미 그 부대에 붙은 장비</b>는 그대로다.
/// 필터를 걸면 그 상황에서 id 를 못 찾아 기본 부대로 흘러간다.</para>
///
/// <para><b>스레드</b> — 저장 버튼(UI 스레드)과 시작 루틴(백그라운드)이 동시에 부를 수 있어
/// <see cref="SemaphoreSlim"/> 로 해석 구간을 직렬화한다. 성공 캐시는 <c>volatile</c> 로 게시한다.</para>
///
/// <para><b>재시도</b> — 실패해도 영구 포기하지 않는다(서버가 세션 중 올라갈 수 있다). 다만
/// <see cref="RETRY_INTERVAL_MS"/> 동안은 다시 나가지 않는다 — 저장 1회에 장비 수십 건이 돌 수 있어
/// 매 건 재시도는 그대로 요청 폭증이 된다. 시계는 <see cref="Environment.TickCount64"/>(단조 증가)를 쓴다
/// (<c>DateTime.Now</c> 직접 호출 금지 규칙 · 시각 역행 내성).</para>
/// </remarks>
public class UnitScopeService : IUnitScopeService
{
    #region - Ctors -
    /// <param name="unitApiService">
    /// 부대 편제 API. <b>선택 주입</b>이며 <c>null</c>(등록 없는 호스트·DB 모드)이면 해석을 포기한다.
    /// </param>
    /// <param name="natsSetupModel">
    /// NATS 설정 — <c>GroupNats</c> 가 곧 <b>우리 부대 코드</b>다(<c>"{Domain}.{Group}.{Subsystem}"</c> subject 의 부대 토큰).
    /// <b>선택 주입</b>이며 <c>null</c>·공백이면 해석할 코드가 없으므로 포기한다.
    /// </param>
    /// <param name="contractProbe">
    /// 서버 계약 세대 프로브. <b>선택 주입</b>이고 미주입·미확보면 <see cref="EnumServerContract.V6_3"/> 로 간주해
    /// <b>해석도 전송도 하지 않는다</b> — 운영 6.3.2 무회귀가 우선이다.
    /// </param>
    public UnitScopeService(
        IUnitApiService? unitApiService = null,
        INatsSetupModel? natsSetupModel = null,
        IServerContractProbe? contractProbe = null,
        ILogService? log = null)
    {
        _unitApiService = unitApiService;
        _natsSetupModel = natsSetupModel;
        _contractProbe = contractProbe;
        _log = log;
    }
    #endregion

    #region - Implementation of IUnitScopeService -
    /// <summary>현재 서버 계약 세대. 프로브 미주입·미확보면 <see cref="EnumServerContract.V6_3"/>.</summary>
    private EnumServerContract Contract => _contractProbe?.Contract ?? EnumServerContract.V6_3;

    /// <inheritdoc/>
    /// <remarks>⚠ <c>== V8_0</c> 동치 비교 금지 — 9.0 서버에서 부대 축이 조용히 꺼진다.</remarks>
    public bool IsUnitEra => Contract >= EnumServerContract.V8_0;

    /// <inheritdoc/>
    public string? UnitCode
    {
        get
        {
            var code = _natsSetupModel?.GroupNats;
            return string.IsNullOrWhiteSpace(code) ? null : code!.Trim();
        }
    }

    /// <inheritdoc/>
    public int? CurrentUnitId => IsUnitEra ? Resolved : null;

    /// <inheritdoc/>
    public bool IsResolved => Resolved.HasValue;

    /// <summary>캐시된 해석 결과(0 은 미해석).</summary>
    private int? Resolved
    {
        get { var id = _resolvedIdOrZero; return id > 0 ? id : null; }
    }

    /// <inheritdoc/>
    public async Task<int?> ResolveAsync(CancellationToken token = default)
    {
        // ① 계약 게이트 — 8.0 미만에서는 네트워크에 나가지 않는다(운영 6.3.2 에 /api/units 는 0건).
        if (!IsUnitEra) return null;

        // ② 캐시 히트 — 부대 id 는 세션 중 바뀌지 않는다(코드 불변 · id 불변).
        var cached = Resolved;
        if (cached.HasValue) return cached;

        var code = UnitCode;
        if (code == null)
        {
            WarnOnce($"부대 코드(GroupNats)가 설정되지 않아 unit_id 를 해석할 수 없습니다 — "
                   + $"서버 계약 {Contract} 에서 unit_id 를 생략하면 서버가 기본 부대로 귀속시킵니다(응답에 신호 없음).");
            return null;
        }

        if (_unitApiService == null)
        {
            WarnOnce($"부대 API(IUnitApiService)가 등록되지 않아 unit_id 를 해석할 수 없습니다(부대 코드: {code}) — "
                   + $"서버 계약 {Contract} 에서 unit_id 생략은 기본 부대 귀속으로 처리됩니다.");
            return null;
        }

        // ③ 권한 게이트(units:view — 서버 8.0 신설 모듈). 없으면 403 왕복을 만들지 않고 접는다.
        //    스로틀보다 앞에 둔다 — 로그인 직후 권한 스냅샷이 아직 없어 한 번 거절된 경우
        //    다음 쓰기에서 즉시 재시도돼야 한다(스로틀 뒤에 두면 60초 동안 막힌다).
        if (!DevicePermissionGate.CanViewUnits())
        {
            if (!_permWarned)
            {
                _permWarned = true;
                _log?.Warning($"[{nameof(UnitScopeService)}] 부대 조회 권한(units:view)이 없어 unit_id 를 해석하지 못했습니다"
                            + $"(부대 코드: {code}). 장비 쓰기에서 unit_id 가 생략되고 서버가 기본 부대로 귀속시킵니다.");
            }
            return null;
        }

        // ④ 재시도 스로틀 — 저장 1회에 수십 건이 돌 수 있어 건마다 재시도하면 요청 폭증이 된다.
        if (_lastAttemptTick != 0 && Environment.TickCount64 - _lastAttemptTick < RETRY_INTERVAL_MS)
            return null;

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // 게이트 통과를 기다리는 동안 다른 호출이 해석했을 수 있다(이중 조회 방지).
            cached = Resolved;
            if (cached.HasValue) return cached;
            if (_lastAttemptTick != 0 && Environment.TickCount64 - _lastAttemptTick < RETRY_INTERVAL_MS)
                return null;

            _lastAttemptTick = Environment.TickCount64;
            return await ResolveCoreAsync(code, token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
    #endregion

    #region - Implementation of IService -
    /// <summary>
    /// 시작 루틴 — 8.0 서버일 때만 부대 id 를 <b>미리</b> 1회 해석한다(첫 저장에서 왕복을 없앤다).
    /// </summary>
    /// <remarks>
    /// 실패는 치명이 아니다. 프로브가 이 시점에 아직 확보되지 않았으면 조용히 건너뛰고,
    /// 첫 쓰기 때 <see cref="ResolveAsync"/> 가 다시 시도한다(기동 순서에 의존하지 않는다).
    /// </remarks>
    public async Task ExecuteAsync(CancellationToken token = default)
    {
        if (!IsUnitEra)
        {
            _log?.Info($"[{nameof(UnitScopeService)}] 서버 계약 {Contract} — 부대 축(unit_id) 미사용(8.0 미만). 해석을 건너뜁니다.");
            return;
        }

        try { await ResolveAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { /* 종료 중 — 무시 */ }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(UnitScopeService)}] 시작 시 부대 id 선해석 실패(첫 쓰기에서 재시도): {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    #endregion

    #region - Processes -
    /// <summary>목록을 페이지 순회하며 <paramref name="code"/> 와 같은 부대를 찾는다.</summary>
    private async Task<int?> ResolveCoreAsync(string code, CancellationToken token)
    {
        var seen = new List<string>();

        for (int page = 1; page <= MAX_PAGES; page++)
        {
            var response = await _unitApiService!
                .GetUnitsAsync(page: page, limit: PAGE_LIMIT, token: token)
                .ConfigureAwait(false);

            if (!response.Success || response.Data == null)
            {
                _log?.Warning($"[{nameof(UnitScopeService)}] 부대 목록 조회 실패(page={page}) — unit_id 를 생략합니다"
                            + $"(서버가 기본 부대로 귀속시킵니다). code={response.Error?.Code}, message={response.Message}");
                return null;
            }

            foreach (var unit in response.Data)
            {
                if (string.Equals(unit.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedIdOrZero = unit.Id;
                    _log?.Info($"[{nameof(UnitScopeService)}] 부대 해석 완료 — code={unit.Code}, unit_id={unit.Id}, "
                             + $"name={unit.Name}, enable={unit.IsEnable} (계약 {Contract})");
                    return unit.Id;
                }
                seen.Add(unit.Code);
            }

            var totalPages = response.Pagination?.TotalPages ?? 1;
            if (page >= totalPages) break;
        }

        // 여기까지 오면 우리 코드가 서버 편제에 없다 — 조용히 넘기면 안 되는 지점이다.
        _log?.Warning($"[{nameof(UnitScopeService)}] 부대 코드 '{code}'(GroupNats)에 해당하는 부대를 서버 편제에서 찾지 못했습니다 — "
                    + $"unit_id 를 생략합니다(서버가 기본 부대로 귀속시키고 응답에는 아무 신호도 남지 않습니다). "
                    + $"서버 부대 코드 목록: [{string.Join(", ", seen)}]");
        return null;
    }

    /// <summary>같은 구성 결함을 매 건 반복 기록하지 않는다(로그 폭주 방지) — 내용은 1회 남긴다.</summary>
    private void WarnOnce(string message)
    {
        if (_configWarned) return;
        _configWarned = true;
        _log?.Warning($"[{nameof(UnitScopeService)}] {message}");
    }
    #endregion

    #region - Attributes -
    /// <summary>목록 페이지 크기 — 서버 상한(<c>limit.maximum=100</c>, 스웨거 실측).</summary>
    private const int PAGE_LIMIT = 100;

    /// <summary>순회 상한. 100×20 = 2000 부대면 어떤 전개에서도 충분하고, 서버 버그 시 무한 순회를 막는다.</summary>
    private const int MAX_PAGES = 20;

    /// <summary>해석 실패 후 재시도 최소 간격(ms). 저장 배치가 건마다 재조회하는 것을 막는다.</summary>
    private const long RETRY_INTERVAL_MS = 60_000;

    private readonly IUnitApiService? _unitApiService;
    private readonly INatsSetupModel? _natsSetupModel;
    private readonly IServerContractProbe? _contractProbe;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 해석 성공 캐시. <b>0 = 미해석</b>(부대 id 는 항상 1 이상이다 — 서버·UnitApiService 모두 <c>&lt;= 0</c> 을 거절한다).
    /// </summary>
    /// <remarks>
    /// <c>int?</c> 가 아니라 <c>int</c> 인 이유: <c>volatile</c> 은 값 형식 <c>Nullable&lt;int&gt;</c> 에 붙일 수 없고
    /// (CS0677), 여러 스레드가 읽는 캐시라 원자적 게시가 필요하다.
    /// </remarks>
    private volatile int _resolvedIdOrZero;

    private long _lastAttemptTick;
    private bool _configWarned;
    private bool _permWarned;
    #endregion
}
