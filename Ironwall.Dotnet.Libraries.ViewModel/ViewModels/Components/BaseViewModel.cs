using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 6:21:15 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseViewModel<T> : Conductor<IScreen>
                                , IHandle<CloseAllMessageModel>
                                , IBaseViewModel<T> where T : IBaseModel
{
    #region - Ctors -
    public BaseViewModel(T model)
    {
        _log = IoC.Get<ILogService>();
        _eventAggregator = IoC.Get<IEventAggregator>();
        _className = this.GetType().Name.ToString();
        _model = model;
    }

    public BaseViewModel(IEventAggregator eventAggregator, ILogService log, T model)
    {
        _model = model;
        _className = this.GetType().Name.ToString();
        _eventAggregator = eventAggregator;
        _log = log;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -

    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            base.OnActivateAsync(cancellationToken);
            _log?.Info($"######### {_className} OnActivate!! #########");
            _eventAggregator?.SubscribeOnUIThread(this);
            _cancellationTokenSource = new CancellationTokenSource();
        }
        catch (Exception)
        {
        }

        return Task.CompletedTask;
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        try
        {
            base.OnDeactivateAsync(close, cancellationToken);
            _log?.Info($"######### {_className} OnDeactivate!! #########");
            _eventAggregator?.Unsubscribe(this);
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            GC.Collect();
        }
        catch (Exception)
        {
        }

        return Task.CompletedTask;
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    public Task HandleAsync(CloseAllMessageModel message, CancellationToken cancellationToken)
    {
        return TryCloseAsync();
    }
    #endregion
    #region - Properties -

    public T Model
    {
        get => _model;
        set { _model = value; NotifyOfPropertyChange(() => Model); }
    }

    #endregion
    #region - Attributes -
    protected string? _className = string.Empty;
    protected T _model;
    protected IEventAggregator? _eventAggregator;
    protected ILogService? _log;
    protected CancellationTokenSource? _cancellationTokenSource;
    public const int ACTION_TOKEN_TIMEOUT = 5000;
    #endregion

}