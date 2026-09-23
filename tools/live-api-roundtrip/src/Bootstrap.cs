using System.Net.Http;
using System.Net.Security;
using Ironwall.Dotnet.Libraries.Accounts.Api.Handlers;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace LiveApiRoundTrip;

/// <summary>
/// Attaches the REAL library services to the LOCAL test server.
/// Nothing here reads the host app's appsettings.json - the URL is a literal
/// loopback constant, asserted before use.
/// </summary>
public sealed class Bootstrap
{
    public const string BASE_URL = "https://127.0.0.1:8000/api";
    public const string LOGIN_ID = "admin";
    public const string LOGIN_PW  = "admin123";

    public ApiSetupModel Setup { get; private set; }
    public ILogService Log { get; private set; }
    public IApiService Api { get; private set; }
    public ITokenStorageService Tokens { get; private set; }
    public IAccountApiService AccountApi { get; private set; }
    public IServerContractProbe Probe { get; private set; }
    public WireCaptureHandler Wire { get; private set; }

    readonly Recorder _rec;
    public Bootstrap(Recorder rec) { _rec = rec; }

    public async Task InitAsync()
    {
        LoopbackGuard.Assert(BASE_URL);                 // <-- hard safety gate

        Log = new LogService();
        Setup = new ApiSetupModel { Url = BASE_URL, Timeout = 30 };
        LoopbackGuard.Assert(Setup.Url);

        Tokens = new TokenStorageService();

        // transport we own: cert bypass for the local self-signed cert.
        var tls = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
            {
                // Only ever bypass for loopback.
                if (req.RequestUri is not null) LoopbackGuard.Assert(req.RequestUri);
                return true;
            }
        };

        IAccountApiService accountApiRef = null;
        var bearer = new BearerAuthHandler(Tokens, () => accountApiRef, Log) { InnerHandler = tls };
        Wire = new WireCaptureHandler(bearer, _rec.Wire);

        Api = new ApiService(Log, Setup, Wire);
        Api.Initialize();

        AccountApi = new AccountApiService(Api, Log);
        accountApiRef = AccountApi;

        Probe = new ServerContractProbe(Log, Setup);
    }

    public async Task<bool> LoginAsync()
    {
        Wire.CurrentTag = "login";
        var res = await AccountApi.LoginAsync(LOGIN_ID, LOGIN_PW).ConfigureAwait(false);
        var data = res?.Data;
        if (data is null || string.IsNullOrEmpty(data.AccessToken)) return false;
        Tokens.SetTokens(data.AccessToken, data.RefreshToken, data.SessionId);
        return true;
    }
}
