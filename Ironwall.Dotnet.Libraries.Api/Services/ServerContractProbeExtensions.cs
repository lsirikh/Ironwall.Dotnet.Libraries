using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Api.Services;
/****************************************************************************
   Purpose      : 계약 세대 소비자용 지연 확보 헬퍼 — FR-08 미확보 첫 호출 대응
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="IServerContractProbe"/> 소비자 편의 확장.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — <see cref="ServerContractBootService"/> 가 부팅 선두에서 확보하지만,
/// ① 예산(<see cref="ServerContractBootService.BUDGET_SEC"/>) 초과로 배경으로 넘어간 경우
/// ② 프로브가 선택 주입이라 <c>null</c> 인 경우
/// 두 가지에서는 <see cref="IServerContractProbe.Contract"/> 를 <b>그냥 읽으면</b> 폴백
/// <see cref="EnumServerContract.V6_3"/> 이 나온다.</para>
/// <para><b>쓰는 법</b> — 분기 직전에 동기 프로퍼티 대신 이 확장을 await 한다:
/// <code>var contract = await _contractProbe.GetContractAsync(token).ConfigureAwait(false);</code>
/// <see cref="IServerContractProbe.ResolveAsync"/> 가 멱등이라 확보 후에는 <b>왕복이 없다</b>
/// (첫 호출만 최대 1왕복, 이후는 필드 읽기 수준).</para>
/// <para><b>안전 방향</b> — <c>null</c> 프로브·확보 실패·취소는 전부
/// <see cref="EnumServerContract.V6_3"/>(현 운영 판본)으로 수렴한다. 예외를 던지지 않는다.</para>
/// </remarks>
public static class ServerContractProbeExtensions
{
    /// <summary>
    /// 계약 세대를 돌려준다. 아직 미확보면 <b>그 자리에서 1회 확보를 시도</b>한다(멱등).
    /// </summary>
    /// <param name="probe">프로브. <c>null</c>(미등록·미주입)이면 <see cref="EnumServerContract.V6_3"/>.</param>
    /// <param name="token">취소 토큰. 취소되면 확보를 포기하고 현재 값(폴백 포함)을 돌려준다.</param>
    public static async ValueTask<EnumServerContract> GetContractAsync(this IServerContractProbe? probe,
                                                                      CancellationToken token = default)
    {
        if (probe is null) return EnumServerContract.V6_3;
        if (probe.IsResolved) return probe.Contract;

        try
        {
            await probe.ResolveAsync(token).ConfigureAwait(false);
        }
        catch
        {
            // 프로브는 예외를 던지지 않도록 설계돼 있다. 방어적 흡수 — 폴백으로 진행한다.
        }

        return probe.Contract;
    }

    /// <summary>
    /// 계약이 7.0 이상(축 전환 이후)인지. 미확보면 그 자리에서 확보를 시도한다.
    /// </summary>
    public static async ValueTask<bool> IsAxisEraAsync(this IServerContractProbe? probe,
                                                       CancellationToken token = default)
        => await probe.GetContractAsync(token).ConfigureAwait(false) >= EnumServerContract.V7_0;
}
