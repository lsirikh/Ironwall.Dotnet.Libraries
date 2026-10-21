using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Services;

/****************************************************************************
   Purpose      : NATS SYNC_USER · SYNC_USER_GROUP 자가 필터 수신(G08, 브로커 명세 §9.19 · §9.20)
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.

   Description  : 서버(DBApi)가 계정 · 그룹을 바꾸면 알림만 온다(권위 아님 — 재조회는 REST).
                  · 계정 · 그룹 콘솔이 열려 있을 때만, 500ms 안에 온 알림을 모아 한 번 다시 읽는다(users:view / user_groups:view 있을 때만).
                  · SYNC_USER 의 resource_id 가 로그인 본인이면 /auth/me/permissions 를 다시 평가한다(403 은 조용히 넘긴다).
                  · 호스트 라우터는 건드리지 않는다 — 같은 NATS 이벤트를 cmd 문자열로 스스로 거른다(DetectionSyncNatsService 방식).
****************************************************************************/

/// <summary>알림을 받아 다시 읽을 화면 — 계정 콘솔이 구현한다.</summary>
public interface IAccountSyncTarget
{
    /// <summary>지금 열려 있는가.</summary>
    bool IsSyncTargetOpen { get; }

    /// <summary>서버가 바뀌었다고 알린 목록을 다시 읽는다(UI 스레드에서 부른다).</summary>
    Task ReloadFromServerAsync(bool users, bool groups);
}

public sealed class AccountSyncNatsService : IService
{
    public const string SyncUser = "SYNC_USER";
    public const string SyncUserGroup = "SYNC_USER_GROUP";

    public AccountSyncNatsService(
        ILogService? log,
        INatsService? natsService,
        IAccountApiService api,
        IPermissionService permission,
        ITokenStorageService? tokenStorage,
        Func<IAccountSyncTarget?> target,
        TimeSpan? window = null)
    {
        _log = log;
        _natsService = natsService;
        _api = api;
        _permission = permission;
        _tokenStorage = tokenStorage;
        _target = target;
        _window = window ?? TimeSpan.FromMilliseconds(500);
    }

    #region - IService -
    public Task ExecuteAsync(CancellationToken token = default) => StartService();

    /// <summary>구독 등록 — 여러 번 불러도 한 번만 구독한다. NATS 가 없으면(DB 모드) 아무것도 하지 않는다.</summary>
    public Task StartService()
    {
        if (_natsService is null) return Task.CompletedTask;
        _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        _natsService.NatsSubscribeEventAsync += OnNatsAsync;
        _log?.Info($"{nameof(AccountSyncNatsService)} started — {SyncUser} · {SyncUserGroup} 구독 등록");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token = default)
    {
        if (_natsService is not null) _natsService.NatsSubscribeEventAsync -= OnNatsAsync;
        return Task.CompletedTask;
    }
    #endregion

    #region - Processes -
    internal Task OnNatsAsync(MessageArgsModel e)
    {
        // 로그인 전에는 받지 않는다(로그인 게이팅 — 다른 수신 서비스와 같다).
        if (_tokenStorage is { IsAuthenticated: false }) return Task.CompletedTask;
        foreach (var envelope in Parse(e.Data))
            Accept(envelope);
        return Task.CompletedTask;
    }

    private void Accept(JObject envelope)
    {
        var cmd = envelope.Value<string>("cmd");
        var isUser = string.Equals(cmd, SyncUser, StringComparison.Ordinal);
        var isGroup = string.Equals(cmd, SyncUserGroup, StringComparison.Ordinal);
        if (!isUser && !isGroup) return;
        if (string.Equals(envelope.Value<string>("m_type"), "RSP", StringComparison.Ordinal)) return;   // 응답은 알림이 아니다

        var resourceId = (envelope["body"] as JObject)?.Value<int?>("resource_id") ?? 0;
        var self = isUser && resourceId > 0
                   && string.Equals(_tokenStorage?.UserId, resourceId.ToString(), StringComparison.Ordinal);

        lock (_gate)
        {
            _pendingUsers |= isUser;
            _pendingGroups |= isGroup;
            _pendingSelf |= self;
            if (_flushScheduled) return;
            _flushScheduled = true;
        }
        LastFlush = FlushLaterAsync();
    }

    /// <summary>창 안에 온 알림을 모아 한 번만 처리한다.</summary>
    private async Task FlushLaterAsync()
    {
        try
        {
            await Task.Delay(_window).ConfigureAwait(false);
            bool users, groups, self;
            lock (_gate)
            {
                (users, groups, self) = (_pendingUsers, _pendingGroups, _pendingSelf);
                _pendingUsers = _pendingGroups = _pendingSelf = false;
                _flushScheduled = false;
            }

            if (self) await ReevaluateMyPermissionsAsync().ConfigureAwait(false);

            var target = _target();
            if (target is null || !target.IsSyncTargetOpen) return;
            users &= _permission.CanView("users");
            groups &= _permission.CanView("user_groups");
            if (!users && !groups) return;

            await Execute.OnUIThreadAsync(() => target.ReloadFromServerAsync(users, groups)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(AccountSyncNatsService)}] 처리 실패: {ex.Message}");
        }
    }

    private async Task ReevaluateMyPermissionsAsync()
    {
        var res = await _api.GetMyPermissionsAsync().ConfigureAwait(false);
        if (res is { Success: true, Data: not null })
        {
            _permission.Refresh(res.Data);
            _log?.Info($"[{nameof(AccountSyncNatsService)}] 내 계정이 바뀌어 권한을 다시 평가했습니다");
        }
        else if (res is not null && res.IsForbidden())
        {
            // 조용히 넘긴다 — 권한 판정의 권위는 서버(요청 시점)다.
        }
        else
        {
            _log?.Warning($"[{nameof(AccountSyncNatsService)}] 권한 재평가 실패: {res?.StatusCode} {res?.Error?.Code}");
        }
    }

    private IReadOnlyList<JObject> Parse(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return Array.Empty<JObject>();
        try
        {
            return JToken.Parse(data) switch
            {
                JObject single => new[] { single },
                JArray array => array.OfType<JObject>().ToList(),
                _ => Array.Empty<JObject>(),
            };
        }
        catch (JsonException)
        {
            return Array.Empty<JObject>();   // 다른 수신 서비스가 같은 본문을 받아 따로 로그를 남긴다
        }
    }
    #endregion

    #region - Attributes -
    private readonly ILogService? _log;
    private readonly INatsService? _natsService;
    private readonly IAccountApiService _api;
    private readonly IPermissionService _permission;
    private readonly ITokenStorageService? _tokenStorage;
    private readonly Func<IAccountSyncTarget?> _target;
    private readonly TimeSpan _window;
    private readonly object _gate = new();
    private bool _pendingUsers, _pendingGroups, _pendingSelf, _flushScheduled;

    /// <summary>시험 동기화용 — 마지막으로 예약한 처리.</summary>
    internal Task? LastFlush { get; private set; }
    #endregion
}
