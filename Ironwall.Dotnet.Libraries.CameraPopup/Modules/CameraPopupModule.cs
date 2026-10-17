using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;
using Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Modules;

/****************************************************************************
   Purpose      : 카메라 팝업 호스트 감시자 · 이벤트 창 관리자 DI 등록(PRD camera-popup-modes FR-09 · 11 · 12 · 24~29)
                  GIS 부트스트래퍼 배선(Start 호출 · 상태 안내)은 호스트 앱 — 여기서는 등록만.
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// <c>builder.RegisterModule(new CameraPopupModule(options, () =&gt; ((IStreamingSetupModel)setup).CameraPopupSettings))</c>.
/// <c>ILogService</c> 는 호스트 앱이 등록한다. 감시자는 컨테이너가 해제될 때 호스트 프로세스를 함께 내린다.
/// </summary>
/// <remarks>
/// 설정 대리자를 주지 않으면(그리고 <see cref="ICameraPopupSettingsSource"/> 를 따로 등록하지 않으면)
/// 이벤트 창 관리자는 "사용 안 함"으로 동작한다 — 탐지 창이 뜨지 않는 안전한 쪽.
/// 모니터 조회(<see cref="IDisplayMonitorProvider"/>)는 먼저 등록된 것이 있으면 그것을 쓴다.
/// </remarks>
public class CameraPopupModule : Module
{
    private readonly CameraPopupHostOptions _options;
    private readonly Func<CameraPopupSettings>? _settings;

    public CameraPopupModule(CameraPopupHostOptions? options = null, Func<CameraPopupSettings>? settings = null)
    {
        _options = options ?? new CameraPopupHostOptions();
        _settings = settings;
    }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);
        builder.RegisterInstance(_options).AsSelf().SingleInstance();
        builder.RegisterType<CameraPopupHostSupervisor>()
            .As<ICameraPopupHost>()
            .AsSelf()
            .SingleInstance();

        // ── 이벤트 창(T-04/T-06) ──
        if (_settings is not null)
            builder.RegisterInstance(new DelegateCameraPopupSettingsSource(_settings)).As<ICameraPopupSettingsSource>().SingleInstance();
        builder.RegisterType<Win32DisplayMonitorProvider>().As<IDisplayMonitorProvider>().SingleInstance().PreserveExistingDefaults();
        builder.Register(c => new EventWindowManager(
                c.Resolve<ICameraPopupHost>(),
                c.ResolveOptional<ICameraPopupSettingsSource>() ?? DelegateCameraPopupSettingsSource.Disabled,
                c.Resolve<IDisplayMonitorProvider>(),
                c.ResolveOptional<ILogService>()))
            .As<IEventWindowManager>()
            .AsSelf()
            .SingleInstance();
    }
}
