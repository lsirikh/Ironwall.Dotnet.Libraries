using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/****************************************************************************
    Purpose      : MessageService<T> 단위 테스트 — StopAsync 구독 태스크 대기 검증
    Created By   : GHLee
    Created On   : 2026-03-05
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

public class MessageServiceTests
{
    /// <summary>
    /// 테스트용 MessageService 구체 구현 — NATS 서버 불필요
    /// </summary>
    private class TestMessageService : MessageService<TestMessageService>
    {
        public override TestMessageService? Connect(INatsSetupModel setupModel) => this;
        public override Task<TestMessageService?> ConnectAsync(INatsSetupModel setupModel)
            => Task.FromResult<TestMessageService?>(this);
        public override Task PublishAsync(string subject, string data) => Task.CompletedTask;

        /// <summary>테스트에서 구독 태스크를 직접 주입</summary>
        public void AddSubscriptionTask(Task task) => _subscriptionTasks.Add(task);
    }

    [Fact]
    public async Task StopAsync_ShouldAwaitAllSubscriptionTasksBeforeReturning()
    {
        // Arrange
        var service = new TestMessageService();
        var tcs = new TaskCompletionSource();
        var taskCompleted = false;

        service.AddSubscriptionTask(Task.Run(async () =>
        {
            await tcs.Task;
            taskCompleted = true;
        }));

        // Act — StopAsync 시작 (아직 완료 안 됨)
        var stopTask = service.StopAsync();

        // 50ms 대기 후 아직 구독 태스크가 완료되지 않았음을 확인
        await Task.Delay(50);
        Assert.False(taskCompleted, "StopAsync가 구독 태스크 완료를 기다리지 않고 반환되었습니다.");

        // 구독 태스크 완료 신호
        tcs.SetResult();
        await stopTask;

        // Assert — StopAsync가 태스크 완료를 기다렸음
        Assert.True(taskCompleted, "StopAsync는 모든 구독 태스크 완료 후 반환해야 합니다.");
    }

    [Fact]
    public async Task StopAsync_ShouldHandleCancelledSubscriptionTasksGracefully()
    {
        // Arrange
        var service = new TestMessageService();
        var cts = new CancellationTokenSource();

        service.AddSubscriptionTask(Task.Run(async () =>
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }, cts.Token));

        // 구독 태스크 취소
        cts.Cancel();
        await Task.Delay(50); // 취소 전환 대기

        // Act + Assert — OperationCanceledException이 외부로 전파되지 않아야 함
        var ex = await Record.ExceptionAsync(() => service.StopAsync());
        Assert.Null(ex);
    }
}
