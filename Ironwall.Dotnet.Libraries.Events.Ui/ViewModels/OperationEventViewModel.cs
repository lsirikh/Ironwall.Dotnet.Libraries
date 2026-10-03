using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;

/// <summary>
/// 운영 이벤트 목록의 한 행 — 함체 · 통문 개폐, 함체 환경 임계 경보. 조회 전용(서버가 스스로 적재한다).
/// </summary>
public class OperationEventViewModel : ExEventViewModel
{
    public OperationEventViewModel(OperationEventModel model) : base(model)
    {
        Operation = model;
    }

    /// <summary>원본 모델(사유 · 심각도 · 상세 원문).</summary>
    public OperationEventModel Operation { get; }

    /// <summary>사유 원문(<c>GATE_OPEN</c> …).</summary>
    public string Reason => Operation.Reason;

    /// <summary>사유 글자("통문 열림"). 모르는 값은 원문.</summary>
    public string ReasonText => OperationEventText.ReasonText(Operation.Reason);

    /// <summary>심각도 원문(<c>WARNING</c> …).</summary>
    public string? Severity => Operation.Severity;

    /// <summary>심각도 글자("경고"). 없으면 "—".</summary>
    public string SeverityText => OperationEventText.SeverityText(Operation.Severity);

    /// <summary>경고 이상인가 — 행 표시 · '경고 이상' 칩.</summary>
    public bool IsWarning => OperationEventText.IsWarningOrAbove(Operation.Severity);

    /// <summary>상세 한 줄. 없으면 "—".</summary>
    public string DetailText => string.IsNullOrWhiteSpace(Operation.DetailText) ? "—" : Operation.DetailText!;
}
