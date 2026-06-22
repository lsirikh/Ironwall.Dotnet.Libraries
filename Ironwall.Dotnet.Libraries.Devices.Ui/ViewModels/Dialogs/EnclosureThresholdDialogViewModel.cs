using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 함체 임계값 설정 다이얼로그 (카메라 상세 Conductor 패턴)
   Created By   : GHLee
   Created On   : 6/22/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 함체 임계값 편집 다이얼로그 — CameraDetailDialogViewModel(Conductor.Collection.OneActive) 복제.
/// 모델 직접 편집(영속은 그리드 Save). 임계값 미설정 시 빈 설정을 모델에 생성해 편집 반영.
/// </summary>
public class EnclosureThresholdDialogViewModel : Conductor<BasePanelViewModel>.Collection.OneActive
{
    private readonly IEnclosureDeviceModel _model;

    public EnclosureThresholdDialogViewModel(IEnclosureDeviceModel model)
    {
        _model = model;
        _model.ThresholdConfig ??= new EnclosureThresholdConfigModel();
        DisplayName = $"{model.DeviceName} 임계값";
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var settingVm = new EnclosureThresholdSettingViewModel(_model.ThresholdConfig!);
        settingVm.DisplayName = "임계값";
        await ActivateItemAsync(settingVm, cancellationToken);
    }
}
