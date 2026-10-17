using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;
using Ironwall.Dotnet.Libraries.OnvifSolution.Modules;
using Ironwall.Dotnet.Libraries.OnvifSolution.Services;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers;

/****************************************************************************
   Purpose      : 제공자 등록부 — 종류(VideoProviderKind) → 영상 · PTZ 제공자(PRD camera-popup-modes FR-17/18)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 영상과 PTZ 를 따로 고른다:
/// <list type="bullet">
/// <item>영상 — <see cref="VideoProviderKind.Onvif"/> → ONVIF GetStreamUri · <see cref="VideoProviderKind.Rtsp"/> → 저장 주소 ·
/// <see cref="VideoProviderKind.ExternalVms"/> → 자리(지원 안 함). 시험 무늬 · 파일은 등록부 밖(호스트 생산자가 직접).</item>
/// <item>PTZ — ONVIF · RTSP 주소 둘 다 ONVIF PTZ(현행 더블클릭 팝업은 영상 소스와 무관하게 ONVIF 로 PTZ 를 했다 —
/// "RTSP 주소" 제공자로 바꿨다고 PTZ 가 사라지면 기존 사용자 퇴행). 외부 VMS → 자리. 나머지 → 없음(null).</item>
/// </list>
/// </summary>
public sealed class CameraProviderRegistry
{
    private readonly Dictionary<VideoProviderKind, ICameraVideoProvider> _video = new();
    private readonly Dictionary<VideoProviderKind, ICameraPtzProvider> _ptz = new();

    public CameraProviderRegistry(ICameraPtzProvider onvifPtz, IEnumerable<ICameraVideoProvider> videoProviders, ExternalVmsProvider? externalVms = null)
    {
        ArgumentNullException.ThrowIfNull(onvifPtz);
        foreach (var provider in videoProviders ?? Enumerable.Empty<ICameraVideoProvider>()) _video[provider.Kind] = provider;
        var external = externalVms ?? new ExternalVmsProvider();
        _video.TryAdd(VideoProviderKind.ExternalVms, external);
        _ptz[VideoProviderKind.Onvif] = onvifPtz;
        _ptz[VideoProviderKind.Rtsp] = onvifPtz;
        _ptz[VideoProviderKind.ExternalVms] = external;
    }

    /// <summary>영상 제공자. 없으면 null(시험 무늬 · 파일).</summary>
    public ICameraVideoProvider? GetVideo(VideoProviderKind kind) => _video.TryGetValue(kind, out var p) ? p : null;

    /// <summary>PTZ 제공자. 없으면 null(시험 무늬 · 파일 — "지원 안 함").</summary>
    public ICameraPtzProvider? GetPtz(VideoProviderKind kind) => _ptz.TryGetValue(kind, out var p) ? p : null;

    /// <summary>
    /// 운영 등록부 — OnvifSolution(Expect 끄기 · 준비 병렬화, T-01) 위에 T-01 PTZ 컨트롤러 하나를 두고
    /// ONVIF 영상 · PTZ 가 연결 캐시를 함께 쓴다. 호스트 프로세스에서만 부른다.
    /// </summary>
    public static CameraProviderRegistry CreateDefault(ILogService? log)
    {
        var builder = new ContainerBuilder();
        if (log is not null) builder.RegisterInstance(log).As<ILogService>().SingleInstance();
        builder.RegisterModule(new OnvifServiceModule(log));
        var container = builder.Build();
        var onvif = container.Resolve<IOnvifService>();
        var controller = new PtzController(onvif, log);
        return new CameraProviderRegistry(
            new OnvifPtzProvider(controller),
            new ICameraVideoProvider[] { new OnvifVideoProvider(controller), new RtspUrlVideoProvider() });
    }
}
