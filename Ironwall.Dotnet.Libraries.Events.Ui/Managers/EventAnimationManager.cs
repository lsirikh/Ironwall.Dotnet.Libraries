using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using System;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Managers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/28/2025 5:42:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventAnimationManager
{
    #region - Ctors -
    public EventAnimationManager(ILogService log
                                    , IEventAggregator ea
                                    , EventSetupModel eventSetupModel)
    {
        _eventSetupModel = eventSetupModel;
        _log = log;
        _ea = ea;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void ProcessNewEvent(EnumEventType eventType)
    {
        // 기존 이벤트가 있고 아직 조치보고 안된 경우
        if (_currentEventState != null && !_currentEventState.IsReported)
        {
            if (!ShouldReplaceCurrentEvent(eventType)) return;

            // 기존 타이머 정리
            _currentEventState.AutoReportTimer?.Stop();
        }

        // 새 이벤트 상태 생성
        _currentEventState = new EventState
        {
            EventType = eventType,
            OccurredTime = DateTime.Now,
            IsReported = false
        };

        // 자동조치보고 설정에 따른 타이머 시작
        if (_eventSetupModel.IsAutoEventDiscard)
        {
            StartAutoReportTimer(_eventSetupModel.TimeDiscardSec);
        }

        _log?.Info($"새 이벤트 처리: {eventType}, 자동조치: {_eventSetupModel.IsAutoEventDiscard}");
    }

    private bool ShouldReplaceCurrentEvent(EnumEventType eventType)
    {
        return true;
    }

    public void ReportCurrentEvent()
    {
        if (_currentEventState != null && !_currentEventState.IsReported)
        {
            _currentEventState.IsReported = true;
            _currentEventState.AutoReportTimer?.Stop();

            _log?.Info($"수동 조치보고 처리: {_currentEventState.EventType}");
            RestoreToNormal();
        }
    }

    private void StartAutoReportTimer(int timeoutSeconds)
    {
        _currentEventState.AutoReportTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(timeoutSeconds)
        };

        _currentEventState.AutoReportTimer.Tick += (s, e) =>
        {
            _currentEventState.AutoReportTimer.Stop();
            _currentEventState.IsReported = true;

            _log?.Info($"자동 조치보고 처리: {_currentEventState.EventType} ({timeoutSeconds}초 경과)");
            RestoreToNormal();
        };

        _currentEventState.AutoReportTimer.Start();
    }

    private void RestoreToNormal()
    {
        // 콜백을 통해 상위 컴포넌트에 복원 알림
        OnEventRestored?.Invoke();
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private EventState _currentEventState;
    private EventSetupModel _eventSetupModel;
    private ILogService _log;
    private IEventAggregator? _ea;
    public event System.Action OnEventRestored;
    #endregion
}