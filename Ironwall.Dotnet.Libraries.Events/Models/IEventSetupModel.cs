namespace Ironwall.Dotnet.Libraries.Events.Models;

public interface IEventSetupModel
{
    /// <summary>탐지 이벤트 자동조치보고 활성 여부(= "탐지 이벤트 해제" 토글).</summary>
    bool IsAutoEventDiscard { get; set; }
    bool IsSound { get; set; }
    int LengthMaxEventPrev { get; set; }
    int LengthMinEventPrev { get; set; }
    /// <summary>탐지 이벤트 자동조치보고 타임아웃(초)(= "탐지 이벤트 해제" 초).</summary>
    int TimeDiscardSec { get; set; }
    int TimeDurationSound { get; set; }

    /// <summary>장애 이벤트 자동조치보고 활성 여부(= "장애 이벤트 해제" 토글). 탐지의 <see cref="IsAutoEventDiscard"/>와 독립 — false면 장애 자동조치보고 미발송.</summary>
    bool IsMalfunctionAutoEventDiscard { get; set; }
    /// <summary>장애 이벤트 자동조치보고 타임아웃(초)(= "장애 이벤트 해제" 초). 탐지의 <see cref="TimeDiscardSec"/>와 독립.</summary>
    int MalfunctionTimeDiscardSec { get; set; }
}