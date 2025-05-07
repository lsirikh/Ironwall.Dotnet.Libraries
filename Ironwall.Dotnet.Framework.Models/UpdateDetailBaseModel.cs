using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;

using System;

namespace Ironwall.Dotnet.Framework.Models;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 5/15/2023 4:59:23 PM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class UpdateDetailBaseModel : IUpdateDetailBaseModel
{

    #region - Ctors -
    public UpdateDetailBaseModel()
    {
        UpdateTime = DateTime.MinValue;
    }

    public UpdateDetailBaseModel(DateTime dateTime)
    {
        UpdateTime = dateTime;
    }

    public UpdateDetailBaseModel(IUpdateDetailBaseModel model)
    {
        UpdateTime = model.UpdateTime;
    }

    public UpdateDetailBaseModel(IUpdateMapperBase model)
    {
        UpdateTime = DateTime.Parse(model.UpdateTime);
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
    [JsonProperty("updatetime", Order = 9)]
    public DateTime UpdateTime { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
