using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Watchdog;
/****************************************************************************
   Purpose      : 재시작 정책 — 지수 백오프 + 슬라이딩 윈도우 서킷브레이커.
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 크래시루프 보호. 재시작마다 지수 백오프(5/10/20/40/60s 상한)를 적용하고,
/// 윈도우(기본 10분) 내 재시작이 상한(기본 5회)을 넘으면 DEGRADED로 전환하여 재시작을 중단한다.
/// 순수 로직(<see cref="IClock"/> 주입)으로 단위 테스트 가능하다.
/// </summary>
internal sealed class RestartPolicy
{
    #region - Ctors -
    public RestartPolicy(WatchdogOptions opt, IClock clock)
    {
        _opt = opt;
        _clock = clock;
    }
    #endregion
    #region - Properties -
    public int RestartCount { get; private set; }
    public DateTime? LastRestartUtc { get; private set; }
    public bool IsDegraded { get; private set; }
    #endregion
    #region - Processes -
    /// <summary>지금 재시작이 허용되는가(백오프 경과 + 서킷브레이커 미개방).</summary>
    public bool CanRestartNow()
    {
        if (IsDegraded) return false;

        var now = _clock.UtcNow;
        _recent.RemoveAll(t => (now - t).TotalMilliseconds > _opt.RestartWindowMs);

        if (_recent.Count >= _opt.MaxRestartsPerWindow)
        {
            IsDegraded = true;
            return false;
        }

        if (_nextAllowedUtc.HasValue && now < _nextAllowedUtc.Value)
            return false;

        return true;
    }

    /// <summary>재시작 수행 기록 + 다음 허용 시각(백오프) 계산.</summary>
    public void RecordRestart()
    {
        var now = _clock.UtcNow;
        _recent.Add(now);
        RestartCount++;
        LastRestartUtc = now;
        _consecutive++;

        int exp = Math.Min(_consecutive - 1, 4);           // 0..4
        long backoff = Math.Min(_opt.MaxBackoffMs, 5000L * (1L << exp)); // 5,10,20,40,80→cap60
        _nextAllowedUtc = now.AddMilliseconds(backoff);
    }

    /// <summary>대상이 안정적으로 정상일 때 백오프 카운터 리셋(서킷브레이커 상태는 유지).</summary>
    public void NotifyHealthy()
    {
        _consecutive = 0;
        _nextAllowedUtc = null;
    }
    #endregion
    #region - Attributes -
    private readonly WatchdogOptions _opt;
    private readonly IClock _clock;
    private readonly List<DateTime> _recent = new();
    private int _consecutive;
    private DateTime? _nextAllowedUtc;
    #endregion
}
