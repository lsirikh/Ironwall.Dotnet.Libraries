using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;

/****************************************************************************
   Purpose      : 조립 쓰기의 계약 가드 — 6.3 서버에는 한 줄도 보내지 않는다
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부품 모델이 <b>없는 판본</b>(6.3)에 조립 본문을 보내지 못하게 막는 단일 관문.
/// </summary>
/// <remarks>
/// <para><b>왜 대시보드 아래에도 필요한가</b> — 화면이 조립 버튼을 감추는 것만으로는 부족하다.
/// 이 두 서비스(<see cref="PresetRegistrar"/> · <see cref="ComponentApplyService"/>)는 프리뷰 도구 ·
/// 다른 VM · 테스트에서 <b>직접</b> 불릴 수 있고, 그때 6.3 서버로 나가면 본문이 평면 레거시 모양으로
/// 재조립된다(<c>DeviceApiService.ShapeWrite</c> 가 <c>UseAxisWrite</c> 를 <b>살아 있는 계약으로 덮어쓴다</b> —
/// 호출부가 무엇을 켰든 상관없다). 그 결과는 "부품이 조용히 빠진 채 성공"이 아니라
/// <b>장비의 평면 필드가 빈 값으로 덮이는 쓰기</b>다 — 조립기가 채우지 않은 <c>ip_address</c> ·
/// <c>mode</c> · <c>speaker_type</c> 가 전부 기본값으로 나간다.</para>
/// <para>그래서 판단은 <b>보내기 전</b>에 하고, 막혔으면 <b>API 를 한 번도 부르지 않는다</b>.</para>
/// </remarks>
internal static class AssemblyWriteGuard
{
    /// <summary>사람에게 보일 한 줄 — 왜 아무 일도 일어나지 않았는지가 문장에 들어 있어야 한다.</summary>
    internal const string LEGACY_CONTRACT_MESSAGE =
        "현재 서버에서는 부품 구성을 지원하지 않아 등록하거나 적용하지 않았습니다.";

    /// <summary>축 계약(7.0+)이 아니면 <c>true</c> — 부르는 쪽은 즉시 실패를 돌려준다.</summary>
    internal static bool IsBlocked(DeviceQueryPolicy policy) => !policy.IsAxisContract;
}
