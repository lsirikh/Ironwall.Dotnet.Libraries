using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Redis.Models;
using Ironwall.Dotnet.Libraries.Redis.Services;
using Moq;
using StackExchange.Redis;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Redis.Redis.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/1/2025 1:16:52 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class RedisServiceTests
{
    private readonly RedisSetupModel _setupModel;
    private readonly ILogService _log;

    public RedisServiceTests()
    {
        _setupModel = new RedisSetupModel
        {
            IpAddressRedis = "localhost",
            PortRedis = 6379,
            PasswordRedis = "",
            NameChannel = "unit-test-channel"
        };

        _log = new LogService(); // 간단한 콘솔 로깅
    }

    [Fact]
    public async Task ConnectAndPublishAsync_ShouldPublishAndReceiveMessage()
    {
        // Arrange
        var redisService = new RedisService(_log);
        await redisService.ConnectAsync(_setupModel);
        await redisService.ExecuteAsync(); // 구독 시작

        var tcs = new TaskCompletionSource<string>();

        redisService.RedisSubscribeEventAsync += async (e) =>
        {
            var msg = e.Message;
            Console.WriteLine($"Received from channel: {msg}");
            tcs.TrySetResult(msg);
            await Task.CompletedTask;
        };

        // Act
        var testMessage = $"Hello Redis! {Guid.NewGuid()}";
        await redisService.PublishAsync(_setupModel.NameChannel, testMessage);

        // Assert
        var received = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.True(received == tcs.Task, "Did not receive message in time.");
        Assert.Equal(testMessage, tcs.Task.Result);
    }
}
