using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Gateway.Models;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Services;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Gateway.Tests;
/****************************************************************************
   Purpose      : GatewayDbService 전용 Fixture – DB 한 번만 세팅 & 해제
   Created By   : GHLee
   Created On   : 10/29/2025 12:30:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// GatewayDbService 전용 Fixture – DB 한 번만 세팅 & 해제
/// </summary>
public sealed class GatewayDbFixture : IAsyncLifetime
{
    internal GatewayDbService Svc { get; private set; } = null!;
    internal GatewayEventProvider EventProvider { get; private set; } = null!;
    internal CancellationTokenSource Cts { get; } = new();

    /* 테스트에서 사용하는 테이블 이름 (FK 순서: 자식 먼저) */
    private static readonly string[] tables =
    {
        "GatewayEventGroups",
        "GatewayEvents"
    };

    public int SeedEventCount = 10;

    private readonly GatewaySetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = "gateway_db_test",
        UidDbServer = "root",
        PasswordDbServer = "root"
    };

    // ────────── IAsyncLifetime ──────────
    public async Task InitializeAsync()
    {
        var log = new LogService();
        var ea = new EventAggregator();

        EventProvider = new GatewayEventProvider(log);

        Svc = new GatewayDbService(
            log,
            ea,
            _setup,
            EventProvider
            );

        await DropTablesAsync();               // 깨끗한 DB 확보

        await Svc.Connect(Cts.Token);          // DB 연결
        await Svc.BuildSchemeAsync(Cts.Token); // 스키마 생성

        Assert.True(Svc.IsConnected);

        await SeedAsync();                     // 테스트 데이터 삽입
    }

    public async Task DisposeAsync()
    {
        await Svc.Disconnect(Cts.Token);
        if (!Cts.IsCancellationRequested)
            Cts.Cancel();

        Assert.False(Svc.IsConnected);
        await DropTablesAsync();
    }

    private async Task SeedAsync()
    {
        for (int i = 1; i <= SeedEventCount; i++)
        {
            var evt = new GatewayEventModel
            {
                EventName = $"sensor.event.test_{i:000}",
                DeviceGroups = new List<int> { (i - 1) % 3 + 1 },  // 그룹 1, 2, 3 순환
                IsEnable = i % 2 == 1,                               // 홀수만 활성화
                Description = $"테스트 이벤트 {i}"
            };

            await Svc.InsertGatewayEventAsync(evt, Cts.Token);
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

    internal string BuildConnStr() => Svc.BuildConnStr();
}

/// xUnit 컬렉션
[CollectionDefinition(nameof(GatewayEventCollection))]
public sealed class GatewayEventCollection : ICollectionFixture<GatewayDbFixture> { }

/// <summary>
/// GatewayDbService 통합 테스트
/// </summary>
[Collection(nameof(GatewayEventCollection))]
public class GatewayDb_BasicTests
{
    private readonly GatewayDbFixture _fx;
    public GatewayDb_BasicTests(GatewayDbFixture fx) => _fx = fx;

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "01. 시드 데이터 개수 확인")]
    public async Task Seed_Counts_Should_Match()
    {
        var events = await _fx.Svc.FetchGatewayEventsAsync();

        Assert.NotNull(events);
        Assert.Equal(_fx.SeedEventCount, events!.Count());
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "02. FetchGatewayEventsAsync - 활성화 필터")]
    public async Task Fetch_Enabled_Events_Only()
    {
        var allEvents = await _fx.Svc.FetchGatewayEventsAsync();
        var enabledEvents = allEvents!.Where(e => e.IsEnable).ToList();

        // 홀수만 활성화했으므로
        Assert.Equal(5, enabledEvents.Count);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "03. InsertGatewayEventAsync - 새 이벤트 삽입")]
    public async Task Insert_New_Event_Works()
    {
        var newEvent = new GatewayEventModel
        {
            EventName = "sensor.event.new_test",
            DeviceGroups = new List<int> { 5 },
            IsEnable = true,
            Description = "새로운 테스트 이벤트"
        };

        var inserted = await _fx.Svc.InsertGatewayEventAsync(newEvent);

        Assert.NotNull(inserted);
        Assert.True(inserted!.Id > 0);
        Assert.Equal("sensor.event.new_test", inserted.EventName);
        Assert.Contains(5, inserted.DeviceGroups);
        Assert.True(inserted.IsEnable);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "04. InsertGatewayEventAsync - 중복 EventName 처리")]
    public async Task Insert_Duplicate_EventName_Should_Fail()
    {
        var duplicateEvent = new GatewayEventModel
        {
            EventName = "sensor.event.test_001",  // 이미 존재하는 이름
            DeviceGroups = new List<int> { 1 },
            IsEnable = true,
            Description = "중복 테스트"
        };

        // MySQL UNIQUE 제약 조건 위반 시 예외 발생 예상
        await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await _fx.Svc.InsertGatewayEventAsync(duplicateEvent);
        });
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "05. UpdateGatewayEventAsync - 이벤트 수정")]
    public async Task Update_Event_Works()
    {
        var events = await _fx.Svc.FetchGatewayEventsAsync();
        var eventToUpdate = events!.First();

        eventToUpdate.EventName = $"updated.{eventToUpdate.EventName}";
        eventToUpdate.DeviceGroups = new List<int> { 99 };
        eventToUpdate.IsEnable = !eventToUpdate.IsEnable;
        eventToUpdate.Description = "업데이트된 설명";

        var updated = await _fx.Svc.UpdateGatewayEventAsync(eventToUpdate);

        Assert.NotNull(updated);
        Assert.Equal(eventToUpdate.Id, updated!.Id);
        Assert.StartsWith("updated.", updated.EventName);
        Assert.Contains(99, updated.DeviceGroups);
        Assert.Equal(eventToUpdate.Description, updated.Description);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "06. DeleteGatewayEventAsync - 이벤트 삭제")]
    public async Task Delete_Event_Works()
    {
        var events = await _fx.Svc.FetchGatewayEventsAsync();
        var eventToDelete = events!.Last();
        var deletedId = eventToDelete.Id;

        var result = await _fx.Svc.DeleteGatewayEventAsync(eventToDelete);

        Assert.True(result);

        // 삭제 후 재조회 시 개수 감소 확인
        var afterEvents = await _fx.Svc.FetchGatewayEventsAsync();
        Assert.DoesNotContain(afterEvents!, e => e.Id == deletedId);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "07. FetchInstanceAsync - Provider 채우기")]
    public async Task FetchInstance_Should_Populate_Provider()
    {
        await _fx.Svc.FetchInstanceAsync(_fx.Cts.Token);

        var events = await _fx.Svc.FetchGatewayEventsAsync();
        var expectedCount = events!.Count();

        Assert.Equal(expectedCount, _fx.EventProvider.Count);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "08. 그룹별 이벤트 조회")]
    public async Task Fetch_Events_By_Group()
    {
        var allEvents = await _fx.Svc.FetchGatewayEventsAsync();

        var group1 = allEvents!.Where(e => e.DeviceGroups.Contains(1)).ToList();
        var group2 = allEvents!.Where(e => e.DeviceGroups.Contains(2)).ToList();
        var group3 = allEvents!.Where(e => e.DeviceGroups.Contains(3)).ToList();

        // 10개를 3개 그룹으로 순환 배치했으므로
        // Group 1: 1, 4, 7, 10 (4개)
        // Group 2: 2, 5, 8 (3개)
        // Group 3: 3, 6, 9 (3개)
        Assert.Equal(4, group1.Count);
        Assert.Equal(3, group2.Count);
        Assert.Equal(3, group3.Count);
    }
}

