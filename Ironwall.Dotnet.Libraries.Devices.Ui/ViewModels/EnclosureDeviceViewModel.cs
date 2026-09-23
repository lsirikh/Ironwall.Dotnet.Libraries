using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public class EnclosureDeviceViewModel : DeviceViewModel, IEnclosureDeviceViewModel
{
    #region - Ctors -
    /// <param name="model">행 데이터.</param>
    /// <param name="policy">
    /// 서버 계약 정책(D-31 후속, 히터·팬 토글 가용성 판정용). <c>null</c> 이면
    /// <see cref="DeviceQueryPolicy.Resolve()"/> — 실제 앱은 DI 컨테이너가 등록한 정책을 돌려주고
    /// (<c>DeviceUiModule</c>), 컨테이너가 없는 맥락(헤드리스 하네스·단위테스트)에서는 안전한
    /// 6.3 기본값으로 폴백한다. 하네스처럼 결정적 판정이 필요하면 명시적으로 넘긴다.
    /// </param>
    public EnclosureDeviceViewModel(IEnclosureDeviceModel model, DeviceQueryPolicy? policy = null)
        : base(model)
    {
        _policy = policy ?? DeviceQueryPolicy.Resolve();
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

    /// <summary>(D-31 후속) 6.3 은 평면 필드가 곧 계약이라 항상 켤 수 있다 — 축 계약만 선언 여부를 따진다.</summary>
    public bool IsHeaterToggleEnabled
        => _policy.IsLegacyContract || !string.IsNullOrEmpty((_model as IEnclosureDeviceModel)!.HeaterComponentKey);

    public string? HeaterToggleUnavailableReason
        => IsHeaterToggleEnabled ? null : UNDECLARED_REASON;

    public bool IsFanToggleEnabled
        => _policy.IsLegacyContract || !string.IsNullOrEmpty((_model as IEnclosureDeviceModel)!.FanComponentKey);

    public string? FanToggleUnavailableReason
        => IsFanToggleEnabled ? null : UNDECLARED_REASON;
    #endregion
    #region - Attributes -
    private const string UNDECLARED_REASON = "이 함체는 이 부품이 선언되지 않았습니다 — 조립기(부품 구성)에서 먼저 등록해야 사용할 수 있습니다.";
    private readonly DeviceQueryPolicy _policy;
    #endregion
}
