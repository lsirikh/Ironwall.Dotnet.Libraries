using System;

namespace Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames
{
    class DecodedVideoFrame : IDecodedVideoFrame
    {
        private readonly Action<IntPtr, int, TransformParameters> _transformAction;

        public DecodedVideoFrame(Action<IntPtr, int, TransformParameters> transformAction)
        {
            _transformAction = transformAction;
        }

        public DecodedVideoFrame(Action<IntPtr, int, TransformParameters> transformTo, IDecodedVideoFrameParameters currentFrameParameters)
        {
            _transformAction = transformTo;
            CurrentFrameParameters = currentFrameParameters;
        }

        public IDecodedVideoFrameParameters CurrentFrameParameters { get; }

        public void TransformTo(IntPtr buffer, int bufferStride, TransformParameters transformParameters)
        {
            _transformAction(buffer, bufferStride, transformParameters);
        }
    }
}