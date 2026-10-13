using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;

/****************************************************************************
   Purpose      : 운영 이벤트 조치보고 창(operation-event-card PRD v1.1 §3) — 탐지 · 장애 창과 같은 틀 · 같은 문구 목록, 사진 없음.
                  보내는 길은 운영 카드의 SendActionDetailed(서버 저장 성공 뒤에만 카드 종결 · NATS ACTION_REPORT).
                  호스트는 OpenEventReportDialogMessageModel{EventType="OPERATION"} 을 이 뷰모델로 띄운다.
   Created By   : Claude
   Created On   : 2026-10-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class OperationReportDialogViewModel : EventReportDialogViewModel
{
    /// <summary>창을 여는 메시지의 종류 값.</summary>
    public const string EventType = "OPERATION";

    public OperationReportDialogViewModel() : base()
    {
    }

    public OperationReportDialogViewModel(IEventAggregator eventAggregator, ILogService log)
        : base(eventAggregator, log)
    {
    }

    /// <summary>창이 든 운영 카드(장비 · 사유 · 심각도 · 상세를 읽기 전용으로 보인다).</summary>
    public OperationEventCardViewModel? Operation => Model as OperationEventCardViewModel;

    public override async void ClickOk()
    {
        // 보내는 중 · 이미 다른 곳에서 조치됨 — 두 번째 조치를 만들지 않는다. 단추도 CanClickOk 로 꺼져 있다.
        if (!CanClickOk) return;

        if (!ActionReportRules.CanReport(ResolvePermissionService()))
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "권한 없음",
                Explain = ActionReportRules.NO_PERMISSION_TEXT
            });
            return;
        }

        if (Operation is not { } vm) return;

        var content = (SelectableItemViewModel?.Name == "기타") ? Memo : SelectableItemViewModel?.Name;
        var invalid = ActionReportRules.ValidateContent(content);
        if (invalid is not null)
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "조치 내용 확인",
                Explain = invalid
            });
            return;
        }

        ActionSendResult result;
        SetSending(true);
        try
        {
            result = await vm.SendActionDetailed(content, ActionReportRules.FormatActor(_user));
        }
        finally
        {
            SetSending(false);
        }

        if (!result.CanCloseDialog)
        {
            OnSendFailed();
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "조치보고 실패",
                Explain = string.IsNullOrWhiteSpace(result.Reason)
                    ? "조치보고를 저장하지 못했습니다. 연결 상태를 확인한 뒤 다시 시도하세요."
                    : $"조치보고를 저장하지 못했습니다. {result.Reason}"
            });
            return;
        }

        await _eventAggregator.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
    }

    public override async void ClickCancel()
    {
        await _eventAggregator.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
    }

    public override void UpdateData(EventCardBaseViewModel eventModel, IAccountModel user)
    {
        base.UpdateData(eventModel, user);
        NotifyOfPropertyChange(nameof(Operation));
    }

    private IPermissionService? ResolvePermissionService()
    {
        try { return IoC.Get<IPermissionService>(); }
        catch { return null; }
    }
}
