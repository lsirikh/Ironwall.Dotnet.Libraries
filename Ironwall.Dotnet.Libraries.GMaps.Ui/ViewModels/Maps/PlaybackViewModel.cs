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
    private readonly TrackPointStore _store;
    private readonly PlaybackOverlayManager _overlay;
    private readonly ITrackingSetupModel? _setup;
    private readonly ILogService? _log;
    private bool _suppressSeek;

    /// <summary>트랙 클릭 시 지도 센터링 요청(MapViewModel이 처리).</summary>
    public event System.Action<double, double>? FocusRequested;
    /// <summary>콘솔 닫기 요청(MapViewModel이 패널 숨김).</summary>
    public event System.Action? CloseRequested;

    public PlaybackViewModel(PlaybackEngine engine, TrackPointStore store, PlaybackOverlayManager overlay,
                             ITrackingSetupModel? setup = null, ILogService? log = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _setup = setup;
        _log = log;

        _engine.Frame += OnFrame;
        _engine.Completed += OnCompleted;

        ToTime = DateTime.Now;
        FromTime = ToTime.AddMinutes(-30);

        LoadCommand = new AsyncRelayCommand(_ => LoadAsync());
        PresetCommand = new AsyncRelayCommand(p => LoadPresetAsync(p));
        PlayPauseCommand = new RelayCommand(_ => TogglePlay());
        StopCommand = new RelayCommand(_ => _engine.Stop());
        SpeedCommand = new RelayCommand(p => SetSpeed(p));
        FocusCommand = new RelayCommand(p => Focus(p as PlaybackTrackItem));
        CloseCommand = new RelayCommand(_ => Close());
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
    public DateTime FromTime { get; set; }
    public DateTime ToTime { get; set; }

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
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand SpeedCommand { get; }          // 파라미터: 배속(string)
    public ICommand FocusCommand { get; }
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

        var points = await _store.FetchAsync(null, fromUtc, toUtc).ConfigureAwait(true);
        _engine.Load(points);
        BuildEvents(points);
        _overlay.SetEnabledTracks(EnabledSet());
        HasData = points.Count > 0;
        Status = HasData ? $"{points.Count}점 · {Events.Count}트랙 로드" : "해당 기간 데이터 없음";
        _log?.Info($"[Playback] 로드 {points.Count}점 / {Events.Count}트랙 ({FromTime:HH:mm}~{ToTime:HH:mm})");
        UpdateTimeLabel();
    }

    private void BuildEvents(IReadOnlyList<ITrackPointModel> points)
    {
        foreach (var it in Events) it.PropertyChanged -= OnTrackToggle;
        Events.Clear();
        foreach (var g in points.GroupBy(p => p.TrackId))
        {
            var list = g.OrderBy(p => p.ObservedAt).ToList();
            var last = list[^1];
            var item = new PlaybackTrackItem
            {
                TrackId = g.Key,
                CameraId = last.CameraId,
                Label = last.Label ?? "",
                ThreatLevel = last.ThreatLevel ?? "",
                Start = list[0].ObservedAt.ToLocalTime(),
                End = last.ObservedAt.ToLocalTime(),
                Count = list.Count,
                IsEnabled = true,
            };
            item.PropertyChanged += OnTrackToggle;
            Events.Add(item);
        }
    }

    private void OnTrackToggle(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackTrackItem.IsEnabled))
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
        var snap = _engine.SnapshotAt(_engine.CurrentTime).FirstOrDefault(x => x.TrackId == item.TrackId);
        if (snap is not null)
            FocusRequested?.Invoke(snap.Current.Latitude, snap.Current.Longitude);
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
        foreach (var it in Events) it.PropertyChanged -= OnTrackToggle;
    }
}

/// <summary>Playback 이벤트 리스트 항목(트랙 요약 + 체크박스 표시상태).</summary>
public sealed class PlaybackTrackItem : PropertyChangedBase
{
    public string TrackId { get; set; } = string.Empty;
    public int CameraId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ThreatLevel { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Count { get; set; }

    private bool _isEnabled = true;
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; NotifyOfPropertyChange(); } }

    public string TypeGlyph => TrackingEnumExtensions.ParseTargetType(Label) switch
    {
        EnumTargetType.Person => "🚶",
        EnumTargetType.Vehicle => "🚗",
        EnumTargetType.Animal => "🐾",
        _ => "❓",
    };

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
