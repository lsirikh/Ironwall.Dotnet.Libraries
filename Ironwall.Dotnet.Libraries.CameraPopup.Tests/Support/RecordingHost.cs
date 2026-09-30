using System.Collections.Concurrent;
using System.Reflection;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Support;

/// <summary>
/// <see cref="ICameraPopupHost"/> 가짜 — 부른 멤버와 인자를 순서대로 적는다. <see cref="DispatchProxy"/> 라
/// 창구에 멤버가 늘어도(다른 태스크가 넓혀도) 시험이 깨지지 않는다. <see cref="ThrowOnCall"/> 이면 모든 호출이 던진다(FR-27 시험).
/// </summary>
public class RecordingHost : DispatchProxy
{
    private readonly ConcurrentQueue<(string Member, object?[] Args)> _calls = new();
    private EventHandler<CameraPopupStatusEventArgs>? _status;

    public bool ThrowOnCall { get; set; }

    /// <summary>호출을 이만큼 붙잡는다(느린 감시자 흉내 — 관리자가 잠금 밖에서 부르는지 본다).</summary>
    public int DelayMs { get; set; }

    public static (ICameraPopupHost Host, RecordingHost Recorder) Create()
    {
        var host = Create<ICameraPopupHost, RecordingHost>();
        return (host, (RecordingHost)(object)host);
    }

    public IReadOnlyList<(string Member, object?[] Args)> Calls => _calls.ToArray();

    public IReadOnlyList<OpenEventWindow> Opened => Calls.Where(c => c.Member == nameof(ICameraPopupHost.OpenEventWindow))
                                                         .Select(c => (OpenEventWindow)c.Args[0]!).ToList();

    public IReadOnlyList<(string Key, EventWindowCloseReason Reason, bool ReturnHome)> Closed
        => Calls.Where(c => c.Member == nameof(ICameraPopupHost.CloseEventWindow))
                .Select(c => ((string)c.Args[0]!, (EventWindowCloseReason)c.Args[1]!, (bool)c.Args[2]!)).ToList();

    public IReadOnlyList<string> BroughtToFront => Calls.Where(c => c.Member == nameof(ICameraPopupHost.BringEventWindowToFront))
                                                        .Select(c => (string)c.Args[0]!).ToList();

    /// <summary>호스트가 보낸 상태 메시지를 흉내 낸다.</summary>
    public void Raise(IIpcMessage message) => _status?.Invoke(this, new CameraPopupStatusEventArgs(message));

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;
        var name = targetMethod.Name;
        args ??= Array.Empty<object?>();
        if (name == "add_StatusReceived") { _status += (EventHandler<CameraPopupStatusEventArgs>?)args[0]; return null; }
        if (name == "remove_StatusReceived") { _status -= (EventHandler<CameraPopupStatusEventArgs>?)args[0]; return null; }
        if (name.StartsWith("add_", StringComparison.Ordinal) || name.StartsWith("remove_", StringComparison.Ordinal)) return null;
        if (name == "get_State") return CameraPopupHostState.Running;

        _calls.Enqueue((name, args));
        if (DelayMs > 0) Thread.Sleep(DelayMs);
        if (ThrowOnCall) throw new InvalidOperationException("host broke: " + name);

        var type = targetMethod.ReturnType;
        return type == typeof(void) || !type.IsValueType ? null : Activator.CreateInstance(type);
    }
}
