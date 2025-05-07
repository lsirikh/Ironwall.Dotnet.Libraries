using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols.Points
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 4/21/2023 9:59:34 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PointClass : IPointClass
    {

        #region - Ctors -
        public PointClass()
        {
                
        }

        public PointClass(int pGroup, int sequence, double x, double y)
        {
            PointGroup = pGroup;
            Sequence = sequence;
            X = x;
            Y = y;
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
        /// 레코드 혹은 인스턴스의 고유 번호
        /// </summary>
        [JsonProperty("id", Order = 1)]
        public int Id { get; set; }
        /// <summary>
        /// 포인트 정보가 소속된 ShapeSymbolModel 
        /// 혹은 ObjectShapeModel 아이디
        /// </summary>
        [JsonProperty("pointgroup", Order = 2)]
        public int PointGroup { get; set; }
        /// <summary>
        /// 포인트의 순서 정보
        /// </summary>
        [JsonProperty("sequence", Order = 3)]
        public int Sequence { get; set; }
        /// <summary>
        /// 포인트 x위치 정보
        /// </summary>
        [JsonProperty("x", Order = 4)]
        public double X { get; set; }
        /// <summary>
        /// 포인트 y위치 정보
        /// </summary>
        [JsonProperty("y", Order = 5)]
        public double Y { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
