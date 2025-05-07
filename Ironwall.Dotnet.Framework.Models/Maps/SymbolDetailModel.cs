using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Maps
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/15/2023 3:01:34 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolDetailModel : UpdateDetailBaseModel, ISymbolDetailModel
    {

        #region - Ctors -
        public SymbolDetailModel()
        {

        }

        public SymbolDetailModel(int map, int symbol, int shapeSymbol, int objectShape, DateTime updateTime)
        {
            Map = map;
            Symbol = symbol;
            ShapeSymbol = shapeSymbol;
            ObjectShape = objectShape;
            UpdateTime = updateTime;
        }

        public SymbolDetailModel(ISymbolDetailModel model)
            : base(model)
        {
            Map = model.Map;
            Symbol = model.Symbol;
            ShapeSymbol = model.ShapeSymbol;
            ObjectShape = model.ObjectShape;
        }

        public SymbolDetailModel(ISymbolInfoTableMapper model)
           : base(model)
        {
            Map = model.Map;
            Symbol = model.Symbol;
            ShapeSymbol = model.ShapeSymbol;
            ObjectShape = model.ObjectShape;
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
        [JsonProperty("symbol", Order = 2)]
        public int Symbol { get; set; }
        [JsonProperty("shapesymbol", Order = 3)]
        public int ShapeSymbol { get; set; }
        [JsonProperty("objectshape", Order = 4)]
        public int ObjectShape { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
