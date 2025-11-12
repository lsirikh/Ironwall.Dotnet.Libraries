using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Db.Models;
using Ironwall.Dotnet.Libraries.Devices.Db.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using MySql.Data.MySqlClient;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Db.Tests;
/****************************************************************************
   Purpose      : DeviceDbService 전용 Fixture – DB 한 번만 세팅 & 해제                                                          
   Created By   : GHLee                                                
   Created On   : 5/27/2025 5:28:11 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/


/// <summary>
/// DeviceDbService 전용 Fixture – DB 한 번만 세팅 & 해제
/// </summary>
public sealed class DeviceDbFixture : IAsyncLifetime
{
    internal DeviceDbService Svc { get; private set; } = null!;
    internal DeviceProvider DevProvider = new();
    internal ControllerDeviceProvider CtrlProvider = null!;
    internal SensorDeviceProvider SnsProvider = null!;
    internal CameraDeviceProvider CamProvider = null!;


    internal CancellationTokenSource Cts { get; } = new();

    /* 테스트에서 사용하는 테이블 이름만 나열 */
    private static readonly string[] tables =
    {
        "CameraDevices",      // FK 테이블 먼저 삭제
        "SensorDevices",
        "ControllerDevices",
        "Devices"             // 부모 테이블 마지막 삭제
    };


    public int CtrlCount = 3;
    public int SnsPerCtrl = 80;
    public int CameraCount = 0;

    private readonly DeviceDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        //DbDatabase = "monitor_db_test",
        DbDatabase = "monitor_db",
        UidDbServer = "root",
        PasswordDbServer = "root"
    };

    // ────────── IAsyncLifetime ──────────
    [Fact(DisplayName = "Initialize DB Service")]
    public async Task InitializeAsync()
    {

        var log = new LogService();
        var ea = new EventAggregator();

        // 세부 Provider는 (log, DevProvider) 주입
        CtrlProvider = new ControllerDeviceProvider(log, DevProvider);
        SnsProvider = new SensorDeviceProvider(log, DevProvider);
        CamProvider = new CameraDeviceProvider(log, DevProvider); // 카메라 Provider 추가

        Svc = new DeviceDbService(
                log,
                ea,
                DevProvider,
                CtrlProvider,
                SnsProvider,
                CamProvider,
                _setup);

        await DropTablesAsync();               // 깨끗한 DB 확보


        await Svc.StartService(Cts.Token);       // Connect + BuildScheme + FetchInstance
        await SeedAsync();                       // 5×200 Bulk Insert
        Assert.True(Svc.IsConnected);
    }

    [Fact(DisplayName = "Dispose DB Service")]
    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        if (!Cts.IsCancellationRequested)
            Cts.Cancel();

        Assert.False(Svc.IsConnected);
        await DropTablesAsync();
    }

    private async Task SeedAsync()
    {
        // 1. Controller + Sensor 생성
        for (int c = 1; c <= CtrlCount; c++)
        {
            var ctrl = new ControllerDeviceModel
            {
                DeviceGroup = c,
                DeviceNumber = 1,
                DeviceName = $"제어기_{c:00}",
                DeviceType = EnumDeviceType.Controller,
                Status = EnumDeviceStatus.ACTIVATED,
                IpAddress = $"192.168.{c}.1",
                Port = 9000 + c,
                Devices = new List<IBaseDeviceModel>()
            };

            for (int s = 1; s <= SnsPerCtrl; s++)
            {
                ctrl.Devices.Add(new SensorDeviceModel
                {
                    DeviceGroup = c,
                    DeviceNumber = s,
                    DeviceName = $"펜스센서_{c:00}-{s:000}",
                    DeviceType = EnumDeviceType.Fence,
                    Status = EnumDeviceStatus.ACTIVATED,
                    Controller = ctrl
                });
            }

            await Svc.InsertControllerAsync(ctrl, Cts.Token);
        }

        // 2. Camera 생성
        for (int cam = 1; cam <= CameraCount; cam++)
        {
            // 카메라 타입을 순환적으로 할당 (int 값으로 계산 후 enum으로 변환)
            int typeIndex = cam % 5;
            EnumCameraType cameraType = typeIndex switch
            {
                0 => EnumCameraType.THERMAL,
                1 => EnumCameraType.FIXED,
                2 => EnumCameraType.PTZ,
                3 => EnumCameraType.FISHEYES,
                4 => EnumCameraType.NONE,
                _ => EnumCameraType.FIXED
            };

            // 카메라 모드를 순환적으로 할당 (int 값으로 계산 후 enum으로 변환)
            int modeIndex = cam % 4;
            EnumCameraMode cameraMode = modeIndex switch
            {
                0 => EnumCameraMode.ETC,
                1 => EnumCameraMode.ONVIF,
                2 => EnumCameraMode.EMSTONE_API,
                3 => EnumCameraMode.INNODEP_API,
                4 => EnumCameraMode.NONE,
                _ => EnumCameraMode.ONVIF
            };

            var camera = new CameraDeviceModel
            {
                DeviceGroup = (cam - 1) / 10 + 100, // 100, 101, 102, 103... 그룹으로 분산
                DeviceNumber = ((cam - 1) % 10) + 1,  // 각 그룹 내에서 1~10 번호
                DeviceName = $"IP카메라_{cam:000}_{cameraType}",
                DeviceType = EnumDeviceType.IpCamera,
                Version = "v2.0.1",
                Status = cam % 7 == 0 ? EnumDeviceStatus.ERROR : EnumDeviceStatus.ACTIVATED, // 7번째마다 ERROR
                IpAddress = $"192.168.200.{cam}",
                Port = cameraType == EnumCameraType.THERMAL ? 80 : 8554, // 열화상 카메라는 다른 포트
                Username = "admin",
                Password = "sensorway123",
                RtspUri = $"rtsp://192.168.200.{cam}/stream1",
                RtspPort = cameraType == EnumCameraType.FISHEYES ? 8554 : 554, // 어안 카메라는 다른 RTSP 포트
                Mode = cameraMode,
                Category = cameraType
            };

            await Svc.InsertCameraAsync(camera, Cts.Token);
        }
    }

    private async Task DropTablesAsync()
    {
        /* 1단계: DB 없으면 생성해 두기 (부트스트랩 연결) */
        var bootstrap = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            SslMode = MySqlSslMode.Disabled
        };
        await using (var conn = new MySqlConnection(bootstrap.ToString()))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync($@"
            CREATE DATABASE IF NOT EXISTS `{_setup.DbDatabase}`
            CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci;");
        }

        /* 2단계: DB 포함 연결로 테이블 DROP */
        bootstrap.Database = _setup.DbDatabase;
        await using var dbConn = new MySqlConnection(bootstrap.ToString());
        await dbConn.OpenAsync();

        // 💡 외래키 체크 비활성화
        await dbConn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");

        foreach (var t in tables)
            await dbConn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");

        // 💡 외래키 체크 재활성화
        await dbConn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1;");
    }
}

