using Newtonsoft.Json;
using System.Collections.Generic;
using System.Xml;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/3/2024 11:13:59 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class FilterTypeModel : IFilterTypeModel
    {
        public FilterTypeModel()
        {
            Any = new List<XmlElement>();
        }
        [JsonProperty("any", Order = 1)]
        public List<XmlElement> Any { get; set; }
    }
}
