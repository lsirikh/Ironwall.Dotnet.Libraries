using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 4:12:54 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*---------------------------  Rectangle DTO  ---------------------------*/
public sealed class RectangleDto
{
    [JsonProperty("x", Order = 1)] public int X { get; init; }
    [JsonProperty("y", Order = 2)] public int Y { get; init; }
    [JsonProperty("width", Order = 3)] public int Width { get; init; }
    [JsonProperty("height", Order = 4)] public int Height { get; init; }
}

/*---------------------------  Multicast DTO  ---------------------------*/
public sealed class MulticastConfigDto
{
    [JsonProperty("address", Order = 1)] public IpAddressDto Address { get; init; } = default!;
    [JsonProperty("port", Order = 2)] public int Port { get; init; }
    [JsonProperty("ttl", Order = 3)] public int TTL { get; init; }
    [JsonProperty("auto_start", Order = 4)] public bool AutoStart { get; init; }
}

/*------------------------------------------------------------------------
| IP-Address DTO & Enum                                                  |
------------------------------------------------------------------------*/
public sealed class IpAddressDto
{
    [JsonProperty("type", Order = 1)] public EnumIpType Type { get; init; }
    [JsonProperty("ipv4_address", Order = 2)] public string? IPv4Address { get; init; }
    [JsonProperty("ipv6_address", Order = 3)] public string? IPv6Address { get; init; }
}

