using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 좌표 조회 seam (Playback read) — 로컬/서버 교체점
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// Playback이 시간범위로 추적 좌표를 읽는 포트. 구현=로컬 <see cref="TrackPointStore"/>(현재) /
/// 미래 <c>ApiTrackPointReader</c>(서버 read-only GET). <see cref="ITrackPointWriter"/>의 읽기 짝 —
/// 다중 스테이션 시 서버 권위 이력으로 전환하려면 이 포트 구현체만 DI 교체.
/// <para>소비자=<c>PlaybackViewModel</c>(GMaps.Ui)이라 인터페이스도 GMaps.Ui에 둠(writer는 Events.Ui 핸들러가 호출해 Events.Ui에 위치).</para>
/// <para>분석: <c>docs/analyses/Tracking_API_vs_LocalDB-analysis.md</c> §1.1, §6.</para>
/// </summary>
public interface ITrackPointReader
{
    /// <summary>시간범위(UTC) 좌표 조회 — observed_at ASC. 실패 시 빈 목록(비블로킹).</summary>
    Task<List<ITrackPointModel>> FetchAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// 마지막 <see cref="FetchAsync"/> 가 남긴 운영자 알림(권한 없음 · 실패 · 부분). 문제가 없었으면 null(G12).
    /// 기본 구현(로컬 판독기 등)은 늘 null.
    /// </summary>
    string? LastFetchNotice => null;

    /// <summary>Playback 조회 전 점검 — 저장소를 쓸 수 없으면 알림 문구, 아니면 null(G13). 기본 구현은 늘 null.</summary>
    Task<string?> CheckAvailabilityAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
}
