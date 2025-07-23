using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 3:22:26 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventModule : Module
{
    #region - Ctors -
    public EventModule(IEventSetupModel model, ILogService? log = default, int count = default)
    {
        _log = log;
        _count = count;
        _model = model;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            var setupModel = new EventSetupModel(_model);
            builder.RegisterInstance(setupModel);

            builder.RegisterType<EventProvider>().SingleInstance();
            builder.RegisterType<EventModelConverter>().SingleInstance();

            builder.RegisterType<DetectionEventProvider>().As<DetectionEventProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);
            builder.RegisterType<ConnectionEventProvider>().As<ConnectionEventProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);
            builder.RegisterType<MalfunctionEventProvider>().As<MalfunctionEventProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);
            builder.RegisterType<ActionEventProvider>().As<ActionEventProvider>()
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
    private IEventSetupModel _model;
    #endregion
}