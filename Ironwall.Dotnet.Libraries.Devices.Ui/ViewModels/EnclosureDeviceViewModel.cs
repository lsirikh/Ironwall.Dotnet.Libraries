using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public class EnclosureDeviceViewModel : DeviceViewModel, IEnclosureDeviceViewModel
{
    #region - Ctors -
    public EnclosureDeviceViewModel(IEnclosureDeviceModel model)
        : base(model)
    {
    }
    #endregion
    #region - Properties -
    public string DoorStatus
    {
        get { return (_model as IEnclosureDeviceModel)!.DoorStatus; }
        set
        {
            (_model as IEnclosureDeviceModel)!.DoorStatus = value;
            NotifyOfPropertyChange(() => DoorStatus);
        }
    }

    public bool HeaterEnabled
    {
        get { return (_model as IEnclosureDeviceModel)!.HeaterEnabled; }
        set
        {
            (_model as IEnclosureDeviceModel)!.HeaterEnabled = value;
            NotifyOfPropertyChange(() => HeaterEnabled);
        }
    }

    public bool FanEnabled
    {
        get { return (_model as IEnclosureDeviceModel)!.FanEnabled; }
        set
        {
            (_model as IEnclosureDeviceModel)!.FanEnabled = value;
            NotifyOfPropertyChange(() => FanEnabled);
        }
    }

    public IEnclosureThresholdConfigModel? ThresholdConfig
        => (_model as IEnclosureDeviceModel)!.ThresholdConfig;

    public string ThresholdSummary
    {
        get
        {
            var tc = ThresholdConfig;
            if (tc == null) return "-";
            return $"T:{tc.TempHigh ?? 0}/{tc.TempLow ?? 0} H:{tc.HumidityHigh ?? 0} C:{tc.CurrentHigh ?? 0} V:{tc.VoltageLow ?? 0} Vib:{tc.VibrationHigh ?? 0}";
        }
    }
    #endregion
}
