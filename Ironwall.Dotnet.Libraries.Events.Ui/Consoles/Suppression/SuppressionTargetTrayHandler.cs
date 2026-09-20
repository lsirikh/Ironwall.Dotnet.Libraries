using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 장비 · 그룹 목록 → 대상 칩 트레이 드롭 처리.
                  드래그와 [추가 ▶] · Enter 가 '같은 함수' 하나를 부른다.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>목록 행(무엇이든) → 대상 칩. 순수 판정이 뷰모델 타입을 모르도록 변환을 한 곳에 모은다.</summary>
public static class SuppressionTargetCandidateFactory
{
    public static SuppressionTargetChip? From(object? row) => row switch
    {
        SuppressionTargetChip chip => chip,
        IBaseDeviceModel device => new SuppressionTargetChip(
            SuppressionTargetKind.Device, device.Id,
            string.IsNullOrWhiteSpace(device.DeviceName) ? $"#{device.Id}" : device.DeviceName!,
            device.Location ?? string.Empty),
        IDeviceGroupModel group => new SuppressionTargetChip(
            SuppressionTargetKind.Group, group.Id,
            string.IsNullOrWhiteSpace(group.Name) ? $"#{group.Id}" : group.Name,
            $"장비 {group.DeviceCount}대"),
        _ => null,
    };

    public static IReadOnlyList<SuppressionTargetChip> FromRows(IEnumerable<object>? rows)
        => rows?.Select(From).Where(c => c is not null).Select(c => c!).ToList()
           ?? (IReadOnlyList<SuppressionTargetChip>)Array.Empty<SuppressionTargetChip>();
}

/// <summary>
/// 대상 칩 트레이 드롭존의 처리기.
/// </summary>
/// <remarks>
/// <para>드롭은 <b>서버를 부르지 않는다</b> — 초안에 칩을 쌓을 뿐이고, 전송은 [저장] 한 번이다.</para>
/// <para><see cref="CanDrop"/> 은 끄는 동안 매 프레임 불린다 — 서버 호출도, 목록 순회도 최소로 한다.</para>
/// </remarks>
public sealed class SuppressionTargetTrayHandler : IDragDropHandler
{
    private readonly Func<string> _mode;
    private readonly Func<IReadOnlyList<SuppressionTargetChip>> _existing;
    private readonly Func<bool> _canEdit;
    private readonly Action<SuppressionTargetPlan> _accept;

    /// <param name="mode">지금 폼의 대상 유형.</param>
    /// <param name="existing">트레이에 이미 있는 칩(중복 판정).</param>
    /// <param name="canEdit">편집 권한.</param>
    /// <param name="accept">받아들인 계획을 초안에 반영한다(UI 스레드에서 불린다).</param>
    public SuppressionTargetTrayHandler(Func<string> mode,
                                        Func<IReadOnlyList<SuppressionTargetChip>> existing,
                                        Func<bool> canEdit,
                                        Action<SuppressionTargetPlan> accept)
    {
        _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        _existing = existing ?? throw new ArgumentNullException(nameof(existing));
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        _accept = accept ?? throw new ArgumentNullException(nameof(accept));
    }

    /// <summary>담기가 끝났다(담았든 못 담았든) — 서랍 아래에 남길 한 줄.</summary>
    public event Action<string>? Completed;

    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (payload is null || target is null) return false;
        if (!string.Equals(target.ZoneKey, SuppressionTargetDrop.ZoneKey, StringComparison.Ordinal)) return false;

        return SuppressionTargetDrop
            .Plan(SuppressionTargetCandidateFactory.FromRows(payload.Items), _mode(), _existing(), _canEdit())
            .CanAdd;
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (payload is null || target is null) return;
        if (!string.Equals(target.ZoneKey, SuppressionTargetDrop.ZoneKey, StringComparison.Ordinal)) return;
        Add(payload.Items);
    }

    /// <summary>드래그 · [추가 ▶] · Enter 가 함께 쓰는 <b>단 하나의</b> 담기 경로.</summary>
    public string Add(IEnumerable<object>? rows)
    {
        var plan = SuppressionTargetDrop.Plan(
            SuppressionTargetCandidateFactory.FromRows(rows), _mode(), _existing(), _canEdit());

        if (plan.CanAdd) _accept(plan);

        var line = SuppressionTargetDrop.ResultLine(plan);
        Completed?.Invoke(line);
        return line;
    }
}
