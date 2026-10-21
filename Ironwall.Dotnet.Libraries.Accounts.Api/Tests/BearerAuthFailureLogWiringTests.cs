using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Tests;

/// <summary>
/// G02 — 실패 응답 한 줄 로그(<c>req=&lt;request_id&gt;</c>)가 <b>앱 로그</b>까지 닿는가.
/// <para>응답 변환기(<see cref="ApiMessageHelper"/>)의 줄 내용은 Messages 시험이 본다. 여기서는 그 줄을 앱 로거로 잇는
/// 유일한 배선(<see cref="BearerAuthHandler"/> 생성자)을 본다 — 배선이 빠지면 줄은 만들어져도 아무 데도 안 남는다.</para>
/// <para><see cref="ApiMessageHelper.FailureLog"/> 는 정적이라 같은 핸들러를 만드는 시험과 겹치지 않게 직렬 컬렉션에 둔다.</para>
/// </summary>
[Collection(BearerAuthHandlerStaticCollection.Name)]
public class BearerAuthFailureLogWiringTests
{
    [Trait("Scenario", "GOP815-G02")]
    [Fact]
    public async Task should_write_the_failure_line_with_request_id_to_the_app_log_when_the_handler_is_built_with_a_logger()
    {
        var saved = ApiMessageHelper.FailureLog;
        ApiMessageHelper.FailureLog = null;
        try
        {
            // Arrange — 앱이 하듯 로거를 넣어 핸들러를 만든다.
            var log = new CapturingLog();
            using var handler = new BearerAuthHandler(new TokenStorageService(), () => null!, log);
            var id = "wire-" + Guid.NewGuid().ToString("N");
            using var response = new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent(
                    "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"x\",\"details\":null},\"meta\":{\"request_id\":\"" + id + "\"}}",
                    Encoding.UTF8, "application/json"),
                RequestMessage = new HttpRequestMessage(HttpMethod.Patch, "http://gop.test/api/devices/7"),
            };

            // Act
            await response.ToApiResponseAsync<object>();

            // Assert
            Assert.Contains(log.Warnings, w => w.Contains("req=" + id) && w.Contains("409"));
        }
        finally { ApiMessageHelper.FailureLog = saved; }
    }

    private sealed class CapturingLog : ILogService
    {
        public readonly ConcurrentQueue<string> Warnings = new();
        public void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
        public void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
        public void Warning(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) => Warnings.Enqueue(msg);
        public event EventHandler<LogEventArgs>? LogEvent { add { } remove { } }
    }
}
