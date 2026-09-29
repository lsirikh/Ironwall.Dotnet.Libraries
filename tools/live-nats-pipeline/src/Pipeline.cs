using System.IO;
using System.Windows.Threading;
using Autofac;
using Caliburn.Micro;
using Dotnet.Monitoring.Solution.Models;
using Dotnet.Monitoring.Solution.Services;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Modules;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.Redis.Services;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace LiveNatsPipeline;

/// <summary>
/// The fake device inventory (ids/types mirror the loopback GIS app log of 2026-09-30; bodies are the server's
/// own v7 fixtures). Groups 11/12/13 are probe-only numbers.
/// </summary>
public static class Inv
{
    public static readonly DevSpec Ctrl1 = new(2113, "controller", "Controller", 1, "PRB-CTRL-01", new[] { 11 });
    public static readonly DevSpec Ctrl2 = new(2115, "controller", "Controller", 2, "PRB-CTRL-02", new[] { 12 });
    public static readonly DevSpec Smart = new(2131, "sensor", "SmartSensor2", 101, "PRB-SMART-01", new[] { 11 }, 2113);
    public static readonly DevSpec Smart2 = new(2132, "sensor", "SmartSensor2", 102, "PRB-SMART-02", new[] { 11 }, 2113);
    public static readonly DevSpec Multi = new(2126, "sensor", "Multi", 103, "PRB-MULTI-01", new[] { 11, 12 }, 2113);
    public static readonly DevSpec Pir = new(2114, "sensor", "PIR", 201, "PRB-PIR-01", new[] { 12 }, 2115);
    public static readonly DevSpec Fence = new(2200, "sensor", "Fence", 202, "PRB-FENCE-01", new[] { 13 }, 2115);
    public static readonly DevSpec Compound = new(2201, "sensor", "SmartCompound", 203, "PRB-COMPOUND-01", new[] { 13 }, 2115);
    public static readonly DevSpec Camera = new(379, "camera", "PTZ", 301, "PRB-CAM-01", new[] { 11 });
    public static readonly DevSpec Enclosure = new(2159, "enclosure", "Unknown", 501, "PRB-ENC-01", new[] { 13 });
    public static readonly DevSpec Gate = new(2157, "gate", "Sliding", 601, "PRB-GATE-01", new[] { 13 }, 2115);
    public static readonly DevSpec Lamp = new(2156, "lamp", "Unknown", 701, "PRB-LAMP-01", Array.Empty<int>());
    public static readonly DevSpec Speaker = new(2154, "speaker", "Unknown", 801, "PRB-SPK-01", Array.Empty<int>());

    public static readonly DevSpec[] All = { Ctrl1, Ctrl2, Smart, Smart2, Multi, Pir, Fence, Compound, Camera, Enclosure, Gate, Lamp, Speaker };
}

public sealed class Pipeline
{
    public CaptureLog Log = null!;
    public EventAggregator Ea = null!;
    public EaRecorder EaRec = new();
    public FakeGopServer Server = null!;
    public ApiService Api = null!;
    public DeviceApiService DeviceApi = null!;
    public EventApiService EventApi = null!;
    public ServerApiService ServerApi = null!;
    public DeviceProvider Devices = null!;
    public CameraDeviceProvider Cameras = null!;
    public DeviceProviderService DeviceSvc = null!;
    public TokenStorageService Tokens = null!;
    public EventSetupModel EventSetup = null!;
    public EventQueueManager Eqm = null!;
    public SymbolEventManager Sem = null!;
    public EventCardListPanelViewModel Cards = null!;
    public NatsSetupModel NatsSetup = null!;
    public GuardedNatsService Nats = null!;
    public IContainer NatsContainer = null!;
    public DetectionNatsSyncService Det = null!;
    public MalfunctionNatsSyncService Mal = null!;
    public OperationEventNatsSyncService Op = null!;
    public DetectionSyncNatsService DetSync = null!;
    internal NatsDomainService Host = null!;
    public readonly Dictionary<int, PidsSymbolModel> DeviceSymbols = new();
    public readonly Dictionary<int, PidsGroupSymbolModel> GroupSymbols = new();
    public readonly List<string> AutoRecoveryFired = new();
    public readonly List<string> AutoReportFired = new();
    public Dispatcher Ui = null!;
    public string NatsUrl = "";
    public bool Real;
    public const string FakeToken = "probe-local-token-not-a-jwt";
    string? _realAccess, _realRefresh, _realSession;

    /// <summary>Re-open the login gate after a scenario closed it (fake token, or the one real session's tokens).</summary>
    public void RestoreTokens()
    {
        if (!Real) { Tokens.SetTokens(FakeToken); return; }
        _realAccess ??= Tokens.AccessToken; _realRefresh ??= Tokens.RefreshToken; _realSession ??= Tokens.SessionId;
        if (!Tokens.IsAuthenticated && _realAccess != null) Tokens.SetTokens(_realAccess, _realRefresh, _realSession);
    }