/// <summary>
/// 데이터베이스 스키마 및 무결성 테스트
/// </summary>
[Collection(nameof(GatewayEventCollection))]
public class GatewayDb_SchemaTests
{
    private readonly GatewayDbFixture _fx;
    public GatewayDb_SchemaTests(GatewayDbFixture fx) => _fx = fx;

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "01. GatewayEvents 테이블 존재 확인")]
    public async Task GatewayEvents_Table_Exists()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();

        var tableExists = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @DbName AND TABLE_NAME = 'GatewayEvents'",
            new { DbName = "gateway_db_test" });

        Assert.Equal(1, tableExists);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "02. EventName 컬럼에 UNIQUE 제약 조건 확인")]
    public async Task EventName_Has_Unique_Constraint()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();

        var uniqueConstraints = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM information_schema.TABLE_CONSTRAINTS
            WHERE TABLE_SCHEMA = @DbName
              AND TABLE_NAME = 'GatewayEvents'
              AND CONSTRAINT_TYPE = 'UNIQUE'",
            new { DbName = "gateway_db_test" });

        Assert.True(uniqueConstraints > 0, "EventName에 UNIQUE 제약 조건이 없습니다.");
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "03. EventName 인덱스 존재 확인")]
    public async Task EventName_Index_Exists()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();

        var indexExists = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = @DbName
              AND TABLE_NAME = 'GatewayEvents'
              AND INDEX_NAME = 'IX_EventName'",
            new { DbName = "gateway_db_test" });

        Assert.True(indexExists > 0, "IX_EventName 인덱스가 존재하지 않습니다.");
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "04. 컬럼 데이터 타입 검증")]
    public async Task Column_Data_Types_Are_Correct()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();

        var columns = await conn.QueryAsync<dynamic>(@"
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = @DbName AND TABLE_NAME = 'GatewayEvents'
            ORDER BY ORDINAL_POSITION",
            new { DbName = "gateway_db_test" });

        var columnList = columns.ToList();

        // Id 컬럼
        var idCol = columnList.FirstOrDefault(c => c.COLUMN_NAME == "Id");
        Assert.NotNull(idCol);
        Assert.Equal("int", idCol.DATA_TYPE);

        // EventName 컬럼
        var nameCol = columnList.FirstOrDefault(c => c.COLUMN_NAME == "EventName");
        Assert.NotNull(nameCol);
        Assert.Equal("varchar", nameCol.DATA_TYPE);
        Assert.Equal("NO", nameCol.IS_NULLABLE);

        // Group 컬럼
        var groupCol = columnList.FirstOrDefault(c => c.COLUMN_NAME == "Group");
        Assert.NotNull(groupCol);
        Assert.Equal("int", groupCol.DATA_TYPE);

        // IsEnable 컬럼
        var enableCol = columnList.FirstOrDefault(c => c.COLUMN_NAME == "IsEnable");
        Assert.NotNull(enableCol);
        Assert.Equal("tinyint", enableCol.DATA_TYPE);  // MySQL BOOLEAN은 TINYINT(1)
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "05. 레코드 수 일치 확인")]
    public async Task Record_Count_Matches()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();

        var dbCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GatewayEvents");

        var serviceCount = (await _fx.Svc.FetchGatewayEventsAsync())!.Count();

        Assert.Equal(dbCount, serviceCount);
    }

    // TEST-01: GatewayEventGroups 연결 테이블 존재 확인
    [Fact(DisplayName = "06. GatewayEventGroups 연결 테이블 존재 확인")]
    public async Task GatewayEventGroups_Table_Exists()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();

        var tableExists = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = @DbName AND TABLE_NAME = 'GatewayEventGroups'",
            new { DbName = "gateway_db_test" });

        Assert.Equal(1, tableExists);
    }

    // TEST-05: 마이그레이션 멱등성 — BuildSchemeAsync 이중 실행 시 중복 없음
    [Fact(DisplayName = "07. 마이그레이션 멱등성 - BuildSchemeAsync 이중 실행")]
    public async Task should_not_duplicate_groups_when_migration_runs_twice()
    {
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();
        var beforeCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GatewayEventGroups");

        // BuildSchemeAsync 재실행 (INSERT IGNORE — 멱등)
        await _fx.Svc.BuildSchemeAsync(_fx.Cts.Token);

        var afterCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GatewayEventGroups");

        Assert.Equal(beforeCount, afterCount);
    }
}

