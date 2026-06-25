using System.Net;
using System.Net.Http;
using Ironwall.Dotnet.Libraries.Api.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// FR-6 예외→상태코드 매핑 검증 (ApiService.BuildExceptionResponse, internal via InternalsVisibleTo).
/// 기존 catch→BadRequest(400) 일괄변환 폐지 → 타임아웃 504 / 연결실패 503 / 그 외 500.
/// </summary>
public class ApiServiceErrorMappingTests
{
    [Fact]
    public void should_map_timeout_to_gateway_timeout()
        => Assert.Equal(HttpStatusCode.GatewayTimeout,
            ApiService.BuildExceptionResponse(new TaskCanceledException()).StatusCode);

    [Fact]
    public void should_map_httprequest_failure_to_service_unavailable()
        => Assert.Equal(HttpStatusCode.ServiceUnavailable,
            ApiService.BuildExceptionResponse(new HttpRequestException("conn refused")).StatusCode);

    [Fact]
    public void should_map_other_exception_to_internal_server_error()
        => Assert.Equal(HttpStatusCode.InternalServerError,
            ApiService.BuildExceptionResponse(new InvalidOperationException()).StatusCode);
}
