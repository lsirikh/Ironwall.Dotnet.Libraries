using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Args;

public class BroadcastSendEventArgs : EventArgs
{
    public int FileGroupId { get; }
    public int Repeat { get; }
    public int LinkedDeviceId { get; }

    public BroadcastSendEventArgs(int fileGroupId, int repeat, int linkedDeviceId)
    {
        FileGroupId = fileGroupId;
        Repeat = repeat;
        LinkedDeviceId = linkedDeviceId;
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
