using System.Linq;
using System.Reflection;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// 서버 <c>error.code</c> 닫힌 어휘 — 명세 §12.2 v8.0.4 에서 16 → 19종(412 <c>VERSION_CONFLICT</c> · 412 <c>PRECONDITION_FAILED</c> ·
/// 428 <c>PRECONDITION_REQUIRED</c>). 서버 회신 2026-09-29 §3 「오류 코드를 enum 으로 받으신다면 세 값을 더해 주십시오」.
/// </summary>
public class ApiErrorCodesVocabularyTests
{
    /// <summary>명세 §12.2 표의 19종(2026-09-29, 서버 v8.0.4) — 정본 <c>app/schemas/common.py:ERROR_CODES</c>.</summary>
    private static readonly string[] ServerCodes =
    {
        "BAD_REQUEST", "UNAUTHORIZED", "SESSION_REVOKED", "FORBIDDEN", "NOT_FOUND", "METHOD_NOT_ALLOWED", "CONFLICT",
        "ENDPOINT_REMOVED", "GONE", "PRECONDITION_FAILED", "VERSION_CONFLICT", "PAYLOAD_TOO_LARGE", "VALIDATION_ERROR",
        "PRECONDITION_REQUIRED", "TOO_MANY_REQUESTS", "INTERNAL_ERROR", "BAD_GATEWAY", "SERVICE_UNAVAILABLE", "UNKNOWN_ERROR",
    };

    /// <summary>서버 어휘가 아닌 상수 — 클라 로컬 의사 코드 2 · 안정 sub-code 1.</summary>
    private static readonly string[] NotServerCodes = { "PARSE_ERROR", "GATEWAY_TIMEOUT", "PDF_FILE_MISSING" };

    [Fact]
    public void should_declare_exactly_the_19_server_codes_when_spec_is_v8_0_4()
    {
        // Arrange
        var declared = typeof(ApiErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Except(NotServerCodes)
            .OrderBy(c => c, System.StringComparer.Ordinal)
            .ToList();

        // Act
        var expected = ServerCodes.OrderBy(c => c, System.StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(expected, declared);
    }

    [Theory]
    [InlineData(412, "PRECONDITION_FAILED")]      // 서버 상태→코드 매핑 폴백과 같게(§12.2 — VERSION_CONFLICT 는 봉투로만 온다)
    [InlineData(428, "PRECONDITION_REQUIRED")]
    public async System.Threading.Tasks.Task should_fall_back_to_the_server_code_when_412_or_428_body_is_not_an_envelope(int status, string expected)
    {
        // Arrange — 앞단 프록시 등이 봉투 없이 돌려준 경우. 종전엔 UNKNOWN_ERROR 로 떨어져 분기가 사라졌다.
        using var response = new System.Net.Http.HttpResponseMessage((System.Net.HttpStatusCode)status)
        {
            Content = new System.Net.Http.StringContent("precondition", System.Text.Encoding.UTF8, "text/plain"),
        };

        // Act
        var result = await Ironwall.Dotnet.Libraries.Messages.Helpers.ApiMessageHelper.ToApiResponseAsync<object>(response);

        // Assert
        Assert.Equal(expected, result.Error?.Code);
        Assert.Equal(status, result.StatusCode);
    }

    [Theory]
    [InlineData("VERSION_CONFLICT")]
    [InlineData("PRECONDITION_REQUIRED")]
    [InlineData("SOMETHING_ADDED_LATER")]
    public void should_keep_error_code_as_plain_string_when_code_is_new_or_unknown(string code)
    {
        // Arrange — 모르는 값에서 역직렬화가 실패하지 않아야 한다(회신 §3). Code 는 enum 이 아니라 문자열이다.
        var json = "{\"code\":\"" + code + "\",\"message\":\"x\",\"details\":null}";

        // Act
        var error = JsonConvert.DeserializeObject<ApiError>(json);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(code, error!.Code);
    }
}
