using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Db.Models;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using log4net;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Data;
using ZstdSharp;

namespace Ironwall.Dotnet.Libraries.Devices.Db.Services;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/26/2025 11:06:45 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class DeviceDbService : TaskService, IDeviceDbService
{

    #region - Ctors -
    public DeviceDbService(ILogService log
                            , IEventAggregator eventAggregator
                            , DeviceProvider deviceProvider
                            , ControllerDeviceProvider controllerProvider
                            , SensorDeviceProvider sensorProvider
                            , CameraDeviceProvider cameraProvider
                            , DeviceDbSetupModel setupModel)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _setup = setupModel;
        _deviceProvider = deviceProvider;
        _sensorProvider = sensorProvider;
        _controllerProvider = controllerProvider;
        _cameraProvider = cameraProvider;
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
    public async Task StartService(CancellationToken token = default)
    {
        try
        {
            await Connect(token);
            await BuildSchemeAsync(token);
            await FetchInstanceAsync(token);

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
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");

            // ControllerDevices
            var createControllerTable = @"
                            CREATE TABLE IF NOT EXISTS `ControllerDevices` (
                                `Id`            INT AUTO_INCREMENT PRIMARY KEY,
                                `DeviceNumber`  INT          NOT NULL,
                                `DeviceGroup`   INT          NOT NULL,
                                `DeviceName`    VARCHAR(255),
                                `DeviceType`    VARCHAR(20)  NOT NULL,   -- EnumDeviceType.ToString()
                                `Version`       VARCHAR(50),
                                `Status`        VARCHAR(15)  NOT NULL,   -- EnumDeviceStatus.ToString()
                                `IpAddress`     VARCHAR(45)  NOT NULL,
                                `IpPort`        SMALLINT     NOT NULL,
                                `CreatedAt`     DATETIME     DEFAULT CURRENT_TIMESTAMP,
                                `UpdatedAt`     DATETIME     DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                UNIQUE KEY `UK_Controller_Device` (`DeviceGroup`,`DeviceNumber`)
                            );";

            // ─────────────────── SensorDevices ───────────────────────────────
            var createSensorTable = @"
                            CREATE TABLE IF NOT EXISTS `SensorDevices` (
                                `Id`            INT AUTO_INCREMENT PRIMARY KEY,
                                `ControllerId`  INT          NOT NULL,
                                `DeviceNumber`  INT          NOT NULL,
                                `DeviceGroup`   INT          NOT NULL,
                                `DeviceName`    VARCHAR(255),
                                `DeviceType`    VARCHAR(20)  NOT NULL,   -- EnumDeviceType.ToString()
                                `Version`       VARCHAR(50),
                                `Status`        VARCHAR(15)  NOT NULL,   -- EnumDeviceStatus.ToString()
                                `CreatedAt`     DATETIME     DEFAULT CURRENT_TIMESTAMP,
                                `UpdatedAt`     DATETIME     DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                CONSTRAINT `FK_Sensor_Controller`
                                    FOREIGN KEY (`ControllerId`) REFERENCES `ControllerDevices` (`Id`)
                                    ON DELETE CASCADE,
                                INDEX `IX_Sensor_Controller` (`ControllerId`)
                            );";

            var createCameraTableSql = @"
                            CREATE TABLE IF NOT EXISTS `CameraDevices` (
                                `Id`              INT AUTO_INCREMENT PRIMARY KEY,
                                `DeviceNumber`    INT NOT NULL,
                                `DeviceGroup`     INT NOT NULL,
                                `DeviceName`      VARCHAR(255),
                                `DeviceType`      VARCHAR(20) NOT NULL,
                                `Version`         VARCHAR(50),
                                `Status`          VARCHAR(15) NOT NULL,
                                `IpAddress`       VARCHAR(45) NOT NULL,
                                `IpPort`          SMALLINT NOT NULL,
                                `Username`        VARCHAR(100) NOT NULL,
                                `Password`        VARCHAR(255) NOT NULL,
                                `RtspUri`         VARCHAR(255),
                                `RtspPort`        SMALLINT,
                                `Mode`            VARCHAR(20),
                                `Category`        VARCHAR(20),
                                `CreatedAt`       DATETIME DEFAULT CURRENT_TIMESTAMP,
                                `UpdatedAt`      DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                                UNIQUE KEY `UK_Camera_Device` (`DeviceGroup`, `DeviceNumber`)
                            );";


            await _conn.ExecuteAsync(createControllerTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "ControllerDevices DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createSensorTable);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "SensorDevices DB테이블을 생성합니다..." });
            await _conn.ExecuteAsync(createCameraTableSql);
            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "CameraDevices DB테이블을 생성합니다..." });

            _log?.Info("테이블 생성/확인 완료.");
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
        }
    }

    public async Task FetchInstanceAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("DB 연결이 이루어지지 않았습니다.");


            var controllers = await FetchControllersAsync(token);
            _deviceProvider.Clear();
            _controllerProvider.Clear();
            if (controllers?.Any() != false)
                foreach (var item in controllers.OfType<IControllerDeviceModel>())
                {
                    _deviceProvider.Add(item);
                }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "ControllerProvider의 정보를 모두 불러왔습니다..." });

            var sensors = await FetchSensorsAsync(token);
            _sensorProvider.Clear();
            if (sensors?.Any() != false)
                foreach (var item in sensors.OfType<ISensorDeviceModel>())
                {
                    _deviceProvider.Add(item);
                }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "SensorProvider의 정보를 모두 불러왔습니다..." });

            // FetchInstanceAsync에 Camera 장치 로딩 추가
            var cameras = await FetchCamerasAsync(token);
            _cameraProvider.Clear();
            if (cameras?.Any() != false)
                foreach (var cam in cameras)
                {
                    _deviceProvider.Add(cam);
                }

            if (_eventAggregator != null)
                await _eventAggregator.PublishOnUIThreadAsync(new SplashScreenMessage() { Title = this.GetType().Name.ToString(), Message = "CameraProvider의 정보를 모두 불러왔습니다..." });
        }
        catch (Exception ex)
        {
            _log?.Error($"Raised {nameof(Exception)} : {ex}");
            throw;
        }
    }

    /// <summary>
    /// ControllerDevices + SensorDevices 전체를 읽어 Controller-centric 구조로 반환
    /// </summary>
    public async Task<List<IControllerDeviceModel>?> FetchControllersAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
                            SELECT * FROM ControllerDevices;
                            SELECT * FROM SensorDevices;";

            using var multi = await _conn.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: token));

            var ctrlRows = (await multi.ReadAsync<ControllerSQL>()).ToList();
            var snsRows = (await multi.ReadAsync<SensorSQL>()).ToList();

            var dict = ctrlRows
                       .Select(r => r.ToDomain())
                       .ToDictionary(c => c.Id);

            foreach (var s in snsRows)
            {
                if (!dict.TryGetValue(s.ControllerId, out var parent))
                    continue;

                var sensor = s.ToDomain();
                sensor.Controller = parent;

                parent.Devices ??= new List<IBaseDeviceModel>();
                parent.Devices.Add(sensor);
            }

            var list = dict.Values.Cast<IControllerDeviceModel>().ToList();
            _log?.Info($"FetchControllersAsync 완료 - Controllers {list.Count}, Sensors {snsRows.Count()}");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchControllersAsync Error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// ControllerDevices 1건 + 연결된 SensorDevices 모두 로드
    /// </summary>
    /// <param name="model">
    ///   식별 키
    ///   • model.Id   > 0 → PK로 조회  
    ///   • else model.DeviceGroup + DeviceNumber 로 조회
    /// </param>
    public async Task<IControllerDeviceModel?> FetchControllerAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            if (!(id > 0)) throw new Exception($"Input parameter \"id\" is 0, which is not acceptable.");

            const string sql = @"
                            SELECT * FROM ControllerDevices WHERE Id = @Id;
                            SELECT * FROM SensorDevices    WHERE ControllerId = @Id;";

            using var multi = await _conn.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: token));

            var ctrlRow = await multi.ReadFirstOrDefaultAsync<ControllerSQL>();
            if (ctrlRow == null) return null;

            var controller = ctrlRow.ToDomain();
            controller.Devices = new List<IBaseDeviceModel>();

            foreach (var sRow in await multi.ReadAsync<SensorSQL>())
            {
                var sensor = sRow.ToDomain();
                sensor.Controller = controller;
                controller.Devices.Add(sensor);
            }

            _log?.Info($"FetchControllerAsync 완료 - ControllerId={controller.Id}, SensorCount={controller.Devices.Count}");
            return controller;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchControllerAsync Error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="model"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<int> InsertControllerAsync(IControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
                    INSERT INTO ControllerDevices
                    (DeviceNumber, DeviceGroup, DeviceName, DeviceType, Version, Status, IpAddress, IpPort)
                    VALUES (@DeviceNumber,@DeviceGroup,@DeviceName,@DeviceType,@Version,@Status,@IpAddress,@Port);
                    SELECT LAST_INSERT_ID();";

            var id = await _conn.ExecuteScalarAsync<int>(sql, new
            {
                model.DeviceNumber,
                model.DeviceGroup,
                model.DeviceName,
                DeviceType = model.DeviceType.ToString(),
                model.Version,
                Status = model.Status.ToString(),
                model.IpAddress,
                Port = model.Port
            });

            model.Id = id;

            // 하위 센서 동시 저장
            if (model.Devices is { Count: > 0 })
            {
                foreach (ISensorDeviceModel s in model.Devices)
                    await InsertSensorAsync(s, token);
            }
            return id;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertControllerAsync Error: {ex}");
            throw;
        }

    }

    public async Task<IControllerDeviceModel?> UpdateControllerAsync(IControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            // 기본 필드 업데이트
            const string sql = @"
                        UPDATE ControllerDevices SET
                            DeviceNumber = @DeviceNumber,
                            DeviceGroup  = @DeviceGroup,
                            DeviceName   = @DeviceName,
                            DeviceType   = @DeviceType,
                            Version      = @Version,
                            Status       = @Status,
                            IpAddress    = @IpAddress,
                            IpPort       = @Port
                        WHERE Id = @Id;";


            if (!(model.Id > 0)) throw new Exception($"Input parameter \"id\" is 0, which is not acceptable.");
            int affected = await _conn.ExecuteAsync(sql, new
            {
                model.Id,
                model.DeviceNumber,
                model.DeviceGroup,
                model.DeviceName,
                DeviceType = model.DeviceType.ToString(),
                model.Version,
                Status = model.Status.ToString(),
                model.IpAddress,
                Port = model.Port
            });
            if (affected == 0) return null;

            _log?.Info($"UpdateControllerAsync 완료 - Id={model.Id}, Rows={affected}");

            // 변경 후 최신 레코드 반환
            return await FetchControllerAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateControllerAsync Error: {ex}");
            throw;
        }
    }

    public async Task<bool> DeleteControllerAsync(IControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"DELETE FROM ControllerDevices WHERE Id = @Id;";

            int ret = await _conn.ExecuteAsync(sql, new { model.Id });

            if (ret > 0)
                _log?.Info($"DeleteControllerAsync 완료 - Rows={ret}");
            else
                _log?.Warning($"DeleteControllerAsync 대상 없음 - Controller");

            // FK ON DELETE CASCADE 로 센서 자동 삭제
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteControllerAsync Error: {ex}");
            throw;
        }
    }

    /// <summary>
    /// SensorDevices 전체를 읽어 완전한 네비게이션(센서 → 컨트롤러 → 형제 센서)까지
    /// 채운 리스트를 반환한다. 왕복은 1회만 사용.
    /// </summary>
    public async Task<List<ISensorDeviceModel>?> FetchSensorsAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            // ── 다중 결과셋 한 번에 받기 ─────────────────────────────────────
            const string sql = @"
                        SELECT * FROM ControllerDevices;
                        SELECT * FROM SensorDevices;";

            using var multi = await _conn.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: token));

            // 1. 컨트롤러 행들 → 도메인 + 딕셔너리
            var ctrlDict = (await multi.ReadAsync<ControllerSQL>())
                           .Select(r => r.ToDomain())
                           .ToDictionary(c => c.Id);

            // 2. 센서 행들
            var sensors = new List<ISensorDeviceModel>();
            var sibRows = (await multi.ReadAsync<SensorSQL>()).ToList();

            foreach (var sRow in sibRows)
            {
                // 도메인 변환
                var sensor = sRow.ToDomain();

                // 부모 연결
                if (ctrlDict.TryGetValue(sRow.ControllerId, out var parent))
                {
                    sensor.Controller = parent;
                    parent.Devices ??= new List<IBaseDeviceModel>();
                    parent.Devices.Add(sensor);          // 형제까지 모두 채워짐
                }
                else
                {
                    _log?.Warning($"[FetchSensors] ControllerId={sRow.ControllerId} 누락");
                }

                sensors.Add(sensor);
            }

            _log?.Info($"FetchSensorsAsync 완료 - SensorCount={sensors.Count}, ControllerCount={ctrlDict.Count}");
            return sensors;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchSensorsAsync Error: {ex}");
            throw;
        }

    }

    /// <summary>
    /// SensorDevices 단일 행 + 부모 Controller 연결
    /// </summary>
    /// <param name="id">SensorDevices.Id (PK)</param>
    public async Task<ISensorDeviceModel?> FetchSensorAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");


            const string sql = @"
                            SELECT * FROM SensorDevices WHERE Id = @Id;
                            SELECT * FROM ControllerDevices WHERE Id = (
                                SELECT ControllerId FROM SensorDevices WHERE Id = @Id
                            );
                            SELECT * FROM SensorDevices WHERE ControllerId = (
                                SELECT ControllerId FROM SensorDevices WHERE Id = @Id
                            );";

            using var multi = await _conn.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: token));

            var sRow = await multi.ReadFirstOrDefaultAsync<SensorSQL>();
            var cRow = await multi.ReadFirstOrDefaultAsync<ControllerSQL>();
            var sibRows = (await multi.ReadAsync<SensorSQL>()).ToList();  // 형제 센서

            if (sRow == null || cRow == null) throw new Exception($"Controller or Sensor records were not found, matched with ControllerId of SensorSQL");

            var controller = cRow.ToDomain();
            controller.Devices = new List<IBaseDeviceModel>();

            foreach (var sib in sibRows)
            {
                var sn = sib.ToDomain();
                sn.Controller = controller;
                controller.Devices.Add(sn);
                if (sn.Id == id)          // 요청한 센서 찾기
                    sRow = sib;
            }

            var sensor = controller.Devices.OfType<SensorDeviceModel>()
                                           .First(s => s.Id == id);

            _log?.Info($"FetchSensorAsync 완료 - SensorId={sensor.Id}, ControllerId={sensor.Controller?.Id}");
            return sensor;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchSensorAsync Error: {ex}");
            return null;
        }

    }

    /// <summary>
    /// 센서 단일 저장(FK 필요)
    /// </summary>
    public async Task<ISensorDeviceModel?> InsertSensorAsync(ISensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            const string sql = @"
                    INSERT INTO SensorDevices
                    (ControllerId, DeviceNumber, DeviceGroup, DeviceName, DeviceType, Version, Status)
                    VALUES (@ControllerId, @DeviceNumber, @DeviceGroup, @DeviceName, @DeviceType, @Version, @Status);
                    SELECT LAST_INSERT_ID();";

            if (model.Controller == null) throw new Exception($"Sensor doesn't have a controller instance.");

            var id = await _conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
            {
                ControllerId = model.Controller!.Id,
                model.DeviceNumber,
                model.DeviceGroup,
                model.DeviceName,
                DeviceType = model.DeviceType.ToString(),
                model.Version,
                Status = model.Status.ToString()
            }, cancellationToken: token));

            model.Id = id;
            return model;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertSensorAsync Error: {ex}");
            throw;
        }

    }

    public async Task<ISensorDeviceModel?> UpdateSensorAsync(ISensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            if (model.Id <= 0)
                throw new Exception("Sensor.Id is not set.");

            if (model.Controller?.Id is null or <= 0)
                throw new Exception("Sensor.Controller.Id is missing.");

            const string sql = @"
                            UPDATE SensorDevices SET
                                ControllerId = @ControllerId,
                                DeviceNumber = @DeviceNumber,
                                DeviceGroup  = @DeviceGroup,
                                DeviceName   = @DeviceName,
                                DeviceType   = @DeviceType,
                                Version      = @Version,
                                Status       = @Status
                            WHERE Id = @Id;";

            var affected = await _conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                model.Id,
                ControllerId = model.Controller!.Id,
                model.DeviceNumber,
                model.DeviceGroup,
                model.DeviceName,
                DeviceType = model.DeviceType.ToString(),
                model.Version,
                Status = model.Status.ToString()
            }, cancellationToken: token));

            if (affected == 0) return null;

            _log?.Info($"UpdateSensorAsync 완료 - Id={model.Id}, Rows={affected}");
            return await FetchSensorAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateSensorAsync Error: {ex}");
            throw;
        }
    }

    public async Task<bool> DeleteSensorAsync(ISensorDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            if (model.Id <= 0)
                throw new Exception("Sensor.Id is not set.");

            const string sql = @"DELETE FROM SensorDevices WHERE Id = @Id;";
            var ret = await _conn.ExecuteAsync(new CommandDefinition(sql, new { model.Id }, cancellationToken: token));

            if (ret > 0)
                _log?.Info($"DeleteSensorAsync 완료 - Id={model.Id}");
            else
                _log?.Warning($"DeleteSensorAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteSensorAsync Error: {ex}");
            throw;
        }
    }

    public async Task<List<ICameraDeviceModel>?> FetchCamerasAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = "SELECT * FROM CameraDevices;";
            var rows = (await _conn.QueryAsync<CameraSQL>(sql)).ToList();
            var list = rows.Select(r => (ICameraDeviceModel)r.ToDomain()).ToList();

            _log?.Info($"FetchCamerasAsync 완료 - Cameras {list.Count}");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchCamerasAsync Error: {ex}");
            throw;
        }

    }

    public async Task<ICameraDeviceModel?> FetchCameraAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            if (!(id > 0)) throw new Exception($"Input parameter \"id\" is 0, which is not acceptable.");

            const string sql = "SELECT * FROM CameraDevices WHERE Id = @Id;";
            var row = await _conn.QueryFirstOrDefaultAsync<CameraSQL>(sql, new { Id = id });
            if (row == null) throw new NullReferenceException();

            var item = row?.ToDomain();
            _log?.Info($"FetchCameraAsync 완료 - CameraId={item.Id}");
            return item;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchCameraAsync Error: {ex}");
            return null;
        }
    }

    public async Task<ICameraDeviceModel?> InsertCameraAsync(ICameraDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
                    INSERT INTO CameraDevices
                    (DeviceNumber, DeviceGroup, DeviceName, DeviceType, Version, Status, IpAddress, IpPort, Username, Password, RtspUri, RtspPort, Mode, Category)
                    VALUES
                    (@DeviceNumber, @DeviceGroup, @DeviceName, @DeviceType, @Version, @Status, @IpAddress, @IpPort, @Username, @Password, @RtspUri, @RtspPort, @Mode, @Category);
                    SELECT LAST_INSERT_ID();";

            var id = await _conn.ExecuteScalarAsync<int>(sql, new
            {
                model.DeviceNumber,
                model.DeviceGroup,
                model.DeviceName,
                DeviceType = model.DeviceType.ToString(),
                model.Version,
                Status = model.Status.ToString(),
                model.IpAddress,
                IpPort = model.Port,
                Username = model.Username,
                Password = model.Password,
                model.RtspUri,
                model.RtspPort,
                Mode = model.Mode.ToString(),
                Category = model.Category.ToString()
            });

            model.Id = id;
            return model;
        }
        catch (Exception ex)
        {
            _log?.Error($"InsertCameraAsync Error: {ex}");
            return null;
        }

    }

    public async Task<ICameraDeviceModel?> UpdateCameraAsync(ICameraDeviceModel model, CancellationToken token = default)
    {
        try
        {

            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
                    UPDATE CameraDevices SET
                        DeviceNumber = @DeviceNumber,
                        DeviceGroup  = @DeviceGroup,
                        DeviceName   = @DeviceName,
                        DeviceType   = @DeviceType,
                        Version      = @Version,
                        Status       = @Status,
                        IpAddress    = @IpAddress,
                        IpPort       = @IpPort,
                        Username     = @Username,
                        Password     = @Password,
                        RtspUri      = @RtspUri,
                        RtspPort     = @RtspPort,
                        Mode         = @Mode,
                        Category     = @Category
                    WHERE Id = @Id;";

            if (!(model.Id > 0)) throw new Exception($"Input parameter \"id\" is 0, which is not acceptable.");

            int affected = await _conn.ExecuteAsync(sql, new
            {
                model.Id,
                model.DeviceNumber,
                model.DeviceGroup,
                model.DeviceName,
                DeviceType = model.DeviceType.ToString(),
                model.Version,
                Status = model.Status.ToString(),
                model.IpAddress,
                IpPort = model.Port,
                Username = model.Username,
                Password = model.Password,
                model.RtspUri,
                model.RtspPort,
                Mode = model.Mode.ToString(),
                Category = model.Category.ToString()
            });

            if (affected == 0) return null;

            _log?.Info($"UpdateCameraAsync 완료 - Id={model.Id}, Rows={affected}");

            return await FetchCameraAsync(model.Id, token);
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateCameraAsync Error: {ex}");
            return null;
        }

    }

    public async Task<bool> DeleteCameraAsync(ICameraDeviceModel model, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");


            const string sql = "DELETE FROM CameraDevices WHERE Id = @Id;";
            int ret = await _conn.ExecuteAsync(sql, new { model.Id });

            if (ret > 0)
                _log?.Info($"DeleteCameraAsync 완료 - Id={model.Id}");
            else
                _log?.Warning($"DeleteCameraAsync 대상 없음 - Id={model.Id}");

            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteCameraAsync Error: {ex}");
            return false;
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
    private DeviceDbSetupModel _setup;
    private DeviceProvider _deviceProvider;
    private SensorDeviceProvider _sensorProvider;
    private ControllerDeviceProvider _controllerProvider;
    private CameraDeviceProvider _cameraProvider;
    private MySqlConnection _conn;
    #endregion

}

