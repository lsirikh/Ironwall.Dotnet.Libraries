using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/27/2025 7:34:02 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceDashboardViewModel : BasePanelViewModel
{
    #region - Ctors -
    public DeviceDashboardViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , DeviceTabControlViewModel tabControlViewModel
                                , ControllerDevicePanelViewModel controllerDevicePanelViewModel
                                , SensorDevicePanelViewModel sensorDevicePanelViewModel
                                , CameraDevicePanelViewModel cameraDevicePanelViewModel
                                ) : base(eventAggregator, log)
    {
        TabControlViewModel = tabControlViewModel;
        ControllerPanelViewModel = controllerDevicePanelViewModel;
        SensorPanelViewModel = sensorDevicePanelViewModel;
        CameraPanelViewModel = cameraDevicePanelViewModel;

    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        IsSelected = false;
        ControllerPanelViewModel.CheckSelectedItems += ControllerPanelViewModel_CheckSelectedItems;
        ControllerPanelViewModel.UpdateAction += ControllerPanelViewModel_UpdateAction;
        SensorPanelViewModel.CheckSelectedItems += SensorPanelViewModel_CheckSelectedItems;
        SensorPanelViewModel.UpdateAction += SensorPanelViewModel_UpdateAction;
        CameraPanelViewModel.CheckSelectedItems += CameraPanelViewModel_CheckSelectedItems;
        CameraPanelViewModel.UpdateAction += CameraPanelViewModel_UpdateAction;

        await TabControlViewModel.ActivateAsync();

        await TabControlViewModel.ActivateItemAsync(ControllerPanelViewModel);

        await DataInitialize(cancellationToken);
        await GetDeviceType(cancellationToken);


        


    }



    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(close, cancellationToken);
        ControllerPanelViewModel.CheckSelectedItems -= ControllerPanelViewModel_CheckSelectedItems;
        ControllerPanelViewModel.UpdateAction -= ControllerPanelViewModel_UpdateAction;
        SensorPanelViewModel.CheckSelectedItems -= SensorPanelViewModel_CheckSelectedItems;
        SensorPanelViewModel.UpdateAction -= SensorPanelViewModel_UpdateAction;
        CameraPanelViewModel.CheckSelectedItems -= CameraPanelViewModel_CheckSelectedItems;
        CameraPanelViewModel.UpdateAction -= CameraPanelViewModel_UpdateAction;

        ClearData();
        SelectedItemEditor = null;
        TabControlViewModel.Items.Clear();
        await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);
        await TabControlViewModel.DeactivateAsync(true);
    }
    #endregion
    #region - Binding Methods -
    private void ControllerPanelViewModel_CheckSelectedItems(IList<ControllerDeviceViewModel> selectedItems)
    {

        if (!(selectedItems.Count > 0))
        {
            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
           SelectedItemEditor =  new ControllerSelectionViewModel(selectedItems);
           (SelectedItemEditor as ControllerSelectionViewModel)!.RefreshAll();
           IsSelected = true;
        }    

    }

    private void SensorPanelViewModel_CheckSelectedItems(IList<SensorDeviceViewModel> selectedItems)
    {
        if (!(selectedItems.Count > 0))
        {
            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
            SelectedItemEditor = new SensorSelectionViewModel(selectedItems);
            (SelectedItemEditor as SensorSelectionViewModel)!.RefreshAll();
            IsSelected = true;
        }
    }

    private async void CameraPanelViewModel_CheckSelectedItems(IList<CameraDeviceViewModel> selectedItems)
    {
        if (!(selectedItems.Count > 0))
        {
            if (SelectedItemEditor != null)
                await SelectedItemEditor.DeactivateAsync(true);

            SelectedItemEditor = null;
            IsSelected = false;
        }
        else
        {
            SelectedItemEditor = new CameraSelectionViewModel(selectedItems);
            await SelectedItemEditor.ActivateAsync();
            (SelectedItemEditor as CameraSelectionViewModel)!.RefreshAll();
            IsSelected = true;
        }
    }
    
    private async void SensorPanelViewModel_UpdateAction()
    {
        await GetDeviceType();
    }

    private async void ControllerPanelViewModel_UpdateAction()
    {
        await GetDeviceType();
    }

    private async void CameraPanelViewModel_UpdateAction()
    {
        await GetDeviceType();
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


            _log?.Info($"Tag : {tab.Tag}");

            // 1) 시각 트리 탐색으로 ContentControl 확보
            //var contentControl = VisualTreeHelperEx.FindChild<ContentControl>(tab);

            await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);

            ///PREPARING_TIME_MS는 기존의 ViewModel과 View를 바인딩하고, 유연하게 View를 불러오는 시간적 여유를 
            ///제공하므로써, UI/UX의 경험을 보다 우수하게 만들 수 있다. OnActiveTab에서 제어하지 않고, 
            ///각 ViewModel에서 제어를 하게 되면 여러가지 타이밍적 에러에 의해서 버그가 발생하고 해결하기 어려운 과제로 만들게 된다.
            //await Task.Delay(PREPARING_TIME_MS);

            switch (tab.Tag)
            {
                case "ControllerDeviceViewModel":
                    await TabControlViewModel.ActivateItemAsync(ControllerPanelViewModel);
                    break;

                case "SensorDeviceViewModel":
                    await TabControlViewModel.ActivateItemAsync(SensorPanelViewModel);
                    break;

                case "CameraDeviceViewModel":
                    await TabControlViewModel.ActivateItemAsync(CameraPanelViewModel);
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
        return Task.Run(() =>
        {
            DeviceProvider = IoC.Get<DeviceProvider>();
            NotifyOfPropertyChange(() => DeviceProvider);
        });
    }

    private void ClearData()
    {
        Controller = 0;
        MultiSensor = 0;
        FenseSensor = 0;
        SmartSensor = 0;
        ContactSensor = 0;
        UndergroundSensor = 0;
        PIRSensor = 0;
        IOController = 0;
        LaserSensor = 0;
        IPCamera = 0;
    }

    private Task GetDeviceType(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            Controller = DeviceProvider.OfType<IControllerDeviceModel>().Count();
            await Task.Delay(100, cancellationToken);
            MultiSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.Multi).Count();
            await Task.Delay(100, cancellationToken);
            FenseSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.Fence).Count();
            await Task.Delay(100, cancellationToken);
            SmartSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.SmartSensor).Count();
            await Task.Delay(100, cancellationToken);
            ContactSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.Contact).Count();
            await Task.Delay(100, cancellationToken);
            UndergroundSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.Underground).Count();
            await Task.Delay(100, cancellationToken);
            PIRSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.PIR).Count();
            await Task.Delay(100, cancellationToken);
            IOController = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.IoController).Count();
            await Task.Delay(100, cancellationToken);
            LaserSensor = DeviceProvider.OfType<ISensorDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.Laser).Count();
            await Task.Delay(100, cancellationToken);
            IPCamera = DeviceProvider.OfType<ICameraDeviceModel>().Where(t => t.DeviceType == EnumDeviceType.IpCamera).Count();
            await Task.Delay(100, cancellationToken);
        });
    }


    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public int Controller
    {
        get { return _controller; }
        set
        {
            _controller = value;
            NotifyOfPropertyChange(() => Controller);
        }
    }

    public int MultiSensor
    {
        get { return _multiSensor; }
        set
        {
            _multiSensor = value;
            NotifyOfPropertyChange(() => MultiSensor);
        }
    }
    public int FenseSensor
    {
        get { return _fenseSensor; }
        set
        {
            _fenseSensor = value;
            NotifyOfPropertyChange(() => FenseSensor);
        }
    }

    public int SmartSensor
    {
        get { return _smartSensor; }
        set
        {
            _smartSensor = value;
            NotifyOfPropertyChange(() => SmartSensor);
        }
    }

    public int UndergroundSensor
    {
        get { return _undergroundSensor; }
        set
        {
            _undergroundSensor = value;
            NotifyOfPropertyChange(() => UndergroundSensor);
        }
    }

    public int ContactSensor
    {
        get { return _contactSensor; }
        set
        {
            _contactSensor = value;
            NotifyOfPropertyChange(() => ContactSensor);
        }
    }
    public int PIRSensor
    {
        get { return _pirSensor; }
        set
        {
            _pirSensor = value;
            NotifyOfPropertyChange(() => PIRSensor);
        }
    }
    public int IOController
    {
        get { return _ioController; }
        set
        {
            _ioController = value;
            NotifyOfPropertyChange(() => IOController);
        }
    }
    public int LaserSensor
    {
        get { return _laserSensor; }
        set
        {
            _laserSensor = value;
            NotifyOfPropertyChange(() => LaserSensor);
        }
    }
    public int IPCamera
    {
        get { return _ipCamera; }
        set
        {
            _ipCamera = value;
            NotifyOfPropertyChange(() => IPCamera);
        }
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

    public DeviceTabControlViewModel TabControlViewModel { get; }
    public ControllerDevicePanelViewModel ControllerPanelViewModel { get; }
    public SensorDevicePanelViewModel SensorPanelViewModel { get; }
    public CameraDevicePanelViewModel CameraPanelViewModel { get; }
    public DeviceProvider DeviceProvider { get; private set; }
    #endregion
    #region - Attributes -
    private int _controller;
    private int _multiSensor;
    private int _fenseSensor;
    private int _smartSensor;
    private int _undergroundSensor;
    private int _contactSensor;
    private int _pirSensor;
    private int _ioController;
    private int _laserSensor;
    private int _ipCamera;

    private bool _isSelected;
    private BasePanelViewModel? _selectedItemEditor;
    #endregion
}