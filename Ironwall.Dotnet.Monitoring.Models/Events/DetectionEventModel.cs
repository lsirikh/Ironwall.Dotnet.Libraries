using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:45:20 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionEventModel : ExEventModel, IDetectionEventModel
{
    #region - Ctors -
    public DetectionEventModel()
    {
    }

    public DetectionEventModel(IExEventModel model): base(model)
    {
        
    }

    public DetectionEventModel(IDetectionEventModel model) : base(model)
    {
        Result = model.Result;
        Signal = model.Signal;
        AiModel = model.AiModel;
        InferenceMs = model.InferenceMs;
        Thumbnail = model.Thumbnail;
        Objects = model.Objects;
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
    [JsonProperty("result", Order = 6)]
    public EnumDetectionType Result { get; set; }

    /// <summary>탐지 신호 크기(detail.signal). null=미제공, 0=AI_DETECT.</summary>
    [JsonProperty("signal", Order = 7, NullValueHandling = NullValueHandling.Ignore)]
    public int? Signal { get; set; }

    /// <summary>AI 추론 모델명(detail.model).</summary>
    [JsonProperty("ai_model", Order = 8, NullValueHandling = NullValueHandling.Ignore)]
    public string? AiModel { get; set; }

    /// <summary>AI 추론 소요 ms(detail.inference_ms).</summary>
    [JsonProperty("inference_ms", Order = 9, NullValueHandling = NullValueHandling.Ignore)]
    public int? InferenceMs { get; set; }

    /// <summary>이벤트 썸네일 URL(detail.thumbnail).</summary>
    [JsonProperty("thumbnail", Order = 10, NullValueHandling = NullValueHandling.Ignore)]
    public string? Thumbnail { get; set; }

    /// <summary>AI 탐지 객체 목록(detail.objects[]).</summary>
    [JsonProperty("objects", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public System.Collections.Generic.List<DetectionObjectModel>? Objects { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}