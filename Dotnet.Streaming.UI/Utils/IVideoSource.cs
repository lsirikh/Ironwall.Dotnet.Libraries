using System;
using Dotnet.Streaming.UI.Darknet;
using Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames;

namespace Dotnet.Streaming.UI
{
    public interface IVideoSource
    {
        event EventHandler<IDecodedVideoFrame> FrameReceived;

    }
}