    public async Task BuildAsync(string outDir, string natsUrl)
    {
        Ui = Dispatcher.CurrentDispatcher;
        NatsUrl = natsUrl;
        Safety.AssertLoopback(natsUrl.Replace("nats://", "http://"));

        // Caliburn: same platform provider the app's bootstrapper installs; IoC resolves nothing (no container).
        PlatformProvider.Current = new XamlPlatformProvider();
        IoC.GetInstance = (t, k) => null!;
        IoC.GetAllInstances = t => Array.Empty<object>();
        IoC.BuildUp = o => { };

        Log = new CaptureLog(Path.Combine(outDir, "pipeline.log"));
        Ea = new EventAggregator();
        Ea.SubscribeOnPublishedThread(EaRec);

        // ---- REST ----
        //  default : library ApiService over the in-process fake (never the network)
        //  --real  : library ApiService → ReadOnlyGate (GET + one login only) → Bearer → loopback TLS, dedicated test account
        Server = new FakeGopServer(Path.Combine(AppContext.BaseDirectory, "fixtures", "v7_examples_e.json"));
        foreach (var d in Inv.All) Server.Put(d);
        Tokens = new TokenStorageService();
        ApiSetupModel apiSetup;
        if (!Real)
        {
            apiSetup = new ApiSetupModel { Url = FakeGopServer.BaseUrl, Timeout = 10 };
            Safety.AssertLoopback(apiSetup.Url);
            Api = new ApiService(Log, apiSetup, Server);
            Api.Initialize();
        }
        else
        {
            var url = Environment.GetEnvironmentVariable("LNP_API_URL") ?? "https://127.0.0.1:8000/api";
            Safety.AssertLoopback(url);
            var credFile = Environment.GetEnvironmentVariable("LNP_CRED_FILE")
                           ?? throw new InvalidOperationException("--real needs LNP_CRED_FILE (id=/pw= lines of the dedicated test account)");
            var (id, pw) = ReadOnlyGate.ReadCredential(credFile);
            apiSetup = new ApiSetupModel { Url = url, Timeout = 30 };
            var tls = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (req, cert, chain, errors) =>
                {
                    if (req.RequestUri is not null) Safety.AssertLoopback(req.RequestUri.ToString());
                    return true;   // self-signed loopback test server only
                }
            };
            Ironwall.Dotnet.Libraries.Accounts.Api.Services.IAccountApiService? accountRef = null;
            var bearer = new Ironwall.Dotnet.Libraries.Accounts.Api.Handlers.BearerAuthHandler(Tokens, () => accountRef!, Log) { InnerHandler = tls };
            Api = new ApiService(Log, apiSetup, new ReadOnlyGate(bearer, Server.Requests));
            Api.Initialize();
            var account = new Ironwall.Dotnet.Libraries.Accounts.Api.Services.AccountApiService(Api, Log);
            accountRef = account;
            var login = await account.LoginAsync(id, pw);   // exactly one login; token reused for the whole run
            if (login?.Data is null || string.IsNullOrEmpty(login.Data.AccessToken))
                throw new InvalidOperationException("dedicated-account login failed (see pipeline.log)");
            Tokens.SetTokens(login.Data.AccessToken, login.Data.RefreshToken, login.Data.SessionId);
            Log.Probe("real mode: logged in once with the dedicated test account (credential values not logged)");
        }
        var probe = new FakeProbe();
        DeviceApi = new DeviceApiService(Log, Api, apiSetup, probe);
        EventApi = new EventApiService(Log, Api, apiSetup, probe);
        ServerApi = new ServerApiService(Log, Api, apiSetup, probe);

        Devices = new DeviceProvider();
        Cameras = new CameraDeviceProvider(Log, Devices);
        var serverProvider = new ServerProvider(Log);
        DeviceSvc = new DeviceProviderService(Log, Ea, DeviceApi, Devices,
            new ControllerDeviceProvider(Log, Devices), new SensorDeviceProvider(Log, Devices), Cameras,
            new DeviceGroupProvider(Log), ServerApi, serverProvider, new DeviceQueryPolicy(probe, Log));
        await DeviceSvc.FetchAllDevicesAsync();
        Log.Probe($"devices loaded: {Devices.Count()} → " + string.Join(", ", Devices.Select(d => $"{d.Id}:{d.DeviceType}[{string.Join("/", d.DeviceGroups ?? new())}]")));

        if (!Real) Tokens.SetTokens(FakeToken);   // login gate open; no real session exists

        EventSetup = new EventSetupModel(new ProbeEventSetup
        {
            IsAutoEventDiscard = false, TimeDiscardSec = 3600,
            IsMalfunctionAutoEventDiscard = false, MalfunctionTimeDiscardSec = 3600,
        });
        Eqm = new EventQueueManager(Log, EventSetup);
        Sem = new SymbolEventManager(Ea, Log, EventSetup, Eqm);

