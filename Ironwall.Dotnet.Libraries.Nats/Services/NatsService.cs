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
            _log?.Info($"[Connect] Connecting to {serverUrl}, Subject: {setupModel.EffectiveSubject}");

            _defaultSubject = setupModel.EffectiveSubject;

            // DomainNats + GroupNats가 설정된 경우 "all" 브로드캐스트 subject 자동 추가
            _additionalSubjects.Clear();
            if (!string.IsNullOrEmpty(setupModel.DomainNats) && !string.IsNullOrEmpty(setupModel.GroupNats))
            {
                var broadcastSubject = $"{setupModel.DomainNats}.{setupModel.GroupNats}.all.>";
                if (broadcastSubject != _defaultSubject)
                    _additionalSubjects.Add(broadcastSubject);
            }

            _log?.Info($"[Connect] Additional subjects: [{string.Join(", ", _additionalSubjects)}]");

            // Create NatsOpts using the 'with' operator for immutable record
            var opts = NatsOpts.Default with
            {
                Url = serverUrl,
                ConnectTimeout = TimeSpan.FromMilliseconds(setupModel.ConnectionTimeoutNats)
            };

            // Set optional properties only if they have values
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
            _log?.Info($"[Connect] NATS Connection established to {serverUrl}");

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
            _log?.Info($"[ConnectAsync] Connecting to {serverUrl}, Subject: {setupModel.EffectiveSubject}");

            _defaultSubject = setupModel.EffectiveSubject;

            // DomainNats + GroupNats가 설정된 경우 "all" 브로드캐스트 subject 자동 추가
            _additionalSubjects.Clear();
            if (!string.IsNullOrEmpty(setupModel.DomainNats) && !string.IsNullOrEmpty(setupModel.GroupNats))
            {
                var broadcastSubject = $"{setupModel.DomainNats}.{setupModel.GroupNats}.all.>";
                if (broadcastSubject != _defaultSubject)
                    _additionalSubjects.Add(broadcastSubject);
            }

            _log?.Info($"[ConnectAsync] Additional subjects: [{string.Join(", ", _additionalSubjects)}]");
            _log?.Info($"[ConnectAsync] Creating NatsOpts - URL: {serverUrl}, Name: {setupModel.SubsystemNats ?? "null"}, Username: {setupModel.UsernameNats ?? "null"}");

            // Create NatsOpts using the 'with' operator for immutable record
            var opts = NatsOpts.Default with
            {
                Url = serverUrl,
                ConnectTimeout = TimeSpan.FromMilliseconds(setupModel.ConnectionTimeoutNats)
            };

            // Set optional properties only if they have values
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

            _log?.Info("[ConnectAsync] NatsOpts created, creating NatsConnection...");
            Connection = new NatsConnection(opts);
            _log?.Info("[ConnectAsync] NatsConnection created, pinging server...");

            // NATS v2: 연결은 지연 초기화됨 - Ping으로 연결 확인
            await Connection.PingAsync();

            _log?.Info("[ConnectAsync] Connection successful!");
            return this;
        }
        catch (Exception ex)
        {
            _log?.Error($"[ConnectAsync] Connection failed: {ex.Message}");
            _log?.Error($"[ConnectAsync] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                _log?.Error($"[ConnectAsync] Inner exception: {ex.InnerException.Message}");
            }
            return null;
        }
    }

    public override async Task PublishAsync(string subject, string msg)
    {
        try
        {
            _log?.Info($"[PublishAsync] Attempting to publish to '{subject}': {msg}");

            if (Connection == null)
            {
                _log?.Warning("[PublishAsync] Connection is null! Cannot publish.");
                return;
            }

            await Connection.PublishAsync(subject, msg);
            _log?.Info($"[PublishAsync] Successfully published to '{subject}': {msg}");
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
            if (Connection == null)
            {
                _log?.Warning("[RequestAsync] Connection is null!");
                return null;
            }

            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
            using var cts = new CancellationTokenSource(effectiveTimeout);

            var reply = await Connection.RequestAsync<string, string>(
                subject, data, cancellationToken: cts.Token);

            _log?.Info($"[RequestAsync] Reply from '{subject}': {reply.Data}");
            return reply.Data;
        }
        catch (OperationCanceledException)
        {
            _log?.Warning($"[RequestAsync] Timeout for '{subject}'");
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
