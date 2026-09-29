namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 부품 관측(<c>DOOR_ACTUATOR</c> · <c>DOOR_SENSOR</c> 의 state)에서 읽은 문 위치.
/// </summary>
/// <remarks>
/// <c>EnumDoorState</c>(Unknown/Closed/Open)에는 <c>RUNNING</c> 자리가 없어 <c>DoorStateMachine.FromServer</c> 가
/// 구동 중을 Unknown 으로 버린다. 여기서는 구동 중을 따로 들고 있어 아이콘이 "움직이는 중"을 그릴 수 있게 한다.
/// </remarks>
public enum DoorMotionState
{
    /// <summary>문 부품이 없거나 관측이 없다.</summary>
    Unknown = 0,
    Closed = 1,
    Open = 2,
    /// <summary><c>DOOR_ACTUATOR.state = RUNNING</c> — 열리거나 닫히는 중.</summary>
    Running = 3,
}
