using Ironwall.Dotnet.Framework.Models.Accounts;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points;
using Ironwall.Dotnet.Framework.Models.Maps.Symbols;
using Ironwall.Dotnet.Framework.Models.Maps;
using Ironwall.Dotnet.Framework.Enums;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/18/2023 4:41:52 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolDataSaveRequestModel
        : UserSessionBaseRequestModel, ISymbolDataSaveRequestModel
    {
        #region - Ctors -
        public SymbolDataSaveRequestModel()
        {
            Command = EnumCmdType.SYMBOL_DATA_SAVE_REQUEST;
        }

        public SymbolDataSaveRequestModel(
            ILoginSessionModel model
            //, Body<MapModel> maps
            , List<PointClass> points
            , List<SymbolModel> symbols
            , List<ShapeSymbolModel> shapes
            , List<ObjectShapeModel> objects
            )
            : base(model)
        {
            Command = EnumCmdType.SYMBOL_DATA_SAVE_REQUEST;

            //Maps = maps;
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
        //[JsonProperty("maps", Order = 5)]
        //public Body<MapModel> Maps { get; private set; }
        [JsonProperty("points", Order = 6)]
        public List<PointClass>? Points { get; private set; }
        [JsonProperty("symbols", Order = 7)]
        public List<SymbolModel>? Symbols { get; private set; }
        [JsonProperty("shapes", Order = 8)]
        public List<ShapeSymbolModel>? Shapes { get; private set; }
        [JsonProperty("objects", Order = 9)]
        public List<ObjectShapeModel>? Objects { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
