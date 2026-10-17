namespace Ironwall.Dotnet.Libraries.Streaming.Models;
/****************************************************************************
   Purpose      : 재생 상태 전이 로직 - 유효한 상태 전이 규칙 정의
   Created By   : GHLee
   Created On   : 12/03/2025
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 재생 상태 전이 로직
/// 유효한 상태 전이 규칙을 정의하고 검증
/// </summary>
public static class PlaybackStateTransition
{
    /// <summary>
    /// 상태별 허용되는 전이 맵
    /// </summary>
    private static readonly Dictionary<PlaybackState, HashSet<PlaybackState>> _allowedTransitions = new()
    {
        [PlaybackState.None] = new HashSet<PlaybackState>
        {
            PlaybackState.Connecting,
            PlaybackState.Prewarming,
            PlaybackState.ImageDisplay
        },

        [PlaybackState.Prewarming] = new HashSet<PlaybackState>
        {
            PlaybackState.Ready,
            PlaybackState.Error,
            PlaybackState.Stopped
        },

        [PlaybackState.Ready] = new HashSet<PlaybackState>
        {
            PlaybackState.Connecting,
            PlaybackState.Playing,
            PlaybackState.Stopped,
            PlaybackState.Error
        },

        [PlaybackState.Connecting] = new HashSet<PlaybackState>
        {
            PlaybackState.Buffering,
            PlaybackState.Playing,
            PlaybackState.Error,
            PlaybackState.Stopped,
            PlaybackState.Disconnected
        },

        [PlaybackState.Buffering] = new HashSet<PlaybackState>
        {
            PlaybackState.Playing,
            PlaybackState.Error,
            PlaybackState.Stopped,
            PlaybackState.Reconnecting,
            PlaybackState.Disconnected
        },

        [PlaybackState.Playing] = new HashSet<PlaybackState>
        {
            PlaybackState.Paused,
            PlaybackState.Buffering,
            PlaybackState.Degraded,
            PlaybackState.Stopped,
            PlaybackState.Error,
            PlaybackState.Reconnecting,
            PlaybackState.Disconnected,
            PlaybackState.Restricted
        },

        [PlaybackState.Degraded] = new HashSet<PlaybackState>
        {
            PlaybackState.Playing,
            PlaybackState.Buffering,
            PlaybackState.Paused,
            PlaybackState.Stopped,
            PlaybackState.Error,
            PlaybackState.Reconnecting,
            PlaybackState.Disconnected
        },

        [PlaybackState.Paused] = new HashSet<PlaybackState>
        {
            PlaybackState.Playing,
            PlaybackState.Stopped,
            PlaybackState.Disconnected
        },

        [PlaybackState.Stopped] = new HashSet<PlaybackState>
        {
            PlaybackState.None,
            PlaybackState.Connecting,
            PlaybackState.Prewarming
        },

        [PlaybackState.Error] = new HashSet<PlaybackState>
        {
            PlaybackState.None,
            PlaybackState.Connecting,
            PlaybackState.Reconnecting,
            PlaybackState.Stopped
        },

        [PlaybackState.Reconnecting] = new HashSet<PlaybackState>
        {
            PlaybackState.Connecting,
            PlaybackState.Buffering,
            PlaybackState.Playing,
            PlaybackState.Error,
            PlaybackState.Stopped,
            PlaybackState.Disconnected
        },

        [PlaybackState.Disconnected] = new HashSet<PlaybackState>
        {
            PlaybackState.None,
            PlaybackState.Connecting,
            PlaybackState.Reconnecting,
            PlaybackState.Stopped
        },

        [PlaybackState.ImageDisplay] = new HashSet<PlaybackState>
        {
            PlaybackState.None,
            PlaybackState.Connecting,
            PlaybackState.Stopped
        },

        [PlaybackState.Restricted] = new HashSet<PlaybackState>
        {
            PlaybackState.Playing,
            PlaybackState.Stopped,
            PlaybackState.None
        }
    };

    /// <summary>
    /// 상태 전이가 유효한지 확인
    /// </summary>
    public static bool IsValidTransition(PlaybackState from, PlaybackState to)
    {
        if (from == to)
            return true;

        if (_allowedTransitions.TryGetValue(from, out var allowedStates))
        {
            return allowedStates.Contains(to);
        }

        return false;
    }

    /// <summary>
    /// 특정 상태에서 허용되는 전이 목록 반환
    /// </summary>
    public static IReadOnlyCollection<PlaybackState> GetAllowedTransitions(PlaybackState from)
    {
        if (_allowedTransitions.TryGetValue(from, out var allowedStates))
        {
            return allowedStates;
        }

        return Array.Empty<PlaybackState>();
    }

    /// <summary>
    /// 재생 중이거나 유사한 상태인지 확인 (프레임 출력 중)
    /// </summary>
    public static bool IsPlayingOrSimilar(PlaybackState state)
    {
        return state == PlaybackState.Playing || state == PlaybackState.Degraded;
    }

    /// <summary>
    /// 활성 상태인지 확인 (연결/재생 중)
    /// </summary>
    public static bool IsActiveState(PlaybackState state)
    {
        return state == PlaybackState.Connecting ||
               state == PlaybackState.Buffering ||
               state == PlaybackState.Playing ||
               state == PlaybackState.Degraded ||
               state == PlaybackState.Paused ||
               state == PlaybackState.Reconnecting ||
               state == PlaybackState.Prewarming ||
               state == PlaybackState.Ready;
    }

    /// <summary>
    /// 오류 상태인지 확인
    /// </summary>
    public static bool IsErrorState(PlaybackState state)
    {
        return state == PlaybackState.Error || state == PlaybackState.Disconnected;
    }

    /// <summary>
    /// 종료 상태인지 확인
    /// </summary>
    public static bool IsTerminalState(PlaybackState state)
    {
        return state == PlaybackState.None ||
               state == PlaybackState.Stopped ||
               state == PlaybackState.Error ||
               state == PlaybackState.Disconnected;
    }

    /// <summary>
    /// 상태 설명 반환
    /// </summary>
    public static string GetStateDescription(PlaybackState state)
    {
        return state switch
        {
            PlaybackState.None => "초기 상태",
            PlaybackState.Connecting => "연결 중",
            PlaybackState.Buffering => "버퍼링 중",
            PlaybackState.Playing => "재생 중",
            PlaybackState.Paused => "일시 정지",
            PlaybackState.Stopped => "정지됨",
            PlaybackState.Error => "에러 발생",
            PlaybackState.Reconnecting => "재연결 중",
            PlaybackState.Disconnected => "연결 끊김",
            PlaybackState.ImageDisplay => "이미지 표시",
            PlaybackState.Restricted => "감시 제한",
            PlaybackState.Prewarming => "연결 준비 중",
            PlaybackState.Ready => "준비 완료",
            PlaybackState.Degraded => "성능 저하",
            _ => "알 수 없음"
        };
    }
}
