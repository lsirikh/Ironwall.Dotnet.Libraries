using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/****************************************************************************
   Purpose      : 꾸미기 항목(나무 · 바닥 · 소품 · 담장) 넣기 · 고치기 · 다시 놓기 · 지우기 Undo 1단위 — map-3d 에 그 항목을 앞(before) · 뒤(after)
                  상태로 다시 저장한다(null = 지움). 직접 그린 건물(UserBuildingCommand)과 같은 관용구.
   Note         : 설명은 Map3DDecoration.UndoPrefix 로 시작한다 — 꾸미기 중 Ctrl+Z · Ctrl+Y 가 이 기록만 되돌린다.
                  꾸미기도 영역 단위로 저장되므로 「다시 만들기」로 job 이 바뀌면 항목 가운데를 덮는 지금 영역의 job 으로 적용한다.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class DecorationCommand : IUndoableCommand
{
    private readonly Func<string, string, Map3DDecorationItem?, CancellationToken, Task> _apply;
    private readonly Func<string, UbPoint, string>? _resolveJob;
    private readonly string _uid;
    private string _job;
    private readonly Map3DDecorationItem? _before, _after;

    public DecorationCommand(string description, string job, string uid, Map3DDecorationItem? before, Map3DDecorationItem? after,
                             Func<string, string, Map3DDecorationItem?, CancellationToken, Task> apply, Func<string, UbPoint, string>? resolveJob = null)
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

    private string CurrentJob()
    {
        if (_resolveJob != null && (_after ?? _before) is { Points.Count: > 0 } s) _job = _resolveJob(_job, Map3DUserBuilding.Center(s.Points));
        return _job;
    }
}
