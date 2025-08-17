using BitMiracle.LibTiff.Classic;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.Projections;
using System;
using System.IO;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using System.Windows.Media;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Color = System.Drawing.Color;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/28/2025 5:47:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 수동 지리참조 기준점
/// </summary>
public class ManualGeoPoint
{
    public double PixelX { get; set; }        // 이미지의 픽셀 X 좌표
    public double PixelY { get; set; }        // 이미지의 픽셀 Y 좌표
    public double Latitude { get; set; }      // 실제 위도
    public double Longitude { get; set; }     // 실제 경도
    public string? Description { get; set; }   // 설명 (선택사항)
}

/// <summary>
/// 확장된 지리참조 방식 지원
/// </summary>
public enum ExtendedGeoReference
{
    WebMercator = 10,    // Web Mercator (기존 방식)
    UTMZone52N = 11,     // UTM Zone 52N (한국 전용)
    UTMControlPoints = 12 // UTM 기준점 방식
}


/// <summary>
/// 수동 지리참조 TIF 타일 변환 서비스 (Geo 정보 없는 파일 지원)
/// </summary>
public class TileGenerationService
{
    private readonly ILogService _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly GMapSetupModel _setup;
    private const string DIRECTORY = "c:/Tiles";

    public TileGenerationService(ILogService log
                                , IEventAggregator ea
                                , GMapSetupModel setup)
    {
        _log = log;
        _eventAggregator = ea;
        _setup = setup;

        if (!Directory.Exists(_setup.TileDirectory))
            Directory.CreateDirectory(_setup.TileDirectory ?? DIRECTORY);
    }

    /// <summary>
    /// TIF 파일 정보 분석 (Geo 정보 포함 여부 확인)
    /// </summary>
    public async Task<TifFileInfo> AnalyzeTifFileAsync(string tifFilePath)
    {
        return await Task.Run(() =>
        {
            var info = new TifFileInfo { FilePath = tifFilePath };

            try
            {
                var fileInfo = new FileInfo(tifFilePath);
                info.FileSize = fileInfo.Length;
                info.FileExists = fileInfo.Exists;

                if (!info.FileExists)
                {
                    info.ErrorMessage = "파일이 존재하지 않습니다.";
                    return info;
                }

                using var tif = Tiff.Open(tifFilePath, "r");
                if (tif == null)
                {
                    info.ErrorMessage = "TIF 파일을 열 수 없습니다.";
                    return info;
                }

                // 기본 정보 읽기
                info.Width = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                info.Height = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                info.BitsPerSample = tif.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 8;
                info.SamplesPerPixel = tif.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;
                info.PhotometricInterpretation = tif.GetField(TiffTag.PHOTOMETRIC)?[0].ToInt() ?? 1;
                info.Compression = tif.GetField(TiffTag.COMPRESSION)?[0].ToInt() ?? 1;
                info.ScanlineSize = tif.ScanlineSize();

                // 해상도 정보
                var xRes = tif.GetField(TiffTag.XRESOLUTION);
                var yRes = tif.GetField(TiffTag.YRESOLUTION);
                if (xRes != null && yRes != null)
                {
                    info.XResolution = xRes[0].ToDouble();
                    info.YResolution = yRes[0].ToDouble();
                    info.ResolutionUnit = tif.GetField(TiffTag.RESOLUTIONUNIT)?[0].ToInt() ?? 2;
                }

                // GeoTIFF 정보 확인
                var geoKeys = tif.GetField(TiffTag.GEOTIFF_GEOKEYDIRECTORYTAG);
                info.IsGeoTiff = geoKeys != null;

                // 메모리 계산
                info.TotalPixels = (long)info.Width * info.Height;
                info.EstimatedRawSize = info.TotalPixels * info.BitsPerSample * info.SamplesPerPixel / 8;
                info.EstimatedBitmapSize = info.TotalPixels * 3; // RGB 24bit

                // 호환성 검사
                CheckCompatibility(info);

                info.IsValid = true;
                _log?.Info($"TIF 분석 완료: {info.Width}x{info.Height}, GeoTIFF: {info.IsGeoTiff}");
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"분석 실패: {ex.Message}";
                _log?.Error(info.ErrorMessage);
            }

            return info;
        });
    }