/// xUnit 컬렉션
[CollectionDefinition(nameof(DeviceBulkCollection))]
public sealed class DeviceBulkCollection : ICollectionFixture<DeviceDbFixture> { }

/// <summary>
/// 5×200 데이터 기준 DeviceDbService 통합 테스트
/// </summary>
[Collection(nameof(DeviceBulkCollection))]
public class DeviceDb_BulkTests
{
    private readonly DeviceDbFixture _fx;
    public DeviceDb_BulkTests(DeviceDbFixture fx) => _fx = fx;

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "01. 시드 데이터 개수 확인")]
    public async Task Seed_Counts_Should_Match()
    {
        var ctrls = await _fx.Svc.FetchControllersAsync();
        var sensors = await _fx.Svc.FetchSensorsAsync();

        Assert.Equal(_fx.CtrlCount, ctrls!.Count);
        Assert.Equal(_fx.CtrlCount * _fx.SnsPerCtrl, sensors!.Count);

        // 부모-자식 연결 무결성
        Assert.All(ctrls, c => Assert.Equal(_fx.SnsPerCtrl, c.Devices!.Count));
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "02. FetchControllerAsync / FetchSensorAsync")]
    public async Task Fetch_Single_Entities()
    {
        // 임의 Controller 1개
        var anyCtrl = (await _fx.Svc.FetchControllersAsync())!.First();
        var fetchedC = await _fx.Svc.FetchControllerAsync(anyCtrl.Id);
        Assert.Equal(anyCtrl.DeviceName, fetchedC!.DeviceName);
        Assert.Equal(_fx.SnsPerCtrl, fetchedC.Devices!.Count);

        // 임의 Sensor 1개
        var anySensorId = ((SensorDeviceModel)fetchedC.Devices!.First()).Id;
        var fetchedS = await _fx.Svc.FetchSensorAsync(anySensorId);
        Assert.Equal(anyCtrl.Id, fetchedS!.Controller!.Id);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "03. UpdateControllerAsync")]
    public async Task Update_Controller_Works()
    {
        var ctrl = (await _fx.Svc.FetchControllersAsync())!.First();
        ctrl.DeviceName = "변경된_제어기";
        ctrl.Status = EnumDeviceStatus.ERROR;

        var after = await _fx.Svc.UpdateControllerAsync(ctrl);
        Assert.Equal("변경된_제어기", after!.DeviceName);
        Assert.Equal(EnumDeviceStatus.ERROR, after.Status);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "04. DeleteControllerAsync (Cascade)")]
    public async Task Delete_Controller_Cascade_Sensors()
    {
        var ctrl = (await _fx.Svc.FetchControllersAsync())!.First();
        var firstSensorId = ((SensorDeviceModel)ctrl.Devices!.First()).Id;

        Assert.True(await _fx.Svc.DeleteControllerAsync(ctrl));          // 삭제

        Assert.Null(await _fx.Svc.FetchControllerAsync(ctrl.Id));        // 부모 Gone
        Assert.Null(await _fx.Svc.FetchSensorAsync(firstSensorId));      // 자식 Gone
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "05. FetchInstanceAsync → DeviceProvider 채우기")]
    public async Task FetchInstance_Should_Populate_DeviceProvider()
    {
        await _fx.Svc.FetchInstanceAsync();   // 최신 캐시 로드

        var ctrlCnt = (await _fx.Svc.FetchControllersAsync())!.Count;
        var snsCnt = (await _fx.Svc.FetchSensorsAsync())!.Count;
        int expected = ctrlCnt + snsCnt;

        Assert.Equal(expected, _fx.DevProvider.Count);
        Assert.Equal(ctrlCnt, _fx.CtrlProvider.Count);   // 수정된 로직(Provider 비움)
        Assert.Equal(snsCnt, _fx.SnsProvider.Count);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "06. 정규화된 스키마 데이터 확인")]
    public async Task Normalized_Schema_Data_Integrity()
    {
        // Devices 테이블 직접 확인
        using var conn = new MySqlConnection(_fx.Svc.BuildConnStr());
        await conn.OpenAsync();

        // 실제 시드 데이터의 DeviceType에 맞춰 수정
        var deviceCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Devices WHERE DeviceType IN ('Controller', 'Fence')");

        var controllerSpecCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM ControllerDevices");

        var sensorSpecCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SensorDevices");

        // Controller 5개 + Sensor(Fence) 500개 (5 * 100)
        Assert.Equal(_fx.CtrlCount + _fx.CtrlCount * _fx.SnsPerCtrl, deviceCount);
        Assert.Equal(_fx.CtrlCount, controllerSpecCount);
        Assert.Equal(_fx.CtrlCount * _fx.SnsPerCtrl, sensorSpecCount);

        // 추가 검증: FK 관계 무결성 확인
        var orphanSensors = await conn.ExecuteScalarAsync<int>(@"
        SELECT COUNT(*) 
        FROM SensorDevices s 
        LEFT JOIN ControllerDevices c ON s.ControllerId = c.Id 
        WHERE c.Id IS NULL");

        Assert.Equal(0, orphanSensors); // 고아 센서가 없어야 함
    }
}

