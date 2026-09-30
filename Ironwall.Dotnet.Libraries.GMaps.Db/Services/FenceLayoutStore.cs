using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using MySql.Data.MySqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;

/// <summary>
/// 결선 펜스 구성 로컬 저장소(fence-wiring-editor FR-11 · FR-16) — 지도와 같은 로컬 DB(<c>gmap_tiles_db</c>)의 <c>WiringFenceLayouts</c> 표.
/// 제어기 id 한 행 · JSON 본문 · 행 판(동시 저장 충돌 판정).
/// </summary>
/// <remarks>
/// <para><b>부팅 경로에 들어가지 않는다</b>(NFR-06) — <c>IService</c> 로 등록하지 않고, 표는 결선 창이 처음 읽거나 쓸 때
/// <c>CREATE TABLE IF NOT EXISTS</c> 로 만든다(<see cref="GMapDbService.BuildSchemeAsync"/> 의 다른 표와 같은 문법 · 멱등).</para>
/// <para>연결은 <see cref="IGMapDbService.OpenConnectionAsync"/> 가 준다 — 작업마다 새 연결(공유 연결을 병렬로 쓰지 않는다).</para>
/// <para>실패는 예외로 던지지 않는다 — 결선 창은 로컬 저장만 못 할 뿐 서버 저장 · 편집은 그대로다. 로그는 한 줄.</para>
/// </remarks>
internal sealed class FenceLayoutStore : IFenceLayoutStore
{
    internal const string TABLE = "WiringFenceLayouts";

    internal const string CREATE_TABLE_SQL = @"
        CREATE TABLE IF NOT EXISTS `WiringFenceLayouts` (
            `ControllerId` INT PRIMARY KEY,
            `Schema`       INT NOT NULL DEFAULT 1,
            `Revision`     INT NOT NULL DEFAULT 1,
            `Body`         MEDIUMTEXT NOT NULL,
            `UpdatedAt`    DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        );";

    private readonly IGMapDbService _db;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    public FenceLayoutStore(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public async Task<FenceLayoutDocument?> LoadAsync(int controllerId, CancellationToken token = default)
    {
        if (controllerId <= 0) return null;
        try
        {
            await using var conn = await _db.OpenConnectionAsync(token).ConfigureAwait(false);
            await EnsureSchemaAsync(conn, token).ConfigureAwait(false);
            var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
                "SELECT `Revision`, `Body`, `UpdatedAt` FROM `WiringFenceLayouts` WHERE `ControllerId` = @Id;",
                new { Id = controllerId }, cancellationToken: token)).ConfigureAwait(false);
            if (row is null) return null;
            var document = FenceLayoutJson.Deserialize(row.Body, row.Revision, row.UpdatedAt);
            if (document is null) _log?.Warning($"[{nameof(FenceLayoutStore)}] 제어기 {controllerId} 펜스 구성 본문을 읽지 못했습니다 — 없는 것으로 봅니다.");
            return document;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(FenceLayoutStore)}] 제어기 {controllerId} 펜스 구성 불러오기 실패: {ex.Message}");
            return null;
        }
    }

    public async Task<FenceLayoutSaveResult> SaveAsync(FenceLayoutDocument document, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.ControllerId <= 0) return new FenceLayoutSaveResult(FenceLayoutSaveStatus.Failed, 0, "제어기를 알 수 없어 저장하지 않았습니다.");
        try
        {
            var body = FenceLayoutJson.Serialize(document);
            await using var conn = await _db.OpenConnectionAsync(token).ConfigureAwait(false);
            await EnsureSchemaAsync(conn, token).ConfigureAwait(false);

            int affected;
            int next;
            if (document.Revision <= 0)
            {
                next = 1;
                affected = await conn.ExecuteAsync(new CommandDefinition(
                    "INSERT IGNORE INTO `WiringFenceLayouts` (`ControllerId`, `Schema`, `Revision`, `Body`) VALUES (@Id, @Schema, 1, @Body);",
                    new { Id = document.ControllerId, Schema = document.Schema, Body = body }, cancellationToken: token)).ConfigureAwait(false);
            }
            else
            {
                next = document.Revision + 1;
                affected = await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE `WiringFenceLayouts` SET `Schema` = @Schema, `Revision` = @Next, `Body` = @Body WHERE `ControllerId` = @Id AND `Revision` = @Revision;",
                    new { Id = document.ControllerId, Schema = document.Schema, Next = next, Body = body, Revision = document.Revision },
                    cancellationToken: token)).ConfigureAwait(false);
            }

            if (affected == 0)
            {
                _log?.Warning($"[{nameof(FenceLayoutStore)}] 제어기 {document.ControllerId} 펜스 구성 저장 충돌(판 {document.Revision}) — 쓰지 않았습니다.");
                return new FenceLayoutSaveResult(FenceLayoutSaveStatus.Conflict, document.Revision,
                    "다른 GIS 가 이 제어기의 펜스 구성을 먼저 저장했습니다 — 창을 다시 열어 확인하세요.");
            }
            return new FenceLayoutSaveResult(FenceLayoutSaveStatus.Saved, next, "펜스 구성을 이 PC 에 저장했습니다.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(FenceLayoutStore)}] 제어기 {document.ControllerId} 펜스 구성 저장 실패: {ex.Message}");
            return new FenceLayoutSaveResult(FenceLayoutSaveStatus.Failed, document.Revision, "펜스 구성을 로컬 DB 에 저장하지 못했습니다.");
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
