namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>호스트 종료 코드. 감시자는 <see cref="IsPlannedRestart"/> 인 종료를 충돌 예산에 세지 않는다.</summary>
public static class HostExitCodes
{
    public const int Normal = 0;
    public const int BadArguments = 2;
    public const int ParentGone = 3;
    public const int NoClient = 4;
    public const int ClientRejected = 5;
    public const int UnhandledException = 10;
    public const int DispatcherException = 11;
    /// <summary>자기 메모리 한도 초과 — 깨끗이 내려가 재시작을 요청(FR-25).</summary>
    public const int MemoryLimit = 20;
    public const int ClientDisconnected = 21;

    public static bool IsPlannedRestart(int exitCode) => exitCode == MemoryLimit;
}
