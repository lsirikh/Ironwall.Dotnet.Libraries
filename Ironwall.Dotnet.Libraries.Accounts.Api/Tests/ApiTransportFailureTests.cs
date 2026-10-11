using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Ironwall.Dotnet.Libraries.Accounts.Api.Gateways;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// 원격지 설치 준비 — <b>연결 실패를 사람이 알 수 있게</b>.
/// <para>종전에는 인증서 신뢰 실패 · 연결 거부 · 시간 초과 · 이름 해석 실패가 전부
/// "HTTP 503: An error occurred while sending the request." 한 줄로 뭉개졌다. 원격 HTTPS 서버의 인증서를 이 PC 가
/// 신뢰하지 않으면 운영자는 "서버에 연결할 수 없습니다"만 보고 원인을 알 수 없었다.</para>
/// <para>가짜 <see cref="DelegatingHandler"/> 가 실제 <c>HttpClient</c> 가 던지는 모양의 예외를 던진다 — 네트워크에 나가지 않는다.
/// 인증서 검증을 끄는 우회는 없다(<see cref="should_not_contain_certificate_validation_bypass_anywhere"/>).</para>
/// </summary>
public class ApiTransportFailureTests
{
    private const string CertPhrase = "서버 인증서를 신뢰할 수 없습니다";

    /// <summary>.NET 8 HttpClient 가 신뢰할 수 없는 인증서에서 던지는 모양 그대로.</summary>
    private static HttpRequestException UntrustedCertificate() => new(
        HttpRequestError.SecureConnectionError,
        "The SSL connection could not be established, see inner exception.",
        new AuthenticationException("The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot"));

    [Fact]
    [Trait("Scenario", "DEP-211")]
    public async Task should_tell_certificate_is_untrusted_when_tls_handshake_fails()
    {
        // Arrange — 실제 ApiService 파이프라인 + 예외 주입 처리기
        var api = CreateApi(UntrustedCertificate());

        // Act
        using var res = await api.GetRequestAsync("devices/controllers");
        var parsed = await res.ToApiResponseAsync<object>();

        // Assert — 상위 분기는 종전 그대로(503 · SERVICE_UNAVAILABLE), 세부 사유 · 문구가 새로 남는다
        Assert.Equal(503, parsed.StatusCode);
        Assert.Equal("SERVICE_UNAVAILABLE", parsed.Error?.Code);
        Assert.Equal("CERT_UNTRUSTED", parsed.Error?.DetailsErrorCode);
        var text = parsed.ErrorText();
        Assert.Contains(CertPhrase, text);
        Assert.Contains("신뢰 저장소", text);
        Assert.DoesNotContain("An error occurred while sending the request", text);
    }

    public static IEnumerable<object[]> TransportCases() => new[]
    {
        new object[] { new HttpRequestException("No connection could be made because the target machine actively refused it. (localhost:8000)",
                            new SocketException((int)SocketError.ConnectionRefused)), 503, "CONNECTION_REFUSED", "연결을 거부" },
        new object[] { new HttpRequestException("No such host is known. (gop-server:8136)",
                            new SocketException((int)SocketError.HostNotFound)), 503, "NAME_RESOLUTION_FAILED", "이름을 찾을 수 없습니다" },
        new object[] { new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.", null), 503, "NAME_RESOLUTION_FAILED", "이름을 찾을 수 없습니다" },
        new object[] { new HttpRequestException("A connection attempt failed because the connected party did not properly respond after a period of time",
                            new SocketException((int)SocketError.TimedOut)), 504, "CONNECT_TIMEOUT", "시간이 초과" },
        new object[] { new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 10 seconds elapsing.",
                            new TimeoutException()), 504, "REQUEST_TIMEOUT", "시간이 초과" },
        new object[] { new HttpRequestException("conn refused"), 503, "CONNECTION_FAILED", "서버에 연결할 수 없습니다" },
        new object[] { new HttpRequestException("The SSL connection could not be established, see inner exception.",
                            new AuthenticationException("The remote certificate was rejected by the provided RemoteCertificateValidationCallback.")), 503, "CERT_UNTRUSTED", CertPhrase },
    };

    [Theory]
    [Trait("Scenario", "DEP-212")]
    [MemberData(nameof(TransportCases))]
    public async Task should_classify_transport_failure_into_code_and_message(Exception thrown, int status, string subCode, string phrase)
    {
        // Arrange
        var api = CreateApi(thrown);

        // Act
        using var res = await api.PostRequestAsync("auth/login", new { login_id = "x" });
        var parsed = await res.ToApiResponseAsync<object>();

        // Assert
        Assert.Equal(status, parsed.StatusCode);
        Assert.Equal(subCode, parsed.Error?.DetailsErrorCode);
        Assert.Contains(phrase, parsed.ErrorText());
        // 상위 분기 무회귀 — 503 은 서버 불가, 504 는 시간 초과로 판정된다
        if (status == 503) Assert.True(ApiStatusHelper.IsServerUnavailable(parsed.StatusCode, parsed.Error));
        else Assert.True(parsed.IsTimeout());
    }

