using System.Linq;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>
/// 카메라 도메인 모델(<see cref="ICameraDeviceModel"/>)의 스트리밍 정보를
/// 스트리밍 라이브러리 입력(<see cref="RtspConnectionInfo"/>)으로 <b>옮겨 담는</b> 어댑터(URL 조립 아님).
/// <para>
/// 우선순위(기본 preferSub=true — 작은 팝업+Hub CPU 디코딩이라 저해상 서브가 유리):
/// <c>Urls.RtspSub → Urls.RtspMain → (둘 다 없으면) IpAddress+IpPort</c>.
/// </para>
/// </summary>
public static class CameraConnectionAdapter
{
    /// <summary>
    /// 카메라 모델을 RTSP 연결정보로 변환. 유효 URL/IP가 없으면 null(→ '영상 없음').
    /// </summary>
    /// <param name="camera">카메라 장비 모델(null 허용 — null이면 null 반환).</param>
    /// <param name="preferSub">true면 서브스트림 우선(기본), false면 메인 우선.</param>
    public static RtspConnectionInfo? ToConnectionInfo(ICameraDeviceModel? camera, bool preferSub = true)
    {
        if (camera == null) return null;

        var main = camera.Urls?.RtspMain;
        var sub = camera.Urls?.RtspSub;

        var url = preferSub
            ? FirstNonEmpty(sub, main)
            : FirstNonEmpty(main, sub);

        var info = new RtspConnectionInfo
        {
            Username = camera.UserName ?? string.Empty,
            Password = camera.UserPassword ?? string.Empty,
            IpAddress = camera.IpAddress ?? string.Empty,
            Port = camera.IpPort > 0 ? camera.IpPort : 554,
            // 사용된 스트림 표시(0=Main, 1=Sub)
            StreamType = preferSub && !string.IsNullOrWhiteSpace(sub) ? 1 : 0,
        };

        // URL이 있으면 그대로 사용. 없으면 GetFullUrl()이 IpAddress+Port+StreamPath로 조립.
        if (!string.IsNullOrWhiteSpace(url))
            info.Url = url!;

        // 유효성: Url 또는 IpAddress가 있어야 의미. 둘 다 없으면 null.
        return info.IsValid() ? info : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
