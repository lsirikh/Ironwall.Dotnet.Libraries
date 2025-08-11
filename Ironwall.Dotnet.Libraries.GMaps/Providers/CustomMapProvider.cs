using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Defines;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Providers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/25/2025 10:00:17 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public class CustomMapProvider : BaseMapProdiver<ICustomMapModel>
{
    #region - Ctors -
    public CustomMapProvider(ILogService log, MapProvider provider) : base(log, provider)
    {
    }
    #endregion
    #region - Implementation of Interface -
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
    #endregion
}