using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      : 이벤트 패널의 운영 이벤트 카드 (operation-event-card PRD v1.1 §2 · §3)
                      서버 규격(operation-event PRD FR-07)대로 조치보고 대상이다 — [조치보고] 는 탐지 · 장애 카드와 같은 길
                      (POST /api/events/actions, from_event_id = 운영 이벤트 id → 성공 뒤에만 카드 종결 · NATS ACTION_REPORT).
                      [닫기] 는 서버 호출 없는 치우기로 남는다. 글은 이벤트 콘솔 '운영' 레일과 같은 OperationEventText.
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

        public OperationEventCardViewModel(IEventAggregator ea, ILogService log, OperationEventModel model)
            : base(ea, log, model)
        {
        }
        #endregion
        #region - Overrides -
        protected override Task CloseDialog() => Task.CompletedTask;

        /// <summary>역사적 입구 — 다이얼로그가 부른다. 진짜 결말은 <see cref="SendActionDetailed"/> 가 안다.</summary>
        public override async Task<bool> SendAction(string? content, string? idUser)
            => (await SendActionDetailed(content, idUser).ConfigureAwait(true)).CanCloseDialog;

        /// <summary>
        /// 운영 이벤트 조치보고 — 장애 카드와 같은 정본 경로. 자물쇠는 ("operation", 번호) 라 같은 번호의 탐지 · 장애를 막지 않는다.
        /// 서버가 저장했을 때만 알림(<see cref="OperationReportedMessageModel"/>) · NATS 발행 요청을 낸다 — 실패하면 카드는 그대로 남는다.
        /// </summary>
        public override async Task<ActionSendResult> SendActionDetailed(string? content, string? idUser, CancellationToken token = default)
        {
            IdUser = string.IsNullOrWhiteSpace(idUser) ? ActionReportRules.FormatActor(IoC.Get<IAccountModel>()) : idUser.Trim();
            Contents = content ?? "자동 조치보고";

            var guard = IoC.Get<IActionReportGuard>();
            var keyed = guard as IKeyedActionReportGuard;
            var entered = keyed is not null ? keyed.TryEnter(EventCardKind.Operation, Model.Id) : guard.TryEnter(Model.Id);
            if (!entered)
            {
                _log?.Info($"[ACTION_REPORT] Operation Event({Model.Id}) 조치보고 진행 중 — 수동 중복 스킵");
                return ActionSendResult.GuardSkipped("다른 곳에서 같은 이벤트를 조치보고하는 중이라 보내지 않았습니다.");
            }
            try
            {
                if (token.IsCancellationRequested)
                    return new ActionSendResult(ActionSendOutcome.Cancelled, "중단했습니다. 보냈는지는 목록을 [새로 불러오기] 해 확인하세요.");

                var dto = new ActionEventCreateDto
                {
                    User = IdUser ?? string.Empty,
                    Content = Contents,
                    FromEventId = Model.Id
                };
                var response = await IoC.Get<IEventApiService>().CreateActionEventAsync(ActionReportRules.ApplyCreatedAtPolicy(dto), token);
                if (!response.Success)
                {
                    _log?.Error($"[ACTION_REPORT] Operation INSERT 실패: {response.Message}");
                    return ActionSendResult.ServerRefused(response.StatusCode);
                }

                await _eventAggregator.PublishOnCurrentThreadAsync(new OperationReportedMessageModel(this, Contents, IdUser));
                await _eventAggregator.PublishOnBackgroundThreadAsync(
                    ActionReportMessages.Create(response, Model.Id, EnumEventType.Operation, Contents, IdUser, Model));

                _model.Status = EnumTrueFalse.True;   // 서버는 조치가 생기면 action_reported 를 참으로 다시 계산한다(장애 카드와 같다)

                await base.SendAction(content, idUser);
                return ActionSendResult.Created(response.Data?.Id ?? 0);
            }
            catch (OperationCanceledException)
            {
                return new ActionSendResult(ActionSendOutcome.Cancelled, "중단했습니다. 보냈는지는 목록을 [새로 불러오기] 해 확인하세요.");
            }
            finally
            {
                if (keyed is not null) keyed.Exit(EventCardKind.Operation, Model.Id); else guard.Exit(Model.Id);
            }
        }

        /// <summary>
        /// SYNC_OPERATION_EVENT{UPDATED} 다시 읽기 반영(G46) — 운영자가 바꾼 심각도 · 상세 · 조치 상태만 고친다(새 이벤트가 아니다).
        /// </summary>
        public void ApplyServerUpdate(OperationEventModel updated)
        {
            if (updated is null) return;
            _model.Severity = updated.Severity;
            _model.DetailText = updated.DetailText;
            _model.Status = updated.Status;
            NotifyOfPropertyChange(nameof(SeverityText));
            NotifyOfPropertyChange(nameof(IsWarning));
            NotifyOfPropertyChange(nameof(DetailText));
        }
        #endregion
        #region - Properties -
        /// <summary>사유 원문(<c>GATE_OPEN</c> …).</summary>
        public string Reason => _model.Reason;

        /// <summary>사유 글("통문 열림"). 모르는 사유는 원문.</summary>
        public string ReasonText => OperationEventText.ReasonText(_model.Reason, _model.CategoryDevice);

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

        /// <summary>
        /// 서버가 운영 이벤트 조치를 받는 판본인가(7.0 이상 — 운영 경로가 있는 판본). 아니면 [조치보고] 단추를 숨기고
        /// [전체 조치보고] 도 서버에 보내지 않고 치우기만 한다. 계약 프로브가 없으면(미구성 · 시험) 보수적으로 아니다 —
        /// EventApiService 의 기본값(6.3)과 같다.
        /// </summary>
        public bool IsReportSupported
        {
            get
            {
                try { return IoC.Get<IServerContractProbe>() is { Contract: >= EnumServerContract.V7_0 }; }
                catch { return false; }
            }
        }
        #endregion
    }
}
