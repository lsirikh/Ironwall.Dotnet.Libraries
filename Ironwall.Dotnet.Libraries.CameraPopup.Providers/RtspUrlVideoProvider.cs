using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Onvif;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers;

/****************************************************************************
   Purpose      : RTSP 주소 영상 제공자 — 장비에 저장된 주소 + 계정(PRD camera-popup-modes FR-18)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/
/// <summary>
/// 장비에 저장된 RTSP 주소를 그대로 연다. 서버 v7.0 계약상 저장 주소엔 계정이 없어(userinfo 422) 계정을 싣는다 —
/// 주소에 이미 userinfo 가 있으면 운영자가 넣은 값을 존중한다(<c>RtspUrlCredentials.WithCredentials</c>, 61b99bdf 와 같은 규칙).
/// </summary>
public sealed class RtspUrlVideoProvider : ICameraVideoProvider
{
    public VideoProviderKind Kind => VideoProviderKind.Rtsp;

    public Task<StreamResolution> ResolveStreamAsync(string cameraId, VideoProviderInfo info, CancellationToken ct)
    {
        if (info is null || string.IsNullOrWhiteSpace(info.Uri))
            return Task.FromResult(StreamResolution.Fail(CameraErrorCodes.BadRequest, "empty uri"));
        return Task.FromResult(StreamResolution.Ok(WithCredentials(info.Uri!, info.Username, info.Password), "rtsp-url"));
    }

    /// <summary>
    /// 계정이 없는 주소에만 계정을 싣는다(순수). 사용자명이 비었거나 authority 에 이미 userinfo 가 있으면 원문.
    /// 퍼센트 인코딩은 <see cref="OnvifRtspUrlComposer"/> 가 한다.
    /// </summary>
    public static string WithCredentials(string url, string? username, string? password)
        => string.IsNullOrEmpty(username) || HasUserInfo(url) ? url : OnvifRtspUrlComposer.Compose(url, username, password);

    /// <summary>authority 에 userinfo(<c>user[:pw]@</c>)가 있는가. 경로 · 쿼리의 '@' 는 세지 않는다.</summary>
    public static bool HasUserInfo(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        var schemeIdx = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx < 0) return false;
        var start = schemeIdx + 3;
        var end = url.IndexOfAny(new[] { '/', '?', '#' }, start);
        if (end < 0) end = url.Length;
        return url.IndexOf('@', start, end - start) >= 0;
    }
}
