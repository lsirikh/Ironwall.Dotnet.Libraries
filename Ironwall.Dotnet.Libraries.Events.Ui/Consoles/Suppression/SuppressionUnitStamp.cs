using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 새 억제 스케줄에 실을 부대 id — 콘솔 서랍과 옛 억제 패널이 같은 규칙을 쓴다.
   Created By   : GHLee
   Created On   : 2026-09-24
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 억제 스케줄 <b>생성</b>의 <c>unit_id</c> 규칙.
/// </summary>
/// <remarks>
/// <para>서버 8.0 (<c>schemas/event_suppression.py:68-71</c>): 생략하면 기본 부대로 귀속하고 <b>서버 로그에만</b>
/// 경고를 남긴다 — "다음 차수부터 필수". 응답에는 신호가 없어 클라이언트가 모른 채 지나간다
/// (실서버 왕복 E2a · E2f 로 확인: 두 창 모두 키를 싣지 않았다).</para>
/// <para>8.0 미만(운영 6.3.2 포함) 쓰기 스키마에는 이 키가 없다(<c>extra=forbid</c>) — 그래서 판본(<see cref="IUnitScopeService.IsUnitEra"/>)으로 가른다.</para>
/// <para>수정(PATCH)에는 쓰지 않는다 — RFC 7396 에서 보내지 않은 키는 그대로 남으니 원래 부대가 보존된다(D-13 과 같은 결론).</para>
/// </remarks>
public static class SuppressionUnitStamp
{
    /// <summary>
    /// 새 스케줄에 실을 부대 id. 8.0 이상이고 해석에 성공했을 때만 값이 있다. 실패는 <c>null</c> + 경고(키를 싣지 않는다).
    /// </summary>
    public static async Task<int?> ResolveForCreateAsync(IUnitScopeService? scope, ILogService? log, string caller, CancellationToken token = default)
    {
        if (scope is null || !scope.IsUnitEra) return null;
        int? unitId;
        try { unitId = await scope.ResolveAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            log?.Warning($"[{caller}] 부대 id 해석 실패 — unit_id 를 생략합니다(서버가 기본 부대로 귀속): {ex.Message}");
            return null;
        }
        if (unitId is null)
            log?.Warning($"[{caller}] 부대 id 를 해석하지 못해 unit_id 를 생략했습니다(부대 코드: {scope.UnitCode ?? "(미설정)"}).");
        return unitId;
    }

    /// <summary>부대 해석기를 늦게 찾는다 — 장비 쓰기 관문과 같은 관용구. 미등록이면 <c>null</c>.</summary>
    public static IUnitScopeService? ResolveFromIoC()
    {
        try { return IoC.Get<IUnitScopeService>(); }
        catch { return null; }
    }
}
