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
            NotifyOfPropertyChange(() => DoorStatusDisplay);
        }
    }

    /// <summary>
    /// 도어 상태 <b>표시 전용</b> — 값이 비었으면 공백이 아니라 "미상"으로 드러낸다(단방향).
    /// </summary>
    /// <remarks>
    /// 서버 7.0+ 는 문 위치를 스칼라(<c>door_status</c>)가 아니라 <c>device_status.components.door.state</c> 로 준다.
    /// 스칼라가 비는 것을 <c>CLOSED</c> 로 덮지 않기로 했으므로(DtoToModelHelper.NormalizeDoorScalar)
    /// 이 칸이 비어 보일 수 있다. <b>모르는 것과 닫힌 것은 다르다</b> — 빈 칸은 "닫힘"으로 오독되므로
    /// 명시적으로 미상임을 적는다. 원본 <see cref="DoorStatus"/> 값은 바꾸지 않는다.
    /// </remarks>
    public string DoorStatusDisplay
        => string.IsNullOrWhiteSpace(DoorStatus) ? "미상" : DoorStatus;

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