    [Fact]
    [Trait("Scenario", "DEP-213")]
    public async Task should_carry_certificate_reason_into_login_outcome_when_login_cannot_reach_server()
    {
        // Arrange — 로그인 경로 전체: ApiService → AccountApiService → ApiAccountGateway
        var accountApi = new AccountApiService(CreateApi(UntrustedCertificate()));
        var gateway = new ApiAccountGateway(accountApi, new TokenStorageService(), new PermissionService());

        // Act
        var outcome = await gateway.AuthenticateAsync("e2e", "pw");

        // Assert — 로그인 화면이 "아이디 또는 비밀번호" 나 뭉개진 503 이 아니라 인증서 사유를 받는다
        Assert.False(outcome.Success);
        Assert.Equal("CERT_UNTRUSTED", outcome.ErrorCode);
        Assert.Contains(CertPhrase, outcome.Message);
    }

    [Fact]
    [Trait("Scenario", "DEP-214")]
    public async Task should_carry_certificate_reason_into_sso_exchange_result()
    {
        // Arrange — SSO 교환도 같은 ApiService 를 탄다(원격 설치에서 SSO 붙이기의 첫 관문)
        var accountApi = new AccountApiService(CreateApi(UntrustedCertificate()));

        // Act
        var result = await accountApi.SsoExchangeAsync("sso-token");

        // Assert — 사유 문구 · 하위 사유가 남고, 재시도 가능 판정은 종전대로 false(같은 인증서로는 다시 해도 실패)
        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Contains(CertPhrase, result.Message);
        Assert.Equal("CERT_UNTRUSTED", result.Reason);
        Assert.False(result.Retryable);
    }

    [Fact]
    [Trait("Scenario", "DEP-215")]
    public void should_not_contain_certificate_validation_bypass_anywhere()
    {
        // 인증서 신뢰 실패의 해법은 "신뢰 저장소에 넣기"다 — 출하 라이브러리(Ironwall.Dotnet.*)에는 검증을 끄는 코드가 없어야 한다.
        // 주석 속 이름은 걸지 않고 대입 · 생성 · 위험 검증기 사용만 찾는다. (이 파일 자신에 걸리지 않게 패턴을 쪼개 둔다.)
        // 개발 도구(tools\ 의 로컬 왕복 하네스)는 GIS 설치본에 들어가지 않으므로 범위 밖이다.
        var forbidden = new[]
        {
            new System.Text.RegularExpressions.Regex("ServerCertificateCustom" + @"ValidationCallback\s*="),
            new System.Text.RegularExpressions.Regex("DangerousAcceptAny" + "ServerCertificateValidator"),
            new System.Text.RegularExpressions.Regex("ServerCertificate" + @"ValidationCallback\s*\+?="),
            new System.Text.RegularExpressions.Regex("RemoteCertificate" + @"ValidationCallback\s*\+?="),
            new System.Text.RegularExpressions.Regex(@"new\s+" + "RemoteCertificate" + "ValidationCallback"),
        };

        var root = FindRepositoryRoot();
        var hits = new List<string>();
        var scanned = 0;
        foreach (var project in Directory.EnumerateDirectories(root, "Ironwall.Dotnet.*"))
        {
            foreach (var file in Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, file);
                if (IsExcluded(rel)) continue;
                scanned++;
                var text = File.ReadAllText(file);
                foreach (var f in forbidden)
                    if (f.IsMatch(text)) hits.Add($"{rel}: {f}");
            }
        }

        Assert.True(scanned > 100, $"훑은 파일이 너무 적다({scanned}) — 저장소 뿌리를 잘못 찾았다");
        Assert.True(hits.Count == 0, "인증서 검증 우회 코드가 있다:\n" + string.Join("\n", hits));
    }

    #region - 도우미 -
    private static ApiService CreateApi(Exception toThrow)
    {
        var api = new ApiService(null, new ApiSetupModel { Url = "https://gop-server.invalid:8136/api", Timeout = 5 },
                                 new ThrowingHandler(toThrow));
        api.Initialize();
        return api;
    }

    /// <summary>요청을 내보내지 않고 주어진 예외를 던진다 — 실제 <c>HttpClient</c> 가 연결 단계에서 던지는 자리.</summary>
    private sealed class ThrowingHandler : DelegatingHandler
    {
        private readonly Exception _ex;
        public ThrowingHandler(Exception ex) => _ex = ex;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _ex;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    /// <summary>빌드 산출물 · 외부 원본(벤더 · 서드파티 · 다른 worktree 사본)은 우리 코드가 아니다.</summary>
    private static bool IsExcluded(string relative)
    {
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var p in parts)
        {
            if (p is "bin" or "obj" or "third_party" or "GMap.NET" or "node_modules") return true;
            if (p.StartsWith(".", StringComparison.Ordinal)) return true;   // .harness-worktrees · .claude 등
        }
        return false;
    }
    #endregion
}
