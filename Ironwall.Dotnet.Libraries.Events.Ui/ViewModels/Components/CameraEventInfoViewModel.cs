using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
/****************************************************************************
   Purpose      : 카메라 이벤트 KPI 요약 ViewModel (총 건수, 일평균, 활성 카메라 수)
   Created By   : GHLee
   Created On   : 3/3/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class CameraEventInfoViewModel : BasePanelViewModel
{
    #region - Ctors -
    public CameraEventInfoViewModel(EventProvider eventProvider
                                  , IEventAggregator? eventAggregator = null
                                  , ILogService? log = null)
    {
        _eventProvider = eventProvider;
        _className = this.GetType().Name.ToString();
        _eventAggregator = eventAggregator ?? IoC.Get<IEventAggregator>();
        _log = log ?? IoC.Get<ILogService>();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void SetData(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;
    }

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
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detectionEvents = _eventProvider.OfType<IDetectionEventModel>();
            TotalCount = CalcTotalCount(_startDate, _endDate, detectionEvents);
            DailyAverage = CalcDailyAverage(_startDate, _endDate, detectionEvents);
            ActiveCameraCount = CalcActiveCameraCount(_startDate, _endDate, detectionEvents);
        }, cancellationToken);
    }

    /// <summary>
    /// Statistics API DTO를 직접 받아 KPI를 업데이트한다.
    /// </summary>
    public Task DataInitializeFromStats(EventSummaryDto summary, CancellationToken ct = default)
    {
        IsChartLoading = true;
        return Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                TotalCount = summary.CameraDetection;
                DailyAverage = summary.DailyAverages?.CameraDetection ?? 0;
                ActiveCameraCount = summary.ActiveDevices?.Cameras ?? 0;
            }
            finally
            {
                DispatcherService.Invoke(() => IsChartLoading = false);
            }
        }, ct);
    }

    /// <summary>
    /// 카메라 탐지 총 건수 (static — 테스트 가능)
    /// </summary>
    public static int CalcTotalCount(DateTime start, DateTime end, IEnumerable<IDetectionEventModel> events)
    {
        return events
            .Where(e => e.DateTime >= start && e.DateTime < end)
            .Count(e => e.Device?.DeviceType == EnumDeviceType.IpCamera);
    }

    /// <summary>
    /// 일평균 (static — 테스트 가능)
    /// </summary>
    public static double CalcDailyAverage(DateTime start, DateTime end, IEnumerable<IDetectionEventModel> events)
    {
        var total = CalcTotalCount(start, end, events);
        var days = Math.Max(1, (end - start).Days);
        return Math.Round((double)total / days, 1);
    }

    /// <summary>
    /// 활성 카메라 수 (static — 테스트 가능)
    /// </summary>
    public static int CalcActiveCameraCount(DateTime start, DateTime end, IEnumerable<IDetectionEventModel> events)
    {
        return events
            .Where(e => e.DateTime >= start && e.DateTime < end)
            .Where(e => e.Device?.DeviceType == EnumDeviceType.IpCamera)
            .Select(e => e.Device?.Id)
            .Distinct()
            .Count();
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    private int _totalCount;
    public int TotalCount
    {
        get => _totalCount;
        set { _totalCount = value; NotifyOfPropertyChange(); }
    }

    private double _dailyAverage;
    public double DailyAverage
    {
        get => _dailyAverage;
        set { _dailyAverage = value; NotifyOfPropertyChange(); }
    }

    private int _activeCameraCount;
    public int ActiveCameraCount
    {
        get => _activeCameraCount;
        set { _activeCameraCount = value; NotifyOfPropertyChange(); }
    }
    public bool IsChartLoading
    {
        get => _isChartLoading;
        set { _isChartLoading = value; NotifyOfPropertyChange(() => IsChartLoading); }
    }

    #endregion
    #region - Attributes -
    private EventProvider _eventProvider;
    private DateTime _startDate;
    private DateTime _endDate;
    private CancellationTokenSource? _infoCts;
    private bool _isChartLoading;
    #endregion
}