/*---------------------------  Rotate DTOs  ---------------------------*/
public sealed class RotateExtensionDto
{
    /*  <Any/> XML 보관용  */
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class RotateDto
{
    [JsonProperty("mode", Order = 1)] public RotateModeDto Mode { get; init; }
    [JsonProperty("degree", Order = 2)] public int? Degree { get; init; }
    [JsonProperty("ext", Order = 3)] public RotateExtensionDto? Extension { get; init; }
}

/*---------------------------  Scene-Orientation DTO  ---------------------------*/
public sealed class SceneOrientationDto
{
    [JsonProperty("mode", Order = 1)] public SceneOrientationModeDto Mode { get; init; }
    [JsonProperty("orientation", Order = 2)] public string? Orientation { get; init; }
}

/*---------------------------  Lens DTOs  ---------------------------*/
public sealed class LensOffsetDto
{
    [JsonProperty("x", Order = 1)] public float? X { get; init; }
    [JsonProperty("y", Order = 2)] public float? Y { get; init; }
}

public sealed class LensProjectionDto
{
    [JsonProperty("angle", Order = 1)] public float Angle { get; init; }
    [JsonProperty("radius", Order = 2)] public float Radius { get; init; }
    [JsonProperty("transmittance", Order = 3)] public float? Transmittance { get; init; }
    [JsonProperty("any", Order = 4)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class LensDescriptionDto
{
    [JsonProperty("offset", Order = 1)] public LensOffsetDto? Offset { get; init; }
    [JsonProperty("projection", Order = 2)] public IReadOnlyList<LensProjectionDto>? Projection { get; init; }
    [JsonProperty("x_factor", Order = 3)] public float XFactor { get; init; }
    [JsonProperty("any", Order = 4)] public IReadOnlyList<string>? Any { get; init; }
    [JsonProperty("focal_length", Order = 5)] public float? FocalLength { get; init; }
}

/*---------------------------  Video-Resolution DTO  ---------------------------*/
public class VideoResolutionDto
{
    [JsonProperty("width", Order = 1)]
    public int Width { get; set; }
    [JsonProperty("height", Order = 2)]
    public int Height { get; set; }
}

/*---------------------------  Video-Rate-Control DTO  ---------------------------*/
public sealed class VideoRateControlDto
{
    [JsonProperty("frame_rate_limit", Order = 1)] public int FrameRateLimit { get; init; }
    [JsonProperty("encoding_interval", Order = 2)] public int EncodingInterval { get; init; }
    [JsonProperty("bitrate_limit", Order = 3)] public int BitrateLimit { get; init; }
}

/*---------------------------  MPEG-4 / H.264 DTOs  ---------------------------*/
public sealed class Mpeg4ConfigDto
{
    [JsonProperty("gov_length", Order = 1)] public int GovLength { get; init; }
    [JsonProperty("profile", Order = 2)] public EnumMpeg4Profile Profile { get; init; }
}

public sealed class H264ConfigDto
{
    [JsonProperty("gov_length", Order = 1)] public int GovLength { get; init; }
    [JsonProperty("profile", Order = 2)] public EnumH264Profile Profile { get; init; }
}

/*---------------------------  Analytics-Engine DTOs  ---------------------------*/
public sealed class AnalyticsEngineConfigDto
{
    [JsonProperty("modules", Order = 1)] public List<ConfigDto> Modules { get; init; } = new();
}

public sealed class RuleEngineConfigDto
{
    [JsonProperty("rules", Order = 1)] public List<ConfigDto> Rules { get; init; } = new();
}

/*---------------------------  Config & Item-List DTOs  ---------------------------*/
public sealed class ConfigDto
{
    [JsonProperty("name", Order = 1)] public string Name { get; init; } = default!;
    [JsonProperty("qname", Order = 2)] public string? TypeQualifiedName { get; init; }
    [JsonProperty("params", Order = 3)] public ItemListDto? Parameters { get; init; }
}

public sealed class ItemListDto
{
    [JsonProperty("simple_items", Order = 1)] public List<SimpleItemDto> SimpleItems { get; init; } = new();
    [JsonProperty("element_items", Order = 2)] public List<ElementItemDto> ElementItems { get; init; } = new();
}

public sealed class SimpleItemDto
{
    [JsonProperty("name", Order = 1)] public string Name { get; init; } = default!;
    [JsonProperty("value", Order = 2)] public string Value { get; init; } = default!;
}

public sealed class ElementItemDto
{
    [JsonProperty("name", Order = 1)] public string Name { get; init; } = default!;
    [JsonProperty("raw_xml", Order = 2)] public string RawXml { get; init; } = default!;
}

/*---------------------------  Analytics-Engine-Input DTO  ---------------------------*/
public sealed class AnalyticsEngineInputDto : ConfigEntityDto
{
    [JsonProperty("src_id", Order = 4)] public SourceIdentificationDto? SourceIdentification { get; init; }
    /* 필요 시 VideoInput / MetadataInput 확장 */
}

public sealed class SourceIdentificationDto
{
    [JsonProperty("name", Order = 1)] public string? Name { get; init; }
    [JsonProperty("tokens", Order = 2)] public List<string> Tokens { get; init; } = new();
}
/*---------------------------  PTZ-FILTER DTO  ---------------------------*/
public sealed class PtzFilterDto
{
    [JsonProperty("status", Order = 1)]
    public bool Status { get; init; }

    [JsonProperty("position", Order = 2)]
    public bool Position { get; init; }

    [JsonProperty("field_of_view", Order = 3)]
    public bool FieldOfView { get; init; }
}

/*---------------------------  PTZ-Vector DTO  ---------------------------*/
public sealed class PtzVectorDto
{
    [JsonProperty("pan_tilt", Order = 1)] public Vector2DDto? PanTilt { get; init; }
    [JsonProperty("zoom", Order = 2)] public Vector1DDto? Zoom { get; init; }
}


/*---------------------------  EVENT-SUBSCRIPTION DTO  ---------------------------*/
public sealed class EventSubscriptionDto
{
    [JsonProperty("filter", Order = 1)]
    public FilterTypeDto? Filter { get; init; }

    [JsonProperty("subscription_policy", Order = 2)]
    public SubscriptionPolicyDto? SubscriptionPolicy { get; init; }
}

/*---------------------------  FILTER-TYPE DTO  ---------------------------*/
public sealed class FilterTypeDto
{
    /*  원본 FilterType 는 <any/> XML 노드만 보유          */
    /*  실전에서는 XPath / Topic 문자열 배열로 수집            */
    [JsonProperty("topics", Order = 1)]
    public List<string> Topics { get; init; } = new();
}

/*---------------------------  SUBSCRIPTION-POLICY DTO  ---------------------------*/
public sealed class SubscriptionPolicyDto
{
    /*  확장 요소만 존재 → Raw XML 들을 문자열로 보관     */
    [JsonProperty("any_elements", Order = 1)]
    public List<string> AnyElements { get; init; } = new();
}

/*---------------------------  Float-Range DTO  ---------------------------*/
public sealed class FloatRangeDto
{
    [JsonProperty("min", Order = 1)] public float Min { get; init; }
    [JsonProperty("max", Order = 2)] public float Max { get; init; }
}

/*---------------------------  Vector DTOs  ---------------------------*/
public sealed class Vector2DDto
{
    [JsonProperty("x", Order = 1)] public float X { get; init; }
    [JsonProperty("y", Order = 2)] public float Y { get; init; }
    [JsonProperty("space", Order = 3)] public string? Space { get; init; }
}

public sealed class Vector1DDto
{
    [JsonProperty("x", Order = 1)] public float X { get; init; }
    [JsonProperty("space", Order = 2)] public string? Space { get; init; }
}

/*---------------------------  Space-Description DTOs  ---------------------------*/
public sealed class Space2DDescriptionDto
{
    [JsonProperty("uri", Order = 1)] public string URI { get; init; } = default!;
    [JsonProperty("x_rng", Order = 2)] public FloatRangeDto XRange { get; init; } = default!;
    [JsonProperty("y_rng", Order = 3)] public FloatRangeDto YRange { get; init; } = default!;
}

public sealed class Space1DDescriptionDto
{
    [JsonProperty("uri", Order = 1)] public string URI { get; init; } = default!;
    [JsonProperty("x_rng", Order = 2)] public FloatRangeDto XRange { get; init; } = default!;
}

/*---------------------------  Limit DTOs  ---------------------------*/
public sealed class PanTiltLimitsDto
{
    [JsonProperty("range", Order = 1)]
    public Space2DDescriptionDto Range { get; init; } = default!;
}

public sealed class ZoomLimitsDto
{
    [JsonProperty("range", Order = 1)]
    public Space1DDescriptionDto Range { get; init; } = default!;
}

/*---------------------------  PTZ-Speed DTO  ---------------------------*/
public sealed class PtzSpeedDto
{
    [JsonProperty("pan_tilt", Order = 1)] public Vector2DDto? PanTilt { get; init; }
    [JsonProperty("zoom", Order = 2)] public Vector1DDto? Zoom { get; init; }
}


/*---------------------------  PT-Control-Direction DTO  ---------------------------*/
public sealed class EFlipDto
{
    [JsonProperty("mode", Order = 1)] public EnumEFlipMode Mode { get; init; }
    [JsonProperty("any", Order = 2)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class ReverseDto
{
    [JsonProperty("mode", Order = 1)] public EnumReverseMode Mode { get; init; }
    [JsonProperty("any", Order = 2)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class PtControlDirExtDto
{
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class PtControlDirectionDto
{
    [JsonProperty("e_flip", Order = 1)] public EFlipDto? EFlip { get; init; }
    [JsonProperty("reverse", Order = 2)] public ReverseDto? Reverse { get; init; }
    [JsonProperty("ext", Order = 3)] public PtControlDirExtDto? Extension { get; init; }
}



/*--------------------------- Preset-Tour Ext ---------------------------*/
public sealed class PtzPresetTourSupportedExtDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Preset-Tour Supported ---------------------------*/
public sealed class PtzPresetTourSupportedDto
{
    [JsonProperty("max_tours", Order = 1)]
    public int MaximumNumberOfPresetTours { get; init; }

    [JsonProperty("operations", Order = 2)]
    public IReadOnlyList<EnumPtzPresetTourOperation> Operations { get; init; } = new List<EnumPtzPresetTourOperation>();

    [JsonProperty("ext", Order = 3)]
    public PtzPresetTourSupportedExtDto? Extension { get; init; }
}

/*--------------------------- PTZ-Spaces Ext ---------------------------*/
public sealed class PtzSpacesExtDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}


/*--------------------------- PTZ-Spaces ---------------------------*/
public sealed class PtzSpacesDto
{
    [JsonProperty("abs_pan_tilt_pos", Order = 1)] public IReadOnlyList<Space2DDescriptionDto>? AbsPanTiltPos { get; init; }
    [JsonProperty("abs_zoom_pos", Order = 2)] public IReadOnlyList<Space1DDescriptionDto>? AbsZoomPos { get; init; }
    [JsonProperty("rel_pan_tilt_trans", Order = 3)] public IReadOnlyList<Space2DDescriptionDto>? RelPanTiltTrans { get; init; }
    [JsonProperty("rel_zoom_trans", Order = 4)] public IReadOnlyList<Space1DDescriptionDto>? RelZoomTrans { get; init; }
    [JsonProperty("cont_pan_tilt_vel", Order = 5)] public IReadOnlyList<Space2DDescriptionDto>? ContPanTiltVel { get; init; }
    [JsonProperty("cont_zoom_vel", Order = 6)] public IReadOnlyList<Space1DDescriptionDto>? ContZoomVel { get; init; }
    [JsonProperty("pan_tilt_speed", Order = 7)] public IReadOnlyList<Space1DDescriptionDto>? PanTiltSpeed { get; init; }
    [JsonProperty("zoom_speed", Order = 8)] public IReadOnlyList<Space1DDescriptionDto>? ZoomSpeed { get; init; }
    [JsonProperty("ext", Order = 9)] public PtzSpacesExtDto? Extension { get; init; }
}

/*--------------------------- Stub-DTO(미정의 타입 대응) -------------*/
public sealed class ProfileExtension2Dto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

public sealed class AudioDecoderConfigDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Imaging-Settings (1.0) ------------------*/
public class ImagingSettingsDto
{
    [JsonProperty("backlight", Order = 1)]
    public BacklightCompDto? BacklightCompensation { get; init; }

    [JsonProperty("brightness", Order = 2)]
    public float? Brightness { get; init; }

    [JsonProperty("saturation", Order = 3)]
    public float? ColorSaturation { get; init; }

    [JsonProperty("contrast", Order = 4)]
    public float? Contrast { get; init; }

    [JsonProperty("exposure", Order = 5)]
    public ExposureDto? Exposure { get; init; }

    [JsonProperty("focus", Order = 6)]
    public FocusConfigDto? Focus { get; init; }

    [JsonProperty("ir_cut", Order = 7)]
    public EnumIrCutFilterMode? IrCutFilter { get; init; }

    [JsonProperty("sharpness", Order = 8)]
    public float? Sharpness { get; init; }

    [JsonProperty("wdr", Order = 9)]
    public WideDynamicRangeDto? WideDynamicRange { get; init; }

    [JsonProperty("white_bal", Order = 10)]
    public WhiteBalanceDto? WhiteBalance { get; init; }

    [JsonProperty("ext", Order = 11)]
    public ImagingSettingsExtDto? Extension { get; init; }
}

/*--------------------------- Imaging-Ext(1.0) ------------------------*/
public sealed class ImagingSettingsExtDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}



/*--------------------------- Video-Source-Ext ------------------------*/
public sealed class VideoSourceExtensionDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }

    [JsonProperty("imaging", Order = 2)]
    public ImagingSettings20Dto? Imaging { get; init; }

    [JsonProperty("ext2", Order = 3)]
    public VideoSourceExtension2Dto? Extension { get; init; }
}
/*--------------------------- Imaging-Settings (2.0) ------------------*/
public sealed class ImagingSettings20Dto : ImagingSettingsDto
{
    /* 모든 필드는 1.0 DTO 에 포함되어 있으므로
       추가 확장은 별도 속성으로 배치 */

    [JsonProperty("image_stab", Order = 50)]
    public ImageStabilizationDto? ImageStabilization { get; init; }

    [JsonProperty("ext20", Order = 51)]
    public ImagingSettingsExt20Dto? Extension20 { get; init; }
}

public sealed class VideoSourceExtension2Dto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Imaging-Ext(2.0) ------------------------*/
public sealed class ImagingSettingsExt20Dto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }

    [JsonProperty("img_stab", Order = 2)]
    public ImageStabilizationDto? ImageStabilization { get; init; }

    [JsonProperty("ext202", Order = 3)]
    public ImagingSettingsExt202Dto? Extension { get; init; }
}

public sealed class ImageStabilizationDto
{
    [JsonProperty("mode", Order = 1)] public EnumImageStabilizationMode? Mode { get; init; }
    [JsonProperty("level", Order = 2)] public float? Level { get; init; }
}


/*--------------------------- Imaging-Ext(2.02) -----------------------*/
public sealed class ImagingSettingsExt202Dto
{
    [JsonProperty("ir_cut_adj", Order = 1)]
    public List<IrCutFilterAutoAdjustmentDto> IrCutFilterAutoAdjustments { get; init; } = new();

