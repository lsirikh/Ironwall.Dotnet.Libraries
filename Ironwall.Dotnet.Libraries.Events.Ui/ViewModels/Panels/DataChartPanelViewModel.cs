using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Theme.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView.Painting;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;

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
                                       , EventProviderService providerService)
                                       : base(eventAggregator, log)
        {
            _providerService = providerService;

            // 차트용 컬렉션 초기화
            Series = new ObservableCollection<ISeries>();
            XAxes = new ObservableCollection<Axis>();
            YAxes = new ObservableCollection<Axis>();

            // IMPL-21: 테마 반응 — 레전드/축 글씨 theme-aware (다크 안보임 수정)
            _themeService = TryResolveThemeService();
            if (_themeService != null)
                _themeService.ThemeChanged += OnThemeChanged;
            LegendTextPaint.Color = ChartThemeProvider.LegendTextColor(CurrentTheme);
        }

        private static IThemeService? TryResolveThemeService()
        {
            try { return IoC.Get<IThemeService>(); }
            catch { return null; }
        }
        private BaseTheme CurrentTheme => _themeService?.Current ?? BaseTheme.Light;
        private void OnThemeChanged(object? sender, BaseTheme theme)
        {
            try { LegendTextPaint = new SolidColorPaint { Color = ChartThemeProvider.LegendTextColor(theme), SKTypeface = ChartThemeProvider.KoreanTypeface() }; _ = DataInitialize(); }
            catch (Exception ex) { _log?.Warning($"[DataChartPanelViewModel] OnThemeChanged 실패: {ex.Message}"); }
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
            if (close && _themeService != null)
                _themeService.ThemeChanged -= OnThemeChanged;
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
            IsChartLoading = true;
            return Task.Run(async () =>
            {
                try
                {
                    /*──────────────────────────────────────────────────────────────
                     *  Statistics API 단일 호출 → 라인 차트 생성
                     *──────────────────────────────────────────────────────────────*/
                    var dashboardDto = await _providerService.FetchEventDashboardAsync(
                        StartDate, EndDate, "hour", ct);

                    if (dashboardDto == null)
                    {
                        _log?.Warning("FetchEventDashboardAsync returned null — skip chart update");
                        return;
                    }

                    LastDashboardDto = dashboardDto;

                    var (seriesList, xLabels) = ChartHelper.BuildLineSeriesFromTrend(
                        dashboardDto.Trend, StartDate, EndDate);

                    var xAxis = new Axis
                    {
                        Labels = xLabels,
                        LabelsRotation = 15,
                        TextSize = 12,
                        LabelsPaint = new SolidColorPaint(ChartThemeProvider.TextColor(CurrentTheme), 2),
                        UnitWidth = 1,
                        ShowSeparatorLines = false
                    };

                    var yAxis = new Axis
                    {
                        Name = "Events",
                        NameTextSize = 14,
                        LabelsPaint = new SolidColorPaint(ChartThemeProvider.TextColor(CurrentTheme), 2),
                        NamePaint = new SolidColorPaint(ChartThemeProvider.TextColor(CurrentTheme), 2),
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
                catch (Exception ex)
                {
                    _log?.Error($"Exception in DataInitialize: {ex.GetType().Name} - {ex.Message}");
                }
                finally
                {
                    DispatcherService.Invoke(() => IsChartLoading = false);
                    UpdateAction?.Invoke(StartDate, EndDate);
                }
            });
        }

        /// <summary>
        /// Statistics API를 재호출하여 LastDashboardDto를 갱신한다.
        /// 이벤트 삭제/추가 후 차트를 최신 상태로 유지하기 위해 사용.
        /// </summary>
        public async Task<EventDashboardDto?> RefreshDashboardDtoAsync(CancellationToken ct = default)
        {
            var dto = await _providerService.FetchEventDashboardAsync(
                StartDate, EndDate, "hour", ct);
            if (dto != null)
                LastDashboardDto = dto;
            return LastDashboardDto;
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

        // 토글 시 차트가 다시 그리도록 알림 속성으로(재할당+Notify).
        private SolidColorPaint _legendTextPaint = new SolidColorPaint
        {
            Color = ChartThemeProvider.LegendTextColor(BaseTheme.Light),
            SKTypeface = ChartThemeProvider.KoreanTypeface()
        };
        public SolidColorPaint LegendTextPaint
        {
            get => _legendTextPaint;
            set { _legendTextPaint = value; NotifyOfPropertyChange(nameof(LegendTextPaint)); }
        }

        public SolidColorPaint LedgendBackgroundPaint { get; set; } =
            new SolidColorPaint(new SKColor(240, 240, 240, 00));


        public EventDashboardDto? LastDashboardDto { get; private set; }

        public bool IsChartLoading
        {
            get => _isChartLoading;
            set { _isChartLoading = value; NotifyOfPropertyChange(() => IsChartLoading); }
        }

        #endregion
        #region - Attributes -
        public delegate void SendDate(DateTime start, DateTime end);
        public event SendDate? UpdateAction;
        protected DateTime _startDate;
        protected DateTime _endDate;
        protected DateTime _endDateDisplay;
        private EventProviderService _providerService;
        private bool _isChartLoading;
        private readonly IThemeService? _themeService;
        #endregion
    }
}