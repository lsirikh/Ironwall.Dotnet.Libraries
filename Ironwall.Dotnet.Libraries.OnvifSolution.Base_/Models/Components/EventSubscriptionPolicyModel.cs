using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/3/2024 11:13:13 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class EventSubscriptionModel : IEventSubscriptionModel
    {
        [JsonProperty("filter", Order = 1)]
        public FilterTypeModel Filter { get; set; }
        [JsonProperty("subscription_policy", Order = 2)]
        public FilterTypeModel SubscriptionPolicy { get; set; }
    }
}
