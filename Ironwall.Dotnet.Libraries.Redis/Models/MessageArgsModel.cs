using System;

namespace Ironwall.Dotnet.Libraries.Redis.Models
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 2/2/2024 4:35:26 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MessageArgsModel : EventArgs
    {

        #region - Ctors -
        public MessageArgsModel()
        {
        }

        public MessageArgsModel(string? channel, string? subscriptionChannel, string? message) 
        {
            Channel = channel;
            SubscriptionChannel = subscriptionChannel;
            Message = message;
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
        public string? Channel { get; set; }
        public string? SubscriptionChannel { get; set; }
        public string? Message { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
