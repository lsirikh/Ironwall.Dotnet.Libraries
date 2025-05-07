using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Vms
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/12/2024 11:15:25 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class VmsApiModel : BaseModel, IVmsApiModel
    {
        #region - Ctors -
        public VmsApiModel()
        {

        }

        public VmsApiModel(int id, string ip, uint port, string username, string password)
        {
            Id = id;
            ApiAddress = ip;
            ApiPort = port;
            Username = username;
            Password = password;
        }

        public VmsApiModel(IVmsApiModel model) : base(model)
        {
            ApiAddress = model.ApiAddress;
            ApiPort = model.ApiPort;
            Username = model.Username;
            Password = model.Password;
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
        [JsonProperty("api_address", Order = 2)]
        public string ApiAddress { get; set; }
        [JsonProperty("api_port", Order = 3)]
        public uint ApiPort { get; set; }
        [JsonProperty("username", Order = 4)]
        public string Username { get; set; }
        [JsonProperty("password", Order = 5)]
        public string Password { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}