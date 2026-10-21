using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using NATS.Client.Core;

namespace Ironwall.Dotnet.Libraries.Nats.Services;

/****************************************************************************
    Purpose      : NATS 서비스 구현 클래스
    Created By   : GHLee
    Created On   : 10/28/2025
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
    Email        : lsirikh@naver.com
 ****************************************************************************/

/// <summary>
/// NATS Core 기능을 구현하는 서비스 클래스
/// </summary>
internal class NatsService : MessageService<INatsService>, INatsService
{
    #region - Ctors -
    public NatsService()
    {
    }

    public NatsService(ILogService log) : base(log)
    {
    }
    #endregion

    #region - Overrides -
    public override INatsService? Connect(INatsSetupModel setupModel)
    {
        try
        {
            var serverUrl = $"nats://{setupModel.IpAddressNats}:{setupModel.PortNats}";
            _defaultSubject = setupModel.EffectiveSubject;

            _additionalSubjects.Clear();
            if (!string.IsNullOrEmpty(setupModel.DomainNats) && !string.IsNullOrEmpty(setupModel.GroupNats))
            {
                var broadcastSubject = $"{setupModel.DomainNats}.{setupModel.GroupNats}.all.>";
                if (broadcastSubject != _defaultSubject)
                    _additionalSubjects.Add(broadcastSubject);

                // ⚠ 전역 자원은 **부대 토큰이 아니라 `global`** 로 발행된다.
                //    서버 `db_monitor/main.py:61,64,89` — GLOBAL_UNIT_TOKEN="global",
                //    GLOBAL_CMDS = {SYNC_CATALOG, SYNC_CATEGORY, SYNC_ACTION_REPORT_TEMPLATE,
                //                   SYNC_FILE_GROUP, SYNC_UNIT} + v8.0.4 SYNC_UNIT_LAYOUT(all.sync.unit-layout).
                //    서버 공지 2026-09-29 가 요구한 `sensorway.global.all.sync.>` 는 이 한 줄(`{domain}.global.>`)이 이미 덮는다.
                //    우리 기본 구독은 `{도메인}.{부대}.{서브시스템}.>` 리터럴이라 두 번째 토큰이
                //    `global` 인 메시지를 **와일드카드로도 받지 못한다.**
                //    명세가 "단일 부대 배포는 무변화"라고 했지만 이 5종은 예외다 —
                //    구독하지 않으면 배포 당일 카테고리·카탈로그 갱신이 **조용히** 끊긴다.
                //    (소비 훅은 no-op 이 아니다: NatsDomainService.OnServerTopologyChanged 재정의 실재)
                var globalSubject = $"{setupModel.DomainNats}.global.>";
                if (globalSubject != _defaultSubject && !_additionalSubjects.Contains(globalSubject))
                    _additionalSubjects.Add(globalSubject);
            }

            var opts = NatsOpts.Default with
            {
                Url = serverUrl,
                ConnectTimeout = TimeSpan.FromMilliseconds(setupModel.ConnectionTimeoutNats)
            };

            if (!string.IsNullOrEmpty(setupModel.SubsystemNats))
            {
                opts = opts with { Name = setupModel.SubsystemNats };
            }

            if (!string.IsNullOrEmpty(setupModel.UsernameNats))
            {
                opts = opts with
                {
                    AuthOpts = new NatsAuthOpts
                    {
                        Username = setupModel.UsernameNats,
                        Password = setupModel.PasswordNats
                    }
                };
            }

            Connection = new NatsConnection(opts);
            WatchReconnect(Connection);
            return this;
        }
        catch (Exception ex)
        {
            _log?.Error($"[Connect] NATS Connection failed: {ex.Message}");
            return null;
        }
    }

