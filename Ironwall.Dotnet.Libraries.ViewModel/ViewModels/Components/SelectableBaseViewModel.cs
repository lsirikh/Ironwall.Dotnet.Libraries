using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/11/2025 9:34:15 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SelectableBaseViewModel : BasePanelViewModel, ISelectableBaseViewModel
{
    #region - Ctors -
    public SelectableBaseViewModel(IEventAggregator eventAggregator
                                    , ILogService log) : base(eventAggregator, log)
    {

    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            _isSelected = value;
            NotifyOfPropertyChange(() => IsSelected);
        }
    }
    #endregion
    #region - Attributes -
    private bool _isSelected;
    #endregion
}