using Autofac;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Modules;
using Ironwall.Dotnet.Libraries.Sounds.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Sounds.Ui.Views;
using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Ui.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/11/2025 7:03:48 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SoundUiModule : Module
{
    #region - Ctors -
    public SoundUiModule(ISoundSetupModel soundSetup, ILogService? log = default, int count = default)
    {
        _log = log;
        _soundSetup = soundSetup;
        _count = count;
    }
    #endregion
    #region - Implementation of Interface -
    protected override void Load(ContainerBuilder builder)
    {
        try
        {
            builder.RegisterModule(new SoundModule(_soundSetup, _log, _count++));
            builder.RegisterType<SoundSettingViewModel>().SingleInstance();
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
    private ISoundSetupModel _soundSetup;
    private int _count;
    #endregion
}