using Ironwall.Dotnet.Framework.Models.Devices;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/28/2023 4:01:25 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PresetTableMapper : OptionMapperBase, IPresetTableMapper
    {

        #region - Ctors -
        public PresetTableMapper()
        {

        }

        public PresetTableMapper(ICameraPresetModel model) : base(model)
        {
            PresetName = model.PresetName;
            IsHome = model.IsHome;
            Pan = model.Pan;
            Tilt = model.Tilt;
            Zoom = model.Zoom;
            Delay = model.Delay;
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
        [JsonProperty("preset_name", Order = 3)]
        public string PresetName { get; set; }
        [JsonProperty("ishome", Order = 4)]
        public bool IsHome { get; set; }
        [JsonProperty("pan", Order = 5)]
        public double Pan { get; set; }
        [JsonProperty("tilt", Order = 6)]
        public double Tilt { get; set; }
        [JsonProperty("zoom", Order = 7)]
        public double Zoom { get; set; }
        [JsonProperty("delay", Order = 8)]
        public int Delay { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
