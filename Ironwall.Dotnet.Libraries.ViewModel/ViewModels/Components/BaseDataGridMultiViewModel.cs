using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Windows.Controls;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/10/2025 5:18:39 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseDataGridMultiViewModel<T> : BasePanelViewModel
{
    #region - Ctors -
    public BaseDataGridMultiViewModel(IEventAggregator eventAggregator, ILogService log) : base(eventAggregator, log)
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
    public abstract void OnSelectionChanged(IList<T> selectedItems);
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    
    public IList<T> SelectedItems
    {
        get => _selectedItems;
        set
        {
            if (ReferenceEquals(_selectedItems, value)) return;

            _selectedItems = value;
            OnSelectionChanged(_selectedItems);
            CheckSelectedItems?.Invoke(_selectedItems);
            NotifyOfPropertyChange(() => SelectedItems);
            NotifyOfPropertyChange(() => SelectedItemCount);
        }
    }

    public int SelectedItemCount => _selectedItems.Count;
   
    public delegate void SelectedItemsChanged(IList<T> selectedItems);
    public event SelectedItemsChanged? CheckSelectedItems;
    #endregion
    #region - Attributes -
    protected CancellationTokenSource? _pCancellationTokenSource;
    private IList<T> _selectedItems = new List<T>();
    #endregion
}