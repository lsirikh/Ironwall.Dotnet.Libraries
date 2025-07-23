using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Db.Models;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using Newtonsoft.Json.Linq;
using System;
using System.Data;
using System.Threading;
using System.Windows.Media.Media3D;

namespace Ironwall.Dotnet.Libraries.Events.Db.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 3:46:35 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class EventDbService : TaskService, IEventDbService
{

    #region - Ctors -
    public EventDbService(ILogService log
                            , IEventAggregator eventAggregator
                            , EventProvider eventProvider
                            , DetectionEventProvider detectionProvider
                            , MalfunctionEventProvider malfunctionProvider
                            , ConnectionEventProvider connectionProvider
                            , ActionEventProvider actionProvider
                            , DeviceProvider deviceProvider
                            , EventDbSetupModel setupModel)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _setup = setupModel;
        _eventProvider = eventProvider;
        _detectionProvider = detectionProvider;
        _connectionProvider = connectionProvider;
        _malfunctionProvider = malfunctionProvider;
        _actionProvider = actionProvider;
        _deviceProvider = deviceProvider;
    }
    #endregion
    #region - Implementation of Interface -
    protected override async Task RunTask(CancellationToken token = default)
    {
        await StartService(token);
    }

    protected override async Task ExitTask(CancellationToken token = default)
    {
        await StopService(token);
    }

    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public async Task StartService(CancellationToken token = default)
    {
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            await Connect(token);
            await BuildSchemeAsync(token);
            await FetchInstanceAsync(token:token);

        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task StopService(CancellationToken token = default)
    {
        try
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource!.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            await Disconnect(token);
        }
        catch (Exception)
        {
            throw;
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
            csb.Database = _setup.DbDatabase.ToLowerInvariant();   // DB 있을 때만 지정
        return csb.ToString();
    }


    public async Task Connect(CancellationToken token = default)
    {

        try
        {
            // ── 0. DB 이름 통일(대소문자 문제 차단) ──────────────────────────
            var dbName = (_setup.DbDatabase ?? "monitor_DB").ToLowerInvariant();
            _setup.DbDatabase = dbName;            // 내부 상태도 통일

            // 1. DB 없는 상태에서도 열 수 있도록 includeDb = false
            await using (var bootstrap = new MySqlConnection(BuildConnStr(includeDb: false)))
            {
                await bootstrap.OpenAsync(token);

                // DB가 없으면 생성
                var createDbSql =
                    $"CREATE DATABASE IF NOT EXISTS `{dbName}` " +
                    "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci;";
                var result = await bootstrap.ExecuteAsync(createDbSql, token);
                if (result > 0)
                    // DB가 없다면 CREATE
                    _log?.Info($"DB({dbName})가 이미 생성되어 있습니다.");
            }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name, Message = "DB테이블을 호출했습니다." });


            // ── 2. 애플리케이션용 커넥션(데이터베이스 포함) ──────────────────
            _conn = new MySqlConnection(BuildConnStr(includeDb: true));
            await _conn.OpenAsync(token);
            var msg = $"DB 연결 성공: {_setup.IpDbServer}:{_setup.PortDbServer}/{dbName}";
            _log?.Info(msg);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name, Message = msg });
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }

    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken token = default)
    {
        try
        {
            var conn = new MySqlConnection(BuildConnStr(includeDb: true));
            await conn.OpenAsync(token);
            var msg = $"DB 연결 성공: {_setup.IpDbServer}:{_setup.PortDbServer}/{_setup.DbDatabase}";
            _log?.Info(msg);
            return conn;
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
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
                _log?.Info("MySQL/MariaDB 연결이 정상적으로 종료되었습니다.");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }

    public async Task BuildSchemeAsync(CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            // ─────────────────── ExEvents (공통) ───────────────────────────────
            var createExEventsSql = @"
            CREATE TABLE IF NOT EXISTS `ExEvents` (
                `Id`            INT AUTO_INCREMENT PRIMARY KEY,
                `EventGroup`    VARCHAR(64),                  -- group_event
                `EventType`     VARCHAR(20)  NOT NULL,        -- EnumEventType.ToString()
                `DeviceId`      INT         DEFAULT NULL,     -- 이벤트 발생 장치 PK (nullable)
                `DeviceType`    VARCHAR(20) DEFAULT NULL,     -- EnumDeviceType.ToString()
                `Status`        VARCHAR(10) NOT NULL,         -- EnumTrueFalse.ToString()
                `EventTime`     DATETIME    NOT NULL,
                `CreatedAt`     DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`     DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX `IX_ExEvents_Time` (`EventTime`)
            );";

            // ─────────────────── DetectionEvents 상세 ─────────────────────────
            //var createDetectionSql = @"
            //CREATE TABLE IF NOT EXISTS `DetectionEvents` (
            //    `ExEventId` INT PRIMARY KEY,          -- FK = ExEvents.Id
            //    `Result`    INT NOT NULL,             -- result
            //    CONSTRAINT `FK_Detection_ExEvent`
            //        FOREIGN KEY (`ExEventId`) REFERENCES `ExEvents` (`Id`)
            //        ON DELETE CASCADE
            //);"; 
            
            var createDetectionSql = @"
            CREATE TABLE IF NOT EXISTS `DetectionEvents` (
                `ExEventId` INT PRIMARY KEY,          -- FK = ExEvents.Id
                `Result`    VARCHAR(20) DEFAULT NULL,             -- result
                CONSTRAINT `FK_Detection_ExEvent`
                    FOREIGN KEY (`ExEventId`) REFERENCES `ExEvents` (`Id`)
                    ON DELETE CASCADE
            );";


            // ─────────────────── ConnectionEvents 상세 ────────────────────────
            // 추가 속성 없음 → 별도 컬럼 없이 FK 보존
            var createConnectionSql = @"
            CREATE TABLE IF NOT EXISTS `ConnectionEvents` (
                `ExEventId` INT PRIMARY KEY,
                CONSTRAINT `FK_Connection_ExEvent`
                    FOREIGN KEY (`ExEventId`) REFERENCES `ExEvents` (`Id`)
                    ON DELETE CASCADE
            );";

            // ─────────────────── MalfunctionEvents 상세 ───────────────────────
            var createMalfunctionSql = @"
            CREATE TABLE IF NOT EXISTS `MalfunctionEvents` (
                `ExEventId`    INT PRIMARY KEY,
                `Reason`       VARCHAR(20) NOT NULL,  -- EnumFaultType.ToString()
                `FirstStart`   INT NOT NULL,
                `FirstEnd`     INT NOT NULL,
                `SecondStart`  INT NOT NULL,
                `SecondEnd`    INT NOT NULL,
                CONSTRAINT `FK_Malfunction_ExEvent`
                    FOREIGN KEY (`ExEventId`) REFERENCES `ExEvents` (`Id`)
                    ON DELETE CASCADE
            );";

            // ─────────────────── ActionEvents ────────────────────────────────
            var createActionSql = @"
            CREATE TABLE IF NOT EXISTS `ActionEvents` (
                `Id`            INT AUTO_INCREMENT PRIMARY KEY,
                `Content`       TEXT,
                `User`          VARCHAR(64),
                `OriginEventId` INT DEFAULT NULL,      -- ExEvents.Id
                `EventTime`     DATETIME NOT NULL,
                `CreatedAt`     DATETIME DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`     DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                CONSTRAINT `FK_Action_Origin`
                    FOREIGN KEY (`OriginEventId`) REFERENCES `ExEvents` (`Id`)
                    ON DELETE CASCADE
                    ON UPDATE CASCADE,
                INDEX `IX_Action_Time` (`EventTime`)
            );";

            // ─────────────────── 실행 순서 ────────────────────────────────────
            await conn.ExecuteAsync(createExEventsSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage { Title = nameof(BuildSchemeAsync), Message = "ExEvents 테이블 생성…" });

            await conn.ExecuteAsync(createDetectionSql);
            await conn.ExecuteAsync(createConnectionSql);
            await conn.ExecuteAsync(createMalfunctionSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage { Title = nameof(BuildSchemeAsync), Message = "Detail 이벤트 테이블 생성…" });

            await conn.ExecuteAsync(createActionSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage { Title = nameof(BuildSchemeAsync), Message = "ActionEvents 테이블 생성…" });

            _log?.Info("이벤트 관련 테이블 생성/확인 완료.");
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }
    
    public async Task FetchInstanceAsync(
        DateTime? startDate = null,
         DateTime? endDate = null, 
         CancellationToken token = default)
    {

        bool gateEntered = false;

        if (!await _processGate.WaitAsync(0))
            return;

        gateEntered = true;

        try
        {

            if (_cancellationTokenSource != null && !_cancellationTokenSource!.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }


            if(token == default)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }
            else
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            }


            List<IDetectionEventModel>? detections = await FetchDetectionEventsAsync(startDate: startDate, endDate:endDate, token:token);
            _eventProvider.Clear();
            _detectionProvider.Clear();
            /* 2) Detection */
            if (detections?.Any() != false)
                foreach (var item in detections.OfType<IDetectionEventModel>())
                    _eventProvider.Add(item);

            if (_eventAggregator != null) { }
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "DetectionEventProvider의 정보를 모두 불러왔습니다..." });

            /* 3) Connection */
            List<IConnectionEventModel>? connections = await FetchConnectionEventsAsync(startDate: startDate, endDate: endDate, token: token);
            _connectionProvider.Clear();
            if (connections?.Any() != false)
                foreach (var item in connections.OfType<IConnectionEventModel>())
                    _eventProvider.Add(item);

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "ConnectionEventProvider의 정보를 모두 불러왔습니다..." });

            /* 4) Malfunction */
            List<IMalfunctionEventModel>? faults = await FetchMalfunctionEventsAsync(startDate: startDate, endDate: endDate, token: token);
            _malfunctionProvider.Clear();
            if (faults?.Any() != false)
                foreach (var ev in faults)
                    _eventProvider.Add(ev);

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "MalfunctionEventProvider의 정보를 모두 불러왔습니다..." });

            /* 5) Action */
            List<IActionEventModel>? actions = await FetchActionEventsAsync(startDate: startDate, endDate: endDate, token: token);
            _actionProvider.Clear();
            if (actions?.Any() != false)
                foreach (var ev in actions)
                    _eventProvider.Add(ev);

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "ActionEventProvider의 정보를 모두 불러왔습니다..." });

        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
            throw;
        }
        finally
        {
            if (gateEntered)
                _processGate.Release();
        }
    }

    /* --------------------------------------------------------------------
     * 1) DetectionEvents + ExEvents 전체 로드
     * ------------------------------------------------------------------*/
    public async Task<List<IDetectionEventModel>?> FetchDetectionEventsAsync(
     DateTime? startDate = null,
     DateTime? endDate = null,
     CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);


            /* 기본 기간: (지금-24h) ~ 지금 */
            DateTime end = endDate ?? DateTime.Now;
            DateTime start = startDate ?? end.AddDays(-1);

            /*─── 센서 캐시( id → 객체 ) ───*/
            var sensorDict = _deviceProvider
                             .OfType<ISensorDeviceModel>()
                             .ToDictionary(s => s.Id);

            /*─── 1회 JOIN + multi-mapping ───*/
            const string sql = @"
        SELECT  e.Id, e.EventGroup, e.EventType, e.DeviceId, e.DeviceType,
                e.Status, e.EventTime,
                d.ExEventId, d.Result
        FROM    ExEvents         e
        JOIN    DetectionEvents  d ON d.ExEventId = e.Id
        WHERE   e.EventType = @Evt
          AND   e.EventTime BETWEEN @Start AND @End;";

            var list = (await conn.QueryAsync<ExEventSQL, DetectionSQL, IDetectionEventModel>(
                            sql,
                            map: (ex, det) =>
                            {
                                if (token.IsCancellationRequested) throw new TaskCanceledException("Task was cancelled!!");

                                /* ExEvent → Domain */
                                var baseDom = ex.ToDomain();

                                /* Detection 상세 덧씌우기 */
                                var detDom = det.ToDomain(baseDom);

                                /* 센서 연결(O(1)) */
                                if (baseDom.Device is { Id: > 0 } &&
                                    sensorDict.TryGetValue(baseDom.Device.Id, out var sn))
                                    detDom.Device = sn;

                                return detDom;
                            },
                            param: new
                            {
                                Evt = EnumEventType.Intrusion.ToString(),
                                Start = start,
                                End = end
                            },
                            splitOn: "ExEventId"))
                       .ToList();

            _log?.Info($"FetchDetectionEventsAsync 완료 - {list.Count}건, 기간 {start:u}~{end:u}");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }

    }

    /* --------------------------------------------------------------------
     * 2) 단일 DetectionEvent (Id = ExEvents PK) 로드
     * ------------------------------------------------------------------*/
    public async Task<IDetectionEventModel?> FetchDetectionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            /*─────────────────────────
             * 센서-ID → 센서 객체 사전
             * (Provider에 이미 캐시돼 있다면 한 줄 O(1) 탐색)
             *────────────────────────*/
            var sensorDict = _deviceProvider
                             .OfType<ISensorDeviceModel>()
                             .ToDictionary(s => s.Id);

            /*─────────────────────────
             *  Ex+Detection 1회 JOIN
             *────────────────────────*/
            const string sql = @"
                                SELECT  e.Id, e.EventGroup, e.EventType, e.DeviceId, e.DeviceType,
                                        e.Status, e.EventTime,
                                        d.ExEventId, d.Result
                                FROM    ExEvents        e
                                JOIN    DetectionEvents d ON d.ExEventId = e.Id
                                WHERE   e.Id = @Id
                                  AND   e.EventType = @Evt;";          // 파라미터화

            var evt = (await conn.QueryAsync<ExEventSQL, DetectionSQL, IDetectionEventModel>(
               sql,
               (ex, det) =>
               {
                   var baseDom = ex.ToDomain();
                   var detDom = det.ToDomain(baseDom);

                   if (sensorDict.TryGetValue(baseDom.Device.Id, out var sn))
                       detDom.Device = sn;

                   return detDom;
               },
               new
               {
                   Id = id,
                   Evt = EnumEventType.Intrusion.ToString()
               },
               splitOn: "ExEventId")).SingleOrDefault();

            _log?.Info(evt != null
                ? $"FetchDetectionEventAsync 완료 - Id={evt.Id}"
                : $"FetchDetectionEventAsync 대상 없음 - Id={id}");

            return evt;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 3) INSERT DetectionEvent
     * ------------------------------------------------------------------*/
    public async Task<int> InsertDetectionEventAsync(IDetectionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            /* ① ExEvents 먼저 삽입 */
            const string exSql = @"
            INSERT INTO ExEvents
            (EventGroup, EventType, DeviceId, DeviceType, Status, EventTime)
            VALUES (@EventGroup, @MessageType, @DeviceId, @DeviceType, @Status, @EventTime);
            SELECT LAST_INSERT_ID();";

            var eventId = await conn.ExecuteScalarAsync<int>(exSql, new
            {
                EventGroup = model.EventGroup,
                MessageType = model.MessageType.ToString(),
                DeviceId = model?.Device?.Id,
                DeviceType = model?.Device?.DeviceType.ToString(),
                Status = model?.Status.ToString(),
                EventTime = model.DateTime
            });

            /* ② DetectionEvents 상세 삽입 */
            const string detSql = @"
            INSERT INTO DetectionEvents (ExEventId, Result)
            VALUES (@ExEventId, @Result);";

            await conn.ExecuteAsync(detSql, new
            {
                ExEventId = eventId,
                Result = model?.Result.ToString(),
            });

            model.Id = eventId;
            return eventId;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 4) UPDATE DetectionEvent
     *   · 공통(ExEvents) + 상세(Drows) 모두 갱신
     * ------------------------------------------------------------------*/
    public async Task<IDetectionEventModel?> UpdateDetectionEventAsync(IDetectionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            /* 1. 공통 파라미터 한 번만 작성 */
            var param = new
            {
                model.Id,
                model.EventGroup,
                DeviceId = (model.Device as BaseDeviceModel)?.Id,
                DeviceType = model.Device?.DeviceType.ToString(),
                Status = model.Status.ToString(),
                EventTime = model.DateTime,      // 필요 시 .ToUniversalTime()
                Result = model.Result.ToString(),
            };

            const string exSql = @"
                            UPDATE ExEvents SET
                                EventGroup = @EventGroup,
                                DeviceId   = @DeviceId,
                                DeviceType = @DeviceType,
                                Status     = @Status,
                                EventTime  = @EventTime
                            WHERE Id = @Id;";

            const string detSql = @"
                            UPDATE DetectionEvents SET
                                Result = @Result
                            WHERE ExEventId = @Id;";


            /* 3. 공통 테이블 업데이트 */
            int exAffected = await conn.ExecuteAsync(exSql, param);
            if (exAffected == 0)
                throw new KeyNotFoundException($"ExEvents row not found. Id={model.Id}");

            /* 4. 상세 테이블 업데이트 */
            int detAffected = await conn.ExecuteAsync(detSql, param);
            if (detAffected == 0)
                throw new KeyNotFoundException($"DetectionEvents row not found. Id={model.Id}");

            _log?.Info($"DetectionEvent 업데이트 완료 - Id={model.Id}");


            return await FetchDetectionEventAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 5) DELETE DetectionEvent (FK CASCADE 로 상세 레코드도 함께 삭제)
     * ------------------------------------------------------------------*/
    public async Task<bool> DeleteDetectionEventAsync(
        IDetectionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentException(nameof(model.Id));

            const string sql = "DELETE FROM ExEvents WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteDetectionEventAsync 완료 - Id={model.Id}"
                : $"DeleteDetectionEventAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 1) MalfunctionEvents + ExEvents 전체 로드
     * ------------------------------------------------------------------*/
    public async Task<List<IMalfunctionEventModel>?> FetchMalfunctionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            DateTime end = endDate ?? DateTime.Now;
            DateTime start = startDate ?? end.AddDays(-1);

            /*─── 센서 캐시 ───*/
            var sensorDict = _deviceProvider
                             .OfType<ISensorDeviceModel>()
                             .ToDictionary(s => s.Id);

            /*─── JOIN + multi-mapping ───*/
            const string sql = @"
        SELECT  e.Id, e.EventGroup, e.EventType, e.DeviceId, e.DeviceType,
                e.Status, e.EventTime,
                m.ExEventId, m.Reason, m.FirstStart, m.FirstEnd, m.SecondStart, m.SecondEnd
        FROM    ExEvents          e
        JOIN    MalfunctionEvents m ON m.ExEventId = e.Id
        WHERE   e.EventType = @Evt
          AND   e.EventTime BETWEEN @Start AND @End;";

            var list = (await conn.QueryAsync<ExEventSQL, MalfunctionSQL, IMalfunctionEventModel>(
                            sql,
                            (ex, mf) =>
                            {



                                var baseDom = ex.ToDomain();
                                var mfDom = mf.ToDomain(baseDom);

                                if (sensorDict.TryGetValue(mfDom.Device.Id, out var sn))
                                    mfDom.Device = sn;

                                return mfDom;
                            },
                            new { Evt = EnumEventType.Fault.ToString(), Start = start, End = end },
                            splitOn: "ExEventId"))
                        .ToList();

            _log?.Info($"FetchMalfunctionEventsAsync 완료 - {list.Count}건, 기간 {start:u}~{end:u}");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 2) 단일 MalfunctionEvent (Id = ExEvents PK) 로드
     * ------------------------------------------------------------------*/
    public async Task<IMalfunctionEventModel?> FetchMalfunctionEventAsync(
        int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

            var sensorDict = _deviceProvider.OfType<ISensorDeviceModel>()
                                            .ToDictionary(s => s.Id);

            const string sql = @"
        SELECT  e.Id, e.EventGroup, e.EventType, e.DeviceId, e.DeviceType,
                e.Status, e.EventTime,
                m.ExEventId, m.Reason, m.FirstStart, m.FirstEnd, m.SecondStart, m.SecondEnd
        FROM    ExEvents          e
        JOIN    MalfunctionEvents m ON m.ExEventId = e.Id
        WHERE   e.Id = @Id AND e.EventType = @Evt;";

            var ev = (await conn.QueryAsync<ExEventSQL, MalfunctionSQL, IMalfunctionEventModel>(
                          sql,
                          (ex, mf) =>
                          {
                              if (token.IsCancellationRequested) throw new TaskCanceledException("Task was cancelled!!");

                              var baseDom = ex.ToDomain();
                              var mfDom = mf.ToDomain(baseDom);

                              if (sensorDict.TryGetValue(mfDom.Device.Id, out var sn))
                                  mfDom.Device = sn;
                              return mfDom;
                          },
                          new { Id = id, Evt = EnumEventType.Fault.ToString() },
                          splitOn: "ExEventId")).SingleOrDefault();

            _log?.Info(ev != null
                ? $"FetchMalfunctionEventAsync 완료 - Id={ev.Id}"
                : $"FetchMalfunctionEventAsync 대상 없음 - Id={id}");

            return ev;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 3) INSERT MalfunctionEvent
     * ------------------------------------------------------------------*/
    public async Task<int> InsertMalfunctionEventAsync(
        IMalfunctionEventModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);

        using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            const string exSql = @"
        INSERT INTO ExEvents(EventGroup,EventType,DeviceId,DeviceType,Status,EventTime)
        VALUES(@EventGroup,@Evt,@DeviceId,@DeviceType,@Status,@EventTime);
        SELECT LAST_INSERT_ID();";

            int id = await conn.ExecuteScalarAsync<int>(exSql, new
            {
                model.EventGroup,
                Evt = EnumEventType.Fault.ToString(),
                DeviceId = model.Device?.Id,
                DeviceType = model.Device?.DeviceType.ToString(),
                Status = model.Status.ToString(),
                EventTime = model.DateTime
            }, tx);

            const string mfSql = @"
        INSERT INTO MalfunctionEvents
        (ExEventId,Reason,FirstStart,FirstEnd,SecondStart,SecondEnd)
        VALUES(@Id,@Reason,@F1,@F2,@S1,@S2);";

            await conn.ExecuteAsync(mfSql, new
            {
                Id = id,
                Reason = model.Reason.ToString(),
                F1 = model.FirstStart,
                F2 = model.FirstEnd,
                S1 = model.SecondStart,
                S2 = model.SecondEnd
            }, tx);

            await tx.CommitAsync(token);
            model.Id = id;
            return id;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 4) UPDATE MalfunctionEvent
     * ------------------------------------------------------------------*/
    public async Task<IMalfunctionEventModel?> UpdateMalfunctionEventAsync(
        IMalfunctionEventModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);

        if (model.Id <= 0) throw new ArgumentOutOfRangeException(nameof(model.Id));

        var p = new
        {
            model.Id,
            model.EventGroup,
            DeviceId = model.Device?.Id,
            DeviceType = model.Device?.DeviceType.ToString(),
            Status = model.Status.ToString(),
            EventTime = model.DateTime,
            Reason = model.Reason.ToString(),
            model.FirstStart,
            model.FirstEnd,
            model.SecondStart,
            model.SecondEnd
        };

        using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            const string exSql = @"
        UPDATE ExEvents SET
            EventGroup = @EventGroup,
            DeviceId   = @DeviceId,
            DeviceType = @DeviceType,
            Status     = @Status,
            EventTime  = @EventTime
        WHERE Id = @Id;";

            const string mfSql = @"
        UPDATE MalfunctionEvents SET
            Reason       = @Reason,
            FirstStart   = @FirstStart,
            FirstEnd     = @FirstEnd,
            SecondStart  = @SecondStart,
            SecondEnd    = @SecondEnd
        WHERE ExEventId = @Id;";

            int a = await conn.ExecuteAsync(exSql, p, tx);
            int b = await conn.ExecuteAsync(mfSql, p, tx);

            if (a == 0 || b == 0)
                throw new KeyNotFoundException($"MalfunctionEvent Id={model.Id} not found.");

            await tx.CommitAsync(token);
            return await FetchMalfunctionEventAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 5) DELETE MalfunctionEvent (FK CASCADE)
     * ------------------------------------------------------------------*/
    public async Task<bool> DeleteMalfunctionEventAsync(IMalfunctionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentOutOfRangeException(nameof(model.Id));

            const string sql = "DELETE FROM ExEvents WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteMalfunctionEventAsync 완료 - Id={model.Id}"
                : $"MalfunctionEvent 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 1) ConnectionEvents + ExEvents 전체 로드
     * ------------------------------------------------------------------*/
    public async Task<List<IConnectionEventModel>?> FetchConnectionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);


            DateTime end = endDate ?? DateTime.Now;
            DateTime start = startDate ?? end.AddDays(-1);

            var sensorDict = _deviceProvider.OfType<ISensorDeviceModel>()
                                            .ToDictionary(s => s.Id);


            const string sql = @"
            SELECT  e.Id, e.EventGroup, e.EventType, e.DeviceId, e.DeviceType,
                    e.Status, e.EventTime,
                    c.ExEventId                           -- ← 두 번째 엔터티 시작 컬럼
            FROM    ExEvents         e
            JOIN    ConnectionEvents c ON c.ExEventId = e.Id
            WHERE   e.EventType = @Evt
              AND   e.EventTime BETWEEN @Start AND @End;";

            var list = (await conn.QueryAsync<ExEventSQL, ConnectionSQL, IConnectionEventModel>(sql,
                         (ex, co) =>
                         {
                             if (token.IsCancellationRequested) throw new TaskCanceledException("Task was cancelled!!");

                             var baseDom = ex.ToDomain();
                             var coDom = co.ToDomain(baseDom);

                             if (sensorDict.TryGetValue(coDom.Device.Id, out var sn))
                                 coDom.Device = sn;

                             return coDom;
                         },
                         new
                         {
                             Evt = EnumEventType.Connection.ToString(),
                             Start = start,
                             End = end
                         },
                         splitOn: "ExEventId"))
                     .ToList();


            _log?.Info($"FetchConnectionEventsAsync 완료 - {list.Count}건, 기간 {start:u}~{end:u}");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 2) 단일 ConnectionEvent 로드
     * ------------------------------------------------------------------*/
    public async Task<IConnectionEventModel?> FetchConnectionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

            var sensorDict = _deviceProvider.OfType<ISensorDeviceModel>()
                                            .ToDictionary(s => s.Id);

            const string sql = @"
            SELECT  e.Id, e.EventGroup, e.EventType, e.DeviceId, e.DeviceType,
                    e.Status, e.EventTime,
                    c.ExEventId                           -- ← 두 번째 엔터티 시작 컬럼
            FROM    ExEvents         e
            JOIN    ConnectionEvents c ON c.ExEventId = e.Id
            WHERE    e.Id = @Id AND e.EventType = @Evt;";

            var ev = (await conn.QueryAsync<ExEventSQL, ConnectionSQL, IConnectionEventModel>(
                         sql,
                         (ex, co) =>
                         {
                             var baseDom = ex.ToDomain();
                             var coDom = co.ToDomain(baseDom);

                             if (sensorDict.TryGetValue(coDom.Device.Id, out var sn))
                                 coDom.Device = sn;
                             return coDom;
                         },
                         new { Id = id, Evt = EnumEventType.Connection.ToString() },
                         splitOn: "ExEventId")).SingleOrDefault();

            _log?.Info($"FetchConnectionEventAsync 완료 - Id={id}");
            return ev;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchConnectionEventAsync Error: {ex}");
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 3) INSERT ConnectionEvent
     * ------------------------------------------------------------------*/
    public async Task<int> InsertConnectionEventAsync(
        IConnectionEventModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);


        using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            const string exSql = @"
        INSERT INTO ExEvents(EventGroup,EventType,DeviceId,DeviceType,Status,EventTime)
        VALUES(@EventGroup,@Evt,@DeviceId,@DeviceType,@Status,@EventTime);
        SELECT LAST_INSERT_ID();";

            int id = await conn.ExecuteScalarAsync<int>(exSql, new
            {
                model.EventGroup,
                Evt = EnumEventType.Connection.ToString(),
                DeviceId = model.Device?.Id,
                DeviceType = model.Device?.DeviceType.ToString(),
                Status = model.Status.ToString(),
                EventTime = model.DateTime
            }, tx);

            const string conSql = @"INSERT INTO ConnectionEvents(ExEventId) VALUES (@Id);";
            await conn.ExecuteAsync(conSql, new { Id = id }, tx);

            await tx.CommitAsync(token);
            model.Id = id;
            return id;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(CancellationToken.None);
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 4) UPDATE ConnectionEvent  (ExEvents 만 갱신)
     * ------------------------------------------------------------------*/
    public async Task<IConnectionEventModel?> UpdateConnectionEventAsync(
        IConnectionEventModel model, CancellationToken token = default)
    {
        try
        {

            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentOutOfRangeException(nameof(model.Id));

            const string sql = @"
        UPDATE ExEvents SET
            EventGroup = @EventGroup,
            DeviceId   = @DeviceId,
            DeviceType = @DeviceType,
            Status     = @Status,
            EventTime  = @EventTime
        WHERE Id = @Id;";

            int affected = await conn.ExecuteAsync(sql, new
            {
                model.Id,
                model.EventGroup,
                DeviceId = model.Device?.Id,
                DeviceType = model.Device?.DeviceType.ToString(),
                Status = model.Status.ToString(),
                EventTime = model.DateTime
            });

            if (affected == 0)
                throw new KeyNotFoundException($"ConnectionEvent Id={model.Id} not found.");

            return await FetchConnectionEventAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 5) DELETE ConnectionEvent (FK CASCADE)
     * ------------------------------------------------------------------*/
    public async Task<bool> DeleteConnectionEventAsync(IConnectionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);
            if (model.Id <= 0) throw new ArgumentOutOfRangeException(nameof(model.Id));

            const string sql = "DELETE FROM ExEvents WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteConnectionEventAsync 완료 - Id={model.Id}"
                : $"ConnectionEvent 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 1) ActionEvents 전체 로드 (기간 필터)
     * ------------------------------------------------------------------*/
    public async Task<List<IActionEventModel>?> FetchActionEventsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);


            DateTime end = endDate ?? DateTime.Now;
            DateTime start = startDate ?? end.AddDays(-1);

            /* ExEvent + Action LEFT JOIN  (OriginEventId nullable) */
            const string sql = @"
        SELECT a.Id As ActionId, a.Content, a.User, a.EventTime, a.OriginEventId,
               e.Id,        e.EventGroup,  e.EventType,
               e.DeviceId,              e.DeviceType, e.Status, e.EventTime
        FROM   ActionEvents a
        LEFT JOIN ExEvents  e ON e.Id = a.OriginEventId
        WHERE  a.EventTime BETWEEN @Start AND @End;";

            /* 센서 매핑 준비 */
            var sensorDict = _deviceProvider.OfType<ISensorDeviceModel>()
                                            .ToDictionary(s => s.Id);
            var cmd = new CommandDefinition(
              sql,
              parameters: new { Start = start, End = end },
              cancellationToken: token);

            var list = (await conn.QueryAsync<ActionSQL, ExEventSQL?, IActionEventModel>(
                            cmd,
                            map: (act, ex) =>
                            {
                                if (token.IsCancellationRequested) throw new TaskCanceledException("Task was cancelled!!");

                                /* OriginEvent (nullable) */
                                IExEventModel? origin = ex is null ? null : ex.ToDomain();
                                if (origin == null) throw new NullReferenceException("OriginEvent was not exist...");
                                var dom = act.ToDomain(origin);

                                /* Device 연결 (OriginEvent.Device가 있을 때만) */
                                if (origin?.Device.Id is > 0 &&
                                    sensorDict.TryGetValue(origin.Device.Id, out var sn))
                                    origin.Device = sn;

                                return dom;
                            },
                            splitOn: "Id"))
                       .ToList();

            _log?.Info($"FetchActionEventsAsync 완료 - {list.Count}건, {start:u}~{end:u}");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 2) 단일 ActionEvent 로드
     * ------------------------------------------------------------------*/
    public async Task<IActionEventModel?> FetchActionEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = @"
        SELECT a.Id As ActionId, a.Content, a.User, a.EventTime, a.OriginEventId,
               e.Id,        e.EventGroup,  e.EventType,
               e.DeviceId,              e.DeviceType, e.Status, e.EventTime
        FROM   ActionEvents a
        LEFT JOIN ExEvents  e ON e.Id = a.OriginEventId
        WHERE  a.Id = @Id;";

            var sensorDict = _deviceProvider.OfType<ISensorDeviceModel>()
                                            .ToDictionary(s => s.Id);

            var ev = (await conn.QueryAsync<ActionSQL, ExEventSQL?, IActionEventModel>(
                         sql,
                         (act, ex) =>
                         {
                             IExEventModel? origin = ex is null ? null : ex.ToDomain();
                             var dom = act.ToDomain(origin);

                             if (origin?.Device.Id is > 0 &&
                                 sensorDict.TryGetValue(origin.Device.Id, out var sn))
                                 origin.Device = sn;

                             return dom;
                         },
                         new { Id = id },
                         splitOn: "Id")).SingleOrDefault();

            _log?.Info(ev != null
                ? $"FetchActionEventAsync 완료 - Id={ev.Id}"
                : $"ActionEvent 없음 - Id={id}");

            return ev;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 3) INSERT ActionEvent
     * ------------------------------------------------------------------*/
    public async Task<int> InsertActionEventAsync(IActionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);


            const string sql = @"
        INSERT INTO ActionEvents(Content, User, OriginEventId, EventTime)
        VALUES(@Content, @User, @OriginEventId, @EventTime);
        SELECT LAST_INSERT_ID();";

            int id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                model.Content,
                model.User,
                OriginEventId = model.OriginEvent?.Id,
                EventTime = model.DateTime
            });

            model.Id = id;

            _log?.Info($"The action event({id}) was successfully inserted...");
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 4) UPDATE ActionEvent
     * ------------------------------------------------------------------*/
    public async Task<IActionEventModel?> UpdateActionEventAsync(IActionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentOutOfRangeException(nameof(model.Id));

            const string sql = @"
        UPDATE ActionEvents SET
            Content       = @Content,
            User          = @User,
            OriginEventId = @OriginEventId,
            EventTime     = @EventTime
        WHERE Id = @Id;";

            int affected = await conn.ExecuteAsync(sql, new
            {
                Id = model.Id,
                Content = model.Content,
                User = model.User,
                OriginEventId = model.OriginEvent?.Id,
                EventTime = model.DateTime
            });

            if (affected == 0)
                throw new KeyNotFoundException($"ActionEvent Id={model.Id} not found.");

            return await FetchActionEventAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    /* --------------------------------------------------------------------
     * 5) DELETE ActionEvent
     * ------------------------------------------------------------------*/
    public async Task<bool> DeleteActionEventAsync(IActionEventModel model, CancellationToken token = default)
    {
        try
        {
            await using var conn = await OpenConnectionAsync(token);

            if (model.Id <= 0) throw new ArgumentOutOfRangeException(nameof(model.Id));

            const string sql = @"DELETE FROM ActionEvents WHERE Id = @Id;";
            int ret = await conn.ExecuteAsync(sql, new { Id = model.Id });

            _log?.Info(ret > 0
                ? $"DeleteActionEventAsync 완료 - Id={model.Id}"
                : $"ActionEvent 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
            throw;
        }
    }

    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool IsConnected =>
           _conn != null && _conn.State == ConnectionState.Open;
    #endregion
    #region - Attributes -
    private ILogService _log;
    private IEventAggregator _eventAggregator;
    private EventDbSetupModel _setup;
    private EventProvider _eventProvider;
    private DetectionEventProvider _detectionProvider;
    private ConnectionEventProvider _connectionProvider;
    private MalfunctionEventProvider _malfunctionProvider;
    private ActionEventProvider _actionProvider;
    private DeviceProvider _deviceProvider;
    private MySqlConnection _conn;
    private readonly SemaphoreSlim _processGate = new(1, 1);
    protected CancellationTokenSource? _cancellationTokenSource;

    #endregion

}

/* somewhere in EventDbService.cs 또는 별도 static class 파일 */
internal static class EventModelExtensions
{
    /// <summary>
    /// ExEvents INSERT 에 필요한 파라미터 객체로 변환
    /// </summary>
    public static object ToInsertParams(this IExEventModel m) => new
    {
        m.EventGroup,
        EventType = m.MessageType!.ToString(),
        DeviceId = (m.Device as BaseDeviceModel)?.Id,
        DeviceType = m.Device?.DeviceType.ToString(),
        Status = m.Status.ToString(),
        EventTime = m.DateTime
    };
}

/* ── 1. ExEvents ─────────────────────────────────────────── */

internal sealed class ExEventSQL
{
    public int Id { get; set; }
    public string? EventGroup { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? DeviceId { get; set; }
    public string? DeviceType { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EventTime { get; set; }

    public ExEventModel ToDomain() => new()
    {
        Id = Id,
        EventGroup = EventGroup,
        Device = new BaseDeviceModel() { Id = DeviceId ?? 0},
        MessageType = Enum.Parse<EnumEventType>(EventType),
        Status = Enum.Parse<EnumTrueFalse>(Status),
        DateTime = EventTime
        // Device 연결은 나중에 Resolve
    };
}

/* ── 2-1. DetectionEvents ─────────────────────────────────────────── */
internal sealed class DetectionSQL
{
    public int ExEventId { get; set; }
    public string Result { get; set; }

    public DetectionEventModel ToDomain(IExEventModel model)
    {
        var ev = new DetectionEventModel(model) { Result = Enum.Parse<EnumDetectionType>(Result)  };
        return ev;
    }
}

/* ── 2-2. ConnectionEvents (추가 컬럼 없음 → 빈 클래스) ─────────────── */
internal sealed class ConnectionSQL
{
    public int ExEventId { get; set; }

    public ConnectionEventModel ToDomain(IExEventModel model)
    {
        var ev = new ConnectionEventModel(model) {  };
        return ev;
    }
}

/* ── 2-3. MalfunctionEvents ───────────────────────────────────────── */
internal sealed class MalfunctionSQL
{
    public int ExEventId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int FirstStart { get; set; }
    public int FirstEnd { get; set; }
    public int SecondStart { get; set; }
    public int SecondEnd { get; set; }

    public MalfunctionEventModel ToDomain(IExEventModel model)
    {
        var ev = new MalfunctionEventModel(model) 
        {
            Reason = Enum.Parse<EnumFaultType>(Reason),
            FirstStart = FirstStart,
            FirstEnd = FirstEnd,
            SecondStart = SecondStart,
            SecondEnd = SecondEnd
        };
        return ev;
    }
}

/* ── 3. ActionEvents ───────────────────────────────────────── */
internal sealed record ActionSQL
{
    int ActionId { get; set; }
    string? Content { get; set; }
    string? User { get; set; }
    int? OriginEventId { get; set;}
    DateTime EventTime { get; set; }
    /* Row → Domain : origin 은 외부에서 주입 */
    public ActionEventModel ToDomain(IExEventModel? origin) => new()
    {
        Id = ActionId,
        Content = Content,
        User = User,
        DateTime = EventTime,
        OriginEvent = origin
    };
}