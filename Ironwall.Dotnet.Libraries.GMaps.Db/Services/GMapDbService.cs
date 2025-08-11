using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Linq;
using System.Threading;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/25/2025 10:38:55 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
// <summary>
/// Map DB Service - EventDbService 패턴 기반
/// </summary>
internal class GMapDbService : TaskService, IGMapDbService
{

    #region - Ctors -
    public GMapDbService(ILogService log,
                       IEventAggregator eventAggregator,
                       MapProvider mapProvider,
                       CustomMapProvider customMapProvider,
                       DefinedMapProvider definedMapProvider,
                       GMapDbSetupModel setupModel)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _setup = setupModel;
        _mapProvider = mapProvider;
        _customMapProvider = customMapProvider;
        _definedMapProvider = definedMapProvider;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task RunTask(CancellationToken token = default)
    {
        await StartService(token);
    }

    protected override async Task ExitTask(CancellationToken token = default)
    {
        await StopService(token);
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public async Task<bool> StartService(CancellationToken token = default)
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            await Connect(token);
            await BuildSchemeAsync(token);
            await FetchInstanceAsync(token: token);
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"MapDbService 시작 실패: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StopService(CancellationToken token = default)
    {
        try
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            await Disconnect(token);
            return true;
        }
        catch (Exception ex)
        {
            _log?.Error($"MapDbService 중지 실패: {ex.Message}");
            return false;
        }
    }


