using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Events;

public interface IDetectionEventModel : IExEventModel
{
    EnumDetectionType Result { get; set; }

    /// <summary>
    /// 탐지 신호 크기(detail.signal). null=미제공(레거시), 0=AI_DETECT(의미 크기는 confidence).
    /// </summary>
    int? Signal { get; set; }

    /// <summary>AI 추론 모델명(detail.model, 예: "yolov8n").</summary>
    string? AiModel { get; set; }

    /// <summary>AI 추론 소요(ms, detail.inference_ms).</summary>
    int? InferenceMs { get; set; }

    /// <summary>이벤트 썸네일 URL(detail.thumbnail).</summary>
    string? Thumbnail { get; set; }

    /// <summary>AI 탐지 객체 목록(detail.objects[] — label/confidence/bbox).</summary>
    System.Collections.Generic.List<DetectionObjectModel>? Objects { get; set; }
}