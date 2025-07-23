using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Security.Principal;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

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
        #region - Implementation of Interface -
        public override async void ClickOk()
        {
            var user = $"{_user?.Username}({_user?.EmployeeNumber})";
            if (!(Model is MalfunctionEventCardViewModel vm)) return;
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