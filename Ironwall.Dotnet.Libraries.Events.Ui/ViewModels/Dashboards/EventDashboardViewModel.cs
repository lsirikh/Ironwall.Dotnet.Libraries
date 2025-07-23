using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Threading;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:44:27 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventDashboardViewModel : BasePanelViewModel
{
    #region - Ctors -
    public EventDashboardViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , EventTabControlViewModel tabControlViewModel
                                , DetectionEventPanelViewModel detectionEventPanelViewModel
                                , MalfunctionEventPanelViewModel malfunctionEventPanelViewModel
                                , ConnectionEventPanelViewModel connectionEventPanelViewModel
                                , ActionEventPanelViewModel actionEventPanelViewModel
                                , EventInfoViewModel eventInfoViewModel
                                , DataChartPanelViewModel dataChartPanelViewModel
                                ) : base(eventAggregator, log)
    {
        TabControlViewModel = tabControlViewModel;
        DetectionPanelViewModel = detectionEventPanelViewModel;
        MalfunctionPanelViewModel = malfunctionEventPanelViewModel;
        ConnectionPanelViewModel = connectionEventPanelViewModel;
        ActionPanelViewModel = actionEventPanelViewModel;
        EventInfoViewModel = eventInfoViewModel;
        DataChartPanelViewModel = dataChartPanelViewModel;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        IsButtonEnable = false;
        IsSelected = false;

        //EndDate = DateTime.Parse("2025-06-26 06:00:00");
        EndDate = DateTime.Now;
        StartDate = EndDate.AddDays(-1);
        

        DetectionPanelViewModel.UpdateAction += DetectionPanelViewModel_UpdateAction;
        DetectionPanelViewModel.CheckSelectedItems += DetectionPanelViewModel_CheckSelectedItems;
        MalfunctionPanelViewModel.UpdateAction += MalfunctionPanelViewModel_UpdateAction;
        MalfunctionPanelViewModel.CheckSelectedItems += MalfunctionPanelViewModel_CheckSelectedItems;
        ConnectionPanelViewModel.UpdateAction += ConnectionPanelViewModel_UpdateAction;
        ConnectionPanelViewModel.CheckSelectedItems += ConnectionPanelViewModel_CheckSelectedItems;
        ActionPanelViewModel.UpdateAction += ActionPanelViewModel_UpdateAction;
        ActionPanelViewModel.CheckSelectedItems += ActionPanelViewModel_CheckSelectedItems;
        DataChartPanelViewModel.UpdateAction += DataChartPanelViewModel_UpdateAction;

        DataChartPanelViewModel.SetDate(StartDate, EndDate);
        DetectionPanelViewModel.SetDate(StartDate, EndDate);
        MalfunctionPanelViewModel.SetDate(StartDate, EndDate);
        ConnectionPanelViewModel.SetDate(StartDate, EndDate);
        ActionPanelViewModel.SetDate(StartDate, EndDate);

        if (DataChartPanelViewModel.IsActive)
            await DataChartPanelViewModel.DeactivateAsync(true);
        await TabControlViewModel.ActivateItemAsync(DataChartPanelViewModel);
        await TabControlViewModel.ActivateAsync();
        await DataInitialize(cancellationToken);

        //EventInfoViewModel.SetData(startDate: StartDate, endDate: EndDate, new[] {"DET", "MAL", "CON", "ACT"});
        //await EventInfoViewModel.ActivateAsync();
    }

    

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(close, cancellationToken);
        DetectionPanelViewModel.UpdateAction -= DetectionPanelViewModel_UpdateAction;
        DetectionPanelViewModel.CheckSelectedItems -= DetectionPanelViewModel_CheckSelectedItems;
        MalfunctionPanelViewModel.UpdateAction -= MalfunctionPanelViewModel_UpdateAction;
        ConnectionPanelViewModel.UpdateAction -= ConnectionPanelViewModel_UpdateAction;
        ActionPanelViewModel.UpdateAction -= ActionPanelViewModel_UpdateAction;
        DataChartPanelViewModel.UpdateAction -= DataChartPanelViewModel_UpdateAction;


        ClearData();
        SelectedItemEditor = null;
        await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);
        TabControlViewModel.Items.Clear();
        await TabControlViewModel.DeactivateAsync(true);
        await EventInfoViewModel.DeactivateAsync(true);
    }

    private async void DataChartPanelViewModel_UpdateAction(DateTime start, DateTime end)
    {
        if (EventInfoViewModel.IsActive)
            await EventInfoViewModel.DeactivateAsync(true);

        await EventInfoViewModel.ActivateAsync();


        EventInfoViewModel.SetData(startDate: start, endDate: end, new[] { "DET", "MAL", "CON", "ACT" });
        await EventInfoViewModel.DataInitialize();
        IsButtonEnable = true;
    }


    private async void ActionPanelViewModel_UpdateAction(DateTime start, DateTime end)
    {
        if (EventInfoViewModel.IsActive)
            await EventInfoViewModel.DeactivateAsync(true);

        await EventInfoViewModel.ActivateAsync();

        EventInfoViewModel.SetData(startDate: start, endDate: end, new[] { "ACT" });
        await EventInfoViewModel.DataInitialize();
        IsButtonEnable = true;
    }

    private void ActionPanelViewModel_CheckSelectedItems(IList<ActionEventViewModel> selectedItems)
    {
        if (!(selectedItems.Count > 0))
        {
            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
            SelectedItemEditor = new ActionSelectionViewModel(selectedItems);
            (SelectedItemEditor as ActionSelectionViewModel)!.RefreshAll();
            IsSelected = true;
        }
    }

    private async void ConnectionPanelViewModel_UpdateAction(DateTime start, DateTime end)
    {
        if (EventInfoViewModel.IsActive)
            await EventInfoViewModel.DeactivateAsync(true);

        await EventInfoViewModel.ActivateAsync();
        EventInfoViewModel.SetData(startDate: start, endDate: end, new[] { "CON" });
        await EventInfoViewModel.DataInitialize();
        IsButtonEnable = true;
    }

    private void ConnectionPanelViewModel_CheckSelectedItems(IList<ConnectionEventViewModel> selectedItems)
    {
        if (!(selectedItems.Count > 0))
        {
            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
            SelectedItemEditor = new ConnectionSelectionViewModel(selectedItems);
            (SelectedItemEditor as ConnectionSelectionViewModel)!.RefreshAll();
            IsSelected = true;
        }
    }

    private async void MalfunctionPanelViewModel_UpdateAction(DateTime start, DateTime end)
    {
        if (EventInfoViewModel.IsActive)
            await EventInfoViewModel.DeactivateAsync(true);

        await EventInfoViewModel.ActivateAsync();

        EventInfoViewModel.SetData(startDate: start, endDate: end, new[] { "MAL" });
        await EventInfoViewModel.DataInitialize();
        IsButtonEnable = true;
    }

    private void MalfunctionPanelViewModel_CheckSelectedItems(IList<MalfunctionEventViewModel> selectedItems)
    {
        if (!(selectedItems.Count > 0))
        {
            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
            SelectedItemEditor = new MalfunctionSelectionViewModel(selectedItems);
            (SelectedItemEditor as MalfunctionSelectionViewModel)!.RefreshAll();
            IsSelected = true;
        }
    }


    private async void DetectionPanelViewModel_UpdateAction(DateTime start, DateTime end)
    {
        if (EventInfoViewModel.IsActive)
            await EventInfoViewModel.DeactivateAsync(true);

        await EventInfoViewModel.ActivateAsync();

        EventInfoViewModel.SetData(startDate: start, endDate: end, new[] { "DET"});
        await EventInfoViewModel.DataInitialize();
        IsButtonEnable = true;
    }

    private void DetectionPanelViewModel_CheckSelectedItems(IList<DetectionEventViewModel> selectedItems)
    {
        if (!(selectedItems.Count > 0))
        {
            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
            SelectedItemEditor = new DetectionSelectionViewModel(selectedItems);
            (SelectedItemEditor as DetectionSelectionViewModel)!.RefreshAll();
            IsSelected = true;
        }
    }
    #endregion
    #region - Binding Methods -
    private void ClearData()
    {

        
    }

    #endregion
    #region - Processes -
    /// <summary>
    /// 여러가지 Setup에 관련된 ViewModel을 순환적으로 활성화 시키는 역할을 하고, 
    /// 각 ViewModel과 View 바인딩의 라이프사이클을 올바르게 유지하고 지속시키는 역할을 해준다.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    public async void OnActiveTab(object sender, SelectionChangedEventArgs args)
    {
        try
        {
            if (!(args.Source is TabControl)) return;

            if (args.AddedItems.Count == 0 || args.AddedItems[0] is not TabItem tab) return;

            IsButtonEnable = false;

            var token = _cancellationTokenSource!.Token;

            _log?.Info($"Tag : {tab.Tag}");

            if(TabControlViewModel.IsActive)
                await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true, token);

            ///PREPARING_TIME_MS는 기존의 ViewModel과 View를 바인딩하고, 유연하게 View를 불러오는 시간적 여유를 
            ///제공하므로써, UI/UX의 경험을 보다 우수하게 만들 수 있다. OnActiveTab에서 제어하지 않고, 
            ///각 ViewModel에서 제어를 하게 되면 여러가지 타이밍적 에러에 의해서 버그가 발생하고 해결하기 어려운 과제로 만들게 된다.

            switch (tab.Tag)
            {
                case "DataChartPanelViewModel":
                    await TabControlViewModel.ActivateItemAsync(DataChartPanelViewModel, token);
                    break;

                case "DetectionEventViewModel":
                    await TabControlViewModel.ActivateItemAsync(DetectionPanelViewModel, token);
                    break;

                case "MalfunctionEventViewModel":
                    await TabControlViewModel.ActivateItemAsync(MalfunctionPanelViewModel, token);
                    break;

                case "ConnectionEventViewModel":
                    await TabControlViewModel.ActivateItemAsync(ConnectionPanelViewModel, token);
                    break;

                case "ActionEventViewModel":
                    await TabControlViewModel.ActivateItemAsync(ActionPanelViewModel, token);
                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"{ex.Message}");
        }
    }

    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {

            EventProvider = IoC.Get<EventProvider>();
            NotifyOfPropertyChange(() => EventProvider);
        });
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -

    public bool IsButtonEnable
    {
        get { return _isButtonEnable; }
        set { _isButtonEnable = value; NotifyOfPropertyChange(() => IsButtonEnable); }
    }

    public bool IsSelected
    {
        get { return _isSelected; }
        set { _isSelected = value; NotifyOfPropertyChange(() => IsSelected); }
    }

    public BasePanelViewModel? SelectedItemEditor
    {
        get { return _selectedItemEditor; }
        set { _selectedItemEditor = value; NotifyOfPropertyChange(() => SelectedItemEditor); }
    }

    public EventTabControlViewModel TabControlViewModel { get; private set; }
    public DetectionEventPanelViewModel DetectionPanelViewModel { get; private set; }
    public MalfunctionEventPanelViewModel MalfunctionPanelViewModel { get; private set; }
    public ConnectionEventPanelViewModel ConnectionPanelViewModel { get; private set; }
    public ActionEventPanelViewModel ActionPanelViewModel { get; private set; }
    public EventInfoViewModel EventInfoViewModel { get; }
    public DataChartPanelViewModel DataChartPanelViewModel { get; }
    public EventProvider EventProvider { get; private set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    #endregion
    #region - Attributes -
    private bool _isButtonEnable;
    private const int TIMEOUT = 30000;
    private bool _isSelected;
    private BasePanelViewModel? _selectedItemEditor;
    #endregion
}