        // ---- symbols: same registration shape as MapViewModel.InitializeDeviceSymbol (per device + per group) ----
        foreach (var dev in Devices.ToList())
        {
            var sym = new PidsSymbolModel { Title = $"SYM-{dev.Id}" };
            DeviceSymbols[dev.Id] = sym;
            Sem.RegisterDeviceSymbol(dev, sym);
            foreach (var g in dev.DeviceGroups ?? new())
            {
                if (!GroupSymbols.TryGetValue(g, out var gs))
                {
                    gs = new PidsGroupSymbolModel { Title = $"ZONE-{g}" };
                    GroupSymbols[g] = gs;
                }
                Sem.RegisterGroupSymbol(g, dev, gs);
            }
        }

        // ---- card list (real VM, activated like the shell does) ----
        var eps = new EventProviderService(Log, EventApi, Devices);
        Cards = new EventCardListPanelViewModel(Ea, Log, eps, new AccountModel(), EventApi, Sem, Eqm, new ActionReportGuard());
        await ((IActivate)Cards).ActivateAsync();

        // ---- EQM <-> SEM / card wiring: mirror of EventUiModule build callback ----
        Eqm.OnDeviceStateChanged += Sem.HandleDeviceStateChanged;
        Eqm.OnAutoRecovery += id =>
        {
            lock (AutoRecoveryFired) AutoRecoveryFired.Add(id);
            _ = Cards.HandleAutoRecoveryAsync(id).ContinueWith(t => { if (t.IsFaulted) Log.Error($"[AutoRecovery] {t.Exception?.GetBaseException()}"); });
        };
        Eqm.OnAutoReport += entry =>
        {
            lock (AutoReportFired) AutoReportFired.Add(entry.EntryId);
            _ = Cards.HandleAutoReportAsync(entry).ContinueWith(t => entry.AutoReportInFlight = false);
        };
        Eqm.OnGroupStateChanged += Sem.HandleGroupStateChanged;
        Eqm.StartSharedTimer();

        // ---- NATS: the library NatsService via its own Autofac module, group unit999 only ----
        NatsSetup = new NatsSetupModel
        {
            IpAddressNats = new Uri(natsUrl).Host, PortNats = new Uri(natsUrl).Port,
            DomainNats = Safety.Domain, GroupNats = Safety.Group, SubsystemNats = Safety.Subsystem,
            ConnectionTimeoutNats = 5000,
        };
        var b = new ContainerBuilder();
        b.RegisterModule(new NatsModule(NatsSetup, Log));
        NatsContainer = b.Build();
        Nats = new GuardedNatsService(NatsContainer.Resolve<INatsService>());
        if (Nats.Subject != $"{Safety.Domain}.{Safety.Group}.{Safety.Subsystem}.>")
            throw new InvalidOperationException($"SAFETY ABORT: pipeline subject {Nats.Subject}");

        // sync services first (EventUiModule build callback), host broker after (IService Order) - same as the app
        Det = new DetectionNatsSyncService(Log, Nats, Sem, Eqm, EventSetup, Ea, Tokens, Devices, new DefaultDoorContactPolicy());
        Mal = new MalfunctionNatsSyncService(Log, Nats, Sem, Eqm, EventSetup, Ea, Tokens, Devices);
        Op = new OperationEventNatsSyncService(Log, Nats, Sem, Tokens);
        DetSync = new DetectionSyncNatsService(Log, Nats, Eqm, EventApi, Ea, Tokens);
        await Det.StartService(); await Mal.StartService(); await Op.StartService(); await DetSync.StartService();

        Host = new NatsDomainService(Log, Ea, Nats, RecordingProxy<IRedisService>.Create(), EventApi,
            RecordingProxy<ISoundService>.Create(), Devices, Cameras, new GatewayEventProvider(Log), Cards, Sem,
            new SetupModel(), DeviceSvc, ServerApi, serverProvider, Eqm, Tokens);
        await Host.ExecuteAsync();

        RestoreTokens();   // caches the real session tokens (real mode) so a scenario that closes the gate can reopen it
        await Nats.ExecuteAsync();   // RegisterSubscribers: sensorway.unit999.gis.> + sensorway.unit999.all.> + sensorway.global.>
    }

    /// <summary>Drop the pipeline's NATS client and build a new one (the shared broker itself is never touched).</summary>
    public async Task ReconnectAsync()
    {
        await Nats.StopAsync();
        if (Nats.Connect(NatsSetup) == null) throw new InvalidOperationException("reconnect: Connect returned null");
        await Nats.ExecuteAsync();
    }

    public IBaseDeviceModel Dev(DevSpec d) => Devices.First(x => x.Id == d.Id);
    public PidsSymbolModel Sym(DevSpec d) => DeviceSymbols[d.Id];
}

/// <summary>Auto action-report is OFF (it would POST to the server); timeouts long so nothing expires mid-run.</summary>
public sealed class ProbeEventSetup : IEventSetupModel
{
    public bool IsAutoEventDiscard { get; set; }
    public bool IsSound { get; set; }
    public int TimeDurationSound { get; set; } = 1;
    public int TimeDiscardSec { get; set; } = 3600;
    public int LengthMaxEventPrev { get; set; } = 100;
    public int LengthMinEventPrev { get; set; } = 1;
    public bool IsMalfunctionAutoEventDiscard { get; set; }
    public int MalfunctionTimeDiscardSec { get; set; } = 3600;
}
