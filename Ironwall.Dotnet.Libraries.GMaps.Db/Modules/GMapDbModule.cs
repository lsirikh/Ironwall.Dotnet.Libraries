using Autofac;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Modules;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/25/2025 10:31:05 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapDbModule : Module
{
    #region - Ctors -
    public GMapDbModule(IGMapSetupModel gMapSetup, IMariaDbSetupModel gMapDbSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _gMapSetupModel = gMapSetup;
        _gMapDbSetupModel = gMapDbSetup;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            builder.RegisterModule(new GMapModule(_gMapSetupModel, _log, _count)); // 4

            var setupModel = new GMapDbSetupModel(_gMapDbSetupModel);
            builder.RegisterInstance(setupModel).AsSelf().SingleInstance();
            builder.RegisterType<GMapDbService>().As<IGMapDbService>().As<IService>()
                .SingleInstance().WithMetadata("Order", _count++);
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
    private IGMapSetupModel _gMapSetupModel;
    private IMariaDbSetupModel _gMapDbSetupModel;
    private int _count;
    #endregion
}