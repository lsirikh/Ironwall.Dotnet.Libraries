using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

/// <summary>
/// 아이콘 문 표시 모양을 정하는 순수 함수.
/// </summary>
/// <remarks>
/// <para>입력 두 채널: ① 심볼 개폐 형태 축 <see cref="EnumDoorState"/>(SYNC_DEVICE · OPERATION_EVENT · 접점이 확정) ②
/// 부품 관측 <see cref="DoorMotionState"/>(구동 중 <c>RUNNING</c> 을 아는 유일한 곳). 구동 중이면 그것이 이긴다 —
/// 형태 축에는 구동 중 자리가 없어 <c>DoorStateMachine.FromServer</c> 가 Unknown 으로 버린다.</para>
/// <para>3D 하우징은 문짝 각도로 열림/닫힘을 이미 그리므로 <paramref name="leavesShowPosition"/>=true 면 구동 중만 표시한다.</para>
/// </remarks>
public static class DoorIndicatorRules
{
    public static DoorIndicatorKind Resolve(bool hasDoor, EnumDoorState doorState, DoorMotionState motion, bool leavesShowPosition)
    {
        if (!hasDoor) return DoorIndicatorKind.None;
        if (motion == DoorMotionState.Running) return DoorIndicatorKind.Running;
        if (leavesShowPosition) return DoorIndicatorKind.None;

        return doorState switch
        {
            EnumDoorState.Open => DoorIndicatorKind.Open,
            EnumDoorState.Closed => DoorIndicatorKind.Closed,
            _ => motion switch
            {
                DoorMotionState.Open => DoorIndicatorKind.Open,
                DoorMotionState.Closed => DoorIndicatorKind.Closed,
                _ => DoorIndicatorKind.Unknown,
            },
        };
    }

    /// <summary>툴팁 · 자동화 이름용 한글.</summary>
    public static string Text(DoorIndicatorKind kind) => kind switch
    {
        DoorIndicatorKind.Open => "문 열림",
        DoorIndicatorKind.Closed => "문 닫힘",
        DoorIndicatorKind.Running => "문 동작 중",
        DoorIndicatorKind.Unknown => "문 상태 모름",
        _ => string.Empty,
    };
}
