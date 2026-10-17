using System.Xml;
using Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// PRD camera-popup-modes FR-22 — PTZ 정지가 대기 중인 이동을 취소하고 먼저 나간다.
/// 실코드(PtzCommandGate · PtzMotionPolicy) 직접 링크. 시간 대기 없이 태스크 상태로 순서를 본다.
/// </summary>
public class PtzCommandGateTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task should_enter_immediately_when_gate_is_free()
    {
        var gate = new PtzCommandGate();

        await gate.WaitAsync();

        Assert.True(gate.IsHeld);
        Assert.Equal(0, gate.PendingCount);
    }

    [Fact]
    public async Task should_cancel_pending_move_when_stop_requested()
    {
        // Arrange — 앞 명령이 게이트를 쥔 동안 이동이 줄 서 있다
        var gate = new PtzCommandGate();
        await gate.WaitAsync();
        var move = gate.WaitMoveAsync();

        // Act
        var stop = gate.WaitStopAsync();

        // Assert — 이동은 취소, 정지는 다음 차례
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => move.WaitAsync(Wait));
        Assert.False(stop.IsCompleted);
        gate.Release();
        await stop.WaitAsync(Wait);
        Assert.Equal(0, gate.PendingCount);
    }

    [Fact]
    public async Task should_grant_stop_before_earlier_queued_commands_when_released()
    {
        // Arrange — 상태 조회가 먼저 줄 섰고 정지가 뒤에 왔다
        var gate = new PtzCommandGate();
        await gate.WaitAsync();
        var status = gate.WaitAsync();
        var stop = gate.WaitStopAsync();

        // Act
        gate.Release();

        // Assert — 정지가 새치기, 상태 조회는 정지가 끝난 뒤
        await stop.WaitAsync(Wait);
        Assert.False(status.IsCompleted);
        gate.Release();
        await status.WaitAsync(Wait);
    }

    [Fact]
    public async Task should_keep_move_queued_after_stop_when_stop_is_pending()
    {
        // Arrange — 정지가 대기 중인데 새 누름(이동)이 뒤에 들어왔다
        var gate = new PtzCommandGate();
        await gate.WaitAsync();
        var stop = gate.WaitStopAsync();
        var newMove = gate.WaitMoveAsync();

        // Act
        gate.Release();

        // Assert — 정지 먼저, 새 이동은 취소되지 않고 그다음
        await stop.WaitAsync(Wait);
        Assert.False(newMove.IsCompleted);
        gate.Release();
        var epoch = await newMove.WaitAsync(Wait);
        Assert.False(epoch.IsCancellationRequested);
    }

    [Fact]
    public async Task should_cancel_move_epoch_when_stop_arrives_after_move_entered()
    {
        // Arrange — 이동이 이미 게이트를 얻어 나가는 중(유지 재전송은 이 세대 토큰에 묶인다)
        var gate = new PtzCommandGate();
        var epoch = await gate.WaitMoveAsync();

        // Act — 정지는 나가는 이동을 끊지 않고 뒤에 선다
        var stop = gate.WaitStopAsync();

        // Assert
        Assert.True(epoch.IsCancellationRequested);
        Assert.False(stop.IsCompleted);
        gate.Release();
        await stop.WaitAsync(Wait);
    }

    [Fact]
    public async Task should_leave_pending_moves_when_stop_token_already_cancelled()
    {
        // Arrange — 새 제스처가 인계해 뗌 정지 토큰이 이미 취소됐다(LWW)
        var gate = new PtzCommandGate();
        await gate.WaitAsync();
        var move = gate.WaitMoveAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var stop = gate.WaitStopAsync(cts.Token);

        // Assert — 정지만 버려지고 새 이동은 살아 있다
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop.WaitAsync(Wait));
        Assert.False(move.IsCompleted);
        gate.Release();
        await move.WaitAsync(Wait);
    }

    [Fact]
    public async Task should_free_gate_when_only_waiter_is_cancelled()
    {
        // Arrange
        var gate = new PtzCommandGate();
        await gate.WaitAsync();
        using var cts = new CancellationTokenSource();
        var pending = gate.WaitAsync(cts.Token);

        // Act
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Wait));
        gate.Release();

        // Assert — 취소된 대기자에게 넘기지 않고 비운다
        Assert.Equal(0, gate.PendingCount);
        Assert.False(gate.IsHeld);
    }

    [Fact]
    public void should_throw_when_released_without_holder()
    {
        var gate = new PtzCommandGate();

        Assert.Throws<SemaphoreFullException>(() => gate.Release());
    }

    [Fact]
    public void should_refresh_move_before_timeout_when_holding()
    {
        // 안전 제한 2초(이전 PT10S) · 유지 재전송은 제한 안에 두 번 이상
        Assert.Equal("PT2S", PtzMotionPolicy.MoveTimeout);
        Assert.Equal(XmlConvert.ToTimeSpan(PtzMotionPolicy.MoveTimeout), PtzMotionPolicy.MoveTimeoutSpan);
        Assert.True(PtzMotionPolicy.KeepAliveInterval * 2 <= PtzMotionPolicy.MoveTimeoutSpan);
    }
}
