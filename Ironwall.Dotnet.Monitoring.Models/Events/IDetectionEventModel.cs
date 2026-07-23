using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Events;

public interface IDetectionEventModel : IExEventModel
{
    EnumDetectionType Result { get; set; }

    /// <summary>
    /// 탐지 신호 크기(detail.signal). null=미제공(레거시), 0=AI_DETECT(의미 크기는 confidence).
    /// </summary>
    int? Signal { get; set; }
}