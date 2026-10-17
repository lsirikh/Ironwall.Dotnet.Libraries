using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 메시지 종류 문자열 ↔ CLR 타입 대응표. 종류 문자열은 계약이다 — 클래스 이름을 바꿔도 여기 문자열은 유지한다.
/// 모르는 종류는 <see cref="TryGetType"/> 가 false — 받는 쪽은 버리고 계속한다(앞으로 호환).
/// </summary>
public static class MessageRegistry
{
    private static readonly Dictionary<string, Type> ByName = new(StringComparer.Ordinal)
    {
        ["Hello"] = typeof(Hello),
        ["HelloAck"] = typeof(HelloAck),
        ["Heartbeat"] = typeof(Heartbeat),
        ["HeartbeatAck"] = typeof(HeartbeatAck),
        ["OpenOverlayStream"] = typeof(OpenOverlayStream),
        ["CloseStream"] = typeof(CloseStream),
        ["OpenEventWindow"] = typeof(OpenEventWindow),
        ["CloseEventWindow"] = typeof(CloseEventWindow),
        ["BringToFront"] = typeof(BringToFront),
        ["SetTheme"] = typeof(SetTheme),
        ["Ptz"] = typeof(PtzCommand),
        ["PtzFocus"] = typeof(PtzFocusCommand),
        ["CameraRequest"] = typeof(CameraRequest),
        ["CameraResponse"] = typeof(CameraResponse),
        ["Debug"] = typeof(DebugCommand),
        ["StreamStateChanged"] = typeof(StreamStateChanged),
        ["WindowOpened"] = typeof(WindowOpened),
        ["WindowClosed"] = typeof(WindowClosed),
        ["WindowMoved"] = typeof(WindowMoved),
        ["TileClosed"] = typeof(TileClosed),
        ["PinChanged"] = typeof(PinChanged),
        ["HostError"] = typeof(HostError),
    };

    private static readonly Dictionary<Type, string> ByType = ByName.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>등록된 모든 종류 이름.</summary>
    public static IReadOnlyCollection<string> Names => ByName.Keys;

    public static bool TryGetType(string name, out Type type)
    {
        if (name is not null && ByName.TryGetValue(name, out var t)) { type = t; return true; }
        type = typeof(object);
        return false;
    }

    /// <summary>CLR 타입의 종류 이름. 등록되지 않은 타입이면 <see cref="ArgumentException"/>.</summary>
    public static string NameOf(Type type)
        => ByType.TryGetValue(type, out var name)
            ? name
            : throw new ArgumentException($"IPC 메시지로 등록되지 않은 타입: {type.FullName}", nameof(type));
}
