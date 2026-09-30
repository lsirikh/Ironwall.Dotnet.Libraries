using System.Diagnostics;
using System.IO.Pipes;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>호스트 exe 를 직접 띄워 파이프 보안 · 부모 감시를 본다.</summary>
[Collection(SurvivalCollection.Name)]
[Trait("Category", "Survival")]
public class HostProcessTests
{
    private readonly ITestOutputHelper _output;

    public HostProcessTests(ITestOutputHelper output) => _output = output;

    private static Process StartStandInParent()
    {
        // 부모 역할 대역 — 오래 사는 무해한 프로세스.
        var psi = new ProcessStartInfo("ping", "-n 120 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        var p = Process.Start(psi)!;
        p.BeginOutputReadLine();
        return p;
    }

    private static (Process Host, HostLaunchArguments Args) StartHost(int parentPid)
    {
        var token = PipeNaming.CreateToken();
        var args = new HostLaunchArguments
        {
            ParentProcessId = parentPid,
            PipeName = PipeNaming.Build(parentPid, token),
            Token = token,
            Headless = true,
            LogDirectory = HostPaths.NewLogDirectory(),
        };
        var psi = new ProcessStartInfo(HostPaths.RequireHostExe()) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args.ToArgumentList()) psi.ArgumentList.Add(a);
        return (Process.Start(psi)!, args);
    }

    private static async Task<bool> TryHandshakeAsync(HostLaunchArguments args, string token)
    {
        using var pipe = new NamedPipeClientStream(".", args.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await pipe.ConnectAsync(cts.Token);
        try
        {
            await FrameCodec.WriteFrameAsync(pipe, IpcSerializer.Serialize(new Hello { Token = token, ClientProcessId = Environment.ProcessId }, 1), cts.Token);
            var frame = await FrameCodec.ReadFrameAsync(pipe, cts.Token);
            return frame is not null
                   && IpcSerializer.TryDeserialize(frame, out var msg, out _, out _) == DecodeStatus.Ok
                   && msg is HelloAck;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or InvalidDataException)
        {
            return false;
        }
    }

    [Fact]
    public async Task should_reject_client_when_process_is_not_the_parent()
    {
        using var parent = StartStandInParent();
        var (host, args) = StartHost(parent.Id);
        using var _ = host;
        try
        {
            // 토큰까지 맞게 보내도 pid 가 부모가 아니므로 거절된다.
            Assert.False(await TryHandshakeAsync(args, args.Token));
            Assert.False(host.HasExited); // 거절 후에도 진짜 부모를 기다린다
        }
        finally
        {
            parent.Kill();
            Assert.True(host.WaitForExit(5000), "host did not exit after parent died");
            Assert.Equal(HostExitCodes.ParentGone, host.ExitCode);
        }
    }

    [Fact]
    public async Task should_reject_hello_when_token_is_wrong()
    {
        var (host, args) = StartHost(Environment.ProcessId);
        using var _ = host;
        try
        {
            Assert.False(await TryHandshakeAsync(args, PipeNaming.CreateToken()));
            Assert.True(await TryHandshakeAsync(args, args.Token)); // 올바른 토큰은 그 뒤에도 받아들여진다
        }
        finally
        {
            if (!host.HasExited) host.Kill();
        }
    }

    [Fact]
    public void should_exit_when_parent_process_dies()
    {
        using var parent = StartStandInParent();
        var (host, _) = StartHost(parent.Id);
        using var _h = host;

        Thread.Sleep(800);
        Assert.False(host.HasExited);
        var sw = Stopwatch.StartNew();
        parent.Kill();

        Assert.True(host.WaitForExit(5000));
        _output.WriteLine($"parent death → host exit {sw.ElapsedMilliseconds} ms code={host.ExitCode}");
        Assert.Equal(HostExitCodes.ParentGone, host.ExitCode);
    }

    [Fact]
    public void should_exit_with_bad_arguments_code_when_arguments_are_invalid()
    {
        var psi = new ProcessStartInfo(HostPaths.RequireHostExe(), "--parent-pid 1 --pipe x --token y") { UseShellExecute = false, CreateNoWindow = true };
        using var host = Process.Start(psi)!;

        Assert.True(host.WaitForExit(10_000));
        Assert.Equal(HostExitCodes.BadArguments, host.ExitCode);
    }
}
