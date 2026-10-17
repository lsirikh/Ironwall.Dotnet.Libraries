using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Ironwall.Dotnet.Libraries.Api.Services;

/// <summary>
/// 서버에 <b>닿지 못한</b> 요청의 사유 분류 — 인증서 신뢰 실패 · 연결 거부 · 시간 초과 · 이름 해석 실패.
/// <para><b>왜</b>: 종전에는 전부 <c>HTTP 503: An error occurred while sending the request.</c> 한 줄로 뭉개져
/// 원격지 설치에서 "서버 HTTPS 인증서를 이 PC 가 신뢰하지 않는다" 같은 원인을 운영자가 알 수 없었다.</para>
/// <para><b>싣는 자리</b>(<see cref="ApiService.BuildExceptionResponse"/>): 상위 <c>error.code</c> 는 종전과 같다
/// (503 <c>SERVICE_UNAVAILABLE</c> · 504 <c>GATEWAY_TIMEOUT</c> — 기존 분기 무회귀). 세부 사유는 저장소의
/// 안정 sub-code 자리 <c>error.details.error_code</c> 에, 사람용 문구는 <c>error.message</c> 에 싣는다.</para>
/// <para>⚠ 아래 코드는 서버 어휘가 아니라 <b>클라 로컬</b> 코드다. 인증서 신뢰 실패의 해법은 신뢰 저장소에 넣는 것이며
/// 검증을 끄는 우회는 두지 않는다.</para>
/// </summary>
public static class ApiTransportFailure
{
    #region - 클라 로컬 sub-code -
    /// <summary>TLS 핸드셰이크에서 서버 인증서를 신뢰하지 못했다(<see cref="AuthenticationException"/>).</summary>
    public const string CertificateUntrusted = "CERT_UNTRUSTED";

    /// <summary>서버가 연결을 거부했다(포트에 듣는 프로세스 없음 · 방화벽 거부).</summary>
    public const string ConnectionRefused = "CONNECTION_REFUSED";

    /// <summary>연결 단계에서 시간 초과(응답 없는 주소 · 방화벽이 버림).</summary>
    public const string ConnectTimeout = "CONNECT_TIMEOUT";

    /// <summary>서버 이름을 IP 로 바꾸지 못했다(DNS · hosts).</summary>
    public const string NameResolutionFailed = "NAME_RESOLUTION_FAILED";

    /// <summary>그 밖의 연결 실패(분류 근거 없음).</summary>
    public const string ConnectionFailed = "CONNECTION_FAILED";

    /// <summary>요청 전체 시간 초과(<c>HttpClient.Timeout</c>) — 서버가 받았는지 알 수 없다.</summary>
    public const string RequestTimeout = "REQUEST_TIMEOUT";
    #endregion

    /// <summary>예외 → sub-code. 연결 실패가 아닌 예외(호출부 버그 등)는 <c>null</c>.</summary>
    public static string? Classify(Exception ex)
    {
        if (ex is null) return null;

        // 원인 사슬을 끝까지 본다 — HttpClient 는 실제 사유를 InnerException 에 싼다.
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is AuthenticationException) return CertificateUntrusted;
            if (e is SocketException s)
            {
                switch (s.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused: return ConnectionRefused;
                    case SocketError.TimedOut: return ConnectTimeout;
                    case SocketError.HostNotFound:
                    case SocketError.NoData:
                    case SocketError.TryAgain: return NameResolutionFailed;
                }
            }
        }

        if (ex is HttpRequestException h)
        {
            return h.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => NameResolutionFailed,
                HttpRequestError.SecureConnectionError => CertificateUntrusted,
                _ => ConnectionFailed,
            };
        }

        if (ex is TaskCanceledException) return RequestTimeout;
        return null;
    }

    /// <summary>sub-code 가 이 분류의 코드인가.</summary>
    public static bool IsTransportCode(string? code) => code is CertificateUntrusted or ConnectionRefused or ConnectTimeout
                                                               or NameResolutionFailed or ConnectionFailed or RequestTimeout;

    /// <summary>시간 초과 계열인가 — 합성 상태를 504 로 만든다(로그인 "시간 초과" 문구 · <c>IsTimeout</c> 판정과 맞춘다).</summary>
    public static bool IsTimeout(string? code) => code is ConnectTimeout or RequestTimeout;

    /// <summary>운영자에게 보일 한 줄 문구(다음 행동 포함). 모르는 코드는 <c>null</c>.</summary>
    public static string? Describe(string? code) => code switch
    {
        CertificateUntrusted => "서버 인증서를 신뢰할 수 없습니다 — 서버 인증서(또는 발급한 루트 CA)를 이 PC 의 신뢰 저장소(신뢰할 수 있는 루트 인증 기관)에 넣어야 합니다.",
        ConnectionRefused => "서버가 연결을 거부했습니다 — 서버 주소 · 포트와 서버가 실행 중인지 확인하세요.",
        ConnectTimeout => "서버 연결 시간이 초과되었습니다 — 네트워크 · 방화벽과 서버 주소를 확인하세요.",
        NameResolutionFailed => "서버 이름을 찾을 수 없습니다 — 서버 주소(호스트 이름)와 DNS 설정을 확인하세요.",
        ConnectionFailed => "서버에 연결할 수 없습니다 — 네트워크와 서버 주소를 확인하세요.",
        RequestTimeout => "요청 시간이 초과되었습니다 — 서버 응답이 없습니다.",
        _ => null,
    };

    /// <summary>진단 로그용 — 원인 사슬의 가장 안쪽 메시지(바깥은 "see inner exception" 이라 쓸모가 없다).</summary>
    public static string InnermostMessage(Exception ex)
    {
        var e = ex;
        while (e.InnerException != null) e = e.InnerException;
        return e.Message;
    }
}
