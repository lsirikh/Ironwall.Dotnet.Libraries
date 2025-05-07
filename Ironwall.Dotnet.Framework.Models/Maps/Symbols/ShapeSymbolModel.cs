using Newtonsoft.Json;
using System.Windows.Media;

namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols
{
    /****************************************************************************
        Purpose      : 도형의 형태를 갖는 Symbol                                                          
        Created By   : GHLee                                                
        Created On   : 4/21/2023 9:22:24 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ShapeSymbolModel : SymbolModel, IShapeSymbolModel
    {

        #region - Ctors -
        public ShapeSymbolModel()
        {

        }
        public ShapeSymbolModel(IShapeSymbolModel model)
            : base(model)
        {
            ShapeStrokeThick = model.ShapeStrokeThick;
            ShapeStroke = model.ShapeStroke;
            ShapeFill = model.ShapeFill;
            Points = model.Points;
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
        /// <summary>
        /// Stroke 두께
        /// </summary>
        [JsonProperty("shapestrokethick", Order = 14)]
        public double ShapeStrokeThick { get; set; } = 2;
        /// <summary>
        /// Stroke의 색깔
        /// </summary>
        [JsonProperty("shapestroke", Order = 15)]
        public string ShapeStroke { get; set; } = "#FFFF0000";
        /// <summary>
        /// Shape 색 채움
        /// </summary>
        [JsonProperty("shapefill", Order = 16)]
        public string ShapeFill { get; set; } = "#00FFFFFF";
        /// <summary>
        /// PointCollection
        /// </summary>
        [JsonIgnore]
        public PointCollection Points {get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
