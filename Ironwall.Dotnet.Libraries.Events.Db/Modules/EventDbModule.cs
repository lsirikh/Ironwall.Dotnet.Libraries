using Autofac;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Db.Models;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Db.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 5:46:22 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventDbModule : Module
{
    #region - Ctors -
    public EventDbModule(IMariaDbSetupModel model, ILogService? log = default,  int count = default)
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
            var setupModel = new EventDbSetupModel(_model);
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<EventDbService>().As<IEventDbService>().As<IService>()
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