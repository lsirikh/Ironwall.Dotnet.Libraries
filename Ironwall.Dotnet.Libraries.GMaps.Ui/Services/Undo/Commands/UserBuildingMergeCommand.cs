using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 직접 그린 건물 「합치기」 Undo 1단위 — 되돌리기 = 합친 건물을 지우고 원래 건물들을 같은 uid · 윤곽 · 높이 · 층수 · 이름 · 지붕 ·
                  외벽 그대로 다시 저장, 다시 하기 = 같은 uid 들로 다시 합치기(map-3d 가 새 uid 를 주면 그것을 다음 되돌리기에 쓴다).
   Note         : 설명은 Map3DUserBuilding.UndoPrefix 로 시작한다 — 그리기 중 Ctrl+Z 가 이 기록만 되돌린다. 적용 함수는 MapViewModel 이 준다
                  (실패는 예외 — 편집 기록이 그 칸을 남긴다). job 은 UserBuildingCommand 와 같이 적용할 때 그 영역의 지금 job 으로 찾는다.
   Created On   : 2026-10-20 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class UserBuildingMergeCommand : IUndoableCommand
{
    private readonly IReadOnlyList<(string Uid, UserBuildingState State)> _members;
    private readonly Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> _merge;
    private readonly Func<string, string, IReadOnlyList<(string Uid, UserBuildingState State)>, CancellationToken, Task> _unmerge;
    private readonly Func<string, UbPoint, string>? _resolveJob;
    private string _job;
    private string _mergedUid;

    /// <param name="members">합치기 전 원래 건물들(uid · 상태).</param>
    /// <param name="mergedUid">처음 합친 건물 uid.</param>
    /// <param name="merge">(job, 원래 uid 들) → 합친 건물 uid.</param>
    /// <param name="unmerge">(job, 합친 uid, 원래 건물들) — 합친 것을 지우고 원래 것을 다시 저장.</param>
    /// <param name="resolveJob">옛 job · 건물 가운데 → 그 영역의 지금 job(null = 처음 job 그대로).</param>
    public UserBuildingMergeCommand(string description, string job, IReadOnlyList<(string Uid, UserBuildingState State)> members, string mergedUid,
                                    Func<string, IReadOnlyList<string>, CancellationToken, Task<string>> merge,
                                    Func<string, string, IReadOnlyList<(string Uid, UserBuildingState State)>, CancellationToken, Task> unmerge,
                                    Func<string, UbPoint, string>? resolveJob = null)
    {
        Description = description;
        _job = job;
        _members = members;
        _mergedUid = mergedUid;
        _merge = merge;
        _unmerge = unmerge;
        _resolveJob = resolveJob;
    }

    public string Description { get; }
    public int ScopeMapId { get; set; }
    /// <summary>지금 합친 건물 uid(다시 하기로 바뀔 수 있다).</summary>
    public string MergedUid => _mergedUid;

    public async Task ExecuteAsync(CancellationToken ct = default)
        => _mergedUid = await _merge(CurrentJob(), _members.Select(m => m.Uid).ToList(), ct);

    public Task UndoAsync(CancellationToken ct = default) => _unmerge(CurrentJob(), _mergedUid, _members, ct);

    private string CurrentJob()
    {
        if (_resolveJob != null && _members.Count > 0 && _members[0].State.Outline.Count > 0)
            _job = _resolveJob(_job, Map3DUserBuilding.Center(_members[0].State.Outline));
        return _job;
    }
}
