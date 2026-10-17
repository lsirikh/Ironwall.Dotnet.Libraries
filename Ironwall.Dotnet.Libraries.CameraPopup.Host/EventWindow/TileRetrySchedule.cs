using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 타일 하나의 자동 재시도 일정(순수 상태 — 시계는 밖에서 준다). 실패하면 <see cref="StreamRetryBackoff"/> 간격 뒤로 예약하고,
/// 재생되면 횟수를 0 으로 되돌린다. 사람이 "다시 시도"를 누르면 <see cref="Reset"/>.
/// </summary>
internal sealed class TileRetrySchedule
{
    private DateTime? _dueAt;

    /// <summary>지금까지 이어진 실패 횟수(재생되면 0).</summary>
    public int Failures { get; private set; }

    public DateTime? DueAt => _dueAt;

    /// <summary>스트림 상태를 반영한다. 재시도를 예약했으면 true.</summary>
    public bool OnState(StreamState state, string? detail, DateTime now)
    {
        switch (state)
        {
            case StreamState.Playing:
                Failures = 0;
                _dueAt = null;
                return false;
            case StreamState.Failed or StreamState.Stalled:
                if (!StreamRetryBackoff.ShouldRetry(detail))
                {
                    _dueAt = null;
                    return false;
                }
                Failures++;
                _dueAt = now + StreamRetryBackoff.DelayFor(Failures);
                return true;
            default:
                return false;
        }
    }

    /// <summary>때가 됐으면 예약을 꺼내고 몇 번째 재시도인지 돌려준다.</summary>
    public bool TryTake(DateTime now, out int attempt)
    {
        attempt = Failures;
        if (_dueAt is not { } due || now < due) return false;
        _dueAt = null;
        return true;
    }

    public void Reset()
    {
        Failures = 0;
        _dueAt = null;
    }
}
