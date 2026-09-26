using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Security.Principal;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/4/2025 10:12:36 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class MalfunctionReportDialogViewModel : EventReportDialogViewModel
    {
        #region - Ctors -
        public MalfunctionReportDialogViewModel() : base()
        {
        }

        public MalfunctionReportDialogViewModel(IEventAggregator eventAggregator, ILogService log) 
            : base(eventAggregator, log)
        {
        }
        #endregion
        #region - 조치보고 권한 게이팅 (서버 계약 events:edit) -
        private IPermissionService? _permissionService;
        private bool _permissionResolved;
        private IPermissionService? ResolvePermissionService()
        {
            if (_permissionResolved) return _permissionService;
            try { _permissionService = IoC.Get<IPermissionService>(); _permissionResolved = _permissionService != null; }   // 성공 시에만 캐시(영구 fail-open 방지)
            catch { _permissionService = null; }
            return _permissionService;
        }
        // 조치보고 = POST /events/actions = 서버 events:edit(v6.3.2 · 8.0.x 동일). control 이 아니다 — ActionReportRules 참조.
        private bool CanReportAction() => ActionReportRules.CanReport(ResolvePermissionService());
        #endregion
        #region - Implementation of Interface -
        public override async void ClickOk()
        {
            // 권한 게이트 — SendAction 호출 이전 검사. 서버가 403 을 줄 요청은 보내지 않는다.
            if (!CanReportAction())
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "권한 없음",
                    Explain = ActionReportRules.NO_PERMISSION_TEXT
                });
                return;
            }

            var user = $"{_user?.Username}({_user?.EmployeeNumber})";
            if (!(Model is MalfunctionEventCardViewModel vm)) return;

            // 내용 게이트 — '기타' + 빈 메모는 서버 422(content min_length=1). 보내기 전에 이유를 알린다.
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

            // (EA3) SendAction 성공 여부 확인 — 실패 시 다이얼로그 유지 + 오류 알림(성공 오인식 방지)
            //   실패하면 서버가 준 까닭을 그대로 보인다 — "네트워크를 확인" 한 줄은 403·422 를 가렸다.
            var result = await vm.SendActionDetailed(content, user);

            if (!result.CanCloseDialog)
            {
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
        #endregion
        #region - Overrides -
        public override void UpdateData(EventCardBaseViewModel eventModel, IAccountModel user)
        {
            base.UpdateData(eventModel, user);

            var model = (eventModel as MalfunctionEventCardViewModel)!.Model as IMalfunctionEventModel;
            var viewModel = new MalfunctionEventViewModel(model!);
            var list = new List<MalfunctionEventViewModel>() { viewModel };
            SelectedItemEditor = new MalfunctionSelectionViewModel(list) { IsEditable = false }; // 조치보고=읽기 전용(스크롤은 유지)
            (SelectedItemEditor as MalfunctionSelectionViewModel)!.RefreshAll();
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        #endregion



    }
}