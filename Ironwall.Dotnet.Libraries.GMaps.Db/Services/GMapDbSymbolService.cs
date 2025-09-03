using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MySql.Data.MySqlClient;
using System;
using System.Buffers;
using System.Data;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Media.Media3D;

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
                               GMapDbSetupModel setupModel)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _symbolProvider = symbolProvider;
        _geometrySymbolProvider = geometrySymbolProvider;
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

            // ── GeometrySymbols 테이블 (간소화) ──
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

            // 실행 순서
            await conn.ExecuteAsync(createSymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "Symbols 테이블 생성…" });

            await conn.ExecuteAsync(createGeometrySymbolsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage
                { Title = nameof(BuildSchemeAsync), Message = "GeometrySymbols 테이블 생성…" });

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
                ShapeType = @ShapeType, Opacity = @Opacity
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
        OperationState = Enum.Parse<EnumOperationState>(OperationState),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = Enum.Parse<EnumMarkerCategory>(Category),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,
        FillColor = Enum.Parse<EnumColorType>(FillColor),
        StrokeColor = Enum.Parse<EnumColorType>(StrokeColor),
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
        OperationState = Enum.Parse<EnumOperationState>(OperationState),
        Latitude = (double)Latitude,
        Longitude = (double)Longitude,
        Altitude = Altitude,
        Zoom = (double)Zoom,
        Bearing = (double)Bearing,
        Width = (double)Width,
        Height = (double)Height,
        Category = Enum.Parse<EnumMarkerCategory>(Category),
        ShowShape = ShowShape,
        ShowTitle = ShowTitle,

        FillColor = Enum.Parse<EnumColorType>(FillColor),
        StrokeColor = Enum.Parse<EnumColorType>(StrokeColor),
        StrokeThickness = (double)StrokeThickness,

        // GeometrySymbol 전용 속성들 (간소화)
        ShapeType = Enum.Parse<EnumShapeType>(GeometryShapeType),
        Opacity = (double)GeometryOpacity
    };
}
#endregion