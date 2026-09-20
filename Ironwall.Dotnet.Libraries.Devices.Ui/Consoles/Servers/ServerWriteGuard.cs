using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터의 계약 게이트 — 없는 경로를 부르지 않는다 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 판본마다 <b>있고 없음이 갈리는</b> 서버 경로의 단일 관문 — 화면이 감추는 것만으로는 부족하다.
/// </summary>
/// <remarks>
/// <para>서비스·미리보기·테스트가 뷰모델을 거치지 않고 직접 부를 수 있다. 그래서 <b>판단은 서비스 층에서</b>
/// 한 번 더 하고, 막혔으면 <b>API 를 한 번도 부르지 않는다</b>(선례: <c>AssemblyWriteGuard</c>).</para>
/// <para>판정은 언제나 <b>순서 비교</b>다(<c>&gt;=</c>). <c>== V7_0</c> 같은 동치 비교는 8.0 서버를
/// 조용히 레거시로 떨어뜨린다 — 레포에서 이미 한 번 값을 치른 실수다.</para>
/// </remarks>
internal static class ServerWriteGuard
{
    /// <summary>부대 편제(<c>unit_id</c> · 부대 필터 · "부대" 열)가 있는 판본인가 — 8.0 이상.</summary>
    internal static bool IsUnitEra(DeviceQueryPolicy policy)
        => (policy ?? throw new ArgumentNullException(nameof(policy))).Contract >= EnumServerContract.V8_0;

    /// <summary>
    /// 프록시 설정 경로(<c>GET /api/servers/{id}/proxy-settings</c>)가 살아 있는 판본인가.
    /// 7.0 에서 <c>410 ENDPOINT_REMOVED</c> 묘비가 됐다.
    /// </summary>
    internal static bool CanReadProxySettings(DeviceQueryPolicy policy)
        => (policy ?? throw new ArgumentNullException(nameof(policy)))
            .IsEndpointAvailable(EnumDeviceLegacyEndpoint.ServerProxySettings);

    /// <summary>프록시 설정 창이 사라진 판본에서 "설정" 절에 적는 한 줄(스토리보드 L1361).</summary>
    internal const string PROXY_ABSORBED_NOTE =
        "프록시 설정 창은 없어졌습니다(410) — 운용 모드는 이 서버의 server_config 로 옮겨졌습니다.";

    /// <summary>부대 축이 없는 판본에서 "소속 부대" 절에 적는 한 줄.</summary>
    internal const string UNIT_NOT_IN_CONTRACT_NOTE =
        "이 서버 판본에는 부대 편제가 없습니다(8.0 이상에서만).";

    /// <summary>상태는 이 콘솔이 쓰지 않는다 — 어디서든 같은 문장을 쓴다.</summary>
    internal const string STATUS_IS_OBSERVED_NOTE =
        "상태는 관측 값입니다 — 서버 매니저가 보고합니다. 이 화면에서는 고칠 수 없습니다.";
}
