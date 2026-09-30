namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>호스트 상태 변화. <see cref="Reason"/> 은 사람이 읽는 사유(로그 · 안내용).</summary>
public sealed class CameraPopupHostStateChangedEventArgs : EventArgs
{
    public CameraPopupHostStateChangedEventArgs(CameraPopupHostState oldState, CameraPopupHostState newState, string? reason, int? hostProcessId, int? exitCode)
    {
        OldState = oldState;
        NewState = newState;
        Reason = reason;
        HostProcessId = hostProcessId;
        ExitCode = exitCode;
    }

    public CameraPopupHostState OldState { get; }
    public CameraPopupHostState NewState { get; }
    public string? Reason { get; }

    /// <summary>Running 이면 새 호스트 pid.</summary>
    public int? HostProcessId { get; }

    /// <summary>호스트가 스스로 끝났으면 그 종료 코드.</summary>
    public int? ExitCode { get; }
}
