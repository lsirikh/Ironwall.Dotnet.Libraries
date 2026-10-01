using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using MySql.Data.MySqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;

/// <summary>
/// 결선 펜스 구성 로컬 저장소(fence-wiring-editor FR-11 · FR-16) — 지도와 같은 로컬 DB(<c>gmap_tiles_db</c>)의 <c>WiringFenceLayoutsV2</c> 표.
/// (서버, 제어기 id) 한 행 · JSON 본문 · 행 판(동시 저장 충돌 판정) · 덮어쓸 때 이전 본문 보관 칸.
/// </summary>
/// <remarks>
/// <para><b>열쇠에 서버가 들어간다</b> — 같은 PC 의 로컬 DB 를 운영 서버와 시험 서버(루프백 · <c>IRONWALL_UITEST_SERVER</c>)가 함께 쓰므로
/// 제어기 id 만으로는 서로 덮는다. 서버 식별은 API 주소의 <c>host:port</c>(<see cref="FenceLayoutRows.ServerKeyOf"/>).
/// 종전 표(<c>WiringFenceLayouts</c> · 제어기 id 만 열쇠)는 읽지 않는다 — 기능이 새것이라 내보낸 데이터가 없다.</para>
/// <para><b>부팅 경로에 들어가지 않는다</b>(NFR-06) — <c>IService</c> 로 등록하지 않고, 표는 결선 창이 처음 읽거나 쓸 때
/// <c>CREATE TABLE IF NOT EXISTS</c> 로 만든다(멱등).</para>
/// <para>연결은 <see cref="IGMapDbService.OpenConnectionAsync"/> 가 준다 — 작업마다 새 연결(공유 연결을 병렬로 쓰지 않는다).</para>
/// <para>판정(없음 · 손상 · 판 계산 · 충돌)은 <see cref="FenceLayoutRows"/> 에 있다 — 여기는 SQL 만 낸다. 실패는 예외로 던지지 않는다(로그 한 줄).</para>
/// </remarks>
internal sealed class FenceLayoutStore : IFenceLayoutStore
{
    internal const string TABLE = "WiringFenceLayoutsV2";

    internal const string CREATE_TABLE_SQL = @"
        CREATE TABLE IF NOT EXISTS `WiringFenceLayoutsV2` (
            `ServerKey`    VARCHAR(190) NOT NULL,
            `ControllerId` INT NOT NULL,
            `Schema`       INT NOT NULL DEFAULT 1,
            `Revision`     INT NOT NULL DEFAULT 1,
            `Body`         MEDIUMTEXT NOT NULL,
            `PreviousBody` MEDIUMTEXT NULL,
            `UpdatedAt`    DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            PRIMARY KEY (`ServerKey`, `ControllerId`)
        );";

    internal const string SELECT_SQL =
        "SELECT `Revision`, `Body`, `UpdatedAt` FROM `WiringFenceLayoutsV2` WHERE `ServerKey` = @Server AND `ControllerId` = @Id;";

    internal const string INSERT_SQL =
        "INSERT IGNORE INTO `WiringFenceLayoutsV2` (`ServerKey`, `ControllerId`, `Schema`, `Revision`, `Body`) VALUES (@Server, @Id, @Schema, 1, @Body);";

    internal const string UPDATE_SQL =
        "UPDATE `WiringFenceLayoutsV2` SET `Schema` = @Schema, `Revision` = @Next, `Body` = @Body " +
        "WHERE `ServerKey` = @Server AND `ControllerId` = @Id AND `Revision` = @Revision;";

    /// <summary>덮어쓰기 — 이전 본문을 먼저 보관 칸으로 옮긴다(MySQL 은 SET 을 왼쪽부터 평가한다).</summary>
    internal const string OVERWRITE_SQL =
        "INSERT INTO `WiringFenceLayoutsV2` (`ServerKey`, `ControllerId`, `Schema`, `Revision`, `Body`) VALUES (@Server, @Id, @Schema, 1, @Body) " +
        "ON DUPLICATE KEY UPDATE `PreviousBody` = `Body`, `Schema` = VALUES(`Schema`), `Revision` = `Revision` + 1, `Body` = VALUES(`Body`);";

