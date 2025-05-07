using Newtonsoft.Json;

using System;

namespace Ironwall.Dotnet.Framework.Models.Maps
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/18/2023 5:09:12 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolMoreDetailModel : UpdateDetailBaseModel, ISymbolMoreDetailModel
    {

        #region - Ctors -
        public SymbolMoreDetailModel()
        {

        }
        public SymbolMoreDetailModel(int map, int points, int symbol, int shapeSymbol, int objectShape, DateTime updateTime)
        {
            Map = map;
            Points = points;
            Symbol = symbol;
            ShapeSymbol = shapeSymbol;
            ObjectShape = objectShape;
            UpdateTime = updateTime;
        }

        public SymbolMoreDetailModel(ISymbolMoreDetailModel model)
        {
            Map = model.Map;
            Points = model.Points;
            Symbol = model.Symbol;
            ShapeSymbol = model.ShapeSymbol;
            ObjectShape = model.ObjectShape;
            UpdateTime = model.UpdateTime;
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
        [JsonProperty("map", Order = 1)]
        public int Map { get; set; }
        [JsonProperty("points", Order = 2)]
        public int Points { get; set; }
        [JsonProperty("symbol", Order = 3)]
        public int Symbol { get; set; }
        [JsonProperty("shapesymbol", Order = 4)]
        public int ShapeSymbol { get; set; }
        [JsonProperty("objectshape", Order = 5)]
        public int ObjectShape { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
