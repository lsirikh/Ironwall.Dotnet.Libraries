using Autofac.Features.Metadata;
using Autofac;
using Caliburn.Micro;
using Ironwall.Dotnet.Framework.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using log4net;
using System;
using Autofac.Core.Registration;
using System.Data;
using System.Windows;

namespace Ironwall.Dotnet.Framework;
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

        // 전역 예외 처리 추가
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
            args.SetObserved(); // 예외가 전파되지 않도록 설정
        };
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    /// <summary>
    /// Register classes, services ,or modules in this method
    /// </summary>
    /// <param name="builder"></param>
    protected abstract void ConfigureContainer(ContainerBuilder builder);

    /// <summary>
    /// StartProgram execute ShellViewModel to display after finished running services and providers.
    /// </summary>
    protected abstract void StartPrograme();
    public async Task Start()
    {
        var token = CancellationTokenSourceHandler.Token;
        if (Container == null) return;
        try
        {
            foreach (var service in Container.Resolve<IEnumerable<Meta<IService>>>()
                                    .OrderBy(s => s.Metadata["Order"])
                                    .Select(s => s.Value))
            {
                _log.Info($"@@@@Starting Service Instance({service.GetType()})@@@@");

                // 백그라운드 스레드에서 실행 강제
                await Task.Run(async () =>
                {
                    await service.ExecuteAsync(token).ConfigureAwait(false);
                });
            }

            await Task.Delay(3000);

            foreach (var service in Container.Resolve<IEnumerable<Meta<ILoadable>>>()
                                    .OrderBy(s => s.Metadata["Order"])
                                    .Select(s => s.Value))
            {
                _log.Info($"####Starting Provider Instance({service.GetType()})####");

                await Services.DispatcherService.BeginInvoke(async () =>
                {
                    await service.Initialize(token).ConfigureAwait(false);
                });
            }

            StartPrograme();
        }
        catch (Exception ex)
        {
            _log.Error($"Raised {nameof(Exception)} in {nameof(Start)} : {ex}");
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
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async ValueTask DisposeAsync()
    {
        if (Container == null) return;

        await Container.DisposeAsync().ConfigureAwait(false);

        // Registries are not likely to have async tasks to dispose of,
        // so we will leave it as a straight dispose.
        //ComponentRegistry.Dispose();

        // Do not call the base, otherwise the standard Dispose will fire.
    }

    /// <summary>
    /// Ons the startup.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The e.</param>
    protected override async void OnStartup(object sender, StartupEventArgs e)
    {
        await Start();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected override void OnExit(object sender, EventArgs e)
    {
        try
        {
            if (Container == null) return;

            foreach (var service in Container.Resolve<IEnumerable<Meta<IService>>>()
                                    .OrderBy(s => s.Metadata["Order"])
                                    .Select(s => s.Value))
            {
                _log.Info($"@@@@Uninitializing Service Instance({service.GetType()})@@@@");
                service.StopAsync();
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