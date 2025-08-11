using BitMiracle.LibTiff.Classic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using System.Collections.Concurrent;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using GMap.NET.WindowsPresentation;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
       Purpose      : 이미지 오버레이 서비스 (TifOverlayService 통합)                                                          
       Created By   : GHLee                                                
       Created On   : 7/30/2025 8:30:00 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
public class ImageOverlayService
{
    #region - Fields -
    private readonly ILogService _log;
    private readonly TileGenerationService _tileGenerationService;
    private readonly ConcurrentDictionary<string, GMapCustomImage> _activeOverlays;
    private readonly object _lockObject = new object();
    #endregion

    #region - Constructor -
    public ImageOverlayService(ILogService log, TileGenerationService tileGenerationService)
    {
        _log = log;
        _tileGenerationService = tileGenerationService;
        _activeOverlays = new ConcurrentDictionary<string, GMapCustomImage>();
    }
    #endregion

    #region - Properties -
    /// <summary>
    /// 활성화된 오버레이 목록
    /// </summary>
    public IReadOnlyDictionary<string, GMapCustomImage> ActiveOverlays => _activeOverlays;

    /// <summary>
    /// 활성화된 오버레이 개수
    /// </summary>
    public int ActiveOverlayCount => _activeOverlays.Count;
    #endregion

