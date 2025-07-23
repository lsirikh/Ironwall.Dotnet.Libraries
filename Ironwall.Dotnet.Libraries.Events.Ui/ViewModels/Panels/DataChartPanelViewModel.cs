using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
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
                                       , IEventDbService eventDbService
                                       , EventProvider eventProvider)
                                       : base(eventAggregator, log)
        {
            _dbService = eventDbService;
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
                    await _dbService.FetchInstanceAsync(StartDate, EndDate, ct);

                    var eventTypes = new[]
                    {
                        new CategoryMeta("Detection",  new SKColor(255, 205, 0), _eventProvider.OfType<IDetectionEventModel>()),
                        new CategoryMeta("Malfunction", new SKColor(30, 144, 255), _eventProvider.OfType<IMalfunctionEventModel>()),
                        new CategoryMeta("Connection", new SKColor(155, 89, 182), _eventProvider.OfType<IConnectionEventModel>()),
                        new CategoryMeta("Action", new SKColor(50, 205, 50), _eventProvider.OfType<IActionEventModel>())
                    };

                    var xLabels = new SortedSet<string>();
                    var seriesList = new List<ISeries>();

                    foreach (var type in eventTypes)
                    {
                        var grouped = type.Events
                            .Where(ev => ev.DateTime >= StartDate && ev.DateTime <= EndDate)
                            .GroupBy(ev => ev.DateTime.ToString("yyyy-MM-dd HH"))
                            .OrderBy(g => g.Key)
                            .Select(g => new { Time = g.Key, Count = g.Count() })
                            .ToList();

                        xLabels.UnionWith(grouped.Select(g => g.Time));

                        var values = xLabels.Select(label =>
                            grouped.FirstOrDefault(g => g.Time == label)?.Count ?? 0).ToArray();

                        seriesList.Add(new LineSeries<int>
                        {
                            Name = type.Name,
                            LineSmoothness = 0.5,
                            GeometrySize = 8,

                            // 선 색상
                            Stroke = new SolidColorPaint(type.Color, 3),

                            // 포인트 테두리 색 (더 진한 색상)
                            GeometryStroke = new SolidColorPaint(type.Color, 3),

                            // 포인트 내부 색 (더 진한 색상)
                            GeometryFill = new SolidColorPaint(Darken(type.Color, 0.5f)),

                            Fill = null,
                            Values = values
                        });
                    }

                    var xAxis = new Axis
                    {
                        Labels = xLabels.ToArray(),
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
        private IEventDbService _dbService;
        #endregion
    }

    readonly record struct CategoryMeta(
       string Name,          // ← displayName 으로 선언
       SKColor Color,
       IEnumerable<IBaseEventModel> Events);

}