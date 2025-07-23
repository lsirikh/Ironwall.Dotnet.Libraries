using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Db.Models;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using MySql.Data.MySqlClient;
using System;
using Xunit;
using static Org.BouncyCastle.Asn1.Cmp.Challenge;

namespace Ironwall.Dotnet.Libraries.Events.Db.Tests;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 5:42:07 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// DeviceDbService 전용 Fixture – DB 한 번만 세팅 & 해제
/// </summary>
public sealed class EventDbFixture : IAsyncLifetime
{
    public IEventDbService Svc { get; private set; } = null!;
    public EventProvider EventProvider = new();
    public DetectionEventProvider DetectProvider = null!;
    public MalfunctionEventProvider MalfunctionProvider = null!;
    public ConnectionEventProvider ConnectionProvider = null!;
    public ActionEventProvider ActionProvider = null!;
    public DeviceProvider DeviceProvider = null;
    internal CancellationTokenSource Cts { get; } = new();

    private const int CtrlCount = 5;
    private const int SnsPerCtrl = 200;
    public int EventCount = 5000;

    public List<int> InsertedIds = new List<int>();

    const int PREVIOUS_DAY = -7;
    // EventDbFixture 내부
    internal int ActionEventCount = 5000;

    // Action 이벤트용
    internal List<int> InsertedActionIds { get; } = new();

    /* 테스트용 이벤트 테이블 목록 – 실제 존재하는 것만 나열 */
    private static readonly string[] _eventTables =
    {
        "ActionEvents",
        "MalfunctionEvents",
        "ConnectionEvents",
        "DetectionEvents",
        "ExEvents"
    };

    private readonly EventDbSetupModel _setup = new()
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

        DeviceProvider = new DeviceProvider();
        int conIndex = 1;
        int sensorIndex = 1;
        for (int c = 1; c <= CtrlCount; c++)
        {
            var ctrl = new ControllerDeviceModel
            {
                Id = conIndex++,
                DeviceGroup = c,
                DeviceNumber = 1,
                DeviceName = $"제어기_{c:00}",
                DeviceType = EnumDeviceType.Controller,
                Status = EnumDeviceStatus.ACTIVATED,
                IpAddress = $"192.168.{c}.1",
                Port = 9000 + c,
                Devices = new List<IBaseDeviceModel>()
            };
            DeviceProvider.Add(ctrl);
            for (int s = 1; s <= SnsPerCtrl; s++)
            {
                var sensor = new SensorDeviceModel
                {
                    Id = sensorIndex++,
                    DeviceGroup = c,
                    DeviceNumber = s,
                    DeviceName = $"펜스센서_{c:00}-{s:000}",
                    DeviceType = EnumDeviceType.Fence,
                    Status = EnumDeviceStatus.ACTIVATED,
                    Controller = ctrl
                };
                ctrl.Devices.Add(sensor);
                DeviceProvider.Add(sensor);
            }
            
        }

        // 세부 Provider는 (log, DevProvider) 주입
        DetectProvider = new DetectionEventProvider(log, EventProvider);
        MalfunctionProvider = new MalfunctionEventProvider(log, EventProvider);
        ConnectionProvider = new ConnectionEventProvider(log, EventProvider);
        ActionProvider = new ActionEventProvider(log, EventProvider);

        Svc = new EventDbService(
                log,
                ea,
                EventProvider,
                DetectProvider,
                MalfunctionProvider,
                ConnectionProvider,
                ActionProvider,
                DeviceProvider,
                _setup);

        //await DropTablesAsync();               // 깨끗한 DB 확보

