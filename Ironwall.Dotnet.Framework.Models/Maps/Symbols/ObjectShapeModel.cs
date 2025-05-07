using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Maps.Symbols
{
    /****************************************************************************
        Purpose      : Canvas의 시현되는 요소이지만, Ironwall에 활용되는 구역 및 
                        장비, 센서 번호 등의 속성을 포함하고 있다.                                                           
        Created By   : GHLee                                                
        Created On   : 4/21/2023 8:45:51 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ObjectShapeModel : ShapeSymbolModel, IObjectShapeModel
    {

        #region - Ctors -
        public ObjectShapeModel()
        {
        }

        public ObjectShapeModel(IObjectShapeModel model) : base(model)
        {
            IdController = model.IdController;
            IdSensor = model.IdSensor;
            NameArea = model.NameArea;
            NameDevice = model.NameDevice;
            TypeDevice = model.TypeDevice;
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
        /// 제어기 번호
        /// </summary>
        [JsonProperty("idcontroller", Order = 17)]
        public int IdController { get; set; }
        /// <summary>
        /// 센서 번호
        /// </summary>
        [JsonProperty("idsensor", Order = 18)]
        public int IdSensor { get; set; }
        /// <summary>
        /// 구역 이름
        /// </summary>
        [JsonProperty("namearea", Order = 19)]
        public string NameArea { get; set; } 
        /// <summary>
        /// 장비 이름
        /// </summary>
        [JsonProperty("namedevice", Order = 20)]
        public string NameDevice { get; set; }
        /// <summary>
        /// 장비 종류
        /// </summary>
        [JsonProperty("typedevice", Order = 21)]
        public int TypeDevice { get; set; }

        #endregion
        #region - Attributes -
        #endregion
    }
}
