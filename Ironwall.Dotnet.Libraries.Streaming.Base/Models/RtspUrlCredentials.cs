using System;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

/****************************************************************************
   Purpose      : RTSP URL 계정 결합 · 로그 마스킹(순수 로직, WPF 무의존)
   Created By   : Claude Code
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카메라 계정을 RTSP URL 의 userinfo 로 싣고, 로그에 찍기 전에 가린다.
/// <para>
/// 서버 v7.0 계약은 <c>connection.urls</c> 에 계정을 넣으면 422 로 거부하고 계정은
/// <c>connection.credentials</c> 에만 둔다. 그래서 Url 모드 팝업이 받은 RTSP URL 은 항상 계정이 없고,
/// 그대로 LibVLC 에 넘기면 계정이 필요한 카메라는 DESCRIBE 401 로 영상이 비었다(192.168.202.108 실측).
/// LibVLC 는 URL userinfo 를 RTSP Basic/Digest 협상에 쓴다 — Onvif 모드가 이미 같은 방식으로 재생한다
/// (<c>OnvifRtspUrlComposer</c>). 공유 세션 키(<see cref="RtspCameraKey"/>)는 userinfo 를 보지 않는다.
/// </para>
/// </summary>
public static class RtspUrlCredentials
{
    // "://" 뒤 authority 안의 userinfo — 경로·쿼리·조각 앞에서 멈추고, 마지막 '@' 까지(인코딩 안 된 '@' 비밀번호도 가린다).
    private static readonly Regex UserInfoPattern = new(@"(?<=://)[^/?#\s]+@", RegexOptions.Compiled);

    /// <summary>URL 의 authority 에 userinfo(<c>user[:pw]@</c>)가 있는가. 쿼리·경로의 '@' 는 세지 않는다.</summary>
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

    /// <summary>
    /// 계정이 없는 URL 에 계정을 싣는다. 사용자명이 비었거나, URL 에 이미 userinfo 가 있거나(운영자가 직접 넣은 값 존중),
    /// 스킴이 없으면 원문 그대로 돌려준다. 사용자명·비밀번호는 퍼센트 인코딩한다(<c>@ : / # ? %</c> 가 URL 경계를 깨지 않게).
    /// </summary>
    public static string WithCredentials(string? url, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(url)) return url ?? string.Empty;
        if (string.IsNullOrEmpty(username) || HasUserInfo(url)) return url;

        var schemeIdx = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx < 0) return url;
        var hostStart = schemeIdx + 3;
        if (hostStart >= url.Length) return url;

        var cred = Uri.EscapeDataString(username)
                 + (string.IsNullOrEmpty(password) ? string.Empty : ":" + Uri.EscapeDataString(password));
        return string.Concat(url.AsSpan(0, hostStart), cred, "@", url.AsSpan(hostStart));
    }

    /// <summary>로그용 — 문자열 안의 모든 URL userinfo 를 <c>***@</c> 로 가린다(예외 메시지 같은 자유 텍스트에도 쓴다).</summary>
    public static string Mask(string? text)
        => string.IsNullOrEmpty(text) ? string.Empty : UserInfoPattern.Replace(text, "***@");
}
