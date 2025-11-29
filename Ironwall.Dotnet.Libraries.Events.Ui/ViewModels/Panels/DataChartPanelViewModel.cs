using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Windows.Shapes;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/28/2025 5:09:04 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DataChartPanelViewModel : BasePanelViewModel
    {
        
        #region - Ctors -
        public DataChartPanelViewModel(IEventAggregator eventAggregator
                                       , ILogService log
                                       , EventProviderService providerService
                                       , EventProvider eventProvider)
                                       : base(eventAggregator, log)
        {
            _providerService = providerService;
            _eventProvider = eventProvider;

            // 차트용 컬렉션 초기화
            Series = new ObservableCollection<ISeries>();
            XAxes = new ObservableCollection<Axis>();
            YAxes = new ObservableCollection<Axis>();
        }
        #endregion
        #region - Implementation of Interface -
        protected override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await DataInitialize(cancellationToken).ConfigureAwait(false);
            await base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            Series.Clear();
            XAxes.Clear();
            YAxes.Clear();
            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        public bool CanClckSearch => true;
        public async void ClickSearch()
        {
            try
            {
                if (_cancellationTokenSource != null)
                    _cancellationTokenSource.Cancel();

                _cancellationTokenSource = new CancellationTokenSource();
                await DataInitialize(_cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }

        }
        public bool CanClickCancel => true;
        public void ClickCancel()
        {
            try
            {
                if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested)
                    return;

                _cancellationTokenSource.Cancel();
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }
        #endregion
        #region - Processes -
        public void SetDate(DateTime startDate, DateTime endDate)
        {
            _startDate = startDate;
            _endDate = endDate;
            EndDateDisplay = StartDate;
        }
        private Task DataInitialize(CancellationToken ct = default)
        {
            return Task.Run(async () =>
            {
                try
                {
                    /*──────────────────────────────────────────────────────────────
                     *  1. GOP API로부터 이벤트 데이터 병렬 수집
                     *──────────────────────────────────────────────────────────────*/
                    var detectionTask = _providerService.FetchDetectionEventsAsync(StartDate, EndDate, ct);
                    var malfunctionTask = _providerService.FetchMalfunctionEventsAsync(StartDate, EndDate, ct);
                    var connectionTask = _providerService.FetchConnectionEventsAsync(StartDate, EndDate, ct);
                    var actionTask = _providerService.FetchActionEventsAsync(StartDate, EndDate, ct);

                    await Task.WhenAll(detectionTask, malfunctionTask, connectionTask, actionTask);

                    /*──────────────────────────────────────────────────────────────
                     *  2. EventProvider 갱신 (메모리 캐시 역할)
                     *──────────────────────────────────────────────────────────────*/
                    _eventProvider.Clear();
                    foreach (var item in detectionTask.Result) _eventProvider.Add(item);
                    foreach (var item in malfunctionTask.Result) _eventProvider.Add(item);
                    foreach (var item in connectionTask.Result) _eventProvider.Add(item);
                    foreach (var item in actionTask.Result) _eventProvider.Add(item);

                    /*──────────────────────────────────────────────────────────────
                     *  3. 차트 카테고리 정의
                     *     - Name   : 범례에 표시될 시리즈명
                     *     - Color  : 라인 & 포인트 색상
                     *     - Events : 해당 타입의 이벤트 컬렉션
                     *──────────────────────────────────────────────────────────────*/
                    var eventTypes = new[]
                    {
                new CategoryMeta("Detection",   new SKColor(255, 205, 0),   _eventProvider.OfType<IDetectionEventModel>()),
                new CategoryMeta("Malfunction", new SKColor(30, 144, 255),  _eventProvider.OfType<IMalfunctionEventModel>()),
                new CategoryMeta("Connection",  new SKColor(155, 89, 182),  _eventProvider.OfType<IConnectionEventModel>()),
                new CategoryMeta("Action",      new SKColor(50, 205, 50),   _eventProvider.OfType<IActionEventModel>())
            };

                    /*──────────────────────────────────────────────────────────────
                     *  4. [1차 순회] 시간축 라벨 수집 & 카테고리별 집계
                     *  
                     *     ┌─────────────────────────────────────────────────────┐
                     *     │  WHY 2-PASS?                                        │
                     *     │  ─────────────────────────────────────────────────  │
                     *     │  각 카테고리마다 이벤트가 발생한 시간대가 다르다.   │
                     *     │  모든 시간 라벨을 먼저 수집해야 빠진 구간을 0으로   │
                     *     │  채워서 라인이 끊어지지 않게 만들 수 있다.          │
                     *     └─────────────────────────────────────────────────────┘
                     *
                     *     xLabels    : 전체 시간축 (SortedSet → 자동 정렬)
                     *     groupedData: 카테고리별 { 시간 → 이벤트 수 } 딕셔너리
                     *──────────────────────────────────────────────────────────────*/
                    var xLabels = new SortedSet<string>();
                    var groupedData = new Dictionary<string, Dictionary<string, int>>();

                    foreach (var type in eventTypes)
                    {
                        var grouped = type.Events
                            .GroupBy(ev => ev.DateTime.ToString("yyyy-MM-dd HH"))
                            .ToDictionary(g => g.Key, g => g.Count());

                        groupedData[type.Name] = grouped;
                        xLabels.UnionWith(grouped.Keys);
                    }

                    /*──────────────────────────────────────────────────────────────
                     *  5. [2차 순회] 시리즈 생성
                     *  
                     *     xLabels가 확정된 후, 각 카테고리별로 values 배열 생성.
                     *     해당 시간대에 이벤트가 없으면 0을 채워 연속 라인 보장.
                     *     
                     *     ┌───────┬───────┬───────┬───────┐
                     *     │ 03:00 │ 05:00 │ 07:00 │ 09:00 │  ← xLabels
                     *     ├───────┼───────┼───────┼───────┤
                     *     │   3   │   0   │  15   │   0   │  ← Detection
                     *     │  30   │  28   │  14   │  22   │  ← Malfunction
                     *     │   0   │   0   │   0   │   0   │  ← CONNECTION
                     *     │   2   │   0   │   0   │   1   │  ← ACTION
                     *     └───────┴───────┴───────┴───────┘
                     *──────────────────────────────────────────────────────────────*/
                    var xLabelArray = xLabels.ToArray();
                    var seriesList = new List<ISeries>();

                    foreach (var type in eventTypes)
                    {
                        var dict = groupedData[type.Name];

                        // 없는 시간대는 0으로 채움 → 라인 연속성 확보
                        var values = xLabelArray
                            .Select(label => dict.TryGetValue(label, out var count) ? count : 0)
                            .ToArray();

                        seriesList.Add(new LineSeries<int>
                        {
                            Name = type.Name,
                            LineSmoothness = 0.5,
                            GeometrySize = 8,

                            Stroke = new SolidColorPaint(type.Color, 3),
                            GeometryStroke = new SolidColorPaint(type.Color, 3),
                            GeometryFill = new SolidColorPaint(Darken(type.Color, 0.5f)),

                            Fill = null,
                            Values = values
                        });
                    }

                    /*──────────────────────────────────────────────────────────────
                     *  6. 축(Axis) 설정
                     *──────────────────────────────────────────────────────────────*/
                    var xAxis = new Axis
                    {
                        Labels = xLabelArray,
                        LabelsRotation = 15,
                        TextSize = 12,
                        UnitWidth = 1,
                        ShowSeparatorLines = false
                    };

                    var yAxis = new Axis
                    {
                        Name = "Events",
                        NameTextSize = 14,
                        MinLimit = 0
                    };

                    /*──────────────────────────────────────────────────────────────
                     *  7. UI 스레드에서 차트 컬렉션 갱신
                     *──────────────────────────────────────────────────────────────*/
                    DispatcherService.Invoke(() =>
                    {
                        Series.Clear();
                        foreach (var s in seriesList)
                            Series.Add(s);

                        XAxes.Clear();
                        XAxes.Add(xAxis);

                        YAxes.Clear();
                        YAxes.Add(yAxis);
                    });
                }
                catch (TaskCanceledException ex)
                {
                    _log?.Warning($"TaskCanceledException in DataInitialize: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _log?.Error($"Exception in DataInitialize: {ex.GetType().Name} - {ex.Message}");
                }
                finally
                {
                    UpdateAction?.Invoke(StartDate, EndDate);
                }
            });
        }

        private SKColor Darken(SKColor color, float factor)
        {
            return new SKColor(
                (byte)(color.Red * factor),
                (byte)(color.Green * factor),
                (byte)(color.Blue * factor),
                color.Alpha);
        }

        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public DateTime StartDate
        {
            get { return _startDate; }
            set
            {
                _startDate = value;
                NotifyOfPropertyChange(() => StartDate);
                EndDateDisplay = _startDate;
            }
        }

        public DateTime EndDate
        {
            get { return _endDate; }
            set
            {
                _endDate = value;
                NotifyOfPropertyChange(() => EndDate);
            }
        }

        public DateTime EndDateDisplay
        {
            get { return _endDateDisplay; }
            set
            {
                _endDateDisplay = value;
                NotifyOfPropertyChange(() => EndDateDisplay);
            }
        }

        public ObservableCollection<ISeries> Series { get; }
        public ObservableCollection<Axis> XAxes { get; }
        public ObservableCollection<Axis> YAxes { get; }

        public SolidColorPaint LegendTextPaint { get; set; } =
        new SolidColorPaint
        {
            Color = new SKColor(50, 50, 50),
            SKTypeface = SKTypeface.FromFamilyName("Caliber")
        };

        public SolidColorPaint LedgendBackgroundPaint { get; set; } =
            new SolidColorPaint(new SKColor(240, 240, 240, 00));


        #endregion
        #region - Attributes -
        public delegate void SendDate(DateTime start, DateTime end);
        public event SendDate? UpdateAction;
        protected DateTime _startDate;
        protected DateTime _endDate;
        protected DateTime _endDateDisplay;
        private EventProvider _eventProvider;
        private EventProviderService _providerService;
        #endregion
    }

    readonly record struct CategoryMeta(
       string Name,          // ← displayName 으로 선언
       SKColor Color,
       IEnumerable<IBaseEventModel> Events);

}