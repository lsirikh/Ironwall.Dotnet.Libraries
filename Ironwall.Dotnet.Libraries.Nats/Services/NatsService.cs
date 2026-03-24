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
        try
        {
            if (Connection == null) return null;

            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
            using var cts = new CancellationTokenSource(effectiveTimeout);

            var reply = await Connection.RequestAsync<string, string>(
                subject, data, cancellationToken: cts.Token);

            return reply.Data;
        }
        catch (OperationCanceledException)
        {
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
