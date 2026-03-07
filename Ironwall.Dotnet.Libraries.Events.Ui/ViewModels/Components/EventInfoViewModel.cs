using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using System;
using System.Collections.ObjectModel;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.Painting;
using System.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Caliburn.Micro;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using LiveChartsCore.Measure;
using LiveChartsCore.Drawing;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System.Xml.Linq;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using MahApps.Metro.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2025 10:13:25 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EventInfoViewModel : BasePanelViewModel
    {
        #region - Ctors -
        public EventInfoViewModel(DeviceProvider deviceProvider
                                , EventProvider eventProvider
                                , EventProviderService providerService
                                , IEventAggregator? eventAggregator = null
                                , ILogService? log = null)
        {
            _deviceProvider = deviceProvider;
            _eventProvider = eventProvider;
            LSeries = new ObservableCollection<ISeries>();
            DSeries = new ObservableCollection<ISeries>();
            _providerService = providerService;

            // BasePanelViewModel initialization
            _className = this.GetType().Name.ToString();
            _eventAggregator = eventAggregator ?? IoC.Get<IEventAggregator>();
            _log = log ?? IoC.Get<ILogService>();

            _names = new[] { "DET", "MAL", "CON", "ACT" };
            RefreshActiveness();
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            //await DataInitialize(_cancellationTokenSource!.Token).ConfigureAwait(false);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            LSeries.Clear();
            DSeries.Clear();
            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        //  실제 토글 로직 - 인덱스 경계 확인만 해주면 OK
        private void ToggleVisibility(int index, bool isEnable)
        {
            if (index < 0 || index >= LSeries.Count) return;

            var sL = LSeries[index];
            sL.IsVisible = isEnable;

            var sD = DSeries[index];
            sD.IsVisible = isEnable;

            // 시리즈 객체 내부에서 INotifyPropertyChanged 를 구현하므로
            // 별도 NotifyOfPropertyChange 호출 없이도 차트가 즉시 재렌더링됩니다.
        }


        public void SetData(DateTime startDate, DateTime endDate, string[] names)
        {
            SetDate(startDate, endDate);
            SetNames(names);
        }

        private void SetDate(DateTime startDate, DateTime endDate)
        {
            _startDate = startDate;
            _endDate = endDate;
        }

        private void SetNames(string[] names)
        {
            _names = names;
            RefreshActiveness();
        }

        private void RefreshActiveness()
        {
            SetFlag(ref _isDetectionActive, _names.Contains("DET"), nameof(IsDetectionActive));
            SetFlag(ref _isMalfunctionActive, _names.Contains("MAL"), nameof(IsMalfunctionActive));
            SetFlag(ref _isConnectionActive, _names.Contains("CON"), nameof(IsConnectionActive));
            SetFlag(ref _isActionActive, _names.Contains("ACT"), nameof(IsActionActive));
        }

        private void SetFlag(ref bool field, bool value, string propertyName)
        {
            if (field == value) return;
            field = value;
            NotifyOfPropertyChange(propertyName);
        }

        private void OnPointMeasured(ChartPoint<float, RoundedRectangleGeometry, LabelGeometry> point)
        {
            var perPointDelay = 100; // in milliseconds
            var delay = point.Context.Entity.MetaData!.EntityIndex * perPointDelay;
            var speed = (float)point.Context.Chart.AnimationsSpeed.TotalMilliseconds + delay;

            // the animation takes a function, that represents the progress of the animation
            // the parameter is the progress of the animation, it goes from 0 to 1
            // the function must return a value from 0 to 1, where 0 is the initial state
            // and 1 is the end state

            point.Visual?.SetTransition(
                new Animation(progress =>
                {
                    var d = delay / speed;

                    return progress <= d
                        ? 0
                        : EasingFunctions.BuildCustomElasticOut(1.5f, 0.60f)((progress - d) / (1 - d));
                },
                TimeSpan.FromMilliseconds(speed)));
        }




        /// <summary>
        /// 이전 DataInitialize를 취소하고 새 CancellationToken을 반환
        /// </summary>
        public CancellationToken CancelAndRestart()
        {
            if (_infoCts != null && !_infoCts.IsCancellationRequested)
            {
                _infoCts.Cancel();
                _infoCts.Dispose();
            }
            _infoCts = new CancellationTokenSource();
            return _infoCts.Token;
        }

        public Task DataInitialize(CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    /*──────────────────────────────────────────────────────────────
                       *  ★ 수정: SetData()에서 설정한 _startDate, _endDate 사용
                    *──────────────────────────────────────────────────────────────*/
                    var startDate = _startDate;
                    var endDate = _endDate;

                    // 만약 날짜가 설정 안 됐으면 기본값 사용
                    if (startDate == default || endDate == default)
                    {
                        endDate = DateTime.Now;
                        startDate = endDate.AddDays(-7);
                    }

                    var detectionTask = _providerService.FetchDetectionEventsAsync(startDate, endDate, cancellationToken);
                    var malfunctionTask = _providerService.FetchMalfunctionEventsAsync(startDate, endDate, cancellationToken);
                    var connectionTask = _providerService.FetchConnectionEventsAsync(startDate, endDate, cancellationToken);
                    var actionTask = _providerService.FetchActionEventsAsync(startDate, endDate, cancellationToken);

                    await Task.WhenAll(detectionTask, malfunctionTask, connectionTask, actionTask);

                    // Update EventProvider with fetched events
                    _eventProvider.Clear();
                    foreach (var item in detectionTask.Result) _eventProvider.Add(item);
                    foreach (var item in malfunctionTask.Result) _eventProvider.Add(item);
                    foreach (var item in connectionTask.Result) _eventProvider.Add(item);
                    foreach (var item in actionTask.Result) _eventProvider.Add(item);

                    // 디버깅: Connection 이벤트 카운트 로그
                    _log?.Info($"[EventInfoViewModel] Fetched {detectionTask.Result.Count} detection, " +
                               $"{malfunctionTask.Result.Count} malfunction, " +
                               $"{connectionTask.Result.Count} connection, " +
                               $"{actionTask.Result.Count} action events");

                   // //Connection 이벤트의 Device 상태 확인
                   //var connectionEvents = connectionTask.Result;
                   // foreach (var ev in connectionEvents)
                   // {
                   //     switch (ev.Device.DeviceType)
                   //     {
                   //         case Enums.EnumDeviceType.NONE:
                   //             break;
                   //         case Enums.EnumDeviceType.Controller:
                   //             var controller = ev.Device as IControllerDeviceModel;
                   //             _log?.Info($"[ConnectionEvent] ID={ev.Id}, controller.Id={controller?.Id}, " +
                   //                        $"Controller.DeviceNumber={controller?.DeviceNumber}");
                   //             break;
                   //         case Enums.EnumDeviceType.Multi:
                   //         case Enums.EnumDeviceType.Fence:
                   //         case Enums.EnumDeviceType.Underground:
                   //         case Enums.EnumDeviceType.Contact:
                   //         case Enums.EnumDeviceType.PIR:
                   //         case Enums.EnumDeviceType.IoController:
                   //         case Enums.EnumDeviceType.Laser:
                   //         case Enums.EnumDeviceType.Cable:
                   //         case Enums.EnumDeviceType.SmartSensor:
                   //         case Enums.EnumDeviceType.SmartSensor2:
                   //         case Enums.EnumDeviceType.SmartCompound:
                   //         case Enums.EnumDeviceType.Radar:
                   //         case Enums.EnumDeviceType.OpticalCable:
                   //             var sensor = ev.Device as ISensorDeviceModel;
                   //             _log?.Info($"[ConnectionEvent] ID={ev.Id}, Sensor.Id={sensor?.Id}, " +
                   //                        $"Controller={(sensor?.Controller != null ? sensor.Controller.DeviceNumber.ToString() : "null")}");
                   //             break;
                   //         case Enums.EnumDeviceType.IpCamera:
                   //             break;
                   //         case Enums.EnumDeviceType.IpSpeaker:
                   //             break;
                   //         case Enums.EnumDeviceType.Fence_Group:
                   //             break;
                   //         default:
                   //             break;
                   //     }
                   // }

                    // 컨트롤러(Device) 번호 → 문자열 레이블
                    var devices = _deviceProvider.OfType<IControllerDeviceModel>()
                                                .OrderBy(d => d.DeviceNumber);          // 보기 좋게 정렬

                    // ★ 빈 데이터 폴백: devices가 비어있으면 "No Data" 라벨로 대체
                    var deviceLabels = devices.Any()
                        ? devices.Select(d => d.DeviceNumber.ToString()).ToArray()
                        : new[] { "No Data" };

                    var xLabel = new Axis
                    {
                        Labels = deviceLabels,
                        Name = "controller",
                        Position = AxisPosition.Start,
                        NameTextSize = 15,
                        LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        UnitWidth = 1,
                        // (선택) 줄눈 제거
                        NamePadding = new Padding(0, -10, 0, 5),   // L,T,R,B
                        NamePaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        ShowSeparatorLines = false
                    };

                    var yLabels = new Axis
                    {
                        Name = "events",
                        Position = AxisPosition.Start,
                        LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        NameTextSize = 15,
                        NamePaint = new SolidColorPaint(new SKColor(255, 255, 255), 0),
                        NamePadding = new Padding(0, 5, 0, -10),   // L,T,R,B
                        MinLimit = 0
                    };


                    DispatcherService.Invoke(() => {
                        var newLSeries = new ObservableCollection<ISeries>();
                        var newDSeries = new ObservableCollection<ISeries>();

                        XAxes.Clear();
                        XAxes.Add(xLabel);
                        YAxes.Clear();
                        YAxes.Add(yLabels);

                        foreach (var code in _names)
                        {
                            if (!_meta.TryGetValue(code, out var m)) continue;


                            /* 1) 데이터 계산 */
                            var counts = m.Counter(_startDate, _endDate, devices, _eventProvider);

                            // ★ 빈 데이터 폴백: counts가 비어있으면 0값으로 채움
                            if (counts.Count == 0)
                                counts = new List<double> { 0 };

                            /* 2) Bar + Pie 시리즈 */
                            var bar = ChartHelper.MakeBar(
                                m.DisplayName, counts, m.BarColor, SKColors.White);

                            var pie = ChartHelper.MakePie(
                                m.DisplayName, counts.Sum(), m.PieColor, SKColors.White);

                            /* 3) 추가 */
                            newLSeries.Add(bar);
                            newDSeries.Add(pie);
                        }

                        LSeries = newLSeries;
                        DSeries = newDSeries;
                        NotifyOfPropertyChange(() => LSeries);
                        NotifyOfPropertyChange(() => DSeries);
                    });

                }
                catch (OperationCanceledException ex)
                {
                    _log?.Warning($"Raised {nameof(OperationCanceledException)}({nameof(DataInitialize)}) : {ex.Message}");
                }
                finally
                {
                }
            });
        }

        /// <summary>
        /// Statistics API DTO를 직접 받아 Bar/Pie 차트를 생성한다.
        /// EventDashboardViewModel → DataChartPanelViewModel.LastDashboardDto 경유.
        /// </summary>
        public Task DataInitializeFromStats(EventSummaryDto summary, EventByDeviceDto byDevice,
                                            CancellationToken ct = default)
        {
            IsChartLoading = true;
            return Task.Run(() =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    // Bar chart — by-device 기반
                    var (barSeries, xLabels) = ChartHelper.BuildBarSeriesFromByDevice(byDevice, _names);

                    // Pie chart — summary 기반
                    var pieSeries = ChartHelper.BuildPieSeriesFromSummary(summary, _names);

                    // 디버그: API에서 받은 제어기 이름 확인
                    _log?.Info($"[EventInfoViewModel] xLabels: [{string.Join(", ", xLabels)}]");

                    var koreanTypeface = SKTypeface.FromFamilyName("Malgun Gothic");

                    var xAxis = new Axis
                    {
                        Labels = xLabels,
                        Name = "controller",
                        Position = AxisPosition.Start,
                        NameTextSize = 15,
                        LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2)
                        {
                            SKTypeface = koreanTypeface
                        },
                        UnitWidth = 1,
                        NamePadding = new Padding(0, -10, 0, 5),
                        NamePaint = new SolidColorPaint(new SKColor(255, 255, 255), 2)
                        {
                            SKTypeface = koreanTypeface
                        },
                        ShowSeparatorLines = false
                    };

                    var yAxis = new Axis
                    {
                        Name = "events",
                        Position = AxisPosition.Start,
                        LabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                        NameTextSize = 15,
                        NamePaint = new SolidColorPaint(new SKColor(255, 255, 255), 0),
                        NamePadding = new Padding(0, 5, 0, -10),
                        MinLimit = 0
                    };

                    DispatcherService.Invoke(() =>
                    {
                        XAxes.Clear();
                        XAxes.Add(xAxis);
                        YAxes.Clear();
                        YAxes.Add(yAxis);

                        LSeries = new ObservableCollection<ISeries>(barSeries);
                        DSeries = new ObservableCollection<ISeries>(pieSeries);
                        NotifyOfPropertyChange(() => LSeries);
                        NotifyOfPropertyChange(() => DSeries);
                    });
                }
                catch (OperationCanceledException ex)
                {
                    _log?.Warning($"Raised {nameof(OperationCanceledException)}({nameof(DataInitializeFromStats)}) : {ex.Message}");
                }
                finally
                {
                    DispatcherService.Invoke(() => IsChartLoading = false);
                }
            });
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public bool IsDetectionEnable
        {
            get { return _isDetectionEnable; }
            set 
            { _isDetectionEnable = value; ToggleVisibility(0, value); }
        }


        public bool IsMalfunctionEnable
        {
            get { return _isMalfunctionEnable; }
            set { _isMalfunctionEnable = value; ToggleVisibility(1, value); }
        }


        public bool IsConnectionEnable
        {
            get { return _isConnectionEnable; }
            set { _isConnectionEnable = value; ToggleVisibility(2, value); }
        }


        public bool IsActionEnable
        {
            get { return _isActionEnable; }
            set { _isActionEnable = value; ToggleVisibility(3, value); }
        }

        public SolidColorPaint LegendTextPaint { get; set; } =
        new SolidColorPaint
        {
            Color = new SKColor(50, 50, 50),
            SKTypeface = SKTypeface.FromFamilyName("Malgun Gothic")
        };

        public SolidColorPaint LedgendBackgroundPaint { get; set; } =
            new SolidColorPaint(new SKColor(240, 240, 240, 00));

        public SolidColorPaint TooltipTextPaint { get; set; } =
        new SolidColorPaint
        {
            Color = new SKColor(50, 50, 50),
            SKTypeface = SKTypeface.FromFamilyName("Malgun Gothic")
        };

        public ObservableCollection<ISeries> LSeries { get; private set; }
        public ObservableCollection<ISeries> DSeries { get; private set; }

        public ObservableCollection<Axis> XAxes { get; } = [];
        public ObservableCollection<Axis> YAxes { get; } = [];

        private readonly Dictionary<string, CategoryMeta> _meta = new()
        {
            ["DET"] = new(
                "Detection",
                new SKColor(255, 205, 0),
                new SKColor(255, 205, 0),
                (from, to, devices, evts) =>
                DataHelper.GetDetectionCountsByDevice(from, to, devices, evts.OfType<IDetectionEventModel>())),
            ["MAL"] = new(
                "Malfunction",
                new SKColor(30, 144, 255),
                new SKColor(30, 144, 255),
                (from, to, devices, evts) =>
                DataHelper.GetMalfunctionCountsByDevice(from, to, devices, evts.OfType<IMalfunctionEventModel>())),
            ["CON"] = new(
                "Connection",
                new SKColor(155, 89, 182),
                new SKColor(155, 89, 182),
                (from, to, devices, evts) =>
                DataHelper.GetConnectionCountsByDevice(from, to, devices, evts.OfType<IConnectionEventModel>())),
            ["ACT"] = new(
                "Action",
                new SKColor(50, 205, 50),
                new SKColor(50, 205, 50),
                (from, to, devices, evts) =>
                DataHelper.GetActionCountsByDevice(from, to, devices, evts.OfType<IActionEventModel>()))
        };

        public bool IsDetectionActive 
        { 
            get => _isDetectionActive; 
            private set => SetFlag(ref _isDetectionActive, value, nameof(IsDetectionActive)); 
        }
        public bool IsMalfunctionActive 
        { 
            get => _isMalfunctionActive; 
            private set => SetFlag(ref _isMalfunctionActive, value, nameof(IsMalfunctionActive)); 
        }
        public bool IsConnectionActive 
        { 
            get => _isConnectionActive;
            private set => SetFlag(ref _isConnectionActive, value, nameof(IsConnectionActive)); 
        }
        public bool IsActionActive 
        {
            get => _isActionActive; 
            private set => SetFlag(ref _isActionActive, value, nameof(IsActionActive));
        }

        public bool IsChartLoading
        {
            get => _isChartLoading;
            set { _isChartLoading = value; NotifyOfPropertyChange(() => IsChartLoading); }
        }

        #endregion
        #region - Attributes -
        private EventProviderService _providerService;
        private DeviceProvider _deviceProvider;
        private EventProvider _eventProvider;
        private CancellationTokenSource? _infoCts;
        private string[] _names;
        private bool _isChartLoading;
        private DateTime _startDate;
        private DateTime _endDate;

        private bool _isDetectionActive;
        private bool _isMalfunctionActive;
        private bool _isConnectionActive;
        private bool _isActionActive;

        private bool _isDetectionEnable = true;
        private bool _isMalfunctionEnable = true;
        private bool _isConnectionEnable = true;
        private bool _isActionEnable = true;
        #endregion
    }

    // ─── 파일 최상위 (using 아래 아무 곳) ────────────────────────────
    readonly record struct CategoryMeta(
        string DisplayName,          // ← displayName 으로 선언
        SKColor BarColor,
        SKColor PieColor,
        CountsCounter Counter);

    // delegate 도 "파일 최상위" 에 둔다
    delegate List<double> CountsCounter(
        DateTime from, DateTime to,
        IEnumerable<IBaseDeviceModel> devices,
        IEnumerable<IBaseEventModel> evts);
}