//#POCO(Plain Old CLR Object)
//  필드/속성만 갖는 순수 데이터 홀더
//  프레임워크 의존 코드(엔티티베이스, INotifyPropertyChanged 등) 없음
//#DB-POCO
//  DB 레코드 구조에 1:1 대응하도록 만든 POCO
//  컬럼명·타입 그대로 보존해 Dapper/ADO.NET이 자동 매핑하기 쉽도록 설계

internal sealed class ControllerSQL
{
    public int Id { get; set; }
    public int DeviceNumber { get; set; }
    public int DeviceGroup { get; set; }
    public string? DeviceName { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int IpPort { get; set; }

    public ControllerDeviceModel ToDomain() => new()
    {
        Id = Id,
        DeviceNumber = DeviceNumber,
        DeviceGroup = DeviceGroup,
        DeviceName = DeviceName,
        DeviceType = Enum.Parse<EnumDeviceType>(DeviceType),
        Version = Version,
        Status = Enum.Parse<EnumDeviceStatus>(Status),
        IpAddress = IpAddress,
        Port = IpPort
    };
}

internal sealed class SensorSQL
{
    public int Id { get; set; }
    public int ControllerId { get; set; }
    public int DeviceNumber { get; set; }
    public int DeviceGroup { get; set; }
    public string? DeviceName { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Status { get; set; } = string.Empty;

    public SensorDeviceModel ToDomain() => new()
    {
        Id = Id,
        DeviceNumber = DeviceNumber,
        DeviceGroup = DeviceGroup,
        DeviceName = DeviceName,
        DeviceType = Enum.Parse<EnumDeviceType>(DeviceType),
        Version = Version,
        Status = Enum.Parse<EnumDeviceStatus>(Status),
    };
}

internal sealed class CameraSQL
{
    public int Id { get; set; }
    public int DeviceNumber { get; set; }
    public int DeviceGroup { get; set; }
    public string? DeviceName { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int IpPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? RtspUri { get; set; }
    public int RtspPort { get; set; }
    public string? Mode { get; set; }
    public string? Category { get; set; }

    public CameraDeviceModel ToDomain() => new()
    {
        Id = Id,
        DeviceNumber = DeviceNumber,
        DeviceGroup = DeviceGroup,
        DeviceName = DeviceName,
        DeviceType = Enum.Parse<EnumDeviceType>(DeviceType),
        Version = Version,
        Status = Enum.Parse<EnumDeviceStatus>(Status),
        IpAddress = IpAddress,
        Port = IpPort,
        Username = Username,
        Password = Password,
        RtspUri = RtspUri,
        RtspPort = RtspPort,
        Mode = Enum.TryParse(Mode, out EnumCameraMode m) ? m : EnumCameraMode.NONE,
        Category = Enum.TryParse(Category, out EnumCameraType c) ? c : EnumCameraType.NONE
    };
}