    [JsonProperty("ext203", Order = 2)]
    public ImagingSettingsExt203Dto? Extension { get; init; }
}

/*--------------------------- Imaging-Ext(2.03) -----------------------*/
public sealed class ImagingSettingsExt203Dto
{
    [JsonProperty("tone_comp", Order = 1)]
    public ToneCompensationDto? ToneCompensation { get; init; }

    [JsonProperty("defog", Order = 2)]
    public DefoggingDto? Defogging { get; init; }

    [JsonProperty("noise_red", Order = 3)]
    public NoiseReductionDto? NoiseReduction { get; init; }

    [JsonProperty("ext204", Order = 4)]
    public ImagingSettingsExt204Dto? Extension { get; init; }
}

/*--------------------------- Imaging-Ext(2.04) -----------------------*/
public sealed class ImagingSettingsExt204Dto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Ir-Cut-Auto-Adj -------------------------*/
public sealed class IrCutFilterAutoAdjustmentDto
{
    [JsonProperty("type", Order = 1)] public string BoundaryType { get; init; } = default!;
    [JsonProperty("offset", Order = 2)] public float? BoundaryOffset { get; init; }
    [JsonProperty("resp_time", Order = 3)] public string? ResponseTime { get; init; }
    [JsonProperty("ext", Order = 4)] public IrCutAutoAdjExtDto? Extension { get; init; }
}

public sealed class IrCutAutoAdjExtDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Tone-Compensation ----------------------*/
public sealed class ToneCompensationExtDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

public sealed class ToneCompensationDto
{
    [JsonProperty("mode", Order = 1)] public string Mode { get; init; } = default!;
    [JsonProperty("level", Order = 2)] public float? Level { get; init; }
    [JsonProperty("ext", Order = 3)] public ToneCompensationExtDto? Extension { get; init; }
}


/*--------------------------- Defogging ------------------------------*/
public sealed class DefoggingExtDto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

public sealed class DefoggingDto
{
    [JsonProperty("mode", Order = 1)] public string Mode { get; init; } = default!;
    [JsonProperty("level", Order = 2)] public float? Level { get; init; }
    [JsonProperty("ext", Order = 3)] public DefoggingExtDto? Extension { get; init; }
}

/*--------------------------- Noise-Reduction ------------------------*/
public sealed class NoiseReductionDto
{
    [JsonProperty("level", Order = 1)] public float Level { get; init; }
    [JsonProperty("any", Order = 2)] public IReadOnlyList<string>? Any { get; init; }
}


/*--------------------------- Sub-DTOs -------------------------------*/
public sealed class BacklightCompDto
{
    [JsonProperty("mode", Order = 1)] public EnumBacklightCompensationMode Mode { get; init; }
    [JsonProperty("level", Order = 2)] public float Level { get; init; }
}

public sealed class ExposureDto
{
    [JsonProperty("mode", Order = 1)] public EnumExposureMode Mode { get; init; }
    [JsonProperty("prio", Order = 2)] public EnumExposurePriority Priority { get; init; }
    [JsonProperty("win", Order = 3)] public WindowRectDto? Window { get; init; }
    [JsonProperty("min_et", Order = 4)] public float? MinExposureTime { get; init; }
    [JsonProperty("max_et", Order = 5)] public float? MaxExposureTime { get; init; }
    [JsonProperty("min_gn", Order = 6)] public float? MinGain { get; init; }
    [JsonProperty("max_gn", Order = 7)] public float? MaxGain { get; init; }
    [JsonProperty("min_ir", Order = 8)] public float? MinIris { get; init; }
    [JsonProperty("max_ir", Order = 9)] public float? MaxIris { get; init; }
    [JsonProperty("exp_t", Order = 10)] public float? ExposureTime { get; init; }
    [JsonProperty("gain", Order = 11)] public float? Gain { get; init; }
    [JsonProperty("iris", Order = 12)] public float? Iris { get; init; }
}

/*--------------------------- Window (left-right-top-bottom) ----------*/
public sealed class WindowRectDto
{
    [JsonProperty("left", Order = 1)] public float? Left { get; init; }
    [JsonProperty("top", Order = 2)] public float? Top { get; init; }
    [JsonProperty("right", Order = 3)] public float? Right { get; init; }
    [JsonProperty("bottom", Order = 4)] public float? Bottom { get; init; }
}

public sealed class FocusConfigDto
{
    [JsonProperty("mode", Order = 1)] public EnumAutoFocusMode Mode { get; init; }
    [JsonProperty("def_speed", Order = 2)] public float? DefaultSpeed { get; init; }
    [JsonProperty("near_limit", Order = 3)] public float? NearLimit { get; init; }
    [JsonProperty("far_limit", Order = 4)] public float? FarLimit { get; init; }
}

public sealed class WideDynamicRangeDto
{
    [JsonProperty("mode", Order = 1)] public EnumWideDynamicMode Mode { get; init; }
    [JsonProperty("level", Order = 2)] public float? Level { get; init; }
}

public sealed class WhiteBalanceDto
{
    [JsonProperty("mode", Order = 1)] public EnumWhiteBalanceMode Mode { get; init; }
    [JsonProperty("cr_gain", Order = 2)] public float? CrGain { get; init; }
    [JsonProperty("cb_gain", Order = 3)] public float? CbGain { get; init; }
}


/*--------------------------- Focus-Status-Ext DTO ---------------------------*/
public sealed class FocusStatusExtDto
{
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Focus-Status DTO ---------------------------*/
public sealed class FocusStatusDto
{
    [JsonProperty("position", Order = 1)] public float Position { get; init; }
    [JsonProperty("move_status", Order = 2)] public EnumFocusMoveStatus MoveStatus { get; init; }
    [JsonProperty("error", Order = 3)] public string? Error { get; init; }
    [JsonProperty("ext", Order = 4)] public FocusStatusExtDto? Extension { get; init; }
}

/*--------------------------- Imaging-Status-Ext DTO ---------------------------*/
public sealed class ImagingStatusExtDto
{
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- Imaging-Status DTO ---------------------------*/
public sealed class ImagingStatusDto
{
    [JsonProperty("focus_status", Order = 1)] public FocusStatusDto FocusStatus { get; init; } = default!;
    [JsonProperty("ext", Order = 2)] public ImagingStatusExtDto? Extension { get; init; }
}

/*--------------------------- Continuous-Focus-Options DTO ---------------------------*/
public sealed class ContinuousFocusOptionsDto
{
    [JsonProperty("speed", Order = 1)] public FloatRangeDto Speed { get; init; } = default!;
}

/*--------------------------- Relative-Focus-Options DTO ---------------------------*/
public sealed class RelativeFocusOptionsDto
{
    [JsonProperty("distance", Order = 1)] public FloatRangeDto Distance { get; init; } = default!;
    [JsonProperty("speed", Order = 2)] public FloatRangeDto Speed { get; init; } = default!;
}

/*--------------------------- Absolute-Focus-Options DTO ---------------------------*/
public sealed class AbsoluteFocusOptionsDto
{
    [JsonProperty("position", Order = 1)] public FloatRangeDto Position { get; init; } = default!;
    [JsonProperty("speed", Order = 2)] public FloatRangeDto Speed { get; init; } = default!;
}

/*--------------------------- Move-Options-20 DTO ---------------------------*/
public sealed class MoveOptions20Dto
{
    [JsonProperty("absolute", Order = 1)] public AbsoluteFocusOptionsDto? Absolute { get; init; }
    [JsonProperty("relative", Order = 2)] public RelativeFocusOptionsDto? Relative { get; init; }
    [JsonProperty("continuous", Order = 3)] public ContinuousFocusOptionsDto? Continuous { get; init; }
}

/*--------------------------- Continuous-Focus DTO ---------------------------*/
public sealed class ContinuousFocusDto
{
    [JsonProperty("speed", Order = 1)] public float Speed { get; init; }
}

/*--------------------------- Relative-Focus DTO ---------------------------*/
public sealed class RelativeFocusDto
{
    [JsonProperty("distance", Order = 1)] public float Distance { get; init; }
    [JsonProperty("speed", Order = 2)] public float? Speed { get; init; }
}

/*--------------------------- Absolute-Focus DTO ---------------------------*/
public sealed class AbsoluteFocusDto
{
    [JsonProperty("position", Order = 1)] public float Position { get; init; }
    [JsonProperty("speed", Order = 2)] public float? Speed { get; init; }
}

/*--------------------------- Focus-Move DTO ---------------------------*/
public sealed class FocusMoveDto
{
    [JsonProperty("absolute", Order = 1)] public AbsoluteFocusDto? Absolute { get; init; }
    [JsonProperty("relative", Order = 2)] public RelativeFocusDto? Relative { get; init; }
    [JsonProperty("continuous", Order = 3)] public ContinuousFocusDto? Continuous { get; init; }
}
