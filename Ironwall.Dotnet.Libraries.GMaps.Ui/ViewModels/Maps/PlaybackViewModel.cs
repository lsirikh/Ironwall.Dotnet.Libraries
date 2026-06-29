using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using Caliburn.Micro;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : Playback 콘솔 ViewModel — 기간선택·이벤트리스트·재생컨트롤
   Created By   : GHLee
   Created On   : 2026-06-26
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 추적 Playback 콘솔(오버레이 윈도우)의 VM. 로컬 DB에서 기간 조회 → <see cref="PlaybackEngine"/> 재생 →
/// <see cref="PlaybackOverlayManager"/> 가 같은 지도에 격리 렌더.
/// <para>기간 = 프리셋(최근 N분)+절대범위+MaxPlaybackHours 가드 · 멀티타겟 단일 타임라인 · 이벤트리스트=체크박스 필터+클릭 포커스.</para>
/// </summary>
public sealed class PlaybackViewModel : PropertyChangedBase, IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly ITrackPointReader _reader;   // 로컬/서버 교체 seam(분석 §1.1)
    private readonly PlaybackOverlayManager _overlay;
    private readonly ITrackingSetupModel? _setup;
    private readonly ILogService? _log;
    private bool _suppressSeek;

    /// <summary>트랙 클릭 시 지도 센터링 요청(MapViewModel이 처리).</summary>
    public event System.Action<double, double>? FocusRequested;
    /// <summary>콘솔 닫기 요청(MapViewModel이 패널 숨김).</summary>
    public event System.Action? CloseRequested;

    public PlaybackViewModel(PlaybackEngine engine, ITrackPointReader reader, PlaybackOverlayManager overlay,
                             ITrackingSetupModel? setup = null, ILogService? log = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _setup = setup;
        _log = log;

        _engine.Frame += OnFrame;
        _engine.Completed += OnCompleted;

        ToTime = DateTime.Now;
        FromTime = ToTime.AddMinutes(-30);

        Calendar = new RangeCalendarViewModel();
        Calendar.RangeCompleted += OnCalendarRange;

        LoadCommand = new AsyncRelayCommand(_ => LoadAsync());
        PresetCommand = new AsyncRelayCommand(p => LoadPresetAsync(p));
        ToggleCalendarCommand = new RelayCommand(_ => ToggleCalendar());
        PlayPauseCommand = new RelayCommand(_ => TogglePlay());
        StopCommand = new RelayCommand(_ => _engine.Stop());
        SpeedCommand = new RelayCommand(p => SetSpeed(p));
        FocusCommand = new RelayCommand(p => Focus(p as PlaybackTrackItem));
        JumpToSessionCommand = new RelayCommand(p => JumpToSession(p as PlaybackTrackItem));
        CloseCommand = new RelayCommand(_ => Close());
    }

    /// <summary>달력 팝업 토글 — 열 때 현재 From/To로 달력을 동기화한다.</summary>
    private void ToggleCalendar()
    {
        if (!IsCalendarOpen) Calendar.Sync(FromTime, ToTime);
        IsCalendarOpen = !IsCalendarOpen;
    }

    /// <summary>달력 2클릭 완료 — 시작일 00:00 ~ 종료일 23:59:59로 기간 설정 후 팝업 닫기.</summary>
    private void OnCalendarRange(DateTime start, DateTime end)
    {
        FromTime = start.Date;                              // 00:00:00
        ToTime = end.Date.AddDays(1).AddSeconds(-1);        // 23:59:59 (종료일 끝)
        IsCalendarOpen = false;
        // MaxPlaybackHours 가드가 종일/다일 범위를 끝 N시간으로 클램프 → [불러오기] 전에 사전 안내
        int maxH = _setup?.MaxPlaybackHours ?? 6;
        Status = (ToTime - FromTime).TotalHours > maxH
            ? $"{start:yyyy-MM-dd}~{end:yyyy-MM-dd} 선택 · ⚠ 최대 {maxH}시간만 재생됩니다(끝 구간) · [불러오기]"
            : $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd} 선택됨 · [불러오기]를 누르세요";
    }

    /// <summary>콘솔 닫기 — 재생 정지 + 재생 마커 제거 후 닫기 요청.</summary>
    public void Close()
    {
        _engine.Stop();
        _overlay.ClearAll();
        IsPlaying = false;
        CloseRequested?.Invoke();
    }

    /// <summary>MapViewModel이 MainMap 준비 후 호출 — 재생 오버레이를 지도에 연결.</summary>
    public void AttachMap(GMapControl map) => _overlay.Attach(map);

    #region - Bindable State -
    private DateTime _fromTime;
    public DateTime FromTime { get => _fromTime; set { _fromTime = value; NotifyOfPropertyChange(); } }

    private DateTime _toTime;
    public DateTime ToTime { get => _toTime; set { _toTime = value; NotifyOfPropertyChange(); } }

    /// <summary>기간 선택 캘린더(2클릭 시작/끝). 팝업 토글은 <see cref="IsCalendarOpen"/>.</summary>
    public RangeCalendarViewModel Calendar { get; }

    private bool _isCalendarOpen;
    /// <summary>달력 팝업 표시 여부.</summary>
    public bool IsCalendarOpen { get => _isCalendarOpen; set { _isCalendarOpen = value; NotifyOfPropertyChange(); } }

    public ObservableCollection<PlaybackTrackItem> Events { get; } = new();

    private string _status = "기간을 선택하고 [불러오기]";
    public string Status { get => _status; set { _status = value; NotifyOfPropertyChange(); } }

    private bool _hasData;
    public bool HasData { get => _hasData; set { _hasData = value; NotifyOfPropertyChange(); } }

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; set { _isPlaying = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(PlayPauseGlyph)); } }
    public string PlayPauseGlyph => _isPlaying ? "⏸" : "▶";

    private double _progress;   // 0~100 (슬라이더)
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value; NotifyOfPropertyChange();
            if (!_suppressSeek) _engine.SeekToProgress(value / 100.0);
        }
    }

    private double _speed = 1.0;
    public double Speed { get => _speed; set { _speed = value; NotifyOfPropertyChange(); } }

    private string _timeLabel = "--:--:-- / --:--:--";
    public string TimeLabel { get => _timeLabel; set { _timeLabel = value; NotifyOfPropertyChange(); } }
    #endregion

    #region - Commands -
    public ICommand LoadCommand { get; }
    public ICommand PresetCommand { get; }        // 파라미터: 분(string) / "today"
    public ICommand ToggleCalendarCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SpeedCommand { get; }          // 파라미터: 배속(string)
    public ICommand FocusCommand { get; }
    public ICommand JumpToSessionCommand { get; }
    public ICommand CloseCommand { get; }
    #endregion

    private async Task LoadPresetAsync(object? p)
    {
        ToTime = DateTime.Now;
        FromTime = (p?.ToString()) switch
        {
            "today" => DateTime.Today,
            var s when int.TryParse(s, out var m) => ToTime.AddMinutes(-m),
            _ => ToTime.AddMinutes(-30),
        };
        NotifyOfPropertyChange(nameof(FromTime));
        NotifyOfPropertyChange(nameof(ToTime));
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var fromUtc = FromTime.ToUniversalTime();
        var toUtc = ToTime.ToUniversalTime();
        if (toUtc <= fromUtc) { Status = "종료 시각이 시작보다 빨라야 합니다."; return; }

        // MaxPlaybackHours 가드 — 초과 시 시작을 당겨 제한
        int maxH = _setup?.MaxPlaybackHours ?? 6;
        if ((toUtc - fromUtc).TotalHours > maxH)
        {
            fromUtc = toUtc.AddHours(-maxH);
            FromTime = fromUtc.ToLocalTime();
            NotifyOfPropertyChange(nameof(FromTime));
            Status = $"최대 {maxH}시간으로 제한됨";
        }

        // 설정(트레일 길이·gap 분절)을 재생 엔진에 반영 — 라이브와 동일 기준
        if (_setup is not null)
        {
            _engine.TrailMaxPoints = _setup.TrailMaxPoints;
            _engine.GapThresholdSec = _setup.GapThresholdSec;
        }

        var points = await _reader.FetchAsync(null, fromUtc, toUtc).ConfigureAwait(true);
        _engine.Load(points);
        BuildEvents(points);
        _overlay.SetEnabledTracks(EnabledSet());
        HasData = points.Count > 0;
        Status = HasData ? $"{points.Count}점 · {Events.Count}건 로드" : "해당 기간 데이터 없음";
        _log?.Info($"[Playback] 로드 {points.Count}점 / {Events.Count}건 ({FromTime:HH:mm}~{ToTime:HH:mm})");
        UpdateTimeLabel();
    }

    private void BuildEvents(IReadOnlyList<ITrackPointModel> points)
    {
        foreach (var it in Events) it.PropertyChanged -= OnTrackToggle;
        Events.Clear();

        // 같은 track_id라도 시간 공백(gap)이 크면 별개 탐지 세션으로 분절 → 각 세션이 한 행
        // 트레일 분절(GapThresholdSec, 기본 10s)과 분리: 짧은 프레임드랍엔 안 쪼개고 '새 출현'만 분리
        const double gapSec = 30;
        var built = new List<PlaybackTrackItem>();
        foreach (var g in points.GroupBy(p => p.TrackId))
        {
            var list = g.OrderBy(p => p.ObservedAt).ToList();
            var times = list.Select(p => p.ObservedAt).ToList();
            var segments = TrackSessionSplitter.SplitByGap(times, gapSec);
            bool multi = segments.Count > 1;
            for (int si = 0; si < segments.Count; si++)
            {
                var (start, count) = segments[si];
                var seg = list.GetRange(start, count);
                var last = seg[^1];
                built.Add(new PlaybackTrackItem
                {
                    TrackId = g.Key,
                    SessionId = multi ? $"{g.Key}#{si + 1}" : g.Key,
                    CameraId = last.CameraId,
                    Label = last.Label ?? "",
                    ThreatLevel = last.ThreatLevel ?? "",
                    Start = seg[0].ObservedAt.ToLocalTime(),
                    StartUtc = seg[0].ObservedAt,   // 시크용(엔진 observed_at 도메인과 동일)
                    End = last.ObservedAt.ToLocalTime(),
                    Count = count,
                    Latitude = last.Latitude,       // 세션 대표 좌표(마지막 관측) — 따라보기 센터링용
                    Longitude = last.Longitude,
                    IsEnabled = true,
                });
            }
        }

        // 시작 시각 오름차순으로 보기 좋게 정렬
        foreach (var item in built.OrderBy(x => x.Start))
        {
            item.PropertyChanged += OnTrackToggle;
            Events.Add(item);
        }
    }

    private bool _syncingToggle;
    private void OnTrackToggle(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaybackTrackItem.IsEnabled)) return;
        if (_syncingToggle) return;
        // 오버레이는 track 단위 키라 세션 단위로 못 숨김 → 같은 track의 여러 세션 행을 함께 토글(일관성)
        if (sender is PlaybackTrackItem changed)
        {
            _syncingToggle = true;
            foreach (var it in Events.Where(x => x.TrackId == changed.TrackId && x.IsEnabled != changed.IsEnabled))
                it.IsEnabled = changed.IsEnabled;
            _syncingToggle = false;
        }
        _overlay.SetEnabledTracks(EnabledSet());
    }

    private HashSet<string> EnabledSet()
        => Events.Where(x => x.IsEnabled).Select(x => x.TrackId).ToHashSet();

    private void TogglePlay()
    {
        if (!HasData) return;
        if (_engine.IsPlaying) _engine.Pause(); else _engine.Play();
        IsPlaying = _engine.IsPlaying;
    }

    private void SetSpeed(object? p)
    {
        if (double.TryParse(p?.ToString(), out var s)) { _engine.SetSpeed(s); Speed = s; }
    }

    private void Focus(PlaybackTrackItem? item)
    {
        if (item is null) return;
        // 세션 대표 좌표로 직접 센터링 — 재생 위치가 이 세션 구간 밖(또는 정지)이어도 동작
        FocusRequested?.Invoke(item.Latitude, item.Longitude);
    }

    /// <summary>세션 행 더블클릭 — 그 세션 시작 시각으로 시크 + 지도 센터링 + 재생 시작.</summary>
    private void JumpToSession(PlaybackTrackItem? item)
    {
        if (item is null || !HasData) return;
        _engine.SeekTo(item.StartUtc);                          // observed_at 도메인 직접 시크(Frame→슬라이더/라벨 갱신)
        FocusRequested?.Invoke(item.Latitude, item.Longitude);  // 지도 센터링
        _engine.Play();
        IsPlaying = _engine.IsPlaying;
    }

    private void OnFrame()
    {
        DispatcherService.Invoke(() =>
        {
            _suppressSeek = true;
            Progress = Math.Round(_engine.Progress * 100.0, 2);   // 부동소수 찌꺼기 제거
            _suppressSeek = false;
            IsPlaying = _engine.IsPlaying;
            UpdateTimeLabel();
        });
    }

    private void OnCompleted() => DispatcherService.Invoke(() => IsPlaying = false);

    private void UpdateTimeLabel()
        => TimeLabel = $"{_engine.CurrentTime.ToLocalTime():HH:mm:ss} / {_engine.End.ToLocalTime():HH:mm:ss}";

    public void Dispose()
    {
        _engine.Frame -= OnFrame;
        _engine.Completed -= OnCompleted;
        Calendar.RangeCompleted -= OnCalendarRange;
        foreach (var it in Events) it.PropertyChanged -= OnTrackToggle;
    }
}

