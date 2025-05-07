using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Ais;

/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 1/14/2025 7:10:19 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class NetworkSettingModel : BaseModel, INetworkSettingModel
{
    #region - Ctors -
    public NetworkSettingModel()
    {

    }

    public NetworkSettingModel(int id, bool isAvailable, string name, string ipAddress, int port) : base(id)
    {
        IsAvailable = isAvailable;
        Name = name;
        IpAddress = ipAddress;
        Port = port;
    }

    public NetworkSettingModel(INetworkSettingModel model): base(model) 
    {
        IsAvailable = model.IsAvailable;
        Name = model.Name;
        IpAddress = model.IpAddress;
        Port = model.Port;
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
    [JsonProperty("is_available", Order = 2)]
    public bool IsAvailable { get; set; }
    [JsonProperty("channel_name", Order = 3)]
    public string Name { get; set; } = string.Empty;
    [JsonProperty("ip_address", Order = 4)]
    public string IpAddress { get; set; } = string.Empty;
    [JsonProperty("port", Order = 5)]
    public int Port { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}