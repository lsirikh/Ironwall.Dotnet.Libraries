using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 카메라 팝업 제어 허브의 화면 위치(X,Y) 영속 추상화 (CameraPopup_ControlHub FR-08)
   Created By   : Claude Code
   Created On   : 2026-07-27
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 제어 허브(<c>CameraPopupControlHub</c>)의 드래그 이동 위치(Canvas 화면 좌표)를 영속·복원.
/// RTSP 팝업 위치(<see cref="ICameraPopupPositionStore"/>)와 동일 저장소(GMapDb)·폴백 전략을 답습하되,
/// 허브는 전역 1개라 단일 값(카메라 Id 없음). 화면 고정 좌표(맵 팬/줌에 불변).
/// </summary>
public interface ICameraPopupHubPositionStore
{
    /// <summary>저장된 허브 화면 좌표 조회. 없으면 null(→ 호출측이 기본 우하단 도킹).</summary>
    Task<(double X, double Y)?> TryGetAsync(CancellationToken token = default);

    /// <summary>드래그 종료 위치(Canvas Left/Top)를 저장(Upsert). DB 실패해도 인메모리로 세션 내 유지.</summary>
    Task SaveAsync(double x, double y, CancellationToken token = default);
}
