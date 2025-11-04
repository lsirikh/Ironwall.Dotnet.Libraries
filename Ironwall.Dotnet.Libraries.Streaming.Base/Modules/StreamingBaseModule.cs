using Autofac;
using Ironwall.Dotnet.Libraries.Streaming.Base.Providers;
using System;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Modules;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 10/10/2025 10:39:21 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class StreamingBaseModule : Module
{
    #region - Ctors -
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);
        builder.RegisterType<CameraDeviceProvider>().SingleInstance();
        builder.RegisterType<CameraEventProvider>().SingleInstance();
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
    #endregion
}