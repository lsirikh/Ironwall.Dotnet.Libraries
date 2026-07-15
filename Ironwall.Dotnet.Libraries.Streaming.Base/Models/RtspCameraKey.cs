using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;

/****************************************************************************
   Purpose      : 카메라 스트림 공유 키 파생(순수 로직, WPF 무의존)
   Created By   : Claude Code
   Created On   : 2026-07-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 카메라 스트림 공유 키 파생 — Hub / SharedSession / Row가 공통 사용.
/// <para>
/// 키 = <c>host:port/path[?query]</c> — <b>자격증명만 제외</b>(같은 스트림·로그 안전),
/// <b>쿼리는 포함</b>한다. RTSP에서 쿼리는 채널/스트림 선택자(예: <c>?channel=2&amp;subtype=1</c>)라
/// 제외하면 서로 다른 카메라 스트림이 같은 키로 병합되어 N개 뷰어가
/// 동일 영상을 공유하는 버그가 된다(A/B 동일영상 — 2026-07-15 분석
/// docs/analyses/Rtsp_Popup_Streaming_Ptz-analysis.md §5).
/// </para>
/// <remarks>단위테스트가 이 파일을 소스링크로 직접 검증한다(tests/GMaps.Ui.Tests — 복제 금지).</remarks>
/// </summary>
public static class RtspCameraKey
{
    /// <summary>
    /// 풀 URL(파싱 가능 시) 또는 IP 구성요소로부터 스트림 공유 키를 만든다.
    /// </summary>
    /// <param name="fullUrl">완전한 RTSP URL(자격증명 포함 가능 — 키에는 미포함).</param>
    /// <param name="ipAddress">URL 파싱 실패/부재 시 폴백 구성요소.</param>
    /// <param name="port">폴백 포트.</param>
    /// <param name="streamPath">폴백 경로.</param>
    public static string Derive(string? fullUrl, string? ipAddress, int port, string? streamPath)
    {
        if (!string.IsNullOrEmpty(fullUrl) && Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
        {
            // rtsp는 .NET 기본 스킴이 아니라 포트 미지정 시 -1 → RTSP 표준 554 폴백.
            var p = uri.Port > 0 ? uri.Port : 554;
            var path = uri.AbsolutePath.TrimStart('/');
            // uri.Query = "?a=b" 또는 빈 문자열. Host/Port/Path/Query 어디에도 자격증명 없음 → 키는 로그 안전.
            return $"{uri.Host}:{p}/{path}{uri.Query}";
        }
        return $"{ipAddress}:{port}/{streamPath}";
    }
}
