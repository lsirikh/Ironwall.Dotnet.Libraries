using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/7/2025 4:56:16 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DetectionReportedMessageModel
    {
        //public DetectionReportDialogViewModel ViewModel { get; private set; }
        //public DetectionReportedMessageModel(DetectionReportDialogViewModel detectionReportDialogViewModel)
        //{
        //    ViewModel = detectionReportDialogViewModel;
        //}

        public DetectionEventCardViewModel ViewModel { get; private set; }

        public string? Content { get; private set; }
        public string? User { get; private set; }


        public DetectionReportedMessageModel(DetectionEventCardViewModel detectionEventCardViewModel, string? content, string? user)
        {
            ViewModel = detectionEventCardViewModel;
            Content = content;
            User = user;
        }
    }

    public class MalfunctionReportedMessageModel
    {
        //public MalfunctionReportDialogViewModel ViewModel { get; private set; }
        //public MalfunctionReportedMessageModel(MalfunctionReportDialogViewModel malfunctionReportDialogViewModel)
        //{
        //    ViewModel = malfunctionReportDialogViewModel;
        //}
        public MalfunctionEventCardViewModel ViewModel { get; private set; }
        public string? Content { get; private set; }
        public string? User { get; private set; }


        public MalfunctionReportedMessageModel(MalfunctionEventCardViewModel malfunctionEventCardViewModel, string? content, string? user)
        {
            ViewModel = malfunctionEventCardViewModel;
            Content = content;
            User = user;
        }
    }
}