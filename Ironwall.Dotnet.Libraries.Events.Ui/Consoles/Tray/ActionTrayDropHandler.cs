using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;

/// <summary>
/// 목록 행(뷰모델) → <see cref="ActionTrayCandidate"/> 로 옮긴다. 순수 판정(<see cref="ActionTrayDrop"/>)이
/// 뷰모델 타입을 모르도록 변환을 여기 한 곳에 모은다.
/// </summary>
public static class ActionTrayCandidateFactory
{
    public static ActionTrayCandidate? From(object? row) => row switch
    {
        DetectionEventViewModel detection => new ActionTrayCandidate(
            detection.Model?.Id ?? 0,
            ActionTrayDrop.KindDetection,
            Label(detection.DeviceLabel, detection.DateTime),
            detection.IsActionReported),

        MalfunctionEventViewModel malfunction => new ActionTrayCandidate(
            malfunction.Model?.Id ?? 0,
            ActionTrayDrop.KindMalfunction,
            Label(malfunction.DeviceLabel, malfunction.DateTime),
            malfunction.IsActionReported),

        // 연결 · 조치 행은 원본이 아니다 — 종류를 비워 두면 판정이 "원본이 아님" 으로 센다.
        ConnectionEventViewModel connection => new ActionTrayCandidate(
            connection.Model?.Id ?? 0, "connection", Label(connection.DeviceLabel, connection.DateTime), false),

        ActionEventViewModel action => new ActionTrayCandidate(
            action.Model?.Id ?? 0, "action", Label(action.User, action.DateTime), false),

        _ => null,
    };

    public static IReadOnlyList<ActionTrayCandidate> FromRows(IEnumerable<object> rows)
        => rows.Select(From).Where(c => c is not null).Select(c => c!.Value).ToList();

    private static string Label(string? who, DateTime when)
        => $"{(string.IsNullOrWhiteSpace(who) ? "(장비 없음)" : who)} · {when:yyyy-MM-dd HH:mm}";
}

/// <summary>
/// 조치 트레이 드롭존의 처리기. 드롭은 <b>서버를 부르지 않는다</b> — 고른 건마다 Draft 한 줄을 쌓는다.
/// </summary>
/// <remarks>
/// 키보드 폴백(툴바 [N건 조치보고])도 <see cref="Queue"/> 를 그대로 부른다 — 길이 둘이어도 담는 경로는 하나다.
/// </remarks>
public sealed class ActionTrayDropHandler : IDragDropHandler
{
    private readonly ActionTrayViewModel _tray;
    private readonly Func<bool> _canControl;

    public ActionTrayDropHandler(ActionTrayViewModel tray, Func<bool> canControl)
    {
        _tray = tray ?? throw new ArgumentNullException(nameof(tray));
        _canControl = canControl ?? throw new ArgumentNullException(nameof(canControl));
    }

    /// <summary>담기가 끝났다(담았든 못 담았든) — 상태 띠에 남길 한 줄.</summary>
    public event Action<string>? Completed;

    public bool CanDrop(DragPayload payload, DropTarget target)
    {
        if (target.ZoneKey != ActionTrayDrop.ZoneKey || _tray.IsApplying) return false;
        return ActionTrayDrop.Plan(ActionTrayCandidateFactory.FromRows(payload.Items), _canControl(), _tray.Count).CanQueue;
    }

    public void Drop(DragPayload payload, DropTarget target)
    {
        if (target.ZoneKey != ActionTrayDrop.ZoneKey) return;
        Queue(payload.Items);
    }

    /// <summary>드래그와 버튼이 함께 쓰는 단 하나의 담기 경로.</summary>
    public string Queue(IEnumerable<object> rows)
    {
        var plan = ActionTrayDrop.Plan(ActionTrayCandidateFactory.FromRows(rows), _canControl(), _tray.Count);
        var line = _tray.Enqueue(plan);
        Completed?.Invoke(line);
        return line;
    }
}