[Collection(nameof(DeviceBulkCollection))]
public class CameraDeviceDbTests
{
    private readonly DeviceDbFixture _fx;
    public CameraDeviceDbTests(DeviceDbFixture fx) => _fx = fx;

    [Fact(DisplayName = "01. Camera Insert → Fetch → Update → Delete")]
    public async Task Camera_CRUD_Works_Correctly()
    {
        var insertedCameras = new List<ICameraDeviceModel>();

        for (int i = 1; i <= 30; i++)
        {
            var camera = new CameraDeviceModel
            {
                DeviceGroup = 1,
                DeviceNumber = i,
                DeviceName = $"카메라_{i:00}",
                DeviceType = EnumDeviceType.IpCamera,
                Version = "v1.0",
                Status = EnumDeviceStatus.ACTIVATED,
                IpAddress = $"192.168.100.{i}",
                Port = 8554,
                Username = "admin",
                Password = "sensorway1",
                RtspUri = $"rtsp://192.168.100.{i}/stream1",
                RtspPort = 554,
                Mode = EnumCameraMode.ONVIF,
                Category = EnumCameraType.FIXED
            };

            var inserted = await _fx.Svc.InsertCameraAsync(camera);
            Assert.NotNull(inserted);
            Assert.True(inserted!.Id > 0);
            insertedCameras.Add(inserted);
        }

        var fetched = await _fx.Svc.FetchCameraAsync(insertedCameras.FirstOrDefault().Id);
        Assert.Equal("카메라_01", fetched!.DeviceName);
        Assert.Equal("192.168.100.1", fetched.IpAddress);

        fetched.DeviceName = "업데이트된 카메라";
        fetched.Category = EnumCameraType.PTZ;
        var updated = await _fx.Svc.UpdateCameraAsync(fetched);
        Assert.Equal("업데이트된 카메라", updated!.DeviceName);
        Assert.Equal(EnumCameraType.PTZ, updated.Category);

        var deleted = await _fx.Svc.DeleteCameraAsync(updated);
        Assert.True(deleted);

    }

    [Fact(DisplayName = "02. Fetch All Cameras")]
    public async Task Fetch_All_Cameras()
    {
        var cameras = await _fx.Svc.FetchCamerasAsync();
        Assert.NotNull(cameras);
        Assert.True(cameras!.Count >= 0);  // 최소 0개 이상
    }
}