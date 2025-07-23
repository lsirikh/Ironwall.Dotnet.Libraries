using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Providers;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/11/2025 4:26:32 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SoundModule : Module
{
    #region - Ctors -
    public SoundModule(ISoundSetupModel model, ILogService? log = default, int count = default)
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
            var setupModel = new SoundSetupModel(_model);
            builder.RegisterInstance(setupModel);

            builder.RegisterType<SoundSourceProvider>().SingleInstance();
            builder.RegisterType<AudioDeviceInfoProvider>().SingleInstance();
            builder.RegisterType<SoundService>().As<ISoundService>().As<IService>()
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
    private int _count;
    private ISoundSetupModel _model;
    #endregion
}