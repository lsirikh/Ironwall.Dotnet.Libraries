using Ironwall.Dotnet.Libraries.Base.Models;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 6:28:41 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CloseAllMessageModel;
public class OpenLoginPanelMessageModel;
public class OpenLogoutPanelMessageModel;
public class OpenWindyPanelMessageModel;
public class OpenSetupPanelMessageModel;
public class OpenMyPagePanelMessageModel;
public class OpenDevicePanelMessageModel;
public class OpenEventPanelMessageModel;
public class OpenAccountManagerPanelMessageModel;
public class OpenVcaPanelMessageModel;
public class ClosePanelMessageModel;
public class CloseDialogMessageModel;
public class OpenRegisterDialogMessageModel;
public class OpenResetPasswordDialogMessageModel;
public class OpenDeleteAccountDialogMessageModel;
public class OpenEditAccountDialogMessageModel;
public class OpenPreEventRemoveAllDialogMessageModel;
public class OpenOnvifPropertyDialogMessageModel;
public class OpenEventReportDialogMessageModel
{
    public string? EventType { get; set; }
}
public class OpenPreEventRemoveDialogMessageModel;
public class OpenPreEventFaultDetailsDialogMessageModel;
public class OpenPostEventDetailsDialogMessageModel;
public class OpenPostEventFaultDetailsDialogMessageModel;
public class OpenDiscoveryDialogMessageModel;
public class OpenDeleteAccountAdminPopupMessageModel;
public class OpenAboutSetupPanelMessageModel;
public class ClosePopupMessageModel;
public class CloseAllWindowsMessageModel;
public class RefreshAccountsMessageModel;

public class CallEditAccountAdminProcessMessageModel : IMessageModel { }
public class CallDeleteAccountAdminProcessMessageModel : IMessageModel { }
public class CallResetPasswordAdminProcessMessageModel : IMessageModel { }
public class CallEditProcessMessageModel : IMessageModel { }
public class CallResetProcessMessageModel : IMessageModel { }
public class CallResetPasswordProcessMessageModel : IMessageModel { }
public class CallDeleteProcessMessageModel : IMessageModel { }
public class CallDeleteControllerDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteCameraDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteSensorDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteDetectionEventProcessMessageModel : IMessageModel { }
public class CallDeleteMalfunctionEventProcessMessageModel : IMessageModel { }
public class CallDeleteConnectionEventProcessMessageModel : IMessageModel { }
public class CallDeleteActionEventProcessMessageModel : IMessageModel { }
public class ExitProgramMessageModel : IMessageModel { }
public class OpenConfirmPopupMessageModel : CommonMessageModel { }
public class OpenInfoPopupMessageModel : CommonMessageModel { }
public class OpenProgressPopupMessageModel : IMessageModel { }
public class CallAllEventReportMessageModel : IMessageModel { }
public sealed class ChangeModeWindyMessageModel : EventMessageModel<int>
{
}
public class StatusMessageModel
{
    public StatusMessageModel()
    {
    }

    public StatusMessageModel(string log)
    {
        Log = log;
    }

    public string? Log { get; set; }
}