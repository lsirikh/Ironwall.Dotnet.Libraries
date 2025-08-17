using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/15/2025 2:32:26 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapModule : Module
{
    #region - Ctors -
    public GMapModule(IGMapSetupModel model, ILogService? log = default, int count = default)
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
            var setupModel = new GMapSetupModel(_model);
            builder.RegisterInstance(setupModel);

            builder.RegisterType<MapProvider>().SingleInstance();
            builder.RegisterType<CustomMapProvider>().As<CustomMapProvider>()
                .As<ILoadable>().SingleInstance().WithMetadata("Order", _count++);
            builder.RegisterType<DefinedMapProvider>().As<DefinedMapProvider>()
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
    private IGMapSetupModel _model;
    #endregion
}