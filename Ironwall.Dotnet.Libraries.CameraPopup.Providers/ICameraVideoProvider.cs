using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers;

/****************************************************************************
   Purpose      : 영상 제공자 추상화(PRD camera-popup-modes FR-17)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// "이 카메라를 재생하려면 어떤 주소를 여는가"를 정한다. 더블클릭 오버레이 · 이벤트 창 타일은 이 추상만 부른다 —
/// ONVIF 를 직접 모른다. 구현은 호스트 프로세스 안에서만 돈다(§0).
/// <para>계약: 절대 던지지 않는다 — 단, 제공자가 아예 지원하지 않으면 <see cref="NotSupportedException"/>
/// (호스트가 "지원 안 함"으로 알린다). 취소 · 시간 초과는 실패 결과로 돌려준다.</para>
/// </summary>
public interface ICameraVideoProvider
{
    VideoProviderKind Kind { get; }

    /// <summary>재생 주소(계정 포함). <paramref name="ct"/> 가 끝나면 즉시 실패로 돌아온다.</summary>
    Task<StreamResolution> ResolveStreamAsync(string cameraId, VideoProviderInfo info, CancellationToken ct);
}

/// <summary>영상 주소 결과. <see cref="ToString"/> 은 계정을 가린다.</summary>
public sealed record StreamResolution(bool Success, string? Uri, string? ErrorCode, string? Detail)
{
    public static StreamResolution Ok(string uri, string? detail = null) => new(true, uri, null, detail);

    public static StreamResolution Fail(string errorCode, string? detail = null) => new(false, null, errorCode, detail);

    public override string ToString()
        => Success ? $"ok uri={VideoProviderInfo.RedactUri(Uri)} {Detail}" : $"fail {ErrorCode} {Detail}";
}
