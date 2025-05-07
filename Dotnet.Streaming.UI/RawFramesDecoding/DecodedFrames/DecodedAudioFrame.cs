using System;

namespace Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames
{
    class DecodedAudioFrame : IDecodedAudioFrame
    {
        public DateTime Timestamp { get; }
        public ArraySegment<byte> DecodedBytes { get; }
        public AudioFrameFormat Format { get; }

        public DecodedAudioFrame(DateTime timestamp, ArraySegment<byte> decodedData, AudioFrameFormat format)
        {
            Timestamp = timestamp;
            DecodedBytes = decodedData;
            Format = format;
        }
    }
}
