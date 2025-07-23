using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using K4os.Compression.LZ4.Internal;
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

        private void IninializeDialog()
        {
            int id = 1;
            CollectionActionItem = new ObservableCollection<SelectableItemViewModel>
                {
                    new SelectableItemViewModel(id ++, "야생동물출현"),
                    new SelectableItemViewModel(id ++, "강풍/폭우"),
                    new SelectableItemViewModel(id ++, "울타리 점검/작업"),
                    new SelectableItemViewModel(id ++, "침입발생 특경출동조치"),
                    new SelectableItemViewModel(id ++, "오경보"),
                };
            EtcViewModel = new SelectableItemViewModel(id++, "기타");

            EtcViewModel.PropertyChanged += ChangeSelectableItem;

            CollectionActionItem.Apply((item) =>
            {
                item.PropertyChanged += ChangeSelectableItem;
            });

            SelectableItemViewModel = CollectionActionItem.FirstOrDefault();
            if(SelectableItemViewModel != null)
                SelectableItemViewModel.IsSelected = true;

            Memo = string.Empty;
            Refresh();
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