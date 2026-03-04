using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using MySql.Data.MySqlClient;
using System;
using System.Buffers;
using System.Data;
using System.Security.Cryptography;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/18/2025 7:28:04 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class GMapDbSymbolService : TaskService, IGMapDbSymbolService
{
    #region - Ctors -
    /// <summary>
    /// GMapDbSymbolService 생성자
    /// </summary>
    /// <param name="log">로깅 서비스</param>
    /// <param name="eventAggregator">이벤트 집계자 (UI 메시지용)</param>
    /// <param name="symbolProvider">심볼 데이터 제공자</param>
    /// <param name="setupModel">데이터베이스 설정 모델</param>
    public GMapDbSymbolService(ILogService log,
                               IEventAggregator eventAggregator,
                               SymbolProvider symbolProvider,
                               GeometricSymbolProvider geometrySymbolProvider,
                               PidsSymbolProvider pidsSymbolProvider,
                               MilitarySymbolProvider militarySymbolProvider,
                               LineSymbolProvider lineSymbolProvider,
                               InfraSymbolProvider infraSymbolProvider,
                               PidsGroupSymbolProvider pidsGroupSymbolProvider,
                               GMapDbSetupModel setupModel)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _symbolProvider = symbolProvider;
        _geometrySymbolProvider = geometrySymbolProvider;
        _pidsSymbolProvider = pidsSymbolProvider;
        _militarySymbolProvider = militarySymbolProvider;
        _lineSymbolProvider = lineSymbolProvider;
        _infraSymbolProvider = infraSymbolProvider;
        _pidsGroupSymbolProvider = pidsGroupSymbolProvider;
        _setup = setupModel;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    /// <summary>
    /// TaskService의 RunTask 오버라이드 - 서비스 시작
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    protected override async Task RunTask(CancellationToken token = default)
    {
        await StartService(token);
    }

    /// <summary>
    /// TaskService의 ExitTask 오버라이드 - 서비스 종료
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    protected override async Task ExitTask(CancellationToken token = default)
    {
        await StopService(token);
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// Symbol DB 서비스 시작
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>성공 여부</returns>
    /// <exception cref="Exception">서비스 시작 실패 시</exception>
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
            _log?.Error($"SymbolDbService 시작 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Symbol DB 서비스 중지
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>성공 여부</returns>
    /// <exception cref="Exception">서비스 중지 실패 시</exception>
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
            _log?.Error($"SymbolDbService 중지 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// MySQL 연결 문자열 생성
    /// </summary>
    /// <param name="includeDb">데이터베이스명 포함 여부 (기본값: true)</param>
    /// <returns>MySQL 연결 문자열</returns>
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

    /// <summary>
    /// 데이터베이스 연결 설정 및 데이터베이스 생성
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    /// <exception cref="Exception">DB 연결 실패 시</exception>
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

            var msg = $"Symbol DB 연결 성공: {_setup.IpDbServer}:{_setup.PortDbServer}/{dbName}";
            _log?.Info(msg);

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage()
                { Title = nameof(GMapDbSymbolService), Message = msg });
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol DB 연결 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 새로운 MySQL 연결 인스턴스 생성 및 열기
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>열린 MySqlConnection 인스턴스</returns>
    /// <exception cref="Exception">연결 생성 실패 시</exception>
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

    /// <summary>
    /// 데이터베이스 연결 해제
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    public async Task Disconnect(CancellationToken token = default)
    {
        try
        {
            if (_conn != null && _conn.State == ConnectionState.Open)
            {
                await _conn.CloseAsync();
                _conn.Dispose();
                _log?.Info("Symbol DB 연결 종료 완료");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol DB 연결 종료 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// Symbol 관련 데이터베이스 스키마 생성
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    /// <exception cref="Exception">스키마 생성 실패 시</exception>
    /// <remarks>
    /// Symbols 테이블을 생성하며, 다음 인덱스를 포함합니다:
    /// - IX_Symbols_Pid: Pid 기반 조회 최적화
    /// - IX_Symbols_Category: 카테고리별 조회 최적화
    /// - IX_Symbols_State: 상태별 조회 최적화
    /// - IX_Symbols_Location: 위치 기반 조회 최적화 (Latitude, Longitude)
    /// </remarks>
    public async Task BuildSchemeAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            // ── Symbols 테이블 ──
            var createSymbolsSql = @"
                CREATE TABLE IF NOT EXISTS `Symbols` (
                    `Id`                INT AUTO_INCREMENT PRIMARY KEY,
                    `Pid`               INT NOT NULL DEFAULT 0,
                    `Title`             VARCHAR(200) NOT NULL,                          -- 심볼제목
                    `TitleSize`         DECIMAL(4,2) DEFAULT 10.0,                      -- 제목크기
                    `OperationState`    VARCHAR(20) NOT NULL DEFAULT 'NONE',            -- 운영상태
                    `Latitude`          DECIMAL(10,8) NOT NULL,                         -- 위도
                    `Longitude`         DECIMAL(11,8) NOT NULL,                         -- 경도
                    `Altitude`          FLOAT DEFAULT 0,                                -- 고도
                    `Zoom`              DECIMAL(3, 1) DEFAULT 17,                       -- 디스플레이용(Zoom)
                    `Bearing`           DECIMAL(6,3) DEFAULT 0,                         -- 심볼각도
                    `Width`             DECIMAL(8,3) DEFAULT 30,                        -- 넓이
                    `Height`            DECIMAL(8,3) DEFAULT 30,                        -- 크기
                    `Category`          VARCHAR(30) NOT NULL DEFAULT 'BASIC_SHAPES',    -- 심볼 카테고리
                    `ShowShape`         BOOLEAN DEFAULT TRUE,                           -- 심볼 Visibility
                    `ShowTitle`         BOOLEAN DEFAULT FALSE,                          -- 타이틀 Visibility
                    `FillColor`         VARCHAR(20) NOT NULL DEFAULT 'Blue',            -- 내부 색상
                    `StrokeColor`       VARCHAR(20) NOT NULL DEFAULT 'White',           -- 외부 선 라인 색상
                    `StrokeThickness`   DECIMAL(4,2) DEFAULT 1.0,                       -- 외부 선 라인 두께
                    `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                    `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    `CreatedBy`         VARCHAR(100),
                    INDEX `IX_Symbols_Pid` (`Pid`),
                    INDEX `IX_Symbols_Category` (`Category`),
                    INDEX `IX_Symbols_State` (`OperationState`),
                    INDEX `IX_Symbols_Location` (`Latitude`, `Longitude`),
                    INDEX `IX_Symbols_Colors` (`FillColor`, `StrokeColor`)
                );";

            // ── GeometrySymbols 테이블  ──
            var createGeometrySymbolsSql = @"
            CREATE TABLE IF NOT EXISTS `GeometrySymbols` (
                `SymbolId`          INT PRIMARY KEY,
                `ShapeType`         VARCHAR(20) NOT NULL DEFAULT 'Circle',              -- Geometric 타입
                `Opacity`           DECIMAL(3,2) DEFAULT 1.0,
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_GeometrySymbols_Symbols`
                    FOREIGN KEY (`SymbolId`) REFERENCES `Symbols` (`Id`)
                    ON DELETE CASCADE,
                INDEX `IX_GeometrySymbols_ShapeType` (`ShapeType`)
            );";

            // ── PidsSymbols 테이블 ──
            var createPidsSymbolsSql = @"
            CREATE TABLE IF NOT EXISTS `PidsSymbols` (
                `SymbolId`          INT PRIMARY KEY,
                `LinkedDeviceId`    INT NOT NULL DEFAULT 0,
                `DeviceType`        VARCHAR(20) NOT NULL DEFAULT 'Fence',
                `ShowFOV`           BOOLEAN DEFAULT FALSE,
                `FOVColor`          VARCHAR(20) NOT NULL DEFAULT 'Red',
                `FOVOpacity`        DECIMAL(3,2) DEFAULT 0.3,
                `EventStatus`       VARCHAR(20) NOT NULL DEFAULT 'Normal',
                `BaseBearing`       DECIMAL(5,2) DEFAULT 0.0,
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_PidsSymbols_Symbols`
                    FOREIGN KEY (`SymbolId`) REFERENCES `Symbols` (`Id`)
                    ON DELETE CASCADE,
                INDEX `IX_PidsSymbols_DeviceType` (`DeviceType`),
                INDEX `IX_PidsSymbols_LinkedDeviceId` (`LinkedDeviceId`),
                INDEX `IX_PidsSymbols_EventStatus` (`EventStatus`)
            );";

            // ── MilitarySymbols 테이블 ──
            var createMilitarySymbolsSql = @"
            CREATE TABLE IF NOT EXISTS `MilitarySymbols` (
                `SymbolId`          INT PRIMARY KEY,
                `Affiliation`       VARCHAR(20) NOT NULL DEFAULT 'Friend',              -- 소속구분 (Friend, Hostile, Neutral, Unknown)
                `BattleDimension`   VARCHAR(20) NOT NULL DEFAULT 'Land',                -- 전투차원 (Land, Sea, Air, Subsurface)
                `StandardIdentity`  VARCHAR(20) NOT NULL DEFAULT 'Present',             -- 표준정체성 (Present, Planned, Anticipated)
                `UnitType`          VARCHAR(50) NOT NULL DEFAULT 'Artillery',            -- 부대종류 (Infantry, Artillery, Armour 등)
                `UnitSize`          VARCHAR(20) NOT NULL DEFAULT 'Company',             -- 부대규모 (Squad, Platoon, Company 등)
                `UnitDesignator`    VARCHAR(100),                                       -- 부대지시자 (부대명)
                `HigherFormation`   VARCHAR(100),                                       -- 상급부대명
                `CallSign`          VARCHAR(50),                                        -- 콜사인
                `CountryCode`       VARCHAR(10),                                        -- 국가코드 (KR, US 등)
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_MilitarySymbols_Symbols`
                    FOREIGN KEY (`SymbolId`) REFERENCES `Symbols` (`Id`)
                    ON DELETE CASCADE,
                INDEX `IX_MilitarySymbols_Affiliation` (`Affiliation`),
                INDEX `IX_MilitarySymbols_BattleDimension` (`BattleDimension`),
                INDEX `IX_MilitarySymbols_UnitType` (`UnitType`),
                INDEX `IX_MilitarySymbols_UnitSize` (`UnitSize`),
                INDEX `IX_MilitarySymbols_StandardIdentity` (`StandardIdentity`)
            );";

            // ── LineSymbols 테이블 ──
            var createLineSymbolsSql = @"
            CREATE TABLE IF NOT EXISTS `LineSymbols` (
                `SymbolId`          INT PRIMARY KEY,
                `LineOpacity`       DECIMAL(3,2) DEFAULT 1.0,
                `IsClosedPath`      BOOLEAN DEFAULT FALSE,
                `ShowArrowHead`     BOOLEAN DEFAULT FALSE,
                `LinePattern`       VARCHAR(20) NOT NULL DEFAULT 'Solid',
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_LineSymbols_Symbols`
                    FOREIGN KEY (`SymbolId`) REFERENCES `Symbols` (`Id`)
                    ON DELETE CASCADE,
                INDEX `IX_LineSymbols_LinePattern` (`LinePattern`)
            );";

            // ── LinePoints 테이블 ──
            var createLinePointsSql = @"
            CREATE TABLE IF NOT EXISTS `LinePoints` (
                `Id`                INT AUTO_INCREMENT PRIMARY KEY,
                `LineSymbolId`      INT NOT NULL,
                `SequenceOrder`     INT NOT NULL,
                `Latitude`          DECIMAL(10,8) NOT NULL,
                `Longitude`         DECIMAL(11,8) NOT NULL,
                `Altitude`          FLOAT DEFAULT 0,
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_LinePoints_LineSymbols`
                    FOREIGN KEY (`LineSymbolId`) REFERENCES `LineSymbols` (`SymbolId`)
                    ON DELETE CASCADE,
                INDEX `IX_LinePoints_LineSymbolId` (`LineSymbolId`),
                INDEX `IX_LinePoints_Sequence` (`LineSymbolId`, `SequenceOrder`),
                UNIQUE KEY `UQ_LinePoints_Symbol_Sequence` (`LineSymbolId`, `SequenceOrder`)
            );";

            // ── InfraSymbols 테이블 ──
            var createInfraSymbolsSql = @"
            CREATE TABLE IF NOT EXISTS `InfraSymbols` (
                `SymbolId`          INT PRIMARY KEY,
                `BuildingType`      VARCHAR(20) NOT NULL DEFAULT 'Factory',
                `BuildingUsage`     VARCHAR(20) NOT NULL DEFAULT 'Office',
                `FloorCount`        INT NOT NULL DEFAULT 1,
                `BasementFloorCount` INT NOT NULL DEFAULT 0,
                `BuildingArea`      DECIMAL(10,2) DEFAULT 100.0,
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_InfraSymbols_Symbols`
                    FOREIGN KEY (`SymbolId`) REFERENCES `Symbols` (`Id`)
                    ON DELETE CASCADE
            );";

            // ── PidsGroupSymbols 테이블 ──
            var createPidsGroupSymbolsSql = @"
            CREATE TABLE IF NOT EXISTS `PidsGroupSymbols` (
                `SymbolId`          INT PRIMARY KEY,
                `LinkedDeviceGroup` INT NOT NULL DEFAULT 0,                         -- 연결된 디바이스 그룹 ID
                `EventStatus`       VARCHAR(20) NOT NULL DEFAULT 'Normal',          -- 이벤트 상태 (Normal, Detecting, Fault, Connection)
                `LineOpacity`       DECIMAL(3,2) DEFAULT 1.0,                       -- 라인 투명도
                `IsClosedPath`      BOOLEAN DEFAULT TRUE,                           -- 닫힌 경로 여부 (그룹은 대부분 닫힌 경로)
                `ShowArrowHead`     BOOLEAN DEFAULT FALSE,                          -- 화살표 표시 여부
                `LinePattern`       VARCHAR(20) NOT NULL DEFAULT 'Solid',           -- 라인 패턴 (Solid, Dash, Dot 등)
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_PidsGroupSymbols_Symbols`
                    FOREIGN KEY (`SymbolId`) REFERENCES `Symbols` (`Id`)
                    ON DELETE CASCADE,
                INDEX `IX_PidsGroupSymbols_DeviceGroup` (`LinkedDeviceGroup`),
                INDEX `IX_PidsGroupSymbols_EventStatus` (`EventStatus`)
            );";

            // ── PidsGroupPoints 테이블 (라인 포인트 저장용) ──
            var createPidsGroupPointsSql = @"
            CREATE TABLE IF NOT EXISTS `PidsGroupPoints` (
                `Id`                INT AUTO_INCREMENT PRIMARY KEY,
                `GroupSymbolId`     INT NOT NULL,
                `SequenceOrder`     INT NOT NULL,
                `Latitude`          DECIMAL(10,8) NOT NULL,
                `Longitude`         DECIMAL(11,8) NOT NULL,
                `Altitude`          FLOAT DEFAULT 0,
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_PidsGroupPoints_PidsGroupSymbols`
                    FOREIGN KEY (`GroupSymbolId`) REFERENCES `PidsGroupSymbols` (`SymbolId`)
                    ON DELETE CASCADE,
                INDEX `IX_PidsGroupPoints_GroupSymbolId` (`GroupSymbolId`),
                INDEX `IX_PidsGroupPoints_Sequence` (`GroupSymbolId`, `SequenceOrder`),
                UNIQUE KEY `UQ_PidsGroupPoints_Symbol_Sequence` (`GroupSymbolId`, `SequenceOrder`)
            );";

            // ── Images 테이블 ──
            var createImagesSql = @"
            CREATE TABLE IF NOT EXISTS `Images` (
                `Id`                INT AUTO_INCREMENT PRIMARY KEY,
                `Title`             VARCHAR(200),                                       -- 이미지 제목
                `FilePath`          VARCHAR(500) NOT NULL,                              -- 이미지 파일 경로
                `Left`              DECIMAL(11,8) NOT NULL DEFAULT 0,                   -- 좌측 경도 (Left Longitude)
                `Top`               DECIMAL(10,8) NOT NULL DEFAULT 0,                   -- 상단 위도 (Top Latitude)
                `Right`             DECIMAL(11,8) NOT NULL DEFAULT 0,                   -- 우측 경도 (Right Longitude)
                `Bottom`            DECIMAL(10,8) NOT NULL DEFAULT 0,                   -- 하단 위도 (Bottom Latitude)
                `Latitude`          DECIMAL(10,8) NOT NULL DEFAULT 0,                   -- 중심 위도
                `Longitude`         DECIMAL(11,8) NOT NULL DEFAULT 0,                   -- 중심 경도
                `Altitude`          FLOAT DEFAULT 0,                                    -- 고도
                `Zoom`              DECIMAL(3,1) DEFAULT 0,                             -- 표시 줌 레벨 (0 = 모든 줌 레벨)
                `Width`             DECIMAL(10,3) DEFAULT 0,                            -- 이미지 너비 (픽셀)
                `Height`            DECIMAL(10,3) DEFAULT 0,                            -- 이미지 높이 (픽셀)
                `Opacity`           DECIMAL(3,2) DEFAULT 1.0,                           -- 투명도 (0.0 ~ 1.0)
                `Rotation`          DECIMAL(6,3) DEFAULT 0,                             -- 회전 각도 (도)
                `Visibility`        BOOLEAN DEFAULT TRUE,                               -- 표시 여부
                `HasGeoReference`   BOOLEAN DEFAULT FALSE,                              -- 지리 참조 여부
                `CoordinateSystem`  VARCHAR(50),                                        -- 좌표계 (예: EPSG:4326)
                `CreatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`         DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX `IX_Images_Location` (`Latitude`, `Longitude`),
                INDEX `IX_Images_Bounds` (`Left`, `Top`, `Right`, `Bottom`)
            );";

            // 실행 순서
            await conn.ExecuteAsync(createSymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "Symbols 테이블 생성…" });

            await conn.ExecuteAsync(createGeometrySymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "GeometrySymbols 테이블 생성…" });

            await conn.ExecuteAsync(createPidsSymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "PidsSymbols 테이블 생성…" });

            // BuildSchemeAsync 메서드에서 실행
            await conn.ExecuteAsync(createMilitarySymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "MilitarySymbols 테이블 생성…" });

            await conn.ExecuteAsync(createLineSymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "LineSymbols 테이블 생성…" });

            await conn.ExecuteAsync(createLinePointsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "LinePoints 테이블 생성…" });

            await conn.ExecuteAsync(createInfraSymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "InfraSymbols 테이블 생성…" });

            await conn.ExecuteAsync(createPidsGroupSymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "PidsGroupSymbols 테이블 생성…" });

            await conn.ExecuteAsync(createPidsGroupPointsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "PidsGroupPoints 테이블 생성…" });

            await conn.ExecuteAsync(createImagesSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "Images 테이블 생성…" });

            _log?.Info("Symbol 관련 테이블 생성/확인 완료");
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol DB 스키마 생성 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 데이터베이스에서 모든 Symbol 인스턴스를 로드하여 SymbolProvider에 저장
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Task</returns>
    /// <exception cref="Exception">데이터 로드 실패 시</exception>
    /// <remarks>
    /// 동시 실행 방지를 위해 SemaphoreSlim을 사용하며,
    /// 기존 CancellationToken을 취소하고 새로운 토큰을 생성합니다.
    /// </remarks>
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

            // 1. 일반 Symbols 로드 (GeometrySymbols 제외)
            var symbols = await FetchSymbolsAsync(token: token);

            // 2. GeometrySymbols 로드
            var geometrySymbols = await FetchGeometrySymbolsAsync(token: token);

            // 3. PidsSymbols 로드 (새로 추가)
            var pidsSymbols = await FetchPidsSymbolsAsync(token: token);

            // 4. MilitarySymbols 로드 (새로 추가)
            var militarySymbols = await FetchMilitarySymbolsAsync(token: token);

            // 5. LineSymbol 로드 (새로 추가)
            var lineSymbols = await FetchLineSymbolsAsync(token: token);

            // 6. InfraSymbol 로드 (새로 추가)
            var infraSymbols = await FetchInfraSymbolsAsync(token: token);

            // 7. PidsGroupSymbol 로드 (새로 추가)
            var pidsGroupSymbols = await FetchPidsGroupSymbolsAsync(token: token);

            _symbolProvider.Clear();

            // 3. 일반 심볼 추가 (BASIC_SHAPES가 아닌 것만)
            if (symbols?.Any() == true)
            {
                foreach (var symbol in symbols)
                {
                    //if(symbol.Category == EnumMarkerCategory.BASIC_SHAPES)
                    //    _symbolProvider.Add(symbol);
                    _symbolProvider.Add(symbol);
                }
            }

            _geometrySymbolProvider.Clear();
            // 4. 기하 심볼 추가 (GeometrySymbolModel로)
            if (geometrySymbols?.Any() == true)
            {
                foreach (var geometrySymbol in geometrySymbols)
                {
                    _symbolProvider.Add(geometrySymbol);
                }
            }

            _pidsSymbolProvider.Clear();
            if (pidsSymbols?.Any() == true)
            {
                foreach (var pidsSymbol in pidsSymbols)
                {
                    _symbolProvider.Add(pidsSymbol);
                }
            }

            _militarySymbolProvider.Clear();
            if (militarySymbols?.Any() == true)
            {
                foreach (var militarySymbol in militarySymbols)
                {
                    _symbolProvider.Add(militarySymbol);
                }
            }

            _lineSymbolProvider.Clear();
            if (lineSymbols?.Any() == true)
            {
                foreach (var lineSymbol in lineSymbols)
                {
                    _symbolProvider.Add(lineSymbol);
                }
            }

            _infraSymbolProvider.Clear();
            if (infraSymbols?.Any() == true)
            {
                foreach (var infraSymbol in infraSymbols)
                {
                    _symbolProvider.Add(infraSymbol);
                }
            }

            _pidsGroupSymbolProvider.Clear();
            if (pidsGroupSymbols?.Any() == true)
            {
                foreach (var pidsGroupSymbol in pidsGroupSymbols)
                {
                    _symbolProvider.Add(pidsGroupSymbol);
                }
            }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage()
                { Title = nameof(GMapDbSymbolService), Message = "Symbol 정보를 모두 불러왔습니다..." });

        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol 데이터 로드 실패: {ex.Message}");
            throw;
        }
        finally
        {
            if (gateEntered)
                _processGate.Release();
        }
    }

    #region - Symbol CRUD -
    public async Task<List<ISymbolModel>?> FetchSymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT  Id, Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, 
                        Bearing, Width, Height, Category, ShowShape, ShowTitle, 
                        FillColor, StrokeColor, StrokeThickness,
                        CreatedAt, UpdatedAt, CreatedBy
                FROM    Symbols
                WHERE   Category = 'BASIC_SHAPES'
                ORDER BY CreatedAt DESC;";

            var list = (await conn.QueryAsync<SymbolSQL>(sql))
                .Select(s => s.ToDomain())
                .ToList();

            _log?.Info($"FetchSymbolsAsync 완료 - {list.Count}건");
            return list.OfType<ISymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<ISymbolModel?> FetchSymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
                SELECT  Id, Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom,
                        Bearing, Width, Height, Category, ShowShape, ShowTitle, 
                        FillColor, StrokeColor, StrokeThickness,
                        CreatedAt, UpdatedAt, CreatedBy
                FROM    Symbols
                WHERE   Id = @Id;";

            var symbolDto = await conn.QuerySingleOrDefaultAsync<SymbolSQL>(sql, new { Id = id });
            var symbol = symbolDto?.ToDomain();

            _log?.Info(symbol != null
                ? $"FetchSymbolAsync 완료 - Id={symbol.Id}"
                : $"FetchSymbolAsync 대상 없음 - Id={id}");

            return symbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<ISymbolModel?> FetchSymbolByPidAsync(int pid, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (pid <= 0)
                throw new ArgumentOutOfRangeException(nameof(pid));

            const string sql = @"
                SELECT  Id, Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, 
                        Bearing, Width, Height, Category, ShowShape, ShowTitle, 
                        FillColor, StrokeColor, StrokeThickness,
                        CreatedAt, UpdatedAt, CreatedBy
                FROM    Symbols
                WHERE   Pid = @Pid;";

            var symbolDto = await conn.QuerySingleOrDefaultAsync<SymbolSQL>(sql, new { Pid = pid });
            var symbol = symbolDto?.ToDomain();

            _log?.Info(symbol != null
                ? $"FetchSymbolByPidAsync 완료 - Pid={symbol.Pid}"
                : $"FetchSymbolByPidAsync 대상 없음 - Pid={pid}");

            return symbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol Pid 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<List<ISymbolModel>?> FetchSymbolsByCategoryAsync(EnumMarkerCategory category, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT  Id, Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, 
                        Bearing, Width, Height, Category, ShowShape, ShowTitle, 
                        FillColor, StrokeColor, StrokeThickness,
                        CreatedAt, UpdatedAt, CreatedBy
                FROM    Symbols
                WHERE   Category = @Category
                ORDER BY CreatedAt DESC;";

            var list = (await conn.QueryAsync<SymbolSQL>(sql, new { Category = category.ToString() }))
                .Select(s => s.ToDomain())
                .ToList();

            _log?.Info($"FetchSymbolsByCategoryAsync 완료 - Category={category}, {list.Count}건");
            return list.OfType<ISymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbols 카테고리별 조회 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<int> InsertSymbolAsync(ISymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                INSERT INTO Symbols
                    (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
                     Width, Height, Category, ShowShape, ShowTitle, 
                     FillColor, StrokeColor, StrokeThickness, CreatedBy)
                    VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                            @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                            @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
                    SELECT LAST_INSERT_ID();";

            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,  //추가
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System" // 또는 현재 사용자 정보
            });

            model.Id = id;
            _log?.Info($"Symbol 삽입 완료 - Id={id}, Title={model.Title}");
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<ISymbolModel?> UpdateSymbolAsync(ISymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            const string sql = @"
               UPDATE Symbols SET
                Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
                Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
                Bearing = @Bearing, Width = @Width, Height = @Height,
                Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
                FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
                CreatedBy = @CreatedBy
               WHERE Id = @Id;";

            var affected = await conn.ExecuteAsync(sql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,  //추가
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System" // 또는 현재 사용자 정보
            });

            if (affected == 0)
                throw new KeyNotFoundException($"Symbol not found. Id={model.Id}");

            _log?.Info($"Symbol 업데이트 완료 - Id={model.Id}");
            return await FetchSymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteSymbolAsync(ISymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteSymbolAsync 완료 - Id={model.Id}"
                : $"DeleteSymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteSymbolByPidAsync(int pid, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (pid <= 0) throw new ArgumentException(nameof(pid));

            const string sql = "DELETE FROM Symbols WHERE Pid = @Pid;";
            int ret = await conn.ExecuteAsync(sql, new { Pid = pid });

            _log?.Info(ret > 0
                ? $"DeleteSymbolByPidAsync 완료 - Pid={pid}"
                : $"DeleteSymbolByPidAsync 대상 없음 - Pid={pid}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol Pid 삭제 실패: {ex.Message}");
            throw;
        }
    }

    public async Task<int> DeleteSymbolsByCategoryAsync(EnumMarkerCategory category, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = "DELETE FROM Symbols WHERE Category = @Category;";
            int ret = await conn.ExecuteAsync(sql, new { Category = category.ToString() });

            _log?.Info($"DeleteSymbolsByCategoryAsync 완료 - Category={category}, {ret}건 삭제");
            return ret;
        }
        catch (Exception ex)
        {
            _log?.Error($"Symbol 카테고리별 삭제 실패: {ex.Message}");
            throw;
        }
    }
    #endregion

    #region - GeometrySymbol CRUD -

    /// <summary>
    /// 모든 기하 심볼 조회 (JOIN 쿼리) - 간소화
    /// </summary>
    public async Task<List<IGeometricSymbolModel>?> FetchGeometrySymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
            SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                    s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                    s.FillColor, s.StrokeColor, s.StrokeThickness,
                    s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                    g.ShapeType as GeometryShapeType, g.Opacity as GeometryOpacity
            FROM    Symbols s
            INNER JOIN GeometrySymbols g ON s.Id = g.SymbolId
            WHERE   s.Category = 'GEOMETRICS'
            ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<GeometrySymbolSQL>(sql))
                .Select(j => j.ToGeometryDomain())
                .ToList();

            _log?.Info($"FetchGeometrySymbolsAsync 완료 - {list.Count}건");
            return list.OfType<IGeometricSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"GeometrySymbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 단일 기하 심볼 조회 - 간소화
    /// </summary>
    public async Task<IGeometricSymbolModel?> FetchGeometrySymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
            SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                    s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                    s.FillColor, s.StrokeColor, s.StrokeThickness,
                    s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                    g.ShapeType as GeometryShapeType, g.Opacity as GeometryOpacity
            FROM    Symbols s
            INNER JOIN GeometrySymbols g ON s.Id = g.SymbolId
            WHERE   s.Id = @Id;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<GeometrySymbolSQL>(sql, new { Id = id });
            var geometrySymbol = joinResult?.ToGeometryDomain();

            _log?.Info(geometrySymbol != null
                ? $"FetchGeometrySymbolAsync 완료 - Id={geometrySymbol.Id}, ShapeType={geometrySymbol.ShapeType}"
                : $"FetchGeometrySymbolAsync 대상 없음 - Id={id}");

            return geometrySymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"GeometrySymbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 기하 심볼 삽입 (트랜잭션 사용) - 간소화
    /// </summary>
    public async Task<int> InsertGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Symbols 테이블에 기본 정보 삽입
            const string symbolSql = @"
            INSERT INTO Symbols
            (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
             Width, Height, Category, ShowShape, ShowTitle, 
             FillColor, StrokeColor, StrokeThickness, CreatedBy)
            VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                    @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                    @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
            SELECT LAST_INSERT_ID();";

            var symbolId = await conn.ExecuteScalarAsync<int>(symbolSql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,  //추가
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. GeometrySymbols 테이블에 기하 정보 삽입 (간소화)
            const string geometrySql = @"
            INSERT INTO GeometrySymbols (SymbolId, ShapeType, Opacity)
            VALUES (@SymbolId, @ShapeType, @Opacity);";

            await conn.ExecuteAsync(geometrySql, new
            {
                SymbolId = symbolId,
                ShapeType = model.ShapeType.ToString(),
                model.Opacity
            }, transaction);

            await transaction.CommitAsync(token);

            model.Id = symbolId;
            _log?.Info($"GeometrySymbol 삽입 완료 - Id={symbolId}, Title={model.Title}, ShapeType={model.ShapeType}");
            return symbolId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"GeometrySymbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 기하 심볼 업데이트 (트랜잭션 사용) - 간소화
    /// </summary>
    public async Task<IGeometricSymbolModel?> UpdateGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. Symbols 테이블 업데이트
            const string symbolSql = @"
            UPDATE Symbols SET
                Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
                Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
                Bearing = @Bearing, Width = @Width, Height = @Height,
                Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
                FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
                CreatedBy = @CreatedBy
            WHERE Id = @Id;";

            var symbolAffected = await conn.ExecuteAsync(symbolSql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,  //추가
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. GeometrySymbols 테이블 업데이트 (간소화)
            const string geometrySql = @"
            UPDATE GeometrySymbols SET
                ShapeType = @ShapeType, Opacity = @Opacity,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE SymbolId = @SymbolId;";

            var geometryAffected = await conn.ExecuteAsync(geometrySql, new
            {
                SymbolId = model.Id,
                ShapeType = model.ShapeType.ToString(),
                model.Opacity
            }, transaction);

            if (symbolAffected == 0 || geometryAffected == 0)
                throw new KeyNotFoundException($"GeometrySymbol not found. Id={model.Id}");

            await transaction.CommitAsync(token);

            _log?.Info($"GeometrySymbol 업데이트 완료 - Id={model.Id}");
            return await FetchGeometrySymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"GeometrySymbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 기하 심볼 삭제 (CASCADE로 자동 삭제됨)
    /// </summary>
    public async Task<bool> DeleteGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // Symbols만 삭제하면 GeometrySymbols는 CASCADE로 자동 삭제
            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteGeometrySymbolAsync 완료 - Id={model.Id}"
                : $"DeleteGeometrySymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"GeometrySymbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Shape 타입별 기하 심볼 조회 - 간소화
    /// </summary>
    public async Task<List<IGeometricSymbolModel>?> FetchGeometrySymbolsByShapeTypeAsync(EnumShapeType shapeType, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
            SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                    s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                    s.FillColor, s.StrokeColor, s.StrokeThickness,
                    s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                    g.ShapeType as GeometryShapeType, g.Opacity as GeometryOpacity
            FROM    Symbols s
            INNER JOIN GeometrySymbols g ON s.Id = g.SymbolId
            WHERE   g.ShapeType = @ShapeType
            ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<GeometrySymbolSQL>(sql, new { ShapeType = shapeType.ToString() }))
                .Select(j => j.ToGeometryDomain())
                .ToList();

            _log?.Info($"FetchGeometrySymbolsByShapeTypeAsync 완료 - ShapeType={shapeType}, {list.Count}건");
            return list.OfType<IGeometricSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"GeometrySymbols ShapeType별 조회 실패: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - PidsSymbol CRUD -

    /// <summary>
    /// 모든 PIDS 심볼 조회 (JOIN 쿼리)
    /// </summary>
    public async Task<List<IPidsSymbolModel>?> FetchPidsSymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle,
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                p.LinkedDeviceId, p.DeviceType, p.ShowFOV, p.FOVColor, p.FOVOpacity, p.EventStatus, p.BaseBearing
        FROM    Symbols s
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId
        WHERE   s.Category = 'PIDS_EQUIPMENT'
        ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<PidsSymbolSQL>(sql))
                .Select(p => p.ToPidsDomain())
                .ToList();

            _log?.Info($"FetchPidsSymbolsAsync 완료 - {list.Count}건");
            return list.OfType<IPidsSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 단일 PIDS 심볼 조회
    /// </summary>
    public async Task<IPidsSymbolModel?> FetchPidsSymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle,
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                p.LinkedDeviceId, p.DeviceType, p.ShowFOV, p.FOVColor, p.FOVOpacity, p.EventStatus, p.BaseBearing
        FROM    Symbols s
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId
        WHERE   s.Id = @Id;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<PidsSymbolSQL>(sql, new { Id = id });
            var pidsSymbol = joinResult?.ToPidsDomain();

            _log?.Info(pidsSymbol != null
                ? $"FetchPidsSymbolAsync 완료 - Id={pidsSymbol.Id}, DeviceType={pidsSymbol.DeviceType}"
                : $"FetchPidsSymbolAsync 대상 없음 - Id={id}");

            return pidsSymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// LinkedDeviceId로 PIDS 심볼 조회
    /// </summary>
    public async Task<IPidsSymbolModel?> FetchPidsSymbolByDeviceIdAsync(int deviceId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (deviceId <= 0)
                throw new ArgumentOutOfRangeException(nameof(deviceId));

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle,
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                p.LinkedDeviceId, p.DeviceType, p.ShowFOV, p.FOVColor, p.FOVOpacity, p.EventStatus, p.BaseBearing
        FROM    Symbols s
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId
        WHERE   p.LinkedDeviceId = @DeviceId;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<PidsSymbolSQL>(sql, new { DeviceId = deviceId });
            var pidsSymbol = joinResult?.ToPidsDomain();

            _log?.Info(pidsSymbol != null
                ? $"FetchPidsSymbolByDeviceIdAsync 완료 - DeviceId={deviceId}, Id={pidsSymbol.Id}"
                : $"FetchPidsSymbolByDeviceIdAsync 대상 없음 - DeviceId={deviceId}");

            return pidsSymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbol DeviceId 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// DeviceType별 PIDS 심볼 조회
    /// </summary>
    public async Task<List<IPidsSymbolModel>?> FetchPidsSymbolsByDeviceTypeAsync(EnumDeviceType deviceType, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle,
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                p.LinkedDeviceId, p.DeviceType, p.ShowFOV, p.FOVColor, p.FOVOpacity, p.EventStatus, p.BaseBearing
        FROM    Symbols s
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId
        WHERE   p.DeviceType = @DeviceType
        ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<PidsSymbolSQL>(sql, new { DeviceType = deviceType.ToString() }))
                .Select(p => p.ToPidsDomain())
                .ToList();

            _log?.Info($"FetchPidsSymbolsByDeviceTypeAsync 완료 - DeviceType={deviceType}, {list.Count}건");
            return list.OfType<IPidsSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbols DeviceType별 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// PIDS 심볼 삽입 (트랜잭션 사용)
    /// </summary>
    public async Task<int> InsertPidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Symbols 테이블에 기본 정보 삽입
            const string symbolSql = @"
        INSERT INTO Symbols
        (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
         Width, Height, Category, ShowShape, ShowTitle, 
         FillColor, StrokeColor, StrokeThickness, CreatedBy)
        VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
        SELECT LAST_INSERT_ID();";

            var symbolId = await conn.ExecuteScalarAsync<int>(symbolSql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. PidsSymbols 테이블에 PIDS 전용 정보 삽입
            const string pidsSql = @"
        INSERT INTO PidsSymbols (SymbolId, LinkedDeviceId, DeviceType, ShowFOV, FOVColor, FOVOpacity, EventStatus, BaseBearing)
        VALUES (@SymbolId, @LinkedDeviceId, @DeviceType, @ShowFOV, @FOVColor, @FOVOpacity, @EventStatus, @BaseBearing);";

            await conn.ExecuteAsync(pidsSql, new
            {
                SymbolId = symbolId,
                model.LinkedDeviceId,
                DeviceType = model.DeviceType.ToString(),
                model.ShowFOV,
                FOVColor = model.FOVColor.ToString(),
                model.FOVOpacity,
                EventStatus = model.EventStatus.ToString(),
                model.BaseBearing
            }, transaction);

            await transaction.CommitAsync(token);

            model.Id = symbolId;
            _log?.Info($"PidsSymbol 삽입 완료 - Id={symbolId}, Title={model.Title}, DeviceType={model.DeviceType}");
            return symbolId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"PidsSymbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// PIDS 심볼 업데이트 (트랜잭션 사용)
    /// </summary>
    public async Task<IPidsSymbolModel?> UpdatePidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. 먼저 레코드 존재 여부 확인
            const string checkSql = @"
        SELECT COUNT(*) FROM Symbols s 
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId 
        WHERE s.Id = @Id;";

            var exists = await conn.ExecuteScalarAsync<int>(checkSql, new { Id = model.Id }, transaction);
            if (exists == 0)
            {
                _log?.Warning($"PidsSymbol 업데이트 대상 없음: Id={model.Id}");
                return null; // 예외 대신 null 반환
            }

            // 1. Symbols 테이블 업데이트
            const string symbolSql = @"
        UPDATE Symbols SET
            Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
            Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
            Bearing = @Bearing, Width = @Width, Height = @Height,
            Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
            FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
            CreatedBy = @CreatedBy
        WHERE Id = @Id;";

            var symbolAffected = await conn.ExecuteAsync(symbolSql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. PidsSymbols 테이블 업데이트
            const string pidsSql = @"
        UPDATE PidsSymbols SET
            LinkedDeviceId = @LinkedDeviceId, DeviceType = @DeviceType, ShowFOV = @ShowFOV,
            FOVColor = @FOVColor, FOVOpacity = @FOVOpacity, EventStatus = @EventStatus,
            BaseBearing = @BaseBearing,
            UpdatedAt = CURRENT_TIMESTAMP
        WHERE SymbolId = @SymbolId;";

            var pidsAffected = await conn.ExecuteAsync(pidsSql, new
            {
                SymbolId = model.Id,
                model.LinkedDeviceId,
                DeviceType = model.DeviceType.ToString(),
                model.ShowFOV,
                FOVColor = model.FOVColor.ToString(),
                model.FOVOpacity,
                EventStatus = model.EventStatus.ToString(),
                model.BaseBearing
            }, transaction);

            if (symbolAffected == 0 || pidsAffected == 0)
                throw new KeyNotFoundException($"PidsSymbol not found. Id={model.Id}");

            await transaction.CommitAsync(token);

            _log?.Info($"PidsSymbol 업데이트 완료 - Id={model.Id}");
            return await FetchPidsSymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"PidsSymbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// PIDS 심볼 삭제 (CASCADE로 PidsSymbols도 자동 삭제됨)
    /// </summary>
    public async Task<bool> DeletePidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // Symbols만 삭제하면 PidsSymbols는 CASCADE로 자동 삭제
            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeletePidsSymbolAsync 완료 - Id={model.Id}"
                : $"DeletePidsSymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// LinkedDeviceId로 PIDS 심볼 삭제
    /// </summary>
    public async Task<bool> DeletePidsSymbolByDeviceIdAsync(int deviceId, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (deviceId <= 0) throw new ArgumentException(nameof(deviceId));

            const string sql = @"
        DELETE s FROM Symbols s
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId
        WHERE p.LinkedDeviceId = @DeviceId;";

            int ret = await conn.ExecuteAsync(sql, new { DeviceId = deviceId });

            _log?.Info(ret > 0
                ? $"DeletePidsSymbolByDeviceIdAsync 완료 - DeviceId={deviceId}"
                : $"DeletePidsSymbolByDeviceIdAsync 대상 없음 - DeviceId={deviceId}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbol DeviceId 삭제 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// EventStatus별 PIDS 심볼 조회
    /// </summary>
    public async Task<List<IPidsSymbolModel>?> FetchPidsSymbolsByEventStatusAsync(EnumEventStatus eventStatus, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle,
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                p.LinkedDeviceId, p.DeviceType, p.ShowFOV, p.FOVColor, p.FOVOpacity, p.EventStatus, p.BaseBearing
        FROM    Symbols s
        INNER JOIN PidsSymbols p ON s.Id = p.SymbolId
        WHERE   p.EventStatus = @EventStatus
        ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<PidsSymbolSQL>(sql, new { EventStatus = eventStatus.ToString() }))
                .Select(p => p.ToPidsDomain())
                .ToList();

            _log?.Info($"FetchPidsSymbolsByEventStatusAsync 완료 - EventStatus={eventStatus}, {list.Count}건");
            return list.OfType<IPidsSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsSymbols EventStatus별 조회 실패: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - MilitarySymbol CRUD -

    /// <summary>
    /// 모든 군사 심볼 조회 (JOIN 쿼리)
    /// </summary>
    public async Task<List<IMilitarySymbolModel>?> FetchMilitarySymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
            SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                    s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                    s.FillColor, s.StrokeColor, s.StrokeThickness,
                    s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                    m.Affiliation, m.BattleDimension, m.StandardIdentity, m.UnitType, m.UnitSize,
                    m.UnitDesignator, m.HigherFormation, m.CallSign, m.CountryCode
            FROM    Symbols s
            INNER JOIN MilitarySymbols m ON s.Id = m.SymbolId
            WHERE   s.Category = 'MILITARY_SYMBOLS'
            ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<MilitarySymbolSQL>(sql))
                .Select(m => m.ToMilitaryDomain())
                .ToList();

            _log?.Info($"FetchMilitarySymbolsAsync 완료 - {list.Count}건");
            return list.OfType<IMilitarySymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"MilitarySymbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 단일 군사 심볼 조회
    /// </summary>
    public async Task<IMilitarySymbolModel?> FetchMilitarySymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
            SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                    s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                    s.FillColor, s.StrokeColor, s.StrokeThickness,
                    s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                    m.Affiliation, m.BattleDimension, m.StandardIdentity, m.UnitType, m.UnitSize,
                    m.UnitDesignator, m.HigherFormation, m.CallSign, m.CountryCode
            FROM    Symbols s
            INNER JOIN MilitarySymbols m ON s.Id = m.SymbolId
            WHERE   s.Id = @Id;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<MilitarySymbolSQL>(sql, new { Id = id });
            var militarySymbol = joinResult?.ToMilitaryDomain();

            _log?.Info(militarySymbol != null
                ? $"FetchMilitarySymbolAsync 완료 - Id={militarySymbol.Id}, UnitType={militarySymbol.UnitType}"
                : $"FetchMilitarySymbolAsync 대상 없음 - Id={id}");

            return militarySymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"MilitarySymbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 군사 심볼 삽입 (트랜잭션 사용)
    /// </summary>
    public async Task<int> InsertMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Symbols 테이블에 기본 정보 삽입
            const string symbolSql = @"
            INSERT INTO Symbols
            (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
             Width, Height, Category, ShowShape, ShowTitle, 
             FillColor, StrokeColor, StrokeThickness, CreatedBy)
            VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                    @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                    @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
            SELECT LAST_INSERT_ID();";

            var symbolId = await conn.ExecuteScalarAsync<int>(symbolSql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. MilitarySymbols 테이블에 군사 심볼 전용 정보 삽입
            const string militarySql = @"
            INSERT INTO MilitarySymbols 
            (SymbolId, Affiliation, BattleDimension, StandardIdentity, UnitType, UnitSize,
             UnitDesignator, HigherFormation, CallSign, CountryCode)
            VALUES (@SymbolId, @Affiliation, @BattleDimension, @StandardIdentity, @UnitType, @UnitSize,
                    @UnitDesignator, @HigherFormation, @CallSign, @CountryCode);";

            await conn.ExecuteAsync(militarySql, new
            {
                SymbolId = symbolId,
                Affiliation = model.Affiliation.ToString(),
                BattleDimension = model.BattleDimension.ToString(),
                StandardIdentity = model.StandardIdentity.ToString(),
                UnitType = model.UnitType.ToString(),
                UnitSize = model.UnitSize.ToString(),
                model.UnitDesignator,
                model.HigherFormation,
                model.CallSign,
                model.CountryCode
            }, transaction);

            await transaction.CommitAsync(token);

            model.Id = symbolId;
            _log?.Info($"MilitarySymbol 삽입 완료 - Id={symbolId}, Title={model.Title}, UnitType={model.UnitType}");
            return symbolId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"MilitarySymbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 군사 심볼 업데이트 (트랜잭션 사용)
    /// </summary>
    public async Task<IMilitarySymbolModel?> UpdateMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. Symbols 테이블 업데이트
            const string symbolSql = @"
            UPDATE Symbols SET
                Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
                Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
                Bearing = @Bearing, Width = @Width, Height = @Height,
                Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
                FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
                CreatedBy = @CreatedBy
            WHERE Id = @Id;";

            var symbolAffected = await conn.ExecuteAsync(symbolSql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. MilitarySymbols 테이블 업데이트
            const string militarySql = @"
            UPDATE MilitarySymbols SET
                Affiliation = @Affiliation, BattleDimension = @BattleDimension, StandardIdentity = @StandardIdentity,
                UnitType = @UnitType, UnitSize = @UnitSize, UnitDesignator = @UnitDesignator,
                HigherFormation = @HigherFormation, CallSign = @CallSign, CountryCode = @CountryCode,
                UpdatedAt = CURRENT_TIMESTAMP
            WHERE SymbolId = @SymbolId;";

            var militaryAffected = await conn.ExecuteAsync(militarySql, new
            {
                SymbolId = model.Id,
                Affiliation = model.Affiliation.ToString(),
                BattleDimension = model.BattleDimension.ToString(),
                StandardIdentity = model.StandardIdentity.ToString(),
                UnitType = model.UnitType.ToString(),
                UnitSize = model.UnitSize.ToString(),
                model.UnitDesignator,
                model.HigherFormation,
                model.CallSign,
                model.CountryCode
            }, transaction);

            if (symbolAffected == 0 || militaryAffected == 0)
                throw new KeyNotFoundException($"MilitarySymbol not found. Id={model.Id}");

            await transaction.CommitAsync(token);

            _log?.Info($"MilitarySymbol 업데이트 완료 - Id={model.Id}");
            return await FetchMilitarySymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"MilitarySymbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 군사 심볼 삭제 (CASCADE로 MilitarySymbols도 자동 삭제됨)
    /// </summary>
    public async Task<bool> DeleteMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // Symbols만 삭제하면 MilitarySymbols는 CASCADE로 자동 삭제
            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteMilitarySymbolAsync 완료 - Id={model.Id}"
                : $"DeleteMilitarySymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"MilitarySymbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - LineSymbol CRUD -

    /// <summary>
    /// 모든 라인 심볼 조회 (JOIN 쿼리 및 포인트 포함)
    /// </summary>
    public async Task<List<ILineSymbolModel>?> FetchLineSymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                l.LineOpacity, l.IsClosedPath, l.ShowArrowHead, l.LinePattern
        FROM    Symbols s
        INNER JOIN LineSymbols l ON s.Id = l.SymbolId
        WHERE   s.Category = 'AREA_BOUNDARY'
        ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<LineSymbolSQL>(sql))
                .Select(l => l.ToLineDomain())
                .ToList();

            // 포인트 로드
            if (list.Any())
            {
                var symbolIds = list.Select(ls => ls.Id).ToList();
                const string pointsSql = @"
            SELECT LineSymbolId, SequenceOrder, Latitude, Longitude, Altitude
            FROM LinePoints
            WHERE LineSymbolId IN @SymbolIds
            ORDER BY LineSymbolId, SequenceOrder;";

                var allPoints = await conn.QueryAsync<LinePointSQL>(pointsSql, new { SymbolIds = symbolIds });

                var pointsLookup = allPoints
                    .GroupBy(p => p.LineSymbolId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(p => p.SequenceOrder).Select(p => p.ToGeoPoint()).ToList());

                foreach (var lineSymbol in list)
                {
                    if (pointsLookup.TryGetValue(lineSymbol.Id, out var points))
                    {
                        lineSymbol.LinePoints = points;
                    }
                }
            }

            _log?.Info($"FetchLineSymbolsAsync 완료 - {list.Count}건");
            return list.OfType<ILineSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"LineSymbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 단일 라인 심볼 조회
    /// </summary>
    public async Task<ILineSymbolModel?> FetchLineSymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                l.LineOpacity, l.IsClosedPath, l.ShowArrowHead, l.LinePattern
        FROM    Symbols s
        INNER JOIN LineSymbols l ON s.Id = l.SymbolId
        WHERE   s.Id = @Id;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<LineSymbolSQL>(sql, new { Id = id });
            var lineSymbol = joinResult?.ToLineDomain();

            if (lineSymbol != null)
            {
                // 포인트 로드
                const string pointsSql = @"
            SELECT SequenceOrder, Latitude, Longitude, Altitude
            FROM LinePoints
            WHERE LineSymbolId = @LineSymbolId
            ORDER BY SequenceOrder;";

                var points = await conn.QueryAsync<LinePointSQL>(pointsSql, new { LineSymbolId = id });
                lineSymbol.LinePoints = points.Select(p => p.ToGeoPoint()).ToList();
            }

            _log?.Info(lineSymbol != null
                ? $"FetchLineSymbolAsync 완료 - Id={lineSymbol.Id}, Points={lineSymbol.LinePoints.Count}개"
                : $"FetchLineSymbolAsync 대상 없음 - Id={id}");

            return lineSymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"LineSymbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 라인 심볼 삽입 (트랜잭션 사용)
    /// </summary>
    public async Task<int> InsertLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default)
    {
        // null 체크 추가
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Symbols 테이블에 기본 정보 삽입
            const string symbolSql = @"
        INSERT INTO Symbols
        (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
         Width, Height, Category, ShowShape, ShowTitle, 
         FillColor, StrokeColor, StrokeThickness, CreatedBy)
        VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
        SELECT LAST_INSERT_ID();";

            var symbolId = await conn.ExecuteScalarAsync<int>(symbolSql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. LineSymbols 테이블에 라인 심볼 전용 정보 삽입
            const string lineSql = @"
        INSERT INTO LineSymbols 
        (SymbolId, LineOpacity, IsClosedPath, ShowArrowHead, LinePattern)
        VALUES (@SymbolId, @LineOpacity, @IsClosedPath, @ShowArrowHead, @LinePattern);";

            await conn.ExecuteAsync(lineSql, new
            {
                SymbolId = symbolId,
                model.LineOpacity,
                model.IsClosedPath,
                model.ShowArrowHead,
                LinePattern = model.LinePattern.ToString()
            }, transaction);

            // 3. LinePoints 삽입
            if (model.LinePoints?.Any() == true)
            {
                const string pointSql = @"
            INSERT INTO LinePoints (LineSymbolId, SequenceOrder, Latitude, Longitude, Altitude)
            VALUES (@LineSymbolId, @SequenceOrder, @Latitude, @Longitude, @Altitude);";

                var pointParams = model.LinePoints.Select((point, index) => new
                {
                    LineSymbolId = symbolId,
                    SequenceOrder = index,
                    Latitude = (decimal)point.Latitude,
                    Longitude = (decimal)point.Longitude,
                    Altitude = (float)point.Altitude
                });

                await conn.ExecuteAsync(pointSql, pointParams, transaction);
            }

            await transaction.CommitAsync(token);

            model.Id = symbolId;
            _log?.Info($"LineSymbol 삽입 완료 - Id={symbolId}, Title={model.Title}, Points={model.LinePoints?.Count ?? 0}개");
            return symbolId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"LineSymbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 라인 심볼 업데이트 (트랜잭션 사용)
    /// </summary>
    public async Task<ILineSymbolModel?> UpdateLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default)
    {
        // null 체크 추가
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. Symbols 테이블 업데이트
            const string symbolSql = @"
        UPDATE Symbols SET
            Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
            Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
            Bearing = @Bearing, Width = @Width, Height = @Height,
            Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
            FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
            CreatedBy = @CreatedBy
        WHERE Id = @Id;";

            var symbolAffected = await conn.ExecuteAsync(symbolSql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. LineSymbols 테이블 업데이트
            const string lineSql = @"
        UPDATE LineSymbols SET
            LineOpacity = @LineOpacity, IsClosedPath = @IsClosedPath,
            ShowArrowHead = @ShowArrowHead, LinePattern = @LinePattern,
            UpdatedAt = CURRENT_TIMESTAMP
        WHERE SymbolId = @SymbolId;";

            var lineAffected = await conn.ExecuteAsync(lineSql, new
            {
                SymbolId = model.Id,
                model.LineOpacity,
                model.IsClosedPath,
                model.ShowArrowHead,
                LinePattern = model.LinePattern.ToString()
            }, transaction);

            // 3. 기존 포인트 삭제 후 새로 삽입
            const string deletePointsSql = "DELETE FROM LinePoints WHERE LineSymbolId = @LineSymbolId;";
            await conn.ExecuteAsync(deletePointsSql, new { LineSymbolId = model.Id }, transaction);

            if (model.LinePoints?.Any() == true)
            {
                const string insertPointSql = @"
            INSERT INTO LinePoints (LineSymbolId, SequenceOrder, Latitude, Longitude, Altitude)
            VALUES (@LineSymbolId, @SequenceOrder, @Latitude, @Longitude, @Altitude);";

                var pointParams = model.LinePoints.Select((point, index) => new
                {
                    LineSymbolId = model.Id,
                    SequenceOrder = index,
                    Latitude = (decimal)point.Latitude,
                    Longitude = (decimal)point.Longitude,
                    Altitude = (float)point.Altitude
                });

                await conn.ExecuteAsync(insertPointSql, pointParams, transaction);
            }

            if (symbolAffected == 0 || lineAffected == 0)
                throw new KeyNotFoundException($"LineSymbol not found. Id={model.Id}");

            await transaction.CommitAsync(token);

            _log?.Info($"LineSymbol 업데이트 완료 - Id={model.Id}");
            return await FetchLineSymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"LineSymbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 라인 심볼 삭제 (CASCADE로 LineSymbols와 LinePoints도 자동 삭제됨)
    /// </summary>
    public async Task<bool> DeleteLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default)
    {
        // null 체크 추가
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // Symbols만 삭제하면 LineSymbols와 LinePoints는 CASCADE로 자동 삭제
            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteLineSymbolAsync 완료 - Id={model.Id}"
                : $"DeleteLineSymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"LineSymbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - InfraSymbol CRUD -

    /// <summary>
    /// 모든 인프라 심볼 조회 (JOIN 쿼리)
    /// </summary>
    public async Task<List<IInfraSymbolModel>?> FetchInfraSymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                i.BuildingType, i.BuildingUsage, i.FloorCount, i.BasementFloorCount, i.BuildingArea
        FROM    Symbols s
        INNER JOIN InfraSymbols i ON s.Id = i.SymbolId
        WHERE   s.Category = 'INFRASTRUCTURE'
        ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<InfraSymbolSQL>(sql))
                .Select(i => i.ToInfraDomain())
                .ToList();

            _log?.Info($"FetchInfraSymbolsAsync 완료 - {list.Count}건");
            return list.OfType<IInfraSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"InfraSymbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 단일 인프라 심볼 조회
    /// </summary>
    public async Task<IInfraSymbolModel?> FetchInfraSymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
        SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                s.FillColor, s.StrokeColor, s.StrokeThickness,
                s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                i.BuildingType, i.BuildingUsage, i.FloorCount, i.BasementFloorCount, i.BuildingArea
        FROM    Symbols s
        INNER JOIN InfraSymbols i ON s.Id = i.SymbolId
        WHERE   s.Id = @Id;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<InfraSymbolSQL>(sql, new { Id = id });
            var infraSymbol = joinResult?.ToInfraDomain();

            _log?.Info(infraSymbol != null
                ? $"FetchInfraSymbolAsync 완료 - Id={infraSymbol.Id}, BuildingType={infraSymbol.BuildingType}"
                : $"FetchInfraSymbolAsync 대상 없음 - Id={id}");

            return infraSymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"InfraSymbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 인프라 심볼 삽입 (트랜잭션 사용)
    /// </summary>
    public async Task<int> InsertInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Symbols 테이블에 기본 정보 삽입
            const string symbolSql = @"
        INSERT INTO Symbols
        (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
         Width, Height, Category, ShowShape, ShowTitle, 
         FillColor, StrokeColor, StrokeThickness, CreatedBy)
        VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
        SELECT LAST_INSERT_ID();";

            var symbolId = await conn.ExecuteScalarAsync<int>(symbolSql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. InfraSymbols 테이블에 인프라 정보 삽입 (현재는 Factory/Office 고정)
            const string infraSql = @"
        INSERT INTO InfraSymbols 
        (SymbolId, BuildingType, BuildingUsage, FloorCount, BasementFloorCount, BuildingArea)
        VALUES (@SymbolId, @BuildingType, @BuildingUsage, @FloorCount, @BasementFloorCount, @BuildingArea);";

            await conn.ExecuteAsync(infraSql, new
            {
                SymbolId = symbolId,
                BuildingType = model.BuildingType.ToString(),    // Enum → String
                BuildingUsage = model.BuildingUsage.ToString(),  // Enum → String
                model.FloorCount,
                model.BasementFloorCount,
                model.BuildingArea
            }, transaction);

            await transaction.CommitAsync(token);

            model.Id = symbolId;
            _log?.Info($"InfraSymbol 삽입 완료 - Id={symbolId}, Title={model.Title}");
            return symbolId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"InfraSymbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 인프라 심볼 업데이트 (트랜잭션 사용)
    /// </summary>
    public async Task<IInfraSymbolModel?> UpdateInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. 존재 여부 확인
            const string checkSql = @"
        SELECT COUNT(*) FROM Symbols s 
        INNER JOIN InfraSymbols i ON s.Id = i.SymbolId 
        WHERE s.Id = @Id;";

            var exists = await conn.ExecuteScalarAsync<int>(checkSql, new { Id = model.Id }, transaction);
            if (exists == 0)
            {
                _log?.Warning($"InfraSymbol 업데이트 대상 없음: Id={model.Id}");
                return null;
            }

            // 2. Symbols 테이블 업데이트
            const string symbolSql = @"
        UPDATE Symbols SET
            Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
            Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
            Bearing = @Bearing, Width = @Width, Height = @Height,
            Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
            FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
            CreatedBy = @CreatedBy
        WHERE Id = @Id;";

            var symbolAffected = await conn.ExecuteAsync(symbolSql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = model.Category.ToString(),
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 3. InfraSymbols 테이블 업데이트
            const string infraSql = @"
        UPDATE InfraSymbols SET
            BuildingType = @BuildingType, BuildingUsage = @BuildingUsage,
            FloorCount = @FloorCount, BasementFloorCount = @BasementFloorCount,
            BuildingArea = @BuildingArea,
            UpdatedAt = CURRENT_TIMESTAMP
        WHERE SymbolId = @SymbolId;";

            var infraAffected = await conn.ExecuteAsync(infraSql, new
            {
                SymbolId = model.Id,
                BuildingType = model.BuildingType.ToString(),    // Enum → String
                BuildingUsage = model.BuildingUsage.ToString(),  // Enum → String
                model.FloorCount,
                model.BasementFloorCount,
                model.BuildingArea
            }, transaction);

            if (symbolAffected == 0 || infraAffected == 0)
                throw new KeyNotFoundException($"InfraSymbol not found. Id={model.Id}");

            await transaction.CommitAsync(token);

            _log?.Info($"InfraSymbol 업데이트 완료 - Id={model.Id}");
            return await FetchInfraSymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"InfraSymbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 인프라 심볼 삭제 (CASCADE로 InfraSymbols도 자동 삭제됨)
    /// </summary>
    public async Task<bool> DeleteInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // Symbols만 삭제하면 InfraSymbols는 CASCADE로 자동 삭제
            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteInfraSymbolAsync 완료 - Id={model.Id}"
                : $"DeleteInfraSymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"InfraSymbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - PidsGroupSymbol CRUD -

    /// <summary>
    /// 모든 PIDS 그룹 심볼 조회 (JOIN 쿼리 및 포인트 포함)
    /// </summary>
    public async Task<List<IPidsGroupSymbolModel>?> FetchPidsGroupSymbolsAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                        s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                        s.FillColor, s.StrokeColor, s.StrokeThickness,
                        s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                        pg.LinkedDeviceGroup, pg.EventStatus, pg.LineOpacity, pg.IsClosedPath, 
                        pg.ShowArrowHead, pg.LinePattern
                FROM    Symbols s
                INNER JOIN PidsGroupSymbols pg ON s.Id = pg.SymbolId
                WHERE   s.Category = 'PIDS_GROUP'
                ORDER BY s.CreatedAt DESC;";

            var list = (await conn.QueryAsync<PidsGroupSymbolSQL>(sql))
                .Select(pg => pg.ToPidsGroupDomain())
                .ToList();

            // 포인트 로드
            if (list.Any())
            {
                var symbolIds = list.Select(pgs => pgs.Id).ToList();
                const string pointsSql = @"
                    SELECT GroupSymbolId, SequenceOrder, Latitude, Longitude, Altitude
                    FROM PidsGroupPoints
                    WHERE GroupSymbolId IN @SymbolIds
                    ORDER BY GroupSymbolId, SequenceOrder;";

                var allPoints = await conn.QueryAsync<PidsGroupPointSQL>(pointsSql, new { SymbolIds = symbolIds });

                var pointsLookup = allPoints
                    .GroupBy(p => p.GroupSymbolId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(p => p.SequenceOrder).Select(p => p.ToGeoPoint()).ToList());

                foreach (var pidsGroupSymbol in list)
                {
                    if (pointsLookup.TryGetValue(pidsGroupSymbol.Id, out var points))
                    {
                        pidsGroupSymbol.LinePoints = points;
                    }
                }
            }

            _log?.Info($"FetchPidsGroupSymbolsAsync 완료 - {list.Count}건");
            return list.OfType<IPidsGroupSymbolModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsGroupSymbols 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 단일 PIDS 그룹 심볼 조회
    /// </summary>
    public async Task<IPidsGroupSymbolModel?> FetchPidsGroupSymbolAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
                SELECT  s.Id, s.Pid, s.Title, s.TitleSize, s.OperationState, s.Latitude, s.Longitude, s.Altitude, s.Zoom,
                        s.Bearing, s.Width, s.Height, s.Category, s.ShowShape, s.ShowTitle, 
                        s.FillColor, s.StrokeColor, s.StrokeThickness,
                        s.CreatedAt, s.UpdatedAt, s.CreatedBy,
                        pg.LinkedDeviceGroup, pg.EventStatus, pg.LineOpacity, pg.IsClosedPath, 
                        pg.ShowArrowHead, pg.LinePattern
                FROM    Symbols s
                INNER JOIN PidsGroupSymbols pg ON s.Id = pg.SymbolId
                WHERE   s.Id = @Id;";

            var joinResult = await conn.QuerySingleOrDefaultAsync<PidsGroupSymbolSQL>(sql, new { Id = id });
            var pidsGroupSymbol = joinResult?.ToPidsGroupDomain();

            if (pidsGroupSymbol != null)
            {
                // 포인트 로드
                const string pointsSql = @"
            SELECT SequenceOrder, Latitude, Longitude, Altitude
            FROM PidsGroupPoints
            WHERE GroupSymbolId = @GroupSymbolId
            ORDER BY SequenceOrder;";

                var points = await conn.QueryAsync<PidsGroupPointSQL>(pointsSql, new { GroupSymbolId = id });
                pidsGroupSymbol.LinePoints = points.Select(p => p.ToGeoPoint()).ToList();
            }

            _log?.Info(pidsGroupSymbol != null
                ? $"FetchPidsGroupSymbolAsync 완료 - Id={pidsGroupSymbol.Id}, Points={pidsGroupSymbol.LinePoints.Count}개"
                : $"FetchPidsGroupSymbolAsync 대상 없음 - Id={id}");

            return pidsGroupSymbol;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsGroupSymbol 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    

    /// <summary>
    /// PIDS 그룹 심볼 삽입 (트랜잭션 사용)
    /// </summary>
    public async Task<int> InsertPidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            // 1. Symbols 테이블에 기본 정보 삽입
            const string symbolSql = @"
                INSERT INTO Symbols
                (Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
                 Width, Height, Category, ShowShape, ShowTitle, 
                 FillColor, StrokeColor, StrokeThickness, CreatedBy)
                VALUES (@Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                        @Width, @Height, @Category, @ShowShape, @ShowTitle, 
                        @FillColor, @StrokeColor, @StrokeThickness, @CreatedBy);
                SELECT LAST_INSERT_ID();";

            var symbolId = await conn.ExecuteScalarAsync<int>(symbolSql, new
            {
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = "PIDS_GROUP",  // PIDS 그룹 카테고리
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. PidsGroupSymbols 테이블에 PIDS 그룹 전용 정보 삽입
            const string pidsGroupSql = @"
                INSERT INTO PidsGroupSymbols 
                (SymbolId, LinkedDeviceGroup, EventStatus, LineOpacity, IsClosedPath, ShowArrowHead, LinePattern)
                VALUES (@SymbolId, @LinkedDeviceGroup, @EventStatus, @LineOpacity, @IsClosedPath, @ShowArrowHead, @LinePattern);";

            await conn.ExecuteAsync(pidsGroupSql, new
            {
                SymbolId = symbolId,
                model.LinkedDeviceGroup,
                EventStatus = model.EventStatus.ToString(),
                model.LineOpacity,
                model.IsClosedPath,
                model.ShowArrowHead,
                LinePattern = model.LinePattern.ToString()
            }, transaction);

            // 3. PidsGroupPoints 삽입
            if (model.LinePoints?.Any() == true)
            {
                const string pointSql = @"
                    INSERT INTO PidsGroupPoints (GroupSymbolId, SequenceOrder, Latitude, Longitude, Altitude)
                    VALUES (@GroupSymbolId, @SequenceOrder, @Latitude, @Longitude, @Altitude);";

                var pointParams = model.LinePoints.Select((point, index) => new
                {
                    GroupSymbolId = symbolId,
                    SequenceOrder = index,
                    Latitude = (decimal)point.Latitude,
                    Longitude = (decimal)point.Longitude,
                    Altitude = (float)point.Altitude
                });

                await conn.ExecuteAsync(pointSql, pointParams, transaction);
            }

            await transaction.CommitAsync(token);

            model.Id = symbolId;
            _log?.Info($"PidsGroupSymbol 삽입 완료 - Id={symbolId}, Title={model.Title}, Points={model.LinePoints?.Count ?? 0}개");
            return symbolId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"PidsGroupSymbol 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// PIDS 그룹 심볼 업데이트 (트랜잭션 사용)
    /// </summary>
    public async Task<IPidsGroupSymbolModel?> UpdatePidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        await using var conn = await OpenConnectionAsync(token);
        await using var transaction = await conn.BeginTransactionAsync(token);

        try
        {
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // 1. Symbols 테이블 업데이트
            const string symbolSql = @"
                UPDATE Symbols SET
                    Pid = @Pid, Title = @Title, TitleSize = @TitleSize, OperationState = @OperationState,
                    Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude, Zoom = @Zoom,
                    Bearing = @Bearing, Width = @Width, Height = @Height,
                    Category = @Category, ShowShape = @ShowShape, ShowTitle = @ShowTitle,
                    FillColor = @FillColor, StrokeColor = @StrokeColor, StrokeThickness = @StrokeThickness,
                    CreatedBy = @CreatedBy
                WHERE Id = @Id;";

            var symbolAffected = await conn.ExecuteAsync(symbolSql, new
            {
                model.Id,
                model.Pid,
                model.Title,
                model.TitleSize,
                OperationState = model.OperationState.ToString(),
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Bearing,
                model.Width,
                model.Height,
                Category = "PIDS_GROUP",
                model.ShowShape,
                model.ShowTitle,
                FillColor = model.FillColor.ToString(),
                StrokeColor = model.StrokeColor.ToString(),
                model.StrokeThickness,
                CreatedBy = "System"
            }, transaction);

            // 2. PidsGroupSymbols 테이블 업데이트
            const string pidsGroupSql = @"
                UPDATE PidsGroupSymbols SET
                    LinkedDeviceGroup = @LinkedDeviceGroup, EventStatus = @EventStatus,
                    LineOpacity = @LineOpacity, IsClosedPath = @IsClosedPath,
                    ShowArrowHead = @ShowArrowHead, LinePattern = @LinePattern,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE SymbolId = @SymbolId;";

            var pidsGroupAffected = await conn.ExecuteAsync(pidsGroupSql, new
            {
                SymbolId = model.Id,
                model.LinkedDeviceGroup,
                EventStatus = model.EventStatus.ToString(),
                model.LineOpacity,
                model.IsClosedPath,
                model.ShowArrowHead,
                LinePattern = model.LinePattern.ToString()
            }, transaction);

            // 3. 기존 포인트 삭제 후 새로 삽입
            const string deletePointsSql = "DELETE FROM PidsGroupPoints WHERE GroupSymbolId = @GroupSymbolId;";
            await conn.ExecuteAsync(deletePointsSql, new { GroupSymbolId = model.Id }, transaction);

            if (model.LinePoints?.Any() == true)
            {
                const string insertPointSql = @"
                    INSERT INTO PidsGroupPoints (GroupSymbolId, SequenceOrder, Latitude, Longitude, Altitude)
                    VALUES (@GroupSymbolId, @SequenceOrder, @Latitude, @Longitude, @Altitude);";

                var pointParams = model.LinePoints.Select((point, index) => new
                {
                    GroupSymbolId = model.Id,
                    SequenceOrder = index,
                    Latitude = (decimal)point.Latitude,
                    Longitude = (decimal)point.Longitude,
                    Altitude = (float)point.Altitude
                });

                await conn.ExecuteAsync(insertPointSql, pointParams, transaction);
            }

            if (symbolAffected == 0 || pidsGroupAffected == 0)
                throw new KeyNotFoundException($"PidsGroupSymbol not found. Id={model.Id}");

            await transaction.CommitAsync(token);

            _log?.Info($"PidsGroupSymbol 업데이트 완료 - Id={model.Id}");
            return await FetchPidsGroupSymbolAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"PidsGroupSymbol 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// PIDS 그룹 심볼 삭제 (CASCADE로 PidsGroupSymbols와 PidsGroupPoints도 자동 삭제됨)
    /// </summary>
    public async Task<bool> DeletePidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            // Symbols만 삭제하면 PidsGroupSymbols와 PidsGroupPoints는 CASCADE로 자동 삭제
            const string sql = "DELETE FROM Symbols WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeletePidsGroupSymbolAsync 완료 - Id={model.Id}"
                : $"DeletePidsGroupSymbolAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsGroupSymbol 삭제 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// LinkedDeviceGroup으로 PIDS 그룹 심볼 삭제
    /// </summary>
    public async Task<bool> DeletePidsGroupSymbolByDeviceGroupAsync(int deviceGroup, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (deviceGroup <= 0) throw new ArgumentException(nameof(deviceGroup));

            const string sql = @"
        DELETE s FROM Symbols s
        INNER JOIN PidsGroupSymbols pg ON s.Id = pg.SymbolId
        WHERE pg.LinkedDeviceGroup = @DeviceGroup;";

            int ret = await conn.ExecuteAsync(sql, new { DeviceGroup = deviceGroup });

            _log?.Info(ret > 0
                ? $"DeletePidsGroupSymbolByDeviceGroupAsync 완료 - DeviceGroup={deviceGroup}"
                : $"DeletePidsGroupSymbolByDeviceGroupAsync 대상 없음 - DeviceGroup={deviceGroup}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"PidsGroupSymbol DeviceGroup 삭제 실패: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region - Image CRUD -
    /// <summary>
    /// 모든 이미지를 조회합니다
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <returns>Image 목록</returns>
    public async Task<List<IImageModel>?> FetchImagesAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                SELECT  Id, Title, FilePath, `Left`, Top, `Right`, Bottom,
                        Latitude, Longitude, Altitude, Zoom, Width, Height,
                        Opacity, Rotation, Visibility, HasGeoReference, CoordinateSystem,
                        CreatedAt, UpdatedAt
                FROM    Images
                ORDER BY CreatedAt DESC;";

            var list = (await conn.QueryAsync<ImageSQL>(sql))
                .Select(s => s.ToDomain())
                .ToList();

            _log?.Info($"FetchImagesAsync 완료 - {list.Count}건");
            return list.OfType<IImageModel>().ToList();
        }
        catch (Exception ex)
        {
            _log?.Error($"Images 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// ID로 단일 이미지를 조회합니다
    /// </summary>
    /// <param name="id">Image ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>Image 모델</returns>
    public async Task<IImageModel?> FetchImageAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
                SELECT  Id, Title, FilePath, `Left`, Top, `Right`, Bottom,
                        Latitude, Longitude, Altitude, Zoom, Width, Height,
                        Opacity, Rotation, Visibility, HasGeoReference, CoordinateSystem,
                        CreatedAt, UpdatedAt
                FROM    Images
                WHERE   Id = @Id;";

            var imageDto = await conn.QuerySingleOrDefaultAsync<ImageSQL>(sql, new { Id = id });
            var image = imageDto?.ToDomain();

            _log?.Info(image != null
                ? $"FetchImageAsync 완료 - Id={image.Id}"
                : $"FetchImageAsync 대상 없음 - Id={id}");

            return image;
        }
        catch (Exception ex)
        {
            _log?.Error($"Image 단일 조회 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 새로운 이미지를 삽입합니다
    /// </summary>
    /// <param name="model">Image 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>생성된 Image ID</returns>
    public async Task<int> InsertImageAsync(IImageModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            const string sql = @"
                INSERT INTO Images
                    (Title, FilePath, `Left`, Top, `Right`, Bottom,
                     Latitude, Longitude, Altitude, Zoom, Width, Height,
                     Opacity, Rotation, Visibility, HasGeoReference, CoordinateSystem)
                VALUES
                    (@Title, @FilePath, @Left, @Top, @Right, @Bottom,
                     @Latitude, @Longitude, @Altitude, @Zoom, @Width, @Height,
                     @Opacity, @Rotation, @Visibility, @HasGeoReference, @CoordinateSystem);
                SELECT LAST_INSERT_ID();";

            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                model.Title,
                model.FilePath,
                model.Left,
                model.Top,
                model.Right,
                model.Bottom,
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Width,
                model.Height,
                model.Opacity,
                model.Rotation,
                model.Visibility,
                model.HasGeoReference,
                model.CoordinateSystem
            });

            model.Id = id;
            _log?.Info($"Image 삽입 완료 - Id={id}, Title={model.Title}");
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error($"Image 삽입 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 이미지를 업데이트합니다
    /// </summary>
    /// <param name="model">Image 모델</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>업데이트된 Image 모델</returns>
    public async Task<IImageModel?> UpdateImageAsync(IImageModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            const string sql = @"
                UPDATE Images SET
                    Title = @Title, FilePath = @FilePath,
                    `Left` = @Left, Top = @Top, `Right` = @Right, Bottom = @Bottom,
                    Latitude = @Latitude, Longitude = @Longitude, Altitude = @Altitude,
                    Zoom = @Zoom, Width = @Width, Height = @Height,
                    Opacity = @Opacity, Rotation = @Rotation, Visibility = @Visibility,
                    HasGeoReference = @HasGeoReference, CoordinateSystem = @CoordinateSystem,
                    UpdatedAt = CURRENT_TIMESTAMP
                WHERE Id = @Id;";

            var affected = await conn.ExecuteAsync(sql, new
            {
                model.Id,
                model.Title,
                model.FilePath,
                model.Left,
                model.Top,
                model.Right,
                model.Bottom,
                model.Latitude,
                model.Longitude,
                model.Altitude,
                model.Zoom,
                model.Width,
                model.Height,
                model.Opacity,
                model.Rotation,
                model.Visibility,
                model.HasGeoReference,
                model.CoordinateSystem
            });

            if (affected == 0)
                throw new KeyNotFoundException($"Image not found. Id={model.Id}");

            _log?.Info($"Image 업데이트 완료 - Id={model.Id}");
            return await FetchImageAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error($"Image 업데이트 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 이미지를 삭제합니다
    /// </summary>
    /// <param name="id">Image ID</param>
    /// <param name="token">취소 토큰</param>
    /// <returns>삭제 성공 여부</returns>
    public async Task<bool> DeleteImageAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (id <= 0) throw new ArgumentException(nameof(id));

            const string sql = "DELETE FROM Images WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = id });

            _log?.Info(ret > 0
                ? $"DeleteImageAsync 완료 - Id={id}"
                : $"DeleteImageAsync 대상 없음 - Id={id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"Image 삭제 실패: {ex.Message}");
            throw;
        }
    }
    #endregion

    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 데이터베이스 연결 상태를 나타냅니다
    /// </summary>
    /// <value>연결되어 있으면 true, 그렇지 않으면 false</value>
    public bool IsConnected => _conn != null && _conn.State == ConnectionState.Open;
    #endregion
    #region - Attributes -
    /// <summary>로깅 서비스</summary>
    private ILogService _log;

    /// <summary>이벤트 집계자 (UI 메시지 전송용)</summary>
    private IEventAggregator _eventAggregator;

    /// <summary>Symbol 데이터 제공자</summary>
    private SymbolProvider _symbolProvider;
    private GeometricSymbolProvider _geometrySymbolProvider;
    private PidsSymbolProvider _pidsSymbolProvider;
    private MilitarySymbolProvider _militarySymbolProvider;
    private LineSymbolProvider _lineSymbolProvider;
    private InfraSymbolProvider _infraSymbolProvider;
    private PidsGroupSymbolProvider _pidsGroupSymbolProvider;

    /// <summary>데이터베이스 설정 모델</summary>
    private GMapDbSetupModel _setup;

    /// <summary>작업 취소 토큰 소스</summary>
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>MySQL 연결 인스턴스</summary>
    private MySqlConnection? _conn;

    /// <summary>동시 처리 방지용 세마포어 (최대 1개 작업)</summary>
    private readonly SemaphoreSlim _processGate = new(1, 1);
    #endregion
}

#region - DTO Classes (SQL 매핑용) -
/// <summary>
/// Symbols 테이블과 매핑되는 데이터 전송 객체 (DTO)
/// </summary>
/// <remarks>
/// 데이터베이스의 Symbols 테이블과 1:1 매핑되며,
/// ToDomain() 메서드를 통해 도메인 모델(SymbolModel)로 변환됩니다.
/// </remarks>
internal class SymbolSQL
{
    /// <summary>자동 증가 기본 키</summary>
    public int Id { get; set; }

    /// <summary>비즈니스 로직상의 식별자</summary>
    public int Pid { get; set; }

    /// <summary>Symbol 제목/이름</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Title 크기</summary>
    public double TitleSize { get; set; }

    /// <summary>동작 상태 (NONE, DEACTIVE, ACTIVE, FAULT)</summary>
    public string OperationState { get; set; } = string.Empty;
    //`Latitude`          DECIMAL(10,8) NOT NULL,                 --위도
    //`Longitude`         DECIMAL(11,8) NOT NULL,                 --경도
    //`Altitude`          FLOAT DEFAULT 0,                        --고도
    //`Zoom`              DECIMAL(4, 3) DEFAULT 17,               --디스플레이용(Zoom)
    //`Bearing`           DECIMAL(6,3) DEFAULT 0,                 --심볼각도
    /// <summary>위도 좌표 (DECIMAL(10,8))</summary>
    public decimal Latitude { get; set; }

    /// <summary>경도 좌표 (DECIMAL(11,8))</summary>
    public decimal Longitude { get; set; }

    /// <summary>고도 (FLOAT)</summary>
    public float Altitude { get; set; }
    /// <summary>줌 (DECIMAL(3,1))</summary>
    public decimal Zoom { get; set; }

    /// <summary>방위각 (DECIMAL(6,3))</summary>
    public decimal Bearing { get; set; }

    /// <summary>Symbol 너비 (DECIMAL(8,3))</summary>
    public decimal Width { get; set; }

    /// <summary>Symbol 높이 (DECIMAL(8,3))</summary>
    public decimal Height { get; set; }

    /// <summary>Symbol 카테고리 (BASIC_SHAPES, VEHICLES 등)</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Shape 표시 여부</summary>
    public bool ShowShape { get; set; }

    /// <summary>Title 표시 여부</summary>
    public bool ShowTitle { get; set; }

    public string FillColor { get; set; } = "Blue";
    public string StrokeColor { get; set; } = "White";
    public decimal StrokeThickness { get; set; } = 1.0m;

    /// <summary>생성 일시</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>수정 일시</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>생성자 정보</summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// DTO를 도메인 모델로 변환합니다
    /// </summary>
    /// <returns>변환된 SymbolModel 인스턴스</returns>
    /// <exception cref="ArgumentException">Enum 변환 실패 시</exception>
    public SymbolModel ToDomain() => new()
    {
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumParseHelper.TryParseEnum(Category, EnumMarkerCategory.BASIC_SHAPES),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

    };
}

/// <summary>
/// Symbols와 GeometrySymbols를 조인한 결과를 담는 DTO (간소화)
/// </summary>
internal sealed class GeometrySymbolSQL : SymbolSQL
{
    // SymbolSQL의 모든 속성 + 아래 기하 심볼 속성들

    /// <summary>기하학적 모양 타입</summary>
    public string GeometryShapeType { get; set; } = "Circle";

    /// <summary>기하 심볼 투명도</summary>
    public decimal GeometryOpacity { get; set; } = 1.0m;

    /// <summary>
    /// JOIN 결과를 GeometrySymbolModel로 변환
    /// </summary>
    public GeometricSymbolModel ToGeometryDomain() => new()
    {
        // SymbolSQL 기본 속성들
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumParseHelper.TryParseEnum(Category, EnumMarkerCategory.BASIC_SHAPES),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,

        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

        // GeometrySymbol 전용 속성들 (간소화)
        ShapeType = EnumParseHelper.TryParseEnum(GeometryShapeType, EnumShapeType.Circle),
        Opacity = (double)GeometryOpacity
    };
}

/// <summary>
/// Symbols와 PidsSymbols를 조인한 결과를 담는 DTO
/// </summary>
internal sealed class PidsSymbolSQL : SymbolSQL
{
    // SymbolSQL의 모든 속성 + 아래 PIDS 전용 속성들

    /// <summary>연결된 디바이스 ID</summary>
    public int LinkedDeviceId { get; set; }

    /// <summary>장비 타입</summary>
    public string DeviceType { get; set; } = "Fence";

    /// <summary>FOV 표시 여부</summary>
    public bool ShowFOV { get; set; }

    /// <summary>FOV 색상</summary>
    public string FOVColor { get; set; } = "Red";

    /// <summary>FOV 투명도</summary>
    public decimal FOVOpacity { get; set; } = 0.3m;

    /// <summary>이벤트 상태</summary>
    public string EventStatus { get; set; } = "Normal";

    /// <summary>기준 방향 각도 (카메라 물리적 설치 방향)</summary>
    public decimal BaseBearing { get; set; } = 0.0m;

    /// <summary>
    /// JOIN 결과를 PidsSymbolModel로 변환
    /// </summary>
    public PidsSymbolModel ToPidsDomain() => new()
    {
        // SymbolSQL 기본 속성들
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumParseHelper.TryParseEnum(Category, EnumMarkerCategory.BASIC_SHAPES),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

        // PidsSymbol 전용 속성들
        LinkedDeviceId = LinkedDeviceId,
        DeviceType = EnumParseHelper.TryParseEnum(DeviceType, EnumDeviceType.NONE),
        ShowFOV = ShowFOV,
        FOVColor = EnumParseHelper.TryParseEnum(FOVColor, EnumColorType.Blue),
        FOVOpacity = (double)FOVOpacity,
        EventStatus = EnumParseHelper.TryParseEnum(EventStatus, EnumEventStatus.Normal),
        BaseBearing = (double)BaseBearing,

        // DetectionBearing 초기값을 BaseBearing으로 설정 (FOV 초기 방향)
        DetectionBearing = (double)BaseBearing

        // DetectionRange, DetectionAngle는 기본값 사용 (런타임 조정 가능)
    };
}

/// <summary>
/// Symbols와 MilitarySymbols를 조인한 결과를 담는 DTO
/// </summary>
internal sealed class MilitarySymbolSQL : SymbolSQL
{
    // SymbolSQL의 모든 속성 + 아래 군사 심볼 전용 속성들

    /// <summary>소속 구분</summary>
    public string Affiliation { get; set; } = "Friend";

    /// <summary>전투 차원 (공중성)</summary>
    public string BattleDimension { get; set; } = "Land";

    /// <summary>표준 정체성 (계획 속성)</summary>
    public string StandardIdentity { get; set; } = "Present";

    /// <summary>부대 종류</summary>
    public string UnitType { get; set; } = "Artillery";

    /// <summary>부대 규모</summary>
    public string UnitSize { get; set; } = "Company";

    /// <summary>부대 지시자 (부대명)</summary>
    public string? UnitDesignator { get; set; }

    /// <summary>상급 부대명</summary>
    public string? HigherFormation { get; set; }

    /// <summary>콜사인</summary>
    public string? CallSign { get; set; }

    /// <summary>국가 코드</summary>
    public string? CountryCode { get; set; }

    /// <summary>
    /// JOIN 결과를 MilitarySymbolModel로 변환
    /// </summary>
    public MilitarySymbolModel ToMilitaryDomain() => new()
    {
        // SymbolSQL 기본 속성들
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumParseHelper.TryParseEnum(Category, EnumMarkerCategory.BASIC_SHAPES),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

        // MilitarySymbol 전용 속성들
        Affiliation = EnumParseHelper.TryParseEnum(Affiliation, EnumMilitaryAffiliation.Unknown),
        BattleDimension = EnumParseHelper.TryParseEnum(BattleDimension, EnumMilitaryBattleDimension.Land),
        StandardIdentity = EnumParseHelper.TryParseEnum(StandardIdentity, EnumMilitaryStandardIdentity.Present),
        UnitType = EnumParseHelper.TryParseEnum(UnitType, EnumMilitaryUnitType.Infantry),
        UnitSize = EnumParseHelper.TryParseEnum(UnitSize, EnumMilitaryUnitSize.Individual),
        UnitDesignator = UnitDesignator,
        HigherFormation = HigherFormation,
        CallSign = CallSign,
        CountryCode = CountryCode
    };
}


/// <summary>
/// Symbols와 LineSymbols를 조인한 결과를 담는 DTO
/// </summary>
internal sealed class LineSymbolSQL : SymbolSQL
{
    // SymbolSQL의 모든 속성 + 아래 라인 심볼 전용 속성들

    /// <summary>라인 투명도</summary>
    public decimal LineOpacity { get; set; } = 1.0m;

    /// <summary>닫힌 경로 여부</summary>
    public bool IsClosedPath { get; set; }

    /// <summary>화살표 머리 표시 여부</summary>
    public bool ShowArrowHead { get; set; }

    /// <summary>라인 패턴 타입</summary>
    public string LinePattern { get; set; } = "Solid";

    /// <summary>
    /// JOIN 결과를 LineSymbolModel로 변환 (포인트는 별도 로드 필요)
    /// </summary>
    public LineSymbolModel ToLineDomain() => new()
    {
        // SymbolSQL 기본 속성들
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumParseHelper.TryParseEnum(Category, EnumMarkerCategory.BASIC_SHAPES),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

        // LineSymbol 전용 속성들
        LineOpacity = (double)LineOpacity,
        IsClosedPath = IsClosedPath,
        ShowArrowHead = ShowArrowHead,
        LinePattern = EnumParseHelper.TryParseEnum(LinePattern, EnumLinePattern.Solid),
        LinePoints = new List<GeoPoint>() // 포인트는 별도 쿼리로 로드
    };
}

/// <summary>
/// LinePoints 테이블과 매핑되는 DTO
/// </summary>
internal sealed class LinePointSQL
{
    public int Id { get; set; }
    public int LineSymbolId { get; set; }
    public int SequenceOrder { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public float Altitude { get; set; }

    /// <summary>
    /// DTO를 GeoPoint로 변환
    /// </summary>
    public GeoPoint ToGeoPoint() => new(
        latitude: (double)Latitude,
        longitude: (double)Longitude,
        altitude: (double)Altitude
    );
}

/// <summary>
/// Symbols와 InfraSymbols를 조인한 결과를 담는 DTO
/// </summary>
internal sealed class InfraSymbolSQL : SymbolSQL
{
    // SymbolSQL의 모든 속성 + 아래 인프라 전용 속성들

    /// <summary>건물 종류 (Factory만)</summary>
    public string BuildingType { get; set; } = "Factory";

    /// <summary>건물 용도 (Office만)</summary>
    public string BuildingUsage { get; set; } = "Office";

    /// <summary>지상 층수</summary>
    public int FloorCount { get; set; } = 1;

    /// <summary>지하 층수</summary>
    public int BasementFloorCount { get; set; } = 0;

    /// <summary>건물 면적 (㎡)</summary>
    public decimal BuildingArea { get; set; } = 100.0m;

    /// <summary>
    /// JOIN 결과를 InfraSymbolModel로 변환
    /// </summary>
    public InfraSymbolModel ToInfraDomain() => new()
    {
        // SymbolSQL 기본 속성들
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumParseHelper.TryParseEnum(Category, EnumMarkerCategory.BASIC_SHAPES),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

        // InfraSymbol 전용 속성들
        BuildingType = EnumBuildingType.Factory,  // 하나뿐이므로 하드코딩
        BuildingUsage = EnumBuildingUsage.Office, // 하나뿐이므로 하드코딩
        FloorCount = FloorCount,
        BasementFloorCount = BasementFloorCount,
        BuildingArea = (double)BuildingArea
    };
}
#endregion
#region - DTO Classes for PidsGroupSymbol -

/// <summary>
/// Symbols와 PidsGroupSymbols를 조인한 결과를 담는 DTO
/// </summary>
internal sealed class PidsGroupSymbolSQL : SymbolSQL
{
    // SymbolSQL의 모든 속성 + 아래 PIDS 그룹 전용 속성들

    /// <summary>연결된 디바이스 그룹 ID</summary>
    public int LinkedDeviceGroup { get; set; }

    /// <summary>이벤트 상태</summary>
    public string EventStatus { get; set; } = "Normal";

    /// <summary>라인 투명도</summary>
    public decimal LineOpacity { get; set; } = 1.0m;

    /// <summary>닫힌 경로 여부</summary>
    public bool IsClosedPath { get; set; } = true;

    /// <summary>화살표 머리 표시 여부</summary>
    public bool ShowArrowHead { get; set; }

    /// <summary>라인 패턴 타입</summary>
    public string LinePattern { get; set; } = "Solid";

    /// <summary>
    /// JOIN 결과를 PidsGroupSymbolModel로 변환 (포인트는 별도 로드 필요)
    /// </summary>
    public PidsGroupSymbolModel ToPidsGroupDomain() => new()
    {
        // SymbolSQL 기본 속성들
        Id = Id,
        Pid = Pid,
        Title = Title,
        TitleSize = TitleSize,
        OperationState = EnumParseHelper.TryParseEnum(OperationState, EnumOperationState.NONE),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = EnumMarkerCategory.AREA_BOUNDARY,
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = EnumParseHelper.TryParseEnum(FillColor, EnumColorType.Blue),
        StrokeColor = EnumParseHelper.TryParseEnum(StrokeColor, EnumColorType.Blue),
        StrokeThickness = (double)StrokeThickness,

        // PidsGroupSymbol 전용 속성들
        LinkedDeviceGroup = LinkedDeviceGroup,
        EventStatus = EnumParseHelper.TryParseEnum(EventStatus, EnumEventStatus.Normal),
        LineOpacity = (double)LineOpacity,
        IsClosedPath = IsClosedPath,
        ShowArrowHead = ShowArrowHead,
        LinePattern = EnumParseHelper.TryParseEnum(LinePattern, EnumLinePattern.Solid),
        LinePoints = new List<GeoPoint>() // 포인트는 별도 쿼리로 로드
    };
}

/// <summary>
/// PidsGroupPoints 테이블과 매핑되는 DTO
/// </summary>
internal sealed class PidsGroupPointSQL
{
    public int Id { get; set; }
    public int GroupSymbolId { get; set; }
    public int SequenceOrder { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public float Altitude { get; set; }

    /// <summary>
    /// DTO를 GeoPoint로 변환
    /// </summary>
    public GeoPoint ToGeoPoint() => new(
        latitude: (double)Latitude,
        longitude: (double)Longitude,
        altitude: (double)Altitude
    );
}

#endregion

#region - DTO Classes for Image -

/// <summary>
/// Images 테이블과 매핑되는 DTO
/// </summary>
internal sealed class ImageSQL
{
    /// <summary>자동 증가 기본 키</summary>
    public int Id { get; set; }

    /// <summary>이미지 제목</summary>
    public string? Title { get; set; }

    /// <summary>이미지 파일 경로</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>좌측 경도 (Left Longitude)</summary>
    public decimal Left { get; set; }

    /// <summary>상단 위도 (Top Latitude)</summary>
    public decimal Top { get; set; }

    /// <summary>우측 경도 (Right Longitude)</summary>
    public decimal Right { get; set; }

    /// <summary>하단 위도 (Bottom Latitude)</summary>
    public decimal Bottom { get; set; }

    /// <summary>중심 위도</summary>
    public decimal Latitude { get; set; }

    /// <summary>중심 경도</summary>
    public decimal Longitude { get; set; }

    /// <summary>고도</summary>
    public float Altitude { get; set; }

    /// <summary>표시 줌 레벨 (0 = 모든 줌 레벨)</summary>
    public decimal Zoom { get; set; }

    /// <summary>이미지 너비 (픽셀)</summary>
    public decimal Width { get; set; }

    /// <summary>이미지 높이 (픽셀)</summary>
    public decimal Height { get; set; }

    /// <summary>투명도 (0.0 ~ 1.0)</summary>
    public decimal Opacity { get; set; } = 1.0m;

    /// <summary>회전 각도 (도)</summary>
    public decimal Rotation { get; set; }

    /// <summary>표시 여부</summary>
    public bool Visibility { get; set; } = true;

    /// <summary>지리 참조 여부</summary>
    public bool HasGeoReference { get; set; }

    /// <summary>좌표계 (예: EPSG:4326)</summary>
    public string? CoordinateSystem { get; set; }

    /// <summary>생성 일시</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>수정 일시</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// DTO를 ImageModel로 변환
    /// </summary>
    public ImageModel ToDomain() => new()
    {
        Id = Id,
        Title = Title,
        FilePath = FilePath,
        Left = (double)Left,
        Top = (double)Top,
        Right = (double)Right,
        Bottom = (double)Bottom,
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Width = (double)Width,
        Height = (double)Height,
        Opacity = (double)Opacity,
        Rotation = (double)Rotation,
        Visibility = Visibility,
        HasGeoReference = HasGeoReference,
        CoordinateSystem = CoordinateSystem
    };
}

#endregion

#region - Enum Parse Helper -
/// <summary>
/// DB에서 읽은 문자열을 Enum으로 안전하게 변환하는 헬퍼.
/// 알 수 없는 문자열이 있으면 예외 대신 fallback 값을 반환한다.
/// </summary>
internal static class EnumParseHelper
{
    internal static T TryParseEnum<T>(string value, T fallback)
        where T : struct, Enum
    {
        if (Enum.TryParse<T>(value, ignoreCase: true, out var result))
            return result;
        return fallback;
    }
}
#endregion