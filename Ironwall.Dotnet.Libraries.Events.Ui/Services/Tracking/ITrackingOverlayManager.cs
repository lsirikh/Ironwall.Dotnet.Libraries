using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 오버레이 마커 단일 진입점 (NATS → 지도 마커)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// TRACKING_STATUS 수신 결과를 GMap 마커로 반영하는 단일 진입점.
/// <para><b>배치 정의(Events.Ui)</b>: NATS 핸들러(Events.Ui)가 호출하고 구현체는 GMaps.Ui에 둔다
/// — GMaps.Ui→Events.Ui 단방향 참조라 순환이 없다(인터페이스를 GMaps.Ui에 두면 순환).</para>
/// <para><b>스레드 안전(NFR-01)</b>: 모든 Markers 변경·마커 생성은 구현체가 UI Dispatcher 단일 직렬화
/// 경로에서만 수행한다. 호출자는 NATS ThreadPool 콜백에서 직접 호출해도 안전해야 한다.</para>
/// </summary>
public interface ITrackingOverlayManager : IAsyncDisposable
{
    /// <summary>
    /// 한 카메라의 targets[]를 일괄 upsert(생성/위치갱신). 단일 Dispatcher 블록에서 처리(NFR-02).
    /// 마커 키 = (cameraId, target.track_id) 복합. observed_at 역전은 구현체가 skip.
    /// </summary>
    /// <param name="cameraId">body.camera_id (복합키 절반).</param>
    /// <param name="targets">body.targets[] (빈 배열이면 무동작).</param>
    /// <param name="messageTtlSec">body.ttl_sec (null이면 설정 폴백).</param>
    Task UpsertBatchAsync(int cameraId, IReadOnlyList<TrackingTargetDto> targets, int? messageTtlSec, CancellationToken ct = default);

    /// <summary>
    /// tracking == lost/idle 수신 시 해당 카메라의 모든 추적 마커를 제거(페이드)한다(§8.3.7 소멸 규칙).
    /// </summary>
    Task ExpireCameraAsync(int cameraId, CancellationToken ct = default);

    /// <summary>추적 마커 전체 제거(추적 비활성 토글 등).</summary>
    Task ClearAllAsync(CancellationToken ct = default);
}
