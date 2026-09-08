using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : SYNC_EVENT_SUPPRESSION 자가필터 서비스 테스트.
                  핵심 계약 3가지를 고정한다 —
                  ① 자기 cmd 만 처리(1Hz 추적 트래픽 위에 얹히므로 선필터가 생명)
                  ② 처리는 폴링 가속 '하나'뿐(suppressing 값을 상태로 신뢰하지 않는다)
                  ③ 파싱 실패로 구독이 죽지 않는다
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary><see cref="EventSuppressionSyncNatsService"/> 단위 테스트.</summary>
public class EventSuppressionSyncNatsServiceTests
{
    /// <summary>즉시 폴링 요청을 세는 스텁 — 실제 서버 호출 없이 '가속 신호'만 관찰한다.</summary>
    private sealed class SpyMonitor : ISuppressionActiveMonitor
    {
        public readonly List<string> Requests = new();

        public IReadOnlyList<EventSuppressionScheduleDto> Active { get; } = new List<EventSuppressionScheduleDto>();
        public event Action? ActiveChanged;
        public DateTime? LastSuccessAt => null;
        public bool IsStale => false;
        public string LastSuccessAgeText => "확인 안 됨";

        public void RequestImmediatePoll(string reason = "") => Requests.Add(reason);

        public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;

        /// <summary>미사용 이벤트 경고 회피용.</summary>
        public void RaiseForCoverage() => ActiveChanged?.Invoke();
    }

    private static (EventSuppressionSyncNatsService svc, SpyMonitor spy, Func<MessageArgsModel, Task> handler)
        Build(ITokenStorageService? token = null)
    {
        Func<MessageArgsModel, Task>? captured = null;
        var nats = new Mock<INatsService>();
        nats.SetupAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
            .Callback<Func<MessageArgsModel, Task>>(h => captured = h);

        var spy = new SpyMonitor();
        var svc = new EventSuppressionSyncNatsService(null, nats.Object, spy, token);
        svc.StartService();

        Assert.NotNull(captured);
        return (svc, spy, captured!);
    }

    private static MessageArgsModel Msg(string data) => new("all.sync.suppression", "all.sync.>", data);

    private const string ValidPayload = """
        {"cmd":"SYNC_EVENT_SUPPRESSION","body":{"action":"UPDATED","resource_id":86,"status":"active","suppressing":false}}
        """;

    // ══════ ① 자기 cmd 만 처리한다 ══════

    [Fact]
    public async Task should_request_poll_when_own_command_arrives()
    {
        var (_, spy, handler) = Build();

        await handler(Msg(ValidPayload));

        Assert.Single(spy.Requests);
        Assert.Contains("SYNC_EVENT_SUPPRESSION", spy.Requests[0]);
        Assert.Contains("UPDATED", spy.Requests[0]);
    }

    [Theory]
    [InlineData("""{"cmd":"DETECT","body":{"id":1}}""")]
    [InlineData("""{"cmd":"SYNC_DEVICE","body":{"id":1}}""")]
    [InlineData("""{"cmd":"TRACKING_STATUS","body":{"id":1}}""")]
    public async Task should_ignore_other_commands(string payload)
    {
        var (_, spy, handler) = Build();

        await handler(Msg(payload));

        Assert.Empty(spy.Requests);
    }