        await Svc.StartService(Cts.Token);       // Connect + BuildScheme + FetchInstance
        Assert.True(Svc.IsConnected);
    }



    /* ───────── 2. DB 내부 테이블만 삭제 ────── */
    private async Task DropTablesAsync()
    {
        await Svc.Connect(Cts.Token);

        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            Database = _setup.DbDatabase,   // ← DB 유지
            SslMode = MySqlSslMode.Disabled
        };

        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();

        foreach (var t in _eventTables)
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS `{t}`;");

    }

    [Fact(DisplayName = "Dispose DB Service")]
    public async Task DisposeAsync()
    {
        await Svc.StopService(Cts.Token);
        
        await DropTablesAsync();

        if (!Cts.IsCancellationRequested)
            Cts.Cancel();

        Assert.False(Svc.IsConnected);
    }

    /*---------------------------------------------------------------------
     *  A. Detection 이벤트 10건 시드
     *-------------------------------------------------------------------*/

    public async Task SeedDetectionAsync()
    {
        /* 1) 시드 데이터 10건 삽입 */
        var sensorDict = DeviceProvider
                            .OfType<ISensorDeviceModel>()
                            .ToDictionary(s => s.Id);

        var date = DateTime.Now.AddDays(PREVIOUS_DAY);
        var random = new Random();
        for (int i = 1; i <= EventCount + 1; i++)
        {
            var controller = DeviceProvider.OfType<IControllerDeviceModel>().FirstOrDefault();
            date = date + TimeSpan.FromMinutes(random.Next(0, 5));
            var det = new DetectionEventModel
            {

                EventGroup = $"G{i % 2}",               // 두 그룹
                MessageType = EnumEventType.Intrusion,
                Status = EnumTrueFalse.True,
                DateTime = date,
                Result = EnumDetectionType.VIBRATION_SENSOR,                         // 고유 결과값
            };
            if (sensorDict.TryGetValue(i % 1000, out var sn))
                det.Device = sn;
            int id = await Svc.InsertDetectionEventAsync(det);
            InsertedIds.Add(id);
        }
    }

    /*---------------------------------------------------------------------
     *  A. Malfunction 이벤트 10건 시드
     *-------------------------------------------------------------------*/
    public async Task SeedMalfunctionAsync()
    {
        /* 1) 시드 데이터 10건 삽입 */
        var sensorDict = DeviceProvider
                            .OfType<ISensorDeviceModel>()
                            .ToDictionary(s => s.Id);

        var date = DateTime.Now.AddDays(PREVIOUS_DAY);
        var random = new Random();

        for (int i = 1; i <= EventCount + 1; i++)
        {
            date = date + TimeSpan.FromMinutes(random.Next(0, 5));
            var mal = new MalfunctionEventModel
            {
                EventGroup = $"M{i % 2}",                  // 두 그룹
                MessageType = EnumEventType.Fault,
                Status = EnumTrueFalse.True,
                DateTime = date,
                Reason = EnumFaultType.FAULT_FENCE,     // 임의 고정
                FirstStart = i,
                FirstEnd = i + 1,
                SecondStart = i + 2,
                SecondEnd = i + 3,
            };

            if (sensorDict.TryGetValue(i % 1000, out var sn))
                mal.Device = sn;

            int id = await Svc.InsertMalfunctionEventAsync(mal);
            InsertedIds.Add(id);                 // → 테스트용 List<int>
        }
    }

    /*---------------------------------------------------------------------
     *  B. Connection 이벤트 10건 시드
     *-------------------------------------------------------------------*/
    public async Task SeedConnectionAsync()
    {
        /* 1) 시드 데이터 10건 삽입 */
        var sensorDict = DeviceProvider
                            .OfType<ISensorDeviceModel>()
                            .ToDictionary(s => s.Id);

        var date = DateTime.Now.AddDays(PREVIOUS_DAY);
        var random = new Random();

        for (int i = 1; i <= EventCount + 1; i++)
        {
            date = date + TimeSpan.FromMinutes(random.Next(0, 5));
            var con = new ConnectionEventModel
            {
                EventGroup = $"C{i % 2}",                  // 두 그룹
                MessageType = EnumEventType.Connection,
                Status = EnumTrueFalse.True,
                DateTime = date,
            };

            if (sensorDict.TryGetValue(i % 1000, out var sn))
                con.Device = sn;

            int id = await Svc.InsertConnectionEventAsync(con);
            InsertedIds.Add(id);                  // → 테스트용 List<int>
        }
    }


    /*---------------------------------------------------------------------
     *  B. Action 이벤트 10건 시드
     *-------------------------------------------------------------------*/
    public async Task SeedActionAsync()
    {
        /* OriginEventId 로 최근 Detection 중 하나를 참조(없으면 null) */
        var anyDetection = InsertedIds.FirstOrDefault();  // Detection PK

        var date = DateTime.Now.AddDays(PREVIOUS_DAY);
        var random = new Random();

        for (int i = 1; i <= EventCount; i++)
        {
            date = date + TimeSpan.FromMinutes(random.Next(0, 5));
            var action = new ActionEventModel
            {
                Content = $"조치내역_{i:00}",
                User = "operator",
                DateTime = date,
                OriginEvent = anyDetection > 0
                                ? await Svc.FetchMalfunctionEventAsync(anyDetection+i-1)
                                //? await Svc.FetchDetectionEventAsync(anyDetection+i-1)
                                : null
            };
            int id = await Svc.InsertActionEventAsync(action);
            InsertedActionIds.Add(id);
        }
    }
}

