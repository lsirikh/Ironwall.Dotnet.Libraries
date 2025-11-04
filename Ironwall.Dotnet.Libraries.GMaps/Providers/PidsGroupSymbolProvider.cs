using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Defines;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Providers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/23/2025 7:42:49 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public class PidsGroupSymbolProvider : BaseSymbolProvider<IPidsGroupSymbolModel>
{
    #region - Ctors -
    public PidsGroupSymbolProvider(ILogService log, SymbolProvider provider) : base(log, provider)
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