    [Fact]
    public async Task should_ignore_when_command_name_only_appears_inside_body()
    {
        // 선필터(문자열 포함)는 통과하지만 cmd 가 다르므로 처리하면 안 된다.
        var (_, spy, handler) = Build();

        await handler(Msg("""{"cmd":"DETECT","body":{"note":"SYNC_EVENT_SUPPRESSION 관련"}}"""));

        Assert.Empty(spy.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task should_ignore_empty_payload(string? payload)
    {
        var (_, spy, handler) = Build();

        await handler(Msg(payload!));

        Assert.Empty(spy.Requests);
    }

    // ══════ ② suppressing 은 가속 신호일 뿐 — 값과 무관하게 폴링을 요청한다 ══════

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public async Task should_request_poll_regardless_of_suppressing_value(string flag)
    {
        var (_, spy, handler) = Build();

        await handler(Msg(
            "{\"cmd\":\"SYNC_EVENT_SUPPRESSION\",\"body\":{\"action\":\"UPDATED\","
            + "\"resource_id\":9,\"suppressing\":" + flag + "}}"));

        Assert.Single(spy.Requests);
    }

    [Fact]
    public async Task should_request_poll_when_suppressing_field_is_absent()
    {
        // 구버전 서버는 이 선택 필드를 주지 않는다 — 그래도 폴링은 가속돼야 한다.
        var (_, spy, handler) = Build();

        await handler(Msg("""{"cmd":"SYNC_EVENT_SUPPRESSION","body":{"action":"CREATED","resource_id":3}}"""));

        Assert.Single(spy.Requests);
    }

    [Fact]
    public async Task should_request_poll_when_body_is_missing()
    {
        var (_, spy, handler) = Build();

        await handler(Msg("""{"cmd":"SYNC_EVENT_SUPPRESSION"}"""));

        Assert.Single(spy.Requests);   // 권위는 폴링이므로 body 가 부실해도 앞당긴다
    }

    // ══════ ③ 파싱 실패로 구독이 죽지 않는다 ══════

    [Fact]
    public async Task should_not_throw_when_payload_is_malformed_json()
    {
        var (_, spy, handler) = Build();

        var ex = await Record.ExceptionAsync(
            () => handler(Msg("""{"cmd":"SYNC_EVENT_SUPPRESSION","body":{ 깨진 """)));

        Assert.Null(ex);                // 다음 폴링 주기가 복구한다
        Assert.Empty(spy.Requests);
    }

    [Fact]
    public async Task should_keep_working_after_a_malformed_message()
    {
        var (_, spy, handler) = Build();

        await handler(Msg("""{"cmd":"SYNC_EVENT_SUPPRESSION","body":{ 깨진 """));
        await handler(Msg(ValidPayload));

        Assert.Single(spy.Requests);    // 구독이 살아 있다
    }

    // ══════ 로그인 게이팅 — 미인증 상태에서 서버 폴링을 유발하지 않는다 ══════

    [Fact]
    public async Task should_not_request_poll_when_not_authenticated()
    {
        var token = new Mock<ITokenStorageService>();
        token.SetupGet(x => x.IsAuthenticated).Returns(false);
        var (_, spy, handler) = Build(token.Object);

        await handler(Msg(ValidPayload));

        Assert.Empty(spy.Requests);
    }

    [Fact]
    public async Task should_request_poll_when_authenticated()
    {
        var token = new Mock<ITokenStorageService>();
        token.SetupGet(x => x.IsAuthenticated).Returns(true);
        var (_, spy, handler) = Build(token.Object);

        await handler(Msg(ValidPayload));

        Assert.Single(spy.Requests);
    }

    // ══════ 구독 수명 — 멱등 등록 · StopAsync 해제 ══════

    [Fact]
    public void should_subscribe_once_when_start_called_twice()
    {
        var nats = new Mock<INatsService>();
        var svc = new EventSuppressionSyncNatsService(null, nats.Object, new SpyMonitor());

        svc.StartService();
        svc.StartService();

        // 멱등 패턴: 매 호출마다 -= 후 += 이므로 add 2회 / remove 2회여야 한다.
        nats.VerifyAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>(), Times.Exactly(2));
        nats.VerifyRemove(m => m.NatsSubscribeEventAsync -= It.IsAny<Func<MessageArgsModel, Task>>(), Times.Exactly(2));
    }

    [Fact]
    public async Task should_unsubscribe_when_stopped()
    {
        var nats = new Mock<INatsService>();
        var svc = new EventSuppressionSyncNatsService(null, nats.Object, new SpyMonitor());
        svc.StartService();

        await svc.StopAsync();

        // ⚠ 이 해제가 없으면 구독이 누수된다(DI 에서 .As<IService>() 누락 시 같은 결과).
        nats.VerifyRemove(m => m.NatsSubscribeEventAsync -= It.IsAny<Func<MessageArgsModel, Task>>(), Times.Exactly(2));
    }

    [Fact]
    public async Task should_delegate_execute_to_start_service()
    {
        var nats = new Mock<INatsService>();
        var svc = new EventSuppressionSyncNatsService(null, nats.Object, new SpyMonitor());

        await svc.ExecuteAsync();

        nats.VerifyAdd(m => m.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>(), Times.Once);
    }
}
