using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System;
using System.Linq;

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
        FrameWidth = model.FrameWidth;
        FrameHeight = model.FrameHeight;
        // 헬퍼(ConvertObjectsFromDto/BuildDetectionDetail)와 대칭 — 깊은 복사(원소·Bbox 공유 방지)
        Objects = model.Objects?.Select(o => new DetectionObjectModel
        {
            Label = o.Label,
            Confidence = o.Confidence,
            Bbox = o.Bbox?.ToList(),
            Thumbnail = o.Thumbnail
        }).ToList();
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

    /// <summary>AI 추론 프레임 폭(px, detail.frame_width) — bbox 스케일 기준.</summary>
    [JsonProperty("frame_width", Order = 11, NullValueHandling = NullValueHandling.Ignore)]
    public int? FrameWidth { get; set; }

    /// <summary>AI 추론 프레임 높이(px, detail.frame_height).</summary>
    [JsonProperty("frame_height", Order = 12, NullValueHandling = NullValueHandling.Ignore)]
    public int? FrameHeight { get; set; }

    /// <summary>AI 탐지 객체 목록(detail.objects[]).</summary>
    [JsonProperty("objects", Order = 13, NullValueHandling = NullValueHandling.Ignore)]
    public System.Collections.Generic.List<DetectionObjectModel>? Objects { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}