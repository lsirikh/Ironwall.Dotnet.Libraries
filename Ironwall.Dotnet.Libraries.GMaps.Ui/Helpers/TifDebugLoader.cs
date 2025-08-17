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
   Created On   : 7/28/2025 9:19:08 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// TIF 파일 디버깅 및 안전한 로더
/// </summary>
public static class TifDebugLoader
{
    /// <summary>
    /// TIF 파일 정보 출력 및 디버깅
    /// </summary>
    public static async Task<TifFileInfo> DebugTifFileAsync(string tifFilePath, ILogService log)
    {
        return await Task.Run(() =>
        {
            var info = new TifFileInfo { FilePath = tifFilePath };

            try
            {
                // 1. 파일 기본 정보
                var fileInfo = new FileInfo(tifFilePath);
                info.FileSize = fileInfo.Length;
                info.FileExists = fileInfo.Exists;

                log?.Info($"=== TIF 파일 디버깅 시작 ===");
                log?.Info($"파일 경로: {tifFilePath}");
                log?.Info($"파일 크기: {info.FileSize:N0} bytes ({info.FileSize / (1024.0 * 1024.0):F1} MB)");
                log?.Info($"파일 존재: {info.FileExists}");

                if (!info.FileExists)
                {
                    info.ErrorMessage = "파일이 존재하지 않습니다.";
                    return info;
                }

                // 2. TIFF 파일 열기
                using var tif = Tiff.Open(tifFilePath, "r");
                if (tif == null)
                {
                    info.ErrorMessage = "TIFF 파일을 열 수 없습니다.";
                    return info;
                }

                // 3. 기본 태그 정보 읽기
                ReadBasicTags(tif, info, log);

                // 4. 고급 태그 정보 읽기
                ReadAdvancedTags(tif, info, log);

                // 5. 메모리 요구사항 계산
                CalculateMemoryRequirements(info, log);

                // 6. System.Drawing.Bitmap 호환성 검사
                CheckBitmapCompatibility(info, log);

                info.IsValid = true;
                log?.Info($"=== TIF 파일 디버깅 완료 ===");
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"디버깅 중 오류 발생: {ex.Message}";
                log?.Error(info.ErrorMessage);
            }

            return info;
        });
    }

    /// <summary>
    /// 기본 태그 정보 읽기
    /// </summary>
    private static void ReadBasicTags(Tiff tif, TifFileInfo info, ILogService log)
    {
        // 필수 태그들
        var widthField = tif.GetField(TiffTag.IMAGEWIDTH);
        var heightField = tif.GetField(TiffTag.IMAGELENGTH);
        var bitsField = tif.GetField(TiffTag.BITSPERSAMPLE);
        var samplesField = tif.GetField(TiffTag.SAMPLESPERPIXEL);
        var photometricField = tif.GetField(TiffTag.PHOTOMETRIC);
        var compressionField = tif.GetField(TiffTag.COMPRESSION);

        info.Width = widthField?[0].ToInt() ?? 0;
        info.Height = heightField?[0].ToInt() ?? 0;
        info.BitsPerSample = bitsField?[0].ToInt() ?? 8;
        info.SamplesPerPixel = samplesField?[0].ToInt() ?? 1;
        info.PhotometricInterpretation = photometricField?[0].ToInt() ?? 1;
        info.Compression = compressionField?[0].ToInt() ?? 1;

        log?.Info($"이미지 크기: {info.Width} x {info.Height}");
        log?.Info($"비트 깊이: {info.BitsPerSample} bits/sample");
        log?.Info($"샘플 수: {info.SamplesPerPixel}");
        log?.Info($"Photometric: {info.PhotometricInterpretation} ({GetPhotometricName(info.PhotometricInterpretation)})");
        log?.Info($"압축: {info.Compression} ({GetCompressionName(info.Compression)})");

        // 스캔라인 크기
        info.ScanlineSize = tif.ScanlineSize();
        log?.Info($"스캔라인 크기: {info.ScanlineSize} bytes");
    }

