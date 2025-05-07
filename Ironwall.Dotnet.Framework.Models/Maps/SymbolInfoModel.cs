using Ironwall.Dotnet.Framework.Models.Devices;
using Newtonsoft.Json;

using System;

namespace Ironwall.Dotnet.Framework.Models.Maps
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/16/2023 9:01:12 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolInfoModel : ISymbolInfoModel
    {

        #region - Ctors -
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        public void Update(ISymbolDetailModel model)
        {
            Map = model.Map;
            Symbol = model.Symbol;
            ShapeSymbol = model.ShapeSymbol;
            ObjectShape = model.ObjectShape;
            UpdateTime = model.UpdateTime;
        }

        public void Clear()
        {
            Map = 0;
            Symbol = 0;
            ShapeSymbol = 0;
            ObjectShape = 0;
            UpdateTime = DateTime.MinValue;
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        [JsonProperty("map", Order = 1)]
        public int Map { get; set; }
        [JsonProperty("symbol", Order = 2)]
        public int Symbol { get; set; }
        [JsonProperty("shapesymbol", Order = 3)]
        public int ShapeSymbol { get; set; }
        [JsonProperty("objectshape", Order = 4)]
        public int ObjectShape { get; set; }
        [JsonProperty("updatetime", Order = 5)]
        public DateTime UpdateTime { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
