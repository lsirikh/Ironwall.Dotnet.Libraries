using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
/****************************************************************************
   Purpose      : 장비 쓰기 DTO 에 unit_id 를 싣는 단일 관문 (서버 8.0 부대 편제)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 쓰기 DTO 에 <c>unit_id</c> 를 싣는 <b>유일한 관문</b>.
/// </summary>
/// <remarks>
/// <para><b>왜 정적 헬퍼인가</b> — 7개 패널 + 2개 다이얼로그가 공유하고, 패널 VM 은 컨테이너가 만들지만
/// 다이얼로그 VM 은 코드에서 <c>new</c> 된다. 생성자 주입을 추가하면 두 경로가 갈라지고
/// 기존 호출부(테스트 포함)의 시그니처가 깨진다. 같은 이유로 만들어진 선례가
/// <see cref="DevicePermissionGate"/>(권한) · <c>DeviceQueryPolicy.Resolve()</c>(조회 정책)다 — 그 관용구를 따른다.</para>
///
/// <para><b>계약</b> — 8.0 미만(프로브 미확보 포함)에서는 <c>unit_id</c> 를 <b>확실히 지운다</b>.
/// 6.3·7.0 쓰기 스키마에 없는 키이고 7.0 이후는 <c>additionalProperties:false</c> 라
/// 실리는 순간 422 다. <c>DeviceApiService.ShapeWrite</c> 가 같은 소거를 한 번 더 하지만
/// 방어를 두 겹으로 두는 편이 낫다(서버 쓰기 경로가 하나가 아니다).</para>
///
/// <para>⚠ <b>수정(PUT/PATCH) 시의 의미</b> — 장비 <b>모델</b>에는 <c>unit_id</c> 축이 없어
/// 편집 본문에 실리는 값은 언제나 <b>"이 클라이언트의 현재 부대"</b>다. 단일 부대 전개
/// (실측 2026-09-18: <c>total=1</c>, <c>unit001</c>)에서는 무해하지만, 다부대로 가면
/// <b>모델·읽기 경로에 <c>unit_id</c> 축을 먼저 신설</b>해야 한다(그때까지 타 부대 장비를 이 화면에서 편집하면 안 된다).
/// 값을 아예 생략하는 쪽은 더 나쁘다 — 8.0 서버가 <b>기본 부대로 재귀속</b>시키고 응답에는 신호를 남기지 않는다.</para>
/// </remarks>
internal static class UnitScopeGate
{
    /// <summary>
    /// 쓰기 직전 DTO 에 <c>unit_id</c> 를 싣는다. 8.0 미만이거나 해석 실패면 <b>싣지 않고</b> 경고를 남긴다.
    /// </summary>
    /// <param name="dto">장비 쓰기 DTO(<see cref="BaseDeviceDto"/> 파생 전부).</param>
    /// <param name="caller">진단용 호출부 이름(로그에 그대로 찍힌다).</param>
    /// <param name="log">호출부 로거(선택).</param>
    internal static async Task StampAsync(
        BaseDeviceDto? dto,
        string caller,
        ILogService? log = null,
        CancellationToken token = default)
    {
        if (dto == null) return;

        var scope = Resolve();
        if (scope == null || !scope.IsUnitEra)
        {
            // 8.0 미만·미등록 — 키 자체가 나가지 않게 확실히 지운다(운영 6.3.2 무회귀).
            dto.UnitId = null;
            return;
        }

        int? unitId;
        try
        {
            unitId = await scope.ResolveAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            log?.Warning($"[{caller}] 부대 id 해석 중 예외 — unit_id 를 생략합니다"
                       + $"(서버가 기본 부대로 귀속시킵니다): {ex.Message}");
            dto.UnitId = null;
            return;
        }

        dto.UnitId = unitId;

        if (unitId == null)
            WarnOmitted(caller, scope.UnitCode, log);
    }

    /// <summary>
    /// <see cref="IUnitScopeService"/> IoC lazy 해석. 미등록(오프라인·DB 모드·테스트) 시 <c>null</c>.
    /// </summary>
    /// <remarks><see cref="DevicePermissionGate.Resolve"/> 와 동일 관용구.</remarks>
    internal static IUnitScopeService? Resolve()
    {
        try { return IoC.Get<IUnitScopeService>(); }
        catch { return null; }
    }

    /// <summary>
    /// 생략 사실을 남긴다 — 서버는 이 누락을 <b>응답으로 알려주지 않으므로</b> 우리 로그가 유일한 관측 지점이다.
    /// </summary>
    /// <remarks>
    /// 저장 1회에 장비 수십 건이 돌 수 있어 <see cref="OMIT_WARN_INTERVAL_MS"/> 로 묶는다
    /// (완전 억제가 아니라 간격 제한 — 사실이 사라지면 안 된다). 시계는 단조 증가하는
    /// <see cref="Environment.TickCount64"/> 를 쓴다.
    /// </remarks>
    private static void WarnOmitted(string caller, string? unitCode, ILogService? log)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastOmitWarnTick);
        if (last != 0 && now - last < OMIT_WARN_INTERVAL_MS) return;
        Interlocked.Exchange(ref _lastOmitWarnTick, now);

        log?.Warning($"[{caller}] 부대 id 를 해석하지 못해 unit_id 를 생략했습니다"
                   + $"(부대 코드: {unitCode ?? "(미설정)"}). 서버 8.0 은 생략 시 장비를 "
                   + $"기본 부대로 귀속시키고 응답에는 아무 신호도 남기지 않습니다 — 부대 편제·GroupNats 설정을 확인하십시오.");
    }

    /// <summary>생략 경고 최소 간격(ms).</summary>
    private const long OMIT_WARN_INTERVAL_MS = 60_000;

    private static long _lastOmitWarnTick;
}
