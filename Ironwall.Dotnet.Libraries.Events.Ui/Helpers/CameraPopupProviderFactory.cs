using Ironwall.Dotnet.Monitoring.Models.Devices;
using Contract = Ironwall.Dotnet.Libraries.CameraPopup.Contracts;
using SettingsKind = Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.VideoProviderKind;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;

/****************************************************************************
   Purpose      : 카메라 팝업 제공자 정보 만들기(순수) — 설정 제공자 종류 + 카메라 장비 → 호스트 계약
                  (PRD camera-popup-modes FR-04 · FR-17/18, T-02). 더블클릭 팝업(GMaps.Ui MapViewModel)과
                  이벤트 창(EventWindowPlanning.BuildProvider)이 같이 쓴다 — 두 경로가 어긋나지 않게 한 곳(2026-09-30, GMaps.Ui 에서 이관).
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 현행 규칙을 그대로 옮긴다(동작 변경 없음 — FR-04):
/// <list type="bullet">
/// <item>ONVIF(옛 "Onvif 조회") — 카메라 IP 가 있어야 한다. 호스트가 GetStreamUri 로 주소를 얻고, 실패하면 저장 주소(FallbackUri)로.
/// IP 가 없으면 RTSP 주소 제공자로 내려간다(옛 <c>useOnvif = … &amp;&amp; IP 있음</c>).</item>
/// <item>RTSP 주소(옛 "Url") — 저장 주소(<see cref="CameraConnectionAdapter"/>: 서브 → 메인 → IP 조립, 계정 결합)가 있어야 한다.</item>
/// <item>PTZ 는 두 경우 모두 ONVIF(Host · Port · 계정) — 옛 팝업도 소스와 무관하게 ONVIF 로 PTZ 를 했다.</item>
/// <item>외부 VMS — 자리만. 호스트가 "지원 안 함"으로 답한다.</item>
/// </list>
/// 재생할 것이 전혀 없으면 null(옛 "RTSP URL 없음" — 팝업을 열지 않는다).
/// </summary>
public static class CameraPopupProviderFactory
{
    /// <summary>ONVIF device_service 기본 포트(옛 EnsurePtzReady 규칙 — IpPort 가 0 이면 80).</summary>
    public const int DefaultOnvifPort = 80;

    public static Contract.Messages.VideoProviderInfo? Build(ICameraDeviceModel? camera, SettingsKind kind)
    {
        if (camera is null) return null;
        var storedUrl = CameraConnectionAdapter.ToConnectionInfo(camera, preferSub: true)?.GetFullUrl();
        bool hasHost = !string.IsNullOrWhiteSpace(camera.IpAddress);
        int port = camera.IpPort > 0 ? camera.IpPort : DefaultOnvifPort;

        if (kind == SettingsKind.ExternalVms)
        {
            return new Contract.Messages.VideoProviderInfo
            {
                Kind = Contract.Protocol.VideoProviderKind.ExternalVms,
                Host = camera.IpAddress,
                Port = port,
                Username = camera.UserName,
                Password = camera.UserPassword,
            };
        }

        if (kind == SettingsKind.Onvif && hasHost)
        {
            return new Contract.Messages.VideoProviderInfo
            {
                Kind = Contract.Protocol.VideoProviderKind.Onvif,
                Host = camera.IpAddress,
                Port = port,
                Username = camera.UserName,
                Password = camera.UserPassword,
                PreferSubStream = true,
                FallbackUri = string.IsNullOrWhiteSpace(storedUrl) ? null : storedUrl,
            };
        }

        if (string.IsNullOrWhiteSpace(storedUrl)) return null;
        return new Contract.Messages.VideoProviderInfo
        {
            Kind = Contract.Protocol.VideoProviderKind.Rtsp,
            Uri = storedUrl,
            Host = hasHost ? camera.IpAddress : null,
            Port = port,
            Username = camera.UserName,
            Password = camera.UserPassword,
        };
    }
}
