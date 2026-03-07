using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
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
public class OpenCameraDetailDialogMessageModel
{
    public object? Dialog { get; set; }
}
public class OpenDeviceAssignDialogMessageModel
{
    public object? Dialog { get; set; }
    public Action? OnCompleted { get; set; }
}
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
public class CallDeleteSpeakerDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteEnclosureDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteLampDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteDeviceGroupProcessMessageModel : IMessageModel { }
public class CallRemoveDeviceFromGroupProcessMessageModel : IMessageModel { }
public class CallDeleteDetectionEventProcessMessageModel : IMessageModel { }
public class CallDeleteMalfunctionEventProcessMessageModel : IMessageModel { }
public class CallDeleteConnectionEventProcessMessageModel : IMessageModel { }
public class CallDeleteActionEventProcessMessageModel : IMessageModel { }
public class CallDelete3rdEventProcessMessageModel : IMessageModel { }
public class ExitProgramMessageModel : IMessageModel { }
public class OpenConfirmPopupMessageModel : CommonMessageModel { }
public class OpenInfoPopupMessageModel : CommonMessageModel { }
public class OpenProgressPopupMessageModel : IMessageModel { }
public class CallAllEventReportMessageModel : IMessageModel { }
public sealed class ChangeModeWindyMessageModel : EventMessageModel<int>
{
}
/// <summary>WindyMode NATS REQ 요청 트리거 — WindyPanelViewModel → NatsDomainService</summary>
public record SendWindyModeMessage(EnumWindyMode Mode);

/// <summary>디바이스 초기 로딩 완료 알림 — SymbolEventManager 일괄 동기화 트리거</summary>
public record AllDevicesLoadedMessage();

/// <summary>NatsSync 기반 단일 디바이스 Status 변경 알림</summary>
public record DeviceStatusChangedMessage(int DeviceId, EnumDeviceType DeviceType, EnumDeviceStatus Status);

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