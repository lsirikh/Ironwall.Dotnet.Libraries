using Caliburn.Micro;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using GMap.NET.MapProviders.Custom;
using System;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System.IO;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/29/2025 2:11:19 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CustomMapService { 
    #region - Ctors -
    public CustomMapService(
            ILogService log,
            IEventAggregator eventAggregator,
            TileGenerationService tileGenerationService,
            CustomMapProvider customMapProvider,
            IGMapDbService gMapDbService,
            GMapSetupModel setup)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _tileGenerationService = tileGenerationService;
        _customMapProvider = customMapProvider;
        _gMapDbService = gMapDbService;
        _setup = setup;
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -

    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    // <summary>
    /// TIF 파일을 타일로 변환하고 DB에 저장하는 통합 워크플로우
    /// </summary>
    public async Task<CustomMapModel> ProcessTifFileAsync(
        string tifFilePath,
        string mapName,
        TifProcessingOptions options,
        IProgress<TileConversionProgress> progress = null)
    {
        try
        {
            _log?.Info($"TIF 파일 처리 시작: {tifFilePath}");

            // 1. TIF 파일 분석
            var tifInfo = await _tileGenerationService.AnalyzeTifFileAsync(tifFilePath);
            if (!tifInfo.IsValid)
            {
                throw new InvalidOperationException($"TIF 파일 분석 실패: {tifInfo.ErrorMessage}");
            }

            // 2. 지리참조 방법에 따른 타일 생성
            CustomMapModel customMap;

            if (options.UseManualCoordinates)
            {
                // 수동 좌표 사용
                customMap = await _tileGenerationService.ConvertTifWithManualCoordsAsync(
                    tifFilePath, mapName,
                    options.ManualMinLatitude, options.ManualMinLongitude,
                    options.ManualMaxLatitude, options.ManualMaxLongitude,
                    options.MinZoom, options.MaxZoom, options.TileSize, progress);
            }
            else if (options.UseControlPoints && options.ControlPoints?.Count >= 2)
            {
                // 기준점 사용
                customMap = await ProcessTifWithControlPointsAsync(
                    tifFilePath, mapName, tifInfo, options, progress);
            }
            else
            {
                throw new ArgumentException("지리참조 방법이 지정되지 않았습니다. 수동 좌표 또는 기준점을 설정해주세요.");
            }

            // 3. 기준점 정보 설정 (기준점 사용 시)
            if (options.UseControlPoints && options.ControlPoints?.Count > 0)
            {
                customMap.ControlPoints = options.ControlPoints.Select(cp => new GeoControlPointModel
                {
                    CustomMapId = 0, // DB 저장 후 업데이트됨
                    PixelX = cp.PixelX,
                    PixelY = cp.PixelY,
                    Latitude = cp.Latitude,
                    Longitude = cp.Longitude,
                    Description = cp.Description
                }).Cast<IGeoControlPointModel>().ToList();
                customMap.ControlPointCount = customMap.ControlPoints.Count;
            }

            // 4. DB에 저장 (실제 구현!)
            var savedId = await _gMapDbService.InsertCustomMapAsync(customMap);
            customMap.Id = savedId;

            // 5. 메모리 Provider에 추가
            _customMapProvider.Add(customMap);

            _log?.Info($"TIF 파일 처리 및 DB 저장 완료: {customMap.Name}, ID: {customMap.Id}");

            // 6. 완료 이벤트 발행
            //await _eventAggregator.PublishOnUIThreadAsync(new TileGenerationCompletedEvent(customMap));

            return customMap;
        }
        catch (Exception ex)
        {
            _log?.Error($"TIF 파일 처리 실패: {ex.Message}");
            //await _eventAggregator.PublishOnUIThreadAsync(new TileGenerationFailedEvent(ex.Message));
            throw;
        }
    }

    /// <summary>
    /// 기준점을 사용한 TIF 처리 (내부 헬퍼)
    /// </summary>
    private async Task<CustomMapModel> ProcessTifWithControlPointsAsync(
        string tifFilePath, string mapName, TifFileInfo tifInfo,
        TifProcessingOptions options, IProgress<TileConversionProgress> progress)
    {
        var startTime = DateTime.Now;

        // 지리참조 정보 생성
        var geoTransform = _tileGenerationService.CreateGeoTransformFromControlPoints(
            options.ControlPoints, tifInfo.Width, tifInfo.Height);

        // CustomMap 모델 생성
        var customMap = new CustomMapModel
        {
            Name = mapName,
            SourceImagePath = tifFilePath,
            OriginalFileSize = tifInfo.FileSize,
            OriginalWidth = tifInfo.Width,
            OriginalHeight = tifInfo.Height,
            TileSize = options.TileSize,
            MinZoomLevel = options.MinZoom,
            MaxZoomLevel = options.MaxZoom,
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
            GeoReferenceMethod = EnumGeoReference.ManualControlPoints
        };

        // 타일 디렉토리 생성
        var tileDirectoryName = $"{Path.GetFileNameWithoutExtension(tifFilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}";
        var tileDirectory = Path.Combine(_setup.TileDirectory, tileDirectoryName);
        Directory.CreateDirectory(tileDirectory);
        customMap.TilesDirectoryPath = tileDirectory;

        // 타일 생성
        var totalTiles = await _tileGenerationService.GenerateTilesFromTifAsync(
            tifFilePath, tifInfo, geoTransform, tileDirectory,
            options.MinZoom, options.MaxZoom, options.TileSize, progress);

        // 최종 정보 업데이트
        customMap.TotalTileCount = totalTiles;
        customMap.TilesDirectorySize = GetDirectorySize(tileDirectory);
        customMap.ProcessedAt = DateTime.Now;
        customMap.ProcessingTimeMinutes = (int)(DateTime.Now - startTime).TotalMinutes;
        customMap.Status = EnumMapStatus.Active;
        customMap.QualityScore = CalculateQualityScore(options.ControlPoints?.Count ?? 0);

        return customMap;
    }


    /// <summary>
    /// 프로그램 시작 시 DB에서 저장된 커스텀 맵들을 로드
    /// </summary>
    public async Task LoadCustomMapsAsync()
    {
        try
        {
            _log?.Info("DB에서 커스텀 맵 로드 시작");

            // 실제 DB에서 조회!
            var customMaps = await _gMapDbService.FetchCustomMapsAsync();

            if (customMaps?.Any() != true)
            {
                _log?.Info("저장된 커스텀 맵이 없습니다.");
                return;
            }

            // 메모리 Provider 초기화
            _customMapProvider.Clear();

            foreach (var customMap in customMaps)
            {
                try
                {
                    // 유효성 검사
                    if (!ValidateCustomMapIntegrity(customMap))
                    {
                        _log?.Warning($"커스텀 맵 유효성 검사 실패: {customMap.Name}");

                        // DB에서 상태 업데이트
                        customMap.Status = EnumMapStatus.Error;
                        await _gMapDbService.UpdateCustomMapAsync(customMap);
                        continue;
                    }

                    // 메모리 Provider에 추가
                    _customMapProvider.Add(customMap);
                    _log?.Info($"커스텀 맵 로드 완료: {customMap.Name}");
                }
                catch (Exception ex)
                {
                    _log?.Error($"커스텀 맵 로드 실패: {customMap.Name}, 오류: {ex.Message}");

                    // 오류 상태로 DB 업데이트
                    customMap.Status = EnumMapStatus.Error;
                    try
                    {
                        await _gMapDbService.UpdateCustomMapAsync(customMap);
                    }
                    catch (Exception dbEx)
                    {
                        _log?.Error($"DB 상태 업데이트 실패: {dbEx.Message}");
                    }
                }
            }

            _log?.Info($"커스텀 맵 로드 완료: 총 {customMaps.Count}개 중 {_customMapProvider.CollectionEntity.Count}개 성공");
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 로드 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 커스텀 맵을 GMap.NET Provider로 활성화
    /// </summary>
    public FileBasedCustomMapProvider ActivateCustomMap(CustomMapModel customMap)
    {
        try
        {
            _log?.Info($"커스텀 맵 활성화: {customMap.Name}");

            // 이미 활성화된 경우 기존 것 반환
            if (_activeProviders.TryGetValue(customMap.Id, out var existingProvider))
            {
                _log?.Info($"이미 활성화된 커스텀 맵: {customMap.Name}");
                return existingProvider;
            }

            // 유효성 검사
            if (!ValidateCustomMapIntegrity(customMap))
            {
                throw new InvalidOperationException($"커스텀 맵이 유효하지 않습니다: {customMap.Name}");
            }

            // 경계 좌표 배열 생성
            double[] bounds = null;
            if (customMap.MinLatitude.HasValue && customMap.MaxLatitude.HasValue &&
                customMap.MinLongitude.HasValue && customMap.MaxLongitude.HasValue)
            {
                bounds = new[]
                {
                        customMap.MinLatitude.Value,
                        customMap.MinLongitude.Value,
                        customMap.MaxLatitude.Value,
                        customMap.MaxLongitude.Value
                    };
            }

            // FileBasedCustomMapProvider 생성
            var provider = new FileBasedCustomMapProvider(
                customMap.TilesDirectoryPath,
                customMap.MinZoomLevel,
                customMap.MaxZoomLevel,
                bounds);

            // Provider 유효성 확인
            if (!provider.IsValid())
            {
                throw new InvalidOperationException($"Provider가 유효하지 않습니다: {customMap.Name}");
            }

            // 활성화된 Provider 목록에 추가
            _activeProviders[customMap.Id] = provider;

            // DB에 마지막 접근 시간 기록
            _ = Task.Run(async () =>
            {
                try
                {
                    customMap.UpdatedAt = DateTime.Now;
                    await _gMapDbService.UpdateCustomMapAsync(customMap);
                }
                catch (Exception ex)
                {
                    _log?.Error($"DB 접근 시간 업데이트 실패: {ex.Message}");
                }
            });

            _log?.Info($"커스텀 맵 활성화 완료: {customMap.Name}, 타일 수: {GetProviderTileCount(provider)}");

            return provider;
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 활성화 실패: {customMap.Name}, 오류: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 커스텀 맵 비활성화
    /// </summary>
    public void DeactivateCustomMap(int customMapId)
    {
        try
        {
            if (_activeProviders.TryRemove(customMapId, out var provider))
            {
                // FileBasedCustomMapProvider에 Dispose 메서드가 있다면 호출
                if (provider is IDisposable disposable)
                    disposable.Dispose();

                _log?.Info($"커스텀 맵 비활성화 완료: ID {customMapId}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 비활성화 실패: ID {customMapId}, 오류: {ex.Message}");
        }
    }


    /// <summary>
    /// 커스텀 맵 업데이트 (메모리 + DB 동기화)
    /// </summary>
    public async Task<bool> UpdateCustomMapAsync(CustomMapModel customMap)
    {
        try
        {
            _log?.Info($"커스텀 맵 업데이트: {customMap.Name}");

            // 1. DB 업데이트
            var updatedMap = await _gMapDbService.UpdateCustomMapAsync(customMap);

            if (updatedMap == null)
            {
                _log?.Warning($"DB 업데이트 실패: {customMap.Name}");
                return false;
            }

            // 2. 메모리 Provider 동기화
            var memoryMap = _customMapProvider.CollectionEntity.FirstOrDefault(m => m.Id == customMap.Id);
            if (memoryMap != null)
            {
                // 기존 항목 제거 후 업데이트된 항목 추가
                _customMapProvider.Clear();
                var otherMaps = _customMapProvider.CollectionEntity.Where(m => m.Id != customMap.Id).ToList();
                foreach (var map in otherMaps)
                    _customMapProvider.Add(map);

                _customMapProvider.Add(updatedMap);
            }

            // 3. 활성화된 Provider가 있다면 재생성 필요할 수 있음
            if (_activeProviders.ContainsKey(customMap.Id))
            {
                DeactivateCustomMap(customMap.Id);
                // 필요시 다시 활성화는 사용자가 수동으로 진행
            }

            _log?.Info($"커스텀 맵 업데이트 완료: {customMap.Name}");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 업데이트 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 커스텀 맵 삭제 (메모리 + DB + 파일)
    /// </summary>
    public async Task<bool> DeleteCustomMapAsync(int customMapId, bool deleteFiles = false)
    {
        try
        {
            var customMap = _customMapProvider.CollectionEntity.FirstOrDefault(m => m.Id == customMapId);
            if (customMap == null)
            {
                _log?.Warning($"삭제할 커스텀 맵을 찾을 수 없음: ID {customMapId}");
                return false;
            }

            _log?.Info($"커스텀 맵 삭제 시작: {customMap.Name}");

            // 1. Provider에서 비활성화
            DeactivateCustomMap(customMapId);

            // 2. DB에서 삭제
            var dbDeleted = await _gMapDbService.DeleteCustomMapAsync(customMap);

            if (!dbDeleted)
            {
                _log?.Warning($"DB에서 삭제 실패: {customMap.Name}");
                return false;
            }

            // 3. 메모리에서 제거
            _customMapProvider.Clear();
            var remainingMaps = _customMapProvider.CollectionEntity.Where(m => m.Id != customMapId).ToList();
            foreach (var map in remainingMaps)
            {
                _customMapProvider.Add(map);
            }

            // 4. 파일 삭제 (선택사항)
            if (deleteFiles && !string.IsNullOrEmpty(customMap.TilesDirectoryPath) &&
                Directory.Exists(customMap.TilesDirectoryPath))
            {
                try
                {
                    Directory.Delete(customMap.TilesDirectoryPath, true);
                    _log?.Info($"타일 디렉토리 삭제 완료: {customMap.TilesDirectoryPath}");
                }
                catch (Exception ex)
                {
                    _log?.Error($"타일 디렉토리 삭제 실패: {ex.Message}");
                    // 파일 삭제 실패는 전체 실패로 보지 않음
                }
            }

            _log?.Info($"커스텀 맵 삭제 완료: {customMap.Name}");
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 삭제 실패: ID {customMapId}, 오류: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// ID로 커스텀 맵 조회 (DB에서)
    /// </summary>
    public async Task<CustomMapModel?> GetCustomMapByIdAsync(int id)
    {
        try
        {
            var customMap = await _gMapDbService.FetchCustomMapAsync(id);
            return customMap as CustomMapModel;
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 조회 실패: ID {id}, 오류: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 모든 커스텀 맵 새로고침 (DB에서 다시 로드)
    /// </summary>
    public async Task RefreshCustomMapsAsync()
    {
        try
        {
            _log?.Info("커스텀 맵 새로고침 시작");

            // 모든 활성 Provider 비활성화
            DeactivateAllCustomMaps();

            // DB에서 다시 로드
            await LoadCustomMapsAsync();

            _log?.Info("커스텀 맵 새로고침 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"커스텀 맵 새로고침 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 모든 활성화된 커스텀 맵 비활성화
    /// </summary>
    public void DeactivateAllCustomMaps()
    {
        var providers = _activeProviders.ToList();
        foreach (var kvp in providers)
        {
            DeactivateCustomMap(kvp.Key);
        }
    }

    /// <summary>
    /// 커스텀 맵 무결성 검증
    /// </summary>
    private bool ValidateCustomMapIntegrity(ICustomMapModel customMap)
    {
        try
        {
            if (customMap == null) return false;
            if (string.IsNullOrEmpty(customMap.Name)) return false;
            if (string.IsNullOrEmpty(customMap.TilesDirectoryPath)) return false;
            if (!Directory.Exists(customMap.TilesDirectoryPath)) return false;
            if (customMap.MinZoomLevel > customMap.MaxZoomLevel) return false;

            // 최소한 하나의 타일이 존재하는지 확인
            for (int zoom = customMap.MinZoomLevel; zoom <= customMap.MaxZoomLevel; zoom++)
            {
                var zoomDir = Path.Combine(customMap.TilesDirectoryPath, zoom.ToString());
                if (Directory.Exists(zoomDir))
                {
                    var xDirs = Directory.GetDirectories(zoomDir);
                    foreach (var xDir in xDirs)
                    {
                        if (Directory.GetFiles(xDir, "*.png").Length > 0)
                            return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 품질 점수 계산
    /// </summary>
    private double CalculateQualityScore(int controlPointCount)
    {
        return controlPointCount switch
        {
            >= 4 => 0.9,   // 4개 이상: 우수
            3 => 0.8,      // 3개: 양호
            2 => 0.6,      // 2개: 보통
            _ => 0.4       // 기타: 주의
        };
    }

    /// <summary>
    /// 디렉토리 크기 계산
    /// </summary>
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


    /// <summary>
    /// Provider의 타일 수 계산 (FileBasedCustomMapProvider에 GetTotalTileCount가 없을 경우 대체)
    /// </summary>
    private int GetProviderTileCount(FileBasedCustomMapProvider provider)
    {
        try
        {
            // FileBasedCustomMapProvider에 GetTotalTileCount 메서드가 있는지 확인
            var method = provider.GetType().GetMethod("GetTotalTileCount");
            if (method != null)
            {
                return (int)method.Invoke(provider, null);
            }

            // 없다면 직접 계산
            var tilesDirectory = provider.TilesDirectory;
            if (string.IsNullOrEmpty(tilesDirectory) || !Directory.Exists(tilesDirectory))
                return 0;

            int totalTiles = 0;
            var zoomDirs = Directory.GetDirectories(tilesDirectory);

            foreach (var zoomDir in zoomDirs)
            {
                var xDirs = Directory.GetDirectories(zoomDir);
                foreach (var xDir in xDirs)
                {
                    totalTiles += Directory.GetFiles(xDir, "*.png").Length;
                }
            }

            return totalTiles;
        }
        catch (Exception ex)
        {
            _log?.Error($"타일 수 계산 실패: {ex.Message}");
            return 0;
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 활성화된 커스텀 맵 Provider 목록
    /// </summary>
    public IReadOnlyDictionary<int, FileBasedCustomMapProvider> ActiveProviders => _activeProviders;

    /// <summary>
    /// 활성화된 커스텀 맵 개수
    /// </summary>
    public int ActiveCustomMapCount => _activeProviders.Count;

    /// <summary>
    /// DB 연결 상태
    /// </summary>
    public bool IsDbConnected => _gMapDbService?.IsConnected == true;

    #endregion
    #region - Attributes -
    private readonly ILogService _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly TileGenerationService _tileGenerationService;
    private readonly CustomMapProvider _customMapProvider;
    private readonly IGMapDbService _gMapDbService;
    private readonly GMapSetupModel _setup;

    private readonly ConcurrentDictionary<int, FileBasedCustomMapProvider> _activeProviders = new();
    #endregion

}

/// <summary>
/// TIF 처리 옵션 (기존과 동일)
/// </summary>
public class TifProcessingOptions
{
    public bool UseManualCoordinates { get; set; }
    public double ManualMinLatitude { get; set; }
    public double ManualMinLongitude { get; set; }
    public double ManualMaxLatitude { get; set; }
    public double ManualMaxLongitude { get; set; }

    public bool UseControlPoints { get; set; }
    public List<ManualGeoPoint> ControlPoints { get; set; } = new();

    public int MinZoom { get; set; } = 10;
    public int MaxZoom { get; set; } = 16;
    public int TileSize { get; set; } = 256;
}