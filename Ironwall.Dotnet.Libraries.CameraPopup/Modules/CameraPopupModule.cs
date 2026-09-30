using Autofac;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Modules;

/****************************************************************************
   Purpose      : 카메라 팝업 호스트 감시자 DI 등록(PRD camera-popup-modes FR-24~29)
                  GIS 부트스트래퍼 배선(Start 호출 · 상태 안내)은 뒤 태스크 — 여기서는 등록만.
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// <c>builder.RegisterModule(new CameraPopupModule(options))</c>. <c>ILogService</c> 는 호스트 앱이 등록한다.
/// 감시자는 컨테이너가 해제될 때 호스트 프로세스를 함께 내린다.
/// </summary>
public class CameraPopupModule : Module
{
    private readonly CameraPopupHostOptions _options;

    public CameraPopupModule(CameraPopupHostOptions? options = null)
    {
        _options = options ?? new CameraPopupHostOptions();
    }

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);
        builder.RegisterInstance(_options).AsSelf().SingleInstance();
        builder.RegisterType<CameraPopupHostSupervisor>()
            .As<ICameraPopupHost>()
            .AsSelf()
            .SingleInstance();
    }
}
