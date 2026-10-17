using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
/****************************************************************************
   Purpose      : 추적 Playback 타임라인 엔진 (멀티타겟 단일 시계, 진입방식 무관 코어)
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 로컬 DB에서 로드한 추적 좌표를 <b>하나의 타임라인</b>에서 재생한다(멀티타겟 동기화).
/// <para>UI(콘솔)·렌더(오버레이)와 분리된 코어 — 진입 방식이 바뀌어도 재사용. <see cref="SnapshotAt"/>(시각별 위치)는
/// 순수 로직이라 단위 테스트 가능. 배속·탐색·자동정지는 <see cref="DispatcherTimer"/>+<see cref="IClock"/> wallclock으로.</para>
/// </summary>
public sealed class PlaybackEngine : IDisposable
{
    private const int TickIntervalMs = 200;          // 렌더 부드러움
    private const double MinSpeed = 0.5, MaxSpeed = 4.0;
    private const int DefaultTrailMax = 30;
    private const int DefaultPresenceWindowSec = 5;   // 이 시간 내 점 없으면 트랙 퇴장(종료/gap)
    private const int DefaultGapThresholdSec = 10;    // 연속 점 간 이 초 초과면 세션 분절

    private readonly IClock _clock;
    private readonly ILogService? _log;
    private readonly Dictionary<string, List<ITrackPointModel>> _byTrack = new();
    private DispatcherTimer? _timer;
    private DateTime _lastTickUtc;

    public PlaybackEngine(IClock clock, ILogService? log = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _log = log;
    }

    #region - State -
    public DateTime Start { get; private set; }
    public DateTime End { get; private set; }
    public DateTime CurrentTime { get; private set; }
    public double Speed { get; private set; } = 1.0;
    public bool IsPlaying { get; private set; }
    public int TrailMaxPoints { get; set; } = DefaultTrailMax;
    public int PresenceWindowSec { get; set; } = DefaultPresenceWindowSec;
    /// <summary>연속 점 간 observed_at 간격이 이 초를 넘으면 트레일 분절(세션 경계 — 직선 연결 방지).</summary>
    public int GapThresholdSec { get; set; } = DefaultGapThresholdSec;

    /// <summary>0.0~1.0 진행률.</summary>
    public double Progress
    {
        get
        {
            var span = (End - Start).TotalMilliseconds;
            return span <= 0 ? 0 : Math.Clamp((CurrentTime - Start).TotalMilliseconds / span, 0, 1);
        }
    }

    /// <summary>매 tick/탐색 시 발화 — 구독자가 <see cref="SnapshotAt"/>로 현재 프레임 렌더.</summary>
    public event Action? Frame;
    /// <summary>재생 종료(End 도달) 시 발화.</summary>
    public event Action? Completed;
    #endregion

    /// <summary>좌표 로드 — observed_at 정렬·트랙별 그룹·구간 확정. CurrentTime=Start.</summary>
    public void Load(IReadOnlyList<ITrackPointModel> points)
    {
        Stop();
        _byTrack.Clear();
        if (points is null || points.Count == 0)
        {
            Start = End = CurrentTime = _clock.UtcNow;
            Frame?.Invoke();
            return;
        }

        foreach (var p in points)
        {
            if (string.IsNullOrWhiteSpace(p.TrackId)) continue;
            if (!_byTrack.TryGetValue(p.TrackId, out var list))
                _byTrack[p.TrackId] = list = new List<ITrackPointModel>();
            list.Add(p);
        }
        foreach (var list in _byTrack.Values)
            list.Sort((a, b) => a.ObservedAt.CompareTo(b.ObservedAt));

        Start = points.Min(p => p.ObservedAt);
        End = points.Max(p => p.ObservedAt);
        CurrentTime = Start;
        _log?.Info($"[Playback] 로드: {points.Count}점 / {_byTrack.Count}트랙 / {Start:HH:mm:ss}~{End:HH:mm:ss}");
        Frame?.Invoke();
    }

    #region - Transport -
    public void Play()
    {
        if (IsPlaying || End <= Start) return;
        if (CurrentTime >= End) CurrentTime = Start;   // 끝에서 다시 누르면 처음부터
        IsPlaying = true;
        _lastTickUtc = _clock.UtcNow;
        _timer ??= CreateTimer();
        _timer.Start();
    }

    public void Pause()
    {
        IsPlaying = false;
        _timer?.Stop();
    }

    public void Stop()
    {
        Pause();
        CurrentTime = Start;
    }

    /// <summary>배속 설정(0.5~4×). 재생 중에도 즉시 반영.</summary>
    public void SetSpeed(double speed)
    {
        Speed = Math.Clamp(speed, MinSpeed, MaxSpeed);
        _lastTickUtc = _clock.UtcNow;   // 배속 변경 시점부터 재측정(점프 방지)
    }

    /// <summary>특정 시각으로 탐색.</summary>
    public void SeekTo(DateTime t)
    {
        CurrentTime = t < Start ? Start : (t > End ? End : t);
        _lastTickUtc = _clock.UtcNow;
        Frame?.Invoke();
    }

    /// <summary>0.0~1.0 진행률로 탐색(타임라인 스크럽).</summary>
    public void SeekToProgress(double progress)
    {
        var p = Math.Clamp(progress, 0, 1);
        SeekTo(Start.AddMilliseconds((End - Start).TotalMilliseconds * p));
    }
    #endregion

    /// <summary>
    /// 시각 t의 트랙별 스냅샷 — t 이하 최근 점=현재 위치, 최근 N점=트레일.
    /// 마지막 점이 t보다 <see cref="PresenceWindowSec"/>초 넘게 과거면 퇴장(미포함). <b>순수 로직(테스트 대상)</b>.
    /// </summary>
    public IReadOnlyList<TrackSnapshot> SnapshotAt(DateTime t)
    {
        var result = new List<TrackSnapshot>();
        var presence = TimeSpan.FromSeconds(Math.Max(1, PresenceWindowSec));

        foreach (var (trackId, list) in _byTrack)
        {
            int idx = UpperBound(list, t);   // ObservedAt <= t 인 마지막 index
            if (idx < 0) continue;           // 아직 등장 전
            var current = list[idx];
            if (t - current.ObservedAt > presence) continue;   // 종료/gap → 퇴장

            // 트레일: idx에서 뒤로 — TrailMax 상한 + 연속 점 gap 초과 시 분절(세션 경계서 멈춤 → 직선 연결 방지)
            int maxTrail = Math.Max(2, TrailMaxPoints);
            var gap = TimeSpan.FromSeconds(Math.Max(1, GapThresholdSec));
            int from = idx;
            while (from > 0
                   && (idx - from + 1) < maxTrail
                   && (list[from].ObservedAt - list[from - 1].ObservedAt) <= gap)
                from--;
            var trail = list.GetRange(from, idx - from + 1);

            double? speed = null;
            if (trail.Count >= 2)
            {
                var a = trail[^2]; var b = trail[^1];
                double dist = TrackingMath.HaversineMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                double dt = (b.ObservedAt - a.ObservedAt).TotalSeconds;
                speed = TrackingMath.ComputeSpeedMps(dist, dt, 0);   // max=0 → 가드 없이 계산값
            }

            result.Add(new TrackSnapshot
            {
                CameraId = current.CameraId,
                TrackId = trackId,
                Current = current,
                Trail = trail,
                SpeedMps = speed ?? 0,
            });
        }
        return result;
    }

    private DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(TickIntervalMs) };
        t.Tick += OnTick;
        return t;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.UtcNow;
        double realMs = (now - _lastTickUtc).TotalMilliseconds;
        _lastTickUtc = now;

        CurrentTime = CurrentTime.AddMilliseconds(realMs * Speed);   // wallclock×배속(드리프트 보정)
        if (CurrentTime >= End)
        {
            CurrentTime = End;
            Pause();
            Frame?.Invoke();
            Completed?.Invoke();
            return;
        }
        Frame?.Invoke();
    }

    // 정렬된 list에서 ObservedAt <= t 인 마지막 index(없으면 -1) — 이진탐색
    private static int UpperBound(List<ITrackPointModel> list, DateTime t)
    {
        int lo = 0, hi = list.Count - 1, ans = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (list[mid].ObservedAt <= t) { ans = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return ans;
    }

    public void Dispose()
    {
        if (_timer is not null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
        _byTrack.Clear();
    }
}

/// <summary>특정 시각의 한 트랙 재생 상태(현재 위치 + 트레일 + 속도).</summary>
public sealed class TrackSnapshot
{
    public int CameraId { get; set; }
    public string TrackId { get; set; } = string.Empty;
    public ITrackPointModel Current { get; set; } = null!;
    public IReadOnlyList<ITrackPointModel> Trail { get; set; } = Array.Empty<ITrackPointModel>();
    public double SpeedMps { get; set; }
}
