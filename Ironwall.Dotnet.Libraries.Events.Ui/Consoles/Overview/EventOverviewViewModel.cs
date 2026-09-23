using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
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
    /// <summary>카메라 — 카메라 탐지 · 사전 경보.</summary>
    Camera,
    /// <summary>함체 · 통문 — 운영 이벤트(문 개폐 · 환경 경보)가 실리는 유일한 장비별 자리(서버 <c>by_device.enclosures/gates</c>).</summary>
    Facility,
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
    private bool _isLoading;
    private int _total;
    private int _days = 1;
    private string _rangeText = string.Empty;
    private int _activeSensors;
    private int _activeCameras;
    private int _activeControllers;
    private OverviewDeviceGroup _deviceGroup = OverviewDeviceGroup.Controller;
    // 차트가 첫 측정을 보내기 전(Loaded 직후 아주 짧은 창)에만 쓰는 잠정값 — 더 이상 DrawMargin 을
    // 박지 않으므로 이 숫자가 실제 여백과 같아야 할 이유가 없다(전엔 같아야 했던 게 결함의 근원이었다).
    // PlotWidth<=0 이면 EventTrendRangeMath 가 그냥 커밋하지 않는다(안전 폐쇄) — 첫 측정 전에 드래그를
    // 시작하는 극단적 타이밍이어도 아무 일도 안 일어날 뿐 잘못 커밋되지 않는다.
    private double _plotLeft;
    private double _plotTop;
    private double _plotWidth;
    private double _plotHeight;
    private double _bandLeft;
    private double _bandWidth;
    private bool _isBandVisible;
    private string _bandLabel = string.Empty;
    private DateTime _start = DateTime.Today;
    private DateTime _end = DateTime.Today.AddDays(1);
    private BaseTheme _theme = BaseTheme.Light;
    private IReadOnlyList<string> _xLabels = Array.Empty<string>();

    public EventOverviewViewModel()
    {
        Slices = new ObservableCollection<EventSliceViewModel>();
        Bars = new ObservableCollection<EventDeviceBarViewModel>();
        Series = new ObservableCollection<ISeries>();
        XAxes = new ObservableCollection<Axis>();
        YAxes = new ObservableCollection<Axis>();
        BuildAxes();
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

    /// <summary>운영 이벤트 — 서버가 총계 밖에서 따로 센 건수(<c>summary.operation</c>).</summary>
    public int OperationCount { get; private set; }

    /// <summary>운영 이벤트는 총계에서 빠진다는 서버 규칙을 화면에 그대로 적는다 — 그리고 그 수를 숨기지 않는다.</summary>
    public string TotalNote => $"총계는 6종 합(센서 탐지 · 카메라 탐지 · 사전 경보 · 장애 · 연결 · 조치)입니다 · "
                             + $"운영 {OperationCount:N0}건은 서버 규칙상 총계 밖(별도 집계) · 조회 기간 {Days}일";
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
            NotifyOfPropertyChange(nameof(IsFacilityGroup));
            NotifyOfPropertyChange(nameof(DeviceGroupNote));
            RebuildBars();
        }
    }

    public bool IsControllerGroup => _deviceGroup == OverviewDeviceGroup.Controller;
    public bool IsCameraGroup => _deviceGroup == OverviewDeviceGroup.Camera;
    public bool IsFacilityGroup => _deviceGroup == OverviewDeviceGroup.Facility;

    public string DeviceGroupNote => _deviceGroup switch
    {
        OverviewDeviceGroup.Controller => "제어기 막대의 탐지는 센서 탐지만 — 카메라 탐지는 제어기에 속하지 않아 카메라 탭에서 봅니다 · 막대를 누르면 그 장비의 내역",
        OverviewDeviceGroup.Camera => "카메라 묶음이 받는 계열은 카메라 탐지 · 사전 경보입니다 · 막대를 누르면 그 장비의 내역",
        _ => "함체 · 통문 막대에는 운영 이벤트(문 개폐 · 환경 경보)가 함께 실립니다 — 운영은 총계 밖입니다 · 막대를 누르면 그 장비의 내역",
    };

    public bool HasBars => Bars.Count > 0;

    public void Drill(EventDeviceBarViewModel? bar)
    {
        if (bar is null) return;
        DrillRequested?.Invoke(IsCameraGroup ? "det" : "det", bar.Name);
    }
    #endregion

    #region - ③ 시간대별 추이 -
    /// <summary>LiveChartsCore(SkiaSharpView.WPF) CartesianChart 가 그리는 다섯 계열(DataChartPanelViewModel 과 같은 관용구).</summary>
    public ObservableCollection<ISeries> Series { get; }
    public ObservableCollection<Axis> XAxes { get; }
    public ObservableCollection<Axis> YAxes { get; }

    /// <summary>툴팁 텍스트/배경 — SkiaSharp 페인트는 DynamicResource 에 못 닿아 테마 전환 때 <see cref="ApplyTheme"/> 로 재공급한다.</summary>
    public SolidColorPaint TooltipTextPaint
    {
        get => _tooltipTextPaint;
        private set { _tooltipTextPaint = value; NotifyOfPropertyChange(); }
    }
    private SolidColorPaint _tooltipTextPaint = ChartThemeProvider.TooltipTextPaint(BaseTheme.Light);

    public SolidColorPaint TooltipBackgroundPaint
    {
        get => _tooltipBackgroundPaint;
        private set { _tooltipBackgroundPaint = value; NotifyOfPropertyChange(); }
    }
    private SolidColorPaint _tooltipBackgroundPaint = ChartThemeProvider.TooltipBackgroundPaint(BaseTheme.Light);

    public string IntervalText => Bucket == TimeSpan.FromHours(1) ? "1시간 단위" : "1일 단위";

    /// <summary>
    /// 한 칸의 폭 — <b>서버가 실제로 집계한 단위</b>(<c>trend.interval</c>)가 정본이다. 없거나 모르는 값이면 기간 규칙
    /// (<see cref="EventTrendRangeMath.BucketFor"/>)으로 짐작한다. 예전엔 짐작만 써서 3일 기간이 '1일 단위'라고 적힌 채
    /// 서버의 시간 칸을 그렸다(실서버 왕복 E3f).
    /// </summary>
    private static TimeSpan ResolveBucket(EventDashboardDto? dashboard, DateTime start, DateTime end)
        => EventTrendBuckets.BucketOf(dashboard?.Trend?.Interval) ?? EventTrendRangeMath.BucketFor(start, end);

    /// <summary>한 칸의 폭 — 끌어 고른 기간이 여기에 맞춰진다.</summary>
    public TimeSpan Bucket { get; private set; } = TimeSpan.FromHours(1);

    /// <summary>플롯 왼쪽 — 차트가 실제로 측정한 그림 영역의 왼쪽(<see cref="Resize"/> 로 들어온다).</summary>
    public double PlotLeft => _plotLeft;
    public double PlotTop => _plotTop;
    public double PlotWidth => _plotWidth;
    public double PlotHeight => _plotHeight;

    /// <summary>
    /// 뷰가 차트의 <b>실측</b> 그림 영역을 알려 준다 — 더 이상 고정 여백을 추측해 빼지 않는다.
    /// <para>예전엔 뷰가 Border 의 전체 폭/높이만 주고, 여기서 고정 상수(PlotPadding*)를 빼서 안쪽 사각형을
    /// <b>추측</b>했다. 그 상수가 실제 차트가 그리는 여백(한글 라벨 높이 · DPI · 폰트 스케일에 좌우됨)과
    /// 어긋나면, 차트를 정확히 못 따라가는 채로 드래그 수학만 딴 자리를 가리켰다(2026-09-24 세 번째 실기
    /// 캡처 — 라벨이 잘리거나 플롯 안으로 겹쳐 찍힌 게 원인이자 증거). 이제 뷰가 차트의
    /// <c>CoreChart.DrawMarginLocation</c>/<c>DrawMarginSize</c>(라이브러리가 제 폰트 메트릭으로 스스로 잰
    /// 값)를 그대로 여기로 밀어 준다 — 추측이 사라졌으니 어긋날 수도 없다.</para>
    /// </summary>
    public void Resize(double plotLeft, double plotTop, double plotWidth, double plotHeight)
    {
        plotWidth = Math.Max(0, plotWidth);
        plotHeight = Math.Max(0, plotHeight);
        if (Math.Abs(_plotLeft - plotLeft) < 0.5 && Math.Abs(_plotTop - plotTop) < 0.5 &&
            Math.Abs(_plotWidth - plotWidth) < 0.5 && Math.Abs(_plotHeight - plotHeight) < 0.5) return;

        _plotLeft = plotLeft;
        _plotTop = plotTop;
        _plotWidth = plotWidth;
        _plotHeight = plotHeight;
        NotifyOfPropertyChange(nameof(PlotLeft));
        NotifyOfPropertyChange(nameof(PlotTop));
        NotifyOfPropertyChange(nameof(PlotWidth));
        NotifyOfPropertyChange(nameof(PlotHeight));
    }

    /// <summary>
    /// 테마가 바뀌었다 — 뷰가 <c>IThemeService.ThemeChanged</c>(구독은 뷰의 Loaded/Unloaded 수명)를 받아 이걸 부른다.
    /// 계열 색도 이제 칩과 같은 테마 토큰 브러시에서 읽으므로(<see cref="SeriesColor"/>) 테마가 바뀌면
    /// 같이 갱신해야 한다 — <see cref="RebuildTrend"/> 를 통째로 다시 부른다(축 텍스트/툴팁 페인트까지 한 번에).
    /// </summary>
    public void ApplyTheme(BaseTheme theme)
    {
        _theme = theme;
        TooltipTextPaint = ChartThemeProvider.TooltipTextPaint(theme);
        TooltipBackgroundPaint = ChartThemeProvider.TooltipBackgroundPaint(theme);
        RebuildTrend();
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
        Bucket = ResolveBucket(dashboard, _start, _end);
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
        OperationCount = summary.Operation;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["sensor"] = summary.SensorDetection,
            ["camera"] = summary.CameraDetection,
            ["alert"] = summary.Alert,
            ["mal"] = summary.Malfunction,
            ["con"] = summary.Connection,
            ["act"] = summary.Action,
        };
        // 서버가 total 을 주지만, 켜고 끄기와 맞물리려면 화면이 스스로 더한 값이 정본이다.
        //   더하는 계열은 서버 total 과 같아야 한다 — 서버 total = 센서 + 카메라 + 사전 경보 + 장애 + 연결 + 조치(운영 제외).
        //   예전엔 사전 경보를 빼고 더해 서버보다 작은 총계를 그렸다(실서버 왕복 E3a).
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
        NotifyOfPropertyChange(nameof(OperationCount));

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
                    (EventSeriesSpec.Alert, IsOn("alert") ? controller.Alert : 0),
                    (EventSeriesSpec.Malfunction, IsOn("mal") ? controller.Malfunction : 0),
                    (EventSeriesSpec.Connection, IsOn("con") ? controller.Connection : 0),
                    (EventSeriesSpec.Action, IsOn("act") ? controller.Action : 0),
                };
                rows.Add(Bar(controller.ControllerName, values));
            }
        }
        else if (_deviceGroup == OverviewDeviceGroup.Camera)
        {
            foreach (var camera in byDevice.Cameras ?? new List<CameraStatsDto>())
            {
                var values = new List<(EventSeriesSpec, int)>
                {
                    (EventSeriesSpec.Camera, IsOn("camera") ? camera.CameraDetection : 0),
                    (EventSeriesSpec.Alert, IsOn("alert") ? camera.Alert : 0),
                };
                rows.Add(Bar(camera.CameraName, values));
            }
        }
        else
        {
            // 함체 · 통문 — 운영 이벤트는 총계 밖이라 켜고 끄는 칩이 없다(항상 보인다).
            foreach (var device in (byDevice.Enclosures ?? new List<DeviceEventStatsDto>()).Concat(byDevice.Gates ?? new List<DeviceEventStatsDto>()))
            {
                var values = new List<(EventSeriesSpec, int)>
                {
                    (EventSeriesSpec.Sensor, IsOn("sensor") ? device.SensorDetection : 0),
                    (EventSeriesSpec.Alert, IsOn("alert") ? device.Alert : 0),
                    (EventSeriesSpec.Malfunction, IsOn("mal") ? device.Malfunction : 0),
                    (EventSeriesSpec.Connection, IsOn("con") ? device.Connection : 0),
                    (EventSeriesSpec.Operation, device.Operation),
                };
                rows.Add(Bar(device.DeviceName ?? $"#{device.DeviceNumber}", values));
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

    /// <summary>
    /// 다섯 계열(LiveCharts <see cref="LineSeries{T}"/>)과 시간 축 라벨을 다시 쌓는다.
    /// <para>픽셀 좌표를 손으로 계산하지 않는다 — 차트가 제 <c>ActualWidth</c>로 스스로 배치한다(레거시
    /// <c>DataChartPanelViewModel.DataInitialize</c>와 같은 관용구). 그래서 2026-09-23 결함
    /// (플롯 폭 0 → 도형이 한 점에 뭉침)은 이 경로에서 구조적으로 재발할 수 없다 — Resize 가
    /// 언제 오든, 심지어 한 번도 안 오든 Series/XAxes 는 항상 채워진다.</para>
    /// </summary>
    private void RebuildTrend()
    {
        Series.Clear();

        // 서버는 빈 칸을 주지 않는다 — 기간 전체를 칸마다 채워야 등간격 그림 = 시각 비례(끌기 수학의 가정)가 된다(E3e).
        if (_dashboard?.Trend?.Series is not { Count: > 0 }) { _xLabels = Array.Empty<string>(); BuildAxes(); return; }
        var dense = EventTrendBuckets.Densify(_dashboard.Trend.Series, _start, _end, Bucket);
        var buckets = dense.Select(b => b.Item).ToList();

        void Add(EventSeriesSpec spec, Func<EventTrendItemDto, int> pick)
        {
            if (!IsOn(spec.Key)) return;

            var color = SeriesColor(spec.BrushKey);
            var stroke = new SolidColorPaint(color, 2.5f);
            if (spec.DashArray is not null) stroke.PathEffect = new DashEffect(ParseDashArray(spec.DashArray));

            Series.Add(new LineSeries<int>
            {
                Name = spec.Name,
                Values = buckets.Select(pick).ToArray(),
                Stroke = stroke,
                GeometryStroke = stroke,
                GeometryFill = new SolidColorPaint(color),
                GeometrySize = 5,
                LineSmoothness = 0,   // 원래 손그림처럼 버킷 사이를 직선으로 — 곡선은 실제 값을 왜곡해 보인다.
                // 센서 탐지만 면적을 깐다(정본 window-layout-system-storyboard.html L2578).
                Fill = spec.Key == "sensor" ? new SolidColorPaint(color.WithAlpha(36)) : null,
            });
        }

        Add(EventSeriesSpec.Sensor, b => b.SensorDetection);
        Add(EventSeriesSpec.Camera, b => b.CameraDetection);
        Add(EventSeriesSpec.Alert, b => b.Alert);
        Add(EventSeriesSpec.Malfunction, b => b.Malfunction);
        Add(EventSeriesSpec.Connection, b => b.Connection);
        Add(EventSeriesSpec.Action, b => b.Action);

        _xLabels = dense.Select(b => EventTrendBuckets.Label(b, Bucket)).ToList();
        BuildAxes();
    }

    /// <summary>
    /// 계열 색의 <b>유일한</b> 출처 — 칩이 읽는 바로 그 브러시 토큰(<see cref="EventSeriesSpec.BrushKey"/>)을
    /// <see cref="BrushTokenResolver"/> 로 읽는다(칩과 똑같이 <c>Application.Current.TryFindResource</c>,
    /// <c>Converters/ConsoleTokenConverters.cs</c> 의 <c>TokenBrushConverter</c>·<c>TokenBrushAssist</c> 와 동일
    /// 경로). 전엔 여기서 <c>ChartHelper.TrendCategories</c> 하드코딩 팔레트를 <b>복제</b>해 썼는데, 그 팔레트가
    /// 칩의 실제 테마 토큰 색(예: StatusWarningBrush=#B26A00)과 전혀 다른 값(#FFCD00)이라 차트 선이 칩과
    /// 따로 노는 결함으로 실측됐다(2026-09-24 라이브 캡처). 두 번째 팔레트를 새로 하드코딩하지 않고
    /// 아예 <b>같은 소스</b>를 부르게 해 구조적으로 다시 어긋날 수 없게 한다.
    /// </summary>
    private static SKColor SeriesColor(string brushKey)
        => BrushTokenResolver(brushKey) is SolidColorBrush brush
            ? new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A)
            : FallbackSeriesColor;

    /// <summary>
    /// 브러시 토큰 해석 — 기본은 칩과 같은 <c>Application.Current.TryFindResource</c>. 헤드리스 테스트(Application
    /// 없음)에서 이 델리게이트를 가짜 리소스 사전으로 갈아 끼워 "칩과 같은 키를 실제로 물어봤는가"를 검증한다
    /// (<c>EventOverviewViewModelSeriesColorTests</c>). internal — 테스트가 같은 어셈블리에서 갈아 끼운다.
    /// </summary>
    internal static Func<string, Brush?> BrushTokenResolver { get; set; } =
        key => Application.Current?.TryFindResource(key) as Brush;

    /// <summary>토큰을 못 찾았을 때만(Application 부재 등) 쓰는 대체색 — slate-400, 정상 경로에선 절대 안 쓰인다.</summary>
    private static readonly SKColor FallbackSeriesColor = new(148, 163, 184);

    private static float[] ParseDashArray(string dashArray)
        => dashArray.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => float.Parse(s, CultureInfo.InvariantCulture))
                    .ToArray();

    /// <summary>
    /// 축을 다시 쌓는다 — 데이터가 바뀌었을 때(<see cref="RebuildTrend"/>)와 테마가 바뀌었을 때
    /// (<see cref="ApplyTheme"/>) 양쪽에서 부른다. 어느 쪽이 먼저 오든 <see cref="_theme"/>·<see cref="_xLabels"/>
    /// 캐시를 쓰므로 결과가 어긋나지 않는다.
    /// </summary>
    private void BuildAxes()
    {
        XAxes.Clear();
        XAxes.Add(new Axis
        {
            Name = "시간",
            NameTextSize = 12,
            NamePaint = ChartThemeProvider.TextPaint(_theme),
            Labels = _xLabels.Count > 0 ? _xLabels.ToArray() : null,
            // 회전 없음(가로) — 회전 라벨은 대각선 바운딩박스라 여백을 더 많이, 더 예측하기 어렵게 먹는다.
            // 가로 한 줄은 높이가 폰트 한 줄로 단순해 DrawMargin 을 Auto 로 맡겼을 때 라이브러리가 정확히
            // 잰다(EventOverviewView.xaml.cs OnChartLoaded — 더 이상 여백을 고정 상수로 추측하지 않는다).
            // 라이브러리가 겹치는 라벨은 알아서 건너뛴다(24개 중 일부만 — 의도된 동작, "빽빽한 24개"보다 낫다).
            TextSize = 11,
            LabelsPaint = ChartThemeProvider.TextPaint(_theme),
            UnitWidth = 1,
            ShowSeparatorLines = false,
        });

        YAxes.Clear();
        YAxes.Add(new Axis
        {
            // 축 이름 없음(의도) — LiveCharts2 는 Y축 Name 을 세로(90°)로 그리고 가로 배치 옵션이 없다.
            // 한글 세로쓰기는 읽기 어렵고, 카드 제목("시간대별 추이")과 값 라벨이 단위를 이미 밝힌다.
            TextSize = 11,
            LabelsPaint = ChartThemeProvider.TextPaint(_theme),
            MinLimit = 0,
            // 값 축 눈금선 — 옅게(알파 40/255), 텍스트와 같은 색이면 너무 강하다.
            SeparatorsPaint = new SolidColorPaint(ChartThemeProvider.TextColor(_theme).WithAlpha(40), 1),
            ShowSeparatorLines = true,
        });

        NotifyOfPropertyChange(nameof(XAxes));
        NotifyOfPropertyChange(nameof(YAxes));
    }
}
