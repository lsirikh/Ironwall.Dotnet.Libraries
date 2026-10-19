using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;

/// <summary>직접 그린 건물 한 채의 상태 — 윤곽(위경도) · 높이 · 층수 · 이름. null 이면 "없음"(지운 상태).</summary>
public sealed record UserBuildingState(IReadOnlyList<UbPoint> Outline, double Height, int? Floors, string? Name);

/****************************************************************************
   Purpose      : 직접 그린 건물 넣기 · 고치기 · 지우기 Undo 1단위 — map-3d 에 그 건물을 앞(before) · 뒤(after) 상태로 다시 저장한다
                  (상태 null = 지움). GIS 편집 기록(IUndoService) 관용구 — ImageCropCommand 처럼 적용 함수를 받는다.
   Note         : 설명은 Map3DUserBuilding.UndoPrefix 로 시작한다 — 그리기 중 Ctrl+Z 가 이 기록만 되돌린다.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public sealed class UserBuildingCommand : IUndoableCommand
{
    private readonly Func<string, string, UserBuildingState?, CancellationToken, Task> _apply;
    private readonly string _job, _uid;
    private readonly UserBuildingState? _before, _after;

    public UserBuildingCommand(string description, string job, string uid, UserBuildingState? before, UserBuildingState? after,
                               Func<string, string, UserBuildingState?, CancellationToken, Task> apply)
    {
        Description = description;
        _job = job;
        _uid = uid;
        _before = before;
        _after = after;
        _apply = apply;
    }

    public string Description { get; }
    public int ScopeMapId { get; set; }
    public Task ExecuteAsync(CancellationToken ct = default) => _apply(_job, _uid, _after, ct);
    public Task UndoAsync(CancellationToken ct = default) => _apply(_job, _uid, _before, ct);
}
