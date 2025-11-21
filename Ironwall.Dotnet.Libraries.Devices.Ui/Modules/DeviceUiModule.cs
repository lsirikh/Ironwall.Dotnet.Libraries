using Autofac;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Modules;
using Ironwall.Dotnet.Libraries.Devices.Modules;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.OnvifSolution.Modules;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/28/2025 3:49:06 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceUiModule : Module
{
    #region - Ctors -
    public DeviceUiModule( IApiSetupModel apiSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _apiSetup = apiSetup;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            builder.RegisterModule(new DeviceModule(_log, _count++));
            //builder.RegisterModule(new DeviceDbModule(_log, _apiSetup, _count++)); // 2
            builder.RegisterModule(new DeviceApiModule(_log, new ApiSetupModel(_apiSetup), count: _count++)); // 2
            builder.RegisterType<DeviceDashboardViewModel>().SingleInstance();
            builder.RegisterType<DeviceTabControlViewModel>().SingleInstance();
            builder.RegisterType<ControllerDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<SensorDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<CameraDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<ControllerDeviceViewModel>().SingleInstance();
            builder.RegisterType<SensorDevicePanelViewModel>().SingleInstance();
            builder.RegisterType<OnvifDialogViewModel>().SingleInstance();
            builder.RegisterType<CameraDeviceViewModel>().SingleInstance();
        }
        catch
        {
            throw;
        }
    }
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
    #endregion
    #region - Attributes -
    private ILogService? _log;
    //private IMariaDbSetupModel _apiSetup;
    private IApiSetupModel _apiSetup;
    private int _count;
    #endregion
}