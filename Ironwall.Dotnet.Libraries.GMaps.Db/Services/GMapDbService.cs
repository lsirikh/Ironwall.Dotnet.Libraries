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
using System.IO;
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
        catch (IOException)
        {
            // 앱 종료 시 소켓 선종료 후 MySQL close 패킷 전송 실패 — 정상 패턴, 무시
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
                    `MbtilesPath`           VARCHAR(500) NULL,
                    `StorageType`           VARCHAR(20) NULL DEFAULT 'PngDirectory',
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

            const string createMapRoisSql = @"
                CREATE TABLE IF NOT EXISTS `MapRois` (
                    `Id`          INT AUTO_INCREMENT PRIMARY KEY,
                    `Title`       VARCHAR(100) NOT NULL,
                    `Latitude`    DECIMAL(10,8) NOT NULL,
                    `Longitude`   DECIMAL(11,8) NOT NULL,
                    `Altitude`    DECIMAL(10,2) DEFAULT 0,
                    `Zoom`        INT DEFAULT 15,
                    `MapId`       INT NOT NULL,
                    `CreatedAt`   DATETIME DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt`   DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    CONSTRAINT `FK_MapRois_Maps`
                        FOREIGN KEY (`MapId`) REFERENCES `Maps` (`Id`)
                        ON DELETE CASCADE,
                    INDEX `IX_MapRois_MapId` (`MapId`)
                );";

            await _conn.ExecuteAsync(createMapRoisSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "MapRois 테이블 생성…" });

            const string createMapLayersSql = @"
                CREATE TABLE IF NOT EXISTS `MapLayers` (
                    `Id`          INT AUTO_INCREMENT PRIMARY KEY,
                    `Name`        VARCHAR(100) NOT NULL,
                    `LayerType`   VARCHAR(20) NOT NULL,
                    `Category`    VARCHAR(50),
                    `IsVisible`   BOOLEAN DEFAULT TRUE,
                    `Opacity`     DECIMAL(3,2) DEFAULT 1.0,
                    `ZOrder`      INT DEFAULT 0,
                    `MapId`       INT,
                    `FilePath`    VARCHAR(500),
                    `CreatedAt`   DATETIME DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt`   DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    INDEX `IX_MapLayers_Type` (`LayerType`),
                    INDEX `IX_MapLayers_Category` (`Category`)
                );";

            await _conn.ExecuteAsync(createMapLayersSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "MapLayers 테이블 생성…" });

            // 카메라별 RTSP 팝업 위치(Geo 앵커) — 다중 클라이언트 공유, CameraId 단독 PK
            const string createCameraPopupPositionsSql = @"
                CREATE TABLE IF NOT EXISTS `CameraPopupPositions` (
                    `CameraId`    INT PRIMARY KEY,
                    `Latitude`    DECIMAL(10,8) NOT NULL,
                    `Longitude`   DECIMAL(11,8) NOT NULL,
                    `UpdatedAt`   DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                );";

            await _conn.ExecuteAsync(createCameraPopupPositionsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "CameraPopupPositions 테이블 생성…" });

            // 카메라 팝업 제어 허브 위치(화면 좌표, 단일 행 Id=1 고정) — CameraPopup_ControlHub. 드래그 이동 위치 기억.
            const string createCameraPopupHubPositionSql = @"
                CREATE TABLE IF NOT EXISTS `CameraPopupHubPosition` (
                    `Id`        INT PRIMARY KEY,
                    `X`         DOUBLE NOT NULL,
                    `Y`         DOUBLE NOT NULL,
                    `UpdatedAt` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                );";

            await _conn.ExecuteAsync(createCameraPopupHubPositionSql);

            // 카메라 PTZ 프리셋(로컬 DB) — pan/tilt/zoom + space URI, (CameraId, PresetName) 유니크
            const string createCameraPtzPresetsSql = @"
                CREATE TABLE IF NOT EXISTS `CameraPtzPresets` (
                    `Id`           INT AUTO_INCREMENT PRIMARY KEY,
                    `CameraId`     INT NOT NULL,
                    `PresetName`   VARCHAR(100) NOT NULL,
                    `IsHome`       TINYINT(1) NOT NULL DEFAULT 0,
                    `Pan`          DOUBLE NOT NULL,
                    `Tilt`         DOUBLE NOT NULL,
                    `Zoom`         DOUBLE NOT NULL DEFAULT 0,
                    `PanTiltSpace` VARCHAR(255) NULL,
                    `ZoomSpace`    VARCHAR(255) NULL,
                    `UpdatedAt`    DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY `uq_camera_preset` (`CameraId`, `PresetName`)
                );";

            await _conn.ExecuteAsync(createCameraPtzPresetsSql);

            // 추적 좌표 영속(로컬 자체 DB, 2026-06-25 결정) — NATS 수신분 기록 + Playback 시간범위 조회
            const string createCameraTrackPointsSql = @"
                CREATE TABLE IF NOT EXISTS `CameraTrackPoints` (
                    `Id`           INT AUTO_INCREMENT PRIMARY KEY,
                    `CameraId`     INT NOT NULL,
                    `TrackId`      VARCHAR(64) NOT NULL,
                    `Label`        VARCHAR(32) NULL,
                    `ThreatLevel`  VARCHAR(16) NULL,
                    `Latitude`     DECIMAL(10,8) NOT NULL,
                    `Longitude`    DECIMAL(11,8) NOT NULL,
                    `DistanceM`    DOUBLE NULL,
                    `SpeedMps`     DOUBLE NULL,
                    `ObservedAt`   DATETIME(3) NOT NULL,
                    `CreatedAt`    DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UNIQUE KEY `ux_track_dedup` (`CameraId`, `TrackId`, `ObservedAt`),  -- 재전송/중복 메시지 dedup
                    INDEX `ix_track_camera_obs` (`CameraId`, `ObservedAt`),
                    INDEX `ix_track_track_obs` (`TrackId`, `ObservedAt`)
                );";
            await _conn.ExecuteAsync(createCameraTrackPointsSql);

            // 기존 테이블(UNIQUE 없던 버전) 1회 마이그레이션 — 중복 제거 후 UNIQUE 추가(best-effort, 비치명)
            try
            {
                var hasUx = await _conn.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM information_schema.STATISTICS
                     WHERE table_schema = DATABASE() AND table_name = 'CameraTrackPoints'
                       AND index_name = 'ux_track_dedup';");
                if (hasUx == 0)
                {
                    await _conn.ExecuteAsync(@"
                        DELETE t1 FROM `CameraTrackPoints` t1
                          JOIN `CameraTrackPoints` t2
                            ON t1.`CameraId`=t2.`CameraId` AND t1.`TrackId`=t2.`TrackId`
                           AND t1.`ObservedAt`=t2.`ObservedAt` AND t1.`Id` > t2.`Id`;");
                    await _conn.ExecuteAsync(
                        "ALTER TABLE `CameraTrackPoints` ADD UNIQUE KEY `ux_track_dedup` (`CameraId`,`TrackId`,`ObservedAt`);");
                }
            }
            catch (Exception ex) { _log?.Warning($"[GMapDb] CameraTrackPoints dedup 마이그레이션 건너뜀: {ex.Message}"); }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "CameraPtzPresets/CameraTrackPoints 테이블 생성…" });

            // MBTiles 컬럼 마이그레이션 (기존 DB에 컬럼이 없으면 추가)
            await MigrateCustomMapsTableAsync();

            _log?.Info("Map 관련 테이블 생성/확인 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"DB 스키마 생성 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>기존 CustomMaps 테이블에 MBTiles 관련 컬럼 추가 (idempotent)</summary>
    private async Task MigrateCustomMapsTableAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            var columns = (await conn.QueryAsync<string>(
                "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='CustomMaps' AND TABLE_SCHEMA=DATABASE();"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!columns.Contains("MbtilesPath"))
                await conn.ExecuteAsync("ALTER TABLE CustomMaps ADD COLUMN MbtilesPath VARCHAR(500) NULL;");

            if (!columns.Contains("StorageType"))
                await conn.ExecuteAsync("ALTER TABLE CustomMaps ADD COLUMN StorageType VARCHAR(20) NULL DEFAULT 'PngDirectory';");
        }
        catch (Exception ex)
        {
            _log?.Warning($"CustomMaps 마이그레이션 경고 (무시 가능): {ex.Message}");
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
                        c.QualityScore, c.MbtilesPath, c.StorageType
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
                        c.QualityScore, c.MbtilesPath, c.StorageType
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
                 ProcessedAt, ProcessingTimeMinutes, QualityScore, MbtilesPath, StorageType)
                VALUES (@MapId, @SourceImagePath, @TilesDirectoryPath, @OriginalWidth, @OriginalHeight,
                        @OriginalFileSize, @TotalTileCount, @TilesDirectorySize, @PixelResolutionX, @PixelResolutionY,
                        @ResolutionUnit, @GeoReferenceMethod, @GeoTransformMatrix, @ControlPointCount,
                        @ProcessedAt, @ProcessingTimeMinutes, @QualityScore, @MbtilesPath, @StorageType);";

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
                model.QualityScore,
                model.MbtilesPath,
                StorageType = model.StorageType.ToString()
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
                    ProcessingTimeMinutes = @ProcessingTimeMinutes, QualityScore = @QualityScore,
                    MbtilesPath = @MbtilesPath, StorageType = @StorageType
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
                model.QualityScore,
                model.MbtilesPath,
                StorageType = model.StorageType.ToString()
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

    public async Task UpdateDefinedMapMetadataAsync(
        int mapId,
        double minLat, double maxLat,
        double minLng, double maxLng,
        int minZoom, int maxZoom,
        CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                UPDATE Maps SET
                    MinLatitude = @MinLat, MaxLatitude = @MaxLat,
                    MinLongitude = @MinLng, MaxLongitude = @MaxLng,
                    MinZoomLevel = @MinZoom, MaxZoomLevel = @MaxZoom,
                    UpdatedAt = NOW()
                WHERE Id = @MapId;";

            await conn.ExecuteAsync(sql, new { MapId = mapId, MinLat = minLat, MaxLat = maxLat, MinLng = minLng, MaxLng = maxLng, MinZoom = minZoom, MaxZoom = maxZoom });
            _log?.Info($"UpdateDefinedMapMetadataAsync 완료 - MapId={mapId}, Zoom={minZoom}~{maxZoom}");
        }
        catch (Exception ex)
        {
            _log?.Error($"DefinedMap 메타데이터 업데이트 실패: {ex.Message}");
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

    #region - MapRoi CRUD -

    public async Task<List<IMapRoiModel>?> FetchMapRoisAsync(int mapId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT Id, Title, Latitude, Longitude, Altitude, Zoom, MapId, CreatedAt, UpdatedAt
                FROM MapRois
                WHERE MapId = @MapId
                ORDER BY Id ASC;";

            var rows = await conn.QueryAsync<MapRoiSQL>(sql, new { MapId = mapId });

            return rows.Select(r => (IMapRoiModel)new MapRoiModel
            {
                Id = r.Id,
                Title = r.Title,
                Latitude = (double)r.Latitude,
                Longitude = (double)r.Longitude,
                Altitude = (double)r.Altitude,
                Zoom = r.Zoom,
                MapId = r.MapId,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"MapRoi 조회 실패 (MapId={mapId}): {ex.Message}");
            throw;
        }
    }

    public async Task<IMapRoiModel?> FetchMapRoiAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT Id, Title, Latitude, Longitude, Altitude, Zoom, MapId, CreatedAt, UpdatedAt
                FROM MapRois
                WHERE Id = @Id;";

            var r = await conn.QueryFirstOrDefaultAsync<MapRoiSQL>(sql, new { Id = id });
            if (r == null) return null;

            return new MapRoiModel
            {
                Id = r.Id,
                Title = r.Title,
                Latitude = (double)r.Latitude,
                Longitude = (double)r.Longitude,
                Altitude = (double)r.Altitude,
                Zoom = r.Zoom,
                MapId = r.MapId,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"MapRoi 단건 조회 실패 (Id={id}): {ex.Message}");
            throw;
        }
    }

    public async Task<int> InsertMapRoiAsync(IMapRoiModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                INSERT INTO MapRois (Title, Latitude, Longitude, Altitude, Zoom, MapId)
                VALUES (@Title, @Latitude, @Longitude, @Altitude, @Zoom, @MapId);
                SELECT LAST_INSERT_ID();";

            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                model.Title,
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.MapId
            });

            model.Id = id;
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error($"MapRoi 삽입 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> UpdateMapRoiTitleAsync(int id, string title, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = "UPDATE MapRois SET Title = @Title WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = id, Title = title });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"MapRoi Title 변경 실패 (Id={id}): {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteMapRoiAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = "DELETE FROM MapRois WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = id });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"MapRoi 삭제 실패 (Id={id}): {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - CameraPopupPosition CRUD (카메라 RTSP 팝업 위치) -

    /// <summary>카메라 팝업 위치를 Upsert(없으면 INSERT, 있으면 UPDATE). UpdatedAt는 컬럼 기본값으로 자동 갱신.</summary>
    public async Task<bool> UpsertCameraPopupPositionAsync(ICameraPopupPositionModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                INSERT INTO CameraPopupPositions (CameraId, Latitude, Longitude)
                VALUES (@CameraId, @Latitude, @Longitude)
                ON DUPLICATE KEY UPDATE Latitude = @Latitude, Longitude = @Longitude;";

            int ret = await conn.ExecuteAsync(sql, new
            {
                model.CameraId,
                model.Latitude,
                model.Longitude
            });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"카메라 팝업 위치 Upsert 실패 (CameraId={model.CameraId}): {ex.Message}");
            throw;
        }
    }

    /// <summary>카메라 Id로 저장된 팝업 위치를 조회. 없으면 null.</summary>
    public async Task<ICameraPopupPositionModel?> GetCameraPopupPositionAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT CameraId, Latitude, Longitude, UpdatedAt
                FROM CameraPopupPositions
                WHERE CameraId = @CameraId;";

            var r = await conn.QueryFirstOrDefaultAsync<CameraPopupPositionSQL>(sql, new { CameraId = cameraId });
            if (r == null) return null;

            return new CameraPopupPositionModel
            {
                CameraId = r.CameraId,
                Latitude = (double)r.Latitude,
                Longitude = (double)r.Longitude,
                UpdatedAt = r.UpdatedAt
            };
        }
        catch (Exception ex)
        {
            _log?.Error($"카메라 팝업 위치 조회 실패 (CameraId={cameraId}): {ex.Message}");
            throw;
        }
    }

    /// <summary>모든 카메라 팝업 위치를 조회(앱 시작 시 일괄 로드용).</summary>
    public async Task<List<ICameraPopupPositionModel>?> FetchCameraPopupPositionsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT CameraId, Latitude, Longitude, UpdatedAt
                FROM CameraPopupPositions;";

            var rows = await conn.QueryAsync<CameraPopupPositionSQL>(sql);
            return rows.Select(r => (ICameraPopupPositionModel)new CameraPopupPositionModel
            {
                CameraId = r.CameraId,
                Latitude = (double)r.Latitude,
                Longitude = (double)r.Longitude,
                UpdatedAt = r.UpdatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"카메라 팝업 위치 전체 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>카메라 Id로 저장된 팝업 위치를 삭제.</summary>
    public async Task<bool> DeleteCameraPopupPositionAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = "DELETE FROM CameraPopupPositions WHERE CameraId = @CameraId;";
            int ret = await conn.ExecuteAsync(sql, new { CameraId = cameraId });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"카메라 팝업 위치 삭제 실패 (CameraId={cameraId}): {ex.Message}");
            throw;
        }
    }

    /*────────────────────── CameraPopupHubPosition (제어 허브 위치, 단일 행 Id=1) ──────────*/

    /// <summary>제어 허브 화면 좌표(X,Y)를 Upsert(단일 행 Id=1). 드래그 종료 시 호출. (CameraPopup_ControlHub FR-08)</summary>
    public async Task UpsertCameraPopupHubPositionAsync(double x, double y, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                INSERT INTO CameraPopupHubPosition (Id, X, Y) VALUES (1, @X, @Y)
                ON DUPLICATE KEY UPDATE X = @X, Y = @Y;";
            await conn.ExecuteAsync(sql, new { X = x, Y = y });
        }
        catch (Exception ex)
        {
            _log?.Error($"허브 위치 Upsert 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>저장된 제어 허브 화면 좌표 조회. 없으면 null(→ 기본 우하단 도킹). (CameraPopup_ControlHub FR-08)</summary>
    public async Task<CameraPopupHubPositionDto?> GetCameraPopupHubPositionAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = "SELECT X, Y FROM CameraPopupHubPosition WHERE Id = 1;";
            return await conn.QueryFirstOrDefaultAsync<CameraPopupHubPositionDto>(sql);
        }
        catch (Exception ex)
        {
            _log?.Error($"허브 위치 조회 실패: {ex.Message}");
            throw;
        }
    }

    /*────────────────────── CameraTrackPoints (추적 좌표 영속, 로컬 DB) ──────────*/

    /// <summary>추적 좌표 일괄 저장(수신 batch). 반환=삽입 행수.</summary>
    public async Task<int> InsertTrackPointsAsync(IEnumerable<ITrackPointModel> points, CancellationToken token = default)
    {
        try
        {
            var rows = points?.ToList() ?? new List<ITrackPointModel>();
            if (rows.Count == 0) return 0;
            await using var conn = await OpenConnectionAsync(token);
            // INSERT IGNORE — ux_track_dedup(CameraId,TrackId,ObservedAt) 중복은 조용히 skip(재전송 dedup)
            const string sql = @"
                INSERT IGNORE INTO CameraTrackPoints (CameraId, TrackId, Label, ThreatLevel, Latitude, Longitude, DistanceM, SpeedMps, ObservedAt)
                VALUES (@CameraId, @TrackId, @Label, @ThreatLevel, @Latitude, @Longitude, @DistanceM, @SpeedMps, @ObservedAt);";
            return await conn.ExecuteAsync(sql, rows.Select(p => new
            {
                p.CameraId, p.TrackId, p.Label, p.ThreatLevel, p.Latitude, p.Longitude, p.DistanceM, p.SpeedMps, p.ObservedAt
            }));
        }
        catch (Exception ex)
        {
            _log?.Error($"추적 좌표 저장 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>시간범위 추적 좌표 조회(observed_at ASC). cameraId null이면 전체 카메라.</summary>
    public async Task<List<ITrackPointModel>?> FetchTrackPointsAsync(int? cameraId, DateTime fromUtc, DateTime toUtc, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                SELECT Id, CameraId, TrackId, Label, ThreatLevel, Latitude, Longitude, DistanceM, SpeedMps, ObservedAt
                FROM CameraTrackPoints
                WHERE ObservedAt >= @From AND ObservedAt <= @To
                  AND (@CameraId IS NULL OR CameraId = @CameraId)
                ORDER BY ObservedAt ASC;";
            var rows = await conn.QueryAsync<TrackPointSQL>(sql, new { CameraId = cameraId, From = fromUtc, To = toUtc });
            return rows.Select(MapTrackPoint).ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"추적 좌표 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>보존정책 — 기준 시각 이전 좌표 삭제. 반환=삭제 행수.</summary>
    public async Task<int> DeleteTrackPointsBeforeAsync(DateTime cutoffUtc, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            return await conn.ExecuteAsync("DELETE FROM CameraTrackPoints WHERE ObservedAt < @Cutoff;", new { Cutoff = cutoffUtc });
        }
        catch (Exception ex)
        {
            _log?.Error($"추적 좌표 보존삭제 실패: {ex.Message}");
            throw;
        }
    }

    private sealed class TrackPointSQL
    {
        public int Id { get; set; }
        public int CameraId { get; set; }
        public string TrackId { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? ThreatLevel { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? DistanceM { get; set; }
        public double? SpeedMps { get; set; }
        public DateTime ObservedAt { get; set; }
    }

    private static ITrackPointModel MapTrackPoint(TrackPointSQL r) => new TrackPointModel
    {
        Id = r.Id,
        CameraId = r.CameraId,
        TrackId = r.TrackId,
        Label = r.Label,
        ThreatLevel = r.ThreatLevel,
        Latitude = r.Latitude,
        Longitude = r.Longitude,
        DistanceM = r.DistanceM,
        SpeedMps = r.SpeedMps,
        ObservedAt = r.ObservedAt,
    };

    /*────────────────────── CameraPtzPresets (PTZ 프리셋, 로컬 DB) ──────────────*/

    /// <summary>카메라의 PTZ 프리셋 목록 조회(Home 우선, 이름 순).</summary>
    public async Task<List<IPtzPresetModel>?> FetchPtzPresetsAsync(int cameraId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                SELECT Id, CameraId, PresetName, IsHome, Pan, Tilt, Zoom, PanTiltSpace, ZoomSpace, UpdatedAt
                FROM CameraPtzPresets
                WHERE CameraId = @CameraId
                ORDER BY IsHome DESC, PresetName ASC;";
            var rows = await conn.QueryAsync<PtzPresetSQL>(sql, new { CameraId = cameraId });
            return rows.Select(MapPtzPreset).ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"PTZ 프리셋 조회 실패 (CameraId={cameraId}): {ex.Message}");
            throw;
        }
    }

    /// <summary>PTZ 프리셋 Upsert((CameraId, PresetName) 유니크 → 동일 이름 덮어쓰기). IsHome은 SetHome으로 별도 관리.</summary>
    public async Task<bool> UpsertPtzPresetAsync(IPtzPresetModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                INSERT INTO CameraPtzPresets (CameraId, PresetName, IsHome, Pan, Tilt, Zoom, PanTiltSpace, ZoomSpace)
                VALUES (@CameraId, @PresetName, @IsHome, @Pan, @Tilt, @Zoom, @PanTiltSpace, @ZoomSpace)
                ON DUPLICATE KEY UPDATE
                    Pan = @Pan, Tilt = @Tilt, Zoom = @Zoom,
                    PanTiltSpace = @PanTiltSpace, ZoomSpace = @ZoomSpace;";
            int ret = await conn.ExecuteAsync(sql, new
            {
                model.CameraId, model.PresetName, model.IsHome,
                model.Pan, model.Tilt, model.Zoom, model.PanTiltSpace, model.ZoomSpace
            });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PTZ 프리셋 저장 실패 (CameraId={model.CameraId}, {model.PresetName}): {ex.Message}");
            throw;
        }
    }

    /// <summary>PTZ 프리셋 삭제(Id).</summary>
    public async Task<bool> DeletePtzPresetAsync(int presetId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = "DELETE FROM CameraPtzPresets WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = presetId });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PTZ 프리셋 삭제 실패 (Id={presetId}): {ex.Message}");
            throw;
        }
    }

    /// <summary>카메라의 Home 프리셋을 지정(presetId만 IsHome=1, 나머지 0 — 단일 SQL CASE, 카메라당 1개 보장).</summary>
    public async Task<bool> SetHomePtzPresetAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                UPDATE CameraPtzPresets
                SET IsHome = CASE WHEN Id = @PresetId THEN 1 ELSE 0 END
                WHERE CameraId = @CameraId;";
            int ret = await conn.ExecuteAsync(sql, new { CameraId = cameraId, PresetId = presetId });
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PTZ Home 설정 실패 (CameraId={cameraId}, PresetId={presetId}): {ex.Message}");
            throw;
        }
    }

    private static IPtzPresetModel MapPtzPreset(PtzPresetSQL r) => new PtzPresetModel
    {
        Id = r.Id,
        CameraId = r.CameraId,
        PresetName = r.PresetName,
        IsHome = r.IsHome,
        Pan = r.Pan,
        Tilt = r.Tilt,
        Zoom = r.Zoom,
        PanTiltSpace = r.PanTiltSpace,
        ZoomSpace = r.ZoomSpace,
        UpdatedAt = r.UpdatedAt
    };

    #endregion

    #region - MapLayer CRUD -

    public async Task<List<IMapLayerModel>?> FetchMapLayersAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = "SELECT * FROM MapLayers ORDER BY LayerType, ZOrder ASC, Id ASC;";
            var rows = await conn.QueryAsync<MapLayerSQL>(sql);
            return rows.Select(r => (IMapLayerModel)new MapLayerModel
            {
                Id = r.Id, Name = r.Name, LayerType = r.LayerType, Category = r.Category,
                IsVisible = r.IsVisible, Opacity = (double)r.Opacity, ZOrder = r.ZOrder,
                MapId = r.MapId, FilePath = r.FilePath, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt
            }).ToList();
        }
        catch (Exception ex) { _log?.Error($"MapLayer 조회 실패: {ex.Message}"); throw; }
    }

    public async Task<int> InsertMapLayerAsync(IMapLayerModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"
                INSERT INTO MapLayers (Name, LayerType, Category, IsVisible, Opacity, ZOrder, MapId, FilePath)
                VALUES (@Name, @LayerType, @Category, @IsVisible, @Opacity, @ZOrder, @MapId, @FilePath);
                SELECT LAST_INSERT_ID();";
            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                model.Name, model.LayerType, model.Category, model.IsVisible,
                model.Opacity, model.ZOrder, model.MapId, model.FilePath
            });
            model.Id = id;
            return id;
        }
        catch (Exception ex) { _log?.Error($"MapLayer 삽입 실패: {ex.Message}"); throw; }
    }

    public async Task<bool> UpdateMapLayerAsync(IMapLayerModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = @"UPDATE MapLayers
                                 SET Name = @Name, IsVisible = @IsVisible, Opacity = @Opacity,
                                     ZOrder = @ZOrder, FilePath = @FilePath
                                 WHERE Id = @Id;";
            return await conn.ExecuteAsync(sql, new
            {
                model.Id, model.Name, model.IsVisible,
                model.Opacity, model.ZOrder, model.FilePath
            }) > 0;
        }
        catch (Exception ex) { _log?.Error($"MapLayer 업데이트 실패 (Id={model.Id}): {ex.Message}"); throw; }
    }

    public async Task<bool> UpdateMapLayerVisibilityAsync(int id, bool isVisible, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = "UPDATE MapLayers SET IsVisible = @IsVisible WHERE Id = @Id;";
            return await conn.ExecuteAsync(sql, new { Id = id, IsVisible = isVisible }) > 0;
        }
        catch (Exception ex) { _log?.Error($"MapLayer Visibility 변경 실패 (Id={id}): {ex.Message}"); throw; }
    }

    public async Task<bool> UpdateMapLayerOpacityAsync(int id, double opacity, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = "UPDATE MapLayers SET Opacity = @Opacity WHERE Id = @Id;";
            return await conn.ExecuteAsync(sql, new { Id = id, Opacity = opacity }) > 0;
        }
        catch (Exception ex) { _log?.Error($"MapLayer Opacity 변경 실패 (Id={id}): {ex.Message}"); throw; }
    }

    public async Task<bool> DeleteMapLayerAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            return await conn.ExecuteAsync("DELETE FROM MapLayers WHERE Id = @Id;", new { Id = id }) > 0;
        }
        catch (Exception ex) { _log?.Error($"MapLayer 삭제 실패 (Id={id}): {ex.Message}"); throw; }
    }

    public async Task SeedDefaultSymbolLayersAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            var defaults = new[]
            {
                ("카메라",       "PidsCamera",     50),
                ("센서",         "PidsSensor",     50),
                ("스피커",       "PidsSpeaker",    50),
                ("컨트롤러",     "PidsController", 50),
                ("조명",         "PidsLamp",       50),
                ("함체",         "PidsEnclosure",  50),
                ("PIDS 그룹",   "PidsGroup",      40),
                ("군사부호",     "Military",       30),
                ("기하학 도형",  "Geometric",      20),
                ("선/경계",      "Line",           20),
                ("인프라",       "Infra",          20),
                ("핀(단일)",     "Basic",          20),
            };

            const string checkSql = "SELECT COUNT(*) FROM MapLayers WHERE LayerType = 'Symbol' AND Category = @Category;";
            const string insertSql = @"INSERT INTO MapLayers (Name, LayerType, Category, IsVisible, Opacity, ZOrder)
                                       VALUES (@Name, 'Symbol', @Category, TRUE, 1.0, @ZOrder);";

            int added = 0;
            foreach (var (name, category, zorder) in defaults)
            {
                var count = await conn.ExecuteScalarAsync<int>(checkSql, new { Category = category });
                if (count == 0)
                {
                    await conn.ExecuteAsync(insertSql, new { Name = name, Category = category, ZOrder = zorder });
                    added++;
                }
            }

            if (added > 0)
                _log?.Info($"기본 심볼 레이어 {added}개 추가 완료");
        }
        catch (Exception ex) { _log?.Error($"기본 레이어 생성 실패: {ex.Message}"); throw; }
    }

    public async Task<int> GetNextZOrderAsync(string layerType, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            const string sql = "SELECT COALESCE(MAX(ZOrder), 0) + 1 FROM MapLayers WHERE LayerType = @LayerType;";
            return await conn.ExecuteScalarAsync<int>(sql, new { LayerType = layerType });
        }
        catch (Exception ex) { _log?.Error($"GetNextZOrder 실패: {ex.Message}"); return 1; }
    }

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
    public string? MbtilesPath { get; set; }
    public string? StorageType { get; set; }

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
        MbtilesPath = MbtilesPath,
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

/// <summary>
/// MapRois 테이블 DTO
/// </summary>
internal sealed class MapRoiSQL
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Altitude { get; set; }
    public int Zoom { get; set; }
    public int MapId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>CameraPopupPositions 테이블 DTO</summary>
internal sealed class CameraPopupPositionSQL
{
    public int CameraId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>CameraPopupHubPosition 조회 결과(제어 허브 화면 좌표). 인터페이스 반환용 public.</summary>
public sealed class CameraPopupHubPositionDto
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>CameraPtzPresets 테이블 DTO</summary>
internal sealed class PtzPresetSQL
{
    public int Id { get; set; }
    public int CameraId { get; set; }
    public string PresetName { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public double Pan { get; set; }
    public double Tilt { get; set; }
    public double Zoom { get; set; }
    public string? PanTiltSpace { get; set; }
    public string? ZoomSpace { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

internal sealed class MapLayerSQL
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LayerType { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsVisible { get; set; }
    public decimal Opacity { get; set; }
    public int ZOrder { get; set; }
    public int? MapId { get; set; }
    public string? FilePath { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
#endregion