using Ironwall.Dotnet.Libraries.Redis.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Redis.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/1/2025 3:34:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class RedisMessageModel : IRedisMessageModel
{
    public RedisMessageModel()
    {
        
    }

    public RedisMessageModel(MessageArgsModel e, JsonSerializerSettings settings)
    {
        Model = e;
        Settings = settings;
    }

    public MessageArgsModel? Model { get; set; }
    public JsonSerializerSettings? Settings { get; set; }
}