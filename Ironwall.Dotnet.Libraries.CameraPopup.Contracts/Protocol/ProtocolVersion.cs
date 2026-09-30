namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// IPC 계약 판. 메시지 모양이 호환되지 않게 바뀌면 <see cref="Current"/> 를 올린다.
/// 필드 추가(선택 필드)는 판을 올리지 않는다 — 모르는 필드는 무시되고 모르는 메시지 종류는 버려진다.
/// </summary>
public static class ProtocolVersion
{
    /// <summary>현재 계약 판.</summary>
    public const int Current = 1;

    /// <summary>가장 오래된 호환 판.</summary>
    public const int MinimumSupported = 1;

    /// <summary>상대가 보낸 판을 이 쪽이 처리할 수 있는지.</summary>
    public static bool IsCompatible(int peerVersion)
        => peerVersion >= MinimumSupported && peerVersion <= Current;
}
