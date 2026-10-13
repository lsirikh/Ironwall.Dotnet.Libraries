using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      : 이벤트 패널의 운영 이벤트 카드 (operation-event-card PRD §2 · §3)
                      조치 대상이 아니다 — 조치보고 단추 없이 [닫기] 로만 치운다. 글은 이벤트 콘솔 '운영' 레일과 같은 OperationEventText.
       Created By   : Claude
       Created On   : 2026-10-13
       Department   : SW Team
       Company      : Sensorway Co., Ltd.
    ****************************************************************************/
    public class OperationEventCardViewModel : EventCardViewModel<OperationEventModel>
    {
        #region - Ctors -
        public OperationEventCardViewModel(OperationEventModel model)
            : base(model)
        {
        }
        #endregion
        #region - Overrides -
        protected override Task CloseDialog() => Task.CompletedTask;
        #endregion
        #region - Properties -
        /// <summary>사유 원문(<c>GATE_OPEN</c> …).</summary>
        public string Reason => _model.Reason;

        /// <summary>사유 글("통문 열림"). 모르는 사유는 원문.</summary>
        public string ReasonText => OperationEventText.ReasonText(_model.Reason);

        /// <summary>심각도 글("경고"). 없으면 "—".</summary>
        public string SeverityText => OperationEventText.SeverityText(_model.Severity);

        /// <summary>경고 이상(WARNING · CRITICAL)인가.</summary>
        public bool IsWarning => OperationEventText.IsWarningOrAbove(_model.Severity);

        /// <summary>개폐 사유인가 — 머리 아이콘(문 · 정보)을 가른다.</summary>
        public bool IsDoor => OperationEventText.IsDoorReason(_model.Reason);

        /// <summary>상세 한 줄. 없으면 "—".</summary>
        public string DetailText => string.IsNullOrWhiteSpace(_model.DetailText) ? "—" : _model.DetailText!;

        /// <summary>[닫기] 단추 자동화 식별자 — <c>{AutomationKey}.Close</c>.</summary>
        public string CloseAutomationKey => AutomationKey + ".Close";
        #endregion
    }
}
