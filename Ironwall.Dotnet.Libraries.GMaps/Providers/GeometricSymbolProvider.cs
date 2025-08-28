using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Defines;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Providers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/19/2025 10:06:13 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public class GeometricSymbolProvider : BaseSymbolProvider<IGeometricSymbolModel>
{
    #region - Ctors -
    public GeometricSymbolProvider(ILogService log, SymbolProvider provider) : base(log, provider)
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