/// xUnit 컬렉션
[CollectionDefinition(nameof(EventBulkCollection))]
public sealed class EventBulkCollection : ICollectionFixture<EventDbFixture> { }

[Collection(nameof(EventBulkCollection))]
public class EventDb_DetectionCrudTests
{
    private readonly EventDbFixture _fx;
    public EventDb_DetectionCrudTests(EventDbFixture fx) => _fx = fx;

    /*──────────────────────────── 01. Fetch & Insert  ───────────────────────────*/

    [Fact(DisplayName = "DetectionEvents – Insert & Fetch")]
    public async Task Insert_And_Fetch_Detection_Events()
    {

        await _fx.SeedDetectionAsync();

        /* 1) FetchDetectionEventsAsync → 전체 개수 일치 */
        var all = await _fx.Svc.FetchDetectionEventsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.EventCount);

        /* 3) 각각 FetchDetectionEventAsync 로 필드 검증 */
        foreach (var id in _fx.InsertedIds)
        {
            var one = await _fx.Svc.FetchDetectionEventAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.Equal(EnumEventType.Intrusion, one.MessageType);
            Assert.Equal(EnumDetectionType.VIBRATION_SENSOR, one.Result);   // Result = i
            Assert.NotNull(one.Device);
        }
    }

    /*──────────────────────────── 02. Update  ───────────────────────────*/
    [Fact(DisplayName = "DetectionEvents – Update")]
    public async Task Update_Detection_Event_Works()
    {
        var all = await _fx.Svc.FetchDetectionEventsAsync();
        var det = all.FirstOrDefault();
        /* 수정 */
        det.EventGroup = "UPDATED";
        det.Status = EnumTrueFalse.False;
        det.Result = EnumDetectionType.PIR_SENSOR;

        var after = await _fx.Svc.UpdateDetectionEventAsync(det);

        Assert.NotNull(after);
        Assert.Equal(det.Id, after!.Id);
        Assert.Equal("UPDATED", after.EventGroup);
        Assert.Equal(EnumTrueFalse.False, after.Status);
        Assert.Equal(EnumDetectionType.PIR_SENSOR, after.Result);
    }

    /*──────────────────────────── 03. Delete  ───────────────────────────*/
    [Fact(DisplayName = "DetectionEvents – Delete")]
    public async Task Delete_Detection_Event_Works()
    {
        /* 삽입 */
        var all = await _fx.Svc.FetchDetectionEventsAsync();
        var det = all.FirstOrDefault();

        /* 삭제 */
        bool ok = await _fx.Svc.DeleteDetectionEventAsync(det);
        Assert.True(ok);

        /* 실제로 사라졌는지 확인 */
        var fetched = await _fx.Svc.FetchDetectionEventAsync(det.Id);
        Assert.Null(fetched);
    }
}

