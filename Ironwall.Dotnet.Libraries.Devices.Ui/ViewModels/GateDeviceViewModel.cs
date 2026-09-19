using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

/// <summary>
/// 통문(gate) 목록 행. 공통 필드(번호·이름·상태·위치…)는 <see cref="DeviceViewModel"/> 몫이고,
/// 여기는 통문 고유의 <b>읽기 전용</b> 표시값만 둔다(device-console-v8 FR-08).
/// </summary>
public class GateDeviceViewModel : DeviceViewModel, IGateDeviceViewModel
{
    /// <summary>문 구동부의 부품 유형 — 카탈로그 <c>component_type</c> 어휘. 부품 <c>key</c> 는 장비마다 자유라 유형으로 찾는다.</summary>
    public const string DoorActuatorType = "DOOR_ACTUATOR";

    /// <summary>값을 모를 때의 표시 — 빈칸이 아니라 "모른다"를 보인다.</summary>
    public const string Unknown = "—";

    #region - Ctors -
    public GateDeviceViewModel(IGateDeviceModel model)
        : base(model)
    {
    }
    #endregion
    #region - Properties -
    /// <summary>
    /// 문 위치. ① v7.0+ 관측 축의 구동부 상태(<c>OPEN</c>·<c>CLOSED</c>·<c>RUNNING</c>) →
    /// ② 없으면 6.3 스칼라 <c>gate_status</c> → ③ 그것도 없으면 <see cref="Unknown"/>.
    /// <para>개폐 <b>명령</b>은 이 값을 바꾸지 않는다 — 매니저의 상태 보고로만 바뀐다. 그래서 읽기 전용이다.</para>
    /// </summary>
    public string DoorPosition
    {
        get
        {
            var observed = _model.Axes?.FindStatusByType(DoorActuatorType)?.State;
            if (!string.IsNullOrWhiteSpace(observed)) return observed!;

            var legacy = (_model as IGateDeviceModel)?.GateStatus;
            return string.IsNullOrWhiteSpace(legacy) ? Unknown : legacy!;
        }
    }

    public string? ConnectionType => _model.Axes?.Connection?.Type;
    public int? ParentDeviceId => _model.Axes?.Connection?.ParentDeviceId;
    public int? Channel => _model.Axes?.Connection?.Channel;
    #endregion
}
