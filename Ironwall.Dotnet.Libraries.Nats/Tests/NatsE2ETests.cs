using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/****************************************************************************
    Purpose      : NATS 연결 및 Subscribe E2E 테스트
    전제조건     : NATS 서버(localhost:4222)가 실행 중이어야 함
    Created By   : GHLee
    Created On   : 2026-03-05
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

public class NatsE2ETests
{
    // ──────────────────────────────────────────────────
    // 테스트 설정 (NATS 서버 주소 / Subject 계층)
    // ──────────────────────────────────────────────────
    private static readonly NatsSetupModel SetupModel = new()
    {
        IpAddressNats       = "localhost",
        PortNats            = 4222,
        DomainNats          = "sensorway",
        GroupNats           = "unit001",
        SubsystemNats       = "gis",
        ConnectionTimeoutNats = 5000,
    };
    // EffectiveSubject → "sensorway.unit001.gis.>"

    private readonly ILogService _log = new LogService();

    // ──────────────────────────────────────────────────
    // Test 1 : 설정 검증 (NATS 서버 불필요)
    // ──────────────────────────────────────────────────

    /// <summary>
    /// NatsSetupModel 인스턴스가 올바른 EffectiveSubject를 생성하는지 확인
    /// </summary>
    [Fact]
    public void SetupModel_EffectiveSubject_ShouldBeComposed()
    {
        var expected = $"{SetupModel.DomainNats}.{SetupModel.GroupNats}.{SetupModel.SubsystemNats}.>";
        Assert.Equal(expected, SetupModel.EffectiveSubject);

        Console.WriteLine($"[Setup] Server          = {SetupModel.IpAddressNats}:{SetupModel.PortNats}");
        Console.WriteLine($"[Setup] EffectiveSubject = {SetupModel.EffectiveSubject}");
        Console.WriteLine($"[Setup] Timeout(ms)      = {SetupModel.ConnectionTimeoutNats}");
    }

    // ──────────────────────────────────────────────────
    // Test 2 : Connection (PingAsync 까지)
    // ──────────────────────────────────────────────────

    /// <summary>
    /// ConnectAsync → PingAsync 가 정상 완료되는지 확인
    /// </summary>
    [Fact]
    public async Task ConnectAsync_ShouldSucceed()
    {
        var svc = new NatsService(_log);

        var result = await svc.ConnectAsync(SetupModel);

        Assert.NotNull(result);
        Console.WriteLine($"[OK] Connected to {SetupModel.IpAddressNats}:{SetupModel.PortNats}");
        Console.WriteLine($"[OK] Subject = {SetupModel.EffectiveSubject}");
    }

    // ──────────────────────────────────────────────────
    // Test 3 : Subscribe → Publish → Receive (단건)
    // ──────────────────────────────────────────────────

    /// <summary>
    /// EffectiveSubject 와일드카드로 구독한 뒤,
    /// 해당 하위 subject로 Publish → 수신 확인 (단건)
    /// </summary>
    [Fact]
    public async Task Subscribe_ThenPublish_ShouldReceiveSingleMessage()
    {
        var svc = new NatsService(_log);
        await svc.ConnectAsync(SetupModel);

        var tcs             = new TaskCompletionSource<string>();
        string? rxSubject   = null;

        // 핸들러를 ExecuteAsync 전에 등록
        svc.NatsSubscribeEventAsync += async (e) =>
        {
            rxSubject = e.Subject;
            tcs.TrySetResult(e.Data ?? string.Empty);
            await Task.CompletedTask;
        };

        await svc.ExecuteAsync();           // 구독 시작
        await Task.Delay(300);              // 구독 준비 대기

        var txSubject = $"{SetupModel.DomainNats}.{SetupModel.GroupNats}.{SetupModel.SubsystemNats}.test.ping";
        var txMessage = $"ping-{Guid.NewGuid()}";

        Console.WriteLine($"[Subscribe] {SetupModel.EffectiveSubject}");
        Console.WriteLine($"[Publish]   subject = {txSubject}");
        Console.WriteLine($"[Publish]   message = {txMessage}");

        await svc.PublishAsync(txSubject, txMessage);

        var done = await Task.WhenAny(tcs.Task, Task.Delay(3000));

        Assert.True(done == tcs.Task,
            $"3초 내 메시지 미수신. NATS 서버({SetupModel.IpAddressNats}:{SetupModel.PortNats}) 실행 여부 확인.");
        Assert.Equal(txMessage, await tcs.Task);

        Console.WriteLine($"[OK] Received on '{rxSubject}': {await tcs.Task}");
    }

    // ──────────────────────────────────────────────────
    // Test 4 : Subscribe → Publish 복수 subject → 모두 수신
    // ──────────────────────────────────────────────────

    /// <summary>
    /// 와일드카드 구독(>.>) 상태에서 서로 다른 하위 subject 3건을 Publish,
    /// 모두 수신되는지 확인
    /// </summary>
    [Fact]
    public async Task Subscribe_ThenPublishMultiple_ShouldReceiveAll()
    {
        var svc = new NatsService(_log);
        await svc.ConnectAsync(SetupModel);

        var rxSubjects  = new List<string>();
        var tcs         = new TaskCompletionSource<bool>();
        const int total = 3;

        svc.NatsSubscribeEventAsync += async (e) =>
        {
            lock (rxSubjects)
            {
                rxSubjects.Add(e.Subject ?? string.Empty);
                Console.WriteLine($"[Received #{rxSubjects.Count}] {e.Subject}: {e.Data}");
                if (rxSubjects.Count >= total)
                    tcs.TrySetResult(true);
            }
            await Task.CompletedTask;
        };

        await svc.ExecuteAsync();
        await Task.Delay(300);

        var prefix = $"{SetupModel.DomainNats}.{SetupModel.GroupNats}.{SetupModel.SubsystemNats}";
        var subjects = new[]
        {
            $"{prefix}.test.alpha",
            $"{prefix}.test.beta",
            $"{prefix}.test.gamma.deep",
        };

        Console.WriteLine($"[Subscribe] {SetupModel.EffectiveSubject}");
        foreach (var s in subjects)
            Console.WriteLine($"[Publish]   {s}");

        await svc.PublishAsync(subjects[0], "msg-alpha");
        await svc.PublishAsync(subjects[1], "msg-beta");
        await svc.PublishAsync(subjects[2], "msg-gamma");

        var done = await Task.WhenAny(tcs.Task, Task.Delay(3000));

        Assert.True(done == tcs.Task,
            $"{total}건 메시지 수신 타임아웃. 수신 수: {rxSubjects.Count}");
        Assert.Equal(total, rxSubjects.Count);
        Assert.Contains(subjects[0], rxSubjects);
        Assert.Contains(subjects[1], rxSubjects);
        Assert.Contains(subjects[2], rxSubjects);

        Console.WriteLine($"[OK] {total}건 모두 수신 완료");
    }

    // ──────────────────────────────────────────────────
    // Test 5 : StopAsync 후 메시지 미수신 확인
    // ──────────────────────────────────────────────────

    /// <summary>
    /// StopAsync 호출 후 Publish해도 이벤트가 발생하지 않는지 확인
    /// </summary>
    [Fact]
    public async Task StopAsync_ThenPublish_ShouldNotReceive()
    {
        var cts = new CancellationTokenSource();
        var svc = new NatsService(_log);
        await svc.ConnectAsync(SetupModel);

        var received = false;

        svc.NatsSubscribeEventAsync += async (e) =>
        {
            received = true;
            await Task.CompletedTask;
        };

        await svc.ExecuteAsync(cts.Token);
        await Task.Delay(300);

        // 구독 중단
        cts.Cancel();
        await svc.StopAsync();
        await Task.Delay(200);

        var txSubject = $"{SetupModel.DomainNats}.{SetupModel.GroupNats}.{SetupModel.SubsystemNats}.test.stop";
        await svc.PublishAsync(txSubject, "should-not-arrive");

        await Task.Delay(500);  // 혹시 늦게 도착하는 경우 대기

        Assert.False(received, "StopAsync 이후 메시지가 수신됨 — 구독 해제 실패");
        Console.WriteLine("[OK] StopAsync 이후 메시지 수신 없음");
    }
}
