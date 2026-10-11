using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Sessions;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/****************************************************************************
   Purpose      : 원격 6.3.x 대비 — 6.3.0 서버의 계정 응답이 200 인데 화면이 조용히 틀리지 않는가
                  · GET/PUT /api/settings/session — 6.3.0 은 동시 로그인 5키가 없다(payload app/schemas/settings.py · routers/settings.py:30-38, 62-68)
                    → 종전엔 "여러 곳 동시 로그인 허용"을 서버 값처럼 보이고, 저장은 서버가 말없이 버렸다(실제 6.3.0 로그인은 같은 계정 세션 전부 종료 — auth.py:572-591)
                  · GET /api/user-sessions — 6.3.0 은 pagination 이 없다(routers/user_sessions.py:81-116) → 종전엔 100건에서 목록이 조용히 끝났다
                  진짜 ApiService · AccountApiService 를 가짜 HTTP 처리기 위에 올린다 — 창 없음 · 원격 없음.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OldServerAccountCompatTests
{
    /// <summary>6.3.0 <c>GET /api/settings/session</c> — 7키뿐(동시 로그인 5키 없음).</summary>
    private const string SessionSettings630 =
        "{\"success\":true,\"data\":{\"session_timeout_hours\":24,\"refresh_expiration_days\":7,\"lockout_threshold\":5," +
        "\"lockout_duration_minutes\":30,\"session_enabled\":true,\"auth_mode\":\"token\",\"jwt_algorithm\":\"HS256\"}}";

    /// <summary>새 판(동시 로그인 키 있음) — 무회귀 대조군.</summary>
    private const string SessionSettingsNew =
        "{\"success\":true,\"data\":{\"session_timeout_hours\":24,\"refresh_expiration_days\":7,\"lockout_threshold\":5," +
        "\"lockout_duration_minutes\":30,\"session_enabled\":true,\"auth_mode\":\"token\",\"jwt_algorithm\":\"HS256\"," +
        "\"session_concurrency_policy\":\"allow\",\"max_concurrent_sessions\":3,\"session_self_replace_enabled\":true," +
        "\"session_history_retention_days\":30,\"login_anomaly_event_enabled\":false}}";

    private sealed class RouteHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, string?> _route;
        public ConcurrentQueue<(string Method, string PathAndQuery, string? Body)> Requests { get; } = new();
        public RouteHandler(Func<HttpRequestMessage, string?> route) => _route = route;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Enqueue((request.Method.Method, request.RequestUri!.PathAndQuery, body));
            var reply = _route(request);
            return new HttpResponseMessage(reply is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(reply ?? "{\"detail\":\"Not Found\"}", Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }

    private static IAccountApiService Api(RouteHandler http)
    {
        var setup = new ApiSetupModel { Url = "http://127.0.0.1:9/api" };
        var api = new ApiService(null, setup, http);
        api.Initialize();
        return new AccountApiService(api);
    }

    private static (AccountSetupPanelViewModel Vm, RouteHandler Http) SetupPanel(string getBody)
    {
        var http = new RouteHandler(r => r.RequestUri!.AbsolutePath == "/api/settings/session" ? getBody : null);
        var vm = new AccountSetupPanelViewModel(new CapturingEventAggregator(), new SilentLog()) { ApiOverride = Api(http) };
        return (vm, http);
    }

    // ─────────────── 세션 정책: 동시 로그인 5키 ───────────────

    [Fact]
    [Trait("Scenario", "DEP-307")]
    public async Task should_show_the_6_3_single_session_behaviour_and_lock_concurrency_when_the_server_lacks_the_keys()
    {
        var (vm, _) = SetupPanel(SessionSettings630);

        await vm.ClickReload();

        Assert.True(vm.ServerSettingsAvailable);                 // 만료 · 잠금 등 5칸은 그대로 편집 가능
        Assert.False(vm.IsConcurrencySupported);
        Assert.False(vm.CanEditConcurrency);
        Assert.Equal("evict_all", vm.ConcurrencyPolicy);         // 6.3.0 로그인의 실제 동작(같은 계정 세션 전부 종료)
        Assert.False(vm.IsAllowPolicy);
        Assert.Contains("지원하지 않습니다", vm.ConcurrencySupportReason);
    }

    [Fact]
    [Trait("Scenario", "DEP-307")]
    public async Task should_not_send_concurrency_keys_the_6_3_server_would_silently_drop()
    {
        var (vm, http) = SetupPanel(SessionSettings630);
        await vm.ClickReload();

        vm.TimeoutHours = 12;
        await vm.ClickSave();

        var put = Assert.Single(http.Requests, r => r.Method == "PUT");
        var keys = JObject.Parse(put.Body!).Properties().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "lockout_duration_minutes", "lockout_threshold", "refresh_expiration_days", "session_enabled", "session_timeout_hours" }, keys);
    }

    [Fact]
    [Trait("Scenario", "DEP-307")]
    public async Task should_keep_concurrency_editable_and_sent_when_the_server_has_the_keys()
    {
        // 무회귀 — 새 판은 종전 그대로(5키를 읽고 · 편집하고 · 보낸다).
        var (vm, http) = SetupPanel(SessionSettingsNew);
        await vm.ClickReload();

        Assert.True(vm.IsConcurrencySupported);
        Assert.True(vm.CanEditConcurrency);
        Assert.Equal(("allow", 3, true, 30), (vm.ConcurrencyPolicy, vm.MaxConcurrentSessions, vm.SessionSelfReplaceEnabled, vm.SessionHistoryRetentionDays));
        Assert.Equal(string.Empty, vm.ConcurrencySupportReason);

        vm.MaxConcurrentSessions = 5;
        await vm.ClickSave();

        var body = JObject.Parse(Assert.Single(http.Requests, r => r.Method == "PUT").Body!);
        Assert.Equal("allow", (string)body["session_concurrency_policy"]!);
        Assert.Equal(5, (int)body["max_concurrent_sessions"]!);
    }

    // ─────────────── 세션 목록: pagination 없음 ───────────────

    private static string SessionPage(int firstId, int count)
    {
        var items = Enumerable.Range(firstId, count).Select(id =>
            "{\"id\":" + id + ",\"user_id\":" + id + ",\"login_id\":\"user" + id + "\",\"role\":\"OPERATOR\",\"ip_address\":\"10.0.0.5\",\"user_agent\":\"GIS\"," +
            "\"expires_at\":\"2026-10-12T09:00:00+09:00\",\"is_active\":true,\"logout_reason\":null,\"logged_out_at\":null," +
            "\"created_at\":\"2026-10-11T09:00:00+09:00\",\"updated_at\":null}");
        return "{\"success\":true,\"data\":[" + string.Join(",", items) + "]}";   // 6.3.0: pagination 키 없음
    }

    private static (TestUserSessionPanel Vm, RouteHandler Http) SessionPanel(int total)
    {
        var http = new RouteHandler(r =>
        {
            if (r.RequestUri!.AbsolutePath != "/api/user-sessions") return null;
            var query = r.RequestUri.Query;
            var page = int.Parse(query.Split('&').First(p => p.TrimStart('?').StartsWith("page=")).Split('=')[1]);
            var first = (page - 1) * 100;
            return SessionPage(first + 1, Math.Max(0, Math.Min(100, total - first)));
        });
        return (new TestUserSessionPanel(new RecordingEventAggregator(), new RecordingLog(), Api(http), new FakeTokenStore()), http);
    }

    [Fact]
    [Trait("Scenario", "DEP-308")]
    public async Task should_keep_scrolling_past_100_sessions_when_the_6_3_server_sends_no_pagination()
    {
        var (vm, http) = SessionPanel(total: 130);

        await vm.ActivateForTestAsync();
        Assert.Equal(100, vm.Items.Count);
        Assert.True(vm.HasMorePages);                 // 한 쪽이 가득 찼다 = 더 있을 수 있다
        Assert.Equal("100건 이상", vm.LoadedCountText);

        await vm.LoadNextPageAsync();

        Assert.Equal(130, vm.Items.Count);
        Assert.False(vm.HasMorePages);                // 덜 찬 쪽 = 끝
        Assert.Equal("130 / 130건", vm.LoadedCountText);
        Assert.Equal(130, vm.TotalCount);
        Assert.Contains(http.Requests, r => r.PathAndQuery.Contains("page=2"));
        await vm.DeactivateForTestAsync();
    }

    [Fact]
    [Trait("Scenario", "DEP-308")]
    public async Task should_stop_at_a_short_first_page_when_the_6_3_server_sends_no_pagination()
    {
        var (vm, _) = SessionPanel(total: 40);

        await vm.ActivateForTestAsync();

        Assert.Equal(40, vm.Items.Count);
        Assert.False(vm.HasMorePages);
        Assert.Equal("40 / 40건", vm.LoadedCountText);
        await vm.DeactivateForTestAsync();
    }
}
