using System;
using Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames;

namespace Dotnet.Streaming.UI
{
    interface IAudioSource
    {
        event EventHandler<IDecodedAudioFrame> FrameReceived;
    }
}
