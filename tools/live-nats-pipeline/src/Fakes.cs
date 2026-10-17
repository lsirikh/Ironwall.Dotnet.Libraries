using System.Collections.Concurrent;
using System.Reflection;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;

namespace LiveNatsPipeline;

/// <summary>Contract probe that never touches the network (the real one GETs /api/info on the test server).</summary>
public sealed class FakeProbe : IServerContractProbe
{
    public EnumServerContract Contract => EnumServerContract.V8_0;
    public string? RawVersion => "8.0.1-probe";
    public bool IsResolved => true;
    public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
}

public sealed record ProxyCall(DateTime At, string Type, string Method, string Args);

/// <summary>
/// Records every call on an interface we do not want to run for real (Redis publish, sound playback)
/// and returns a neutral completed result.
/// </summary>
public class RecordingProxy<T> : DispatchProxy where T : class
{
    public static readonly ConcurrentQueue<ProxyCall> Calls = new();

    public static T Create() => DispatchProxy.Create<T, RecordingProxy<T>>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) return null;
        if (!targetMethod.IsSpecialName)
            Calls.Enqueue(new ProxyCall(DateTime.Now, typeof(T).Name, targetMethod.Name,
                string.Join(", ", (args ?? Array.Empty<object?>()).Select(a => Recorder.Trunc(a?.ToString() ?? "null", 200)))));
        return DefaultFor(targetMethod.ReturnType);
    }

    internal static object? DefaultFor(Type t)
    {
        if (t == typeof(void)) return null;
        if (t == typeof(Task)) return Task.CompletedTask;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var inner = t.GetGenericArguments()[0];
            var value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, new[] { value });
        }
        if (t.IsValueType) return Activator.CreateInstance(t);
        return null;
    }
}

/// <summary>Records every EventAggregator message (card entry mapping, membership changes, sync notices ...).</summary>
public sealed class EaRecorder : IHandle<object>
{
    public readonly ConcurrentQueue<(DateTime At, object Message)> Messages = new();
    public Task HandleAsync(object message, CancellationToken cancellationToken)
    {
        Messages.Enqueue((DateTime.Now, message));
        return Task.CompletedTask;
    }
    public IEnumerable<T> OfType<T>(DateTime since) => Messages.Where(m => m.At >= since).Select(m => m.Message).OfType<T>();
}

public sealed record PipelinePublish(DateTime At, string Subject, string Data, string Kind);

/// <summary>
/// Decorator over the library's (internal) NatsService: forwards everything, but REFUSES any publish/request
/// outside sensorway.unit999.* and records what the pipeline itself put on the wire (e.g. ACTION_REPORT).
/// </summary>
public sealed class GuardedNatsService : INatsService
{
    readonly INatsService _inner;
    public readonly ConcurrentQueue<PipelinePublish> Published = new();

    public GuardedNatsService(INatsService inner) { _inner = inner; }

    public string Subject => _inner.Subject;
    public INatsService Inner => _inner;

    public INatsService? Connect(INatsSetupModel setupModel) { AssertSetup(setupModel); return _inner.Connect(setupModel) is null ? null : this; }
    public async Task<INatsService?> ConnectAsync(INatsSetupModel setupModel) { AssertSetup(setupModel); return await _inner.ConnectAsync(setupModel) is null ? null : this; }

    public Task PublishAsync(string subject, string data)
    {
        Safety.AssertSubject(subject);
        Published.Enqueue(new PipelinePublish(DateTime.Now, subject, data, "PUB"));
        return _inner.PublishAsync(subject, data);
    }

    public Task<string?> RequestAsync(string subject, string data, TimeSpan? timeout = null)
    {
        Safety.AssertSubject(subject);
        Published.Enqueue(new PipelinePublish(DateTime.Now, subject, data, "REQ"));
        return _inner.RequestAsync(subject, data, timeout);
    }

    public event EventHandler<MessageArgsModel> NatsSubscribeEvent
    {
        add => _inner.NatsSubscribeEvent += value;
        remove => _inner.NatsSubscribeEvent -= value;
    }

    public event Func<MessageArgsModel, Task> NatsSubscribeEventAsync
    {
        add => _inner.NatsSubscribeEventAsync += value;
        remove => _inner.NatsSubscribeEventAsync -= value;
    }

    public Task ExecuteAsync(CancellationToken token = default) => _inner.ExecuteAsync(token);
    public Task StopAsync(CancellationToken token = default) => _inner.StopAsync(token);

    static void AssertSetup(INatsSetupModel s)
    {
        Safety.AssertLoopback($"nats://{s.IpAddressNats}:{s.PortNats}");
        if (s.DomainNats != Safety.Domain || s.GroupNats != Safety.Group)
            throw new InvalidOperationException($"SAFETY ABORT: pipeline group must be {Safety.Domain}.{Safety.Group}, got {s.DomainNats}.{s.GroupNats}");
    }
}
