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
    public abstract class EventReportDialogViewModel : BasePanelViewModel
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

            Refresh();
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

        private async Task RefreshPhrasesAsync(IActionReportPhraseSource source)
        {
            try
            {
                var set = await source.LoadAsync().ConfigureAwait(true);
                var current = CollectionActionItem?.Select(i => i.Name).ToList() ?? new List<string?>();
                if (!IsActive || set.Phrases.SequenceEqual(current)) return;
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
        public BasePanelViewModel? _selectedItemEditor;
        #endregion
    }
}