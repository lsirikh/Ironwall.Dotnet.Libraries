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
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/4/2025 10:11:00 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DetectionReportDialogViewModel : EventReportDialogViewModel
    {
        #region - Ctors -
        public DetectionReportDialogViewModel()
        {
        }

        public DetectionReportDialogViewModel(IEventAggregator eventAggregator, ILogService log) 
            : base(eventAggregator, log)
        {
        }
        #endregion
        #region - 조치보고 권한 게이팅 (서버 계약 events:edit) -
        // IoC.Get lazy — 미등록 시 null → 전체허용 폴백
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
            // 보내는 중 · 이미 다른 곳에서 조치됨 — 두 번째 조치를 만들지 않는다(WP-1 ③ · ⑬). 단추도 CanClickOk 로 꺼져 있다.
            if (!CanClickOk) return;

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

            var user = ActionReportRules.FormatActor(_user);   // 보고자 표기 한 모양 — Username(EmployeeNumber)
            if (!(Model is DetectionEventCardViewModel vm)) return;

            // 내용 게이트 — '기타' + 빈 메모는 서버 422(content min_length=1). 보내기 전에 이유를 알린다.
            var content = (SelectableItemViewModel?.Name == "기타") ? Memo : SelectableItemViewModel?.Name;
            var invalid = ActionReportRules.ValidateContent(content) ?? ActionReportRules.ValidateUser(user);   // user 1~100자(G43)
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
            ActionSendResult result;
            SetSending(true);
            try
            {
                result = await vm.SendActionDetailed(content, user);
            }
            finally
            {
                SetSending(false);
            }

            if (!result.CanCloseDialog)
            {
                OnSendFailed();   // 보내는 동안 남이 조치했으면 [확인]을 다시 켜지 않는다(B10)
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

            var card = (eventModel as DetectionEventCardViewModel)!;
            var model = card.Model as IDetectionEventModel;
            var viewModel = new DetectionEventViewModel(model!);
            var list = new List<DetectionEventViewModel>() { viewModel };
            SelectedItemEditor = new DetectionSelectionViewModel(list) { IsEditable = false }; // 조치보고=읽기 전용(스크롤은 유지)
            (SelectedItemEditor as DetectionSelectionViewModel)!.RefreshAll();
            WatchThumbnail(card);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            WatchThumbnail(null);
            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - 사진 늦게 도착 -
        /// <summary>
        /// 사진은 탐지 뒤 SYNC_DETECTION 재조회로 늦게 오는 일이 흔하다 — 카드는 그때 같은 모델의 Thumbnail 을 채우고 알린다.
        /// 창이 열려 있는 동안 그 알림을 받아 사진 칸을 다시 읽힌다(연 순간 '사진 없음' 으로 굳지 않게).
        /// 다른 이벤트로 다시 열거나 창을 닫으면 앞 카드 구독을 끊는다. 호출 스레드: UI.
        /// </summary>
        private void WatchThumbnail(DetectionEventCardViewModel? card)
        {
            if (_thumbnailCard != null) _thumbnailCard.PropertyChanged -= OnCardPropertyChanged;
            _thumbnailCard = card;
            if (_thumbnailCard != null) _thumbnailCard.PropertyChanged += OnCardPropertyChanged;
        }

        private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (nameof(DetectionEventCardViewModel.HasThumbnail) or nameof(DetectionEventCardViewModel.ThumbnailUri))) return;
            (SelectedItemEditor as DetectionSelectionViewModel)?.RefreshThumbnail();
        }

        private DetectionEventCardViewModel? _thumbnailCard;
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