using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Data;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows;
using Ironwall.Dotnet.Libraries.ViewModel.Models;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 7:00:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseDataGridViewModel<T> : BasePanelViewModel
{
    #region - Ctors -
    public BaseDataGridViewModel(IEventAggregator eventAggregator, ILogService log) : base(eventAggregator, log)
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
    protected abstract Task CheckSelectState(CancellationToken cancellationToken = default);
    protected abstract Task SelectAll(bool isSelected);
    public abstract void OnClickCheckBoxItem(object sender, RoutedEventArgs e);
    public async void OnClickCheckBoxColumnHeader(object sender, RoutedEventArgs e)
    {
        bool value = false;
        try
        {
            var checkbox = e.Source as CheckBox;
            if (checkbox != null) 
            { 
                value = checkbox.IsChecked ?? false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
        finally
        {
            await SelectAll(value);
        }
    }

    public async Task ClearSelection()
    {
        IsAllChecked = false;
        await SelectAll(false);
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool IsAllChecked
    {
        get { return _isAllChecked; }
        set
        {
            _isAllChecked = value;
            NotifyOfPropertyChange(() => IsAllChecked);
        }
    }

    public T? SelectedItem
    {
        get { return _selectedItem; }
        set
        {
            _selectedItem = value;
            NotifyOfPropertyChange(() => SelectedItem);

            if (SelectedItem != null)
                PropertyUpdate?.Invoke(this, new ValueNotifyEventArgs<T?>() { Value = SelectedItem });
        }
    }

    public bool IsCheckedCheckBoxColumnHeader
    {
        get => isCheckedCheckBoxColumnHeader;
        set
        {
            isCheckedCheckBoxColumnHeader = value;
            NotifyOfPropertyChange(() => IsCheckedCheckBoxColumnHeader);
        }
    }

    public int SelectedItemCount
    {
        get { return _selectedItemCount; }
        set
        {
            _selectedItemCount = value;
            NotifyOfPropertyChange(() => SelectedItemCount);
        }
    }
    #endregion
    #region - Attributes -
    private bool _isAllChecked;
    private int _selectedItemCount = 0;
    private bool isCheckedCheckBoxColumnHeader;
    private T? _selectedItem;
    protected event EventHandler? PropertyUpdate;
    protected CancellationTokenSource? _pCancellationTokenSource;
    #endregion
}