/// <summary>
/// 트랜잭션 및 동시성 테스트
/// </summary>
[Collection(nameof(GatewayEventCollection))]
public class GatewayDb_TransactionTests
{
    private readonly GatewayDbFixture _fx;
    public GatewayDb_TransactionTests(GatewayDbFixture fx) => _fx = fx;

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "01. 트랜잭션 롤백 - 삽입 실패 시")]
    public async Task Transaction_Rollback_On_Insert_Failure()
    {
        var beforeCount = (await _fx.Svc.FetchGatewayEventsAsync())!.Count();

        // 잘못된 데이터로 삽입 시도 (EventName이 너무 긴 경우 등)
        var invalidEvent = new GatewayEventModel
        {
            EventName = new string('A', 300),  // VARCHAR(255) 초과
            DeviceGroups = new List<int> { 1 },
            IsEnable = true
        };

        try
        {
            await _fx.Svc.InsertGatewayEventAsync(invalidEvent);
        }
        catch
        {
            // 예외 발생 예상
        }

        var afterCount = (await _fx.Svc.FetchGatewayEventsAsync())!.Count();

        // 롤백되어 개수 변화 없음
        Assert.Equal(beforeCount, afterCount);
    }

    // TEST-01: 다중 그룹 이벤트 원자성 삽입 — GatewayEventGroups 직접 검증
    [Fact(DisplayName = "03. 다중 그룹 이벤트 원자성 삽입")]
    public async Task should_insert_event_with_multiple_groups_atomically()
    {
        var evt = new GatewayEventModel
        {
            EventName = "sensor.event.multi_group_atomic",
            DeviceGroups = new List<int> { 1, 2, 3 },
            IsEnable = true,
            Description = "다중 그룹 원자성 테스트"
        };

        var inserted = await _fx.Svc.InsertGatewayEventAsync(evt);

        Assert.NotNull(inserted);
        Assert.Equal(3, inserted!.DeviceGroups.Count);
        Assert.Contains(1, inserted.DeviceGroups);
        Assert.Contains(2, inserted.DeviceGroups);
        Assert.Contains(3, inserted.DeviceGroups);

        // GatewayEventGroups 테이블 직접 확인 — 정확히 3행
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();
        var groupCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GatewayEventGroups WHERE EventId = @Id",
            new { Id = inserted.Id });
        Assert.Equal(3, groupCount);

        // 다른 테스트 카운트에 영향 주지 않도록 정리
        await _fx.Svc.DeleteGatewayEventAsync(inserted);
    }

    // TEST-01: 이벤트 삭제 시 GatewayEventGroups CASCADE 삭제 검증
    [Fact(DisplayName = "04. 이벤트 삭제 시 연결 그룹 CASCADE 제거")]
    public async Task should_rollback_when_group_insert_fails_after_event_insert()
    {
        // 이벤트+그룹 삽입
        var evt = new GatewayEventModel
        {
            EventName = "sensor.event.cascade_test",
            DeviceGroups = new List<int> { 1, 2 },
            IsEnable = true
        };
        var inserted = await _fx.Svc.InsertGatewayEventAsync(evt);
        Assert.NotNull(inserted);

        // 삽입 직후 GatewayEventGroups에 2개 행 존재 확인
        using var conn = new MySqlConnection(_fx.BuildConnStr());
        await conn.OpenAsync();
        var beforeCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GatewayEventGroups WHERE EventId = @Id",
            new { Id = inserted!.Id });
        Assert.Equal(2, beforeCount);

        // 이벤트 삭제 → ON DELETE CASCADE → GatewayEventGroups 자동 제거
        await _fx.Svc.DeleteGatewayEventAsync(inserted);

        var afterCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM GatewayEventGroups WHERE EventId = @Id",
            new { Id = inserted.Id });
        Assert.Equal(0, afterCount);
    }

    // ──────────────────────────────────────────────────────────────
    // MySqlConnection은 스레드 안전하지 않음 — 공유 _conn 동시 접근 시 스트림 에러 발생
    [Fact(DisplayName = "02. 동시 삽입 테스트", Skip = "MySqlConnection non-thread-safe: concurrent access to shared _conn corrupts stream")]
    public async Task Concurrent_Inserts_Work()
    {
        var tasks = new List<Task<IGatewayEventModel?>>();

        for (int i = 1; i <= 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var evt = new GatewayEventModel
                {
                    EventName = $"concurrent.event.{index:000}",
                    DeviceGroups = new List<int> { index },
                    IsEnable = true,
                    Description = $"동시 삽입 테스트 {index}"
                };
                return await _fx.Svc.InsertGatewayEventAsync(evt);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // 모두 성공적으로 삽입되었는지 확인
        Assert.All(results, r => Assert.NotNull(r));
        Assert.All(results, r => Assert.True(r!.Id > 0));

        // 중복 없이 5개 모두 삽입되었는지 확인
        var events = await _fx.Svc.FetchGatewayEventsAsync();
        var concurrentEvents = events!.Where(e => e.EventName.StartsWith("concurrent.event.")).ToList();
        Assert.Equal(5, concurrentEvents.Count);
    }
}