[Collection(nameof(EventBulkCollection))]          // EventDbFixture 공유
public class EventDb_MalfunctionCrudTests
{
    private readonly EventDbFixture _fx;
    public EventDb_MalfunctionCrudTests(EventDbFixture fx) => _fx = fx;

    /*────────────────────────── 01. Insert & Fetch ─────────────────────────*/
    [Fact(DisplayName = "MalfunctionEvents – Insert & Fetch")]
    public async Task Insert_And_Fetch_Malfunction_Events()
    {
        await _fx.SeedMalfunctionAsync();

        /* ② FetchAll */
        var all = await _fx.Svc.FetchMalfunctionEventsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.EventCount);

        /* ③ FetchSingle & 검증 */
        foreach (var id in _fx.InsertedIds)
        {
            var one = await _fx.Svc.FetchMalfunctionEventAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.Equal(EnumFaultType.FAULT_FENCE, one.Reason);
            Assert.NotNull(one.Device);            // Device 매핑 확인(있다면)
        }
    }

    /*────────────────────────── 02. Update ─────────────────────────*/
    [Fact(DisplayName = "MalfunctionEvents – Update")]
    public async Task Update_Malfunction_Event_Works()
    {
        await _fx.SeedMalfunctionAsync();

        var mal = (await _fx.Svc.FetchMalfunctionEventsAsync())!.First();

        /* 수정 */
        mal.EventGroup = "UPDATED";
        mal.Status = EnumTrueFalse.False;
        mal.Reason = EnumFaultType.FAULT_CABLE_CUTTING;

        var after = await _fx.Svc.UpdateMalfunctionEventAsync(mal);

        Assert.NotNull(after);
        Assert.Equal("UPDATED", after!.EventGroup);
        Assert.Equal(EnumTrueFalse.False, after.Status);
        Assert.Equal(EnumFaultType.FAULT_CABLE_CUTTING, after.Reason);
    }

    /*────────────────────────── 03. Delete ─────────────────────────*/
    [Fact(DisplayName = "MalfunctionEvents – Delete")]
    public async Task Delete_Malfunction_Event_Works()
    {
        await _fx.SeedMalfunctionAsync();

        var mal = (await _fx.Svc.FetchMalfunctionEventsAsync())!.First();
        Assert.True(await _fx.Svc.DeleteMalfunctionEventAsync(mal));

        var fetched = await _fx.Svc.FetchMalfunctionEventAsync(mal.Id);
        Assert.Null(fetched);
    }
}

/*======================================================================
 *  EventDb_ConnectionCrudTests
 *  ● Connection 이벤트의 Insert / Fetch / Update / Delete 검증
 *====================================================================*/

[Collection(nameof(EventBulkCollection))]
public class EventDb_ConnectionCrudTests
{
    private readonly EventDbFixture _fx;
    public EventDb_ConnectionCrudTests(EventDbFixture fx) => _fx = fx;

    /*────────────────────────── 01. Insert & Fetch ─────────────────────────*/
    [Fact(DisplayName = "ConnectionEvents – Insert & Fetch")]
    public async Task Insert_And_Fetch_Connection_Events()
    {
        await _fx.SeedConnectionAsync();

        /* ② FetchAll */
        var all = await _fx.Svc.FetchConnectionEventsAsync();
        Assert.NotNull(all);
        Assert.True(all!.Count >= _fx.EventCount);

        /* ③ FetchSingle & 검증 */
        foreach (var id in _fx.InsertedIds)
        {
            var one = await _fx.Svc.FetchConnectionEventAsync(id);
            Assert.NotNull(one);
            Assert.Equal(id, one!.Id);
            Assert.Equal(EnumEventType.Connection, one.MessageType);
        }
    }

    /*────────────────────────── 02. Update ─────────────────────────*/
    [Fact(DisplayName = "ConnectionEvents – Update")]
    public async Task Update_Connection_Event_Works()
    {
        await _fx.SeedConnectionAsync();

        var con = (await _fx.Svc.FetchConnectionEventsAsync())!.First();

        /* 수정 */
        con.EventGroup = "UPDATED";
        con.Status = EnumTrueFalse.False;

        var after = await _fx.Svc.UpdateConnectionEventAsync(con);

        Assert.NotNull(after);
        Assert.Equal("UPDATED", after!.EventGroup);
        Assert.Equal(EnumTrueFalse.False, after.Status);
    }

