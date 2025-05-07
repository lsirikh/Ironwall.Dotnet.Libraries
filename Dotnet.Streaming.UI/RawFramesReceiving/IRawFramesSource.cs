using System;
using RtspClientSharp.RawFrames;

namespace Dotnet.Streaming.UI.RawFramesReceiving
{
    interface IRawFramesSource
    {
        EventHandler<RawFrame>? FrameReceived { get; set; }
        EventHandler<string>? ConnectionStatusChanged { get; set; }

        void Start();
        void Stop();
    }
}