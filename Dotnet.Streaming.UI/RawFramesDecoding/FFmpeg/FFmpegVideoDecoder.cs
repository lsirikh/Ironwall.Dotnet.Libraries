using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RtspClientSharp.RawFrames.Video;
using Dotnet.Streaming.UI.RawFramesDecoding.DecodedFrames;

namespace Dotnet.Streaming.UI.RawFramesDecoding.FFmpeg
{
    class FFmpegVideoDecoder
    {
        private readonly object _syncLock = new object();

        private readonly IntPtr _decoderHandle;
        private readonly FFmpegVideoCodecId _videoCodecId;

        private DecodedVideoFrameParameters _currentFrameParameters =
            new DecodedVideoFrameParameters(0, 0, FFmpegPixelFormat.None);

        private readonly Dictionary<TransformParameters, FFmpegDecodedVideoScaler> _scalersMap =
            new Dictionary<TransformParameters, FFmpegDecodedVideoScaler>();

        private byte[] _extraData = new byte[0];
        private bool _disposed;

        private FFmpegVideoDecoder(FFmpegVideoCodecId videoCodecId, IntPtr decoderHandle)
        {
            _videoCodecId = videoCodecId;
            _decoderHandle = decoderHandle;
        }

        ~FFmpegVideoDecoder()
        {
            Dispose();
        }

        public static FFmpegVideoDecoder CreateDecoder(FFmpegVideoCodecId videoCodecId)
        {
            int resultCode = FFmpegVideoPInvoke.CreateVideoDecoder(videoCodecId, out IntPtr decoderPtr);

            if (resultCode != 0)
                throw new DecoderException(
                    $"An error occurred while creating video decoder for {videoCodecId} codec, code: {resultCode}");

            return new FFmpegVideoDecoder(videoCodecId, decoderPtr);
        }

        #region Unactive code content
        //public unsafe IDecodedVideoFrame TryDecode(RawVideoFrame rawVideoFrame)
        //{
        //    fixed (byte* rawBufferPtr = &rawVideoFrame.FrameSegment.Array[rawVideoFrame.FrameSegment.Offset])
        //    {
        //        int resultCode;

        //        if (rawVideoFrame is RawH264IFrame rawH264IFrame)
        //        {
        //            if (rawH264IFrame.SpsPpsSegment.Array != null &&
        //                !_extraData.SequenceEqual(rawH264IFrame.SpsPpsSegment))
        //            {
        //                if (_extraData.Length != rawH264IFrame.SpsPpsSegment.Count)
        //                    _extraData = new byte[rawH264IFrame.SpsPpsSegment.Count];

        //                Buffer.BlockCopy(rawH264IFrame.SpsPpsSegment.Array, rawH264IFrame.SpsPpsSegment.Offset,
        //                    _extraData, 0, rawH264IFrame.SpsPpsSegment.Count);


        //                fixed (byte* initDataPtr = &_extraData[0])
        //                {
        //                    resultCode = FFmpegVideoPInvoke.SetVideoDecoderExtraData(_decoderHandle,
        //                        (IntPtr)initDataPtr, _extraData.Length);

        //                    if (resultCode != 0)
        //                        throw new DecoderException(
        //                            $"An error occurred while setting video extra data, {_videoCodecId} codec, code: {resultCode}");
        //                }
        //            }
        //        }

        //        try
        //        {
        //            resultCode = FFmpegVideoPInvoke.DecodeFrame(_decoderHandle, (IntPtr)rawBufferPtr,
        //            rawVideoFrame.FrameSegment.Count,
        //            out int width, out int height, out FFmpegPixelFormat pixelFormat);

        //            if (resultCode != 0)
        //                return null;

        //            if (_currentFrameParameters.Width != width || _currentFrameParameters.Height != height ||
        //                _currentFrameParameters.PixelFormat != pixelFormat)
        //            {
        //                _currentFrameParameters = new DecodedVideoFrameParameters(width, height, pixelFormat);
        //                DropAllVideoScalers();
        //            }
        //        }
        //        catch
        //        {
        //        }
        //        return new DecodedVideoFrame(TransformTo);
        //    }
        //}
        #endregion

