using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services.Startup;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Splash;

public class SplashViewModel : Screen, ISplashViewModelBase, IHandle<SplashScreenMessage>
{
    #region - Ctors -

    public SplashViewModel(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;

        // Progress<T>는 UI 스레드에서 생성해야 SynchronizationContext를 캡처한다.
        // IoC.Get<ISplashViewModelBase>()는 Bootstrapper.OnStartup() (UI 스레드)에서 호출되므로 안전하다.
        Progress = new Progress<StartupProgress>(report =>
        {
            ProgressValue = report.TotalFraction * 100;
            Message = string.IsNullOrEmpty(report.Stage)
                ? report.JobName
                : $"{report.JobName} — {report.Stage}";
        });

        var assembly = Assembly.GetEntryAssembly();
        ProductName = assembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                      ?? assembly?.GetName().Name
                      ?? "Application";
        var ver = assembly?.GetName().Version;
        VersionText = ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : string.Empty;
    }

    #endregion

    #region - ISplashViewModelBase -

    public IProgress<StartupProgress> Progress { get; }

    public void AllowClose()
    {
        if (_isCloseable) return;
        _isCloseable = true;
        _ = TryCloseAsync();
    }

    public Task WhenActivated => _activatedTcs.Task;

    #endregion

    #region - Overrides -

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _eventAggregator.SubscribeOnUIThread(this);
        await base.OnActivateAsync(cancellationToken);
        _activatedTcs.TrySetResult();
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _eventAggregator.Unsubscribe(this);
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_isCloseable);

    #endregion

    #region - IHandle<SplashScreenMessage> -

    public Task HandleAsync(SplashScreenMessage message, CancellationToken cancellationToken)
    {
        Message = $"[{message.Title}] {message.Message}";
        return Task.CompletedTask;
    }

    #endregion

    #region - Properties -

    public string Message
    {
        get => _message;
        private set { _message = value; NotifyOfPropertyChange(); }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set { _progressValue = value; NotifyOfPropertyChange(); }
    }

    public string ProductName { get; }
    public string VersionText { get; }

    #endregion

    #region - Attributes -

    private readonly IEventAggregator _eventAggregator;
    private readonly TaskCompletionSource _activatedTcs
        = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _isCloseable;
    private string _message = "시스템을 초기화하는 중입니다...";
    private double _progressValue;

    #endregion
}
