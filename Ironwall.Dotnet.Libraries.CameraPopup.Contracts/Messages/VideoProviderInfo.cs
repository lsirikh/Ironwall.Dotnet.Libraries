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

    /// <summary>
    /// 화면에 그릴 크기(물리 픽셀) — 호스트가 채운다(타일 · 오버레이 상자). 0 보다 크면 ONVIF 프로필을
    /// "이 크기를 덮는 가장 낮은 해상도"로 고른다(작은 타일 = 서브, 크게 보기 · 큰 창 = 메인). 0 이면 <see cref="PreferSubStream"/> 만 본다.
    /// </summary>
    public int TargetWidth { get; init; }

    public int TargetHeight { get; init; }

    /// <summary>
    /// 상자를 꽉 채워 그린다(비율 무시 · 검은 띠 없음). 기본 false = 비율 유지(띠). ROI 설정 창만 켠다 —
    /// 점이 상자(기준 해상도 틀) 기준 0~1 로 저장되므로, 비율이 다른 서브 스트림(720×480 등)이 골라져도 메인 영상 위치와 맞게.
    /// 메인 · 서브는 같은 시야라 늘려 그리면 위치가 맞는다. 구판 호스트는 이 칸을 모른 채 무시한다(예전처럼 띠).
    /// </summary>
    public bool FillBox { get; init; }

    /// <summary>ONVIF 로 주소를 못 얻었을 때 쓸 저장 주소(현행 "URL 조회 폴백").</summary>
    public string? FallbackUri { get; init; }

    /// <summary>그릴 크기만 바꾼 사본(호스트가 타일 · 오버레이 상자 크기를 실어 제공자에 넘긴다).</summary>
    public VideoProviderInfo WithTarget(int width, int height) => Copy(width, height, FillBox);

    /// <summary>상자 꽉 채우기만 바꾼 사본(ROI 설정 창).</summary>
    public VideoProviderInfo WithFillBox(bool fillBox) => Copy(TargetWidth, TargetHeight, fillBox);

    private VideoProviderInfo Copy(int width, int height, bool fillBox) => new()
    {
        Kind = Kind,
        Uri = Uri,
        Username = Username,
        Password = Password,
        ProfileToken = ProfileToken,
        OpenTimeoutMs = OpenTimeoutMs,
        Host = Host,
        Port = Port,
        PreferSubStream = PreferSubStream,
        FallbackUri = FallbackUri,
        TargetWidth = Math.Max(0, width),
        TargetHeight = Math.Max(0, height),
        FillBox = fillBox,
    };

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
