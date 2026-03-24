using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : Camera Detail Dialog - 3 tab Conductor (URLs, Setting, Info)
   Created By   : GHLee
   Created On   : 2/25/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class CameraDetailDialogViewModel : Conductor<BasePanelViewModel>.Collection.OneActive
{
    private readonly ICameraDeviceModel _model;

    public CameraDetailDialogViewModel(ICameraDeviceModel model)
    {
        _model = model;
        DisplayName = $"{model.DeviceName}";
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var infoVm = new CameraInfoViewModel(_model.HardwareSpec ?? new CameraInfoModel());
        infoVm.DisplayName = "HW Spec";
        await ActivateItemAsync(infoVm, cancellationToken);

        var settingVm = new CameraSettingViewModel(_model.Setting ?? new CameraSettingModel());
        settingVm.DisplayName = "Setting";
        await ActivateItemAsync(settingVm, cancellationToken);

        var urlsVm = new CameraUrlsViewModel(_model.Urls ?? new CameraUrlsModel());
        urlsVm.DisplayName = "URLs";
        await ActivateItemAsync(urlsVm, cancellationToken);
        
    }
}
