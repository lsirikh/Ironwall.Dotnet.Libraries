using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/26/2025 10:44:41 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceModule : Module
{
    #region - Ctors -
    public DeviceModule(ILogService? log = default, int count = default)
    {
        _log = log;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            builder.RegisterType<DeviceProvider>().SingleInstance();
            builder.RegisterType<DeviceModelConverter>().SingleInstance();
            builder.RegisterType<DeviceModelListConverter>().SingleInstance();

            builder.RegisterType<ControllerDeviceProvider>().As<ControllerDeviceProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);
            builder.RegisterType<SensorDeviceProvider>().As<SensorDeviceProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);
            builder.RegisterType<CameraDeviceProvider>().As<CameraDeviceProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);

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
    private int _count;
    #endregion
}