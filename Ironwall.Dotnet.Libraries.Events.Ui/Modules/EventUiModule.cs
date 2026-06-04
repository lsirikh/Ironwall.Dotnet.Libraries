using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Modules;
using Ironwall.Dotnet.Libraries.Events.Api.Modules;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Modules;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/23/2025 5:26:00 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventUiModule : Module
{
    
    #region - Ctors -
    public EventUiModule(IEventSetupModel eventSetup, IApiSetupModel apiSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _apiSetup = apiSetup;
        _eventSetup = eventSetup;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterModule(new EventModule(_eventSetup, _log, _count++));
            //builder.RegisterModule(new EventDbModule(_dbSetup, _log, _count++)); // 2
            builder.RegisterModule(new EventApiModule(_log, new ApiSetupModel(_apiSetup), count: _count++));

            // EventProviderService: DeviceProvider 및 EventProvider 의존성 추가
            builder.Register(c => new EventProviderService(
                c.Resolve<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Events.Api.Services.IEventApiService>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Events.Providers.EventProvider>()
            )).SingleInstance();
            
            builder.RegisterType<EventDashboardViewModel>().SingleInstance();
            builder.RegisterType<EventTabControlViewModel>().SingleInstance();
            builder.RegisterType<DetectionEventPanelViewModel>().SingleInstance();
            builder.RegisterType<MalfunctionEventPanelViewModel>().SingleInstance();
            builder.RegisterType<ConnectionEventPanelViewModel>().SingleInstance();
            builder.RegisterType<ActionEventPanelViewModel>().SingleInstance();
            builder.RegisterType<EventInfoViewModel>().SingleInstance();
            builder.RegisterType<CameraEventInfoViewModel>().SingleInstance();
            builder.RegisterType<DataChartPanelViewModel>().SingleInstance();
            builder.RegisterType<EventCardListPanelViewModel>().SingleInstance();
            builder.RegisterType<SymbolEventManager>()
                   .AsSelf()
                   .As<ISymbolEventManager>()
                   .SingleInstance();
            builder.RegisterType<DeviceNatsSyncService>()
                   .As<IDeviceNatsSyncService>()
                   .SingleInstance();
            builder.RegisterType<CameraPtzNatsSyncService>()
                   .As<ICameraPtzNatsSyncService>()
                   .SingleInstance();
            builder.Register(c => new DetectionNatsSyncService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<ISymbolEventManager>(),
                c.Resolve<IEventQueueManager>(),
                _eventSetup,
                c.Resolve<Caliburn.Micro.IEventAggregator>()
            )).As<IDetectionNatsSyncService>().SingleInstance();
            builder.Register(c => new MalfunctionNatsSyncService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<ISymbolEventManager>(),
                c.Resolve<IEventQueueManager>(),
                _eventSetup,
                c.Resolve<Caliburn.Micro.IEventAggregator>()
            )).As<IMalfunctionNatsSyncService>().SingleInstance();
            builder.Register(c => new EventQueueManager(
                       c.ResolveOptional<ILogService>(),
                       c.ResolveOptional<IEventSetupModel>()))
                   .As<IEventQueueManager>()
                   .AsSelf()
                   .SingleInstance();

            // SoundAlarmController: ISoundService가 컨테이너에 등록된 경우에만 생성
            builder.Register(ctx =>
            {
                var soundService = ctx.ResolveOptional<ISoundService>();
                var setupModel = ctx.ResolveOptional<SoundSetupModel>();
                var durationSeconds = setupModel?.DetectionSoundDuration ?? 20;
                return new SoundAlarmController(
                    stopAndPlay: (eventType) => { _ = soundService?.StopAndPlayAsync(eventType); },
                    durationSeconds
                );
            }).As<ISoundAlarmController>().SingleInstance();

            // EventQueueManager ↔ SymbolEventManager 전이 이벤트 와이어링
            builder.RegisterBuildCallback(scope =>
            {
                var eqm = scope.Resolve<EventQueueManager>();
                var sem = scope.Resolve<SymbolEventManager>();

                // 개별 심볼: SSOT 복합 상태 전이 단일 경로 (OnDeviceStateChanged)
                // OnDeviceFirstEvent/OnDeviceEmpty 경로 제거 — OnDeviceStateChanged 발화 순서가 앞서므로 중복
                eqm.OnDeviceStateChanged += sem.HandleDeviceStateChanged;

                // 자동복구: Fault 자동 조치보고
                var elp = scope.Resolve<EventCardListPanelViewModel>();
                eqm.OnAutoRecovery += faultEntryId =>
                {
                    var task = elp.HandleAutoRecoveryAsync(faultEntryId);
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _log?.Error($"[AutoRecovery] 미처리 예외: {t.Exception?.GetBaseException()}");
                    }, TaskScheduler.Default);
                };

                // 자동 조치보고: 타임아웃 만료 시 API 보고 → Dequeue → 카드 제거
                eqm.OnAutoReport += entry =>
                {
                    var task = elp.HandleAutoReportAsync(entry);
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            _log?.Error($"[AutoReport] 미처리 예외: {t.Exception?.GetBaseException()}");
                        entry.AutoReportInFlight = false;
                    }, TaskScheduler.Default);
                };

                // 그룹 심볼: 복합 상태 전이 (OnGroupStateChanged)
                eqm.OnGroupStateChanged += sem.HandleGroupStateChanged;

                // 공유 타이머 시작 (1초 간격, 자동 조치보고 타임아웃 체크)
                eqm.StartSharedTimer();

                // SoundAlarmController 이벤트 와이어링
                // ISoundService가 미등록 시 SAC 비활성화 (State=Playing 고착 방지)
                var sac = scope.Resolve<ISoundAlarmController>();
                var soundService = scope.ResolveOptional<ISoundService>();
                if (soundService != null)
                {
                    eqm.OnAnyEnqueue += sac.OnEventArrived;
                    soundService.OnDetectionPlaybackCompleted += sac.OnPlaybackStopped;
                    soundService.OnMalfunctionPlaybackCompleted += sac.OnPlaybackStopped;
                }
                else
                {
                    _log?.Warning("ISoundService not registered — SoundAlarmController disabled");
                }

                // DetectionNatsSyncService 시작 — NATS DETECT 구독 등록
                var dns = scope.Resolve<IDetectionNatsSyncService>();
                dns.StartService();

                // MalfunctionNatsSyncService 시작 — NATS MALFUNCTION 구독 등록
                var mns = scope.Resolve<IMalfunctionNatsSyncService>();
                mns.StartService();
            });
         
            builder.RegisterType<DetectionReportDialogViewModel>().AsSelf()//  new DetectionReportDialogViewModel() 로도 해결 가능
                                                                  .As<EventReportDialogViewModel>()// 베이스로 요청해도 이 인스턴스를 반환
                                                                  .SingleInstance();              // or InstancePerDependency()
            builder.RegisterType<MalfunctionReportDialogViewModel>().AsSelf()// new DetectionReportDialogViewModel() 로도 해결 가능
                                                                   .As<EventReportDialogViewModel>()// 베이스로 요청해도 이 인스턴스를 반환
                                                                   .SingleInstance();              // or InstancePerDependency()
    }
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    private ILogService? _log;
    private IApiSetupModel _apiSetup;
    private IEventSetupModel _eventSetup;
    private int _count;
    #endregion
}