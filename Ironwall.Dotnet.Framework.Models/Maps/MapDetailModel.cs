using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace Ironwall.Dotnet.Framework.Models.Maps
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 10/10/2023 9:02:30 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MapDetailModel : UpdateDetailBaseModel, IMapDetailModel
    {

        #region - Ctors -
        public MapDetailModel()
        {

        }
        public MapDetailModel(List<MapModel> maps, DateTime updateTime)
        {
            Maps = maps;
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
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        [JsonProperty("maps", Order = 1)]
        public List<MapModel> Maps { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
