using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Nats.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 10/29/2025 8:05:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class NatsMessageModel : INatsMessageModel
{
    public NatsMessageModel()
    {

    }

    public NatsMessageModel(MessageArgsModel e, JsonSerializerSettings settings)
    {
        Model = e;
        Settings = settings;
    }

    public MessageArgsModel? Model { get; set; }
    public JsonSerializerSettings? Settings { get; set; }
}