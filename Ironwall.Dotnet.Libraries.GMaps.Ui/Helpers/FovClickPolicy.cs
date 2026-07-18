namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 카메라 마커 클릭 시 FOV 켜기 정책 (순수 로직 — WPF 무의존, 단위 테스트 가능)
   Created By   : GHLee
   Created On   : 7/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 카메라 심볼 클릭 시 FOV 표시를 켜야 하는지 판정하는 정책 헬퍼.
/// <para>정책(사용자 확정): 클릭 = FOV "켜기" 전용. 끄기는 속성 패널 체크박스로만.</para>
/// <para>이미 켜져 있으면 클릭은 무동작 — 반복 클릭으로 상태가 반대로 튀는 desync를 방지한다.</para>
/// </summary>
internal static class FovClickPolicy
{
    /// <summary>
    /// 현재 FOV 표시 상태에서 카메라 클릭이 FOV를 켜야 하는가.
    /// </summary>
    /// <param name="currentShowFov">현재 FOV 표시 여부</param>
    /// <returns>꺼져 있으면 true(켜기), 이미 켜져 있으면 false(무동작)</returns>
    internal static bool ShouldTurnOnFov(bool currentShowFov) => !currentShowFov;
}
