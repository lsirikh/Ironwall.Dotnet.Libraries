using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Defines;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 4:38:35 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class BaseModel : IBaseModel
{
    #region - Ctors -
    public BaseModel()
    {
    }

    public BaseModel(int id)
    {
        Id = id;
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// BaseModel의 정보 변경에 따른 업데이트
    /// </summary>
    /// <param name="model"></param>
    public virtual void Update(IBaseModel model)
    {
        Id = model.Id;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// Instance or Record Id
    /// </summary>
    [JsonProperty("id", Order = 0)]
    public int Id { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}