    private string BuildConnStr(bool includeDb = true)
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            CharacterSet = "utf8mb4",
            SslMode = MySqlSslMode.Disabled,
            Pooling = true
        };
        if (includeDb)
            csb.Database = _setup.DbDatabase.ToLowerInvariant();
        return csb.ToString();
    }

    public async Task Connect(CancellationToken token = default)
    {
        try
        {
            // DB 이름 통일
            var dbName = (_setup.DbDatabase ?? "gmap_tiles_db").ToLowerInvariant();
            _setup.DbDatabase = dbName;

            // DB 생성 (없으면)
            await using (var bootstrap = new MySqlConnection(BuildConnStr(includeDb: false)))
            {
                await bootstrap.OpenAsync(token);
                var createDbSql = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci;";
                await bootstrap.ExecuteAsync(createDbSql, token);
                _log?.Info($"DB({dbName}) 생성/확인 완료");
            }

            // 애플리케이션 커넥션
            _conn = new MySqlConnection(BuildConnStr(includeDb: true));
            await _conn.OpenAsync(token);

            var msg = $"Map DB 연결 성공: {_setup.IpDbServer}:{_setup.PortDbServer}/{dbName}";
            _log?.Info(msg);

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage()
                { Title = nameof(GMapDbService), Message = msg });
        }
        catch (Exception ex)
        {
            _log?.Error($"Map DB 연결 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken token = default)
    {
        try
        {
            var conn = new MySqlConnection(BuildConnStr(includeDb: true));
            await conn.OpenAsync(token);
            return conn;
        }
        catch (Exception ex)
        {
            _log?.Error($"DB 커넥션 생성 실패: {ex.Message}");
            throw;
        }
    }

    public async Task Disconnect(CancellationToken token = default)
    {
        try
        {
            if (_conn != null && _conn.State == ConnectionState.Open)
            {
                await _conn.CloseAsync();
                _conn.Dispose();
                _log?.Info("Map DB 연결 종료 완료");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"DB 연결 종료 실패: {ex.Message}");
        }
    }

    public async Task BuildSchemeAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            // ─────────────────── Maps (공통 테이블) ───────────────────────────────
            var createMapsSql = @"
                CREATE TABLE IF NOT EXISTS `Maps` (
                    `Id`                INT AUTO_INCREMENT PRIMARY KEY,
                    `Name`              VARCHAR(100) NOT NULL,
                    `Description`       TEXT,
                    `ProviderType`      VARCHAR(20) NOT NULL,        -- Custom, Defined
                    `Category`          VARCHAR(20) NOT NULL,        -- Standard, Satellite, etc.
                    `DataType`          VARCHAR(20) DEFAULT 'Raster',
                    `CoordinateSystem`  VARCHAR(50) DEFAULT 'WGS84',
                    `EpsgCode`          VARCHAR(20),
                    `MinLatitude`       DECIMAL(10,8),
                    `MaxLatitude`       DECIMAL(10,8),
                    `MinLongitude`      DECIMAL(11,8),
                    `MaxLongitude`      DECIMAL(11,8),
                    `MinZoomLevel`      INT DEFAULT 0,
                    `MaxZoomLevel`      INT DEFAULT 18,
                    `TileSize`          INT DEFAULT 256,
                    `Status`            VARCHAR(20) DEFAULT 'Active',
                    `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy`         VARCHAR(100),
                    INDEX `IX_Maps_Provider` (`ProviderType`),
                    INDEX `IX_Maps_Category` (`Category`),
                    INDEX `IX_Maps_Status` (`Status`)
                );";

            // ─────────────────── CustomMaps (상세 테이블) ─────────────────────────
            var createCustomMapsSql = @"
                CREATE TABLE IF NOT EXISTS `CustomMaps` (
                    `MapId`                 INT PRIMARY KEY,
                    `SourceImagePath`       VARCHAR(500) NOT NULL,
                    `TilesDirectoryPath`    VARCHAR(500) NOT NULL,
                    `OriginalWidth`         INT DEFAULT 0,
                    `OriginalHeight`        INT DEFAULT 0,
                    `OriginalFileSize`      BIGINT DEFAULT 0,
                    `TotalTileCount`        INT DEFAULT 0,
                    `TilesDirectorySize`    BIGINT DEFAULT 0,
                    `PixelResolutionX`      DECIMAL(15,10),
                    `PixelResolutionY`      DECIMAL(15,10),
                    `ResolutionUnit`        VARCHAR(20) DEFAULT 'degrees',
                    `GeoReferenceMethod`    VARCHAR(30) DEFAULT 'Automatic',
                    `GeoTransformMatrix`    TEXT,
                    `ControlPointCount`     INT DEFAULT 0,
                    `ProcessedAt`           DATETIME,
                    `ProcessingTimeMinutes` INT,
                    `QualityScore`          DECIMAL(3,2),
                    CONSTRAINT `FK_CustomMaps_Maps`
                        FOREIGN KEY (`MapId`) REFERENCES `Maps` (`Id`)
                        ON DELETE CASCADE
                );";

            // ─────────────────── DefinedMaps (상세 테이블) ────────────────────────
            var createDefinedMapsSql = @"
                CREATE TABLE IF NOT EXISTS `DefinedMaps` (
                    `MapId`             INT PRIMARY KEY,
                    `GMapProviderName`  VARCHAR(100) NOT NULL,
                    `ProviderGuid`      VARCHAR(50),
                    `Vendor`            VARCHAR(20) NOT NULL,        -- Google, Microsoft, etc.
                    `Style`             VARCHAR(20) NOT NULL,        -- Normal, Satellite, etc.
                    `RequiresApiKey`    BOOLEAN DEFAULT FALSE,
                    `ApiKey`            VARCHAR(200),
                    `ServiceUrl`        VARCHAR(500),
                    `DailyRequestLimit` INT,
                    `LicenseInfo`       TEXT,
                    `LastAccessedAt`    DATETIME,
                    `TodayUsageCount`   INT DEFAULT 0,
                    CONSTRAINT `FK_DefinedMaps_Maps`
                        FOREIGN KEY (`MapId`) REFERENCES `Maps` (`Id`)
                        ON DELETE CASCADE
                );";

            // ─────────────────── GeoControlPoints ────────────────────────────────
            var createControlPointsSql = @"
                CREATE TABLE IF NOT EXISTS `GeoControlPoints` (
                    `Id`            INT AUTO_INCREMENT PRIMARY KEY,
                    `CustomMapId`   INT NOT NULL,
                    `PixelX`        DECIMAL(10,2) NOT NULL,
                    `PixelY`        DECIMAL(10,2) NOT NULL,
                    `Latitude`      DECIMAL(10,8) NOT NULL,
                    `Longitude`     DECIMAL(11,8) NOT NULL,
                    `AccuracyMeters` DECIMAL(8,2),
                    `Description`   VARCHAR(200),
                    `CreatedAt`     DATETIME DEFAULT CURRENT_TIMESTAMP,
                    CONSTRAINT `FK_ControlPoints_CustomMaps`
                        FOREIGN KEY (`CustomMapId`) REFERENCES `Maps` (`Id`)
                        ON DELETE CASCADE,
                    INDEX `IX_ControlPoints_Map` (`CustomMapId`)
                );";

            // 실행 순서
            await _conn.ExecuteAsync(createMapsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "Maps 테이블 생성…" });

            await _conn.ExecuteAsync(createCustomMapsSql);
            await _conn.ExecuteAsync(createDefinedMapsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "Map 상세 테이블 생성…" });

            await _conn.ExecuteAsync(createControlPointsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "GeoControlPoints 테이블 생성…" });

            _log?.Info("Map 관련 테이블 생성/확인 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"DB 스키마 생성 실패: {ex.Message}");
            throw;
        }
    }

    public async Task FetchInstanceAsync(CancellationToken token = default)
    {
        bool gateEntered = false;

        if (!await _processGate.WaitAsync(0))
            return;

        gateEntered = true;

        try
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }

            if (token == default)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
            else
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            }

            // CustomMaps 로드
            var customMaps = await FetchCustomMapsAsync(token: token);
            _customMapProvider.Clear();
            if (customMaps?.Any() == true)
            {
                foreach (var map in customMaps)
                {
                    _mapProvider.Add(map);
                }
            }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage()
                { Title = nameof(GMapDbService), Message = "CustomMap 정보를 모두 불러왔습니다..." });

            // DefinedMaps 로드
            var definedMaps = await FetchDefinedMapsAsync(token: token);
            _definedMapProvider.Clear();
            if (definedMaps?.Any() == true)
            {
                foreach (var map in definedMaps)
                {
                    _mapProvider.Add(map);
                }
            }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage()
                { Title = nameof(GMapDbService), Message = "DefinedMap 정보를 모두 불러왔습니다..." });

        }
        catch (Exception ex)
        {
            _log?.Error($"Map 데이터 로드 실패: {ex.Message}");
            throw;
        }
        finally
        {
            if (gateEntered)
                _processGate.Release();
        }
    }

    #region - CustomMap CRUD -
    public async Task<List<ICustomMapModel>?> FetchCustomMapsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT  m.Id, m.Name, m.Description, m.ProviderType, m.Category, m.DataType,
                        m.CoordinateSystem, m.EpsgCode, m.MinLatitude, m.MaxLatitude,
                        m.MinLongitude, m.MaxLongitude, m.MinZoomLevel, m.MaxZoomLevel,
                        m.TileSize, m.Status, m.CreatedAt, m.UpdatedAt, m.CreatedBy,
                        c.MapId, c.SourceImagePath, c.TilesDirectoryPath, c.OriginalWidth,
                        c.OriginalHeight, c.OriginalFileSize, c.TotalTileCount, c.TilesDirectorySize,
                        c.PixelResolutionX, c.PixelResolutionY, c.ResolutionUnit, c.GeoReferenceMethod,
                        c.GeoTransformMatrix, c.ControlPointCount, c.ProcessedAt, c.ProcessingTimeMinutes,
                        c.QualityScore
                FROM    Maps m
                JOIN    CustomMaps c ON c.MapId = m.Id
                WHERE   m.ProviderType = 'Custom'
                ORDER BY m.CreatedAt DESC;";

            var list = (await conn.QueryAsync<MapSQL, CustomMapSQL, ICustomMapModel>(
                sql,
                map: (mapSql, customSql) =>
                {
                    if (token.IsCancellationRequested)
                        throw new TaskCanceledException("Task was cancelled!");

                    return customSql.ToDomain(mapSql);
                },
                splitOn: "MapId"))
                .ToList();

            // 각 CustomMap의 ControlPoints 로드
            foreach (var customMap in list)
            {
                var controlPoints = await FetchControlPointsAsync(customMap.Id, token);
                if (controlPoints?.Any() == true)
                {
                    customMap.ControlPoints = controlPoints.ToList();
                    customMap.ControlPointCount = controlPoints.Count;
                }
            }

            _log?.Info($"FetchCustomMapsAsync 완료 - {list.Count}건");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error($"CustomMaps 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<ICustomMapModel?> FetchCustomMapAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
                SELECT  m.Id, m.Name, m.Description, m.ProviderType, m.Category, m.DataType,
                        m.CoordinateSystem, m.EpsgCode, m.MinLatitude, m.MaxLatitude,
                        m.MinLongitude, m.MaxLongitude, m.MinZoomLevel, m.MaxZoomLevel,
                        m.TileSize, m.Status, m.CreatedAt, m.UpdatedAt, m.CreatedBy,
                        c.MapId, c.SourceImagePath, c.TilesDirectoryPath, c.OriginalWidth,
                        c.OriginalHeight, c.OriginalFileSize, c.TotalTileCount, c.TilesDirectorySize,
                        c.PixelResolutionX, c.PixelResolutionY, c.ResolutionUnit, c.GeoReferenceMethod,
                        c.GeoTransformMatrix, c.ControlPointCount, c.ProcessedAt, c.ProcessingTimeMinutes,
                        c.QualityScore
                FROM    Maps m
                JOIN    CustomMaps c ON c.MapId = m.Id
                WHERE   m.Id = @Id AND m.ProviderType = 'Custom';";

            var customMap = (await conn.QueryAsync<MapSQL, CustomMapSQL, ICustomMapModel>(
                sql,
                (mapSql, customSql) => customSql.ToDomain(mapSql),
                new { Id = id },
                splitOn: "MapId")).SingleOrDefault();

            if (customMap != null)
            {
                // ControlPoints 로드
                var controlPoints = await FetchControlPointsAsync(customMap.Id, token);
                if (controlPoints?.Any() == true)
                {
                    customMap.ControlPoints = controlPoints.ToList();
                }
            }

            _log?.Info(customMap != null
                ? $"FetchCustomMapAsync 완료 - Id={customMap.Id}"
                : $"FetchCustomMapAsync 대상 없음 - Id={id}");

            return customMap;
        }
        catch (Exception ex)
        {
            _log?.Error($"CustomMap 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<int> InsertCustomMapAsync(ICustomMapModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        using var tx = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Maps 테이블에 먼저 삽입
            const string mapSql = @"
                INSERT INTO Maps
                (Name, Description, ProviderType, Category, DataType, CoordinateSystem, EpsgCode,
                 MinLatitude, MaxLatitude, MinLongitude, MaxLongitude, MinZoomLevel, MaxZoomLevel,
                 TileSize, Status, CreatedBy)
                VALUES (@Name, @Description, 'Custom', @Category, @DataType, @CoordinateSystem, @EpsgCode,
                        @MinLatitude, @MaxLatitude, @MinLongitude, @MaxLongitude, @MinZoomLevel, @MaxZoomLevel,
                        @TileSize, @Status, @CreatedBy);
                SELECT LAST_INSERT_ID();";

            var mapId = await conn.ExecuteScalarAsync<int>(mapSql, new
            {
                model.Name,
                model.Description,
                Category = model.Category.ToString(),
                DataType = model.DataType.ToString(),
                model.CoordinateSystem,
                model.EpsgCode,
                model.MinLatitude,
                model.MaxLatitude,
                model.MinLongitude,
                model.MaxLongitude,
                model.MinZoomLevel,
                model.MaxZoomLevel,
                model.TileSize,
                Status = model.Status.ToString(),
                model.CreatedBy
            }, tx);

            // 2. CustomMaps 상세 테이블에 삽입
            const string customSql = @"
                INSERT INTO CustomMaps
                (MapId, SourceImagePath, TilesDirectoryPath, OriginalWidth, OriginalHeight,
                 OriginalFileSize, TotalTileCount, TilesDirectorySize, PixelResolutionX, PixelResolutionY,
                 ResolutionUnit, GeoReferenceMethod, GeoTransformMatrix, ControlPointCount,
                 ProcessedAt, ProcessingTimeMinutes, QualityScore)
                VALUES (@MapId, @SourceImagePath, @TilesDirectoryPath, @OriginalWidth, @OriginalHeight,
                        @OriginalFileSize, @TotalTileCount, @TilesDirectorySize, @PixelResolutionX, @PixelResolutionY,
                        @ResolutionUnit, @GeoReferenceMethod, @GeoTransformMatrix, @ControlPointCount,
                        @ProcessedAt, @ProcessingTimeMinutes, @QualityScore);";

            await conn.ExecuteAsync(customSql, new
            {
                MapId = mapId,
                model.SourceImagePath,
                model.TilesDirectoryPath,
                model.OriginalWidth,
                model.OriginalHeight,
                model.OriginalFileSize,
                model.TotalTileCount,
                model.TilesDirectorySize,
                model.PixelResolutionX,
                model.PixelResolutionY,
                model.ResolutionUnit,
                GeoReferenceMethod = model.GeoReferenceMethod.ToString(),
                model.GeoTransformMatrix,
                model.ControlPointCount,
                model.ProcessedAt,
                model.ProcessingTimeMinutes,
                model.QualityScore
            }, tx);

            // 3. ControlPoints 삽입 (있다면)
            if (model.ControlPoints?.Any() == true)
            {
                foreach (var point in model.ControlPoints)
                {
                    point.CustomMapId = mapId;
                    await InsertControlPointAsync(point, conn, tx, token);
                }
            }

            await tx.CommitAsync(token);
            model.Id = mapId;

            _log?.Info($"CustomMap 삽입 완료 - Id={mapId}, Name={model.Name}");
            return mapId;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error($"CustomMap 삽입 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<ICustomMapModel?> UpdateCustomMapAsync(ICustomMapModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        using var tx = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. Maps 테이블 업데이트
            const string mapSql = @"
                UPDATE Maps SET
                    Name = @Name, Description = @Description, Category = @Category,
                    DataType = @DataType, CoordinateSystem = @CoordinateSystem, EpsgCode = @EpsgCode,
                    MinLatitude = @MinLatitude, MaxLatitude = @MaxLatitude,
                    MinLongitude = @MinLongitude, MaxLongitude = @MaxLongitude,
                    MinZoomLevel = @MinZoomLevel, MaxZoomLevel = @MaxZoomLevel,
                    TileSize = @TileSize, Status = @Status, CreatedBy = @CreatedBy
                WHERE Id = @Id;";

            // 2. CustomMaps 테이블 업데이트
            const string customSql = @"
                UPDATE CustomMaps SET
                    SourceImagePath = @SourceImagePath, TilesDirectoryPath = @TilesDirectoryPath,
                    OriginalWidth = @OriginalWidth, OriginalHeight = @OriginalHeight,
                    OriginalFileSize = @OriginalFileSize, TotalTileCount = @TotalTileCount,
                    TilesDirectorySize = @TilesDirectorySize, PixelResolutionX = @PixelResolutionX,
                    PixelResolutionY = @PixelResolutionY, ResolutionUnit = @ResolutionUnit,
                    GeoReferenceMethod = @GeoReferenceMethod, GeoTransformMatrix = @GeoTransformMatrix,
                    ControlPointCount = @ControlPointCount, ProcessedAt = @ProcessedAt,
                    ProcessingTimeMinutes = @ProcessingTimeMinutes, QualityScore = @QualityScore
                WHERE MapId = @Id;";

            var param = new
            {
                model.Id,
                model.Name,
                model.Description,
                Category = model.Category.ToString(),
                DataType = model.DataType.ToString(),
                model.CoordinateSystem,
                model.EpsgCode,
                model.MinLatitude,
                model.MaxLatitude,
                model.MinLongitude,
                model.MaxLongitude,
                model.MinZoomLevel,
                model.MaxZoomLevel,
                model.TileSize,
                Status = model.Status.ToString(),
                model.CreatedBy,
                model.SourceImagePath,
                model.TilesDirectoryPath,
                model.OriginalWidth,
                model.OriginalHeight,
                model.OriginalFileSize,
                model.TotalTileCount,
                model.TilesDirectorySize,
                model.PixelResolutionX,
                model.PixelResolutionY,
                model.ResolutionUnit,
                GeoReferenceMethod = model.GeoReferenceMethod.ToString(),
                model.GeoTransformMatrix,
                model.ControlPointCount,
                model.ProcessedAt,
                model.ProcessingTimeMinutes,
                model.QualityScore
            };

            int mapAffected = await conn.ExecuteAsync(mapSql, param, tx);
            int customAffected = await conn.ExecuteAsync(customSql, param, tx);

            if (mapAffected == 0 || customAffected == 0)
                throw new KeyNotFoundException($"CustomMap not found. Id={model.Id}");

            // 3. ControlPoints 재등록 (기존 삭제 후 새로 삽입)
            if (model.ControlPoints?.Any() == true)
            {
                // 기존 ControlPoints 삭제
                const string deletePointsSql = "DELETE FROM GeoControlPoints WHERE CustomMapId = @Id;";
                await conn.ExecuteAsync(deletePointsSql, new { Id = model.Id }, tx);

                // 새로운 ControlPoints 삽입
                const string insertPointSql = @"
                                INSERT INTO GeoControlPoints
                                (CustomMapId, PixelX, PixelY, Latitude, Longitude, AccuracyMeters, Description)
                                VALUES (@CustomMapId, @PixelX, @PixelY, @Latitude, @Longitude, @AccuracyMeters, @Description);";

                foreach (var point in model.ControlPoints)
                {
                    await conn.ExecuteAsync(insertPointSql, new
                    {
                        CustomMapId = model.Id,
                        point.PixelX,
                        point.PixelY,
                        point.Latitude,
                        point.Longitude,
                        point.AccuracyMeters,
                        point.Description
                    }, tx);
                }
            }

            await tx.CommitAsync(token);

            _log?.Info($"CustomMap 업데이트 완료 - Id={model.Id}");
            return await FetchCustomMapAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error($"CustomMap 업데이트 실패: {ex.Message}");
            throw;
        }
    }


    public async Task<bool> DeleteCustomMapAsync(ICustomMapModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // FK CASCADE로 CustomMaps, GeoControlPoints도 함께 삭제됨
            const string sql = "DELETE FROM Maps WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteCustomMapAsync 완료 - Id={model.Id}"
                : $"DeleteCustomMapAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"CustomMap 삭제 실패: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - DefinedMap CRUD -
    public async Task<List<IDefinedMapModel>?> FetchDefinedMapsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT  m.Id, m.Name, m.Description, m.ProviderType, m.Category, m.DataType,
                        m.CoordinateSystem, m.EpsgCode, m.MinLatitude, m.MaxLatitude,
                        m.MinLongitude, m.MaxLongitude, m.MinZoomLevel, m.MaxZoomLevel,
                        m.TileSize, m.Status, m.CreatedAt, m.UpdatedAt, m.CreatedBy,
                        d.MapId, d.GMapProviderName, d.ProviderGuid, d.Vendor, d.Style,
                        d.RequiresApiKey, d.ApiKey, d.ServiceUrl, d.DailyRequestLimit,
                        d.LicenseInfo, d.LastAccessedAt, d.TodayUsageCount
                FROM    Maps m
                JOIN    DefinedMaps d ON d.MapId = m.Id
                WHERE   m.ProviderType = 'Defined'
                ORDER BY m.CreatedAt DESC;";

            var list = (await conn.QueryAsync<MapSQL, DefinedMapSQL, IDefinedMapModel>(
                sql,
                map: (mapSql, definedSql) =>
                {
                    if (token.IsCancellationRequested)
                        throw new TaskCanceledException("Task was cancelled!");

                    return definedSql.ToDomain(mapSql);
                },
                splitOn: "MapId"))
                .ToList();

            _log?.Info($"FetchDefinedMapsAsync 완료 - {list.Count}건");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error($"DefinedMaps 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<IDefinedMapModel?> FetchDefinedMapAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
                SELECT  m.Id, m.Name, m.Description, m.ProviderType, m.Category, m.DataType,
                        m.CoordinateSystem, m.EpsgCode, m.MinLatitude, m.MaxLatitude,
                        m.MinLongitude, m.MaxLongitude, m.MinZoomLevel, m.MaxZoomLevel,
                        m.TileSize, m.Status, m.CreatedAt, m.UpdatedAt, m.CreatedBy,
                        d.MapId, d.GMapProviderName, d.ProviderGuid, d.Vendor, d.Style,
                        d.RequiresApiKey, d.ApiKey, d.ServiceUrl, d.DailyRequestLimit,
                        d.LicenseInfo, d.LastAccessedAt, d.TodayUsageCount
                FROM    Maps m
                JOIN    DefinedMaps d ON d.MapId = m.Id
                WHERE   m.Id = @Id AND m.ProviderType = 'Defined';";

            var definedMap = (await conn.QueryAsync<MapSQL, DefinedMapSQL, IDefinedMapModel>(
                sql,
                (mapSql, definedSql) => definedSql.ToDomain(mapSql),
                new { Id = id },
                splitOn: "MapId")).SingleOrDefault();

            _log?.Info(definedMap != null
                ? $"FetchDefinedMapAsync 완료 - Id={definedMap.Id}"
                : $"FetchDefinedMapAsync 대상 없음 - Id={id}");

            return definedMap;
        }
        catch (Exception ex)
        {
            _log?.Error($"DefinedMap 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<int> InsertDefinedMapAsync(IDefinedMapModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        using var tx = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Maps 테이블에 먼저 삽입
            const string mapSql = @"
                INSERT INTO Maps
                (Name, Description, ProviderType, Category, DataType, CoordinateSystem, EpsgCode,
                 MinLatitude, MaxLatitude, MinLongitude, MaxLongitude, MinZoomLevel, MaxZoomLevel,
                 TileSize, Status, CreatedBy)
                VALUES (@Name, @Description, 'Defined', @Category, @DataType, @CoordinateSystem, @EpsgCode,
                        @MinLatitude, @MaxLatitude, @MinLongitude, @MaxLongitude, @MinZoomLevel, @MaxZoomLevel,
                        @TileSize, @Status, @CreatedBy);
                SELECT LAST_INSERT_ID();";

            var mapId = await conn.ExecuteScalarAsync<int>(mapSql, new
            {
                model.Name,
                model.Description,
                Category = model.Category.ToString(),
                DataType = model.DataType.ToString(),
                model.CoordinateSystem,
                model.EpsgCode,
                model.MinLatitude,
                model.MaxLatitude,
                model.MinLongitude,
                model.MaxLongitude,
                model.MinZoomLevel,
                model.MaxZoomLevel,
                model.TileSize,
                Status = model.Status.ToString(),
                model.CreatedBy
            }, tx);

            // 2. DefinedMaps 상세 테이블에 삽입
            const string definedSql = @"
                INSERT INTO DefinedMaps
                (MapId, GMapProviderName, ProviderGuid, Vendor, Style, RequiresApiKey,
                 ApiKey, ServiceUrl, DailyRequestLimit, LicenseInfo, LastAccessedAt, TodayUsageCount)
                VALUES (@MapId, @GMapProviderName, @ProviderGuid, @Vendor, @Style, @RequiresApiKey,
                        @ApiKey, @ServiceUrl, @DailyRequestLimit, @LicenseInfo, @LastAccessedAt, @TodayUsageCount);";

            await conn.ExecuteAsync(definedSql, new
            {
                MapId = mapId,
                model.GMapProviderName,
                model.ProviderGuid,
                Vendor = model.Vendor.ToString(),
                Style = model.Style.ToString(),
                model.RequiresApiKey,
                model.ApiKey,
                model.ServiceUrl,
                model.DailyRequestLimit,
                model.LicenseInfo,
                model.LastAccessedAt,
                model.TodayUsageCount
            }, tx);

            await tx.CommitAsync(token);
            model.Id = mapId;

            _log?.Info($"DefinedMap 삽입 완료 - Id={mapId}, Name={model.Name}");
            return mapId;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error($"DefinedMap 삽입 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<IDefinedMapModel?> UpdateDefinedMapAsync(IDefinedMapModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        using var tx = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. Maps 테이블 업데이트
            const string mapSql = @"
                UPDATE Maps SET
                    Name = @Name, Description = @Description, Category = @Category,
                    DataType = @DataType, CoordinateSystem = @CoordinateSystem, EpsgCode = @EpsgCode,
                    MinLatitude = @MinLatitude, MaxLatitude = @MaxLatitude,
                    MinLongitude = @MinLongitude, MaxLongitude = @MaxLongitude,
                    MinZoomLevel = @MinZoomLevel, MaxZoomLevel = @MaxZoomLevel,
                    TileSize = @TileSize, Status = @Status, CreatedBy = @CreatedBy
                WHERE Id = @Id;";

            // 2. DefinedMaps 테이블 업데이트
            const string definedSql = @"
                UPDATE DefinedMaps SET
                    GMapProviderName = @GMapProviderName, ProviderGuid = @ProviderGuid,
                    Vendor = @Vendor, Style = @Style, RequiresApiKey = @RequiresApiKey,
                    ApiKey = @ApiKey, ServiceUrl = @ServiceUrl, DailyRequestLimit = @DailyRequestLimit,
                    LicenseInfo = @LicenseInfo, LastAccessedAt = @LastAccessedAt, TodayUsageCount = @TodayUsageCount
                WHERE MapId = @Id;";

            var param = new
            {
                model.Id,
                model.Name,
                model.Description,
                Category = model.Category.ToString(),
                DataType = model.DataType.ToString(),
                model.CoordinateSystem,
                model.EpsgCode,
                model.MinLatitude,
                model.MaxLatitude,
                model.MinLongitude,
                model.MaxLongitude,
                model.MinZoomLevel,
                model.MaxZoomLevel,
                model.TileSize,
                Status = model.Status.ToString(),
                model.CreatedBy,
                model.GMapProviderName,
                model.ProviderGuid,
                Vendor = model.Vendor.ToString(),
                Style = model.Style.ToString(),
                model.RequiresApiKey,
                model.ApiKey,
                model.ServiceUrl,
                model.DailyRequestLimit,
                model.LicenseInfo,
                model.LastAccessedAt,
                model.TodayUsageCount
            };

            int mapAffected = await conn.ExecuteAsync(mapSql, param, tx);
            int definedAffected = await conn.ExecuteAsync(definedSql, param, tx);

            if (mapAffected == 0 || definedAffected == 0)
                throw new KeyNotFoundException($"DefinedMap not found. Id={model.Id}");

            await tx.CommitAsync(token);

            _log?.Info($"DefinedMap 업데이트 완료 - Id={model.Id}");
            return await FetchDefinedMapAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error($"DefinedMap 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteDefinedMapAsync(IDefinedMapModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // FK CASCADE로 DefinedMaps도 함께 삭제됨
            const string sql = "DELETE FROM Maps WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteDefinedMapAsync 완료 - Id={model.Id}"
                : $"DeleteDefinedMapAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"DefinedMap 삭제 실패: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - GeoControlPoint CRUD -
    public async Task<List<IGeoControlPointModel>?> FetchControlPointsAsync(int customMapId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT Id, CustomMapId, PixelX, PixelY, Latitude, Longitude, AccuracyMeters, Description
                FROM GeoControlPoints
                WHERE CustomMapId = @CustomMapId
                ORDER BY Id;";

            var list = (await conn.QueryAsync<GeoControlPointSQL>(sql, new { CustomMapId = customMapId }))
                .Select(cp => cp.ToDomain())
                .ToList();

            _log?.Info($"FetchControlPointsAsync 완료 - CustomMapId={customMapId}, {list.Count}건");
            return list.OfType<IGeoControlPointModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"ControlPoints 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<int> InsertControlPointAsync(IGeoControlPointModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                INSERT INTO GeoControlPoints
                (CustomMapId, PixelX, PixelY, Latitude, Longitude, AccuracyMeters, Description)
                VALUES (@CustomMapId, @PixelX, @PixelY, @Latitude, @Longitude, @AccuracyMeters, @Description);
                SELECT LAST_INSERT_ID();";

            int id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                model.CustomMapId,
                model.PixelX,
                model.PixelY,
                model.Latitude,
                model.Longitude,
                model.AccuracyMeters,
                model.Description
            });

            model.Id = id;
            _log?.Info($"ControlPoint 삽입 완료 - Id={id}");
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error($"ControlPoint 삽입 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<int> InsertControlPointAsync(IGeoControlPointModel model,
        MySqlConnection conn, MySqlTransaction tx, CancellationToken token = default)
    {
        const string sql = @"
        INSERT INTO GeoControlPoints
        (CustomMapId, PixelX, PixelY, Latitude, Longitude, AccuracyMeters, Description)
        VALUES (@CustomMapId, @PixelX, @PixelY, @Latitude, @Longitude, @AccuracyMeters, @Description);
        SELECT LAST_INSERT_ID();";

        int id = await conn.ExecuteScalarAsync<int>(sql, new
        {
            model.CustomMapId,
            model.PixelX,
            model.PixelY,
            model.Latitude,
            model.Longitude,
            model.AccuracyMeters,
            model.Description
        }, tx);

        model.Id = id;
        return id;
    }

    public async Task<IGeoControlPointModel?> UpdateControlPointAsync(IGeoControlPointModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            const string sql = @"
                UPDATE GeoControlPoints SET
                    CustomMapId = @CustomMapId, PixelX = @PixelX, PixelY = @PixelY,
                    Latitude = @Latitude, Longitude = @Longitude,
                    AccuracyMeters = @AccuracyMeters, Description = @Description
                WHERE Id = @Id;";

            int affected = await conn.ExecuteAsync(sql, new
            {
                model.Id,
                model.CustomMapId,
                model.PixelX,
                model.PixelY,
                model.Latitude,
                model.Longitude,
                model.AccuracyMeters,
                model.Description
            });

            if (affected == 0)
                throw new KeyNotFoundException($"ControlPoint not found. Id={model.Id}");

            _log?.Info($"ControlPoint 업데이트 완료 - Id={model.Id}");
            return model;
        }
        catch (Exception ex)
        {
            _log?.Error($"ControlPoint 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteControlPointAsync(IGeoControlPointModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            const string sql = "DELETE FROM GeoControlPoints WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteControlPointAsync 완료 - Id={model.Id}"
                : $"DeleteControlPointAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"ControlPoint 삭제 실패: {ex.Message}");
            throw;
        }
    }
    #endregion
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool IsConnected => _conn != null && _conn.State == ConnectionState.Open;

    #endregion
    #region - Attributes -
    private ILogService? _log;
    private IEventAggregator? _eventAggregator;
    private GMapDbSetupModel _setup;
    private MapProvider _mapProvider;
    private CustomMapProvider _customMapProvider;
    private DefinedMapProvider _definedMapProvider;
    private CancellationTokenSource? _cancellationTokenSource;

    // GeoControlPointProvider 제거! 
    private MySqlConnection? _conn;
    private readonly SemaphoreSlim _processGate = new(1, 1);
    #endregion
}

#region - DTO Classes (SQL 매핑용) -
/// <summary>
/// Maps 테이블 DTO
/// </summary>
internal sealed class MapSQL
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProviderType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? CoordinateSystem { get; set; }
    public string? EpsgCode { get; set; }
    public decimal? MinLatitude { get; set; }
    public decimal? MaxLatitude { get; set; }
    public decimal? MinLongitude { get; set; }
    public decimal? MaxLongitude { get; set; }
    public int MinZoomLevel { get; set; }
    public int MaxZoomLevel { get; set; }
    public int TileSize { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// CustomMaps 테이블 DTO
/// </summary>
internal sealed class CustomMapSQL
{
    public int MapId { get; set; }
    public string SourceImagePath { get; set; } = string.Empty;
    public string TilesDirectoryPath { get; set; } = string.Empty;
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public long OriginalFileSize { get; set; }
    public int TotalTileCount { get; set; }
    public long TilesDirectorySize { get; set; }
    public decimal? PixelResolutionX { get; set; }
    public decimal? PixelResolutionY { get; set; }
    public string? ResolutionUnit { get; set; }
    public string GeoReferenceMethod { get; set; } = string.Empty;
    public string? GeoTransformMatrix { get; set; }
    public int? ControlPointCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int? ProcessingTimeMinutes { get; set; }
    public decimal? QualityScore { get; set; }

    public CustomMapModel ToDomain(MapSQL mapSql) => new()
    {
        Id = mapSql.Id,
        Name = mapSql.Name,
        Description = mapSql.Description,
        Category = Enum.Parse<EnumMapCategory>(mapSql.Category),
        DataType = Enum.Parse<EnumMapData>(mapSql.DataType),
        CoordinateSystem = mapSql.CoordinateSystem,
        EpsgCode = mapSql.EpsgCode,
        MinLatitude = (double?)mapSql.MinLatitude,
        MaxLatitude = (double?)mapSql.MaxLatitude,
        MinLongitude = (double?)mapSql.MinLongitude,
        MaxLongitude = (double?)mapSql.MaxLongitude,
        MinZoomLevel = mapSql.MinZoomLevel,
        MaxZoomLevel = mapSql.MaxZoomLevel,
        TileSize = mapSql.TileSize,
        Status = Enum.Parse<EnumMapStatus>(mapSql.Status),
        CreatedAt = mapSql.CreatedAt,
        UpdatedAt = mapSql.UpdatedAt,
        CreatedBy = mapSql.CreatedBy,
        SourceImagePath = SourceImagePath,
        TilesDirectoryPath = TilesDirectoryPath,
        OriginalWidth = OriginalWidth,
        OriginalHeight = OriginalHeight,
        OriginalFileSize = OriginalFileSize,
        TotalTileCount = TotalTileCount,
        TilesDirectorySize = TilesDirectorySize,
        PixelResolutionX = (double?)PixelResolutionX,
        PixelResolutionY = (double?)PixelResolutionY,
        ResolutionUnit = ResolutionUnit,
        GeoReferenceMethod = Enum.Parse<EnumGeoReference>(GeoReferenceMethod),
        GeoTransformMatrix = GeoTransformMatrix,
        ControlPointCount = ControlPointCount,
        ProcessedAt = ProcessedAt,
        ProcessingTimeMinutes = ProcessingTimeMinutes,
        QualityScore = (double?)QualityScore,
        ControlPoints = new List<IGeoControlPointModel>() // 별도로 로드
    };
}

/// <summary>
/// DefinedMaps 테이블 DTO
/// </summary>
internal sealed class DefinedMapSQL
{
    public int MapId { get; set; }
    public string GMapProviderName { get; set; } = string.Empty;
    public string? ProviderGuid { get; set; }
    public string Vendor { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public bool RequiresApiKey { get; set; }
    public string? ApiKey { get; set; }
    public string? ServiceUrl { get; set; }
    public int? DailyRequestLimit { get; set; }
    public string? LicenseInfo { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int? TodayUsageCount { get; set; }

    public DefinedMapModel ToDomain(MapSQL mapSql) => new()
    {
        Id = mapSql.Id,
        Name = mapSql.Name,
        Description = mapSql.Description,
        Category = Enum.Parse<EnumMapCategory>(mapSql.Category),
        DataType = Enum.Parse<EnumMapData>(mapSql.DataType),
        CoordinateSystem = mapSql.CoordinateSystem,
        EpsgCode = mapSql.EpsgCode,
        MinLatitude = (double?)mapSql.MinLatitude,
        MaxLatitude = (double?)mapSql.MaxLatitude,
        MinLongitude = (double?)mapSql.MinLongitude,
        MaxLongitude = (double?)mapSql.MaxLongitude,
        MinZoomLevel = mapSql.MinZoomLevel,
        MaxZoomLevel = mapSql.MaxZoomLevel,
        TileSize = mapSql.TileSize,
        Status = Enum.Parse<EnumMapStatus>(mapSql.Status),
        CreatedAt = mapSql.CreatedAt,
        UpdatedAt = mapSql.UpdatedAt,
        CreatedBy = mapSql.CreatedBy,
        GMapProviderName = GMapProviderName,
        ProviderGuid = ProviderGuid,
        Vendor = Enum.Parse<EnumMapVendor>(Vendor),
        Style = Enum.Parse<EnumMapStyle>(Style),
        RequiresApiKey = RequiresApiKey,
        ApiKey = ApiKey,
        ServiceUrl = ServiceUrl,
        DailyRequestLimit = DailyRequestLimit,
        LicenseInfo = LicenseInfo,
        LastAccessedAt = LastAccessedAt,
        TodayUsageCount = TodayUsageCount
    };
}

/// <summary>
/// GeoControlPoints 테이블 DTO
/// </summary>
internal sealed class GeoControlPointSQL
{
    public int Id { get; set; }
    public int CustomMapId { get; set; }
    public decimal PixelX { get; set; }
    public decimal PixelY { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? AccuracyMeters { get; set; }
    public string? Description { get; set; }

    public GeoControlPointModel ToDomain() => new()
    {
        Id = Id,
        CustomMapId = CustomMapId,
        PixelX = (double)PixelX,
        PixelY = (double)PixelY,
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        AccuracyMeters = (double?)AccuracyMeters,
        Description = Description
    };
}
#endregion