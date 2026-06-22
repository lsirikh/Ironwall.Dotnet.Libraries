using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      : 함체 임계값 편집 탭 (IEnclosureThresholdConfigModel passthrough)
   Created By   : GHLee
   Created On   : 6/22/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 함체 임계값 설정 탭 콘텐츠 VM — 카메라 상세 탭(CameraSettingViewModel) 패턴.
/// 모델을 직접 passthrough 편집하며, 영속은 그리드 Save(ToEnclosureDeviceDto) 흐름이 담당.
/// </summary>
public sealed class EnclosureThresholdSettingViewModel : BasePanelViewModel
{
    private readonly IEnclosureThresholdConfigModel _model;

    public EnclosureThresholdSettingViewModel(IEnclosureThresholdConfigModel model)
    {
        _model = model;
    }

    public double? TempHigh
    {
        get => _model.TempHigh;
        set { _model.TempHigh = value; NotifyOfPropertyChange(() => TempHigh); }
    }
    public double? TempLow
    {
        get => _model.TempLow;
        set { _model.TempLow = value; NotifyOfPropertyChange(() => TempLow); }
    }
    public double? HumidityHigh
    {
        get => _model.HumidityHigh;
        set { _model.HumidityHigh = value; NotifyOfPropertyChange(() => HumidityHigh); }
    }
    public double? HumidityLow
    {
        get => _model.HumidityLow;
        set { _model.HumidityLow = value; NotifyOfPropertyChange(() => HumidityLow); }
    }
    public double? VibrationThreshold
    {
        get => _model.VibrationThreshold;
        set { _model.VibrationThreshold = value; NotifyOfPropertyChange(() => VibrationThreshold); }
    }
}
