using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Conductors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/11/2025 11:44:32 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ConductorOneViewModel : Conductor<Screen>.Collection.OneActive
                                    , IConductorViewModel
                                    , IHandle<CloseAllMessageModel>
{

    #region - Ctors -
    public ConductorOneViewModel(IEventAggregator eventAggregator
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
        IsVisible = true;

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


    /// <summary>
    /// 기존의 Item이 삭제되고 새로운 아이템이 있으면 IsVisible을 이양하고, 
    /// 없는 경우 IsVisible을 false로 하여 View Panel의 계층은 유지하고, 제어는 가능하게 만든다.
    /// </summary>
    /// <param name="newItem"></param>
    /// <param name="closePrevious"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected override Task ChangeActiveItemAsync(Screen newItem, bool closePrevious, CancellationToken cancellationToken)
    {
        if (newItem == null)
        {
            IsVisible = false;
        }
        else
        {
            IsVisible = true;
        }

        return base.ChangeActiveItemAsync(newItem, closePrevious, cancellationToken);
    }

    //public override Task ActivateItemAsync(Screen item, CancellationToken cancellationToken = default)
    //{
    //    /// BaseViewModel을 상속받는  
    //    /// ViewModel만 ActivateItem이 가능
    //    if (!(item is IBasePanelViewModel)) return Task.CompletedTask;
        
    //    base.ActivateItemAsync(item, cancellationToken);

    //    /// 해당 ShellViewModel을 Visible 하게 
    //    /// 관리하기 위해서 Dialog와 Popup Dialog의
    //    /// ShellViewModel의 ActiveItem이 Blank Item 인지
    //    /// 확인하는 과정이 필요하다.
    //    var viewModel = item as IBasePanelViewModel;

    //    IsVisible = true;

    //    return Task.CompletedTask;
    //}
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
            //if (Items != null && Items.Count > 0)
            //{
            //    await ActivateItemAsync(item: Items.FirstOrDefault() ?? throw new NullReferenceException("참조할 ViewModel이 등록되지 않았습니다."), cancellationToken);
            //    IsVisible = false;
            //}
            if (IsActive)
            {
                //이전에 담긴 Item은 무시한다.
                Items.Clear();
                //현재 Conductor에 담긴 Item은 Deactivate 시킨다.
                await DeactivateItemAsync(ActiveItem, true, cancellationToken);
                //결론적으로 Conductor를 Deactivate 시킨다.
                await TryCloseAsync();
            }
            else
                await Task.CompletedTask;

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