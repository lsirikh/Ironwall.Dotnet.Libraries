using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Defines;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Providers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/26/2025 4:24:27 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public class PidsSymbolProvider : BaseSymbolProvider<IPidsSymbolModel>
{
    #region - Ctors -
    public PidsSymbolProvider(ILogService log, SymbolProvider provider)
        :base(log, provider)
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