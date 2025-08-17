using Autofac;
using System;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Db.Modules;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/23/2025 2:22:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class GMapUiModule: Module
{
    #region - Ctors -
    public GMapUiModule(IGMapSetupModel gMapSetup, IMariaDbSetupModel gMapDbSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _count = count;
        _gMapSetup = gMapSetup;
        _gMapDbSetup = gMapDbSetup;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterModule(new GMapDbModule(_gMapSetup, _gMapDbSetup, _log, _count)); // 4

        builder.RegisterType<GMapControl>().SingleInstance();
        builder.RegisterType<GMapCustomControl>().SingleInstance();
        builder.RegisterType<MapViewModel>().SingleInstance();
        builder.RegisterType<TileGenerationService>().SingleInstance();
        builder.RegisterType<CustomMapService>().SingleInstance();
        builder.RegisterType<ImageOverlayService>().SingleInstance();
        //builder.RegisterType<MGRSGridOverlayService>().SingleInstance();
    }
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
    private IGMapSetupModel _gMapSetup;
    private IMariaDbSetupModel _gMapDbSetup;
    #endregion
}