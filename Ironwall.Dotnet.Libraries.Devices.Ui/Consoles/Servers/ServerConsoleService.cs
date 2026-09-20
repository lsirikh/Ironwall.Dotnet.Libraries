using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
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

/// <summary>목록 한 줄이 되기 전의 사실 — DTO + 해석된 유형 + 부대 이름.</summary>
public sealed record ServerListEntry(ServerDto Dto, EnumServerType? Type, string RawTypeServer, string? UnitName);

/// <summary>부대 필터 항목.</summary>
public sealed record ServerUnitOption(int Id, string Name, string Code);

/// <summary>등록 폼의 "분류" 선택지 — 서버 유형은 분류가 쥔다(<c>category_id</c>).</summary>
public sealed record ServerCategoryOption(int Id, string Name, EnumServerType? Type)
{
    public string Display => $"{Name} · {ServerTypeCatalog.TypeLabel(Type)}";
}

/// <summary>한 번의 적재 결과.</summary>
/// <param name="Servers">받은 서버들(유형 해석 완료).</param>
/// <param name="Units">부대 필터에 올릴 것. 8.0 미만이면 비어 있다.</param>
/// <param name="Categories">등록 폼의 분류 선택지.</param>
/// <param name="IsTruncated">총계를 알 수 없어 끊겼을 수 있는가 — 화면이 그 사실을 말해야 한다.</param>
/// <param name="Message">실패했거나 알릴 것이 있으면 한 줄. 성공이고 조용하면 <c>null</c>.</param>
public sealed record ServerLoadResult(
    IReadOnlyList<ServerListEntry> Servers,
    IReadOnlyList<ServerUnitOption> Units,
    IReadOnlyList<ServerCategoryOption> Categories,
    bool IsTruncated,
    string? Message);

/// <summary>쓰기 한 번의 결과.</summary>
public sealed record ServerWriteResult(bool IsSuccess, string Message);

/// <summary>
/// 서버 모니터의 조회·저장 통로. <b>새 인터페이스</b>다 — 기존 <see cref="IServerApiService"/> 를 넓히지 않는다
/// (그 인터페이스의 페이크가 레포 곳곳에 있다).
/// </summary>
public interface IServerConsoleService
{
    /// <summary>현재 서버 계약 세대.</summary>
    EnumServerContract Contract { get; }

    /// <summary>부대 편제(8.0 이상)를 쓸 수 있는가 — "부대" 열 · 부대 필터의 게이트.</summary>
    bool IsUnitEra { get; }

    /// <summary>프록시 설정 경로가 살아 있는 판본인가(6.3 전용).</summary>
    bool CanReadProxySettings { get; }

    Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default);

    /// <summary>단건을 <c>view=full</c> 로 다시 받는다 — 저장 직전의 기준선이다.</summary>
    Task<ServerDto?> GetAsync(int id, CancellationToken token = default);

    /// <summary>접속 · 설정 저장 — 다시 받아 채우고 고친 칸만 덮어 <c>PATCH</c> 1회.</summary>
    Task<ServerWriteResult> SaveAsync(int id, ServerEditDraft draft, CancellationToken token = default);

    /// <summary>
    /// 서버 등록 — <b>본문에 상태가 없다</b>(관측 필드를 쓰면 422). 성공하면 만들어진 Id.
    /// </summary>
    Task<(ServerWriteResult Result, int NewId)> CreateAsync(int categoryId, ServerEditDraft draft, CancellationToken token = default);

    Task<ServerMetricDto?> LatestMetricAsync(int id, CancellationToken token = default);

    Task<IReadOnlyList<ServerMetricDto>> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default);

    /// <summary>운용 모드. 6.3 이면 프록시 설정, 그 위면 <c>null</c> + 안내 한 줄.</summary>
    Task<(ProxySettingDto? Setting, string? Note)> OperationModeAsync(int id, CancellationToken token = default);

    /// <summary>스피커 한 대를 그 서버에 붙인다 — 쓰기 <b>1회</b>.</summary>
    Task<ServerWriteResult> AssignSpeakerAsync(int speakerId, int serverId, CancellationToken token = default);
}

/// <summary>
/// <see cref="IServerApiService"/> 위에 계약 게이트와 "관측 필드 금지" 를 씌운 통로.
/// </summary>
/// <remarks>
/// <para>목록 필터는 <b>부대만</b> 서버로 보낸다(8.0 이상). 유형 필터는 클라이언트가 한다 —
/// 판본마다 키가 다르고 "기타" 처럼 여러 유형을 묶은 칸을 한 질의로 표현할 수 없다
/// (<see cref="ServerTypeCatalog"/> 참고).</para>
/// <para>페이지는 <c>pagination</c> 이 있을 때만 돈다. 총계가 없으면(<c>IsTotalMissing</c>) 한 페이지로 끝내고
/// <b>끊겼을 수 있다</b>고 알린다 — 빈 페이지가 나올 때까지 도는 것보다 사실을 말하는 편이 낫다.</para>
/// </remarks>
public sealed class ServerConsoleService : IServerConsoleService
{
    private const int PageLimit = 100;
    private const int MaxPages = 20;