    internal const string REVISION_SQL =
        "SELECT `Revision` FROM `WiringFenceLayoutsV2` WHERE `ServerKey` = @Server AND `ControllerId` = @Id;";

    private readonly IGMapDbService _db;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    public FenceLayoutStore(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public async Task<FenceLayoutLoadResult> LoadAsync(FenceLayoutKey key, CancellationToken token = default)
    {
        if (!key.IsValid) return FenceLayoutLoadResult.NotFound;
        try
        {
            await using var conn = await _db.OpenConnectionAsync(token).ConfigureAwait(false);
            await EnsureSchemaAsync(conn, token).ConfigureAwait(false);
            var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
                SELECT_SQL, new { Server = key.Server, Id = key.ControllerId }, cancellationToken: token)).ConfigureAwait(false);
            var result = FenceLayoutRows.Interpret(row is not null, row?.Revision ?? 0, row?.Body, row?.UpdatedAt);
            if (result.Status == FenceLayoutLoadStatus.Unreadable)
                _log?.Warning($"[{nameof(FenceLayoutStore)}] {key} 펜스 구성 본문을 읽지 못했습니다(판 {result.RowRevision}) — 덮어쓰기 확인 전에는 쓰지 않습니다.");
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(FenceLayoutStore)}] {key} 펜스 구성 불러오기 실패: {ex.Message}");
            return FenceLayoutLoadResult.Failed(ex.Message);
        }
    }

    public async Task<FenceLayoutSaveResult> SaveAsync(FenceLayoutKey key, FenceLayoutDocument document, FenceLayoutSaveMode mode = FenceLayoutSaveMode.Normal,
                                                       CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!key.IsValid) return new FenceLayoutSaveResult(FenceLayoutSaveStatus.Failed, 0, "제어기를 알 수 없어 저장하지 않았습니다.");
        var plan = FenceLayoutRows.PlanSave(document.Revision, mode);
        try
        {
            var body = FenceLayoutJson.Serialize(document);
            await using var conn = await _db.OpenConnectionAsync(token).ConfigureAwait(false);
            await EnsureSchemaAsync(conn, token).ConfigureAwait(false);

            var args = new { Server = key.Server, Id = key.ControllerId, Schema = document.Schema, Body = body, Next = plan.NextRevision ?? 0, Revision = document.Revision };
            var sql = plan.Kind switch
            {
                FenceLayoutSaveKind.Insert => INSERT_SQL,
                FenceLayoutSaveKind.Update => UPDATE_SQL,
                _ => OVERWRITE_SQL,
            };
            var affected = await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: token)).ConfigureAwait(false);
            int? after = null;
            if (plan.Kind == FenceLayoutSaveKind.Overwrite)
                after = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                    REVISION_SQL, new { Server = key.Server, Id = key.ControllerId }, cancellationToken: token)).ConfigureAwait(false);

            var result = FenceLayoutRows.ResultOf(plan, document.Revision, affected, after);
            if (result.Status == FenceLayoutSaveStatus.Conflict)
                _log?.Warning($"[{nameof(FenceLayoutStore)}] {key} 펜스 구성 저장 충돌(판 {document.Revision}) — 쓰지 않았습니다.");
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(FenceLayoutStore)}] {key} 펜스 구성 저장 실패: {ex.Message}");
            return new FenceLayoutSaveResult(FenceLayoutSaveStatus.Failed, document.Revision, FenceLayoutRows.FAILED);
        }
    }

    /// <summary>표를 한 번만 만든다(멱등 SQL · 여러 창이 동시에 열어도 한 번).</summary>
    private async Task EnsureSchemaAsync(MySqlConnection conn, CancellationToken token)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await conn.ExecuteAsync(new CommandDefinition(CREATE_TABLE_SQL, cancellationToken: token)).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }

    private sealed class Row
    {
        public int Revision { get; set; }
        public string? Body { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
