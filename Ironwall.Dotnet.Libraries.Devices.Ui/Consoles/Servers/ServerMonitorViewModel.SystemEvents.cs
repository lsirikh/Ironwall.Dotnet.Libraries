using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Dto.Systems;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 콘솔 "시스템 이벤트" 레일(G52) · 서버 변경 알림(G54).
                  · 레일 칸은 공급자(ISystemEventSource)가 있을 때만 붙는다.
                  · 목록은 GET /api/system-events — 404 면 "이 서버 판은 제공하지 않음".
                  · SYSTEM_EVENT(새 줄) · SYNC_SYSTEM_EVENT(다시 읽기 · 빼기)는 ServerSystemEventNatsService 가 메시지로 알린다.
                  · ServersChangedMessage(호스트 H1 발행) — 콘솔이 열려 있으면 서버 목록을 다시 읽는다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public partial class ServerMonitorViewModel
{
    /// <summary>한 번에 받아 오는 시스템 이벤트 수(서버 상한 100).</summary>
    public const int SystemEventPageSize = 100;

    /// <summary>404 일 때의 안내.</summary>
    public const string SystemEventsUnsupportedText = "이 서버 판은 시스템 이벤트를 제공하지 않습니다.";

    /// <summary>시스템 이벤트 목록(최신이 위).</summary>
    public ObservableCollection<ServerSystemEventRow> SystemEventRows { get; } = new();

    /// <summary>목록에 줄이 있는가 — 없으면 안내 상자를 보인다.</summary>
    public bool HasSystemEventRows => SystemEventRows.Count > 0;

    /// <summary>시스템 이벤트 칸을 쓸 수 있는 키인가(공급자가 있을 때만).</summary>
    private bool IsSystemEventsRail(string? key)
        => _systemEvents is not null && string.Equals(key, ServerTypeCatalog.SystemEventsKey, StringComparison.Ordinal);

    private string SystemEventsNoteText()
    {
        if (_systemEvents is null) return "시스템 이벤트를 아직 제공하지 않습니다.";   // 공급자 없음(예전 문장)
        if (!_systemEventsSupported) return SystemEventsUnsupportedText;
        return "받은 시스템 이벤트가 없습니다.";
    }

    private string SystemEventsStatusText()
    {
        if (!_systemEventsSupported) return "시스템 이벤트";
        var open = SystemEventRows.Count(r => !r.IsAcknowledged);
        return $"시스템 이벤트 {SystemEventRows.Count}건 · 미확인 {open}";
    }

    /// <summary>목록을 다시 읽는다. 404 면 "이 서버 판은 제공하지 않음". 예외는 막는다(콘솔 보호).</summary>
    public async Task LoadSystemEventsAsync(CancellationToken token)
    {
        if (_systemEvents is null) return;
        try
        {
            var res = await _systemEvents.GetSystemEventsAsync(new SystemEventQuery(Limit: SystemEventPageSize), token).ConfigureAwait(true);
            if (res.StatusCode == 404)
            {
                _systemEventsSupported = false;
                SystemEventRows.Clear();
            }
            else if (res.Success)
            {
                _systemEventsSupported = true;
                SystemEventRows.Clear();
                foreach (var dto in (res.Data ?? new()).OrderByDescending(d => d.CreatedAt, StringComparer.Ordinal))
                    SystemEventRows.Add(new ServerSystemEventRow(dto));
            }
            else
            {
                _log?.Warning($"[ServerMonitor] 시스템 이벤트 조회 실패: status={res.StatusCode} {res.Message}");
                StatusText = "시스템 이벤트를 불러오지 못했습니다. [새로 불러오기]를 누르세요.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log?.Error($"[ServerMonitor] 시스템 이벤트 조회 오류: {ex.Message}");
            StatusText = "시스템 이벤트를 불러오지 못했습니다. [새로 불러오기]를 누르세요.";
        }
        RaiseSystemEvents();
    }

    /// <summary>[확인] — 이미 확인된 것(409/400)도 확인됨으로 둔다.</summary>
    public async Task AcknowledgeSystemEventAsync(ServerSystemEventRow row)
    {
        if (_systemEvents is null || row is null || row.IsAcknowledged) return;
        var by = ResolveAcknowledger();
        var outcome = await _systemEvents.AcknowledgeSystemEventAsync(row.Id, by).ConfigureAwait(true);
        switch (outcome)
        {
            case SystemEventAckOutcome.Acknowledged:
            case SystemEventAckOutcome.AlreadyAcknowledged:
                row.MarkAcknowledged(by);
                StatusText = outcome == SystemEventAckOutcome.Acknowledged ? "시스템 이벤트를 확인했습니다." : "이미 확인된 시스템 이벤트입니다.";
                break;
            case SystemEventAckOutcome.NotSupported:
                _systemEventsSupported = false;
                StatusText = SystemEventsUnsupportedText;
                break;
            default:
                StatusText = "확인하지 못했습니다. 잠시 뒤 다시 시도하세요.";
                break;
        }
        RaiseSystemEvents();
    }

    /// <summary>SYSTEM_EVENT · SYNC_SYSTEM_EVENT(G52) — 열려 있을 때만 목록을 고친다.</summary>
    public async Task HandleAsync(ServerSystemEventMessage message, CancellationToken cancellationToken)
    {
        if (message is null || _systemEvents is null || !IsActive) return;
        if (message.Deleted)
        {
            if (SystemEventRows.FirstOrDefault(r => r.Id == message.Id) is { } gone) SystemEventRows.Remove(gone);
        }
        else if (message.Event is { Id: > 0 } dto)
        {
            var existing = SystemEventRows.FirstOrDefault(r => r.Id == dto.Id);
            if (existing is not null) SystemEventRows[SystemEventRows.IndexOf(existing)] = new ServerSystemEventRow(dto);
            else SystemEventRows.Insert(0, new ServerSystemEventRow(dto));
        }
        else if (message.Id > 0 && SystemEventRows.Any(r => r.Id == message.Id))
        {
            // 알림형 UPDATED — 가진 줄만 다시 읽는다.
            var res = await _systemEvents.GetSystemEventByIdAsync(message.Id, cancellationToken).ConfigureAwait(true);
            var at = SystemEventRows.FirstOrDefault(r => r.Id == message.Id);
            if (at is not null && res.StatusCode == 404) SystemEventRows.Remove(at);
            else if (at is not null && res is { Success: true, Data: { } fresh }) SystemEventRows[SystemEventRows.IndexOf(at)] = new ServerSystemEventRow(fresh);
        }
        RaiseSystemEvents();
    }

    /// <summary>
    /// 서버가 바뀌었다(G54, 호스트 H1 발행) — 콘솔이 열려 있으면 다시 읽는다.
    /// 적용하지 않은 편집이 있으면 덮지 않고 알리기만 한다(운영자가 [새로 불러오기] 를 고른다).
    /// </summary>
    public Task HandleAsync(ServersChangedMessage message, CancellationToken cancellationToken)
    {
        if (!IsActive) return Task.CompletedTask;
        if (Detail.IsDirty)
        {
            StatusText = "다른 곳에서 서버 정보가 바뀌었습니다. 편집을 마친 뒤 [새로 불러오기]를 누르세요.";
            return Task.CompletedTask;
        }
        return ReloadAsync(cancellationToken);
    }

    private void RaiseSystemEvents()
    {
        NotifyOfPropertyChange(nameof(HasSystemEventRows));
        NotifyOfPropertyChange(nameof(SystemEventsNote));
        if (IsSystemEvents) RefreshStatus();
    }

    private static string ResolveAcknowledger()
    {
        try { return IoC.Get<Ironwall.Dotnet.Monitoring.Models.Accounts.IAccountModel>()?.Username is { Length: > 0 } name ? name : "GIS"; }
        catch { return "GIS"; }
    }

    private readonly ISystemEventSource? _systemEvents;
    private bool _systemEventsSupported = true;
}

/// <summary>시스템 이벤트 한 줄 — 심각도는 글자와 함께 <b>모양</b>(아이콘)으로도 가른다(색만으로 구분하지 않는다).</summary>
public sealed class ServerSystemEventRow : PropertyChangedBase
{
    public ServerSystemEventRow(SystemEventDto dto)
    {
        Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        _isAcknowledged = dto.Acknowledged;
    }

    public SystemEventDto Dto { get; }
    public int Id => Dto.Id;
    public string TimeText => DateTimeOffset.TryParse(Dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
        ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : (Dto.CreatedAt ?? "—");
    public string Title => string.IsNullOrWhiteSpace(Dto.Title) ? (Dto.TypeEvent ?? "—") : Dto.Title!;
    public string Message => Dto.Message ?? string.Empty;
    public string ServerText => Dto.ServerDescription ?? (Dto.EffectiveServerId is int id ? $"서버 {id}" : "전체");
    public string SeverityText => SeverityTextOf(Dto.Severity);
    public string SeverityIcon => SeverityIconOf(Dto.Severity);

    public bool IsAcknowledged
    {
        get => _isAcknowledged;
        private set { _isAcknowledged = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(AckText)); NotifyOfPropertyChange(nameof(CanAcknowledge)); }
    }

    /// <summary>[확인] 단추를 누를 수 있는가 — 확인 안 된 줄만.</summary>
    public bool CanAcknowledge => !IsAcknowledged;

    public string AckText => IsAcknowledged ? "확인됨" : "미확인";

    internal void MarkAcknowledged(string by)
    {
        Dto.IsAcknowledged = true;
        Dto.AcknowledgedBy ??= by;
        IsAcknowledged = true;
    }

    /// <summary>심각도 글자. 모르는 값은 원문.</summary>
    public static string SeverityTextOf(string? severity) => severity?.Trim().ToUpperInvariant() switch
    {
        "INFO" => "정보",
        "WARNING" => "경고",
        "ERROR" => "오류",
        "CRITICAL" => "심각",
        null or "" => "—",
        _ => severity!.Trim(),
    };

    /// <summary>심각도 모양 — 정보 ⓘ · 경고 △ · 오류 ○! · 심각 팔각형.</summary>
    public static string SeverityIconOf(string? severity) => severity?.Trim().ToUpperInvariant() switch
    {
        "INFO" => "InformationOutline",
        "WARNING" => "AlertOutline",
        "ERROR" => "AlertCircleOutline",
        "CRITICAL" => "AlertOctagon",
        _ => "HelpCircleOutline",
    };

    private bool _isAcknowledged;
}

/// <summary>
/// 시스템 이벤트 수신 메시지(G52) — <see cref="Event"/> 가 있으면 그 줄을 넣거나 바꾸고,
/// 없으면 <see cref="Id"/> 의 줄을 다시 읽거나(<see cref="Deleted"/> 거짓) 뺀다(참).
/// </summary>
public sealed record ServerSystemEventMessage(int Id, SystemEventDto? Event, bool Deleted);

/// <summary>
/// 서버(관리 대상 서버)가 바뀌었다(G54) — 열린 서버 콘솔이 다시 읽는다. 발행은 호스트 NATS 라우터(SYNC_SERVER · 6.3 SYNC_PROXY_SETTING)가 한다.
/// </summary>
/// <param name="ServerId">바뀐 서버 id. 모르면(일괄) <c>null</c>.</param>
public sealed record ServersChangedMessage(int? ServerId = null);