    private readonly IServerApiService _servers;
    private readonly IDeviceApiService _devices;
    private readonly DeviceQueryPolicy _policy;
    private readonly Lazy<IUnitApiService>? _units;
    private readonly ILogService? _log;

    public ServerConsoleService(
        IServerApiService servers,
        IDeviceApiService devices,
        DeviceQueryPolicy policy,
        Lazy<IUnitApiService>? units = null,
        ILogService? log = null)
    {
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _units = units;
        _log = log;
    }

    public EnumServerContract Contract => _policy.Contract;
    public bool IsUnitEra => ServerWriteGuard.IsUnitEra(_policy);
    public bool CanReadProxySettings => ServerWriteGuard.CanReadProxySettings(_policy);

    #region - Load -
    public async Task<ServerLoadResult> LoadAsync(int? unitId, bool includeDescendants, CancellationToken token = default)
    {
        var empty = Array.Empty<ServerListEntry>();
        var types = await CategoryTypesAsync(token).ConfigureAwait(false);
        var categories = types
            .Select(pair => new ServerCategoryOption(pair.Key, pair.Value.Name, ServerTypeCatalog.ParseType(pair.Value.Raw)))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();
        try
        {
            var units = await UnitsAsync(token).ConfigureAwait(false);

            var collected = new List<ServerDto>();
            var truncated = false;

            for (var page = 1; page <= MaxPages; page++)
            {
                token.ThrowIfCancellationRequested();

                var response = await _servers.GetServersAsync(
                    page: page,
                    limit: PageLimit,
                    view: _policy.View,
                    token: token,
                    unitId: IsUnitEra ? unitId : null,
                    includeDescendants: IsUnitEra && unitId is not null ? includeDescendants : null).ConfigureAwait(false);

                if (!response.Success)
                    return new ServerLoadResult(empty, units, categories, false, $"서버 목록을 받지 못했습니다 — {response.Message}");

                if (response.Data is { Count: > 0 }) collected.AddRange(response.Data.Where(d => d is not null));

                if (response.IsTotalMissing) { truncated = true; break; }
                if (response.HasMorePages != true) break;
            }

            var entries = collected
                .Select(dto =>
                {
                    var raw = types.TryGetValue(dto.CategoryId, out var category) ? category.Raw : string.Empty;
                    var unitName = dto.UnitId is { } id ? units.FirstOrDefault(u => u.Id == id)?.Name : null;
                    return new ServerListEntry(dto, ServerTypeCatalog.ParseType(raw), raw, unitName);
                })
                .ToList();

            return new ServerLoadResult(entries, units, categories, truncated, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 목록 적재 실패 — {ex.Message}");
            return new ServerLoadResult(empty, Array.Empty<ServerUnitOption>(), categories, false,
                "서버 목록을 받지 못했습니다 — 서버에 닿지 못했습니다");
        }
    }

    /// <summary>카테고리 id → (이름, 판별자 원문). 실패하면 빈 사전 — 유형 칸은 "—" 가 되고 목록은 산다.</summary>
    private async Task<IReadOnlyDictionary<int, (string Name, string Raw)>> CategoryTypesAsync(CancellationToken token)
    {
        var map = new Dictionary<int, (string Name, string Raw)>();
        try
        {
            var response = await _servers.GetCategoriesAsync(1, PageLimit, token).ConfigureAwait(false);
            if (response.Success && response.Data is { Count: > 0 })
                foreach (var category in response.Data.Where(c => c is not null))
                    map[category.Id] = (category.Name ?? string.Empty, category.TypeServer ?? string.Empty);
            return map;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 서버 분류를 받지 못했습니다 — 유형 칸이 빕니다: {ex.Message}");
            return map;
        }
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
    #endregion

    #region - Single -
    public async Task<ServerDto?> GetAsync(int id, CancellationToken token = default)
    {
        if (id <= 0) return null;
        try
        {
            // 단건의 서버 기본 프로필은 full 이지만, 계정 · 임계를 확실히 받으려고 섹션을 명시한다.
            var response = await _servers.GetServerByIdAsync(id, DeviceQueryPolicy.VIEW_FULL, "connection,server_config", token).ConfigureAwait(false);
            return response.Success ? response.Data : null;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 서버 {id} 단건 조회 실패 — {ex.Message}");
            return null;
        }
    }

    public async Task<ServerWriteResult> SaveAsync(int id, ServerEditDraft draft, CancellationToken token = default)
    {
        if (id <= 0) return new ServerWriteResult(false, "아직 등록되지 않은 서버입니다");
        if (draft is null) return new ServerWriteResult(false, "바뀐 것이 없습니다");

        try
        {
            // 빈 DTO 에서 본문을 만들지 않는다 — 보내기 직전에 다시 받아 그것으로 채운다.
            var fetched = await GetAsync(id, token).ConfigureAwait(false);
            if (fetched is null) return new ServerWriteResult(false, "저장 전에 서버를 다시 받지 못했습니다 — 아무것도 보내지 않았습니다");

            var errors = ServerRequestBuilder.Validate(draft, fetched);
            if (errors.Count > 0) return new ServerWriteResult(false, string.Join(" · ", errors.Select(e => e.Message)));

            var body = ServerRequestBuilder.BuildPatch(fetched, draft, _policy.Contract);
            var response = await _servers.PatchServerAsync(id, body, token).ConfigureAwait(false);
            return response.Success
                ? new ServerWriteResult(true, "설정을 저장했습니다")
                : new ServerWriteResult(false, $"저장하지 못했습니다 — {response.Message}");
        }
        catch (OperationCanceledException) { return new ServerWriteResult(false, "저장을 취소했습니다"); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 서버 {id} 저장 실패 — {ex.Message}");
            return new ServerWriteResult(false, "저장하지 못했습니다 — 서버에 닿지 못했습니다");
        }
    }

    public async Task<(ServerWriteResult Result, int NewId)> CreateAsync(int categoryId, ServerEditDraft draft, CancellationToken token = default)
    {
        if (categoryId <= 0) return (new ServerWriteResult(false, "분류를 고르십시오"), 0);
        if (draft is null) return (new ServerWriteResult(false, "채운 값이 없습니다"), 0);

        // 빈 기준선 — 등록에는 "다시 받을 것" 이 없다. 그래도 같은 조립기를 쓴다(상태가 실리지 않는 유일한 길이다).
        var seed = new ServerDto { CategoryId = categoryId, CreatedAt = null };
        var errors = ServerRequestBuilder.Validate(draft, seed);
        if (errors.Count > 0) return (new ServerWriteResult(false, string.Join(" · ", errors.Select(e => e.Message))), 0);

        try
        {
            var body = ServerRequestBuilder.BuildPatch(seed, draft, _policy.Contract);
            body.CategoryId = categoryId;

            var response = await _servers.CreateServerAsync(body, token).ConfigureAwait(false);
            if (!response.Success) return (new ServerWriteResult(false, $"등록하지 못했습니다 — {response.Message}"), 0);

            return (new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), response.Data?.Id ?? 0);
        }
        catch (OperationCanceledException) { return (new ServerWriteResult(false, "등록을 취소했습니다"), 0); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 서버 등록 실패 — {ex.Message}");
            return (new ServerWriteResult(false, "등록하지 못했습니다 — 서버에 닿지 못했습니다"), 0);
        }
    }
    #endregion

    #region - Metrics -
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
    #endregion

    #region - Operation mode -
    public async Task<(ProxySettingDto? Setting, string? Note)> OperationModeAsync(int id, CancellationToken token = default)
    {
        // 없어진 경로는 부르지 않는다 — 7.0 은 410 이고 그 사실을 화면이 문장으로 말한다.
        if (!CanReadProxySettings) return (null, ServerWriteGuard.PROXY_ABSORBED_NOTE);
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

    #region - Assign -
    public async Task<ServerWriteResult> AssignSpeakerAsync(int speakerId, int serverId, CancellationToken token = default)
    {
        if (speakerId <= 0) return new ServerWriteResult(false, "아직 등록되지 않은 장비입니다");
        if (serverId <= 0) return new ServerWriteResult(false, "아직 등록되지 않은 서버입니다");

        try
        {
            var fetched = await _devices.GetSpeakerByIdAsync(speakerId, token, DeviceQueryPolicy.VIEW_FULL).ConfigureAwait(false);
            if (!fetched.Success || fetched.Data is null)
                return new ServerWriteResult(false, $"장비 {speakerId} 을(를) 다시 받지 못했습니다 — 보내지 않았습니다");

            var body = ServerRequestBuilder.BuildSpeakerAssign(fetched.Data, serverId);
            await UnitScopeGate.StampAsync(body, nameof(AssignSpeakerAsync), _log, token).ConfigureAwait(false);

            var response = await _devices.PatchSpeakerAsync(speakerId, body, token).ConfigureAwait(false);
            return response.Success
                ? new ServerWriteResult(true, string.Empty)
                : new ServerWriteResult(false, response.Message);
        }
        catch (OperationCanceledException) { return new ServerWriteResult(false, "배정을 취소했습니다"); }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 장비 {speakerId} → 서버 {serverId} 배정 실패 — {ex.Message}");
            return new ServerWriteResult(false, "서버에 닿지 못했습니다");
        }
    }
    #endregion
}
