using Autofac.Features.Metadata;
using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Base;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 1/24/2025 11:21:26 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public abstract class ParentBootstrapper<T> : BootstrapperBase where T : notnull
{
    #region - Ctors -
    public ParentBootstrapper()
    {
        CancellationTokenSourceHandler = new CancellationTokenSource();
        _log = new LogService();
        _class = this.GetType();
        string? projectName = System.Reflection.Assembly.GetEntryAssembly()?.GetName()?.Name;
        _log.Info($"############### Program{projectName} was started. ###############");

        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            if (exception != null)
            {
                _log.Error($"Unhandled exception: {exception.Message}");
                _log.Error($"Stack Trace: {exception.StackTrace}");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            _log.Error($"Unobserved task exception: {args.Exception.Message}");
            _log.Error($"Stack Trace: {args.Exception.StackTrace}");
            args.SetObserved();
        };
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected abstract void ConfigureContainer(ContainerBuilder builder);

    /// <summary>
    /// StartPrograme은 메인 Shell을 표시하는 책임을 갖는다.
    /// </summary>
    protected abstract void StartPrograme();

    public async Task Start(IProgress<StartupProgress>? progress = null)
    {
        if (Container == null) return;
        var token = CancellationTokenSourceHandler.Token;
        try
        {
            var services = Container.Resolve<IEnumerable<Meta<IService>>>()
                                    .OrderBy(s => s.Metadata["Order"])
                                    .Select(s => s.Value).ToList();
            var providers = Container.Resolve<IEnumerable<Meta<ILoadable>>>()
                                     .OrderBy(s => s.Metadata["Order"])
                                     .Select(s => s.Value).ToList();
            int total = services.Count + providers.Count;
            int done = 0;

            foreach (var service in services)
            {
                var name = service.GetType().Name;
                _log.Info($"@@@@Starting Service Instance({name})@@@@");
                progress?.Report(new StartupProgress(name, "시작 중...", (double)done / total));
                await Task.Run(async () =>
                {
                    await service.ExecuteAsync(token).ConfigureAwait(false);
                });
                done++;
                progress?.Report(new StartupProgress(name, "완료", (double)done / total));
            }

            await Task.Delay(3000);

            foreach (var provider in providers)
            {
                var name = provider.GetType().Name;
                _log.Info($"####Starting Provider Instance({name})####");
                progress?.Report(new StartupProgress(name, "초기화 중...", (double)done / total));
                await DispatcherService.BeginInvoke(async () =>
                {
                    await provider.Initialize(token).ConfigureAwait(false);
                });
                done++;
                progress?.Report(new StartupProgress(name, "완료", (double)done / total));
            }

            progress?.Report(new StartupProgress("앱", "시작 완료", 1.0));
            StartPrograme();
        }
        catch (Exception ex)
        {
            _log.Error($"Raised {nameof(Exception)} in {nameof(Start)} : {ex}");
            throw;
        }
    }

    public async Task BaseStart()
    {
        if (Container == null) return;

        var token = CancellationTokenSourceHandler.Token;
        foreach (var service in Container.Resolve<IEnumerable<IService>>())
        {
            await service.ExecuteAsync(token).ConfigureAwait(false);
        }
    }

    public virtual void Stop()
    {
        CancellationTokenSourceHandler?.Cancel();
        CancellationTokenSourceHandler?.Dispose();
        Task.Run(async () => await DisposeAsync());
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public async ValueTask DisposeAsync()
    {
        if (Container == null) return;
        await Container.DisposeAsync().ConfigureAwait(false);
    }

    protected override async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            await Start();
        }
        catch (Exception ex)
        {
            _log.Error($"[Startup] 서비스 초기화 실패: {ex.Message}");
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(1));
        }
    }

    protected override void OnExit(object sender, EventArgs e)
    {
        try
        {
            if (Container == null) return;

            var services = Container.Resolve<IEnumerable<Meta<IService>>>()
                                    .OrderBy(s => s.Metadata["Order"])
                                    .Select(s => s.Value)
                                    .ToList();

            foreach (var service in services)
                _log.Info($"@@@@Uninitializing Service Instance({service.GetType()})@@@@");

            // Task.Run으로 SynchronizationContext 제거 후 WhenAll 동기 대기
            // → WPF 디스패처 교착 없이 NATS/Redis 정리 완료 보장
            // 10초 안전망: 서비스 StopAsync가 무한 대기 시 강제 종료
            using var shutdownCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                Task.Run(async () =>
                    await Task.WhenAll(services.Select(s => s.StopAsync(shutdownCts.Token)))
                              .WaitAsync(shutdownCts.Token))
                    .GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _log.Warning("서비스 종료 타임아웃(10s) — 강제 종료 진행");
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                _log.Warning("서비스 종료 타임아웃(10s) — 강제 종료 진행");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Raised {nameof(Exception)} in {nameof(OnExit)} of {nameof(ParentBootstrapper<T>)} : {ex}");
        }

        Stop();
        base.OnExit(sender, e);
    }

    protected override void Configure()
    {
        ContainerBuilder builder = new();

        RegisterBaseType(builder);
        builder!.RegisterType<T>().SingleInstance();

        ConfigureContainer(builder);

        _container = builder.Build();
    }

    private void RegisterBaseType(ContainerBuilder builder)
    {
        builder.RegisterType<WindowManager>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterType<EventAggregator>().AsImplementedInterfaces().SingleInstance();
        builder.RegisterInstance(_log).As<ILogService>().SingleInstance();
    }

    protected override object? GetInstance(Type service, string key)
    {
        if (Container == null) return null;

        if (string.IsNullOrWhiteSpace(key))
        {
            if (Container.IsRegistered(service))
                return Container.Resolve(service);
        }
        else
        {
            if (Container.IsRegisteredWithKey(key, service))
                return Container.ResolveKeyed(key, service);
        }
        throw new Exception(string.Format("Could not locate any instances of contract {0}.", key ?? service.Name));
    }

    protected override IEnumerable<object>? GetAllInstances(Type service)
    {
        return Container?.Resolve(typeof(IEnumerable<>).MakeGenericType(service)) as IEnumerable<object>;
    }

    protected override void BuildUp(object instance)
    {
        Container?.InjectProperties(instance);
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    protected IContainer? Container
    {
        get { return _container; }
    }
    public CancellationTokenSource CancellationTokenSourceHandler { get; }
    #endregion
    #region - Attributes -
    private IContainer? _container;
    protected LogService _log;
    private Type _class;
    #endregion
}