    public override async Task<INatsService?> ConnectAsync(INatsSetupModel setupModel)
    {
        try
        {
            var serverUrl = $"nats://{setupModel.IpAddressNats}:{setupModel.PortNats}";
            _defaultSubject = setupModel.EffectiveSubject;

            _additionalSubjects.Clear();
            if (!string.IsNullOrEmpty(setupModel.DomainNats) && !string.IsNullOrEmpty(setupModel.GroupNats))
            {
                var broadcastSubject = $"{setupModel.DomainNats}.{setupModel.GroupNats}.all.>";
                if (broadcastSubject != _defaultSubject)
                    _additionalSubjects.Add(broadcastSubject);

                // ⚠ 전역 자원은 **부대 토큰이 아니라 `global`** 로 발행된다.
                //    서버 `db_monitor/main.py:61,64,89` — GLOBAL_UNIT_TOKEN="global",
                //    GLOBAL_CMDS = {SYNC_CATALOG, SYNC_CATEGORY, SYNC_ACTION_REPORT_TEMPLATE,
                //                   SYNC_FILE_GROUP, SYNC_UNIT} + v8.0.4 SYNC_UNIT_LAYOUT(all.sync.unit-layout).
                //    서버 공지 2026-09-29 가 요구한 `sensorway.global.all.sync.>` 는 이 한 줄(`{domain}.global.>`)이 이미 덮는다.
                //    우리 기본 구독은 `{도메인}.{부대}.{서브시스템}.>` 리터럴이라 두 번째 토큰이
                //    `global` 인 메시지를 **와일드카드로도 받지 못한다.**
                //    명세가 "단일 부대 배포는 무변화"라고 했지만 이 5종은 예외다 —
                //    구독하지 않으면 배포 당일 카테고리·카탈로그 갱신이 **조용히** 끊긴다.
                //    (소비 훅은 no-op 이 아니다: NatsDomainService.OnServerTopologyChanged 재정의 실재)
                var globalSubject = $"{setupModel.DomainNats}.global.>";
                if (globalSubject != _defaultSubject && !_additionalSubjects.Contains(globalSubject))
                    _additionalSubjects.Add(globalSubject);
            }

            var opts = NatsOpts.Default with
            {
                Url = serverUrl,
                ConnectTimeout = TimeSpan.FromMilliseconds(setupModel.ConnectionTimeoutNats)
            };

            if (!string.IsNullOrEmpty(setupModel.SubsystemNats))
            {
                opts = opts with { Name = setupModel.SubsystemNats };
            }

            if (!string.IsNullOrEmpty(setupModel.UsernameNats))
            {
                opts = opts with
                {
                    AuthOpts = new NatsAuthOpts
                    {
                        Username = setupModel.UsernameNats,
                        Password = setupModel.PasswordNats
                    }
                };
            }

            Connection = new NatsConnection(opts);
            WatchReconnect(Connection);
            await Connection.PingAsync();
            return this;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ConnectAsync] Connection failed: {ex.Message}");
            return null;
        }
    }

    public override async Task PublishAsync(string subject, string msg)
    {
        try
        {
            if (Connection == null) return;

            await Connection.PublishAsync(subject, msg);
        }
        catch (Exception ex)
        {
            _log?.Error($"[PublishAsync] Error: {ex.Message}");
        }
    }

    public override async Task<string?> RequestAsync(string subject, string data, TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
        try
        {
            if (Connection == null) return null;

            using var cts = new CancellationTokenSource(effectiveTimeout);

            var reply = await Connection.RequestAsync<string, string>(
                subject, data, cancellationToken: cts.Token);

            return reply.Data;
        }
        catch (NatsNoRespondersException)
        {
            // 구독자 자체가 없어 서버가 즉시 503 반환 — 타임아웃과 다른 실패 (수신측 미기동/미구독 진단용)
            _log?.Warning($"[RequestAsync] no responders — '{subject}' 구독자 없음 (즉시 실패, 타임아웃 아님)");
            return null;
        }
        catch (OperationCanceledException)
        {
            _log?.Warning($"[RequestAsync] timeout({effectiveTimeout.TotalSeconds:F0}s) — '{subject}' 응답 미도달");
            return null;
        }
        catch (Exception ex)
        {
            _log?.Error($"[RequestAsync] Error: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region - Reconnect -
    /// <summary>
    /// 새 연결의 열림 사건을 지켜본다(G53). 첫 열림은 처음 연결이라 알리지 않고, 그 뒤 다시 열릴 때마다 <see cref="Reconnected"/> 1회.
    /// 연결을 새로 만들면(재로그인 등) 다시 '첫 연결' 부터 센다.
    /// </summary>
    private void WatchReconnect(NatsConnection connection)
    {
        Interlocked.Exchange(ref _openedCount, 0);
        connection.ConnectionOpened += (_, _) =>
        {
            OnConnectionOpened();
            return ValueTask.CompletedTask;
        };
    }

    /// <summary>연결 열림 1회 처리 — 두 번째 열림부터 재연결로 알린다. 받는 쪽 예외는 여기서 막는다(수신 펌프 보호).</summary>
    internal void OnConnectionOpened()
    {
        if (Interlocked.Increment(ref _openedCount) == 1) return;   // 첫 연결
        _log?.Info("[NatsService] NATS 재연결됨 — Reconnected 알림");
        try { Reconnected?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { _log?.Error($"[NatsService] Reconnected 처리 오류: {ex.Message}"); }
    }

    private int _openedCount;
    #endregion

    #region - Properties -
    public string Subject => _defaultSubject;

    /// <inheritdoc/>
    public event EventHandler? Reconnected;
    #endregion
}
