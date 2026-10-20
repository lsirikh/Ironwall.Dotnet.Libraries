using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/// <summary>직접 그린 건물 한 채의 상태 — 윤곽(위경도) · 높이 · 층수 · 이름 · 지붕 모양 · 외벽 성격. null 이면 "없음"(지운 상태).</summary>
public sealed record UserBuildingState(IReadOnlyList<UbPoint> Outline, double Height, int? Floors, string? Name)
{
    public string Roof { get; init; } = Map3DUserBuilding.RoofFlat;
    public string Use { get; init; } = Map3DUserBuilding.UseAuto;
    /// <summary>외관 — 외벽 색 · 지붕 색 · 외벽 무늬(Hidden 은 쓰지 않음).</summary>
    public Map3DLookState Look { get; init; } = Map3DLookState.Auto;
}

/****************************************************************************
   Purpose      : 직접 그린 건물 넣기 · 고치기 · 지우기 Undo 1단위 — map-3d 에 그 건물을 앞(before) · 뒤(after) 상태로 다시 저장한다
                  (상태 null = 지움). GIS 편집 기록(IUndoService) 관용구 — ImageCropCommand 처럼 적용 함수를 받는다.
   Note         : 설명은 Map3DUserBuilding.UndoPrefix 로 시작한다 — 그리기 중 Ctrl+Z 가 이 기록만 되돌린다.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class UserBuildingCommand : IUndoableCommand
{
    private readonly Func<string, string, UserBuildingState?, CancellationToken, Task> _apply;
    private readonly Func<string, UbPoint, string>? _resolveJob;
    private readonly string _uid;
    private string _job;
    private readonly UserBuildingState? _before, _after;

    public UserBuildingCommand(string description, string job, string uid, UserBuildingState? before, UserBuildingState? after,
                               Func<string, string, UserBuildingState?, CancellationToken, Task> apply)
        : this(description, job, uid, before, after, apply, null) { }

    /// <param name="resolveJob">
    /// 적용할 때 그 건물이 든 영역의 지금 job 을 찾는다(옛 job, 건물 가운데 위경도 → job). 직접 그린 건물은 map-3d 가 job 이 아니라 영역 단위로
    /// 저장하므로 「다시 만들기」로 job 이 바뀌어도(옛 job 은 지워짐 — 404) 같은 영역의 새 job 으로 되돌린다. null 이면 처음 job 그대로.
    /// </param>
    public UserBuildingCommand(string description, string job, string uid, UserBuildingState? before, UserBuildingState? after,
                               Func<string, string, UserBuildingState?, CancellationToken, Task> apply, Func<string, UbPoint, string>? resolveJob)
    {
        Description = description;
        _job = job;
        _uid = uid;
        _before = before;
        _after = after;
        _apply = apply;
        _resolveJob = resolveJob;
    }

    public string Description { get; }
    public int ScopeMapId { get; set; }
    public Task ExecuteAsync(CancellationToken ct = default) => _apply(CurrentJob(), _uid, _after, ct);
    public Task UndoAsync(CancellationToken ct = default) => _apply(CurrentJob(), _uid, _before, ct);

    /// <summary>그 건물 영역의 지금 job(찾은 것은 기억해 다음 되돌리기 · 다시 하기의 출발점으로 쓴다).</summary>
    private string CurrentJob()
    {
        if (_resolveJob != null && (_after ?? _before) is { Outline.Count: > 0 } s) _job = _resolveJob(_job, Map3DUserBuilding.Center(s.Outline));
        return _job;
    }
}
