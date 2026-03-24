using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      : CameraSettingModel passthrough ViewModel
   Created By   : GHLee
   Created On   : 2/25/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public sealed class CameraSettingViewModel : BasePanelViewModel
{
    private readonly ICameraSettingModel _model;

    public CameraSettingViewModel(ICameraSettingModel model)
    {
        _model = model;
    }

    public int CameraId
    {
        get => _model.CameraId;
        set { _model.CameraId = value; NotifyOfPropertyChange(() => CameraId); }
    }

    public string WeatherMode
    {
        get => _model.WeatherMode;
        set { _model.WeatherMode = value; NotifyOfPropertyChange(() => WeatherMode); }
    }

    public string CameraMode
    {
        get => _model.CameraMode;
        set { _model.CameraMode = value; NotifyOfPropertyChange(() => CameraMode); }
    }

    public string Heater
    {
        get => _model.Heater;
        set { _model.Heater = value; NotifyOfPropertyChange(() => Heater); }
    }

    public string Fan
    {
        get => _model.Fan;
        set { _model.Fan = value; NotifyOfPropertyChange(() => Fan); }
    }

    public string Headlight
    {
        get => _model.Headlight;
        set { _model.Headlight = value; NotifyOfPropertyChange(() => Headlight); }
    }

    public string DayNightMode
    {
        get => _model.DayNightMode;
        set { _model.DayNightMode = value; NotifyOfPropertyChange(() => DayNightMode); }
    }

    public string FocusMode
    {
        get => _model.FocusMode;
        set { _model.FocusMode = value; NotifyOfPropertyChange(() => FocusMode); }
    }

    public string IrisMode
    {
        get => _model.IrisMode;
        set { _model.IrisMode = value; NotifyOfPropertyChange(() => IrisMode); }
    }

    public string Tracking
    {
        get => _model.Tracking;
        set { _model.Tracking = value; NotifyOfPropertyChange(() => Tracking); }
    }

    public string? Palette
    {
        get => _model.Palette;
        set { _model.Palette = value; NotifyOfPropertyChange(() => Palette); }
    }
}
