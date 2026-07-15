using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : ONVIF 조회 RTSP URL 자격증명 조합(순수 로직) — CameraPopup_RtspSource_Priority FR-04
   Created By   : Claude Code
   Created On   : 2026-07-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// ONVIF <c>GetStreamUri</c> 반환 URL에 카메라 계정/비번을 임베드한다(단위테스트 소스링크 대상).
/// <para>
/// 반환 URL은 자격증명이 없다(VER-01 실측: <c>rtsp://192.168.202.66/video1s</c>) →
/// <c>rtsp://user:pass@host/...</c>로 결합해 LibVLC가 RTSP 401(Basic/Digest-MD5)
/// 자동 협상에 쓰게 한다. 특수문자는 URL-인코딩(<see cref="Uri.EscapeDataString"/>),
/// 기존 userinfo가 있으면 치환, 경로/쿼리는 원문 보존(파싱·재조립 없이 문자열 삽입 — NFR-02/V-05).
/// </para>
/// </summary>
public static class OnvifRtspUrlComposer
{
    /// <summary>자격증명 임베드. username이 비면 원문 그대로(익명 카메라).</summary>
    public static string Compose(string streamUri, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(streamUri) || string.IsNullOrEmpty(username))
            return streamUri ?? string.Empty;

        var schemeIdx = streamUri.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx < 0) return streamUri;   // 스킴 없는 값 — 손대지 않음
        var authorityStart = schemeIdx + 3;

        var pathStart = streamUri.IndexOf('/', authorityStart);
        var authorityEnd = pathStart < 0 ? streamUri.Length : pathStart;
        if (authorityEnd <= authorityStart) return streamUri;   // 호스트 없음 — 손대지 않음

        // authority 구간 내 마지막 '@' = 기존 userinfo 경계(있으면 치환). 경로/쿼리의 '@'는 검사 범위 밖.
        var at = streamUri.LastIndexOf('@', authorityEnd - 1);
        var hostStart = (at >= authorityStart) ? at + 1 : authorityStart;

        var cred = Uri.EscapeDataString(username)
                 + (string.IsNullOrEmpty(password) ? string.Empty : ":" + Uri.EscapeDataString(password));
        return string.Concat(streamUri.AsSpan(0, authorityStart), cred, "@", streamUri.AsSpan(hostStart));
    }
}
