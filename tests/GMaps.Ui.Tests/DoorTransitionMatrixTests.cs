using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 개폐 상태기계 <b>전이 매트릭스</b> — PRD symbol-detail-and-door-control 계획 TEST-01.
///
/// <para>개별 시나리오는 <see cref="DoorCommandGateTests"/> 가 덮는다. 여기서는
/// (현재 상태 × 명령 × 보고) <b>전 조합</b>을 돌며 <b>어떤 경로로도 깨지면 안 되는 불변식</b>을 단언한다 —
/// 조합 폭발 속에 숨는 종류의 결함(특정 순서에서만 화면이 상태를 지어냄)을 잡기 위해서다.</para>
/// </summary>
public class DoorTransitionMatrixTests
{
    private static readonly DoorUiState[] Reportable =
        { DoorUiState.Unknown, DoorUiState.Closed, DoorUiState.Open };

    private static readonly string?[] Commands = { "OPEN", "CLOSE", "open", "close", "TOGGLE", "", null };

    private static readonly DateTime T0 = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>지정 상태로 데려다 놓는다(보고 채널로만 — 명령은 확정 상태를 바꾸지 못하므로).</summary>
    private static DoorCommandGate At(DoorUiState state)
    {
        var gate = new DoorCommandGate();
        if (state != DoorUiState.Unknown) gate.OnStateReported(state);
        return gate;
    }

    [Fact]
    public void should_never_change_reported_state_by_sending_a_command()
    {
        // 서버 계약: 명령은 상태를 바꾸지 않는다. 낙관적으로 열면 구동 실패 시 화면이 거짓말을 한다.
        foreach (var start in Reportable)
            foreach (var command in Commands)
            {
                var gate = At(start);
                gate.TryBegin(command!, T0);
                Assert.Equal(start, gate.ReportedState);
            }
    }

    [Fact]
    public void should_show_pending_only_while_a_command_is_in_flight()
    {
        foreach (var start in Reportable)
            foreach (var command in Commands)
            {
                var gate = At(start);
                bool accepted = gate.TryBegin(command!, T0);

                Assert.Equal(accepted, gate.IsPending);
                Assert.Equal(accepted ? DoorUiState.Pending : start, gate.DisplayState);
            }
    }

    [Fact]
    public void should_accept_a_command_exactly_when_it_can_change_something()
    {
        // 보낼 수 있는 조건 = 상태를 알고 있고(Unknown 아님) + 이미 그 상태가 아니고 + 대기 중이 아님.
        foreach (var start in Reportable)
            foreach (var command in Commands)
            {
                var gate = At(start);
                var target = DoorCommandGate.ToState(command);
                bool expected = start != DoorUiState.Unknown
                                && target != DoorUiState.Unknown
                                && target != start;

                Assert.Equal(expected, gate.TryBegin(command!, T0));
            }
    }

    [Fact]
    public void should_resolve_pending_on_any_report_even_a_contradicting_one()
    {
        foreach (var start in new[] { DoorUiState.Closed, DoorUiState.Open })
            foreach (var command in new[] { "OPEN", "CLOSE" })
                foreach (var reported in Reportable)
                {
                    var gate = At(start);
                    if (!gate.TryBegin(command, T0)) continue;

                    gate.OnStateReported(reported);

                    Assert.False(gate.IsPending);                    // 보고는 언제나 대기를 푼다
                    Assert.Equal(reported, gate.ReportedState);      // 명령이 아니라 보고가 진실이다
                    Assert.Equal(reported, gate.DisplayState);
                }
    }

    [Fact]
    public void should_never_invent_a_state_on_timeout()
    {
        foreach (var start in new[] { DoorUiState.Closed, DoorUiState.Open })
            foreach (var command in new[] { "OPEN", "CLOSE" })
            {
                var gate = At(start);
                if (!gate.TryBegin(command, T0)) continue;

                Assert.True(gate.Tick(T0.AddSeconds(DoorCommandGate.DefaultTimeoutSec + 1)));

                Assert.False(gate.IsPending);
                Assert.True(gate.LastCommandTimedOut);
                Assert.Equal(start, gate.ReportedState);   // 타임아웃은 "모름"이지 "반대 상태"가 아니다
                Assert.Equal(start, gate.DisplayState);
            }
    }

    [Fact]
    public void should_stay_consistent_across_a_long_random_walk()
    {
        // 순서 의존 결함(특정 시퀀스에서만 대기가 안 풀리는 등)을 잡는다. 시드 고정 = 재현 가능.
        var random = new Random(20260908);
        var gate = new DoorCommandGate();
        var now = T0;

        for (int step = 0; step < 2000; step++)
        {
            now = now.AddSeconds(random.Next(0, 20));
            switch (random.Next(4))
            {
                case 0: gate.TryBegin(Commands[random.Next(Commands.Length)]!, now); break;
                case 1: gate.OnStateReported(Reportable[random.Next(Reportable.Length)]); break;
                case 2: gate.Tick(now); break;
                default: if (random.Next(20) == 0) gate.Reset(); break;
            }

            // 불변식 3개는 어떤 순서에서도 성립해야 한다.
            Assert.Equal(gate.IsPending ? DoorUiState.Pending : gate.ReportedState, gate.DisplayState);
            Assert.Equal(!gate.IsPending && gate.ReportedState != DoorUiState.Unknown, gate.CanSend);
            Assert.True(gate.ReportedState is DoorUiState.Unknown or DoorUiState.Closed or DoorUiState.Open);
        }
    }
}
