using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터가 쓰는 조회·저장 통로 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>부대 필터 항목.</summary>
public sealed record ServerUnitOption(int Id, string Name, string Code);

/// <summary>등록 폼의 "분류" 선택지 — 6.3 은 <c>category_id</c>, 7.0+ 는 <c>category_server</c> 가 정본이다.</summary>
public sealed record ServerCategoryOption(int Id, string Name, EnumServerType? Type, string RawTypeServer)
{
    public string Display => $"{Name} · {ServerTypeCatalog.TypeLabel(Type, RawTypeServer)}";
}

/// <summary>한 번의 적재 결과.</summary>
public sealed record ServerLoadResult(
    IReadOnlyList<ServerAxisView> Servers,
    IReadOnlyList<ServerUnitOption> Units,
    IReadOnlyList<ServerCategoryOption> Categories,
    bool IsTruncated,
    string? Message);

/// <summary>쓰기 한 번의 결과.</summary>
public sealed record ServerWriteResult(bool IsSuccess, string Message);

/// <summary>
/// 서버 모니터의 조회·저장 통로. <b>새 인터페이스</b>다 — 기존 <see cref="IServerApiService"/> 를 넓히지 않는다.
/// </summary>
public interface IServerConsoleService
{
    EnumServerContract Contract { get; }

    /// <summary>부대 편제(8.0 이상)를 쓸 수 있는가.</summary>
    bool IsUnitEra { get; }

    /// <summary>축 계약(7.0 이상)인가 — 모드 절 · 해제 · 전 카테고리 배정의 게이트.</summary>
    bool IsAxisEra { get; }

    Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default);

    Task<ServerAxisView?> GetAsync(int id, CancellationToken token = default);

    Task<ServerWriteResult> SaveAsync(int id, ServerWriteIntent intent, CancellationToken token = default);

    /// <summary>서버 등록 — <b>본문에 상태가 없다</b>. 성공하면 만들어진 Id.</summary>
    Task<(ServerWriteResult Result, int NewId)> CreateAsync(
        ServerCategoryOption category, ServerWriteIntent intent, CancellationToken token = default);

    Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default);

    Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default);

    /// <summary>운용 모드. 6.3 이면 프록시 설정 경로, 7.0+ 는 <c>server_config.modes</c> 라 여기서는 <c>null</c>.</summary>
    Task<(ProxySettingDto? Setting, string? Note)> LegacyOperationModeAsync(int id, CancellationToken token = default);

    /// <summary>장비 한 대의 관리 서버를 바꾼다 — 쓰기 <b>1회</b>. <paramref name="serverId"/> 가 <c>null</c> 이면 해제.</summary>
    Task<ServerWriteResult> AssignDeviceAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, CancellationToken token = default);
}

/// <summary>
/// 축 통로(<see cref="IServerAxisApiService"/>) 위에 콘솔이 필요한 것만 얹는다 — 부대 선택지 · 계측 · 6.3 프록시 설정.
/// </summary>
public sealed class ServerConsoleService : IServerConsoleService
{
    private const int PageLimit = 100;

    private readonly IServerAxisApiService _axis;
    private readonly IServerApiService _servers;
    private readonly DeviceQueryPolicy _policy;
    private readonly Lazy<IUnitApiService>? _units;
    private readonly Lazy<IUnitScopeService>? _unitScope;
    private readonly ILogService? _log;

    public ServerConsoleService(
        IServerAxisApiService axis,
        IServerApiService servers,
        DeviceQueryPolicy policy,
        Lazy<IUnitApiService>? units = null,
        Lazy<IUnitScopeService>? unitScope = null,
        ILogService? log = null)
    {
        _axis = axis ?? throw new ArgumentNullException(nameof(axis));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _units = units;
        _unitScope = unitScope;
        _log = log;
    }

    public EnumServerContract Contract => _policy.Contract;
    public bool IsUnitEra => ServerWriteGuard.IsUnitEra(_policy);
    public bool IsAxisEra => _policy.IsAxisContract;

    #region - Load -
    public async Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
    {
        var categories = (await _axis.GetCategoriesAsync(token).ConfigureAwait(false))
            .Select(c => new ServerCategoryOption(c.Id, c.Name, ServerTypeCatalog.ParseType(c.TypeServer), c.TypeServer))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();

        var units = await UnitsAsync(token).ConfigureAwait(false);
        var result = await _axis.GetServersAsync(unitId, includeDescendants, token).ConfigureAwait(false);

        return new ServerLoadResult(
            result.Servers, units, categories, result.IsTruncated,
            result.IsSuccess
                ? result.IsTruncated ? "서버가 총계를 주지 않아 목록이 끊겼을 수 있습니다 — 첫 페이지만 보입니다" : null
                : result.Message);
    }

    private async Task<IReadOnlyList<ServerUnitOption>> UnitsAsync(CancellationToken token)
    {
        if (!IsUnitEra || _units is null) return Array.Empty<ServerUnitOption>();

        try
        {
            var response = await _units.Value.GetUnitsAsync(1, PageLimit, token: token).ConfigureAwait(false);
            if (!response.Success || response.Data is null) return Array.Empty<ServerUnitOption>();
            return response.Data.Where(u => u is not null)
                .Select(u => new ServerUnitOption(u.Id, u.Name, u.Code))
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 부대 목록을 받지 못했습니다 — 부대 필터를 내리지 않습니다: {ex.Message}");
            return Array.Empty<ServerUnitOption>();
        }
    }

    public Task<ServerAxisView?> GetAsync(int id, CancellationToken token = default) => _axis.GetServerAsync(id, token);
    #endregion