    /*────────────────────────── 03. Delete ─────────────────────────*/
    [Fact(DisplayName = "ConnectionEvents – Delete")]
    public async Task Delete_Connection_Event_Works()
    {
        await _fx.SeedConnectionAsync();

        var con = (await _fx.Svc.FetchConnectionEventsAsync())!.First();
        Assert.True(await _fx.Svc.DeleteConnectionEventAsync(con));

        var fetched = await _fx.Svc.FetchConnectionEventAsync(con.Id);
        Assert.Null(fetched);
    }

    [Collection(nameof(EventBulkCollection))]
    public class EventDb_ActionCrudTests
    {
        private readonly EventDbFixture _fx;
        public EventDb_ActionCrudTests(EventDbFixture fx) => _fx = fx;

        /*────────────────────────── 01. Insert & Fetch ─────────────────────────*/
        [Fact(DisplayName = "ActionEvents – Insert & Fetch")]
        public async Task Insert_And_Fetch_Action_Events()
        {
            /* ① 시드(Action) – Detection 시드가 먼저 있어야 OriginEventId 를 참조할 수 있다 */
            //await _fx.SeedDetectionAsync();   // OriginEvent 생성 (10건)
            await _fx.SeedMalfunctionAsync();   // OriginEvent 생성 (10건)
            //await _fx.SeedConnectionAsync();   // OriginEvent 생성 (10건)
            await _fx.SeedActionAsync();      // ActionEvent 10건 삽입  → InsertedActionIds 채움

            /* ② FetchAll */
            var all = await _fx.Svc.FetchActionEventsAsync();
            Assert.NotNull(all);
            Assert.True(all!.Count >= _fx.EventCount);          // 최소 10건

            /* ③ FetchSingle & 필드 검증 */
            foreach (var id in _fx.InsertedActionIds)
            {
                var one = await _fx.Svc.FetchActionEventAsync(id);
                Assert.NotNull(one);
                Assert.Equal(id, one!.Id);
                Assert.Equal(EnumEventType.Action, one.MessageType); // Action 모델 생성자에서 고정
                /* OriginEvent (탐지) 연결 여부 확인 */
                if (one.OriginEvent != null)
                {
                    Assert.Equal(EnumEventType.Intrusion, one.OriginEvent.MessageType);
                    Assert.NotNull(one.OriginEvent.Device);
                }
            }
        }

        /*────────────────────────── 02. Update ─────────────────────────*/
        [Fact(DisplayName = "ActionEvents – Update")]
        public async Task Update_Action_Event_Works()
        {
            await _fx.SeedDetectionAsync();
            await _fx.SeedActionAsync();

            /* 임의 1건 */
            var act = (await _fx.Svc.FetchActionEventsAsync())!.First();

            /* 수정 */
            act.Content = "UPDATED_CONTENT";
            act.User = "tester";
            act.DateTime = DateTime.UtcNow.AddMinutes(-10);

            var after = await _fx.Svc.UpdateActionEventAsync(act);

            Assert.NotNull(after);
            Assert.Equal("UPDATED_CONTENT", after!.Content);
            Assert.Equal("tester", after.User);
            Assert.Equal(act.Id, after.Id);
        }

        /*────────────────────────── 03. Delete ─────────────────────────*/
        [Fact(DisplayName = "ActionEvents – Delete")]
        public async Task Delete_Action_Event_Works()
        {
            await _fx.SeedDetectionAsync();
            await _fx.SeedActionAsync();

            var act = (await _fx.Svc.FetchActionEventsAsync())!.First();

            Assert.True(await _fx.Svc.DeleteActionEventAsync(act));

            var fetched = await _fx.Svc.FetchActionEventAsync(act.Id);
            Assert.Null(fetched);
        }
    }
}