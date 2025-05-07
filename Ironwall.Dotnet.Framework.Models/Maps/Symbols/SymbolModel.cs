using Newtonsoft.Json;

namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols
{
    /****************************************************************************
        Purpose      : Canvas에서 시현되는 심볼의 기본 모델                                                          
        Created By   : GHLee                                                
        Created On   : 4/21/2023 8:41:16 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SymbolModel : ISymbolModel
    {

        #region - Ctors -
        public SymbolModel()
        {

        }

        public SymbolModel(ISymbolModel model)
        {
            Id = model.Id;
            X = model.X;
            Y = model.Y;
            Z = model.Z;

            Width = model.Width;
            Height = model.Height;
            Angle = model.Angle;
            IsShowLable = model.IsShowLable;
            Lable = model.Lable;

            FontSize = model.FontSize;
            FontColor = model.FontColor;
            IsVisible = model.IsVisible;

            TypeShape = model.TypeShape;
            Layer = model.Layer;
            Map = model.Map;
            IsUsed = model.IsUsed;
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
        /// 심볼의 아이디
        /// </summary>
        [JsonProperty("id", Order = 1)]
        public int Id { get; set; }
        /// <summary>
        /// 심볼의 X축 위치
        /// </summary>
        [JsonProperty("x", Order = 2)]
        public double X { get; set; }
        /// <summary>
        /// 심볼의 Y축 위치
        /// </summary>
        [JsonProperty("y", Order = 3)]
        public double Y { get; set; }
        /// <summary>
        /// 심볼의 Z축 위치
        /// </summary>
        [JsonProperty("z", Order = 4)]
        public double Z { get; set; }
        /// <summary>
        /// 심볼의 넓이
        /// </summary>
        [JsonProperty("width", Order = 5)]
        public double Width { get; set; }
        /// <summary>
        /// 심볼의 높이
        /// </summary>
        [JsonProperty("height", Order = 6)]
        public double Height { get; set; }
        /// <summary>
        /// 심볼의 각도
        /// </summary>
        [JsonProperty("angle", Order = 7)]
        public double Angle { get; set; }
        /// <summary>
        /// 레이블 시현여부
        /// </summary>
        [JsonProperty("isshowlable", Order = 8)]
        public bool IsShowLable { get; set; }
        /// <summary>
        /// 심볼의 레이블
        /// </summary>
        [JsonProperty("lable", Order = 9)]
        public string Lable { get; set; } = "noname";
        /// <summary>
        /// 레이블 글자의 크기 지정
        /// </summary>
        [JsonProperty("fontsize", Order = 10)]
        public double FontSize { get; set; } = 15d;
        /// <summary>
        /// 레이블 글자의 색깔 지정
        /// </summary>
        [JsonProperty("fontcolor", Order = 11)]
        public string FontColor { get; set; } = "#FFFF0000";
        /// <summary>
        /// 심볼 시현 여부
        /// </summary>
        [JsonProperty("isvisible", Order = 12)]
        public bool IsVisible { get; set; } = true;
        /// <summary>
        /// Symbol의 모양 정의
        /// </summary>
        [JsonProperty("typeshape", Order = 13)]
        public int TypeShape { get; set; }
        /// <summary>
        /// 소속된 Layer를 정의
        /// </summary>
        [JsonProperty("layer", Order = 29)]
        public int Layer { get; set; }
        /// <summary>
        /// 소속된 Map을 정의
        /// </summary>
        [JsonProperty("map", Order = 30)]
        public int Map { get; set; }
        /// <summary>
        /// True/False에 따라 연동을 제어 
        /// </summary>
        [JsonProperty("isused", Order = 31)]
        public bool IsUsed { get; set; } = true;
        #endregion
        #region - Attributes -
        #endregion
    }
}
