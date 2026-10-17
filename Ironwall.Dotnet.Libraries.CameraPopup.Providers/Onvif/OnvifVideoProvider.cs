using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

/****************************************************************************
   Purpose      : ONVIF 영상 제공자 — GetProfiles/GetStreamUri + 계정 결합(PRD camera-popup-modes FR-18)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// ONVIF 로 재생 주소를 얻는다(프로필: 해상도 최소 → 비오디오 → 원 순서, <see cref="Ptz.OnvifProfileSelector"/>).
/// 얻은 주소엔 계정이 없어(VER-01 실측) 카메라 계정을 싣는다(<see cref="OnvifRtspUrlComposer"/>).
/// 실패하면 <see cref="VideoProviderInfo.FallbackUri"/>(장비에 저장된 주소)로 폴백 — 현행 "URL 조회 폴백"(FR-06) 유지.
/// 연결 · 프로필은 PTZ 와 같은 캐시를 쓴다(이중 초기화 없음).
/// </summary>
public sealed class OnvifVideoProvider : ICameraVideoProvider
{
    private readonly IPtzController _controller;

    public OnvifVideoProvider(IPtzController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public VideoProviderKind Kind => VideoProviderKind.Onvif;

    public async Task<StreamResolution> ResolveStreamAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
    {
        if (info is null) return StreamResolution.Fail(CameraErrorCodes.BadRequest, "no provider info");
        string? reason;
        if (string.IsNullOrWhiteSpace(info.Host))
        {
            reason = "no host";
        }
        else
        {
            var uri = await OnvifPtzProvider.Bounded(
                _controller.ResolveStreamUriAsync(cameraId, OnvifPtzProvider.ToConnection(info), info.PreferSubStream, ct,
                    info.TargetWidth, info.TargetHeight), ct, null).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(uri))
                return StreamResolution.Ok(OnvifRtspUrlComposer.Compose(uri!, info.Username, info.Password), "onvif");
            reason = ct.IsCancellationRequested ? "timeout" : "GetStreamUri failed";
        }

        if (!string.IsNullOrWhiteSpace(info.FallbackUri))
            return StreamResolution.Ok(RtspUrlVideoProvider.WithCredentials(info.FallbackUri!, info.Username, info.Password), $"fallback ({reason})");
        return StreamResolution.Fail(ct.IsCancellationRequested ? CameraErrorCodes.Timeout : CameraErrorCodes.ResolveFailed, reason);
    }
}
