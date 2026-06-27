using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 추적 오버레이 설정 모델 (appsettings ↔ 맵세팅 UI live)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 오버레이/트레일/TTL 동작을 제어하는 설정 모델.
/// <para>소비앱 God-Model이 <see cref="IGMapSetupModel"/>처럼 구현하며, GMaps.Ui 오버레이 매니저·설정 VM이
/// 동일 live 인스턴스를 공유한다(get/set — 설정 UI 즉시 반영).</para>
/// </summary>
public interface ITrackingSetupModel
{
    /// <summary>추적 오버레이 표시 on/off (live 토글 — OFF 시 upsert skip + 마커 일괄 제거).</summary>
    bool IsTrackingEnabled { get; set; }

    /// <summary>트레일 포인트 큐 상한(FIFO). 기본 30, 상한 50~100.</summary>
    int TrailMaxPoints { get; set; }

    /// <summary>메시지 ttl_sec 미지정 시 폴백 TTL(초). 기본 5, 최솟값 가드 3.</summary>
    int DefaultTtlSec { get; set; }

    /// <summary>tracking=lost 진입 후 마커 유지 유예(초) — 경과 시 제거(페이드).</summary>
    int LostTtlSec { get; set; }

    /// <summary>이 줌 미만에서는 추적 마커를 숨김(가독성).</summary>
    double MinVisibleZoom { get; set; }

    /// <summary>속도 계산 상한(m/s). 초과 시 이전값 유지 + Warning(이상치 방어).</summary>
    double MaxSpeedMs { get; set; }

    /// <summary>observed_at gap이 이 초를 넘으면 트레일 세그먼트 분절.</summary>
    int GapThresholdSec { get; set; }

    /// <summary>Playback 1회 조회 최대 시간(시간). 초과 범위는 제한(과다 로드·OOM 방지).</summary>
    int MaxPlaybackHours { get; set; }

    /// <summary>로컬 추적 좌표 보존 일수. 이 일수 경과분은 주기 삭제(무한 증가 방지). 0=무제한.</summary>
    int RetentionDays { get; set; }

    /// <summary>Playback 데이터 소스 — 로컬 DB / 서버 API 토글(기본 Local). FetchAsync마다 selector가 읽어 라이브 전환.</summary>
    EnumTrackDataSource DataSource { get; set; }
}
