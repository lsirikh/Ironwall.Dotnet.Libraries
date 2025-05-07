using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Conductors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/11/2025 11:31:55 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ConductorAllViewModel : Conductor<Screen>.Collection.AllActive
                                    , IHandle<CloseAllMessageModel>, IConductorViewModel
{

    #region - Ctors -
    public ConductorAllViewModel(IEventAggregator eventAggregator
                                , ILogService log)
    {
        _eventAggregator = eventAggregator;
        _log = log;
        _className = this.GetType().Name.ToString();

    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        base.OnActivateAsync(cancellationToken);
        _eventAggregator?.SubscribeOnUIThread(this);
        _log?.Info($"######### {_className} OnActivate!! #########");
        IsVisible = false;

        return Task.CompletedTask;
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        base.OnDeactivateAsync(close, cancellationToken);
        _eventAggregator?.Unsubscribe(this);
        _log?.Info($"######### {_className} OnDeactivate!! #########");
        IsVisible = false;

        return Task.CompletedTask;
    }

    public override async Task ActivateItemAsync(Screen item, CancellationToken cancellationToken = default)
    {
        /// BaseViewModel을 상속받는  
        /// ViewModel만 ActivateItem이 가능
        if (!(item is IConductorViewModel))
            return;

        await base.ActivateItemAsync(item, cancellationToken);

        /// 해당 ShellViewModel을 Visible 하게 
        /// 관리하기 위해서 Dialog와 Popup Dialog의
        /// ShellViewModel의 ActiveItem이 Blank Item 인지
        /// 확인하는 과정이 필요하다.
        var viewModel = item as IConductorViewModel;

        viewModel.IsVisible = false;

    }

    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CloseAllMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            if (Items != null && Items.Count > 0)
            {
                await ActivateItemAsync(item: Items.FirstOrDefault() ?? throw new NullReferenceException("참조할 ViewModel이 등록되지 않았습니다."), cancellationToken);
                IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
    }
    #endregion
    #region - Properties -
    public bool IsVisible
    {
        get { return _isVisible; }
        set
        {
            _isVisible = value;
            NotifyOfPropertyChange(() => IsVisible);
        }
    }
    #endregion
    #region - Attributes -
    protected IEventAggregator? _eventAggregator;
    protected ILogService? _log;
    private string _className = string.Empty;
    private bool _isVisible;
    #endregion
}