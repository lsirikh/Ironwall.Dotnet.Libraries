using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Defines;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.GMaps.Providers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/11/2025 5:47:25 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]

public class MilitarySymbolProvider : BaseSymbolProvider<IMilitarySymbolModel>
{
    #region - Ctors -
    public MilitarySymbolProvider(ILogService log, SymbolProvider provider) : base(log, provider)
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