using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    public interface IImagingOptionModel
    {
        float Brightness { get; set; }
        LevelModel BrightnessRange { get; set; }

        float ColorSaturation { get; set; }
        LevelModel ColorSaturationRange { get; set; }

        float Contrast { get; set; }
        LevelModel ContrastRange { get; set; }

        float Sharpness { get; set; }
        LevelModel SharpnessRange { get; set; }

        ExposureModel Exposure { get; set; }
        FocusModel Focus { get; set; }

        EnumIrCutFilterMode IrCutFilter { get; set; }

        BacklightCompensationModel BacklightCompensation { get; set; }
        WhiteBalanceModel WhiteBalance { get; set; }
        WideDynamicRangeModel WideDynamicRange { get; set; }


        
    }
}
