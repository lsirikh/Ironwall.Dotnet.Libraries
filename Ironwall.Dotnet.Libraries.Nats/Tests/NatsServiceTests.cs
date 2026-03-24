using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/****************************************************************************
    Purpose      : NATS 서비스 단위 테스트
    Created By   : GHLee
    Created On   : 10/28/2025
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

public class NatsServiceTests
{
    private readonly NatsSetupModel _setupModel;
    private readonly ILogService _log;

    public NatsServiceTests()
    {
        _setupModel = new NatsSetupModel
        {
            IpAddressNats = "192.168.202.195",
            PortNats = 4222,
            DefaultSubjectNats = "test.>",
            SubsystemNats = "NatsServiceTests",
            ConnectionTimeoutNats = 5000
        };

        _log = new LogService(); // 간단한 콘솔 로깅
    }

    [Fact]
    public async Task ConnectAndPublishAsync_ShouldPublishAndReceiveMessage()
    {
        // Arrange
        var natsService = new NatsService(_log);
        await natsService.ConnectAsync(_setupModel);

        var tcs = new TaskCompletionSource<string>();

        // 이벤트 핸들러를 먼저 등록 (ExecuteAsync 전에!)
        natsService.NatsSubscribeEventAsync += async (e) =>
        {
            var data = e.Data ?? string.Empty;
            Console.WriteLine($"Received from subject '{e.Subject}': {data}");
            tcs.TrySetResult(data);
            await Task.CompletedTask;
        };

        await natsService.ExecuteAsync(); // 구독 시작

        // Act
        var testMessage = $"Hello NATS! {Guid.NewGuid()}";
        var testSubject = "test.unit.message";

        // 메시지 전송 전 약간의 대기 (구독이 준비될 시간)
        await Task.Delay(500);

        await natsService.PublishAsync(testSubject, testMessage);

        // Assert
        var received = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.True(received == tcs.Task, "Did not receive message in time.");
        Assert.Equal(testMessage, tcs.Task?.Result);
    }

    [Fact]
    public async Task WildcardSubscription_ShouldReceiveMultipleSubjects()
    {
        // Arrange
        var natsService = new NatsService(_log);
        var setupModel = new NatsSetupModel
        {
            IpAddressNats = "192.168.202.195",
            PortNats = 4222,
            DefaultSubjectNats = "test.wildcard.>", // 다중 레벨 와일드카드
            SubsystemNats = "WildcardTest"
        };

        await natsService.ConnectAsync(setupModel);

        var receivedMessages = new List<string>();
        var tcs = new TaskCompletionSource<bool>();

        // 이벤트 핸들러를 먼저 등록 (ExecuteAsync 전에!)
        natsService.NatsSubscribeEventAsync += async (e) =>
        {
            receivedMessages.Add(e.Subject ?? string.Empty);
            Console.WriteLine($"Received: {e.Subject}");

            if (receivedMessages.Count == 3)
            {
                tcs.TrySetResult(true);
            }
            await Task.CompletedTask;
        };

        await natsService.ExecuteAsync();

        await Task.Delay(500);

        // Act
        await natsService.PublishAsync("test.wildcard.one", "Message 1");
        await natsService.PublishAsync("test.wildcard.two", "Message 2");
        await natsService.PublishAsync("test.wildcard.three.deep", "Message 3");

        // Assert
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.True(completed == tcs.Task, "Did not receive all messages in time.");
        Assert.Equal(3, receivedMessages.Count);
        Assert.Contains("test.wildcard.one", receivedMessages);
        Assert.Contains("test.wildcard.two", receivedMessages);
        Assert.Contains("test.wildcard.three.deep", receivedMessages);
    }

    [Fact(DisplayName = "3.3: RequestAsync — Connection null → null 반환")]
    public async Task NatsService_RequestAsync_ConnectionNull_ReturnsNull()
    {
        // Arrange — Connect 호출 없이 생성
        var service = new NatsService(_log);

        // Act
        var result = await service.RequestAsync("test.subject", "test data");

        // Assert
        Assert.Null(result);
    }
}
