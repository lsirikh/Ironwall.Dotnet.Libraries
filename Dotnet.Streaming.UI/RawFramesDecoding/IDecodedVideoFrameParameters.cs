using Dotnet.Streaming.UI.RawFramesDecoding.FFmpeg;

namespace Dotnet.Streaming.UI.RawFramesDecoding;
public interface IDecodedVideoFrameParameters
{
    int Height { get; }
    FFmpegPixelFormat PixelFormat { get; }
    int Width { get; }

    bool Equals(object obj);
    int GetHashCode();
}