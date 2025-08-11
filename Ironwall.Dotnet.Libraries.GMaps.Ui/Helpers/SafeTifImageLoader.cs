using BitMiracle.LibTiff.Classic;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/28/2025 9:07:37 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 안전한 TIF 이미지 로더 (메모리 효율적)
/// </summary>
public static class SafeTifImageLoader
{
    // 최대 허용 이미지 크기 (메모리 보호)
    private const int MAX_IMAGE_WIDTH = 65536;  // 64K pixels
    private const int MAX_IMAGE_HEIGHT = 65536; // 64K pixels
    private const long MAX_PIXEL_COUNT = 268435456; // 256M pixels (약 1GB RAM)

    /// <summary>
    /// 안전한 TIF 이미지 로드 (크기 제한 및 오류 처리)
    /// </summary>
    public static async Task<Bitmap> LoadTifImageSafeAsync(string tifFilePath, ILogService log = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                // 1. 파일 존재 확인
                if (!File.Exists(tifFilePath))
                    throw new FileNotFoundException($"TIF 파일을 찾을 수 없습니다: {tifFilePath}");

                using var tif = Tiff.Open(tifFilePath, "r");
                if (tif == null)
                    throw new InvalidOperationException("TIF 파일을 열 수 없습니다.");

                // 2. 이미지 정보 안전하게 읽기
                var imageInfo = ReadTifImageInfoSafe(tif);
                log?.Info($"TIF 이미지 정보: {imageInfo.Width}x{imageInfo.Height}, " +
                         $"{imageInfo.BitsPerSample}bit, {imageInfo.SamplesPerPixel}samples");

                // 3. 크기 검증
                ValidateImageSize(imageInfo);

                // 4. 메모리 사용량 예상 및 검증
                var estimatedMemory = EstimateMemoryUsage(imageInfo);
                log?.Info($"예상 메모리 사용량: {estimatedMemory / (1024 * 1024):F1} MB");

                // 5. 이미지 로드 방식 결정
                if (ShouldUseMemoryOptimizedLoading(imageInfo))
                {
                    log?.Info("대용량 이미지: 메모리 최적화 로딩 사용");
                    return LoadLargeImageOptimized(tif, imageInfo, log);
                }
                else
                {
                    log?.Info("일반 크기 이미지: 표준 로딩 사용");
                    return LoadStandardImage(tif, imageInfo, log);
                }
            }
            catch (OutOfMemoryException ex)
            {
                var message = "TIF 이미지가 너무 커서 메모리에 로드할 수 없습니다.";
                log?.Error($"{message}: {ex.Message}");
                throw new InvalidOperationException(message, ex);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Parameter is not valid"))
            {
                var message = "TIF 이미지 형식이 지원되지 않거나 손상되었습니다.";
                log?.Error($"{message}: {ex.Message}");
                throw new InvalidOperationException(message, ex);
            }
            catch (Exception ex)
            {
                log?.Error($"TIF 이미지 로드 실패: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// TIF 이미지 정보 안전하게 읽기
    /// </summary>
    private static TifImageInfo ReadTifImageInfoSafe(Tiff tif)
    {
        var info = new TifImageInfo();

        try
        {
            var widthField = tif.GetField(TiffTag.IMAGEWIDTH);
            var heightField = tif.GetField(TiffTag.IMAGELENGTH);

            if (widthField == null || heightField == null)
                throw new InvalidOperationException("TIF 이미지 크기 정보를 읽을 수 없습니다.");

            info.Width = widthField[0].ToInt();
            info.Height = heightField[0].ToInt();

            // 추가 정보
            var bitsField = tif.GetField(TiffTag.BITSPERSAMPLE);
            var samplesField = tif.GetField(TiffTag.SAMPLESPERPIXEL);
            var photometricField = tif.GetField(TiffTag.PHOTOMETRIC);
            var compressionField = tif.GetField(TiffTag.COMPRESSION);

            info.BitsPerSample = bitsField?[0].ToInt() ?? 8;
            info.SamplesPerPixel = samplesField?[0].ToInt() ?? 1;
            info.PhotometricInterpretation = photometricField?[0].ToInt() ?? 1;
            info.Compression = compressionField?[0].ToInt() ?? 1;

            // 스캔라인 크기
            info.ScanlineSize = tif.ScanlineSize();

            return info;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"TIF 이미지 메타데이터 읽기 실패: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 이미지 크기 검증
    /// </summary>
    private static void ValidateImageSize(TifImageInfo info)
    {
        if (info.Width <= 0 || info.Height <= 0)
            throw new InvalidOperationException($"잘못된 이미지 크기: {info.Width}x{info.Height}");

        if (info.Width > MAX_IMAGE_WIDTH)
            throw new InvalidOperationException($"이미지 너비가 너무 큽니다: {info.Width} (최대: {MAX_IMAGE_WIDTH})");

        if (info.Height > MAX_IMAGE_HEIGHT)
            throw new InvalidOperationException($"이미지 높이가 너무 큽니다: {info.Height} (최대: {MAX_IMAGE_HEIGHT})");

        long totalPixels = (long)info.Width * info.Height;
        if (totalPixels > MAX_PIXEL_COUNT)
            throw new InvalidOperationException($"이미지가 너무 큽니다: {totalPixels:N0} pixels (최대: {MAX_PIXEL_COUNT:N0})");
    }

    /// <summary>
    /// 메모리 사용량 예상
    /// </summary>
    private static long EstimateMemoryUsage(TifImageInfo info)
    {
        long pixelCount = (long)info.Width * info.Height;
        long bytesPerPixel = Math.Max(3, (info.BitsPerSample * info.SamplesPerPixel + 7) / 8); // 최소 RGB
        return pixelCount * bytesPerPixel;
    }

    /// <summary>
    /// 메모리 최적화 로딩 필요 여부 판단
    /// </summary>
    private static bool ShouldUseMemoryOptimizedLoading(TifImageInfo info)
    {
        long pixelCount = (long)info.Width * info.Height;
        return pixelCount > 50_000_000 || // 50M pixels 이상
               info.Width > 16384 ||       // 16K width 이상
               info.Height > 16384;        // 16K height 이상
    }

    /// <summary>
    /// 표준 이미지 로드
    /// </summary>
    private static Bitmap LoadStandardImage(Tiff tif, TifImageInfo info, ILogService log = default)
    {
        var bitmap = new Bitmap(info.Width, info.Height, PixelFormat.Format24bppRgb);

        try
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, info.Width, info.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                var buffer = new byte[info.ScanlineSize];
                var progressStep = Math.Max(1, info.Height / 20); // 5% 단위로 진행률 출력

                for (int row = 0; row < info.Height; row++)
                {
                    if (!tif.ReadScanline(buffer, row))
                        throw new InvalidOperationException($"스캔라인 {row} 읽기 실패");

                    ProcessScanline(buffer, bitmapData, row, info);

                    // 진행률 출력
                    if (log != null && row % progressStep == 0)
                    {
                        var progress = (double)row / info.Height * 100;
                        log?.Info($"이미지 로딩 진행률: {progress:F1}%");
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }
        catch
        {
            bitmap?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 대용량 이미지 최적화 로드 (청크 단위)
    /// </summary>
    private static Bitmap LoadLargeImageOptimized(Tiff tif, TifImageInfo info, ILogService log)
    {
        const int CHUNK_HEIGHT = 1024; // 1K 라인씩 처리

        var bitmap = new Bitmap(info.Width, info.Height, PixelFormat.Format24bppRgb);

        try
        {
            var buffer = new byte[info.ScanlineSize];

            for (int startRow = 0; startRow < info.Height; startRow += CHUNK_HEIGHT)
            {
                int endRow = Math.Min(startRow + CHUNK_HEIGHT, info.Height);
                int chunkHeight = endRow - startRow;

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, startRow, info.Width, chunkHeight),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);

                try
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        if (!tif.ReadScanline(buffer, row))
                            throw new InvalidOperationException($"스캔라인 {row} 읽기 실패");

                        ProcessScanline(buffer, bitmapData, row - startRow, info);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                // 진행률 출력
                var progress = (double)endRow / info.Height * 100;
                log?.Info($"대용량 이미지 로딩 진행률: {progress:F1}%");

                // 가비지 컬렉션 강제 실행 (메모리 압박 해소)
                if (startRow % (CHUNK_HEIGHT * 4) == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            return bitmap;
        }
        catch
        {
            bitmap?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 스캔라인 처리 (다양한 픽셀 형식 지원)
    /// </summary>
    private static void ProcessScanline(byte[] buffer, BitmapData bitmapData, int row, TifImageInfo info)
    {
        var destPtr = bitmapData.Scan0 + (row * bitmapData.Stride);

        try
        {
            switch (info.PhotometricInterpretation)
            {
                case 0: // WhiteIsZero
                    ProcessWhiteIsZero(buffer, destPtr, info);
                    break;
                case 1: // BlackIsZero (Grayscale)
                    ProcessBlackIsZero(buffer, destPtr, info);
                    break;
                case 2: // RGB
                    ProcessRGB(buffer, destPtr, info);
                    break;
                case 3: // Palette
                    ProcessPalette(buffer, destPtr, info);
                    break;
                default:
                    // 기본적으로 그레이스케일로 처리
                    ProcessBlackIsZero(buffer, destPtr, info);
                    break;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"스캔라인 처리 실패 (행: {row}): {ex.Message}", ex);
        }
    }

    private static void ProcessBlackIsZero(byte[] buffer, IntPtr destPtr, TifImageInfo info)
    {
        for (int col = 0; col < info.Width; col++)
        {
            var pixelValue = buffer[col * info.SamplesPerPixel];
            var destOffset = col * 3;

            // BGR 순서로 저장
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset, pixelValue);     // B
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 1, pixelValue); // G
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 2, pixelValue); // R
        }
    }

    private static void ProcessWhiteIsZero(byte[] buffer, IntPtr destPtr, TifImageInfo info)
    {
        for (int col = 0; col < info.Width; col++)
        {
            var pixelValue = (byte)(255 - buffer[col * info.SamplesPerPixel]); // 반전
            var destOffset = col * 3;

            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset, pixelValue);     // B
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 1, pixelValue); // G
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 2, pixelValue); // R
        }
    }

    private static void ProcessRGB(byte[] buffer, IntPtr destPtr, TifImageInfo info)
    {
        if (info.SamplesPerPixel >= 3)
        {
            for (int col = 0; col < info.Width; col++)
            {
                var srcOffset = col * info.SamplesPerPixel;
                var destOffset = col * 3;

                var r = buffer[srcOffset];
                var g = buffer[srcOffset + 1];
                var b = buffer[srcOffset + 2];

                // BGR 순서로 저장
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset, b);     // B
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 1, g); // G
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 2, r); // R
            }
        }
        else
        {
            // RGB가 아닌 경우 그레이스케일로 처리
            ProcessBlackIsZero(buffer, destPtr, info);
        }
    }

    private static void ProcessPalette(byte[] buffer, IntPtr destPtr, TifImageInfo info)
    {
        // 팔레트 처리는 복잡하므로 일단 그레이스케일로 처리
        ProcessBlackIsZero(buffer, destPtr, info);
    }
}