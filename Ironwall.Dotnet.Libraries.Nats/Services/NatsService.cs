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
            _log?.Info($"[Connect] Connecting to {serverUrl}, Subject: {setupModel.DefaultSubjectNats}");

            _defaultSubject = setupModel.DefaultSubjectNats ?? "default.>";

            // Create NatsOpts using the 'with' operator for immutable record
            var opts = NatsOpts.Default with
            {
                Url = serverUrl,
                ConnectTimeout = TimeSpan.FromMilliseconds(setupModel.ConnectionTimeoutNats)
            };

            // Set optional properties only if they have values
            if (!string.IsNullOrEmpty(setupModel.ClientNameNats))
            {
                opts = opts with { Name = setupModel.ClientNameNats };
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
            _log?.Info($"[ConnectAsync] Connecting to {serverUrl}, Subject: {setupModel.DefaultSubjectNats}");

            _defaultSubject = setupModel.DefaultSubjectNats ?? "default.>";

            _log?.Info($"[ConnectAsync] Creating NatsOpts - URL: {serverUrl}, Name: {setupModel.ClientNameNats ?? "null"}, Username: {setupModel.UsernameNats ?? "null"}");

            // Create NatsOpts using the 'with' operator for immutable record
            var opts = NatsOpts.Default with
            {
                Url = serverUrl,
                ConnectTimeout = TimeSpan.FromMilliseconds(setupModel.ConnectionTimeoutNats)
            };

            // Set optional properties only if they have values
            if (!string.IsNullOrEmpty(setupModel.ClientNameNats))
            {
                opts = opts with { Name = setupModel.ClientNameNats };
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

    public override async Task PublishAsync(string subject, string data)
    {
        try
        {
            _log?.Info($"[PublishAsync] Attempting to publish to '{subject}': {data}");

            if (Connection == null)
            {
                _log?.Warning("[PublishAsync] Connection is null! Cannot publish.");
                return;
            }

            await Connection.PublishAsync(subject, data);
            _log?.Info($"[PublishAsync] Successfully published to '{subject}': {data}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[PublishAsync] Error: {ex.Message}");
        }
    }
    #endregion

    #region - Properties -
    public string Subject => _defaultSubject;
    #endregion
}