    #region - Write -
    public async Task<ServerWriteResult> SaveAsync(int id, ServerWriteIntent intent, CancellationToken token = default)
    {
        if (id <= 0) return new ServerWriteResult(false, "아직 등록되지 않은 서버입니다");
        if (intent is null) return new ServerWriteResult(false, "바뀐 것이 없습니다");

        // 빈 DTO 에서 본문을 만들지 않는다 — 보내기 직전에 다시 받아 검사의 기준선으로 쓴다.
        var fetched = await _axis.GetServerAsync(id, token).ConfigureAwait(false);
        if (fetched is null) return new ServerWriteResult(false, "저장 전에 서버를 다시 받지 못했습니다 — 아무것도 보내지 않았습니다");

        var errors = ServerRequestBuilder.Validate(intent, fetched, Contract);
        if (errors.Count > 0) return new ServerWriteResult(false, string.Join(" · ", errors.Select(e => e.Message)));

        await StampUnitAsync(intent, token).ConfigureAwait(false);

        var result = await _axis.PatchServerAsync(id, intent, token).ConfigureAwait(false);
        return new ServerWriteResult(result.IsSuccess, result.IsSuccess ? "설정을 저장했습니다" : result.Message);
    }

    public async Task<(ServerWriteResult Result, int NewId)> CreateAsync(
        ServerCategoryOption category, ServerWriteIntent intent, CancellationToken token = default)
    {
        if (category is null || category.Id <= 0) return (new ServerWriteResult(false, "분류를 고르십시오"), 0);
        if (intent is null) return (new ServerWriteResult(false, "채운 값이 없습니다"), 0);

        var errors = ServerRequestBuilder.Validate(intent, null, Contract);
        if (errors.Count > 0) return (new ServerWriteResult(false, string.Join(" · ", errors.Select(e => e.Message))), 0);

        intent.CategoryId = category.Id;
        intent.TypeServer = string.IsNullOrWhiteSpace(category.RawTypeServer) ? category.Type?.ToString() : category.RawTypeServer;
        await StampUnitAsync(intent, token).ConfigureAwait(false);

        var result = await _axis.CreateServerAsync(intent, token).ConfigureAwait(false);
        return result.IsSuccess
            ? (new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), result.Id)
            : (new ServerWriteResult(false, result.Message), 0);
    }

    public async Task<ServerWriteResult> AssignDeviceAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, CancellationToken token = default)
    {
        var unitId = await ResolveUnitAsync(token).ConfigureAwait(false);
        var result = await _axis.AssignDeviceServerAsync(category, deviceId, serverId, unitId, token).ConfigureAwait(false);
        return new ServerWriteResult(result.IsSuccess, result.Message);
    }

    /// <summary>
    /// 8.0 이상이면 이 클라이언트의 부대를 본문에 싣는다 — 생략하면 서버가 <b>기본 부대로 귀속</b>시키고
    /// 응답에 아무 신호도 남기지 않는다(장비 쓰기의 <see cref="UnitScopeGate"/> 와 같은 규칙).
    /// </summary>
    private async Task StampUnitAsync(ServerWriteIntent intent, CancellationToken token)
    {
        if (!IsUnitEra) { intent.UnitId = null; return; }
        if (intent.UnitId is > 0) return;            // 사람이 고른 부대가 있으면 그것을 존중한다

        intent.UnitId = await ResolveUnitAsync(token).ConfigureAwait(false);
        if (intent.UnitId is null)
            _log?.Warning("[ServerConsole] 부대 id 를 해석하지 못해 unit_id 를 생략했습니다 — 서버가 기본 부대로 귀속시킵니다");
    }

    private async Task<int?> ResolveUnitAsync(CancellationToken token)
    {
        if (!IsUnitEra) return null;
        var scope = ScopeOrNull();
        if (scope is null || !scope.IsUnitEra) return null;

        try { return await scope.ResolveAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 부대 해석 중 예외 — unit_id 를 생략합니다: {ex.Message}");
            return null;
        }
    }

    private IUnitScopeService? ScopeOrNull()
    {
        try { return _unitScope?.Value ?? UnitScopeGate.Resolve(); }
        catch { return null; }
    }
    #endregion

    #region - Metrics · legacy mode -
    public async Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default)
    {
        if (id <= 0) return null;
        try
        {
            var response = await _servers.GetServerMetricLatestAsync(id, token).ConfigureAwait(false);
            return response.Success ? response.Data?.LatestMetrics : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 서버 {id} 최신 계측 실패 — 지표 띠는 '보고 없음' 입니다: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default)
    {
        if (id <= 0) return Array.Empty<ServerMetricDto>();
        try
        {
            var response = await _servers.GetServerMetricsAsync(id, limit: Math.Clamp(limit, 1, 500), token: token).ConfigureAwait(false);
            return response.Success && response.Data is { Count: > 0 }
                ? response.Data.Where(m => m is not null).ToList()
                : Array.Empty<ServerMetricDto>();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 서버 {id} 계측 이력 실패 — {ex.Message}");
            return Array.Empty<ServerMetricDto>();
        }
    }

    public async Task<(ProxySettingDto? Setting, string? Note)> LegacyOperationModeAsync(int id, CancellationToken token = default)
    {
        // 없어진 경로는 부르지 않는다 — 7.0 은 410 이고, 그 판본의 모드는 server_config.modes 가 정본이다.
        if (!ServerWriteGuard.CanReadProxySettings(_policy)) return (null, null);
        if (id <= 0) return (null, null);

        try
        {
            var response = await _servers.GetProxySettingsAsync(id, token).ConfigureAwait(false);
            return response.Success ? (response.Data, null) : (null, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 서버 {id} 프록시 설정 실패 — {ex.Message}");
            return (null, null);
        }
    }
    #endregion
}
