using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 워크벤치가 스스로 내는 경고 — 서버가 검사하지 않는 것들(순수 함수)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>경고의 세기.</summary>
public enum MappingWarningLevel
{
    /// <summary>알려만 준다.</summary>
    Info,
    /// <summary>확인을 요구하지만 저장은 된다.</summary>
    Warning,
    /// <summary>저장을 막는다.</summary>
    Blocking,
}

/// <summary>워크벤치가 내는 경고 한 줄.</summary>
/// <param name="Level">세기.</param>
/// <param name="Text">사용자에게 보일 한국어.</param>
public readonly record struct MappingWarning(MappingWarningLevel Level, string Text);

/// <summary>
/// 서버가 <b>검사하지 않는</b> 것들을 화면이 대신 본다.
/// </summary>
/// <remarks>
/// 서버 문서가 명시한 것만 해도 셋이다 — ① 같은 (장비그룹, 카테고리) 매핑이 여러 개여도 막지 않고
/// ② 붙인 장비가 그 그룹 소속인지 보지 않고 ③ <c>status=false</c> 매핑도 그대로 돌려준다.
/// 매칭을 실제로 실행하는 주체도 서버가 아니라 클라다 — 그래서 <b>워크벤치가 스스로 검증 화면을 가져야 한다</b>.
/// </remarks>
public static class MappingWarnings
{
    /// <summary>
    /// 지금 고른 매핑에 대해 낼 경고 전부.
    /// </summary>
    /// <param name="current">지금 편집 중인 매핑.</param>
    /// <param name="all">서버에 있는 매핑 전부(중복 조건 판정용).</param>
    /// <param name="board">액션 보드.</param>
    /// <param name="devices">장비 조회(캐시). 소속 대조에 쓴다.</param>
    public static IReadOnlyList<MappingWarning> For(
        EventMappingReadDto? current,
        IReadOnlyList<EventMappingReadDto> all,
        MappingBoard board,
        IMappingDeviceSource? devices)
    {
        var list = new List<MappingWarning>();
        if (current is null) return list;

        // ① 장비그룹이 없으면 이 매핑은 아무 이벤트와도 짝지어지지 않는다.
        if (current.DeviceGroupId is null)
            list.Add(new(MappingWarningLevel.Warning, "장비그룹이 지정되지 않아 어떤 이벤트와도 매칭되지 않습니다."));

        // ② 같은 (장비그룹, 카테고리) 조합이 이미 있으면 둘이 동시에 실행된다. 막지는 않는다.
        var duplicates = all.Count(m =>
            m.Id != current.Id
            && m.DeviceGroupId == current.DeviceGroupId
            && string.Equals(m.CategoryEventMapping, current.CategoryEventMapping, System.StringComparison.Ordinal));
        if (duplicates > 0)
            list.Add(new(MappingWarningLevel.Warning,
                $"같은 조건의 맵핑이 {duplicates}건 더 있습니다 — 이벤트가 나면 {duplicates + 1}건이 동시에 실행됩니다."));

        // ③ 매핑 자체가 중지 상태.
        if (!current.Status)
            list.Add(new(MappingWarningLevel.Info, "이 맵핑은 중지 상태입니다 — 저장해도 실행되지 않습니다."));

        // ④ 고아 행은 저장을 막는다. 0 을 되보내면 서버가 404/422 를 내기 때문이다.
        var orphans = board.AllBlockingOrphans().Count;
        if (orphans > 0)
            list.Add(new(MappingWarningLevel.Blocking,
                $"장비가 끊긴 행이 {orphans}건 있습니다 — 장비를 다시 지정하거나 해제해야 저장할 수 있습니다."));

        // ⑤ 그 매핑의 장비그룹에 속하지 않는 장비. 경고만 한다(임시 구성 중일 수 있다).
        if (devices is not null && current.DeviceGroupId is int groupId)
        {
            var strays = 0;
            foreach (var kind in MappingBoard.Kinds)
            {
                foreach (var row in board.LiveRows(kind))
                {
                    if (row.DeviceId is not int id) continue;
                    var info = devices.Find(kind, id);
                    if (info is null) continue;                 // 캐시에 없으면 판정하지 않는다(모르는 것을 경고하지 않는다)
                    if (!info.GroupIds.Contains(groupId)) strays++;
                }
            }
            if (strays > 0)
                list.Add(new(MappingWarningLevel.Warning, $"이 맵핑의 장비그룹에 속하지 않는 장비가 {strays}건 있습니다."));
        }

        // ⑥ 비활성 장비가 섞여 있으면 그 행은 실행되지 않는다.
        var disabled = MappingBoard.Kinds
            .SelectMany(board.LiveRows)
            .Count(r => !r.IsEnable);
        if (disabled > 0)
            list.Add(new(MappingWarningLevel.Info, $"사용 안 함으로 둔 배선이 {disabled}건 있습니다."));

        // ⑦ 감시금지구역 프리셋.
        var restricted = board.LiveRows(MappingActionKind.Camera).Count(r => r.TargetPresetRestricted);
        if (restricted > 0)
            list.Add(new(MappingWarningLevel.Info, $"감시금지구역 프리셋이 {restricted}건 있습니다 — 일반 팝업·녹화에서 제외됩니다."));

        return list;
    }

    /// <summary>경고 중 저장을 막는 것이 있는가.</summary>
    public static bool Blocks(IEnumerable<MappingWarning> warnings)
        => warnings.Any(w => w.Level == MappingWarningLevel.Blocking);

    /// <summary>상태줄 한 줄로 접은 문구. 막는 것이 있으면 그것을 먼저 보여 준다.</summary>
    public static string Summarize(IReadOnlyList<MappingWarning> warnings)
    {
        if (warnings.Count == 0) return string.Empty;
        var blocking = warnings.FirstOrDefault(w => w.Level == MappingWarningLevel.Blocking);
        if (blocking.Text is { Length: > 0 }) return blocking.Text;
        var warning = warnings.FirstOrDefault(w => w.Level == MappingWarningLevel.Warning);
        if (warning.Text is { Length: > 0 })
            return warnings.Count == 1 ? warning.Text : $"{warning.Text} (경고 {warnings.Count}건)";
        return warnings[0].Text;
    }
}