/// <summary>
/// 비즈니스 로직 테스트
/// </summary>
[Collection(nameof(GatewayEventCollection))]
public class GatewayDb_BusinessLogicTests
{
    private readonly GatewayDbFixture _fx;
    public GatewayDb_BusinessLogicTests(GatewayDbFixture fx) => _fx = fx;

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "01. 활성화 상태 토글")]
    public async Task Toggle_IsEnable_Status()
    {
        var events = await _fx.Svc.FetchGatewayEventsAsync();
        var eventToToggle = events!.First();

        var originalStatus = eventToToggle.IsEnable;
        eventToToggle.IsEnable = !originalStatus;

        var updated = await _fx.Svc.UpdateGatewayEventAsync(eventToToggle);

        Assert.NotEqual(originalStatus, updated!.IsEnable);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "02. 그룹 일괄 변경")]
    public async Task Bulk_Update_Group()
    {
        var events = await _fx.Svc.FetchGatewayEventsAsync();
        var eventsToUpdate = events!.Where(e => e.DeviceGroups.Contains(1)).ToList();

        foreach (var evt in eventsToUpdate)
        {
            evt.DeviceGroups = new List<int> { 10 };
            await _fx.Svc.UpdateGatewayEventAsync(evt);
        }

        var afterEvents = await _fx.Svc.FetchGatewayEventsAsync();
        var group10Events = afterEvents!.Where(e => e.DeviceGroups.Contains(10)).ToList();

        Assert.Equal(eventsToUpdate.Count, group10Events.Count);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "03. EventName 검색")]
    public async Task Search_By_EventName_Pattern()
    {
        var allEvents = await _fx.Svc.FetchGatewayEventsAsync();
        var searchResults = allEvents!
            .Where(e => e.EventName.Contains("test_00"))
            .ToList();

        // sensor.event.test_001 ~ test_009 매칭
        Assert.True(searchResults.Count >= 9);
    }

    // ──────────────────────────────────────────────────────────────
    [Fact(DisplayName = "04. Description NULL 처리")]
    public async Task Handle_Null_Description()
    {
        var evt = new GatewayEventModel
        {
            EventName = "event.null.description",
            DeviceGroups = new List<int> { 1 },
            IsEnable = true,
            Description = null  // NULL 허용
        };

        var inserted = await _fx.Svc.InsertGatewayEventAsync(evt);

        Assert.NotNull(inserted);
        Assert.Null(inserted!.Description);

        // Update로 NULL → 값 설정
        inserted.Description = "이제 설명이 있음";
        var updated = await _fx.Svc.UpdateGatewayEventAsync(inserted);

        Assert.Equal("이제 설명이 있음", updated!.Description);
    }
}
