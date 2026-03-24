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
        try
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
            builder.RegisterType<EventQueueManager>()
                   .As<IEventQueueManager>()
                   .AsSelf()
                   .SingleInstance();

            // EventQueueManager ↔ SymbolEventManager 전이 이벤트 와이어링
            builder.RegisterBuildCallback(scope =>
            {
                var eqm = scope.Resolve<EventQueueManager>();
                var sem = scope.Resolve<SymbolEventManager>();

                // 개별 심볼: 0→1 Detecting, N→0 Normal
                eqm.OnDeviceFirstEvent += sem.SetDeviceDetecting;
                eqm.OnDeviceEmpty += sem.RestoreDeviceSymbol;

                // 그룹 심볼: 0→1 Detecting, N→0 Normal
                eqm.OnGroupFirstEvent += sem.SetGroupDetecting;
                eqm.OnGroupEmpty += sem.RestoreGroupSymbol;

                // 공유 타이머 시작 (1초 간격, 자동 조치보고 타임아웃 체크)
                eqm.StartSharedTimer();

                // DetectionNatsSyncService 시작 — NATS DETECT 구독 등록
                var dns = scope.Resolve<IDetectionNatsSyncService>();
                dns.StartService();
            });
         
            builder.RegisterType<DetectionReportDialogViewModel>().AsSelf()//  new DetectionReportDialogViewModel() 로도 해결 가능
                                                                  .As<EventReportDialogViewModel>()// 베이스로 요청해도 이 인스턴스를 반환
                                                                  .SingleInstance();              // or InstancePerDependency()
            builder.RegisterType<MalfunctionReportDialogViewModel>().AsSelf()// new DetectionReportDialogViewModel() 로도 해결 가능
                                                                   .As<EventReportDialogViewModel>()// 베이스로 요청해도 이 인스턴스를 반환
                                                                   .SingleInstance();              // or InstancePerDependency()

        }
        catch
        {
            throw;
        }
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