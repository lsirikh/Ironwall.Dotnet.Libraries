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

    /// <summary>계측 이력. <b><c>null</c> = 불러오지 못함</b>(원문은 로그) · 빈 목록 = 기록 없음 — 화면이 둘을 다른 문장으로 말한다.</summary>
    Task<IReadOnlyList<ServerMetricDto>?> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default);

    /// <summary>운용 모드. 6.3 이면 프록시 설정 경로, 7.0+ 는 <c>server_config.modes</c> 라 여기서는 <c>null</c>.</summary>
    Task<(ProxySettingDto? Setting, string? Note)> LegacyOperationModeAsync(int id, CancellationToken token = default);

    /// <summary>장비 한 대의 관리 서버를 바꾼다 — 쓰기 <b>1회</b>. <paramref name="serverId"/> 가 <c>null</c> 이면 해제.</summary>
    Task<ServerWriteResult> AssignDeviceAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, CancellationToken token = default);

    /// <summary>
    /// 장비 한 대가 서버 기준으로 지금 붙어 있는 관리 서버 — 배정 <b>직전에</b> 읽어 되돌리기의 기준으로 쓴다.
    /// 읽지 못하면 <see cref="DeviceServerLookup.Known"/> 이 false 다("서버 없음" 과 다르다).
    /// </summary>
    /// <remarks>기본 구현은 "읽지 못함" — 이 인터페이스의 페이크를 깨지 않으려고 기본 구현을 둔다.</remarks>
    Task<DeviceServerLookup> GetDeviceServerAsync(EnumDeviceCategory category, int deviceId, CancellationToken token = default)
        => Task.FromResult(DeviceServerLookup.Unknown);

    /// <summary>배정할 수 있는 장비 전부의 지금 관리 서버(장비 Id → 서버 Id, 없으면 null). 읽지 못한 장비는 빠진다.</summary>
    Task<IReadOnlyDictionary<int, int?>> GetDeviceServerMapAsync(CancellationToken token = default)
        => Task.FromResult<IReadOnlyDictionary<int, int?>>(new Dictionary<int, int?>());
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
                ? result.IsTruncated ? "목록의 앞부분만 표시했습니다. 부대 필터나 검색으로 범위를 좁히세요." : null
                : Plain(result.Message, 0, "서버 목록을 불러오지 못했습니다. 잠시 후 [갱신]을 누르세요."));
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
        if (id <= 0) return new ServerWriteResult(false, "아직 등록되지 않은 서버입니다.");
        if (intent is null) return new ServerWriteResult(false, "바뀐 내용이 없습니다.");

        // 빈 DTO 에서 본문을 만들지 않는다 — 보내기 직전에 다시 받아 검사의 기준선으로 쓴다.
        var fetched = await _axis.GetServerAsync(id, token).ConfigureAwait(false);
        if (fetched is null) return new ServerWriteResult(false, "서버 정보를 다시 불러오지 못해 저장하지 않았습니다. 잠시 후 다시 시도하세요.");

        var errors = ServerRequestBuilder.Validate(intent, fetched, Contract);
        if (errors.Count > 0) return new ServerWriteResult(false, string.Join(" · ", errors.Select(e => e.Message)));

        // 수정은 소속을 옮기지 않는다 — 자기 부대를 찍지 않고 방금 다시 받은 서버의 부대를 싣는다(D-13 과 같은 규칙).
        PreserveUnitForEdit(intent, fetched);

        var result = await _axis.PatchServerAsync(id, intent, token).ConfigureAwait(false);
        return new ServerWriteResult(result.IsSuccess, result.IsSuccess
            ? "설정을 저장했습니다."
            : Plain(result.Message, result.StatusCode, "설정을 저장하지 못했습니다. 입력값을 확인하고 다시 시도하세요."));
    }

    public async Task<(ServerWriteResult Result, int NewId)> CreateAsync(
        ServerCategoryOption category, ServerWriteIntent intent, CancellationToken token = default)
    {
        if (category is null || category.Id <= 0) return (new ServerWriteResult(false, "분류를 고르세요."), 0);
        if (intent is null) return (new ServerWriteResult(false, "입력한 값이 없습니다."), 0);

        var errors = ServerRequestBuilder.Validate(intent, null, Contract);
        if (errors.Count > 0) return (new ServerWriteResult(false, string.Join(" · ", errors.Select(e => e.Message))), 0);

        intent.CategoryId = category.Id;
        intent.TypeServer = string.IsNullOrWhiteSpace(category.RawTypeServer) ? category.Type?.ToString() : category.RawTypeServer;
        await StampUnitAsync(intent, token).ConfigureAwait(false);

        var result = await _axis.CreateServerAsync(intent, token).ConfigureAwait(false);
        return result.IsSuccess
            ? (new ServerWriteResult(true, ServerStatusRules.JustRegisteredNotice), result.Id)
            : (new ServerWriteResult(false, Plain(result.Message, result.StatusCode, "서버를 등록하지 못했습니다. 입력값을 확인하고 다시 시도하세요.")), 0);
    }

    /// <remarks>
    /// 배정은 <c>server_id</c> 하나만 바꾸는 병합 패치다 — <b><c>unit_id</c> 를 싣지 않는다</b>.
    /// 예전에는 이 클라이언트의 부대를 실어, 다른 부대 장비를 배정·되돌리기 하면 그 장비가 조용히 내 부대로 옮겨졌다
    /// (실측: 본문 <c>{"server_id":18,"unit_id":1}</c> → 부대 B 의 제어기가 부대 1 로). 서버의 수정 경로는
    /// 미전송 키를 그대로 둔다(<c>device_axes_io.apply_scalar_fields</c> · <c>unit_scope.assert_unit_exists</c>) —
    /// 등록과 달리 기본 부대로 귀속시키지 않는다. 화면이 쥔 장비 모델의 부대는 낡았을 수 있어 싣지 않는다.
    /// </remarks>
    public async Task<ServerWriteResult> AssignDeviceAsync(
        EnumDeviceCategory category, int deviceId, int? serverId, CancellationToken token = default)
    {
        var result = await _axis.AssignDeviceServerAsync(category, deviceId, serverId, unitId: null, token).ConfigureAwait(false);
        return new ServerWriteResult(result.IsSuccess, result.IsSuccess
            ? result.Message
            : Plain(result.Message, result.StatusCode, "서버가 요청을 받아들이지 않았습니다."));
    }

    public async Task<DeviceServerLookup> GetDeviceServerAsync(EnumDeviceCategory category, int deviceId, CancellationToken token = default)
    {
        // 6.3 은 스피커만 서버 축이 있고 그것은 모델에 이미 실려 있다 — 읽으러 가지 않는다.
        if (!IsAxisEra) return DeviceServerLookup.Unknown;
        try { return await _axis.GetDeviceServerAsync(category, deviceId, token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 장비 {deviceId} 의 관리 서버를 읽지 못했습니다: {ex.Message}");
            return DeviceServerLookup.Unknown;
        }
    }

    public async Task<IReadOnlyDictionary<int, int?>> GetDeviceServerMapAsync(CancellationToken token = default)
    {
        var map = new Dictionary<int, int?>();
        if (!IsAxisEra) return map;

        // 장비 Id 는 축 계약에서 전역 유일이다 — 카테고리를 가로질러 한 사전에 담아도 겹치지 않는다.
        foreach (var category in ServerDropRules.AllowedServerTypes.Keys)
        {
            try
            {
                var part = await _axis.GetDeviceServerMapAsync(category, token).ConfigureAwait(false);
                if (part is null) continue;
                foreach (var pair in part) map[pair.Key] = pair.Value;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log?.Warning($"[ServerConsole] {category} 장비의 관리 서버를 읽지 못했습니다: {ex.Message}"); }
        }
        return map;
    }

    /// <summary>
    /// 서버 · 통로의 원문 사유(HTTP 코드 · 서버 메시지)는 <b>화면에 붙이지 않는다</b> — 로그로 보내고 고정 문장을 돌려준다
    /// (U-18 감사 공통 규칙). 같은 이름 충돌(409)만 운영자가 고칠 수 있는 까닭이라 따로 말한다.
    /// </summary>
    private string Plain(string? raw, int statusCode, string fixedText)
    {
        if (!string.IsNullOrWhiteSpace(raw)) _log?.Warning($"[ServerConsole] 서버 응답 원문({statusCode}): {raw}");
        return statusCode == 409 ? "같은 이름의 서버가 이미 있습니다. 다른 이름을 쓰세요." : fixedText;
    }

    /// <summary>
    /// 수정 본문의 부대 — 사람이 고른 부대가 있으면 그것을, 없으면 <b>저장 직전에 다시 받은 서버의 부대</b>를 싣는다.
    /// 이 클라이언트의 부대는 <b>찍지 않는다</b> — 찍으면 다른 부대 서버의 임계만 고쳐도 서버가 내 부대로 옮겨진다
    /// (실측: 본문 <c>{"server_config":…,"unit_id":1}</c> → 부대 B 의 서버가 부대 1 로). 받은 부대가 없으면
    /// 키를 싣지 않는다 — PATCH 에서 미전송은 "그대로 두라" 다.
    /// </summary>
    private void PreserveUnitForEdit(ServerWriteIntent intent, ServerAxisView fetched)
    {
        if (!IsUnitEra) { intent.UnitId = null; return; }
        if (intent.UnitId is > 0) return;            // 사람이 고른 부대가 있으면 그것을 존중한다
        intent.UnitId = fetched.UnitId is > 0 ? fetched.UnitId : null;
    }

    /// <summary>
    /// <b>등록 전용</b> — 8.0 이상이면 이 클라이언트의 부대를 본문에 싣는다. 생략하면 서버가 <b>기본 부대로 귀속</b>시키고
    /// 응답에 아무 신호도 남기지 않는다(장비 쓰기의 <see cref="UnitScopeGate"/> 와 같은 규칙).
    /// 수정·배정에는 쓰지 않는다 — <see cref="PreserveUnitForEdit"/> · <see cref="AssignDeviceAsync"/>.
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

    public async Task<IReadOnlyList<ServerMetricDto>?> MetricHistoryAsync(int id, int limit = 50, CancellationToken token = default)
    {
        if (id <= 0) return Array.Empty<ServerMetricDto>();
        try
        {
            var response = await _servers.GetServerMetricsAsync(id, limit: Math.Clamp(limit, 1, 500), token: token).ConfigureAwait(false);
            if (!response.Success)
            {
                // 실패와 "기록 없음" 을 섞지 않는다 — 실패는 null 로 돌려 화면이 다른 문장을 쓰게 한다.
                _log?.Warning($"[ServerConsole] 서버 {id} 계측 이력 응답 실패");
                return null;
            }
            return response.Data is { Count: > 0 }
                ? response.Data.Where(m => m is not null).ToList()
                : Array.Empty<ServerMetricDto>();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[ServerConsole] 서버 {id} 계측 이력 실패 — {ex.Message}");
            return null;
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