    /// <summary>
    /// 수동 지리 좌표로 GeoTransformInfo 생성
    /// </summary>
    public GeoTransformInfo CreateManualGeoTransform(
        double minLatitude, double minLongitude,
        double maxLatitude, double maxLongitude)
    {
        _log?.Info($"수동 지리 좌표 설정: ({minLatitude:F8}, {minLongitude:F8}) to ({maxLatitude:F8}, {maxLongitude:F8})");

        var geoTransform = new GeoTransformInfo
        {
            MinLatitude = minLatitude,
            MaxLatitude = maxLatitude,
            MinLongitude = minLongitude,
            MaxLongitude = maxLongitude,
            CoordinateSystem = "WGS84",
            EpsgCode = "4326",
            HasGeoReference = true
        };

        return geoTransform;
    }

    /// <summary>
    /// 수동 기준점으로 지리참조 정보 생성
    /// </summary>
    public GeoTransformInfo CreateGeoTransformFromControlPoints(List<ManualGeoPoint> controlPoints, int imageWidth, int imageHeight)
    {
        if (controlPoints == null || controlPoints.Count < 2)
            throw new ArgumentException("최소 2개의 기준점이 필요합니다.");

        _log?.Info($"기준점 {controlPoints.Count}개로 지리참조 계산 시작");

        var geoTransform = new GeoTransformInfo
        {
            CoordinateSystem = "WGS84",
            EpsgCode = "4326",
            HasGeoReference = true
        };

        if (controlPoints.Count == 2)
        {
            // 2점 기준: 단순 선형 변환
            CalculateLinearTransform(controlPoints, imageWidth, imageHeight, geoTransform);
        }
        else
        {
            // 3점 이상: 최소자승법으로 최적 변환
            CalculateLeastSquaresTransform(controlPoints, imageWidth, imageHeight, geoTransform);
        }

        _log?.Info($"지리참조 계산 완료: ({geoTransform.MinLatitude:F6}, {geoTransform.MinLongitude:F6}) to ({geoTransform.MaxLatitude:F6}, {geoTransform.MaxLongitude:F6})");
        return geoTransform;
    }

    #region == Web Mercator 방식 (기존) ==

    // <summary>
    /// 수동 좌표로 TIF 파일을 타일 변환
    /// </summary>
    public async Task<CustomMapModel> ConvertTifWithManualCoordsAsync(
        string tifFilePath,
        string mapName,
        double minLatitude, double minLongitude,
        double maxLatitude, double maxLongitude,
        int minZoom = 10, int maxZoom = 16, int tileSize = 256,
        IProgress<TileConversionProgress> progress = null)
    {
        var startTime = DateTime.Now;

        try
        {
            _log?.Info($"수동 좌표 TIF 변환 시작: {mapName}");

            // 1. TIF 파일 분석
            var tifInfo = await AnalyzeTifFileAsync(tifFilePath);
            if (!tifInfo.IsValid)
                throw new InvalidOperationException($"TIF 파일 분석 실패: {tifInfo.ErrorMessage}");

            // 2. 수동 지리 좌표 설정
            var geoTransform = CreateManualGeoTransform(minLatitude, minLongitude, maxLatitude, maxLongitude);

            // 픽셀 해상도 계산
            geoTransform.PixelSizeX = (maxLongitude - minLongitude) / tifInfo.Width;
            geoTransform.PixelSizeY = (maxLatitude - minLatitude) / tifInfo.Height;

            // 3. CustomMap 모델 생성
            var customMap = CreateCustomMapModel(mapName, tifFilePath, tifInfo, geoTransform,
                minZoom, maxZoom, tileSize, EnumGeoReference.ManualControlPoints);


            // 4. 타일 디렉토리 생성
            var tileDirectoryName = $"{Path.GetFileNameWithoutExtension(tifFilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}";
            var tileDirectory = Path.Combine(_setup.TileDirectory ?? DIRECTORY, tileDirectoryName);
            Directory.CreateDirectory(tileDirectory);
            customMap.TilesDirectoryPath = tileDirectory;
                       
            // 5. 타일 생성
            var totalTiles = await GenerateTilesFromTifAsync(
                tifFilePath, tifInfo, geoTransform, customMap.TilesDirectoryPath,
                minZoom, maxZoom, tileSize, progress);

            // 6. 최종 정보 업데이트
            customMap.TotalTileCount = totalTiles;
            customMap.TilesDirectorySize = GetDirectorySize(tileDirectory);
            customMap.ProcessedAt = DateTime.Now;
            customMap.ProcessingTimeMinutes = (int)(DateTime.Now - startTime).TotalMinutes;
            customMap.Status = EnumMapStatus.Active;
            customMap.QualityScore = 0.7; // 수동 입력의 경우 보통 품질

            _log?.Info($"수동 좌표 TIF 변환 완료: {totalTiles}개 타일, {customMap.ProcessingTimeMinutes}분 소요");

            UpdateCustomMapAfterTileGeneration(customMap, totalTiles, startTime);

            return customMap;
        }
        catch (Exception ex)
        {
            _log?.Error($"수동 좌표 TIF 변환 실패: {ex.Message}");
            //await _eventAggregator.PublishOnUIThreadAsync(new TileGenerationFailedEvent(ex.Message));
            throw;
        }
    }


