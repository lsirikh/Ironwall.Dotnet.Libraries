using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;

/// <summary>장비별 막대의 묶음 — 서버가 묶음마다 다른 계열을 준다.</summary>
public enum OverviewDeviceGroup
{
    /// <summary>제어기 — 센서 탐지 · 장애 · 연결 · 조치(카메라 탐지는 제어기에 속하지 않는다).</summary>
    Controller,
    /// <summary>카메라 — 카메라 탐지만.</summary>
    Camera,
}

/// <summary>
/// 이벤트 개요(T3) — 지금 세 곳에 흩어진 차트 넷을 한 화면에 모은다.
/// </summary>
/// <remarks>
/// <para>정본 window-layout-system-storyboard.html L1041 · L2503-2612.
/// ① 유형별 비중(<c>summary</c>) ② 장비별 이벤트(<c>by_device</c>) ③ 시간대별 추이(<c>trend</c>) ④ 탐지 출처.</para>
/// <para>원천은 <b>기존</b> <c>DataChartPanelViewModel.LastDashboardDto</c> 다 — 새 전송 경로를 만들지 않는다(PRD NFR-02).</para>
/// <para>추이 차트 위를 좌우로 끌면 기간이 바뀐다 — 판정은 <see cref="EventTrendRangeMath"/>(순수).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class EventOverviewViewModel : PropertyChangedBase
{
    private const double PlotPaddingLeft = 40;
    private const double PlotPaddingRight = 12;
    private const double PlotPaddingTop = 10;
    private const double PlotPaddingBottom = 22;

    private bool _isLoading;
    private int _total;
    private int _days = 1;
    private string _rangeText = string.Empty;
    private int _activeSensors;
    private int _activeCameras;
    private int _activeControllers;
    private OverviewDeviceGroup _deviceGroup = OverviewDeviceGroup.Controller;
    private double _plotWidth = 640;
    private double _plotHeight = 200;
    private double _bandLeft;
    private double _bandWidth;
    private bool _isBandVisible;
    private string _bandLabel = string.Empty;
    private DateTime _start = DateTime.Today;
    private DateTime _end = DateTime.Today.AddDays(1);
    private IReadOnlyList<int> _bucketMax = Array.Empty<int>();

    public EventOverviewViewModel()
    {
        Slices = new ObservableCollection<EventSliceViewModel>();
        Bars = new ObservableCollection<EventDeviceBarViewModel>();
        TrendSeries = new ObservableCollection<EventTrendSeriesViewModel>();
        TrendLabels = new ObservableCollection<EventTrendLabel>();
    }

    /// <summary>막대 · 조각을 눌렀다 — 그 장비/계열의 내역으로 내려간다(레일 전환 + 검색어).</summary>
    public event Action<string, string>? DrillRequested;

    /// <summary>추이 차트에서 기간을 끌어 골랐다 — 통계 재조회 1회.</summary>
    public event Action<TrendRange>? RangeSelected;

    #region - 머리 -
    public bool IsLoading { get => _isLoading; set { _isLoading = value; NotifyOfPropertyChange(); } }
    public int Total { get => _total; private set { _total = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(TotalText)); } }
    public string TotalText => Total.ToString("N0", CultureInfo.InvariantCulture);
    public int Days { get => _days; private set { _days = value; NotifyOfPropertyChange(); } }
    public string RangeText { get => _rangeText; private set { _rangeText = value; NotifyOfPropertyChange(); } }
    public int ActiveSensors { get => _activeSensors; private set { _activeSensors = value; NotifyOfPropertyChange(); } }
    public int ActiveCameras { get => _activeCameras; private set { _activeCameras = value; NotifyOfPropertyChange(); } }
    public int ActiveControllers { get => _activeControllers; private set { _activeControllers = value; NotifyOfPropertyChange(); } }

    /// <summary>운영 이벤트는 총계에서 빠진다는 서버 규칙을 화면에 그대로 적는다.</summary>
    public string TotalNote => $"총계는 5종 합(센서 탐지 · 카메라 탐지 · 장애 · 연결 · 조치)입니다 · 조회 기간 {Days}일";
    #endregion

    #region - ① 유형별 비중 -
    public ObservableCollection<EventSliceViewModel> Slices { get; }

    /// <summary>탐지 묶음(센서 + 카메라) 합계.</summary>
    public int DetectionTotal => Slices.Where(s => s.Spec.Key is "sensor" or "camera").Sum(s => s.Count);
    public string DetectionPercentText => Total > 0 ? $"{Math.Round(DetectionTotal * 100.0 / Total)}%" : "—";
    #endregion

    #region - ② 장비별 이벤트 -
    public ObservableCollection<EventDeviceBarViewModel> Bars { get; }

    public OverviewDeviceGroup DeviceGroup
    {
        get => _deviceGroup;
        set
        {
            if (_deviceGroup == value) return;
            _deviceGroup = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsControllerGroup));
            NotifyOfPropertyChange(nameof(IsCameraGroup));
            NotifyOfPropertyChange(nameof(DeviceGroupNote));
            RebuildBars();
        }
    }

    public bool IsControllerGroup => _deviceGroup == OverviewDeviceGroup.Controller;
    public bool IsCameraGroup => _deviceGroup == OverviewDeviceGroup.Camera;

    public string DeviceGroupNote => _deviceGroup == OverviewDeviceGroup.Controller
        ? "제어기 막대의 탐지는 센서 탐지만 — 카메라 탐지는 제어기에 속하지 않아 카메라 탭에서 봅니다 · 막대를 누르면 그 장비의 내역"
        : "카메라 묶음이 받는 계열은 카메라 탐지 하나입니다 · 막대를 누르면 그 장비의 내역";

    public bool HasBars => Bars.Count > 0;

    public void Drill(EventDeviceBarViewModel? bar)
    {
        if (bar is null) return;
        DrillRequested?.Invoke(IsCameraGroup ? "det" : "det", bar.Name);
    }
    #endregion

    #region - ③ 시간대별 추이 -
    public ObservableCollection<EventTrendSeriesViewModel> TrendSeries { get; }
    public ObservableCollection<EventTrendLabel> TrendLabels { get; }

    public string IntervalText => Bucket == TimeSpan.FromHours(1) ? "1시간 단위" : "1일 단위";

    /// <summary>한 칸의 폭 — 끌어 고른 기간이 여기에 맞춰진다.</summary>
    public TimeSpan Bucket { get; private set; } = TimeSpan.FromHours(1);

    /// <summary>플롯 왼쪽(뷰가 알려 준 크기 기준).</summary>
    public double PlotLeft => PlotPaddingLeft;
    public double PlotTop => PlotPaddingTop;
    public double PlotWidth => Math.Max(0, _plotWidth - PlotPaddingLeft - PlotPaddingRight);
    public double PlotHeight => Math.Max(0, _plotHeight - PlotPaddingTop - PlotPaddingBottom);

    /// <summary>뷰가 크기를 알려 준다 — 고정 치수를 뷰모델에 박지 않는다.</summary>
    public void Resize(double width, double height)
    {
        if (Math.Abs(_plotWidth - width) < 0.5 && Math.Abs(_plotHeight - height) < 0.5) return;
        _plotWidth = width;
        _plotHeight = height;
        NotifyOfPropertyChange(nameof(PlotWidth));
        NotifyOfPropertyChange(nameof(PlotHeight));
        RebuildTrendGeometry();
    }

    public double BandLeft { get => _bandLeft; private set { _bandLeft = value; NotifyOfPropertyChange(); } }
    public double BandWidth { get => _bandWidth; private set { _bandWidth = value; NotifyOfPropertyChange(); } }
    public bool IsBandVisible { get => _isBandVisible; private set { _isBandVisible = value; NotifyOfPropertyChange(); } }
    public string BandLabel { get => _bandLabel; private set { _bandLabel = value; NotifyOfPropertyChange(); } }

    /// <summary>끄는 동안 — 띠와 라벨을 갱신한다(서버 미호출).</summary>
    public void UpdateBand(double xPress, double xCurrent)
    {
        var (left, width) = EventTrendRangeMath.BandOf(xPress, xCurrent, PlotLeft, PlotWidth);
        BandLeft = left;
        BandWidth = width;
        IsBandVisible = width > 0;
        BandLabel = EventTrendRangeMath
            .Resolve(xPress, xCurrent, PlotLeft, PlotWidth, _start, _end, Bucket)
            .Label();
    }

    /// <summary>띠를 지운다 — 취소(ESC) · 커밋 뒤 양쪽에서 부른다.</summary>
    public void ClearBand()
    {
        IsBandVisible = false;
        BandWidth = 0;
        BandLabel = string.Empty;
    }

    /// <summary>놓았다 — 한 버킷보다 넓으면 기간을 바꾼다. 좁으면 아무 일도 없다(클릭).</summary>
    public TrendRange CommitBand(double xPress, double xRelease)
    {
        var range = EventTrendRangeMath.Resolve(xPress, xRelease, PlotLeft, PlotWidth, _start, _end, Bucket);
        ClearBand();
        if (range.IsCommittable) RangeSelected?.Invoke(range);
        return range;
    }
    #endregion

    #region - ④ 탐지 출처 -
    public int SensorDetection { get; private set; }
    public int CameraDetection { get; private set; }
    public double SensorDailyAverage { get; private set; }
    public double CameraDailyAverage { get; private set; }

    public double SensorShare => DetectionTotal > 0 ? SensorDetection * 100.0 / DetectionTotal : 0;
    public double CameraShare => DetectionTotal > 0 ? CameraDetection * 100.0 / DetectionTotal : 0;
    public string SensorShareText => $"센서 {SensorShare:0}%";
    public string CameraShareText => $"카메라 {CameraShare:0}%";
    #endregion

    /// <summary>레일 배지가 목록을 아직 불러오지 못했을 때 쓸 서버 요약 건수(종류 키 → 건수).</summary>
    public IReadOnlyDictionary<string, int> SummaryCounts { get; private set; } = new Dictionary<string, int>();

    /// <summary>서버 요약을 화면 모양으로 옮긴다. DTO 가 null 이면 빈 화면(예외 없음).</summary>
    public void Load(EventDashboardDto? dashboard, DateTime start, DateTime end)
    {
        _start = start;
        _end = end > start ? end : start.AddHours(1);
        Bucket = EventTrendRangeMath.BucketFor(_start, _end);
        NotifyOfPropertyChange(nameof(IntervalText));

        _dashboard = dashboard;
        var summary = dashboard?.Summary ?? new EventSummaryDto();

        SensorDetection = summary.SensorDetection;
        CameraDetection = summary.CameraDetection;
        SensorDailyAverage = summary.DailyAverages?.SensorDetection ?? 0;
        CameraDailyAverage = summary.DailyAverages?.CameraDetection ?? 0;

        Days = Math.Max(1, summary.DaysInRange);
        RangeText = $"{_start:MM-dd HH:mm} ~ {_end:MM-dd HH:mm}";
        ActiveSensors = summary.ActiveDevices?.Sensors ?? 0;
        ActiveCameras = summary.ActiveDevices?.Cameras ?? 0;
        ActiveControllers = summary.ActiveDevices?.Controllers ?? 0;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["sensor"] = summary.SensorDetection,
            ["camera"] = summary.CameraDetection,
            ["mal"] = summary.Malfunction,
            ["con"] = summary.Connection,
            ["act"] = summary.Action,
        };
        // 서버가 total 을 주지만, 켜고 끄기와 맞물리려면 화면이 스스로 더한 값이 정본이다.
        Total = counts.Values.Sum();
        SummaryCounts = new Dictionary<string, int>(counts, StringComparer.Ordinal);

        var on = Slices.ToDictionary(s => s.Spec.Key, s => s.IsOn, StringComparer.Ordinal);
        Slices.Clear();
        foreach (var spec in EventSeriesSpec.All)
        {
            var count = counts.TryGetValue(spec.Key, out var c) ? c : 0;
            var slice = new EventSliceViewModel(spec, count, Total > 0 ? count * 100.0 / Total : 0);
            if (on.TryGetValue(spec.Key, out var wasOn)) slice.IsOn = wasOn;
            slice.PropertyChanged += OnSliceToggled;
            Slices.Add(slice);
        }
        NotifyOfPropertyChange(nameof(SensorDetection));
        NotifyOfPropertyChange(nameof(CameraDetection));
        NotifyOfPropertyChange(nameof(SensorDailyAverage));
        NotifyOfPropertyChange(nameof(CameraDailyAverage));
        NotifyOfPropertyChange(nameof(SensorShare));
        NotifyOfPropertyChange(nameof(CameraShare));
        NotifyOfPropertyChange(nameof(DetectionTotal));
        NotifyOfPropertyChange(nameof(DetectionPercentText));
        NotifyOfPropertyChange(nameof(SensorShareText));
        NotifyOfPropertyChange(nameof(CameraShareText));
        NotifyOfPropertyChange(nameof(TotalNote));

        RebuildBars();
        RebuildTrend();
    }

    private EventDashboardDto? _dashboard;

    private void OnSliceToggled(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EventSliceViewModel.IsOn)) return;
        RebuildBars();
        RebuildTrend();
    }

    private bool IsOn(string key) => Slices.FirstOrDefault(s => s.Spec.Key == key)?.IsOn ?? true;

    private void RebuildBars()
    {
        Bars.Clear();
        var byDevice = _dashboard?.ByDevice;
        if (byDevice is null) { NotifyOfPropertyChange(nameof(HasBars)); return; }

        var rows = new List<EventDeviceBarViewModel>();
        if (_deviceGroup == OverviewDeviceGroup.Controller)
        {
            foreach (var controller in byDevice.Controllers ?? new List<ControllerStatsDto>())
            {
                var values = new List<(EventSeriesSpec Spec, int Value)>
                {
                    (EventSeriesSpec.Sensor, IsOn("sensor") ? controller.SensorDetection : 0),
                    (EventSeriesSpec.Malfunction, IsOn("mal") ? controller.Malfunction : 0),
                    (EventSeriesSpec.Connection, IsOn("con") ? controller.Connection : 0),
                    (EventSeriesSpec.Action, IsOn("act") ? controller.Action : 0),
                };
                rows.Add(Bar(controller.ControllerName, values));
            }
        }
        else
        {
            foreach (var camera in byDevice.Cameras ?? new List<CameraStatsDto>())
            {
                var values = new List<(EventSeriesSpec, int)>
                {
                    (EventSeriesSpec.Camera, IsOn("camera") ? camera.CameraDetection : 0),
                };
                rows.Add(Bar(camera.CameraName, values));
            }
        }

        foreach (var row in rows.OrderByDescending(r => r.Total).Take(8)) Bars.Add(row);
        NotifyOfPropertyChange(nameof(HasBars));

        static EventDeviceBarViewModel Bar(string name, IReadOnlyList<(EventSeriesSpec Spec, int Value)> values)
        {
            var total = values.Sum(v => v.Value);
            var max = Math.Max(1, total);
            var segments = values
                .Where(v => v.Value > 0)
                .Select(v => new EventBarSegment(v.Spec, v.Value, v.Value / (double)max))
                .ToList();
            return new EventDeviceBarViewModel(name, name, total, segments);
        }
    }

    private void RebuildTrend()
    {
        TrendSeries.Clear();
        TrendLabels.Clear();

        var buckets = _dashboard?.Trend?.Series ?? new List<EventTrendItemDto>();
        if (buckets.Count == 0) { _bucketMax = Array.Empty<int>(); RebuildTrendGeometry(); return; }

        void Add(EventSeriesSpec spec, Func<EventTrendItemDto, int> pick)
        {
            if (!IsOn(spec.Key)) return;
            TrendSeries.Add(new EventTrendSeriesViewModel(spec, buckets.Select(pick).ToList()));
        }

        Add(EventSeriesSpec.Sensor, b => b.SensorDetection);
        Add(EventSeriesSpec.Camera, b => b.CameraDetection);
        Add(EventSeriesSpec.Malfunction, b => b.Malfunction);
        Add(EventSeriesSpec.Connection, b => b.Connection);
        Add(EventSeriesSpec.Action, b => b.Action);

        _bucketMax = buckets.Select(b => Math.Max(Math.Max(b.SensorDetection, b.CameraDetection),
                                                  Math.Max(b.Malfunction, Math.Max(b.Connection, b.Action)))).ToList();

        var labelStep = Math.Max(1, buckets.Count / 6);
        for (var i = 0; i < buckets.Count; i += labelStep)
            TrendLabels.Add(new EventTrendLabel(0, ShortLabel(buckets[i].TimeBucket)));

        RebuildTrendGeometry();
    }

    private static string ShortLabel(string timeBucket)
    {
        if (DateTime.TryParse(timeBucket, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
            return when.Hour == 0 && when.Minute == 0 ? when.ToString("MM-dd") : when.ToString("HH시");
        return timeBucket.Length > 5 ? timeBucket[^5..] : timeBucket;
    }

    private void RebuildTrendGeometry()
    {
        var width = PlotWidth;
        var height = PlotHeight;
        if (width <= 0 || height <= 0) return;

        var count = TrendSeries.FirstOrDefault()?.Values.Count ?? 0;
        if (count == 0) return;

        var max = Math.Max(1, TrendSeries.SelectMany(s => s.Values).DefaultIfEmpty(0).Max());
        var step = count > 1 ? width / (count - 1) : 0;

        foreach (var series in TrendSeries)
        {
            var points = new PointCollection();
            for (var i = 0; i < series.Values.Count; i++)
                points.Add(new Point(PlotLeft + i * step, PlotTop + height * (1 - series.Values[i] / (double)max)));
            series.Points = points;

            if (series.Spec.Key == "sensor")
            {
                var area = new PointCollection { new(PlotLeft, PlotTop + height) };
                foreach (var p in points) area.Add(p);
                area.Add(new Point(PlotLeft + (count - 1) * step, PlotTop + height));
                series.Area = area;
            }
        }

        var labelStep = Math.Max(1, count / 6);
        for (var i = 0; i < TrendLabels.Count; i++)
            TrendLabels[i] = TrendLabels[i] with { X = PlotLeft + Math.Min(count - 1, i * labelStep) * step };

        NotifyOfPropertyChange(nameof(TrendSeries));
    }
}
