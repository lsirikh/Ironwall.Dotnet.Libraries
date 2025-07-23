using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:13:45 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseDataGridMultiPanelViewModel<T> : BaseDataGridMultiViewModel<T>
                                                                where T : ISelectableBaseViewModel
{
    #region - Ctors -
    public BaseDataGridMultiPanelViewModel(IEventAggregator eventAggregator, ILogService log) : base(eventAggregator, log)
    {
        ViewModelProvider = new ObservableCollection<T>();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        ButtonAllEnable();
        _pCancellationTokenSource = new CancellationTokenSource();
        return base.OnActivateAsync(cancellationToken);
    }
    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await Uninitialize();
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    public abstract void OnClickInsertButton(object sender, RoutedEventArgs e);
    public abstract void OnClickDeleteButton(object sender, RoutedEventArgs e);
    public abstract void OnClickSaveButton(object sender, RoutedEventArgs e);
    public abstract void OnClickReloadButton(object sender, RoutedEventArgs e);

    protected virtual Task Uninitialize()
    {
        try
        {
            DispatcherService.Invoke(() =>
            {
                ButtonAllDisable();
                ViewModelProvider.Clear();
                SelectedItems.Clear();
                IsVisible = false;
            });

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
                _pCancellationTokenSource?.Cancel();
            _pCancellationTokenSource?.Dispose();

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
    protected void ButtonEnableControl(bool isButton, bool saveButton, bool reloadButton)
    {
        IsButtonEnable = isButton;
        SaveButtonEnable = saveButton;
        ReloadButtonEnable = reloadButton;
        Refresh();
    }
    protected void ButtonAllEnable()
    {
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable Start!!");
        IsButtonEnable = true;
        SaveButtonEnable = true;
        ReloadButtonEnable = true;
        Refresh();
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable End!!");
    }
    protected void ButtonAllDisable()
    {
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable Start!!");
        IsButtonEnable = false;
        SaveButtonEnable = false;
        ReloadButtonEnable = false;
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable End!!");
    }
    #endregion
    #region - IHanldes -
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

    public bool IsButtonEnable
    {
        get { return _isButtonEnable; }
        set
        {
            _isButtonEnable = value;
            NotifyOfPropertyChange(() => IsButtonEnable);
        }
    }

    public bool ReloadButtonEnable
    {
        get { return _reloadButtonEnable; }
        set
        {
            _reloadButtonEnable = value;
            NotifyOfPropertyChange(() => ReloadButtonEnable);
        }
    }

    public bool SaveButtonEnable
    {
        get { return _saveButtonEnable; }
        set
        {
            _saveButtonEnable = value;
            NotifyOfPropertyChange(() => SaveButtonEnable);
        }
    }
    public ObservableCollection<T> ViewModelProvider { get; set; }

    #endregion
    #region - Attributes -
    private bool _isVisible;
    private bool _isButtonEnable;
    private bool _reloadButtonEnable;
    private bool _saveButtonEnable;
    protected readonly SemaphoreSlim _processGate = new(1, 1);
    #endregion
}