    /// <summary>
    /// Web Mercator 투영법으로 타일 생성
    /// </summary>
    public async Task<int> GenerateTilesFromTifAsync(
        string tifFilePath, TifFileInfo tifInfo, GeoTransformInfo geoTransform, string tileDirectory,
        int minZoom, int maxZoom, int tileSize, IProgress<TileConversionProgress> progress)
    {
        var totalTiles = 0;
        var projection = MercatorProjection.Instance;
        var geoBounds = new RectLatLng(
            geoTransform.MaxLatitude,
            geoTransform.MinLongitude,
            geoTransform.MaxLongitude - geoTransform.MinLongitude,
            geoTransform.MaxLatitude - geoTransform.MinLatitude);

        // 총 타일 수 계산
        var estimatedTiles = 0;
        for (int z = minZoom; z <= maxZoom; z++)
        {
            var tiles = projection.GetAreaTileList(geoBounds, z, 0);
            estimatedTiles += tiles.Count;
        }

        _log?.Info($"예상 타일 수: {estimatedTiles}");

        for (int zoom = minZoom; zoom <= maxZoom; zoom++)
        {
            var zoomDirectory = Path.Combine(tileDirectory, zoom.ToString());
            Directory.CreateDirectory(zoomDirectory);

            var tileList = projection.GetAreaTileList(geoBounds, zoom, 0);
            _log?.Info($"줌 레벨 {zoom}: {tileList.Count}개 타일 처리");

            var processedInZoom = 0;
            foreach (var tilePoint in tileList)
            {
                try
                {
                    var tileImage = await CreateTileFromTifAsync(
                        tifFilePath, tifInfo, geoTransform, tilePoint, zoom, tileSize, projection);

                    if (tileImage != null)
                    {
                        var xDir = Path.Combine(zoomDirectory, tilePoint.X.ToString());
                        Directory.CreateDirectory(xDir);
                        var tilePath = Path.Combine(xDir, $"{tilePoint.Y}.png");

                        tileImage.Save(tilePath, ImageFormat.Png);
                        tileImage.Dispose();
                        totalTiles++;
                        processedInZoom++;

                        // 진행률 중간 보고 (각 줌 레벨에서 100개마다)
                        if (processedInZoom % 100 == 0)
                        {
                            var progressPercent = (double)totalTiles / estimatedTiles * 100;
                            progress?.Report(new TileConversionProgress
                            {
                                ProcessedTiles = totalTiles,
                                TotalTiles = estimatedTiles,
                                CurrentZoomLevel = zoom,
                                ProgressPercentage = progressPercent
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log?.Error($"타일 {tilePoint.X}/{tilePoint.Y} 실패: {ex.Message}");
                }
            }

            // 줌 레벨 완료 후 진행률 보고
            var zoomProgressPercent = (double)totalTiles / estimatedTiles * 100;
            progress?.Report(new TileConversionProgress
            {
                ProcessedTiles = totalTiles,
                TotalTiles = estimatedTiles,
                CurrentZoomLevel = zoom,
                ProgressPercentage = zoomProgressPercent
            });

            _log?.Info($"줌 레벨 {zoom} 완료: {processedInZoom}개 타일 생성");
            GC.Collect(); // 메모리 정리
        }

        return totalTiles;
    }
    #endregion

    /// <summary>
    /// 이미지 미리보기 생성
    /// </summary>
    public async Task<Bitmap> CreatePreviewImageAsync(string tifFilePath, int maxWidth = 800, int maxHeight = 600)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var tif = Tiff.Open(tifFilePath, "r");
                if (tif == null)
                    throw new InvalidOperationException("TIF 파일을 열 수 없습니다.");

                var width = tif.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                var height = tif.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
                var samplesPerPixel = tif.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;

                // 미리보기 크기 계산
                var scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
                var previewWidth = (int)(width * scale);
                var previewHeight = (int)(height * scale);

                _log?.Info($"미리보기 생성: {width}x{height} -> {previewWidth}x{previewHeight}");

                var preview = new Bitmap(previewWidth, previewHeight, PixelFormat.Format24bppRgb);

                using (var g = Graphics.FromImage(preview))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.Clear(Color.White);

                    // 원본의 일부 스캔라인만 읽어서 미리보기 생성
                    var scanlineBuffer = new byte[tif.ScanlineSize()];
                    var step = Math.Max(1, height / previewHeight);

                    for (int previewRow = 0; previewRow < previewHeight; previewRow++)
                    {
                        var originalRow = Math.Min(previewRow * step, height - 1);

                        if (tif.ReadScanline(scanlineBuffer, originalRow))
                        {
                            for (int previewCol = 0; previewCol < previewWidth; previewCol++)
                            {
                                var originalCol = Math.Min((int)(previewCol / scale), width - 1);
                                var pixelValue = scanlineBuffer[originalCol * samplesPerPixel];

                                var color = Color.FromArgb(pixelValue, pixelValue, pixelValue);
                                preview.SetPixel(previewCol, previewRow, color);
                            }
                        }
                    }
                }
                _log?.Info("미리보기 생성 완료");
                return preview;
            }
            catch (Exception ex)
            {
                _log?.Error($"미리보기 생성 실패: {ex.Message}");
                throw;
            }
        });
    }

    #region Private Helper Methods

    private CustomMapModel CreateCustomMapModel(
        string mapName, string tifFilePath, TifFileInfo tifInfo, GeoTransformInfo geoTransform,
        int minZoom, int maxZoom, int tileSize, EnumGeoReference geoReferenceMethod)
    {
        var tileDirectoryName = $"{Path.GetFileNameWithoutExtension(tifFilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}";
        var tileDirectory = Path.Combine(_setup.TileDirectory ?? DIRECTORY, tileDirectoryName);
        Directory.CreateDirectory(tileDirectory);

        return new CustomMapModel
        {
            Name = mapName,
            SourceImagePath = tifFilePath,
            TilesDirectoryPath = tileDirectory,
            OriginalFileSize = tifInfo.FileSize,
            OriginalWidth = tifInfo.Width,
            OriginalHeight = tifInfo.Height,
            TileSize = tileSize,
            MinZoomLevel = minZoom,
            MaxZoomLevel = maxZoom,
            MinLatitude = geoTransform.MinLatitude,
            MaxLatitude = geoTransform.MaxLatitude,
            MinLongitude = geoTransform.MinLongitude,
            MaxLongitude = geoTransform.MaxLongitude,
            PixelResolutionX = geoTransform.PixelSizeX,
            PixelResolutionY = geoTransform.PixelSizeY,
            Status = EnumMapStatus.Processing,
            Category = EnumMapCategory.Satellite,
            CreatedAt = DateTime.Now,
            CoordinateSystem = geoTransform.CoordinateSystem,
            EpsgCode = geoTransform.EpsgCode,
            GeoReferenceMethod = geoReferenceMethod
        };
    }

    private void UpdateCustomMapAfterTileGeneration(CustomMapModel customMap, int totalTiles, DateTime startTime)
    {
        customMap.TotalTileCount = totalTiles;
        customMap.TilesDirectorySize = GetDirectorySize(customMap.TilesDirectoryPath);
        customMap.ProcessedAt = DateTime.Now;
        customMap.ProcessingTimeMinutes = (int)(DateTime.Now - startTime).TotalMinutes;
        customMap.Status = EnumMapStatus.Active;
        customMap.QualityScore = 0.7; // 기본 품질 점수
    }

    private void CalculateLinearTransform(List<ManualGeoPoint> points, int imageWidth, int imageHeight, GeoTransformInfo geoTransform)
    {
        var p1 = points[0];
        var p2 = points[1];

        var pixelDeltaX = p2.PixelX - p1.PixelX;
        var pixelDeltaY = p2.PixelY - p1.PixelY;
        var geoDeltaX = p2.Longitude - p1.Longitude;
        var geoDeltaY = p2.Latitude - p1.Latitude;

        if (Math.Abs(pixelDeltaX) < 1 || Math.Abs(pixelDeltaY) < 1)
            throw new ArgumentException("기준점들이 너무 가깝습니다.");

        geoTransform.PixelSizeX = Math.Abs(geoDeltaX / pixelDeltaX);
        geoTransform.PixelSizeY = Math.Abs(geoDeltaY / pixelDeltaY);

        var topLeftLng = p1.Longitude - (p1.PixelX * geoTransform.PixelSizeX * Math.Sign(geoDeltaX));
        var topLeftLat = p1.Latitude + (p1.PixelY * geoTransform.PixelSizeY * Math.Sign(geoDeltaY));

        geoTransform.MinLongitude = topLeftLng;
        geoTransform.MaxLongitude = topLeftLng + (imageWidth * geoTransform.PixelSizeX * Math.Sign(geoDeltaX));
        geoTransform.MaxLatitude = topLeftLat;
        geoTransform.MinLatitude = topLeftLat - (imageHeight * geoTransform.PixelSizeY * Math.Sign(geoDeltaY));

        if (geoTransform.MinLongitude > geoTransform.MaxLongitude)
        {
            (geoTransform.MinLongitude, geoTransform.MaxLongitude) = (geoTransform.MaxLongitude, geoTransform.MinLongitude);
        }
        if (geoTransform.MinLatitude > geoTransform.MaxLatitude)
        {
            (geoTransform.MinLatitude, geoTransform.MaxLatitude) = (geoTransform.MaxLatitude, geoTransform.MinLatitude);
        }
    }

    private void CalculateLeastSquaresTransform(List<ManualGeoPoint> points, int imageWidth, int imageHeight, GeoTransformInfo geoTransform)
    {
        var avgPixelSizeX = 0.0;
        var avgPixelSizeY = 0.0;
        var validPairs = 0;

        for (int i = 0; i < points.Count - 1; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                var p1 = points[i];
                var p2 = points[j];

                var pixelDeltaX = p2.PixelX - p1.PixelX;
                var pixelDeltaY = p2.PixelY - p1.PixelY;
                var geoDeltaX = p2.Longitude - p1.Longitude;
                var geoDeltaY = p2.Latitude - p1.Latitude;

                if (Math.Abs(pixelDeltaX) > 1 && Math.Abs(pixelDeltaY) > 1)
                {
                    avgPixelSizeX += Math.Abs(geoDeltaX / pixelDeltaX);
                    avgPixelSizeY += Math.Abs(geoDeltaY / pixelDeltaY);
                    validPairs++;
                }
            }
        }

        if (validPairs == 0)
            throw new ArgumentException("유효한 기준점 쌍이 없습니다.");

        geoTransform.PixelSizeX = avgPixelSizeX / validPairs;
        geoTransform.PixelSizeY = avgPixelSizeY / validPairs;

        var minLat = points.Min(p => p.Latitude);
        var maxLat = points.Max(p => p.Latitude);
        var minLng = points.Min(p => p.Longitude);
        var maxLng = points.Max(p => p.Longitude);

        var latRange = maxLat - minLat;
        var lngRange = maxLng - minLng;
        var pixelRangeX = points.Max(p => p.PixelX) - points.Min(p => p.PixelX);
        var pixelRangeY = points.Max(p => p.PixelY) - points.Min(p => p.PixelY);

        if (pixelRangeX > 0 && pixelRangeY > 0)
        {
            var scaleX = lngRange / pixelRangeX;
            var scaleY = latRange / pixelRangeY;

            geoTransform.MinLongitude = minLng - (points.Min(p => p.PixelX) * scaleX);
            geoTransform.MaxLongitude = geoTransform.MinLongitude + (imageWidth * scaleX);
            geoTransform.MaxLatitude = maxLat + (points.Min(p => p.PixelY) * scaleY);
            geoTransform.MinLatitude = geoTransform.MaxLatitude - (imageHeight * scaleY);
        }
        else
        {
            var centerLat = (minLat + maxLat) / 2;
            var centerLng = (minLng + maxLng) / 2;
            var defaultRange = 0.01;

            geoTransform.MinLatitude = centerLat - defaultRange;
            geoTransform.MaxLatitude = centerLat + defaultRange;
            geoTransform.MinLongitude = centerLng - defaultRange;
            geoTransform.MaxLongitude = centerLng + defaultRange;
        }
    }

    private async Task<Bitmap> CreateTileFromTifAsync(
     string tifFilePath, TifFileInfo tifInfo, GeoTransformInfo geoTransform,
     GPoint tilePoint, int zoom, int tileSize, PureProjection projection)
    {
        return await Task.Run(() =>
        {
            try
            {
                var tilePixelTopLeft = projection.FromTileXYToPixel(tilePoint);
                var tilePixelBottomRight = projection.FromTileXYToPixel(new GPoint(tilePoint.X + 1, tilePoint.Y + 1));
                var tileGeoTopLeft = projection.FromPixelToLatLng(tilePixelTopLeft, zoom);
                var tileGeoBottomRight = projection.FromPixelToLatLng(tilePixelBottomRight, zoom);

                if (!HasIntersection(tileGeoTopLeft, tileGeoBottomRight, geoTransform))
                    return null;

                // 개선된 교차 영역 계산
                var intersectLeft = Math.Max(tileGeoTopLeft.Lng, geoTransform.MinLongitude);
                var intersectRight = Math.Min(tileGeoBottomRight.Lng, geoTransform.MaxLongitude);
                var intersectTop = Math.Min(tileGeoTopLeft.Lat, geoTransform.MaxLatitude);
                var intersectBottom = Math.Max(tileGeoBottomRight.Lat, geoTransform.MinLatitude);

                // 픽셀 정밀도 개선 (소수점 유지)
                var geoToPixelScaleX = tifInfo.Width / (geoTransform.MaxLongitude - geoTransform.MinLongitude);
                var geoToPixelScaleY = tifInfo.Height / (geoTransform.MaxLatitude - geoTransform.MinLatitude);

                var imgLeftExact = (intersectLeft - geoTransform.MinLongitude) * geoToPixelScaleX;
                var imgRightExact = (intersectRight - geoTransform.MinLongitude) * geoToPixelScaleX;
                var imgTopExact = (geoTransform.MaxLatitude - intersectTop) * geoToPixelScaleY;
                var imgBottomExact = (geoTransform.MaxLatitude - intersectBottom) * geoToPixelScaleY;

                // 경계 확장으로 격자 방지 (1픽셀 여유)
                var imgLeft = Math.Max(0, (int)Math.Floor(imgLeftExact) - 1);
                var imgRight = Math.Min(tifInfo.Width, (int)Math.Ceiling(imgRightExact) + 1);
                var imgTop = Math.Max(0, (int)Math.Floor(imgTopExact) - 1);
                var imgBottom = Math.Min(tifInfo.Height, (int)Math.Ceiling(imgBottomExact) + 1);

                var imgWidth = imgRight - imgLeft;
                var imgHeight = imgBottom - imgTop;

                if (imgWidth <= 0 || imgHeight <= 0)
                    return null;

                return ExtractTileFromTifImproved(tifFilePath, tifInfo,
                    imgLeft, imgTop, imgWidth, imgHeight,
                    tileGeoTopLeft, tileGeoBottomRight,
                    intersectLeft, intersectTop, intersectRight, intersectBottom,
                    tileSize, geoTransform);
            }
            catch
            {
                return null;
            }
        });
    }

    private Bitmap ExtractTileFromTifImproved(string tifFilePath, TifFileInfo tifInfo,
    int imgLeft, int imgTop, int imgWidth, int imgHeight,
    PointLatLng tileGeoTopLeft, PointLatLng tileGeoBottomRight,
    double intersectLeft, double intersectTop, double intersectRight, double intersectBottom,
    int tileSize, GeoTransformInfo geoTransform)
    {
        using var tif = Tiff.Open(tifFilePath, "r");
        if (tif == null) return null;

        var tileImage = new Bitmap(tileSize, tileSize, PixelFormat.Format32bppArgb);

        try
        {
            using var g = Graphics.FromImage(tileImage);

            // 투명 배경으로 시작
            g.Clear(Color.Transparent);

            // 격자 방지를 위한 보간법 변경
            g.InterpolationMode = InterpolationMode.NearestNeighbor; // 또는 Bilinear
            g.PixelOffsetMode = PixelOffsetMode.Half; // 픽셀 정렬 개선
            g.SmoothingMode = SmoothingMode.None; // 경계 날카롭게

            // 정확한 매핑 계산
            var tileGeoWidth = tileGeoBottomRight.Lng - tileGeoTopLeft.Lng;
            var tileGeoHeight = tileGeoTopLeft.Lat - tileGeoBottomRight.Lat;

            // 실제 교차 영역의 타일 내 위치 (소수점 정밀도 유지)
            var destLeftExact = (intersectLeft - tileGeoTopLeft.Lng) / tileGeoWidth * tileSize;
            var destTopExact = (tileGeoTopLeft.Lat - intersectTop) / tileGeoHeight * tileSize;
            var destWidthExact = (intersectRight - intersectLeft) / tileGeoWidth * tileSize;
            var destHeightExact = (intersectTop - intersectBottom) / tileGeoHeight * tileSize;

            // 픽셀 완전 정렬
            var destLeft = (int)Math.Round(destLeftExact);
            var destTop = (int)Math.Round(destTopExact);
            var destWidth = (int)Math.Round(destWidthExact);
            var destHeight = (int)Math.Round(destHeightExact);

            // 최소 크기 보장
            destWidth = Math.Max(1, destWidth);
            destHeight = Math.Max(1, destHeight);

            // 원본 이미지에서 정확한 영역 추출
            var regionBitmap = ExtractRegionFromTif(tif, tifInfo, imgLeft, imgTop, imgWidth, imgHeight);

            if (regionBitmap != null)
            {
                var destRect = new Rectangle(destLeft, destTop, destWidth, destHeight);

                // 정확한 좌표로 이미지 그리기
                g.DrawImage(regionBitmap, destRect,
                    new Rectangle(0, 0, regionBitmap.Width, regionBitmap.Height),
                    GraphicsUnit.Pixel);

                regionBitmap.Dispose();
            }

            return tileImage;
        }
        catch (Exception ex)
        {
            _log?.Error($"타일 추출 실패: {ex.Message}");
            tileImage?.Dispose();
            return null;
        }
    }

    private Bitmap ExtractRegionFromTif(Tiff tif, TifFileInfo tifInfo,
    int imgLeft, int imgTop, int imgWidth, int imgHeight)
    {
        var regionBitmap = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);

        try
        {
            var regionData = regionBitmap.LockBits(
                new Rectangle(0, 0, imgWidth, imgHeight),
                ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            var scanlineBuffer = new byte[tifInfo.ScanlineSize];

            try
            {
                for (int row = 0; row < imgHeight; row++)
                {
                    var sourceRow = imgTop + row;
                    if (sourceRow >= 0 && sourceRow < tifInfo.Height &&
                        tif.ReadScanline(scanlineBuffer, sourceRow))
                    {
                        var destPtr = regionData.Scan0 + (row * regionData.Stride);

                        for (int col = 0; col < imgWidth; col++)
                        {
                            var srcCol = imgLeft + col;
                            if (srcCol >= 0 && srcCol < tifInfo.Width)
                            {
                                var srcOffset = srcCol * tifInfo.SamplesPerPixel;
                                var destOffset = col * 3;

                                if (srcOffset < scanlineBuffer.Length)
                                {
                                    // 🔧 색상 처리 개선
                                    ExtractPixelColor(scanlineBuffer, srcOffset, destPtr + destOffset, tifInfo);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                regionBitmap.UnlockBits(regionData);
            }

            return regionBitmap;
        }
        catch
        {
            regionBitmap?.Dispose();
            return null;
        }
    }

    private void ExtractPixelColor(byte[] scanlineBuffer, int srcOffset, IntPtr destPtr, TifFileInfo tifInfo)
    {
        switch (tifInfo.PhotometricInterpretation)
        {
            case 0: // WhiteIsZero
                var whitePixel = (byte)(255 - scanlineBuffer[srcOffset]);
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr, whitePixel);
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + 1, whitePixel);
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + 2, whitePixel);
                break;

            case 2: // RGB
                if (tifInfo.SamplesPerPixel >= 3 && srcOffset + 2 < scanlineBuffer.Length)
                {
                    System.Runtime.InteropServices.Marshal.WriteByte(destPtr, scanlineBuffer[srcOffset + 2]); // B
                    System.Runtime.InteropServices.Marshal.WriteByte(destPtr + 1, scanlineBuffer[srcOffset + 1]); // G
                    System.Runtime.InteropServices.Marshal.WriteByte(destPtr + 2, scanlineBuffer[srcOffset]); // R
                }
                else
                {
                    goto default;
                }
                break;

            case 1: // BlackIsZero
            default:
                var blackPixel = scanlineBuffer[srcOffset];
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr, blackPixel);
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + 1, blackPixel);
                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + 2, blackPixel);
                break;
        }
    }

    private Bitmap ExtractTileFromTif(string tifFilePath, TifFileInfo tifInfo,
        int imgLeft, int imgTop, int imgWidth, int imgHeight,
        PointLatLng tileGeoTopLeft, PointLatLng tileGeoBottomRight,
        double intersectLeft, double intersectTop, double intersectRight, double intersectBottom,
        int tileSize)
    {
        using var tif = Tiff.Open(tifFilePath, "r");
        if (tif == null) return null;

        var tileImage = new Bitmap(tileSize, tileSize, PixelFormat.Format32bppArgb);

        try
        {
            using var g = Graphics.FromImage(tileImage);
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var tileGeoWidth = tileGeoBottomRight.Lng - tileGeoTopLeft.Lng;
            var tileGeoHeight = tileGeoTopLeft.Lat - tileGeoBottomRight.Lat;
            var destLeft = (int)((intersectLeft - tileGeoTopLeft.Lng) / tileGeoWidth * tileSize);
            var destTop = (int)((tileGeoTopLeft.Lat - intersectTop) / tileGeoHeight * tileSize);
            var destWidth = (int)((intersectRight - intersectLeft) / tileGeoWidth * tileSize);
            var destHeight = (int)((intersectTop - intersectBottom) / tileGeoHeight * tileSize);

            destWidth = Math.Max(1, destWidth);
            destHeight = Math.Max(1, destHeight);

            var scanlineBuffer = new byte[tifInfo.ScanlineSize];
            var regionBitmap = new Bitmap(imgWidth, imgHeight, PixelFormat.Format24bppRgb);

            try
            {
                var regionData = regionBitmap.LockBits(
                    new Rectangle(0, 0, imgWidth, imgHeight),
                    ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

                try
                {
                    for (int row = imgTop; row < imgTop + imgHeight; row++)
                    {
                        if (tif.ReadScanline(scanlineBuffer, row))
                        {
                            var destRow = row - imgTop;
                            var destPtr = regionData.Scan0 + (destRow * regionData.Stride);

                            for (int col = 0; col < imgWidth; col++)
                            {
                                var srcCol = imgLeft + col;
                                var srcOffset = srcCol * tifInfo.SamplesPerPixel;
                                var destOffset = col * 3;

                                if (srcOffset < scanlineBuffer.Length)
                                {
                                    byte pixelValue;

                                    switch (tifInfo.PhotometricInterpretation)
                                    {
                                        case 0: // WhiteIsZero
                                            pixelValue = (byte)(255 - scanlineBuffer[srcOffset]);
                                            break;
                                        case 2: // RGB
                                            if (tifInfo.SamplesPerPixel >= 3 && srcOffset + 2 < scanlineBuffer.Length)
                                            {
                                                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset, scanlineBuffer[srcOffset + 2]);
                                                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 1, scanlineBuffer[srcOffset + 1]);
                                                System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 2, scanlineBuffer[srcOffset]);
                                                continue;
                                            }
                                            else
                                            {
                                                pixelValue = scanlineBuffer[srcOffset];
                                            }
                                            break;
                                        case 1: // BlackIsZero
                                        default:
                                            pixelValue = scanlineBuffer[srcOffset];
                                            break;
                                    }

                                    System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset, pixelValue);
                                    System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 1, pixelValue);
                                    System.Runtime.InteropServices.Marshal.WriteByte(destPtr + destOffset + 2, pixelValue);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    regionBitmap.UnlockBits(regionData);
                }

                var destRect = new Rectangle(destLeft, destTop, destWidth, destHeight);
                g.DrawImage(regionBitmap, destRect);
            }
            finally
            {
                regionBitmap.Dispose();
            }

            return tileImage;
        }
        catch
        {
            tileImage?.Dispose();
            return null;
        }
    }

    private bool HasIntersection(PointLatLng tileTopLeft, PointLatLng tileBottomRight, GeoTransformInfo geoTransform)
    {
        return !(tileBottomRight.Lng <= geoTransform.MinLongitude ||
                 tileTopLeft.Lng >= geoTransform.MaxLongitude ||
                 tileTopLeft.Lat <= geoTransform.MinLatitude ||
                 tileBottomRight.Lat >= geoTransform.MaxLatitude);
    }

    private void CheckCompatibility(TifFileInfo info)
    {
        var issues = new List<string>();

        if (info.Width <= 0 || info.Height <= 0)
            issues.Add($"잘못된 이미지 크기: {info.Width}x{info.Height}");

        if (info.EstimatedBitmapSize > int.MaxValue)
            issues.Add($"이미지가 너무 큼: {info.EstimatedBitmapSize:N0} bytes");

        if (info.TotalPixels > 268435456) // 256M pixels
            issues.Add($"픽셀 수가 너무 많음: {info.TotalPixels:N0}");

        info.CompatibilityIssues = issues;
        info.IsBitmapCompatible = issues.Count == 0;
    }

    private long GetDirectorySize(string directoryPath)
    {
        try
        {
            var dir = new DirectoryInfo(directoryPath);
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }
    #endregion
}