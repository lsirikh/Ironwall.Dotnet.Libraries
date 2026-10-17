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
    /// 문 위치. ① v7.0+ 관측 축의 문 위치 부품 상태 — 구동부(<c>DOOR_ACTUATOR</c>) → 문 센서(<c>DOOR_SENSOR</c>, 구동부 없는 일반 통문) →
    /// ② 없으면 6.3 스칼라 <c>gate_status</c> → ③ 그것도 없으면 <see cref="Unknown"/>(<see cref="Ironwall.Dotnet.Monitoring.Models.Helpers.DoorPosition.Of"/>).
    /// <para>개폐 <b>명령</b>은 이 값을 바꾸지 않는다 — 매니저의 상태 보고로만 바뀐다. 그래서 읽기 전용이다.</para>
    /// </summary>
    public string DoorPosition
    {
        get
        {
            return Ironwall.Dotnet.Monitoring.Models.Helpers.DoorPosition.Of(_model) ?? Unknown;
        }
    }

    /// <summary>문 위치의 <b>화면 글</b> — 열림 · 닫힘 · 구동 중 · 미상(목록 열 · 상세 칸). 원값은 <see cref="DoorPosition"/>.</summary>
    public string DoorPositionDisplay
        => Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.DeviceEnumDisplay.DoorStateKorean(DoorPosition == Unknown ? null : DoorPosition);

    public string? ConnectionType => _model.Axes?.Connection?.Type;
    public int? ParentDeviceId => _model.Axes?.Connection?.ParentDeviceId;
    public int? Channel => _model.Axes?.Connection?.Channel;
    #endregion
}
