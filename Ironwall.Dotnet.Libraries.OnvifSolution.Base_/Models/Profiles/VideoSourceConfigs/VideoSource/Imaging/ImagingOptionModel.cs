using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:05:03 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ImagingOptionModel : IImagingOptionModel
    {
        public ImagingOptionModel()
        {
            BrightnessRange = new LevelModel();
            ColorSaturationRange = new LevelModel();
            ContrastRange = new LevelModel();
            SharpnessRange = new LevelModel();

            Exposure = new ExposureModel();
            Focus = new FocusModel();
            BacklightCompensation = new BacklightCompensationModel();
            WhiteBalance = new WhiteBalanceModel();
            WideDynamicRange = new WideDynamicRangeModel();
        }

        [JsonProperty("brightness", Order = 1)]
        public float Brightness { get; set; }

        [JsonProperty("brightness_range", Order = 2)]
        public LevelModel BrightnessRange { get; set; }

        [JsonProperty("color_saturation", Order = 3)]
        public float ColorSaturation { get; set; }

        [JsonProperty("color_saturation_range", Order = 4)]
        public LevelModel ColorSaturationRange { get; set; }

        [JsonProperty("contrast", Order = 5)]
        public float Contrast { get; set; }

        [JsonProperty("contrast_range", Order = 6)]
        public LevelModel ContrastRange { get; set; }

        [JsonProperty("sharpness", Order = 7)]
        public float Sharpness { get; set; }

        [JsonProperty("sharpness_range", Order = 8)]
        public LevelModel SharpnessRange { get; set; }

        [JsonProperty("exposure", Order = 9)]
        public ExposureModel Exposure { get; set; }

        [JsonProperty("focus", Order = 10)]
        public FocusModel Focus { get; set; }

        [JsonProperty("ir_cut_filter_mode", Order = 11)]
        public EnumIrCutFilterMode IrCutFilter { get; set; }

        [JsonProperty("backlight_compensation", Order = 12)]
        public BacklightCompensationModel BacklightCompensation { get; set; }

        [JsonProperty("white_balance", Order = 13)]
        public WhiteBalanceModel WhiteBalance { get; set; }

        [JsonProperty("wide_dynamic_range", Order = 14)]
        public WideDynamicRangeModel WideDynamicRange { get; set; }

    }
}
