using System;
using System.Text.RegularExpressions;

namespace Ironwall.Dotnet.Libraries.Streaming.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:14:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// RTSP URL 유효성 검증 헬퍼
/// </summary>
public static class RtspUrlValidator
{
    private static readonly Regex RtspUrlPattern = new(
    @"^rtsp:\/\/(([a-zA-Z0-9_]+):([^@]+)@)?([a-zA-Z0-9.-]+)(:[0-9]+)?(\/.*)?$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // 기본 형식 검증
        if (!url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            return false;

        // 정규식 검증
        return RtspUrlPattern.IsMatch(url);
    }

    public static bool TryParseUrl(string url, out string host, out int port, out string path)
    {
        host = string.Empty;
        port = 554; // 기본 RTSP 포트
        path = null;

        if (!IsValidUrl(url))
            return false;

        try
        {
            var uri = new Uri(url);
            host = uri.Host;
            port = uri.Port > 0 ? uri.Port : 554;
            path = uri.AbsolutePath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string BuildUrl(
        string host,
        int port = 554,
        string username = null,
        string password = null,
        string path = null)
    {
        if (string.IsNullOrEmpty(host))
            return string.Empty;

        var auth = string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)
            ? string.Empty
            : $"{username}:{password}@";

        var pathPart = string.IsNullOrEmpty(path) ? string.Empty : $"/{path.TrimStart('/')}";

        return $"rtsp://{auth}{host}:{port}{pathPart}";
    }

    public static string SanitizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        // 비밀번호 마스킹
        var pattern = @"(rtsp:\/\/[^:]+:)([^@]+)(@.+)";
        var match = Regex.Match(url, pattern);

        if (match.Success)
        {
            return $"{match.Groups[1].Value}****{match.Groups[3].Value}";
        }

        return url;
    }
}