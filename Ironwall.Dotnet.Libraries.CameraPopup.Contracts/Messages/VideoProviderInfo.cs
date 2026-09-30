using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 영상 제공자 정보(FR-17/18). 계정은 같은 사용자 전용 파이프로만 오가며
/// <see cref="ToString"/> 은 비밀번호를 가린다 — 로그에 그대로 찍어도 새지 않게.
/// </summary>
public sealed class VideoProviderInfo
{
    public VideoProviderKind Kind { get; init; } = VideoProviderKind.TestPattern;

    /// <summary>RTSP 주소 · 파일 경로 · ONVIF 서비스 주소.</summary>
    public string? Uri { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    /// <summary>ONVIF 프로필 토큰 등 제공자별 부가 값.</summary>
    public string? ProfileToken { get; init; }

    /// <summary>스트림 열기 제한 시간(FR-26). 0 이하이면 호스트 기본값.</summary>
    public int OpenTimeoutMs { get; init; }

    public override string ToString()
        => $"{Kind} uri={RedactUri(Uri)} user={(string.IsNullOrEmpty(Username) ? "-" : Username)} pwd={(string.IsNullOrEmpty(Password) ? "-" : "***")}";

    /// <summary>주소 안의 <c>user:pass@</c> 를 가린다(순수 함수).</summary>
    public static string RedactUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return "-";
        int scheme = uri.IndexOf("://", StringComparison.Ordinal);
        int at = uri.IndexOf('@');
        if (scheme < 0 || at < 0 || at < scheme) return uri;
        return uri.Substring(0, scheme + 3) + "***@" + uri.Substring(at + 1);
    }
}
