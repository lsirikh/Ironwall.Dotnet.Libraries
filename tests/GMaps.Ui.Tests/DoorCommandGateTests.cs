using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 통문·함체 개폐 명령 게이트 — PRD symbol-detail-and-door-control FR-03/04/05, NFR-02.
/// <para>핵심 계약: <b>명령은 상태를 바꾸지 않는다.</b> 버튼을 눌러도 확정 상태(<c>ReportedState</c>)는
/// 그대로이고 화면만 Pending 이 된다. 실제 전이는 보고(<c>OnStateReported</c>)로만 일어난다 —
/// 서버가 *"명령 접수만으로 낙관 기록하면 DB 가 거짓말한다"* 고 규정한 설계를 클라가 깨지 않도록 고정한다.</para>
/// </summary>
public class DoorCommandGateTests
{
    private static readonly DateTime T0 = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    private static DoorCommandGate Closed(double timeout = DoorCommandGate.DefaultTimeoutSec)
    {
        var g = new DoorCommandGate(timeout);
        g.OnStateReported(DoorUiState.Closed);
        return g;
    }

    [Fact]
    public void should_block_commands_when_state_never_reported()
    {
        var g = new DoorCommandGate();
        Assert.Equal(DoorUiState.Unknown, g.DisplayState);
        Assert.False(g.CanSend);
        Assert.False(g.TryBegin("OPEN", T0));      // 상태를 모르면 명령 금지(FR-05)
        Assert.False(g.IsPending);
    }

    [Fact]
    public void should_enter_pending_without_changing_reported_state_when_command_sent()
    {
        var g = Closed();
        Assert.True(g.TryBegin("OPEN", T0));

        Assert.True(g.IsPending);
        Assert.Equal(DoorUiState.Pending, g.DisplayState);
        Assert.Equal("OPEN", g.PendingCommand);
        // ★ 핵심: 명령을 보냈다고 확정 상태가 열림이 되면 안 된다(FR-03)
        Assert.Equal(DoorUiState.Closed, g.ReportedState);
        Assert.False(g.CanSend);
    }

    [Fact]
    public void should_ignore_repeat_clicks_while_pending()
    {
        var g = Closed();
        Assert.True(g.TryBegin("OPEN", T0));
        Assert.False(g.TryBegin("OPEN", T0.AddSeconds(1)));    // 연타(NFR-02)
        Assert.False(g.TryBegin("CLOSE", T0.AddSeconds(2)));   // 반대 명령도 대기 중엔 금지
        Assert.Equal("OPEN", g.PendingCommand);
    }

    [Fact]
    public void should_reject_command_that_matches_current_state()
    {
        var g = Closed();
        Assert.False(g.TryBegin("CLOSE", T0));   // 이미 닫힘 — 불필요한 왕복 차단
        Assert.False(g.IsPending);
    }

    [Fact]
    public void should_reject_unknown_command_token()
    {
        var g = Closed();
        Assert.False(g.TryBegin("TOGGLE", T0));
        Assert.False(g.TryBegin("", T0));
        Assert.False(g.TryBegin(null!, T0));
        Assert.False(g.IsPending);
    }

    [Fact]
    public void should_resolve_pending_when_state_is_reported()
    {
        var g = Closed();
        g.TryBegin("OPEN", T0);

        g.OnStateReported(DoorUiState.Open);       // OPERATION_EVENT 도착

        Assert.False(g.IsPending);
        Assert.Equal(DoorUiState.Open, g.DisplayState);
        Assert.Equal(DoorUiState.Open, g.ReportedState);
        Assert.True(g.CanSend);
        Assert.False(g.LastCommandTimedOut);
    }

    [Fact]
    public void should_follow_report_even_when_it_contradicts_the_command()
    {
        // 현장/서버가 권위 — OPEN 을 보냈는데 CLOSED 가 보고되면 그대로 따른다(구동 실패 등)
        var g = Closed();
        g.TryBegin("OPEN", T0);

        g.OnStateReported(DoorUiState.Closed);

        Assert.Equal(DoorUiState.Closed, g.ReportedState);
        Assert.False(g.IsPending);
    }

    [Fact]
    public void should_time_out_pending_without_inventing_a_state()
    {
        var g = Closed(timeout: 15);
        g.TryBegin("OPEN", T0);

        Assert.False(g.Tick(T0.AddSeconds(14.9)));   // 아직
        Assert.True(g.Tick(T0.AddSeconds(15.0)));    // 해제

        Assert.False(g.IsPending);
        Assert.True(g.LastCommandTimedOut);
        // ★ 모르는 것을 지어내지 않는다 — 확정 상태는 여전히 닫힘(PRD R-2)
        Assert.Equal(DoorUiState.Closed, g.ReportedState);
        Assert.True(g.CanSend);                       // 재시도는 가능해야 한다
    }

    [Fact]
    public void should_clear_timeout_warning_on_next_command_or_report()
    {
        var g = Closed(timeout: 1);
        g.TryBegin("OPEN", T0);
        g.Tick(T0.AddSeconds(2));
        Assert.True(g.LastCommandTimedOut);

        g.TryBegin("OPEN", T0.AddSeconds(3));
        Assert.False(g.LastCommandTimedOut);

        g.Tick(T0.AddSeconds(10));                    // 다시 타임아웃
        Assert.True(g.LastCommandTimedOut);
        g.OnStateReported(DoorUiState.Open);
        Assert.False(g.LastCommandTimedOut);
    }

    [Fact]
    public void should_accept_late_report_after_timeout()
    {
        var g = Closed(timeout: 1);
        g.TryBegin("OPEN", T0);
        g.Tick(T0.AddSeconds(5));                     // 타임아웃으로 대기 해제

        g.OnStateReported(DoorUiState.Open);          // 뒤늦게 도착

        Assert.Equal(DoorUiState.Open, g.ReportedState);
        Assert.False(g.LastCommandTimedOut);
    }

    [Fact]
    public void should_not_tick_when_not_pending()
    {
        var g = Closed();
        Assert.False(g.Tick(T0.AddHours(1)));
        Assert.False(g.LastCommandTimedOut);
    }

    [Fact]
    public void should_return_to_unknown_after_reset()
    {
        var g = Closed();
        g.TryBegin("OPEN", T0);

        g.Reset();

        Assert.Equal(DoorUiState.Unknown, g.DisplayState);
        Assert.False(g.IsPending);
        Assert.False(g.CanSend);
        Assert.Null(g.PendingCommand);
    }

    [Theory]
    [InlineData("OPEN", DoorUiState.Open)]
    [InlineData("open", DoorUiState.Open)]
    [InlineData(" Open ", DoorUiState.Open)]
    [InlineData("CLOSE", DoorUiState.Closed)]
    [InlineData("CLOSED", DoorUiState.Closed)]     // 서버 상태 문자열도 관대하게 수용
    [InlineData("", DoorUiState.Unknown)]
    [InlineData(null, DoorUiState.Unknown)]
    [InlineData("HALF", DoorUiState.Unknown)]
    public void should_map_command_token_to_state(string? token, DoorUiState expected)
        => Assert.Equal(expected, DoorCommandGate.ToState(token));

    [Fact]
    public void should_use_default_timeout_when_given_nonpositive()
    {
        var g = new DoorCommandGate(0);
        g.OnStateReported(DoorUiState.Closed);
        g.TryBegin("OPEN", T0);
        Assert.False(g.Tick(T0.AddSeconds(DoorCommandGate.DefaultTimeoutSec - 0.1)));
        Assert.True(g.Tick(T0.AddSeconds(DoorCommandGate.DefaultTimeoutSec)));
    }
}
