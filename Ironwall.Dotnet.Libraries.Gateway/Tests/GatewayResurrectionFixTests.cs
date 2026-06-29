using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Gateway.Models;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Services;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Gateway.Tests;
/****************************************************************************
   Purpose      : 레거시 Group 컬럼 '부활(resurrection)' 근본수정 통합 테스트
                  (GatewayEvent_Group_Resurrection_Fix PRD — FR-01/02/03, NFR-01/02)
   Created By   : GHLee
   Created On   : 6/30/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 레거시 단일 <c>GatewayEvents.Group</c> 스키마를 시드한 뒤 <see cref="GatewayDbService.BuildSchemeAsync"/>의
/// 1회성 마무리(이행→좀비정리→DROP)가 올바르게 동작하는지 검증한다.
/// <para>테스트 클래스가 <see cref="IAsyncLifetime"/>를 직접 구현하므로 xUnit이 테스트마다 새 인스턴스를 만들고
/// InitializeAsync/DisposeAsync가 테스트별로 실행된다 → 매 테스트가 깨끗한 레거시 DB에서 격리 실행된다.</para>
/// 다른 픽스처(<c>gateway_db_test</c>)와 충돌하지 않도록 별도 DB <c>gateway_resurrection_test</c>를 쓴다.
/// </summary>
public class GatewayResurrectionFixTests : IAsyncLifetime   // ← 라이프사이클 메서드에 [Fact] 금지
{
    private const string DbName = "gateway_resurrection_test";

    private readonly GatewaySetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = DbName,
        UidDbServer = "root",
        PasswordDbServer = "root"
    };

    private GatewayDbService _svc = null!;
    private readonly CancellationTokenSource _cts = new();

    // ────────── IAsyncLifetime — 레거시 스키마 + 시드 ──────────
    public async Task InitializeAsync()
    {
        await SeedLegacySchemaAsync();

        var log = new LogService();
        var ea = new EventAggregator();
        var provider = new GatewayEventProvider(log);
        _svc = new GatewayDbService(log, ea, _setup, provider);

        await _svc.Connect(_cts.Token);   // _conn 오픈 (BuildSchemeAsync는 각 테스트에서 호출)
    }

    public async Task DisposeAsync()
    {
        await _svc.Disconnect(_cts.Token);
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        await DropDatabaseAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // 레거시(N:N 이전) 스키마 = GatewayEvents 에 단일 `Group` 컬럼 + IX_Group 존재.
    // 시드:
    //   Event_A  Group=1  조인{1,116}  → 좀비 1 제거 기대 → {116}
    //   Event_B  Group=0  조인{117}    → 무영향        → {117}
    //   Event_C  Group=5  조인{5}      → 유일값 보존    → {5}
    //   Event_D  Group=7  조인{7}      → 레거시==사용자 보존 → {7}
    private async Task SeedLegacySchemaAsync()
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            SslMode = MySqlSslMode.Disabled
        };
        await using (var boot = new MySqlConnection(csb.ToString()))
        {
            await boot.OpenAsync();
            await boot.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{DbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci;");
        }

        csb.Database = DbName;
        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();

        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
        await conn.ExecuteAsync("DROP TABLE IF EXISTS `GatewayEventGroups`;");
        await conn.ExecuteAsync("DROP TABLE IF EXISTS `GatewayEvents`;");
        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1;");

        await conn.ExecuteAsync(@"
            CREATE TABLE `GatewayEvents` (
                `Id`          INT AUTO_INCREMENT PRIMARY KEY,
                `EventName`   VARCHAR(255) NOT NULL UNIQUE,
                `Group`       INT          NOT NULL DEFAULT 0,
                `IsEnable`    BOOLEAN      NOT NULL DEFAULT TRUE,
                `Description` TEXT,
                `CreatedAt`   DATETIME     DEFAULT CURRENT_TIMESTAMP,
                `UpdatedAt`   DATETIME     DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX `IX_EventName` (`EventName`),
                INDEX `IX_Group` (`Group`),
                INDEX `IX_IsEnable` (`IsEnable`)
            );");
        await conn.ExecuteAsync(@"
            CREATE TABLE `GatewayEventGroups` (
                `EventId`  INT NOT NULL,
                `GroupId`  INT NOT NULL,
                PRIMARY KEY (`EventId`, `GroupId`),
                CONSTRAINT `FK_GEG_Event` FOREIGN KEY (`EventId`) REFERENCES `GatewayEvents`(`Id`) ON DELETE CASCADE,
                INDEX `IX_GEG_GroupId` (`GroupId`)
            );");

        await conn.ExecuteAsync(@"
            INSERT INTO `GatewayEvents` (`Id`,`EventName`,`Group`) VALUES
              (1,'Event_A',1),(2,'Event_B',0),(3,'Event_C',5),(4,'Event_D',7);");
        await conn.ExecuteAsync(@"
            INSERT INTO `GatewayEventGroups` (`EventId`,`GroupId`) VALUES
              (1,1),(1,116),(2,117),(3,5),(4,7);");
    }

    private async Task DropDatabaseAsync()
    {
        var csb = new MySqlConnectionStringBuilder
        {
            Server = _setup.IpDbServer,
            Port = (uint)_setup.PortDbServer,
            UserID = _setup.UidDbServer,
            Password = _setup.PasswordDbServer,
            SslMode = MySqlSslMode.Disabled
        };
        await using var conn = new MySqlConnection(csb.ToString());
        await conn.OpenAsync();
        await conn.ExecuteAsync($"DROP DATABASE IF EXISTS `{DbName}`;");
    }

    private async Task<List<int>> GroupsOfAsync(string eventName)
    {
        var events = await _svc.FetchGatewayEventsAsync(_cts.Token);
        return events!.First(e => e.EventName == eventName).DeviceGroups.OrderBy(x => x).ToList();
    }

    private async Task<int> LegacyGroupColumnCountAsync()
    {
        await using var conn = new MySqlConnection(_svc.BuildConnStr());
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @Db AND TABLE_NAME = 'GatewayEvents' AND COLUMN_NAME = 'Group';",
            new { Db = DbName });
    }

    // ════════════════════════ Tests ════════════════════════

    [Fact(DisplayName = "부활수정 01. 그룹이 2개 이상인 이벤트의 레거시 좀비 제거 (FR-02)")]
    public async Task should_remove_legacy_group_zombie_when_event_has_multiple_groups()
    {
        await _svc.BuildSchemeAsync(_cts.Token);

        // Event_A: 레거시 1 + 사용자 116 → 좀비 1 제거 → {116}
        Assert.Equal(new List<int> { 116 }, await GroupsOfAsync("Event_A"));
    }

    [Fact(DisplayName = "부활수정 02. 레거시 Group=0 이벤트는 무영향 (FR-01)")]
    public async Task should_keep_groups_when_legacy_group_is_zero()
    {
        await _svc.BuildSchemeAsync(_cts.Token);

        // Event_B: Group=0 → 이행/정리 대상 아님 → {117}
        Assert.Equal(new List<int> { 117 }, await GroupsOfAsync("Event_B"));
    }

    [Fact(DisplayName = "부활수정 03. 레거시값이 유일 그룹이면 보존 (NFR-02)")]
    public async Task should_preserve_group_when_legacy_is_sole_group()
    {
        await _svc.BuildSchemeAsync(_cts.Token);

        // Event_C(레거시5 유일) / Event_D(레거시7=사용자7, dedup) → count=1 보존
        Assert.Equal(new List<int> { 5 }, await GroupsOfAsync("Event_C"));
        Assert.Equal(new List<int> { 7 }, await GroupsOfAsync("Event_D"));
    }

    [Fact(DisplayName = "부활수정 04. 마무리 후 레거시 Group 컬럼 제거 (FR-03)")]
    public async Task should_drop_legacy_group_column_when_finalize_runs()
    {
        Assert.Equal(1, await LegacyGroupColumnCountAsync());   // 정리 전 존재

        await _svc.BuildSchemeAsync(_cts.Token);

        Assert.Equal(0, await LegacyGroupColumnCountAsync());   // 정리 후 제거
    }

    [Fact(DisplayName = "부활수정 05. BuildScheme 이중 실행 멱등 — 좀비 재부활 없음 (NFR-01)")]
    public async Task should_be_idempotent_when_buildscheme_runs_twice()
    {
        await _svc.BuildSchemeAsync(_cts.Token);   // 1회차: 정리 + DROP
        await _svc.BuildSchemeAsync(_cts.Token);   // 2회차: 컬럼 없음 → 마무리 스킵, 부활 없음

        Assert.Equal(new List<int> { 116 }, await GroupsOfAsync("Event_A"));
        Assert.Equal(new List<int> { 117 }, await GroupsOfAsync("Event_B"));
        Assert.Equal(0, await LegacyGroupColumnCountAsync());
    }
}