        public unsafe IDecodedVideoFrame TryDecode(RawVideoFrame rawVideoFrame)
        {
            lock (_syncLock)
            {

                // (1) 기본 null 체크
                if (rawVideoFrame == null)
                    return null;

                // (2) FrameSegment 유효 범위 체크
                //    Array가 null이면 안 되고,
                //    Offset + Count가 실제 길이를 초과하면 안 됨.
                var segment = rawVideoFrame.FrameSegment;
                if (segment.Array == null || segment.Offset < 0 || segment.Count <= 0 ||
                    segment.Offset + segment.Count > segment.Array.Length)
                {
                    // 여기서 로그를 남기거나, 예외 발생 혹은 null 리턴
                    Debug.WriteLine($"[TryDecode] Invalid segment range. " +
                        $"ArrayNull={segment.Array == null}, " +
                        $"Offset={segment.Offset}, Count={segment.Count}, " +
                        $"ArrayLength={segment.Array?.Length}");

                    return null;
                }

                // (3) fixed 블록으로 포인터 고정
                fixed (byte* rawBufferPtr = &segment.Array[segment.Offset])
                {
                    int resultCode;

                    // (4) SPS/PPS 세팅
                    if (rawVideoFrame is RawH264IFrame rawH264IFrame)
                    {
                        // rawH264IFrame.SpsPpsSegment도 동일하게 범위 체크 필요
                        var spsPpsSeg = rawH264IFrame.SpsPpsSegment;

                        if (spsPpsSeg.Array != null && spsPpsSeg.Count > 0)
                        {
                            if (_extraData.Length != spsPpsSeg.Count)
                                _extraData = new byte[spsPpsSeg.Count];

                            // (5) BlockCopy 시 범위 초과 없는지 유의
                            Buffer.BlockCopy(spsPpsSeg.Array, spsPpsSeg.Offset, _extraData, 0, spsPpsSeg.Count);

                            // PInvoke로 ExtraData 전달
                            fixed (byte* initDataPtr = &_extraData[0])
                            {
                                int extraResult = FFmpegVideoPInvoke.SetVideoDecoderExtraData(
                                    _decoderHandle, (IntPtr)initDataPtr, _extraData.Length);

                                if (extraResult != 0)
                                    throw new DecoderException(
                                        $"Error setting extra data, {_videoCodecId}, code: {extraResult}");
                            }
                        }
                    }

                    try
                    {
                        resultCode = FFmpegVideoPInvoke.DecodeFrame(
                            _decoderHandle,
                            (IntPtr)rawBufferPtr,
                            rawVideoFrame.FrameSegment.Count,
                            out int width,
                            out int height,
                            out FFmpegPixelFormat pixelFormat);

                        if (resultCode != 0)
                            return null;

                        if (_currentFrameParameters.Width != width ||
                            _currentFrameParameters.Height != height ||
                            _currentFrameParameters.PixelFormat != pixelFormat)
                        {
                            _currentFrameParameters = new DecodedVideoFrameParameters(width, height, pixelFormat);
                            DropAllVideoScalers();
                        }

                        // 정상적으로 디코딩 완료 → DecodedVideoFrame 인스턴스 반환
                        return new DecodedVideoFrame(TransformTo, _currentFrameParameters);
                    }
                    catch (Exception ex)
                    {
                        // 반드시 로그나 예외 정보 기록(AccessViolationException 등이 여기로 잡힐 수도 있음)
                        // AccessViolationException은 일반 catch로 못 잡을 수도 있으나,
                        // 다른 예외 원인 디버깅에 도움
                        Debug.WriteLine($"DecodeFrame exception: {ex}");
                        throw;
                    }
                }
            }
        }

        //public void Dispose()
        //{
        //    if (_disposed)
        //        return;

        //    _disposed = true;
        //    FFmpegVideoPInvoke.RemoveVideoDecoder(_decoderHandle);
        //    DropAllVideoScalers();
        //    GC.SuppressFinalize(this);

        //}

        public void Dispose()
        {
            lock (_syncLock)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        // (2) Protected virtual Dispose for subclassing (이 클래스가 sealed면 private로 해도 됨)
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // (a) unmanaged 자원 해제
                FFmpegVideoPInvoke.RemoveVideoDecoder(_decoderHandle);
                DropAllVideoScalers();

                // (b) 관리 자원 해제 (if any) - disposing == true 일 때만
                // ex) managed Stream, etc.

                _disposed = true;
            }
        }

        private void DropAllVideoScalers()
        {
            foreach (var scaler in _scalersMap.Values)
                scaler.Dispose();

            _scalersMap.Clear();
        }

        //private void TransformTo(IntPtr buffer, int bufferStride, TransformParameters parameters)
        //{
        //    try
        //    {
        //        if (!_scalersMap.TryGetValue(parameters, out FFmpegDecodedVideoScaler videoScaler))
        //        {
        //            videoScaler = FFmpegDecodedVideoScaler.Create(_currentFrameParameters, parameters);
        //            _scalersMap.Add(parameters, videoScaler);
        //        }

        //        int resultCode = FFmpegVideoPInvoke.ScaleDecodedVideoFrame(_decoderHandle, videoScaler.Handle, buffer, bufferStride);

        //        if (resultCode != 0)
        //            throw new DecoderException($"An error occurred while converting decoding video frame, {_videoCodecId} codec, code: {resultCode}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"TransformTo exception: {ex}");
        //    }

        //}

        private void TransformTo(IntPtr buffer, int bufferStride, TransformParameters parameters)
        {
            try
            {

                if (buffer == IntPtr.Zero)
                    throw new InvalidOperationException("Buffer pointer is null");

                if (!_scalersMap.TryGetValue(parameters, out FFmpegDecodedVideoScaler videoScaler))
                {
                    videoScaler = FFmpegDecodedVideoScaler.Create(_currentFrameParameters, parameters);
                    if (videoScaler?.Handle == IntPtr.Zero)
                        throw new InvalidOperationException("Scaler handle is invalid");

                    _scalersMap.Add(parameters, videoScaler);
                }

                int resultCode = FFmpegVideoPInvoke.ScaleDecodedVideoFrame(_decoderHandle, videoScaler.Handle, buffer, bufferStride);

                if (resultCode != 0)
                    throw new DecoderException($"Error converting video frame, codec: {_videoCodecId}, code: {resultCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TransformTo exception: {ex}");
                throw;
            }
        }

    }
}