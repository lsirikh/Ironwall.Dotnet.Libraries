namespace Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames;

public interface IDecodedVideoFrame
{
    IDecodedVideoFrameParameters CurrentFrameParameters { get; }

    void TransformTo(nint buffer, int bufferStride, TransformParameters transformParameters);
}