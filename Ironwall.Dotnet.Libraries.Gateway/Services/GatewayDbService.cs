using Caliburn.Micro;
using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Gateway.Models;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Gateway.Services;
/****************************************************************************
   Purpose      : Gateway DB 서비스 (CRUD 및 연결 관리)
   Created By   : GHLee
   Created On   : 10/29/2025 12:16:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
internal class GatewayDbService : TaskService, IGatewayDbService
{
    private readonly ILogService _log;
    private readonly IEventAggregator _eventAggregator;
    private readonly GatewaySetupModel _setup;
    private readonly GatewayEventProvider _eventProvider;
    private MySqlConnection? _conn;

    public GatewayDbService(
        ILogService log,
        IEventAggregator eventAggregator,
        GatewaySetupModel setupModel,
        GatewayEventProvider eventProvider)
    {
        _log = log;
        _eventAggregator = eventAggregator;
        _setup = setupModel;
        _eventProvider = eventProvider;
    }

    protected override async Task RunTask(CancellationToken token = default)
    {
        await StartService(token);
    }

    protected override async Task ExitTask(CancellationToken token = default)
    {
        await StopService(token);
    }

    public async Task StartService(CancellationToken token = default)
    {
        await Connect(token);
        await BuildSchemeAsync(token);
        await FetchInstanceAsync(token);
    }

    public async Task StopService(CancellationToken token = default)
    {
        await Disconnect(token);
    }

    public string BuildConnStr(bool includeDb = true)
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

    public async Task Connect(CancellationToken token = default)
    {
        try
        {
            var dbName = (_setup.DbDatabase ?? "monitor_db").ToLowerInvariant();
            _setup.DbDatabase = dbName;

            // DB 생성 (없으면)
            await using (var bootstrap = new MySqlConnection(BuildConnStr(includeDb: false)))
            {
                await bootstrap.OpenAsync(token);
                var createDbSql = $"CREATE DATABASE IF NOT EXISTS `{dbName}` " +
                                  "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_520_ci;";
                await bootstrap.ExecuteAsync(createDbSql, token);
                _log?.Info($"Gateway DB({dbName}) 확인/생성 완료.");
            }

            // 애플리케이션용 연결
            _conn = new MySqlConnection(BuildConnStr(includeDb: true));
            await _conn.OpenAsync(token);
            _log?.Info($"Gateway DB 연결 성공: {_setup.IpDbServer}:{_setup.PortDbServer}/{dbName}");
        }
        catch (Exception ex)
        {
            _log?.Error($"Gateway DB Connect Error: {ex}");
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
                _log?.Info("Gateway DB 연결 종료.");
            }
        }
        catch (IOException)
        {
            // 앱 종료 시 소켓 선종료 후 MySQL close 패킷 전송 실패 — 정상 패턴, 무시
        }
        catch (Exception ex)
        {
            _log?.Error($"Gateway DB Disconnect Error: {ex}");
        }
    }

    public async Task BuildSchemeAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("Gateway DB 연결이 이루어지지 않았습니다.");

            var createGatewayEventsTable = @"
                CREATE TABLE IF NOT EXISTS `GatewayEvents` (
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
                );";

            var createGatewayEventGroupsTable = @"
                CREATE TABLE IF NOT EXISTS `GatewayEventGroups` (
                    `EventId`  INT NOT NULL,
                    `GroupId`  INT NOT NULL,
                    PRIMARY KEY (`EventId`, `GroupId`),
                    CONSTRAINT `FK_GEG_Event`
                        FOREIGN KEY (`EventId`) REFERENCES `GatewayEvents`(`Id`)
                        ON DELETE CASCADE,
                    INDEX `IX_GEG_GroupId` (`GroupId`)
                );";

            // 기존 Group>0 데이터를 연결 테이블로 이행 (INSERT IGNORE — 멱등)
            var migrateGroupData = @"
                INSERT IGNORE INTO `GatewayEventGroups` (`EventId`, `GroupId`)
                SELECT `Id`, `Group` FROM `GatewayEvents` WHERE `Group` > 0;";

            await _conn.ExecuteAsync(createGatewayEventsTable);
            await _conn.ExecuteAsync(createGatewayEventGroupsTable);
            await _conn.ExecuteAsync(migrateGroupData);
            _log?.Info("GatewayEvents / GatewayEventGroups 테이블 생성/확인 및 데이터 이행 완료.");
        }
        catch (Exception ex)
        {
            _log?.Error($"Gateway BuildSchemeAsync Error: {ex}");
        }
    }

    public async Task FetchInstanceAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn == null || _conn.State != ConnectionState.Open)
                throw new Exception("Gateway DB 연결이 이루어지지 않았습니다.");

            var events = await FetchGatewayEventsAsync(token);
            _eventProvider.Clear();
            if (events?.Any() == true)
            {
                foreach (var item in events)
                {
                    _eventProvider.Add(item);
                }
            }

            _log?.Info($"GatewayEventProvider 로드 완료 - {events?.Count ?? 0}건");
        }
        catch (Exception ex)
        {
            _log?.Error($"Gateway FetchInstanceAsync Error: {ex}");
        }
    }

    public async Task<List<IGatewayEventModel>?> FetchGatewayEventsAsync(CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
                SELECT e.Id, e.EventName, e.IsEnable, e.Description,
                       GROUP_CONCAT(g.GroupId ORDER BY g.GroupId SEPARATOR ',') AS GroupIds
                FROM GatewayEvents e
                LEFT JOIN GatewayEventGroups g ON e.Id = g.EventId
                GROUP BY e.Id
                ORDER BY e.EventName;";

            var rows = (await _conn.QueryAsync<GatewayEventSQL>(sql)).ToList();
            var list = rows.Select(r => (IGatewayEventModel)r.ToDomain()).ToList();

            _log?.Info($"FetchGatewayEventsAsync 완료 - {list.Count}건");
            return list;
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchGatewayEventsAsync Error: {ex}");
            throw;
        }
    }

    public async Task<IGatewayEventModel?> FetchGatewayEventAsync(int id, CancellationToken token = default)
    {
        try
        {
            if (_conn?.State != ConnectionState.Open)
                throw new Exception("DB not connected.");

            const string sql = @"
                SELECT e.Id, e.EventName, e.IsEnable, e.Description,
                       GROUP_CONCAT(g.GroupId ORDER BY g.GroupId SEPARATOR ',') AS GroupIds
                FROM GatewayEvents e
                LEFT JOIN GatewayEventGroups g ON e.Id = g.EventId
                WHERE e.Id = @Id
                GROUP BY e.Id;";

            var row = await _conn.QueryFirstOrDefaultAsync<GatewayEventSQL>(sql, new { Id = id });
            return row?.ToDomain();
        }
        catch (Exception ex)
        {
            _log?.Error($"FetchGatewayEventAsync Error: {ex}");
            return null;
        }
    }

    public async Task<IGatewayEventModel?> InsertGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default)
    {
        if (_conn?.State != ConnectionState.Open)
            throw new InvalidOperationException("Gateway DB 연결이 이루어지지 않았습니다.");

        using var transaction = await _conn.BeginTransactionAsync(token);
        try
        {
            const string insertEvent = @"
                INSERT INTO GatewayEvents (EventName, IsEnable, Description)
                VALUES (@EventName, @IsEnable, @Description);
                SELECT LAST_INSERT_ID();";

            var id = await _conn.ExecuteScalarAsync<int>(insertEvent, new
            {
                model.EventName,
                model.IsEnable,
                model.Description
            }, transaction);

            if (model.DeviceGroups.Count > 0)
            {
                const string insertGroup = @"
                    INSERT IGNORE INTO GatewayEventGroups (EventId, GroupId) VALUES (@EventId, @GroupId);";
                foreach (var groupId in model.DeviceGroups)
                    await _conn.ExecuteAsync(insertGroup, new { EventId = id, GroupId = groupId }, transaction);
            }

            await transaction.CommitAsync(token);
            model.Id = id;
            _log?.Info($"InsertGatewayEventAsync 완료 - Id={id}, EventName={model.EventName}, Groups={string.Join(",", model.DeviceGroups)}");
            return model;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"InsertGatewayEventAsync Error: {ex}");
            throw;
        }
    }

    public async Task<IGatewayEventModel?> UpdateGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default)
    {
        if (_conn?.State != ConnectionState.Open)
            throw new InvalidOperationException("Gateway DB 연결이 이루어지지 않았습니다.");

        using var transaction = await _conn.BeginTransactionAsync(token);
        try
        {
            const string updateEvent = @"
                UPDATE GatewayEvents SET
                    EventName = @EventName,
                    IsEnable = @IsEnable,
                    Description = @Description
                WHERE Id = @Id;";

            var affected = await _conn.ExecuteAsync(updateEvent, new
            {
                model.Id,
                model.EventName,
                model.IsEnable,
                model.Description
            }, transaction);

            // 기존 그룹 연결 전부 삭제 후 신규 삽입 (DELETE+INSERT 패턴)
            await _conn.ExecuteAsync(
                "DELETE FROM GatewayEventGroups WHERE EventId = @Id;",
                new { model.Id }, transaction);

            if (model.DeviceGroups.Count > 0)
            {
                const string insertGroup = @"
                    INSERT IGNORE INTO GatewayEventGroups (EventId, GroupId) VALUES (@EventId, @GroupId);";
                foreach (var groupId in model.DeviceGroups)
                    await _conn.ExecuteAsync(insertGroup, new { EventId = model.Id, GroupId = groupId }, transaction);
            }

            await transaction.CommitAsync(token);
            _log?.Info($"UpdateGatewayEventAsync 완료 - Id={model.Id}, Groups={string.Join(",", model.DeviceGroups)}");
            return affected > 0 ? await FetchGatewayEventAsync(model.Id, token) : null;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            _log?.Error($"UpdateGatewayEventAsync Error: {ex}");
            throw;
        }
    }

    public async Task<bool> DeleteGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default)
    {
        if (_conn?.State != ConnectionState.Open)
            throw new InvalidOperationException("Gateway DB 연결이 이루어지지 않았습니다.");
        try
        {
            // GatewayEventGroups는 ON DELETE CASCADE로 자동 제거됨
            const string sql = @"DELETE FROM GatewayEvents WHERE Id = @Id;";
            int ret = await _conn.ExecuteAsync(sql, new { model.Id });

            _log?.Info($"DeleteGatewayEventAsync 완료 - Rows={ret}");
            return ret > 0;
        }
        catch (Exception ex)
        {
            _log?.Error($"DeleteGatewayEventAsync Error: {ex}");
            throw;
        }
    }

    public bool IsConnected => _conn != null && _conn.State == ConnectionState.Open;
}

// SQL 매핑 클래스
internal sealed class GatewayEventSQL
{
    public int Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public bool IsEnable { get; set; }
    public string? Description { get; set; }
    public string? GroupIds { get; set; }   // GROUP_CONCAT 결과 (쉼표 구분 문자열)

    public GatewayEventModel ToDomain() => new()
    {
        Id = Id,
        EventName = EventName,
        IsEnable = IsEnable,
        Description = Description,
        DeviceGroups = string.IsNullOrEmpty(GroupIds)
            ? new List<int>()
            : GroupIds.Split(',').Select(int.Parse).ToList()
    };
}