    #region - Public Methods -
    /// <summary>
    /// TIF 파일로부터 이미지 오버레이 생성 (ImageModelExtensions 통합 버전)
    /// </summary>
    /// <param name="tifFilePath">TIF 파일 경로</param>
    /// <param name="mapCenter">맵 중심 좌표 (클릭된 위치)</param>
    /// <param name="gMap">GMapControl 인스턴스</param>
    /// <param name="overlayName">오버레이 이름 (선택사항)</param>
    /// <returns>생성된 GMapCustomImage</returns>
    public async Task<GMapCustomImage> CreateTifOverlayAsync(
        string tifFilePath,
        PointLatLng mapCenter,
        GMapControl gMap,
        string overlayName = null)
    {
        try
        {
            _log?.Info($"TIF 오버레이 생성 시작: {tifFilePath}");

            if (!File.Exists(tifFilePath))
            {
                throw new FileNotFoundException($"TIF 파일을 찾을 수 없습니다: {tifFilePath}");
            }

            // 1. TIF 파일 분석
            var tifInfo = await _tileGenerationService.AnalyzeTifFileAsync(tifFilePath);
            if (!tifInfo.IsValid)
            {
                throw new InvalidOperationException($"TIF 파일 분석 실패: {tifInfo.ErrorMessage}");
            }

            _log?.Info($"TIF 파일 정보: {tifInfo.Width}x{tifInfo.Height}, {tifInfo.SamplesPerPixel} 채널");

            // 2. GeoTIFF 지리참조 정보 추출
            var geoTransform = ExtractGeographicInfo(tifFilePath);
            bool hasGeoReference = geoTransform?.HasGeoReference == true;

            _log?.Info($"지리참조 정보: {(hasGeoReference ? "있음" : "없음")} - {geoTransform?.ToString() ?? "N/A"}");

            // 3. TIF를 WPF ImageSource로 변환
            var imageSource = await ConvertTifToImageSourceAsync(tifFilePath, tifInfo);
            if (imageSource == null)
            {
                throw new InvalidOperationException("TIF 파일을 ImageSource로 변환할 수 없습니다.");
            }

            // 4. ImageModel 생성 및 기본 정보 설정
            var model = new ImageModel()
            {
                Title = overlayName ?? Path.GetFileNameWithoutExtension(tifFilePath),
                FilePath = tifFilePath,
                Width = tifInfo.Width,
                Height = tifInfo.Height,
                Visibility = true,
                CoordinateSystem = geoTransform?.CoordinateSystem ?? "WGS84",
                Opacity = 0.7,
                HasGeoReference = hasGeoReference,
                Rotation = 0.0
            };

            // 5. 정확한 지리적 경계 계산 (ImageModelExtensions 활용)
            CalculateModelBounds(model, geoTransform, mapCenter, gMap);

            // 6. Model의 경계를 ImageSource에 적용 (기존 방식과 호환성 유지)
            model.Deconstruct(model.ToRect());

            // 7. GMapCustomImage 생성
            var customImage = new GMapCustomImage(_log ?? throw new NullReferenceException(), model)
            {
                Img = imageSource
            };

            _log?.Info($"CustomImage 생성 후 경계 확인: {customImage.ImageBounds}");

            // 8. 활성 오버레이 목록에 추가
            var overlayId = GenerateOverlayId(tifFilePath);
            if (_activeOverlays.TryAdd(overlayId, customImage))
            {
                _log?.Info($"TIF 오버레이 생성 완료: {customImage.Title} (Id:{customImage.Id})");
                _log?.Info($"최종 경계: {model.BoundsInfo()}");

                // 9. 가시성 및 화면 크기 검증
                if (gMap != null && model is IImageModel iModel)
                {
                    var isVisible = iModel.IsVisibleInViewArea(gMap);
                    var screenSize = iModel.GetScreenSize(gMap);

                    _log?.Info($"가시성: {isVisible}, 화면크기: {screenSize.screenWidth:F1}x{screenSize.screenHeight:F1}");

                    if (!isVisible)
                    {
                        _log?.Warning("생성된 오버레이가 현재 뷰 영역 밖에 있습니다.");
                    }
                }
            }
            else
            {
                _log?.Warning($"중복된 오버레이 ID로 추가 실패: {overlayId}");
            }

            return customImage;
        }
        catch (Exception ex)
        {
            _log?.Error($"TIF 오버레이 생성 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 일반 이미지 파일로부터 오버레이 생성 (ImageModelExtensions 통합 버전)
    /// </summary>
    /// <param name="imageFilePath">이미지 파일 경로</param>
    /// <param name="mapCenter">맵 중심 좌표</param>
    /// <param name="gMap">GMapControl 인스턴스</param>
    /// <param name="overlayName">오버레이 이름 (선택사항)</param>
    /// <returns>생성된 GMapCustomImage</returns>
    public async Task<GMapCustomImage> CreateImageOverlayAsync(
        string imageFilePath,
        PointLatLng mapCenter,
        GMapControl gMap,
        string overlayName = null)
    {
        try
        {
            _log?.Info($"이미지 오버레이 생성 시작: {imageFilePath}");

            if (!File.Exists(imageFilePath))
            {
                throw new FileNotFoundException($"이미지 파일을 찾을 수 없습니다: {imageFilePath}");
            }

            // 1. 이미지 파일 로드 및 정보 추출
            var imageSource = await LoadImageSourceAsync(imageFilePath);
            if (imageSource == null)
            {
                throw new InvalidOperationException("이미지 파일을 로드할 수 없습니다.");
            }

            var imageWidth = (int)imageSource.Width;
            var imageHeight = (int)imageSource.Height;

            _log?.Info($"이미지 정보: {imageWidth}x{imageHeight}");

            // 2. ImageModel 생성
            var model = new ImageModel()
            {
                Title = overlayName ?? Path.GetFileNameWithoutExtension(imageFilePath),
                FilePath = imageFilePath,
                Width = imageWidth,
                Height = imageHeight,
                Visibility = true,
                CoordinateSystem = "WGS84",
                Opacity = 0.7,
                HasGeoReference = false,
                Rotation = 0.0
            };

            // 3. 경계 계산 (일반 이미지는 지리참조 없음)
            CalculateModelBounds(model, null, mapCenter, gMap);

            // 4. Model의 경계를 적용 (호환성 유지)
            model.Deconstruct(model.ToRect());

            // 5. GMapCustomImage 생성
            var customImage = new GMapCustomImage(_log ?? throw new NullReferenceException(), model)
            {
                Img = imageSource
            };

            // 6. 활성 오버레이 목록에 추가
            var overlayId = GenerateOverlayId(imageFilePath);
            if (_activeOverlays.TryAdd(overlayId, customImage))
            {
                _log?.Info($"이미지 오버레이 생성 완료: {customImage.Title}");
                _log?.Info($"최종 경계: {model.BoundsInfo()}");

                // 가시성 검증
                if (gMap != null && model is IImageModel iModel)
                {
                    var isVisible = iModel.IsVisibleInViewArea(gMap);
                    var screenSize = iModel.GetScreenSize(gMap);

                    _log?.Info($"가시성: {isVisible}, 화면크기: {screenSize.screenWidth:F1}x{screenSize.screenHeight:F1}");
                }
            }
            else
            {
                _log?.Warning($"중복된 오버레이 ID로 추가 실패: {overlayId}");
            }

            return customImage;
        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 오버레이 생성 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 오버레이 재배치 (편집 후 좌표 재계산)
    /// </summary>
    /// <param name="customImage">대상 이미지</param>
    /// <param name="newCenter">새 중심 좌표</param>
    /// <param name="gMap">GMapControl 인스턴스</param>
    /// <param name="newRotation">새 회전각</param>
    /// <returns>재배치 성공 여부</returns>
    public bool RepositionOverlay(
        GMapCustomImage customImage,
        PointLatLng newCenter,
        GMapControl gMap,
        double newRotation = 0.0)
    {
        if (customImage?.Model is not ImageModel model || gMap == null)
        {
            _log?.Warning("재배치할 수 없음: 유효하지 않은 파라미터");
            return false;
        }

        try
        {
            _log?.Info($"오버레이 재배치 시작: {customImage.Title}");

            // ImageModelExtensions 사용
            if (model is IImageModel iModel)
            {
                iModel.SyncWithGMapControl(gMap, newCenter);
                model.Rotation = newRotation;

                // CustomImage 동기화
                customImage.ImageBounds = iModel.ToRect();
                customImage.Rotation = newRotation;

                // 가시성 재검사
                var isVisible = iModel.IsVisibleInViewArea(gMap);
                var screenSize = iModel.GetScreenSize(gMap);

                _log?.Info($"재배치 완료: {iModel.BoundsInfo()}");
                _log?.Info($"가시성: {isVisible}, 화면크기: {screenSize.screenWidth:F1}x{screenSize.screenHeight:F1}");
            }
            else
            {
                // 기존 방식 폴백
                var overlayBounds = CalculateImageBounds(newCenter, gMap.Zoom, (int)model.Width, (int)model.Height);
                model.Left = overlayBounds.Left;
                model.Right = overlayBounds.Right;
                model.Top = overlayBounds.Top;
                model.Bottom = overlayBounds.Bottom;
                model.Latitude = newCenter.Lat;
                model.Longitude = newCenter.Lng;
                model.Rotation = newRotation;

                customImage.ImageBounds = overlayBounds;
                customImage.Rotation = newRotation;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"오버레이 재배치 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 오버레이 제거
    /// </summary>
    public bool RemoveOverlay(string overlayId)
    {
        try
        {
            if (_activeOverlays.TryRemove(overlayId, out var overlay))
            {
                overlay?.Dispose();
                _log?.Info($"오버레이 제거 완료: {overlayId}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"오버레이 제거 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 파일 경로로 오버레이 제거
    /// </summary>
    public bool RemoveOverlayByPath(string filePath)
    {
        var overlayId = GenerateOverlayId(filePath);
        return RemoveOverlay(overlayId);
    }

    /// <summary>
    /// 모든 오버레이 제거
    /// </summary>
    public void RemoveAllOverlays()
    {
        try
        {
            var overlaysToRemove = _activeOverlays.ToList();
            foreach (var kvp in overlaysToRemove)
            {
                kvp.Value?.Dispose();
                _activeOverlays.TryRemove(kvp.Key, out _);
            }

            _log?.Info($"모든 오버레이 제거 완료: {overlaysToRemove.Count}개");
        }
        catch (Exception ex)
        {
            _log?.Error($"모든 오버레이 제거 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 오버레이 ID로 조회
    /// </summary>
    public GMapCustomImage GetOverlay(string overlayId)
    {
        _activeOverlays.TryGetValue(overlayId, out var overlay);
        return overlay;
    }

    /// <summary>
    /// 파일 경로로 오버레이 조회
    /// </summary>
    public GMapCustomImage GetOverlayByPath(string filePath)
    {
        var overlayId = GenerateOverlayId(filePath);
        return GetOverlay(overlayId);
    }

    /// <summary>
    /// 특정 좌표에 있는 오버레이들 조회
    /// </summary>
    public List<GMapCustomImage> GetOverlaysAt(PointLatLng point)
    {
        return _activeOverlays.Values
            .Where(overlay => overlay.Contains(point))
            .ToList();
    }

    /// <summary>
    /// 특정 영역과 겹치는 오버레이들 조회
    /// </summary>
    public List<GMapCustomImage> GetOverlaysIntersecting(RectLatLng bounds)
    {
        return _activeOverlays.Values
            .Where(overlay => overlay.IntersectsWith(bounds))
            .ToList();
    }

    /// <summary>
    /// 현재 뷰에서 보이는 오버레이 목록 가져오기 (ImageModelExtensions 활용)
    /// </summary>
    /// <param name="gMap">GMapControl 인스턴스</param>
    /// <returns>보이는 오버레이 목록</returns>
    public List<GMapCustomImage> GetVisibleOverlays(GMapControl gMap)
    {
        if (gMap == null) return new List<GMapCustomImage>();

        try
        {
            var visibleOverlays = new List<GMapCustomImage>();

            foreach (var overlay in _activeOverlays.Values)
            {
                if (overlay.Model is IImageModel iModel)
                {
                    if (iModel.IsVisibleInViewArea(gMap))
                    {
                        visibleOverlays.Add(overlay);
                    }
                }
                else
                {
                    // 기존 방식 폴백 (IntersectsWith 사용)
                    if (overlay.IntersectsWith(gMap.ViewArea))
                    {
                        visibleOverlays.Add(overlay);
                    }
                }
            }

            _log?.Info($"현재 뷰의 가시 오버레이: {visibleOverlays.Count}/{_activeOverlays.Count}개");

            return visibleOverlays;
        }
        catch (Exception ex)
        {
            _log?.Error($"가시 오버레이 조회 실패: {ex.Message}");
            return new List<GMapCustomImage>();
        }
    }

    /// <summary>
    /// 오버레이 투명도 일괄 설정
    /// </summary>
    public void SetAllOverlaysOpacity(double opacity)
    {
        foreach (var overlay in _activeOverlays.Values)
        {
            overlay.Opacity = opacity;
        }
    }

    /// <summary>
    /// 오버레이 표시/숨김 일괄 설정
    /// </summary>
    public void SetAllOverlaysVisibility(bool isVisible)
    {
        foreach (var overlay in _activeOverlays.Values)
        {
            overlay.Visibility = isVisible;
        }
    }

    /// <summary>
    /// 지원되는 이미지 파일 형식인지 확인
    /// </summary>
    public bool IsSupportedImageFormat(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".tif" or ".tiff" => true,
            ".png" => true,
            ".jpg" or ".jpeg" => true,
            ".bmp" => true,
            ".gif" => true,
            _ => false
        };
    }

    /// <summary>
    /// 오버레이 통계 정보 (ImageModelExtensions 통합 버전)
    /// </summary>
    /// <param name="gMap">GMapControl 인스턴스 (선택사항)</param>
    /// <returns>통계 정보</returns>
    public OverlayStatistics GetStatistics(GMapControl gMap = null)
    {
        var overlays = _activeOverlays.Values.ToList();
        int visibleInViewCount = 0;
        double totalScreenArea = 0;

        if (gMap != null)
        {
            foreach (var overlay in overlays)
            {
                if (overlay.Model is IImageModel iModel)
                {
                    if (iModel.IsVisibleInViewArea(gMap))
                    {
                        visibleInViewCount++;

                        var screenSize = iModel.GetScreenSize(gMap);
                        totalScreenArea += screenSize.screenWidth * screenSize.screenHeight;
                    }
                }
            }
        }

        return new OverlayStatistics
        {
            TotalCount = overlays.Count,
            VisibleCount = overlays.Count(o => o.Visibility),
            VisibleInViewCount = visibleInViewCount,
            TifCount = overlays.Count(o => Path.GetExtension(o.FilePath).ToLowerInvariant() is ".tif" or ".tiff"),
            GeoReferencedCount = overlays.Count(o => o.HasGeoReference),
            AverageOpacity = overlays.Any() ? overlays.Average(o => o.Opacity) : 0.0,
            TotalMemoryUsage = overlays.Sum(o => EstimateMemoryUsage(o)),
            TotalScreenArea = totalScreenArea
        };
    }

    /// <summary>
    /// 중복된 오버레이 정리
    /// </summary>
    public void CleanupDuplicateOverlays()
    {
        var duplicates = new List<string>();
        var processedPaths = new HashSet<string>();

        foreach (var kvp in _activeOverlays)
        {
            if (processedPaths.Contains(kvp.Value.FilePath))
            {
                duplicates.Add(kvp.Key);
            }
            else
            {
                processedPaths.Add(kvp.Value.FilePath);
            }
        }

        foreach (var duplicateId in duplicates)
        {
            RemoveOverlay(duplicateId);
        }

        if (duplicates.Count > 0)
        {
            _log?.Info($"중복 오버레이 정리 완료: {duplicates.Count}개 제거");
        }
    }
    #endregion

    #region - Private Methods -
    /// <summary>
    /// TIF 파일에서 지리 정보 추출
    /// </summary>
    private GeoTransformInfo ExtractGeographicInfo(string tifFilePath)
    {
        try
        {
            using var tif = Tiff.Open(tifFilePath, "r");
            if (tif == null)
            {
                _log?.Warning($"TIF 파일을 열 수 없음: {tifFilePath}");
                return null;
            }

            var geoTransform = new GeoTransformInfo
            {
                HasGeoReference = false,
                CoordinateSystem = "WGS84",
                EpsgCode = "4326"
            };

            // 이미지 기본 정보
            var widthField = tif.GetField(TiffTag.IMAGEWIDTH);
            var heightField = tif.GetField(TiffTag.IMAGELENGTH);

            var width = widthField?[0].ToInt() ?? 0;
            var height = heightField?[0].ToInt() ?? 0;

            if (width <= 0 || height <= 0)
            {
                _log?.Warning($"잘못된 이미지 크기: {width}x{height}");
                return null;
            }

            // GeoTIFF 태그 확인
            var geoKeys = tif.GetField(TiffTag.GEOTIFF_GEOKEYDIRECTORYTAG);
            var tiePoints = tif.GetField(TiffTag.GEOTIFF_MODELTIEPOINTTAG);
            var pixelScale = tif.GetField(TiffTag.GEOTIFF_MODELPIXELSCALETAG);
            var transformation = tif.GetField(TiffTag.GEOTIFF_MODELTRANSFORMATIONTAG);

            // ModelTransformation 우선 처리 (가장 정확)
            if (transformation != null && transformation.Length >= 16)
            {
                // FieldValue 배열을 double 배열로 변환
                var matrix = new double[transformation.Length];
                for (int i = 0; i < transformation.Length; i++)
                {
                    matrix[i] = transformation[i].ToDouble();
                }

                // 4x4 변환 행렬에서 GeoTransform 추출
                geoTransform.GeoTransformArray = new double[]
                {
                matrix[3],  // originX
                matrix[0],  // pixelWidth  
                matrix[1],  // rotationX
                matrix[7],  // originY
                matrix[4],  // rotationY
                -matrix[5]  // pixelHeight (음수)
                };
                geoTransform.HasGeoReference = true;
                _log?.Info("ModelTransformation에서 지리참조 정보 추출");
            }
            // ModelTiepoint + ModelPixelScale 처리
            else if (tiePoints != null && pixelScale != null &&
                     tiePoints.Length >= 6 && pixelScale.Length >= 3)
            {
                // FieldValue를 double로 변환
                // TiePoint: [픽셀X, 픽셀Y, 픽셀Z, 지리X, 지리Y, 지리Z]
                double pixelX = tiePoints[0].ToDouble();
                double pixelY = tiePoints[1].ToDouble();
                double geoX = tiePoints[3].ToDouble();
                double geoY = tiePoints[4].ToDouble();

                // PixelScale: [X방향크기, Y방향크기, Z방향크기]
                double scaleX = pixelScale[0].ToDouble();
                double scaleY = pixelScale[1].ToDouble();

                // 원점 계산 (보통 좌상단)
                double originX = geoX - (pixelX * scaleX);
                double originY = geoY + (pixelY * scaleY); // Y축은 반대방향

                geoTransform.GeoTransformArray = new double[]
                {
                originX,  // originX
                scaleX,   // pixelWidth
                0.0,      // rotationX (없음)
                originY,  // originY
                0.0,      // rotationY (없음)
                -scaleY   // pixelHeight (음수)
                };

                geoTransform.HasGeoReference = true;
                _log?.Info($"TiePoint+PixelScale에서 지리참조 정보 추출: Origin({originX:F6}, {originY:F6}), Scale({scaleX:F8}, {scaleY:F8})");
            }

            // 지리참조 정보가 있는 경우 경계 계산
            if (geoTransform.HasGeoReference && geoTransform.GeoTransformArray != null)
            {
                var gt = geoTransform.GeoTransformArray;

                // 모서리 좌표 계산
                geoTransform.MinLongitude = gt[0];
                geoTransform.MaxLatitude = gt[3];
                geoTransform.MaxLongitude = gt[0] + (width * gt[1]) + (height * gt[2]);
                geoTransform.MinLatitude = gt[3] + (width * gt[4]) + (height * gt[5]);

                // 순서 보정 (Min/Max 확실히)
                if (geoTransform.MinLongitude > geoTransform.MaxLongitude)
                {
                    (geoTransform.MinLongitude, geoTransform.MaxLongitude) =
                        (geoTransform.MaxLongitude, geoTransform.MinLongitude);
                }

                if (geoTransform.MinLatitude > geoTransform.MaxLatitude)
                {
                    (geoTransform.MinLatitude, geoTransform.MaxLatitude) =
                        (geoTransform.MaxLatitude, geoTransform.MinLatitude);
                }

                geoTransform.PixelSizeX = Math.Abs(gt[1]);
                geoTransform.PixelSizeY = Math.Abs(gt[5]);

                _log?.Info($"지리적 경계: Lng({geoTransform.MinLongitude:F6} ~ {geoTransform.MaxLongitude:F6}), " +
                          $"Lat({geoTransform.MinLatitude:F6} ~ {geoTransform.MaxLatitude:F6})");
            }
            else
            {
                _log?.Info("지리참조 정보 없음 - 일반 TIF로 처리");
            }

            return geoTransform;
        }
        catch (Exception ex)
        {
            _log?.Error($"GeoTIFF 정보 추출 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 모델 경계 계산 (우선순위별 처리)
    /// 1. GeoTIFF 지리참조 정보 (가장 정확)
    /// 2. GMapControl 기반 화면 좌표 변환 (정확)
    /// 3. 기존 줌 레벨 기반 수학적 계산 (근사)
    /// </summary>
    private void CalculateModelBounds(
        ImageModel model,
        GeoTransformInfo geoTransform,
        PointLatLng mapCenter,
        GMapControl gMap)
    {
        try
        {
            // 우선순위 1: GeoTIFF 실제 지리참조 정보 (가장 정확)
            if (geoTransform?.HasGeoReference == true && geoTransform.GeoTransformArray != null)
            {
                _log?.Info("GeoTIFF 지리참조 정보 사용");

                if (model is IImageModel iModel)
                {
                    iModel.UpdateFromGeoTransform(geoTransform.GeoTransformArray, (int)model.Width, (int)model.Height);
                    _log?.Info($"GeoTIFF 경계 적용: {iModel.BoundsInfo()}");
                }
                return;
            }

            // 우선순위 2: GMapControl 기반 화면 좌표 변환 (정확)
            if (gMap != null && model is IImageModel iModel2)
            {
                _log?.Info("GMapControl 기반 화면 좌표 변환 사용");

                iModel2.SyncWithGMapControl(gMap, mapCenter);

                _log?.Info($"GMapControl 동기화 완료: {iModel2.BoundsInfo()}");
                return;
            }

            // 우선순위 3: 줌 레벨 기반 수학적 계산 (근사) - 기존 방식 활용
            _log?.Info("기존 방식 사용 (줌 레벨 기반)");

            RectLatLng overlayBounds;

            if (geoTransform != null)
            {
                // GeoTIFF 정보가 있지만 지리참조가 없는 경우
                var tifInfo = new TifFileInfo
                {
                    Width = (int)model.Width,
                    Height = (int)model.Height,
                    IsValid = true
                };
                overlayBounds = CalculateOverlayBounds(mapCenter, gMap?.Zoom ?? 15, tifInfo, geoTransform);
            }
            else
            {
                // 일반 이미지
                overlayBounds = CalculateImageBounds(mapCenter, gMap?.Zoom ?? 15, (int)model.Width, (int)model.Height);
            }

            // 경계 설정
            model.Left = overlayBounds.Left;
            model.Right = overlayBounds.Right;
            model.Top = overlayBounds.Top;
            model.Bottom = overlayBounds.Bottom;
            model.Latitude = mapCenter.Lat;
            model.Longitude = mapCenter.Lng;

            _log?.Info($"기존 방식 계산 완료: Left({model.Left:F6}), Right({model.Right:F6}), Top({model.Top:F6}), Bottom({model.Bottom:F6})");
        }
        catch (Exception ex)
        {
            _log?.Error($"모델 경계 계산 실패: {ex.Message}");

            // 폴백: 기본 크기로 설정
            const double defaultSize = 0.01; // 약 1km 정도
            model.Left = mapCenter.Lng - defaultSize / 2;
            model.Right = mapCenter.Lng + defaultSize / 2;
            model.Top = mapCenter.Lat + defaultSize / 2;
            model.Bottom = mapCenter.Lat - defaultSize / 2;
            model.Latitude = mapCenter.Lat;
            model.Longitude = mapCenter.Lng;

            _log?.Warning($"폴백 경계 설정: {defaultSize}도 사각형");
        }
    }


    /// <summary>
    /// 오버레이 경계 계산
    /// </summary>
    private RectLatLng CalculateOverlayBounds(PointLatLng mapCenter, double currentZoom, TifFileInfo tifInfo, GeoTransformInfo geoTransform)
    {
        // GeoTIFF인 경우 실제 지리 좌표 사용
        if (geoTransform?.HasGeoReference == true)
        {
            return new RectLatLng(
                geoTransform.MaxLatitude,
                geoTransform.MinLongitude,
                geoTransform.MaxLongitude - geoTransform.MinLongitude,
                geoTransform.MaxLatitude - geoTransform.MinLatitude
            );
        }

        // 일반 TIF인 경우 줌 레벨과 이미지 비율 기반으로 계산
        return CalculateImageBounds(mapCenter, currentZoom, tifInfo.Width, tifInfo.Height);
    }

    /// <summary>
    /// 줌 레벨 기반 이미지 경계 계산
    /// </summary>
    private RectLatLng CalculateImageBounds(PointLatLng mapCenter, double currentZoom, int imageWidth, int imageHeight)
    {
        // 이미지 종횡비 계산
        double aspectRatio = (double)imageWidth / imageHeight;

        // 줌 레벨에 따른 기본 크기 (도 단위)
        double baseSize = CalculateBaseSizeForZoom(currentZoom);

        // 종횡비에 따른 너비/높이 계산
        double width, height;

        if (aspectRatio >= 1.0) // 가로가 더 긴 이미지
        {
            width = baseSize * aspectRatio;
            height = baseSize;
        }
        else // 세로가 더 긴 이미지
        {
            width = baseSize;
            height = baseSize / aspectRatio;
        }

        // 이미지 크기에 따른 스케일링
        double sizeScale = CalculateSizeScale(imageWidth, imageHeight);
        width *= sizeScale;
        height *= sizeScale;

        // 중심점 기준으로 경계 계산
        return new RectLatLng(
            mapCenter.Lat + height / 2,
            mapCenter.Lng - width / 2,
            width,
            height
        );
    }

    /// <summary>
    /// 줌 레벨에 따른 기본 크기 계산
    /// </summary>
    private double CalculateBaseSizeForZoom(double zoomLevel)
    {
        return zoomLevel switch
        {
            <= 6 => 1.0,      // 국가 단위
            <= 8 => 0.5,      // 지역 단위
            <= 10 => 0.1,     // 도시 단위
            <= 12 => 0.05,    // 구역 단위
            <= 14 => 0.01,    // 동네 단위
            <= 16 => 0.005,   // 블록 단위
            <= 18 => 0.001,   // 건물 단위
            _ => 0.0005       // 상세 단위
        };
    }

    /// <summary>
    /// 이미지 크기에 따른 스케일 계산
    /// </summary>
    private double CalculateSizeScale(int width, int height)
    {
        long totalPixels = (long)width * height;

        return totalPixels switch
        {
            <= 1_000_000 => 0.5,      // 1MP 이하: 작게
            <= 4_000_000 => 1.0,      // 4MP 이하: 기본
            <= 16_000_000 => 1.5,     // 16MP 이하: 약간 크게
            <= 50_000_000 => 2.0,     // 50MP 이하: 크게
            _ => 3.0                   // 50MP 초과: 매우 크게
        };
    }

    /// <summary>
    /// TIF를 WPF ImageSource로 변환
    /// </summary>
    private async Task<ImageSource> ConvertTifToImageSourceAsync(string tifFilePath, TifFileInfo tifInfo)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var tif = Tiff.Open(tifFilePath, "r");
                if (tif == null)
                    throw new InvalidOperationException("TIF 파일을 열 수 없습니다.");

                // 큰 이미지인 경우 미리보기 크기로 축소
                var previewSize = CalculatePreviewSize(tifInfo.Width, tifInfo.Height);

                using var bitmap = CreateBitmapFromTif(tif, tifInfo, previewSize.Width, previewSize.Height);
                return ConvertBitmapToImageSource(bitmap);
            }
            catch (Exception ex)
            {
                _log?.Error($"TIF ImageSource 변환 실패: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 일반 이미지 파일을 ImageSource로 로드
    /// </summary>
    private async Task<ImageSource> LoadImageSourceAsync(string imageFilePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(imageFilePath, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                if (bitmapImage.CanFreeze)
                    bitmapImage.Freeze();

                return bitmapImage;
            }
            catch (Exception ex)
            {
                _log?.Error($"이미지 로드 실패: {ex.Message}");
                return null;
            }
        });
    }

    /// <summary>
    /// 미리보기에 적합한 크기 계산
    /// </summary>
    private Size CalculatePreviewSize(int originalWidth, int originalHeight, int maxSize = 2048)
    {
        if (originalWidth <= maxSize && originalHeight <= maxSize)
        {
            return new Size(originalWidth, originalHeight);
        }

        double scale = Math.Min((double)maxSize / originalWidth, (double)maxSize / originalHeight);
        return new Size(
            (int)(originalWidth * scale),
            (int)(originalHeight * scale)
        );
    }

    /// <summary>
    /// TIF에서 Bitmap 생성
    /// </summary>
    private Bitmap CreateBitmapFromTif(Tiff tif, TifFileInfo tifInfo, int targetWidth, int targetHeight)
    {
        try
        {
            var bitmap = new Bitmap(targetWidth, targetHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var buffer = new byte[tifInfo.ScanlineSize];

            // 스케일링 계산
            double scaleX = (double)tifInfo.Width / targetWidth;
            double scaleY = (double)tifInfo.Height / targetHeight;

            using (var g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.Clear(System.Drawing.Color.White);

                // 스캔라인 단위로 읽어서 스케일링
                for (int y = 0; y < targetHeight; y++)
                {
                    int sourceY = Math.Min((int)(y * scaleY), tifInfo.Height - 1);

                    if (tif.ReadScanline(buffer, sourceY))
                    {
                        for (int x = 0; x < targetWidth; x++)
                        {
                            int sourceX = Math.Min((int)(x * scaleX), tifInfo.Width - 1);
                            int bufferIndex = sourceX * tifInfo.SamplesPerPixel;

                            if (bufferIndex < buffer.Length)
                            {
                                byte pixelValue = buffer[bufferIndex];
                                var color = System.Drawing.Color.FromArgb(pixelValue, pixelValue, pixelValue);
                                bitmap.SetPixel(x, y, color);
                            }
                        }
                    }
                }
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _log?.Error($"TIF Bitmap 생성 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Bitmap을 WPF ImageSource로 변환
    /// </summary>
    private ImageSource ConvertBitmapToImageSource(Bitmap bitmap)
    {
        try
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();

            if (bitmapImage.CanFreeze)
                bitmapImage.Freeze();

            return bitmapImage;
        }
        catch (Exception ex)
        {
            _log?.Error($"ImageSource 변환 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 오버레이 ID 생성
    /// </summary>
    private string GenerateOverlayId(string filePath)
    {
        return $"overlay_{Path.GetFileNameWithoutExtension(filePath)}_{DateTime.Now.Ticks}";
    }
    #endregion

    #region - Utility Methods -
    /// <summary>
    /// 오버레이 통계 정보
    /// </summary>
    public OverlayStatistics GetStatistics()
    {
        var overlays = _activeOverlays.Values.ToList();

        return new OverlayStatistics
        {
            TotalCount = overlays.Count,
            VisibleCount = overlays.Count(o => o.Visibility),
            TifCount = overlays.Count(o => Path.GetExtension(o.FilePath).ToLowerInvariant() is ".tif" or ".tiff"),
            GeoReferencedCount = overlays.Count(o => o.HasGeoReference),
            AverageOpacity = overlays.Any() ? overlays.Average(o => o.Opacity) : 0.0,
            TotalMemoryUsage = overlays.Sum(o => EstimateMemoryUsage(o))
        };
    }

    /// <summary>
    /// 메모리 사용량 추정
    /// </summary>
    private long EstimateMemoryUsage(GMapCustomImage overlay)
    {
        try
        {
            var size = overlay.OriginalImageSize;
            // RGB 24bit 기준으로 대략적인 메모리 사용량 계산
            return (long)(size.Width * size.Height * 3);
        }
        catch
        {
            return 0;
        }
    }
    #endregion
}

