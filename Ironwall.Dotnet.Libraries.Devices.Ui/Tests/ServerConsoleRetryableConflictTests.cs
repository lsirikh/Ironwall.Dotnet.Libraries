using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 서버 콘솔 저장 · 등록 · 배정이 409 동시 쓰기 경합(details.retryable=true)을 "다시 시도" 로 말하는가 (G01)
                  진짜 ApiService · ServerAxisApiService · ServerConsoleService 를 가짜 HTTP 처리기 위에 올린다 — 창 없음 · 원격 없음.
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class ServerConsoleRetryableConflictTests
{
    private const string NameText = "같은 이름의 서버가 이미 있습니다. 다른 이름을 쓰세요.";

    /// <summary>8.0 <c>GET /api/servers/3</c> — 축 모양.</summary>
    private const string Server80 =
        "{\"success\":true,\"data\":{\"id\":3,\"category_server\":\"PROXY\",\"name\":\"PROXY-01\",\"is_enable\":true,\"unit_id\":1,\"status\":\"NORMAL\"," +
        "\"connection\":{\"schema\":1,\"type\":\"IP_DIRECT\",\"ip_address\":\"10.0.0.5\",\"ip_port\":8100}}}";

    private const string Retryable =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"동시에 처리 중인 다른 요청과 충돌해 이 요청을 처리하지 못했습니다\",\"details\":{\"retryable\":true}}}";

    private const string NotRetryable =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"Server with name 'PROXY-01' already exists\",\"details\":null}}";

    private sealed class FixedProbe : IServerContractProbe
    {
        public FixedProbe(EnumServerContract contract) => Contract = contract;
        public EnumServerContract Contract { get; }
        public string? RawVersion => Contract.ToString();
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    /// <summary>GET 은 서버 한 대, 쓰기(POST · PATCH)는 409 를 돌려준다.</summary>
    private sealed class ConflictHandler : DelegatingHandler
    {
        private readonly string _conflict;
        public ConflictHandler(string conflict) => _conflict = conflict;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isWrite = request.Method != HttpMethod.Get;
            return Task.FromResult(new HttpResponseMessage(isWrite ? HttpStatusCode.Conflict : HttpStatusCode.OK)
            {
                Content = new StringContent(isWrite ? _conflict : Server80, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
        }
    }

    private static ServerConsoleService Rig(string conflict)
    {
        var setup = new ApiSetupModel { Url = "http://127.0.0.1:9/api" };
        var api = new ApiService(null, setup, new ConflictHandler(conflict));
        api.Initialize();
        var probe = new FixedProbe(EnumServerContract.V8_0);
        return new ServerConsoleService(
            new ServerAxisApiService(api, setup, probe),
            new ServerApiService(null, api, setup, probe),
            new DeviceQueryPolicy(probe));
    }

    [Theory]
    [InlineData(Retryable, true)]
    [InlineData(NotRetryable, false)]
    public async Task should_say_retry_only_for_a_retryable_conflict_when_saving(string conflict, bool retryable)
    {
        var result = await Rig(conflict).SaveAsync(3, new ServerWriteIntent { Port = 8200 });

        Assert.False(result.IsSuccess);
        Assert.Equal(retryable ? ApiStatusHelper.RetryableConflictMessage : NameText, result.Message);
    }

    [Theory]
    [InlineData(Retryable, true)]
    [InlineData(NotRetryable, false)]
    public async Task should_say_retry_only_for_a_retryable_conflict_when_creating(string conflict, bool retryable)
    {
        var category = new ServerCategoryOption(1, "프록시", EnumServerType.PROXY, "PROXY");

        var (result, newId) = await Rig(conflict).CreateAsync(category, new ServerWriteIntent { Name = "PROXY-01", IpAddress = "10.0.0.2", Port = 8101 });

        Assert.False(result.IsSuccess);
        Assert.Equal(0, newId);
        Assert.Equal(retryable ? ApiStatusHelper.RetryableConflictMessage : NameText, result.Message);
    }

    [Fact]
    public async Task should_say_retry_when_a_device_assignment_hits_a_retryable_conflict()
    {
        var result = await Rig(Retryable).AssignDeviceAsync(EnumDeviceCategory.Controller, 21, 3);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiStatusHelper.RetryableConflictMessage, result.Message);
    }
}
