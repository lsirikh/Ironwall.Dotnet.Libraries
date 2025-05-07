using Ironwall.Dotnet.Framework.Models.Devices;
using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points;
using Ironwall.Dotnet.Framework.Enums;
using Newtonsoft.Json;

using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/17/2023 6:44:21 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolDataLoadResponseModel 
        : ResponseModel, ISymbolDataLoadResponseModel
    {
        #region - Ctors -
        public SymbolDataLoadResponseModel()
        {
            Command = EnumCmdType.SYMBOL_DATA_LOAD_RESPONSE;
        }

        public SymbolDataLoadResponseModel(
            bool success
            , string content
            , List<MapModel> maps
            , List<PointClass> points
            , List<SymbolModel> symbols
            , List<ShapeSymbolModel> shapes
            , List<ObjectShapeModel> objects)
            : base(success, content)
        {
            Command = EnumCmdType.SYMBOL_DATA_LOAD_RESPONSE;
            Maps = maps;
            Points = points;
            Symbols = symbols;
            Shapes = shapes;
            Objects = objects;
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
        [JsonProperty("maps", Order = 4)]
        public List<MapModel>? Maps { get; private set; }
        [JsonProperty("points", Order = 5)]
        public List<PointClass>? Points { get; private set; }
        [JsonProperty("symbols", Order = 6)]
        public List<SymbolModel>? Symbols { get; private set; }
        [JsonProperty("shapes", Order = 7)]
        public List<ShapeSymbolModel>? Shapes { get; private set; }
        [JsonProperty("objects", Order = 8)]
        public List<ObjectShapeModel>? Objects { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
