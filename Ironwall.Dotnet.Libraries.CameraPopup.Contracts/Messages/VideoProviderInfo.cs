using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 영상 제공자 정보(FR-17/18). 계정은 같은 사용자 전용 파이프로만 오가며
/// <see cref="ToString"/> 은 비밀번호를 가린다 — 로그에 그대로 찍어도 새지 않게.
/// <para>종류별로 쓰는 칸:
/// <list type="bullet">
/// <item><see cref="VideoProviderKind.Onvif"/> — <see cref="Host"/> · <see cref="Port"/>(ONVIF device_service) · 계정 ·
/// <see cref="PreferSubStream"/>. 호스트가 GetStreamUri 로 주소를 얻고, 실패하면 <see cref="FallbackUri"/>.</item>
/// <item><see cref="VideoProviderKind.Rtsp"/> — <see cref="Uri"/>(장비에 저장된 주소). 주소에 계정이 없으면 호스트가 싣는다.
/// PTZ 는 <see cref="Host"/> · <see cref="Port"/> 로 ONVIF 를 쓴다(현행 더블클릭 팝업과 같은 동작).</item>
/// <item><see cref="VideoProviderKind.ExternalVms"/> — 자리만. 호스트는 "지원 안 함".</item>
/// </list></para>
/// </summary>
public sealed class VideoProviderInfo
{
    public VideoProviderKind Kind { get; init; } = VideoProviderKind.TestPattern;

    /// <summary>RTSP 주소 · 파일 경로.</summary>
    public string? Uri { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    /// <summary>ONVIF 프로필 토큰 등 제공자별 부가 값.</summary>
    public string? ProfileToken { get; init; }

    /// <summary>스트림 열기 제한 시간(FR-26). 0 이하이면 호스트 기본값.</summary>
    public int OpenTimeoutMs { get; init; }

    /// <summary>카메라 IP · 호스트 이름(ONVIF 영상 주소 조회 · PTZ).</summary>
    public string? Host { get; init; }

    /// <summary>ONVIF device_service 포트. 0 이하이면 80.</summary>
    public int Port { get; init; }

    /// <summary>ONVIF 프로필을 고를 때 낮은 해상도(서브 스트림)를 먼저 — 작은 상자 · 타일에 유리.</summary>
    public bool PreferSubStream { get; init; } = true;

    /// <summary>ONVIF 로 주소를 못 얻었을 때 쓸 저장 주소(현행 "URL 조회 폴백").</summary>
    public string? FallbackUri { get; init; }

    public override string ToString()
        => $"{Kind} uri={RedactUri(Uri)} host={(string.IsNullOrEmpty(Host) ? "-" : Host)}:{(Port > 0 ? Port : 80)} " +
           $"fallback={RedactUri(FallbackUri)} user={(string.IsNullOrEmpty(Username) ? "-" : Username)} pwd={(string.IsNullOrEmpty(Password) ? "-" : "***")}";

    /// <summary>주소 안의 <c>user:pass@</c> 를 가린다(순수 함수). authority 의 마지막 '@' 까지 가린다 — 비밀번호 안의 '@' 도.</summary>
    public static string RedactUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return "-";
        int scheme = uri.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0) return uri;
        int start = scheme + 3;
        int end = uri.IndexOfAny(new[] { '/', '?', '#' }, start);
        if (end < 0) end = uri.Length;
        int at = uri.LastIndexOf('@', end - 1, end - start);
        if (at < start) return uri;
        return uri.Substring(0, start) + "***@" + uri.Substring(at + 1);
    }
}