    /// <summary>
    /// 고급 태그 정보 읽기
    /// </summary>
    private static void ReadAdvancedTags(Tiff tif, TifFileInfo info, ILogService log)
    {
        // 해상도 정보
        var xResField = tif.GetField(TiffTag.XRESOLUTION);
        var yResField = tif.GetField(TiffTag.YRESOLUTION);
        var resUnitField = tif.GetField(TiffTag.RESOLUTIONUNIT);

        if (xResField != null && yResField != null)
        {
            info.XResolution = xResField[0].ToDouble();
            info.YResolution = yResField[0].ToDouble();
            info.ResolutionUnit = resUnitField?[0].ToInt() ?? 2;
            log?.Info($"해상도: {info.XResolution:F2} x {info.YResolution:F2} {GetResolutionUnitName(info.ResolutionUnit)}");
        }

        // 타일 정보
        var tileWidthField = tif.GetField(TiffTag.TILEWIDTH);
        var tileHeightField = tif.GetField(TiffTag.TILELENGTH);

        if (tileWidthField != null && tileHeightField != null)
        {
            info.IsTiled = true;
            info.TileWidth = tileWidthField[0].ToInt();
            info.TileHeight = tileHeightField[0].ToInt();
            log?.Info($"타일 구조: {info.TileWidth} x {info.TileHeight}");
        }
        else
        {
            info.IsTiled = false;
            log?.Info("스트립 구조 (비타일)");
        }

        // 색상 정보
        var colormapField = tif.GetField(TiffTag.COLORMAP);
        info.HasColormap = colormapField != null;
        if (info.HasColormap)
        {
            log?.Info("컬러맵 포함");
        }

        // GeoTIFF 태그 확인
        var geoKeysField = tif.GetField(TiffTag.GEOTIFF_GEOKEYDIRECTORYTAG);
        info.IsGeoTiff = geoKeysField != null;
        log?.Info($"GeoTIFF: {(info.IsGeoTiff ? "예" : "아니오")}");
    }

    /// <summary>
    /// 메모리 요구사항 계산
    /// </summary>
    private static void CalculateMemoryRequirements(TifFileInfo info, ILogService log)
    {
        if (info.Width <= 0 || info.Height <= 0) return;

        long totalPixels = (long)info.Width * info.Height;
        long bytesPerPixel = (info.BitsPerSample * info.SamplesPerPixel + 7) / 8;
        long rawImageSize = totalPixels * bytesPerPixel;
        long bitmapSize = totalPixels * 3; // RGB 24bpp

        info.TotalPixels = totalPixels;
        info.EstimatedRawSize = rawImageSize;
        info.EstimatedBitmapSize = bitmapSize;

        log?.Info($"총 픽셀 수: {totalPixels:N0}");
        log?.Info($"원본 이미지 크기: {rawImageSize:N0} bytes ({rawImageSize / (1024.0 * 1024.0):F1} MB)");
        log?.Info($"비트맵 메모리: {bitmapSize:N0} bytes ({bitmapSize / (1024.0 * 1024.0):F1} MB)");
    }

    /// <summary>
    /// System.Drawing.Bitmap 호환성 검사
    /// </summary>
    private static void CheckBitmapCompatibility(TifFileInfo info, ILogService log)
    {
        var issues = new List<string>();

        // 크기 제한 검사
        if (info.Width <= 0 || info.Height <= 0)
            issues.Add($"잘못된 이미지 크기: {info.Width}x{info.Height}");

        if (info.Width > 65535)
            issues.Add($"폭이 너무 큼: {info.Width} (최대: 65535)");

        if (info.Height > 65535)
            issues.Add($"높이가 너무 큼: {info.Height} (최대: 65535)");

        // 메모리 제한 검사 (2GB 제한)
        if (info.EstimatedBitmapSize > int.MaxValue)
            issues.Add($"이미지가 너무 커서 Bitmap으로 로드 불가: {info.EstimatedBitmapSize:N0} bytes");

        // 픽셀 수 제한
        if (info.TotalPixels > 268435456) // 256M pixels
            issues.Add($"픽셀 수가 너무 많음: {info.TotalPixels:N0}");

        // 형식 호환성
        if (info.BitsPerSample > 16)
            issues.Add($"비트 깊이가 너무 높음: {info.BitsPerSample} (권장: 8 또는 16)");

        if (info.SamplesPerPixel > 4)
            issues.Add($"샘플 수가 너무 많음: {info.SamplesPerPixel} (권장: 1-4)");

        info.CompatibilityIssues = issues;
        info.IsBitmapCompatible = issues.Count == 0;

        if (info.IsBitmapCompatible)
        {
            log?.Info("✅ System.Drawing.Bitmap 호환성: 양호");
        }
        else
        {
            log?.Error("❌ System.Drawing.Bitmap 호환성 문제:");
            foreach (var issue in issues)
            {
                log?.Error($"  - {issue}");
            }
        }
    }

