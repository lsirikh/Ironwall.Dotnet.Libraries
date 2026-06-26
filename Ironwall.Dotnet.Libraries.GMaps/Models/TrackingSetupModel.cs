namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 추적 오버레이 설정 모델 구현 (기본값 + 복사생성자)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// <see cref="ITrackingSetupModel"/> 기본 구현(<c>StreamingSetupModel</c> 선례).
/// <para>단일 live 인스턴스로 DI 등록 → 오버레이 매니저·설정 UI가 공유(get/set 즉시 반영).
/// 값은 appsettings(<c>AppSettings.Tracking</c>)에서 로드/저장.</para>
/// </summary>
public class TrackingSetupModel : ITrackingSetupModel
{
    /// <summary>추적 오버레이 on/off (기본 ON).</summary>
    public bool IsTrackingEnabled { get; set; } = true;

    /// <summary>트레일 포인트 상한(FIFO). 기본 30(D-13).</summary>
    public int TrailMaxPoints { get; set; } = 30;

    /// <summary>메시지 ttl_sec 미지정 시 폴백 TTL(초). 명세 기본 5, 최솟값 가드 3.</summary>
    public int DefaultTtlSec { get; set; } = 5;

    /// <summary>lost 진입 후 마커 유지 유예(초).</summary>
    public int LostTtlSec { get; set; } = 3;

    /// <summary>이 줌 미만에서 추적 마커 숨김(0=항상 표시).</summary>
    public double MinVisibleZoom { get; set; } = 0d;

    /// <summary>속도 계산 상한(m/s). 초과 시 이전값 유지. 기본 50(D-21).</summary>
    public double MaxSpeedMs { get; set; } = 50d;

    /// <summary>observed_at gap이 이 초를 넘으면 트레일 세그먼트 분절. 기본 10.</summary>
    public int GapThresholdSec { get; set; } = 10;

    /// <summary>Playback 1회 조회 최대 시간(시간). 기본 6.</summary>
    public int MaxPlaybackHours { get; set; } = 6;

    /// <summary>로컬 추적 좌표 보존 일수. 기본 7. 0=무제한.</summary>
    public int RetentionDays { get; set; } = 7;

    public TrackingSetupModel() { }

    /// <summary>복사 생성자.</summary>
    public TrackingSetupModel(ITrackingSetupModel model)
    {
        if (model is null) return;
        IsTrackingEnabled = model.IsTrackingEnabled;
        TrailMaxPoints = model.TrailMaxPoints;
        DefaultTtlSec = model.DefaultTtlSec;
        LostTtlSec = model.LostTtlSec;
        MinVisibleZoom = model.MinVisibleZoom;
        MaxSpeedMs = model.MaxSpeedMs;
        MaxPlaybackHours = model.MaxPlaybackHours;
        GapThresholdSec = model.GapThresholdSec;
        RetentionDays = model.RetentionDays;
    }
}
