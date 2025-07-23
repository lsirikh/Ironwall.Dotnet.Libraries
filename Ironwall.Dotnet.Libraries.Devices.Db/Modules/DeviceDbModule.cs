using Autofac;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Db.Models;
using Ironwall.Dotnet.Libraries.Devices.Db.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Db.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/26/2025 11:27:32 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceDbModule : Module
{
    #region - Ctors -
    public DeviceDbModule(ILogService? log, IMariaDbSetupModel model, int count)
    {
        _log = log;
        _model = model;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            var setupModel = new DeviceDbSetupModel(_model);
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<DeviceDbService>().As<IDeviceDbService>().As<IService>()
                .SingleInstance().WithMetadata("Order", _count);
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
    private IMariaDbSetupModel _model;
    private int _count;
    #endregion
}