using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

public class BroadcastSendEventArgs : EventArgs
{
    public int FileGroupId { get; }
    public int Repeat { get; }
    public int LinkedDeviceId { get; }
    /// <summary>음량 0~100(0 허용) — 8.0.15 BROADCAST_PLAY volume. 옛 생성자는 100(생략 시 매니저 기본과 같음).</summary>
    public int Volume { get; } = 100;

    public BroadcastSendEventArgs(int fileGroupId, int repeat, int linkedDeviceId)
    {
        FileGroupId = fileGroupId;
        Repeat = repeat;
        LinkedDeviceId = linkedDeviceId;
    }

    public BroadcastSendEventArgs(int fileGroupId, int repeat, int linkedDeviceId, int volume)
        : this(fileGroupId, repeat, linkedDeviceId)
    {
        Volume = volume;
    }
}

public class TtsSendEventArgs : EventArgs
{
    public string Message { get; }
    public int LinkedDeviceId { get; }

    public TtsSendEventArgs(string message, int linkedDeviceId)
    {
        Message = message;
        LinkedDeviceId = linkedDeviceId;
    }
}