    /// <summary>
    /// 안전한 TIF 이미지 로드 (디버깅 포함)
    /// </summary>
    public static async Task<Bitmap> LoadTifImageSafeWithDebugAsync(string tifFilePath, ILogService log)
    {
        // 1. 먼저 파일 디버깅
        var debugInfo = await DebugTifFileAsync(tifFilePath, log);

        if (!debugInfo.IsValid)
        {
            throw new InvalidOperationException($"TIF 파일 분석 실패: {debugInfo.ErrorMessage}");
        }

        if (!debugInfo.IsBitmapCompatible)
        {
            var issues = string.Join(", ", debugInfo.CompatibilityIssues);
            throw new InvalidOperationException($"System.Drawing.Bitmap 호환성 문제: {issues}");
        }

        // 2. 호환성이 확인되면 로드 시도
        return await Task.Run(() =>
        {
            try
            {
                log?.Info("안전한 이미지 로드 시작...");

                using var tif = Tiff.Open(tifFilePath, "r");
                if (tif == null)
                    throw new InvalidOperationException("TIF 파일을 열 수 없습니다.");

                // 크기 재확인 (안전장치)
                var width = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                var height = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

                if (width != debugInfo.Width || height != debugInfo.Height)
                {
                    throw new InvalidOperationException("이미지 크기가 디버깅 시점과 다릅니다.");
                }

                log?.Info($"Bitmap 생성 시도: {width}x{height}");

                // Bitmap 생성 (가장 중요한 부분)
                Bitmap bitmap;
                try
                {
                    bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    log?.Info("✅ Bitmap 생성 성공");
                }
                catch (ArgumentException ex)
                {
                    log?.Error($"❌ Bitmap 생성 실패: {ex.Message}");
                    log?.Error($"시도한 크기: {width}x{height}");
                    log?.Error($"예상 메모리: {(long)width * height * 3:N0} bytes");
                    throw new InvalidOperationException($"Bitmap 생성 실패 (크기: {width}x{height}): {ex.Message}", ex);
                }

                try
                {
                    // 이미지 데이터 로드
                    LoadImageData(tif, bitmap, debugInfo, log);
                    log?.Info("✅ 이미지 데이터 로드 완료");
                    return bitmap;
                }
                catch
                {
                    bitmap?.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                log?.Error($"이미지 로드 중 오류: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 이미지 데이터 로드
    /// </summary>
    private static void LoadImageData(Tiff tif, Bitmap bitmap, TifFileInfo info, ILogService log)
    {
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, info.Width, info.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format24bppRgb);

        try
        {
            var buffer = new byte[info.ScanlineSize];
            var progressStep = Math.Max(1, info.Height / 10);

            for (int row = 0; row < info.Height; row++)
            {
                if (!tif.ReadScanline(buffer, row))
                    throw new InvalidOperationException($"스캔라인 {row} 읽기 실패");

                ProcessScanlineToRgb(buffer, bitmapData, row, info);

                if (row % progressStep == 0)
                {
                    var percent = (double)row / info.Height * 100;
                    log?.Info($"로딩 진행률: {percent:F1}%");
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    /// <summary>
    /// 스캔라인을 RGB로 변환
    /// </summary>
    private static void ProcessScanlineToRgb(byte[] buffer, BitmapData bitmapData, int row, TifFileInfo info)
    {
        var destPtr = bitmapData.Scan0 + (row * bitmapData.Stride);

        for (int col = 0; col < info.Width; col++)
        {
            byte r, g, b;

            // Photometric 해석에 따른 색상 변환
            switch (info.PhotometricInterpretation)
            {
                case 0: // WhiteIsZero
                    var grayValue = (byte)(255 - buffer[col * info.SamplesPerPixel]);
                    r = g = b = grayValue;
                    break;

                case 1: // BlackIsZero
                    r = g = b = buffer[col * info.SamplesPerPixel];
                    break;

                case 2: // RGB
                    if (info.SamplesPerPixel >= 3)
                    {
                        var srcOffset = col * info.SamplesPerPixel;
                        r = buffer[srcOffset];
                        g = buffer[srcOffset + 1];
                        b = buffer[srcOffset + 2];
                    }
                    else
                    {
                        r = g = b = buffer[col * info.SamplesPerPixel];
                    }
                    break;

                default:
                    r = g = b = buffer[col * info.SamplesPerPixel];
                    break;
            }

            var destOffset = col * 3;
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset, b);     // B
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 1, g); // G
            System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 2, r); // R
        }
    }

    #region Helper Methods

    private static string GetPhotometricName(int photometric)
    {
        return photometric switch
        {
            0 => "WhiteIsZero",
            1 => "BlackIsZero",
            2 => "RGB",
            3 => "Palette",
            4 => "Transparency",
            5 => "CMYK",
            6 => "YCbCr",
            8 => "CIELab",
            _ => "Unknown"
        };
    }

    private static string GetCompressionName(int compression)
    {
        return compression switch
        {
            1 => "None",
            2 => "CCITT 1D",
            3 => "Group 3 Fax",
            4 => "Group 4 Fax",
            5 => "LZW",
            6 => "JPEG (old)",
            7 => "JPEG",
            8 => "Deflate",
            32773 => "PackBits",
            _ => "Unknown"
        };
    }

    private static string GetResolutionUnitName(int unit)
    {
        return unit switch
        {
            1 => "None",
            2 => "Inch",
            3 => "Centimeter",
            _ => "Unknown"
        };
    }

    #endregion
}