/// <summary>Playback 탐지 세션 리스트 항목(세션 단위 요약 + 표시 토글). DataGrid 컬럼에 바인딩.</summary>
public sealed class PlaybackTrackItem : PropertyChangedBase
{
    public string TrackId { get; set; } = string.Empty;
    /// <summary>화면 표시용 탐지 ID. 한 track이 여러 세션으로 분절되면 <c>{TrackId}#n</c>.</summary>
    public string SessionId { get; set; } = string.Empty;
    public int CameraId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ThreatLevel { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    /// <summary>세션 시작 시각(UTC observed_at 도메인) — 더블클릭 시크용.</summary>
    public DateTime StartUtc { get; set; }
    public DateTime End { get; set; }
    public int Count { get; set; }
    /// <summary>세션 대표 좌표(따라보기 센터링용).</summary>
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; NotifyOfPropertyChange(); } }

    private EnumTargetType TargetType => TrackingEnumExtensions.ParseTargetType(Label);

    public string TypeGlyph => TargetType switch
    {
        EnumTargetType.Person => "🚶",
        EnumTargetType.Vehicle => "🚗",
        EnumTargetType.Animal => "🐾",
        _ => "❓",
    };

    /// <summary>탐지 타입(한글). DataGrid "타입" 컬럼.</summary>
    public string TypeText => TargetType switch
    {
        EnumTargetType.Person => "사람",
        EnumTargetType.Vehicle => "차량",
        EnumTargetType.Animal => "동물",
        _ => "기타",
    };

    /// <summary>글리프 + 한글 타입(예: 🐾 동물).</summary>
    public string TypeDisplay => $"{TypeGlyph} {TypeText}";

    /// <summary>탐지 등급(한글). DataGrid "등급" 컬럼.</summary>
    public string ThreatText => TrackingEnumExtensions.ParseThreatLevel(ThreatLevel).ToColorType() switch
    {
        EnumColorType.Red => "위험",
        EnumColorType.Orange => "주의",
        EnumColorType.Green => "일반",
        _ => string.IsNullOrWhiteSpace(ThreatLevel) ? "일반" : ThreatLevel,
    };

    /// <summary>탐지 시간 시작~끝. DataGrid "시작~끝" 컬럼. 같은 날이면 날짜 생략(폭 절약).</summary>
    public string TimeRange => Start.Date == End.Date
        ? $"{Start:HH:mm:ss} ~ {End:HH:mm:ss}"
        : $"{Start:MM-dd HH:mm} ~ {End:MM-dd HH:mm}";

    public string Span => $"{Start:HH:mm:ss}–{End:HH:mm:ss} · {Count}pt";

    public Brush ThreatBrush
    {
        get
        {
            var c = TrackingEnumExtensions.ParseThreatLevel(ThreatLevel).ToColorType();
            var color = c switch
            {
                EnumColorType.Green => Color.FromRgb(0x2E, 0xCC, 0x71),
                EnumColorType.Orange => Color.FromRgb(0xF3, 0x9C, 0x12),
                EnumColorType.Red => Color.FromRgb(0xE7, 0x4C, 0x3C),
                _ => Color.FromRgb(0x95, 0xA5, 0xA6),
            };
            var b = new SolidColorBrush(color); b.Freeze(); return b;
        }
    }
}
