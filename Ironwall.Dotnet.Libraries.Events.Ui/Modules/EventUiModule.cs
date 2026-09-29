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
            builder.RegisterModule(new EventApiModule(_log, new ApiSetupModel(_apiSetup), EventApiName, count: _count++));

            // 썸네일 절대 URL 조합용 base URL seam — DetectionSelectionViewModel이 IoC로 조달
            // (탐지 detail 썸네일이 상대경로 "/api/thumbnails/…" 로 와서 host 결합 필요). 실패해도 default 이미지로 폴백.
            builder.RegisterInstance(new ApiSetupModel(_apiSetup)).As<IApiSetupModel>().SingleInstance();

            // EventProviderService: DeviceProvider 및 EventProvider 의존성 추가
            builder.Register(c => new EventProviderService(
                c.Resolve<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Events.Api.Services.IEventApiService>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Events.Providers.EventProvider>()
            )).SingleInstance();

            // (Phase3) 조치보고 멱등 가드 — 자동/자동복구/배치(EventCardListPanel) + 수동(EventCard.SendAction) 공유 싱글톤
            builder.RegisterType<ActionReportGuard>().As<IActionReportGuard>().SingleInstance();

            // 조치보고 문구 — 조치 트레이 · 조치보고 창이 같은 목록을 쓴다(서버 조치보고 문구 관리 목록, 못 읽으면 기본 문구).
            //   문구 API 는 Reports 모듈이 등록한다 — 없으면(구 호스트) ResolveOptional 이 null → 기본 문구. 늦게 푼다.
            builder.Register(c =>
            {
                var context = c.Resolve<IComponentContext>();
                return new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionReportPhraseSource(
                    () => context.ResolveOptional<Ironwall.Dotnet.Libraries.Reports.Api.Services.IActionReportTemplateApiService>(),
                    c.ResolveOptional<ILogService>());
            }).As<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.IActionReportPhraseSource>().SingleInstance();

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
            // 이벤트 억제(정비 창) 스케줄 관리 패널 — IEventSuppressionApiService(EventApiModule)+DeviceProvider/DeviceGroupProvider 자동 주입.
            builder.RegisterType<EventSuppressionSchedulePanelViewModel>().SingleInstance();
            builder.RegisterType<SymbolEventManager>()
                   .AsSelf()
                   .As<ISymbolEventManager>()
                   .SingleInstance();
            // DeviceNatsSyncService 제거(FR-08): As<IService> 미등록으로 StartService가 호출되지 않던 사문 서비스.
            // SYNC_DEVICE는 메인 솔루션 NatsDomainService.ProcessSyncDeviceAsync가 CREATED/UPDATED/DELETED 전부 처리(권위 경로).
            builder.RegisterType<CameraPtzNatsSyncService>()
                   .As<ICameraPtzNatsSyncService>()
                   .SingleInstance();
            builder.RegisterType<TrackingStatusNatsSyncService>()
                   .As<ITrackingStatusNatsSyncService>()
                   .As<IService>().WithMetadata("Order", _count + 3)   // OnExit StopAsync → NATS 구독 해제
                   .SingleInstance();
            builder.Register(c => new DetectionNatsSyncService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<ISymbolEventManager>(),
                c.Resolve<IEventQueueManager>(),
                _eventSetup,
                c.Resolve<Caliburn.Micro.IEventAggregator>(),
                // 로그인 게이팅(Login_Gated_GIS_Init): 수동 팩토리 new라 옵셔널 파라미터가 자동 주입되지 않음 —
                // 명시 전달 필수(누락 시 게이트 무력 → 로그아웃 상태 알람 수신 + EQM 자동조치보고 유출).
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider>(),   // 소속 제어기 해석(Controller_Fault_AutoRecovery_Extension) — 수동 팩토리=명시 전달 필수
                c.ResolveOptional<IDoorContactPolicy>(),   // FR-13 ③ 통문/함체 접점→개폐 폴백 정책(GMaps.Ui 가 Symbol3D 설정으로 등록, 없으면 기본 true) — 수동 팩토리라 명시 전달 필수(누락 시 자동복구 태깅 죽음)
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Ui.Services.IDeviceProviderService>()   // 캐시 미스 단건 GET(브로커 N-5, 프로브 S11) — 수동 팩토리라 명시 전달 필수
            )).As<IDetectionNatsSyncService>()
              .As<IService>().WithMetadata("Order", _count + 1)   // (EB1) OnExit StopAsync → NATS 구독 해제 (Order는 모듈 _count 관례)
              .SingleInstance();
            // FR-13 ③ 접점 폴백 기본 정책 — GMaps.Ui 가 Symbol3DDoorContactPolicy 를 등록하면 그것이 이긴다(PreserveExistingDefaults: 먼저 등록된 쪽 유지, 나중 등록은 기본 덮어씀).
            builder.RegisterType<DefaultDoorContactPolicy>().As<IDoorContactPolicy>().SingleInstance().PreserveExistingDefaults();
            // FR-13 ②: OPERATION_EVENT(all.event.operation, 서버 PRD v1.4) → 통문/함체 DoorState. 구독은 DETECT 와 같은 NATS 이벤트에서 cmd 로 분기.
            builder.Register(c => new OperationEventNatsSyncService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<ISymbolEventManager>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>()   // 로그인 게이팅(수동 팩토리=명시 전달 필수)
            )).As<IOperationEventNatsSyncService>()
              .As<IService>().WithMetadata("Order", _count + 5)   // OnExit StopAsync → NATS 구독 해제
              .SingleInstance();
            builder.Register(c => new MalfunctionNatsSyncService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<ISymbolEventManager>(),
                c.Resolve<IEventQueueManager>(),
                _eventSetup,
                c.Resolve<Caliburn.Micro.IEventAggregator>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>(),   // 로그인 게이팅(수동 팩토리=명시 전달 필수)
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider>(),   // 제어기무통신 그룹확장(GMap_Controller_Blackout) — 수동 팩토리라 명시 전달 필수(누락 시 기능 죽음)
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Devices.Ui.Services.IDeviceProviderService>()   // 캐시 미스 단건 GET(브로커 N-5) — 수동 팩토리라 명시 전달 필수
            )).As<IMalfunctionNatsSyncService>()
              .As<IService>().WithMetadata("Order", _count + 2)   // (EB1) OnExit StopAsync → NATS 구독 해제 (Order는 모듈 _count 관례)
              .SingleInstance();
            // G-2 활성 억제 배너 데이터 소스 — /active 30s 폴링 모니터(로그인 게이팅, 수동 팩토리라 tokenStorage 명시 전달).
            builder.Register(c => new SuppressionActiveMonitor(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Events.Api.Services.IEventSuppressionApiService>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>()
            )).As<ISuppressionActiveMonitor>()
              .As<IService>().WithMetadata("Order", _count + 4)   // 자동 시작(ExecuteAsync 폴링) + OnExit StopAsync
              .SingleInstance();
            // SYNC_EVENT_SUPPRESSION 자가필터 수신 — 회차 경계 통지를 받아 /active 폴링을 앞당긴다(가속 전용).
            // ⚠ .As<IService>() 필수 — 빠뜨리면 StopAsync 가 안 불려 NATS 구독이 누수된다.
            //    모니터(Order +4)가 먼저 서야 하므로 그 뒤 순번을 준다.
            builder.Register(c => new EventSuppressionSyncNatsService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<ISuppressionActiveMonitor>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>()
            )).As<IEventSuppressionSyncNatsService>()
              .As<IService>().WithMetadata("Order", _count + 7)
              .SingleInstance();
            builder.Register(c => new DetectionSyncNatsService(
                c.ResolveOptional<ILogService>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Nats.Services.INatsService>(),
                c.Resolve<IEventQueueManager>(),
                c.Resolve<Ironwall.Dotnet.Libraries.Events.Api.Services.IEventApiService>(),   // GET /events/detections/{id}
                c.Resolve<Caliburn.Micro.IEventAggregator>(),
                c.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>()   // 로그인 게이팅(수동 팩토리=명시 전달 필수)
            )).As<IDetectionSyncNatsService>()
              .As<IService>().WithMetadata("Order", _count + 4)   // (EB1) OnExit StopAsync → NATS 구독 해제. Order 고유값(기존 +1/+2/+3 사용 중).
              .SingleInstance();
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
                    durationSeconds,
                    stopAll: () => { _ = soundService?.StopAllSoundsAsync(); }   // 활성 0건 → 알람 끄기(WP-1 ⑨)
                );
            }).As<ISoundAlarmController>().SingleInstance();

            // EventQueueManager ↔ SymbolEventManager 전이 이벤트 와이어링
            builder.RegisterBuildCallback(scope =>
            {
                var eqm = scope.Resolve<EventQueueManager>();
                var sem = scope.Resolve<SymbolEventManager>();
                // 로그인 게이팅(Login_Gated_GIS_Init T3 연장): 무인 자동조치보고 차단용 인증 소스.
                // 미등록(DB모드)=null=게이트 비활성(하위호환). ADR: 로그아웃 상태서 조치보고 발송 절대 금지(사용자 요구).
                var tokenStorage = scope.ResolveOptional<Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService>();

                // 개별 심볼: SSOT 복합 상태 전이 단일 경로 (OnDeviceStateChanged)
                // OnDeviceFirstEvent/OnDeviceEmpty 경로 제거 — OnDeviceStateChanged 발화 순서가 앞서므로 중복
                eqm.OnDeviceStateChanged += sem.HandleDeviceStateChanged;

                // 자동복구 · 자동 조치보고 — 둘 다 로그인 게이트를 지난다(WireAutoActions)
                var elp = scope.Resolve<EventCardListPanelViewModel>();
                WireAutoActions(eqm, elp, tokenStorage, _log);

                // 그룹 심볼: 복합 상태 전이 (OnGroupStateChanged)
                eqm.OnGroupStateChanged += sem.HandleGroupStateChanged;

                // 공유 타이머 시작 (1초 간격, 자동 조치보고 타임아웃 체크)
                eqm.StartSharedTimer();

                // SoundAlarmController 이벤트 와이어링 — 알람 소리의 유일한 주인(WP-1 ⑨, 호스트 카드별 재생은 걷었다)
                // ISoundService가 미등록 시 SAC 비활성화 (State=Playing 고착 방지)
                var sac = scope.Resolve<ISoundAlarmController>();
                var soundService = scope.ResolveOptional<ISoundService>();
                if (soundService != null)
                {
                    WireSoundAlarm(eqm, sac, soundService);
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

                // OperationEventNatsSyncService 시작 — NATS OPERATION_EVENT 구독(통문/함체 개폐 형태, FR-13 ②)
                var ons = scope.Resolve<IOperationEventNatsSyncService>();
                ons.StartService();

                // DetectionSyncNatsService 시작 — NATS SYNC_DETECTION 구독 등록(PTZ 회전 후 썸네일 갱신)
                var dsns = scope.Resolve<IDetectionSyncNatsService>();
                dsns.StartService();

                // TrackingStatusNatsSyncService 시작 — NATS TRACKING_STATUS 구독 등록(로그-only stub)
                var tns = scope.Resolve<ITrackingStatusNatsSyncService>();
                tns.StartService();
            });
         
            builder.RegisterType<DetectionReportDialogViewModel>().AsSelf()//  new DetectionReportDialogViewModel() 로도 해결 가능
                                                                  .As<EventReportDialogViewModel>()// 베이스로 요청해도 이 인스턴스를 반환
                                                                  .SingleInstance();              // or InstancePerDependency()
            builder.RegisterType<MalfunctionReportDialogViewModel>().AsSelf()// new DetectionReportDialogViewModel() 로도 해결 가능
                                                                   .As<EventReportDialogViewModel>()// 베이스로 요청해도 이 인스턴스를 반환
                                                                   .SingleInstance();              // or InstancePerDependency()

            // 탐지 신호 이력 다이얼로그 (Detection_Signal_History FR-09) — 단일 인스턴스, Initialize()로 장비 컨텍스트 교체
            builder.RegisterType<DetectionHistoryDialogViewModel>().AsSelf().SingleInstance();

            // N-13 mapping workbench — 이벤트 맵핑 워크벤치(3-Pane · 팔레트 → 액션 보드 · Draft + [적용]).
            // 창 뷰모델은 여기 등록하지 않는다 — 입구가 열 때마다 새로 만든다(Draft 가 창을 넘어 살아남지 않게).
            // 🔴 RegisterType 으로 두면 안 된다 — IApiService · ApiSetupModel 은 ApiModule 이 <b>이름으로만</b>
            //    등록한다("EventApi" · "DeviceApi" …). 이름 없는 해석은 호스트에서 늘 실패해 입구가 감춰졌다
            //    (실창 log-2026-09-27.txt:778 "activating MappingWorkbenchLauncher -> MappingWorkbenchGateway").
            //    EventApiService 가 Initialize() 하고 Bearer 를 붙이는 그 "EventApi" 클라이언트를 그대로 쓴다.
            builder.Register(c => new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingWorkbenchGateway(
                        c.ResolveNamed<Ironwall.Dotnet.Libraries.Api.Services.IApiService>(EventApiName),
                        c.ResolveNamed<ApiSetupModel>(EventApiName),
                        c.ResolveOptionalNamed<Ironwall.Dotnet.Libraries.Api.Services.IServerContractProbe>(EventApiName)
                            ?? c.ResolveOptional<Ironwall.Dotnet.Libraries.Api.Services.IServerContractProbe>(),
                        c.ResolveOptional<ILogService>()))
                   .As<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingWorkbenchGateway>()
                   .SingleInstance();
            builder.RegisterType<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingDeviceSource>()
                   .As<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingDeviceSource>()
                   .SingleInstance();
            builder.RegisterType<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingWorkbenchLauncher>()
                   .As<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingWorkbenchLauncher>()
                   .SingleInstance();
    }
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// 큐(EQM)의 자동 조치 두 갈래를 카드 목록에 잇는다 — 둘 다 <b>로그인 게이트</b>를 지난다.
    /// <list type="bullet">
    ///   <item>자동 조치보고(<c>OnAutoReport</c>): 미인증이면 보내지 않고 다음 틱으로 미룬다(AutoReportInFlight 만 푼다).</item>
    ///   <item>자동복구(<c>OnAutoRecovery</c>): 미인증이면 보내지 않는다 — 종전엔 게이트가 없어 로그아웃 상태에서도
    ///     조치보고가 나갔다(WP-1 ⑤, 사용자 ADR "로그아웃 상태서 조치보고 발송 절대 금지"). 큐 엔트리는 이미 빠졌으므로
    ///     카드는 남고, 재로그인한 담당자가 조치보고하면 심볼은 큐 실제 상태로 다시 계산된다.</item>
    /// </list>
    /// </summary>
    internal static void WireAutoActions(IEventQueueManager eqm, EventCardListPanelViewModel elp,
                                         Ironwall.Dotnet.Libraries.Accounts.Api.Services.ITokenStorageService? tokenStorage, ILogService? log)
    {
        eqm.OnAutoRecovery += faultEntryId =>
        {
            if (tokenStorage is { IsAuthenticated: false })
            {
                log?.Warning($"[AutoRecovery] 미인증 상태 — 자동복구 조치보고 발송 안 함(entry={faultEntryId}, 카드는 남는다)");
                return;
            }
            var task = elp.HandleAutoRecoveryAsync(faultEntryId);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    log?.Error($"[AutoRecovery] 미처리 예외: {t.Exception?.GetBaseException()}");
            }, TaskScheduler.Default);
        };

        // 자동 조치보고: 타임아웃 만료 시 API 보고 → Dequeue → 카드 제거
        eqm.OnAutoReport += entry =>
        {
            // 로그인 게이팅: 로그인 전/로그아웃 상태에서는 자동조치보고 발송 차단(무인 조치보고 금지, 사용자 요구).
            // 로그인 중 Enqueue된 항목이 로그아웃 후 타임아웃되는 경계 케이스 — NATS 수신 게이트로도 못 막는 잔여 경로.
            // 미발송·미Dequeue → 재로그인(=담당자 복귀) 시 다음 틱에 정상 보고. AutoReportInFlight만 해제.
            if (tokenStorage is { IsAuthenticated: false })
            {
                log?.Warning($"[AutoReport] 미인증 상태 — 자동조치보고 발송 보류(entry={entry.EntryId})");
                entry.AutoReportInFlight = false;
                return;
            }
            var task = elp.HandleAutoReportAsync(entry);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    log?.Error($"[AutoReport] 미처리 예외: {t.Exception?.GetBaseException()}");
                entry.AutoReportInFlight = false;
            }, TaskScheduler.Default);
        };
    }

    /// <summary>
    /// 알람 소리의 <b>유일한 주인</b> — 큐(EQM)에 들어오면 울리고, 활성 건수가 0 이 되면(조치보고) 멈추거나 남은 종류로 바꾼다(WP-1 ⑨).
    /// 종전엔 호스트가 카드마다 토큰을 달아 한 번, 이 컨트롤러가 토큰 없이 또 한 번 울려 조치보고해도 토큰 없는 쪽이 계속 울렸다.
    /// 탐지 계열(침입 · 접점 …)은 전부 탐지 소리다 — 사운드 서비스는 Intrusion · Fault 두 소리만 안다.
    /// <b>울리는 것 = 세는 것</b>(WP-8 H1): 큐의 활성 탐지 건수(<c>GetActiveCounts</c>)도 Fault 가 아닌 전부를 센다. 두 규칙이 어긋나면
    /// 접점 · 사전경보가 울리자마자 (0,0) 통지로 꺼진다. 건수 통지는 최신값이 마지막에 닿는다(WP-8 M4, EQM 전달 직렬화).
    /// </summary>
    internal static void WireSoundAlarm(IEventQueueManager eqm, ISoundAlarmController sac, ISoundService soundService)
    {
        eqm.OnAnyEnqueue += type => sac.OnEventArrived(type == Enums.EnumEventType.Fault ? Enums.EnumEventType.Fault : Enums.EnumEventType.Intrusion);
        eqm.OnActiveCountChanged += sac.OnActiveCountsChanged;
        soundService.OnDetectionPlaybackCompleted += sac.OnPlaybackStopped;
        soundService.OnMalfunctionPlaybackCompleted += sac.OnPlaybackStopped;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    /// <summary>이 모듈이 올리는 EventApiModule 의 등록 이름 — 이름으로만 등록된 IApiService · ApiSetupModel 을 찾는 열쇠.</summary>
    private const string EventApiName = "EventApi";

    private ILogService? _log;
    private IApiSetupModel _apiSetup;
    private IEventSetupModel _eventSetup;
    private int _count;
    #endregion
}