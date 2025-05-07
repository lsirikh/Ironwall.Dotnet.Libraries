using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 6/28/2023 3:23:50 PM                                                    
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
