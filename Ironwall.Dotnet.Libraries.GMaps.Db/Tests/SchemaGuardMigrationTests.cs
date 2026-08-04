using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using MySql.Data.MySqlClient;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Tests;

/****************************************************************************
   Purpose      : GMap_Schema_Migration_Idempotency 회귀 테스트
                  (S2 수렴 DB / T1 멱등성 — PRD DoD)
   Created By   : Claude
   Created On   : 8/4/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 스키마 가드 전환의 핵심 회귀를 잡는 테스트.
/// </summary>
/// <remarks>
/// ⚠ 이 테스트는 <b>DropTables 를 하지 않는다.</b> 기존 <c>GMapBaseSymbolFixture</c> 는 매 초기화마다
///   테이블을 지우고 최신 CREATE 로 다시 만들기 때문에 BuildSchemeAsync 가 항상 '방금 만든 최신 스키마'만
///   만난다 → 가드는 언제나 '존재' 판정이 되어 <b>파괴 경로가 구조적으로 관측되지 않는다</b>.
///   빈 DB 에서 그린이 뜨는 것은 안전 근거가 되지 못한다(PRD 리스크 표 3행).
///
///   따라서 이 테스트는 운영 스키마를 복원해 둔 격리 DB(monitor_test_db)를 <b>그대로</b> 사용한다.
///   준비 절차(1회):
///     mariadb-dump monitor_db --single-transaction > dump.sql
///     mariadb -e "DROP DATABASE IF EXISTS monitor_test_db; CREATE DATABASE monitor_test_db CHARACTER SET utf8mb4;"
///     mariadb monitor_test_db &lt; dump.sql
///
///   데이터가 없으면 Skip 한다(빈 DB 에서 통과시키면 거짓 안전이 되므로).
/// </remarks>
public sealed class SchemaGuardMigrationTests
{
    private readonly GMapDbSetupModel _setup = new()
    {
        IpDbServer = "127.0.0.1",
        PortDbServer = 3306,
        DbDatabase = GMapTestDb.Name,   // 운영 monitor_db 금지 — 격리 DB
        UidDbServer = "root",
        PasswordDbServer = "root",
    };

    private string ConnStr =>
        $"Server={_setup.IpDbServer};Port={_setup.PortDbServer};Database={_setup.DbDatabase};" +
        $"Uid={_setup.UidDbServer};Pwd={_setup.PasswordDbServer};";

    private IGMapDbSymbolService CreateService()
    {
        var log = new LogService();
        var ea = new EventAggregator();
        var symbolProvider = new SymbolProvider();
        return new GMapDbSymbolService(
            log, ea, symbolProvider,
            new GeometricSymbolProvider(log, symbolProvider),
            new PidsSymbolProvider(log, symbolProvider),
            new MilitarySymbolProvider(log, symbolProvider),
            new LineSymbolProvider(log, symbolProvider),
            new InfraSymbolProvider(log, symbolProvider),
            new PidsGroupSymbolProvider(log, symbolProvider),
            _setup);
    }

    /// <summary>Symbols 의 (Id, ZOrder) 전량 스냅샷.</summary>
    private async Task<List<(int Id, int ZOrder)>> SnapshotZOrderAsync()
    {
        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<(int Id, int ZOrder)>(
            "SELECT Id, ZOrder FROM Symbols ORDER BY Id;");
        return rows.ToList();
    }

    /// <summary>격리 DB 가 '수렴 상태(데이터 보유)'인지 — 아니면 이 테스트는 무의미하다.</summary>
    private async Task<bool> IsConvergedFixtureAsync()
    {
        try
        {
            await using var conn = new MySqlConnection(ConnStr);
            await conn.OpenAsync();
            var n = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Symbols;");
            return n > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// S2 — 수렴 DB 에서 부팅해도 ZOrder 가 한 행도 바뀌지 않아야 한다.
    /// </summary>
    /// <remarks>
    /// 이 테스트가 막는 사고: 예외 기반 가드를 information_schema 가드로 바꾸면
    /// 같은 try 에 묶여 있던 <c>UPDATE Symbols SET ZOrder = ZIndex</c> 가 처음으로 실행되어
    /// 전 행의 ZOrder 가 ZIndex 기본값(10)으로 평탄화되고, 이어지는 band shift 가 전부 1010 으로
    /// 밀어 상대 순서가 영구 소실된다. 코드 롤백으로 복구 불가능한 유일한 자산이다.
    /// </remarks>
    [Fact]
    public async Task should_preserve_every_zorder_when_booting_against_converged_schema()
    {
        // Arrange
        Assert.True(await IsConvergedFixtureAsync(),
            $"{GMapTestDb.Name} 에 Symbols 데이터가 없습니다. 운영 덤프를 복원한 뒤 실행하세요(빈 DB 통과는 거짓 안전).");

        var before = await SnapshotZOrderAsync();
        var svc = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        try
        {
            await svc.StartService(cts.Token);   // Connect + BuildSchemeAsync + FetchInstance

            // Assert
            var after = await SnapshotZOrderAsync();

            Assert.Equal(before.Count, after.Count);
            Assert.Equal(before, after);         // (Id, ZOrder) 전량 동일

            // band 불변식: 심볼 ZOrder 는 1000+ 대역이어야 한다
            Assert.DoesNotContain(after, r => r.ZOrder < 1000);

            // 순서 고유성: 중복이 생기면 EnsureUniqueZOrder 가 재번호해 DB 에 확정 기록한다
            Assert.Equal(after.Count, after.Select(r => r.ZOrder).Distinct().Count());
        }
        finally
        {
            await svc.StopService(cts.Token);
        }
    }

    /// <summary>
    /// T1 — 같은 DB 로 연속 2회 부팅해도 스냅샷이 동일해야 한다(멱등성).
    /// </summary>
    [Fact]
    public async Task should_stay_identical_when_booting_twice_against_same_database()
    {
        // Arrange
        Assert.True(await IsConvergedFixtureAsync(),
            $"{GMapTestDb.Name} 에 Symbols 데이터가 없습니다. 운영 덤프를 복원한 뒤 실행하세요.");

        var baseline = await SnapshotZOrderAsync();

        // Act — 1회차
        var svc1 = CreateService();
        using var cts1 = new CancellationTokenSource();
        try { await svc1.StartService(cts1.Token); }
        finally { await svc1.StopService(cts1.Token); }
        var afterFirst = await SnapshotZOrderAsync();

        // Act — 2회차
        var svc2 = CreateService();
        using var cts2 = new CancellationTokenSource();
        try { await svc2.StartService(cts2.Token); }
        finally { await svc2.StopService(cts2.Token); }
        var afterSecond = await SnapshotZOrderAsync();

        // Assert
        Assert.Equal(baseline, afterFirst);
        Assert.Equal(afterFirst, afterSecond);
    }

    /// <summary>
    /// 색상 정리 마이그레이션이 정상 EnumColorType 문자열을 건드리지 않아야 한다.
    /// </summary>
    /// <remarks>
    /// 정리 대상은 숫자 문자열(예: "-853795294")뿐이다. WHERE 절의 정규식이 정상 값('White','Red' 등)을
    /// 오탐하면 사용자가 지정한 라벨 색이 초기화된다.
    /// </remarks>
    [Fact]
    public async Task should_not_touch_named_colors_when_cleaning_numeric_title_colors()
    {
        // Arrange
        Assert.True(await IsConvergedFixtureAsync(),
            $"{GMapTestDb.Name} 에 Symbols 데이터가 없습니다.");

        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();
        var namedBefore = await conn.QueryAsync<(int Id, string TitleColor)>(
            "SELECT Id, TitleColor FROM Symbols WHERE TitleColor NOT REGEXP '^-?[0-9]+$' ORDER BY Id;");
        var expected = namedBefore.ToList();

        var svc = CreateService();
        using var cts = new CancellationTokenSource();

        // Act
        try { await svc.StartService(cts.Token); }
        finally { await svc.StopService(cts.Token); }

        // Assert
        await using var conn2 = new MySqlConnection(ConnStr);
        await conn2.OpenAsync();
        var namedAfter = await conn2.QueryAsync<(int Id, string TitleColor)>(
            "SELECT Id, TitleColor FROM Symbols WHERE TitleColor NOT REGEXP '^-?[0-9]+$' ORDER BY Id;");

        Assert.Equal(expected, namedAfter.ToList());
    }

    /// <summary>
    /// 마이그레이션 대상 컬럼이 부팅 후 전부 실재해야 한다(가드가 필요한 ALTER 를 빠뜨리지 않았는지).
    /// </summary>
    [Fact]
    public async Task should_have_every_migrated_column_when_boot_completes()
    {
        // Arrange
        Assert.True(await IsConvergedFixtureAsync(),
            $"{GMapTestDb.Name} 에 Symbols 데이터가 없습니다.");

        var svc = CreateService();
        using var cts = new CancellationTokenSource();
        try { await svc.StartService(cts.Token); }
        finally { await svc.StopService(cts.Token); }

        await using var conn = new MySqlConnection(ConnStr);
        await conn.OpenAsync();

        // Act
        var symbolCols = (await conn.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Symbols';"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imageCols = (await conn.QueryAsync<string>(
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='Images';"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert — 읽기 SQL 이 명시적으로 SELECT 하는 컬럼들
        foreach (var c in new[]
                 {
                     "ZOrder", "Visible", "IsLocked", "LabelOffsetX", "LabelOffsetY",
                     "TitleColor", "TitleBackground", "TitleFontFamily", "TitleBold", "TitleItalic", "TitleMaxWidth",
                 })
            Assert.True(symbolCols.Contains(c), $"Symbols.{c} 컬럼이 없습니다 — 읽기 SQL 이 1054 로 실패합니다.");

        foreach (var c in new[]
                 {
                     "IsLocked", "TitleSize", "ShowTitle", "LabelOffsetU", "LabelOffsetV",
                     "TitleColor", "TitleBackground", "TitleFontFamily", "TitleBold", "TitleItalic", "TitleMaxWidth",
                 })
            Assert.True(imageCols.Contains(c), $"Images.{c} 컬럼이 없습니다 — 읽기 SQL 이 1054 로 실패합니다.");
    }
}
