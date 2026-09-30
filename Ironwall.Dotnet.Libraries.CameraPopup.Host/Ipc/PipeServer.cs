using System.IO.Pipes;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Ipc;

/// <summary>
/// 명명 파이프 서버 — GIS 클라이언트 하나만 받는다.
/// 거절 조건: ① 같은 사용자가 아님(<see cref="PipeOptions.CurrentUserOnly"/>) ② 클라이언트 pid ≠ 실행 인자 부모 pid
/// ③ Hello 의 토큰 · 판 · pid 불일치. 거절한 연결은 끊고 다음 연결을 기다린다(최대 <see cref="MaxAttempts"/>회).
/// </summary>
internal static class PipeServer
{
    public const int MaxAttempts = 5;
    private static readonly TimeSpan ConnectWait = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HelloWait = TimeSpan.FromSeconds(5);

    /// <summary>검증된 연결을 돌려준다. 실패하면 호스트를 종료한다(돌아오지 않음 → null).</summary>
    public static async Task<HostConnection?> AcceptAsync(HostLaunchArguments launch, HostLog log)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(launch.PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 64 * 1024, 64 * 1024);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 같은 이름을 누가 선점 — 토큰이 난수라 정상 경로에서는 없다.
                log.Error($"pipe create failed: {ex.Message}");
                HostExit.Now(HostExitCodes.ClientRejected, "pipe create failed");
                return null;
            }

            using (var wait = new CancellationTokenSource(ConnectWait))
            {
                try
                {
                    await server.WaitForConnectionAsync(wait.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    server.Dispose();
                    log.Error("no client connected in time");
                    HostExit.Now(HostExitCodes.NoClient, "no client");
                    return null;
                }
            }

            if (!NativeMethods.TryGetClientProcessId(server, out var clientPid) || clientPid != launch.ParentProcessId)
            {
                log.Warn($"reject client pid={clientPid} (expected {launch.ParentProcessId})");
                Drop(server);
                continue;
            }

            var reason = await VerifyHelloAsync(server, launch).ConfigureAwait(false);
            if (reason is not null)
            {
                log.Warn($"reject hello: {reason}");
                Drop(server);
                continue;
            }

            var ack = IpcSerializer.Serialize(new HelloAck
            {
                HostProcessId = Environment.ProcessId,
                HostVersion = typeof(PipeServer).Assembly.GetName().Version?.ToString(),
            }, 0);
            await FrameCodec.WriteFrameAsync(server, ack, CancellationToken.None).ConfigureAwait(false);
            log.Info($"client accepted pid={clientPid}");
            return new HostConnection(server, log);
        }

        HostExit.Now(HostExitCodes.ClientRejected, "too many rejected clients");
        return null;
    }

    private static async Task<string?> VerifyHelloAsync(NamedPipeServerStream server, HostLaunchArguments launch)
    {
        try
        {
            using var wait = new CancellationTokenSource(HelloWait);
            var frame = await FrameCodec.ReadFrameAsync(server, wait.Token).ConfigureAwait(false);
            if (frame is null) return "closed before hello";
            var status = IpcSerializer.TryDeserialize(frame, out var message, out _, out var type);
            if (status != DecodeStatus.Ok || message is not Hello hello) return $"first message {type} status={status}";
            if (!ProtocolVersion.IsCompatible(hello.ProtocolVersion)) return $"protocol {hello.ProtocolVersion}";
            if (!PipeNaming.TokensEqual(hello.Token, launch.Token)) return "token mismatch";
            if (hello.ClientProcessId != launch.ParentProcessId) return $"hello pid {hello.ClientProcessId}";
            return null;
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidDataException or EndOfStreamException)
        {
            return ex.GetType().Name;
        }
    }

    private static void Drop(NamedPipeServerStream server)
    {
        try { if (server.IsConnected) server.Disconnect(); } catch (IOException) { } catch (InvalidOperationException) { }
        server.Dispose();
    }
}
