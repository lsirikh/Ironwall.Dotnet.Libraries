using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
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
        #region - FR-EN-10 권한 게이팅 (events:control) -
        private IPermissionService? _permissionService;
        private bool _permissionResolved;
        private IPermissionService? ResolvePermissionService()
        {
            if (_permissionResolved) return _permissionService;
            try { _permissionService = IoC.Get<IPermissionService>(); _permissionResolved = _permissionService != null; }   // 성공 시에만 캐시(영구 fail-open 방지)
            catch { _permissionService = null; }
            return _permissionService;
        }
        private bool CanCtrlEvents() => ResolvePermissionService()?.CanControl("events") ?? true;
        #endregion
        #region - Implementation of Interface -
        public override async void ClickOk()
        {
            // FR-EN-10 ACK 게이트 (CanControl) — SendAction 호출 이전 검사
            if (!CanCtrlEvents())
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "권한 없음",
                    Explain = "조치보고 권한이 없습니다."
                });
                return;
            }

            var user = $"{_user?.Username}({_user?.EmployeeNumber})";
            if (!(Model is MalfunctionEventCardViewModel vm)) return;

            // (EA3) SendAction 성공 여부 확인 — 실패 시 다이얼로그 유지 + 오류 알림(성공 오인식 방지)
            bool ok = (SelectableItemViewModel?.Name == "기타")
                ? await vm.SendAction(Memo, user)
                : await vm.SendAction(SelectableItemViewModel?.Name, user);

            if (!ok)
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "조치보고 실패",
                    Explain = "조치보고 저장에 실패했습니다. 네트워크/서버 상태를 확인 후 다시 시도하세요."
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
            SelectedItemEditor = new MalfunctionSelectionViewModel(list);
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