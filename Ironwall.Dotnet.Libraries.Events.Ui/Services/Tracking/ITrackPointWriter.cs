using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 좌표 로컬 DB 기록 seam (NATS 핸들러용)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 수신 추적 타겟을 로컬 DB에 영속하는 쓰기 seam(영속=우선 로컬 자체 DB, 2026-06-25 결정).
/// <para>인터페이스는 Events.Ui(핸들러가 호출), 구현은 GMaps.Ui(<c>TrackPointStore</c>→GMapDb). GMaps.Ui→Events.Ui 단방향이라 비순환.
/// 후속 서버 전환 시 <c>ApiTrackPointWriter</c>로 교체 가능한 seam.</para>
/// <para>호출 스레드 = NATS 콜백(비-UI). DB I/O라 UI Dispatcher 불요.</para>
/// </summary>
public interface ITrackPointWriter
{
    /// <summary>한 카메라의 targets[]를 로컬 DB에 일괄 기록(active 수신 시).</summary>
    Task WriteBatchAsync(int cameraId, IReadOnlyList<TrackingTargetDto> targets, CancellationToken ct = default);
}
