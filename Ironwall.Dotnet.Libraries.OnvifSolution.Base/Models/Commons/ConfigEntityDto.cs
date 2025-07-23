using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 2:54:40 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ConfigEntityDto : IConfigEntityDto
{
    #region - Ctors -
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
    [JsonProperty("name", Order = 1)] public string Name { get; init; } = default!;
    [JsonProperty("use_count", Order = 2)] public int UseCount { get; init; }
    [JsonProperty("token", Order = 3)] public string Token { get; init; } = default!;
    #endregion
    #region - Attributes -
    #endregion
}