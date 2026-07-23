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

    #region - Properties -
    public string Subject => _defaultSubject;
    #endregion
}
