using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
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
        #region - Implementation of Interface -
        public override async void ClickOk()
        {
            var user = $"{_user?.Username}({_user?.EmployeeNumber})";
            if (!(Model is DetectionEventCardViewModel vm)) return;
            if (SelectableItemViewModel?.Name == "기타")
                await vm.SendAction(Memo, user);
            else
                await vm.SendAction(SelectableItemViewModel?.Name, user);
            

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

            var model = (eventModel as DetectionEventCardViewModel)!.Model as IDetectionEventModel;
            var viewModel = new DetectionEventViewModel(model!);
            var list = new List<DetectionEventViewModel>() { viewModel };
            SelectedItemEditor = new DetectionSelectionViewModel(list);
            (SelectedItemEditor as DetectionSelectionViewModel)!.RefreshAll();
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