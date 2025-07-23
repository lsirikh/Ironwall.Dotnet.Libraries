using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 6:44:38 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class BaseModel : IBaseModel
{
    public BaseModel()
    {

    }

    public BaseModel(int id)
    {
        Id = id;
    }

    public BaseModel(IBaseModel model)
    {
        Id = model.Id;
    }

    [JsonProperty("id", Order = 1)]
    public int Id { get; set; }
}