using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      : 구역(PidsGroup) 심볼 더블클릭 → 그 구역에서 '가장 먼저 발생한' 활성 이벤트의
                  조치보고 패널을 여는 오케스트레이션.
                  GMap_PidsGroup_DoubleClick_ActionReport PRD v1.2 (FR-02~FR-05, FR-07, FR-08)
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 이벤트가 살아있는 구역 심볼을 더블클릭했을 때 조치보고 패널을 여는 진입점.
/// 대상 선정(EQM 최선착) · 원본 모델 해석 · 권한/중복 게이트를 모두 담당한다.
/// </summary>
public interface IGroupActionReportLauncher
{
    /// <summary>
    /// 구역 심볼 하나에 대해 조치보고 패널을 연다.
    /// 조건 미충족(이벤트 없음 · 장비그룹 미연결 · 권한 없음 · 이미 열림 · 모델 미해석)이면
    /// 사유를 로그(필요 시 안내 팝업)로 남기고 <c>false</c>를 반환한다. 예외는 던지지 않는다.
    /// </summary>
    bool TryOpenForGroup(IPidsGroupEditableMarker marker);
}
