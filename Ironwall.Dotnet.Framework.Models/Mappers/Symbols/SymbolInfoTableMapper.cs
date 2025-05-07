using Ironwall.Dotnet.Framework.Models.Maps;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/15/2023 4:28:10 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolInfoTableMapper : UpdateMapperBase, ISymbolInfoTableMapper
    {

        #region - Ctors -
        public SymbolInfoTableMapper()
        {

        }
        public SymbolInfoTableMapper(ISymbolDetailModel model)
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
        public int Map { get; set; }
        public int Symbol { get; set; }
        public int ShapeSymbol { get; set; }
        public int ObjectShape { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
