using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Devices;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 7/4/2023 5:42:11 PM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class MappingInfoModel : IMappingInfoModel
{
    #region - Ctors -
    public MappingInfoModel()
    {
    }

    public MappingInfoModel(int mapping, DateTime updateTime)
    {
        Mapping = mapping;
        UpdateTime = updateTime;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void Update(IMappingInfoModel model)
    {
        Mapping = model.Mapping;
        UpdateTime = model.UpdateTime;
    }

    public void Clear()
    {
        Mapping = 0;
        UpdateTime = DateTime.MinValue;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    [JsonProperty("mapping", Order = 1)]
    public int Mapping { get; set; }
    [JsonProperty("updatetime", Order = 2)]
    public DateTime UpdateTime { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
