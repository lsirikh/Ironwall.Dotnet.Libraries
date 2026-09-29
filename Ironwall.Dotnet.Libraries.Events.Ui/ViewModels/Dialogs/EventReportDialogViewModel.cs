using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/4/2025 9:31:06 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class EventReportDialogViewModel : BasePanelViewModel, IHandle<ActionReportTemplatesChangedMessage>
                                                     , IHandle<Ironwall.Dotnet.Libraries.Events.Ui.Models.RemoteActionReportedMessage>
    {
        #region - Ctors -
        public EventReportDialogViewModel()
        {
        }

        public EventReportDialogViewModel(IEventAggregator eventAggregator, ILogService log)
            : base(eventAggregator, log)
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            IninializeDialog();
            return base.OnActivateAsync(cancellationToken);
        }
        #endregion
        #region - Binding Methods -
        public abstract void ClickOk();
        public abstract void ClickCancel();
        #endregion
        #region - Processes -
        public virtual void UpdateData(EventCardBaseViewModel eventModel, IAccountModel user)
        {
            if (eventModel != null) 
            {
                _model = eventModel;
            }

            if(user != null)
            {
                _user = user;
            }

            // 새 이벤트로 창을 연다 — 앞 이벤트의 '보내는 중' · '이미 조치됨' 표시를 넘겨받지 않는다.
            _isSending = false;
            _isHandledElsewhere = false;
            NotifyReportState();

            Refresh();
        }

        /// <summary>
        /// 원격 조치보고(다른 운영자 · 다른 GIS)가 이 창의 이벤트를 이미 조치했다 — [확인]을 끄고 까닭을 보인다(WP-1 ③).
        /// 모른 채 [확인]을 누르면 서버에 같은 이벤트의 두 번째 조치가 생긴다. 보내는 중(자기 조치의 메아리일 수 있다)이면 무시한다.
        /// </summary>
        public Task HandleAsync(Ironwall.Dotnet.Libraries.Events.Ui.Models.RemoteActionReportedMessage message, CancellationToken cancellationToken)
        {
            if (message is null || !IsActive || _isSending || _model is null) return Task.CompletedTask;
            if (_model.Model?.Id != message.EventId
                || Ironwall.Dotnet.Libraries.Events.Ui.Helpers.EventCardKind.Of(_model) != message.Kind) return Task.CompletedTask;

            _isHandledElsewhere = true;
            _log?.Info($"[{GetType().Name}] 다른 운영자가 이미 조치함 — [확인] 끔: {message.Kind} {message.EventId}");
            NotifyReportState();
            return Task.CompletedTask;
        }

        /// <summary>보내기 시작 · 끝 — [확인] 을 끄고 켠다(두 번 눌러 두 건 보내지 않게, WP-1 ⑬).</summary>
        protected void SetSending(bool sending)
        {
            if (_isSending == sending) return;
            _isSending = sending;
            NotifyReportState();
        }

        private void NotifyReportState()
        {
            NotifyOfPropertyChange(nameof(IsSending));
            NotifyOfPropertyChange(nameof(IsHandledElsewhere));
            NotifyOfPropertyChange(nameof(CanClickOk));
            NotifyOfPropertyChange(nameof(DialogNotice));
        }

        /// <summary>
        /// 창을 열 때 — 문구를 곧바로 채우고(마지막으로 받은 목록 · 없으면 기본 문구) 뒤이어 서버의
        /// 조치보고 문구 관리 목록을 다시 읽어 바뀌었으면 갈아 끼운다(조치 트레이와 같은 목록 — 완성도 감사 E-6 #2).
        /// </summary>
        private void IninializeDialog()
        {
            var source = ResolvePhraseSource();
            BuildItems((source?.LastKnown ?? new ActionReportPhraseSet(ActionReportPhraseSource.Fallback, false)).Phrases, keepSelection: false);
            Memo = string.Empty;
            Refresh();

            if (source is not null) _ = RefreshPhrasesAsync(source);
        }

        /// <summary>
        /// 창이 떠 있는 동안 다른 곳에서 문구 목록이 바뀌었다(서버 <c>SYNC_ACTION_REPORT_TEMPLATE</c>) — 다시 읽어 갈아 끼운다.
        /// 단, 사람이 고른 문구가 새 목록에서 빠졌으면 <b>갈아 끼우지 않는다</b>(고른 것을 말없이 다른 문구로 바꾸지 않는다).
        /// </summary>
        /// <remarks>기다리지 않고 돌아간다 — 호스트의 NATS 처리 줄이 서버 왕복을 기다리지 않도록.</remarks>
        public Task HandleAsync(ActionReportTemplatesChangedMessage message, CancellationToken cancellationToken)
        {
            if (!IsActive) return Task.CompletedTask;
            var source = ResolvePhraseSource();
            if (source is not null) PhraseRefreshTask = RefreshPhrasesAsync(source, protectSelection: true);
            return Task.CompletedTask;
        }

        /// <summary>가장 최근 알림이 시작한 문구 다시 읽기(시험용).</summary>
        internal Task PhraseRefreshTask { get; private set; } = Task.CompletedTask;

        private async Task RefreshPhrasesAsync(IActionReportPhraseSource source, bool protectSelection = false)
        {
            try
            {
                var set = await source.LoadAsync().ConfigureAwait(true);
                var current = CollectionActionItem?.Select(i => i.Name).ToList() ?? new List<string?>();
                if (!IsActive || set.Phrases.SequenceEqual(current)) return;
                if (protectSelection && SelectableItemViewModel is { } chosen
                    && !ReferenceEquals(chosen, EtcViewModel)
                    && !set.Phrases.Contains(chosen.Name ?? string.Empty, StringComparer.Ordinal))
                {
                    _log?.Info($"[{GetType().Name}] 고른 문구 '{chosen.Name}' 이(가) 새 목록에 없어 창의 문구를 그대로 둡니다.");
                    return;
                }
                Execute.OnUIThread(() =>
                {
                    BuildItems(set.Phrases, keepSelection: true);
                    Refresh();
                });
            }
            catch (Exception ex)
            {
                _log?.Warning($"[{GetType().Name}] 조치보고 문구를 갈아 끼우지 못했습니다 — 기본 문구를 씁니다: {ex.Message}");
            }
        }

        /// <summary>문구 줄을 다시 세운다. 고른 문구가 새 목록에도 있으면 그대로 고른다.</summary>
        private void BuildItems(IReadOnlyList<string> phrases, bool keepSelection)
        {
            var previous = keepSelection ? SelectableItemViewModel?.Name : null;

            if (CollectionActionItem is not null)
                foreach (var old in CollectionActionItem) old.PropertyChanged -= ChangeSelectableItem;
            if (EtcViewModel is not null) EtcViewModel.PropertyChanged -= ChangeSelectableItem;
            SelectableItemViewModel = null;

            int id = 1;
            var list = (phrases is { Count: > 0 } ? phrases : ActionReportPhraseSource.Fallback)
                .Where(p => !string.Equals(p, ActionReportPhraseSource.EtcPhrase, StringComparison.Ordinal));
            CollectionActionItem = new ObservableCollection<SelectableItemViewModel>(list.Select(p => new SelectableItemViewModel(id++, p)));
            EtcViewModel = new SelectableItemViewModel(id++, ActionReportPhraseSource.EtcPhrase);

            EtcViewModel.PropertyChanged += ChangeSelectableItem;
            foreach (var item in CollectionActionItem) item.PropertyChanged += ChangeSelectableItem;

            var pick = previous == ActionReportPhraseSource.EtcPhrase
                ? EtcViewModel
                : CollectionActionItem.FirstOrDefault(i => i.Name == previous) ?? CollectionActionItem.FirstOrDefault();
            if (pick != null)
            {
                pick.IsSelected = true;           // ChangeSelectableItem 이 SelectableItemViewModel 을 맞춘다
                SelectableItemViewModel = pick;
            }
        }

        private IActionReportPhraseSource? ResolvePhraseSource()
        {
            try { return IoC.Get<IActionReportPhraseSource>(); }
            catch (Exception ex)
            {
                _log?.Info($"[{GetType().Name}] 조치보고 문구 소스 미해석 — 기본 문구를 씁니다: {ex.Message}");
                return null;
            }
        }

        private void ChangeSelectableItem(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SelectableItemViewModel.IsSelected))
                return;

            var vm = sender as SelectableItemViewModel;
            if (vm == null || !vm.IsSelected)
                return;

            // 이미 같은 아이템이면 아무 것도 안 한다
            if (ReferenceEquals(SelectableItemViewModel, vm))
                return;

            // 이전 선택 해제
            if (SelectableItemViewModel != null)
                SelectableItemViewModel.IsSelected = false;

            // 새 선택 지정 (IsSelected 는 이미 true 이므로 재설정 X)
            SelectableItemViewModel = vm;
        }
       
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public string? Memo { get; set; }
        //SelectableItemViewModel 추후 ClickOk 메소드에서 해당 ViewModel을 넘기게 될 것
        public SelectableItemViewModel? SelectableItemViewModel { get; set; }
        //기타 사항에 대한 SelectableItemViewModel
        public SelectableItemViewModel? EtcViewModel { get; set; }
        //조치보고 사항에 대한 SelectableItemViewModel 모음
        public ObservableCollection<SelectableItemViewModel>? CollectionActionItem { get; private set; }
        public EventCardBaseViewModel? Model => _model;

        /// <summary>조치보고를 보내는 중 — [확인] 이 꺼지고 창 틀의 Enter 도 막힌다.</summary>
        public bool IsSending => _isSending;

        /// <summary>다른 운영자가 이 이벤트를 이미 조치했다(원격 ACTION_REPORT).</summary>
        public bool IsHandledElsewhere => _isHandledElsewhere;

        /// <summary>
        /// [확인] 을 누를 수 있는가 — Caliburn 가드(<c>x:Name="ClickOk"</c> ↔ <c>CanClickOk</c>)라 단추의 켜짐이 여기서 정해진다.
        /// 보내는 중이거나 이미 다른 곳에서 조치됐으면 끈다.
        /// </summary>
        public bool CanClickOk => !_isSending && !_isHandledElsewhere;

        /// <summary>버튼 줄 왼쪽 안내 — 평소엔 사용법, 이미 조치됐으면 그 까닭, 보내는 중이면 기다리라는 말.</summary>
        public string DialogNotice => _isHandledElsewhere ? HANDLED_ELSEWHERE_TEXT
                                    : _isSending ? SENDING_TEXT
                                    : DEFAULT_NOTICE_TEXT;

        /// <summary>원격 조치보고를 받은 창의 안내.</summary>
        public const string HANDLED_ELSEWHERE_TEXT = "다른 운영자가 이미 조치했습니다. [확인]은 꺼졌습니다 — [취소]로 닫으세요.";
        public const string SENDING_TEXT = "조치보고를 보내는 중입니다…";
        public const string DEFAULT_NOTICE_TEXT = "문구를 고르고 [확인]을 누르세요. '기타'는 내용을 적어야 합니다.";

        // 속성 시현을 위한 ViewModel
        public BasePanelViewModel? SelectedItemEditor
        {
            get { return _selectedItemEditor; }
            set { _selectedItemEditor = value; NotifyOfPropertyChange(() => SelectedItemEditor); }
        }
        #endregion
        #region - Attributes -
        protected EventCardBaseViewModel? _model;
        protected IAccountModel? _user;
        private bool _isSending;
        private bool _isHandledElsewhere;
        public BasePanelViewModel? _selectedItemEditor;
        #endregion
    }
}