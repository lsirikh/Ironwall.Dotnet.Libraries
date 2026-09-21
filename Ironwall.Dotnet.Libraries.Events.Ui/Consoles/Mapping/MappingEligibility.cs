using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 드롭 유효성 — 순수 판정(와이어프레임 §3-2 매트릭스)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>드롭 판정 한 건의 결과 — 거절이면 <b>사용자에게 보일 한국어 사유</b>가 함께 온다.</summary>
/// <param name="IsAllowed">놓을 수 있는가.</param>
/// <param name="Reason">거절 사유(허용이면 빈 문자열).</param>
public readonly record struct MappingDropVerdict(bool IsAllowed, string Reason)
{
    /// <summary>허용.</summary>
    public static MappingDropVerdict Allow() => new(true, string.Empty);

    /// <summary>거절 + 사유.</summary>
    public static MappingDropVerdict Block(string reason) => new(false, reason);
}

/// <summary>드래그의 출처.</summary>
public enum MappingDragOrigin
{
    /// <summary>오른쪽 팔레트(장비 후보).</summary>
    Palette,
    /// <summary>가운데 액션 보드(이미 배선된 행).</summary>
    Board,
}

/// <summary>
/// 무엇을 어디에 놓을 수 있는가. <b>UI 도 서버도 모르는 순수 판정</b>이라 헤드리스로 전건 시험한다.
/// </summary>
/// <remarks>
/// 이 판정은 드래그 중 <b>끊임없이</b> 불린다(<c>IDragDropHandler.CanDrop</c>). 서버를 부르거나
/// 목록을 새로 만들지 않는다 — 전부 이미 손에 있는 값으로만 답한다.
/// </remarks>
public static class MappingEligibility
{
    /// <summary>읽기 전용일 때의 사유(드래그 시작 자체를 막는다).</summary>
    public const string ReadOnlyReason = "이벤트 맵핑 편집 권한이 없습니다.";

    /// <summary>매핑을 아직 고르지 않았을 때.</summary>
    public const string NoMappingReason = "먼저 왼쪽에서 이벤트 맵핑을 고르십시오.";

    /// <summary>
    /// 팔레트 → 액션 보드(투입).
    /// </summary>
    /// <param name="sourceKind">끌고 있는 장비의 종류.</param>
    /// <param name="zoneKind">놓으려는 보드 탭의 종류.</param>
    /// <param name="isReadOnly">편집 권한이 없는가.</param>
    /// <param name="hasMapping">매핑을 골랐는가.</param>
    /// <param name="newDeviceCount">그중 <b>아직 안 들어간</b> 장비 수.</param>
    public static MappingDropVerdict PaletteToBoard(
        MappingActionKind sourceKind, MappingActionKind zoneKind,
        bool isReadOnly, bool hasMapping, int newDeviceCount)
    {
        if (isReadOnly) return MappingDropVerdict.Block(ReadOnlyReason);
        if (!hasMapping) return MappingDropVerdict.Block(NoMappingReason);
        if (sourceKind != zoneKind)
            return MappingDropVerdict.Block($"{MappingKindText.Label(zoneKind)} 탭에는 {MappingKindText.Label(sourceKind)}를 놓을 수 없습니다.");
        if (newDeviceCount <= 0)
            return MappingDropVerdict.Block("고른 장비가 이미 전부 등록되어 있습니다.");
        return MappingDropVerdict.Allow();
    }

    /// <summary>
    /// 보드 → 보드(순서 바꾸기). <b>다른 탭으로는 옮길 수 없다</b> — 축이 다르면 장비 종류가 달라진다.
    /// </summary>
    public static MappingDropVerdict BoardReorder(
        MappingActionKind sourceKind, MappingActionKind zoneKind, bool isReadOnly, int rowCount)
    {
        if (isReadOnly) return MappingDropVerdict.Block(ReadOnlyReason);
        if (sourceKind != zoneKind)
            return MappingDropVerdict.Block("다른 탭으로는 옮길 수 없습니다. 해제한 뒤 그 탭에서 다시 넣으십시오.");
        if (rowCount <= 0) return MappingDropVerdict.Block("옮길 행이 없습니다.");
        return MappingDropVerdict.Allow();
    }

    /// <summary>
    /// 보드 → 팔레트(해제).
    /// </summary>
    public static MappingDropVerdict BoardToPalette(bool isReadOnly, bool canDelete, int rowCount)
    {
        if (isReadOnly) return MappingDropVerdict.Block(ReadOnlyReason);
        if (!canDelete) return MappingDropVerdict.Block("배선을 해제할 권한이 없습니다.");
        if (rowCount <= 0) return MappingDropVerdict.Block("해제할 행이 없습니다.");
        return MappingDropVerdict.Allow();
    }

    /// <summary>
    /// 프리셋 칩 → 카메라 행의 슬롯. <b>그 카메라 소유 프리셋만</b> 받는다 —
    /// 아니면 서버가 422 를 내므로 드롭 전에 구조적으로 막는다.
    /// </summary>
    public static MappingDropVerdict PresetToRow(int presetCameraId, int? rowCameraId, bool isReadOnly)
    {
        if (isReadOnly) return MappingDropVerdict.Block(ReadOnlyReason);
        if (rowCameraId is null) return MappingDropVerdict.Block("장비가 끊긴 행입니다. 먼저 카메라를 다시 지정하십시오.");
        if (presetCameraId != rowCameraId.Value) return MappingDropVerdict.Block("다른 카메라의 프리셋입니다.");
        return MappingDropVerdict.Allow();
    }

    /// <summary>
    /// 그 장비들 중 <b>아직 보드에 없는</b> id 만. 팔레트 흐림 표시와 드롭 판정이 같은 함수를 쓴다.
    /// </summary>
    public static IReadOnlyList<int> NewDeviceIds(MappingBoard board, MappingActionKind kind, IEnumerable<int> deviceIds)
        => deviceIds.Where(id => id > 0 && !board.Contains(kind, id)).Distinct().ToList();
}
