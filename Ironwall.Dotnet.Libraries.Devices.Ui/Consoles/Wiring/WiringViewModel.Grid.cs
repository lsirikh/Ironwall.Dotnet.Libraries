using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 9점 격자 배치(2026-10-01 · 사용자: "1개의 판망을 중심으로 9개의 포인트로 하단 · 중단 · 상단 3포인트로 이동 · 끌면 빨강 포인트 안내 · 스냅 · 멀티셀렉트로 함께") —
/// 끌기 · Ctrl+←/→ · 속성 칸 3×3 고르개가 부르는 같은 길. 판단은 순수 함수(<see cref="FenceLayoutMath.GridPoints"/> · <see cref="FenceLayoutMath.PlanGridMove"/> ·
/// <see cref="FenceLayoutMath.StepGridX"/>)에 있고 여기서는 되돌리기 한 걸음 · 상태 줄을 쓴다.
/// </summary>
public sealed partial class WiringViewModel
{
    /// <summary>그 센서가 놓일 수 있는 격자 점(갈래별 · 끌기의 빨강 점).</summary>
    public IReadOnlyList<FenceGridPoint> FenceGridPoints(int key)
        => _board.FenceLayout.IsActive ? FenceLayoutMath.GridPoints(_board.FenceLayout.Panels, _board.CategoryOf(key)) : Array.Empty<FenceGridPoint>();

    /// <summary>다른 센서가 이미 있는 점(칸 · 줄) — <paramref name="excluding"/>(옮기는 센서)는 뺀다.</summary>
    public IReadOnlyCollection<(FenceGridCell Cell, FenceLane Lane)> FenceOccupiedCells(IEnumerable<int> excluding)
    {
        var skip = (excluding ?? Enumerable.Empty<int>()).ToHashSet();
        var layout = _board.FenceLayout;
        return _board.Chain.Keys.Where(k => !skip.Contains(k) && layout.MountOf(k) is not null)
            .Select(k => FenceLayoutMath.Normalize(layout.MountOf(k)!, layout.Panels))
            .Select(m => (FenceLayoutMath.GridOf(m), m.Lane)).ToHashSet();
    }

    /// <summary>센서의 지금 격자 칸(자리가 없으면 없음).</summary>
    public FenceGridCell? FenceGridCellOf(int key)
        => _board.FenceLayout.MountOf(key) is { } m ? FenceLayoutMath.GridOf(FenceLayoutMath.Normalize(m, _board.FenceLayout.Panels)) : null;

    /// <summary>
    /// 끌기 계획 — 잡은 센서를 <paramref name="target"/> 로, 함께 끈 센서는 같은 Δ칸만큼. 갈 수 없는 센서는 <c>null</c>(보드는 그대로).
    /// </summary>
    public IReadOnlyDictionary<int, SensorMountSpec?> PlanFenceGridMove(IReadOnlyList<int> keys, int grabbedKey, FenceGridCell target)
    {
        var layout = _board.FenceLayout;
        var moving = (keys ?? Array.Empty<int>()).Append(grabbedKey).Distinct().Where(k => layout.MountOf(k) is not null)
            .ToDictionary(k => k, k => FenceLayoutMath.Normalize(layout.MountOf(k)!, layout.Panels));
        return FenceLayoutMath.PlanGridMove(moving, grabbedKey, target, _board.CategoryOf, layout.Panels, FenceOccupiedCells(moving.Keys));
    }

    /// <summary>
    /// 끌어 놓기(2D 맞추기) — 모든 센서가 갈 수 있을 때만 옮긴다(하나라도 못 가면 아무것도 바꾸지 않고 까닭을 말한다). 되돌리기 한 걸음 · 사슬 · 번호가 따라간다.
    /// </summary>
    public bool FenceMoveToGrid(IReadOnlyList<int> keys, int grabbedKey, FenceGridCell target)
    {
        if (IsBusy || !_board.FenceLayout.IsActive || _board.FenceLayout.MountOf(grabbedKey) is null) return false;
        var plan = PlanFenceGridMove(keys, grabbedKey, target);
        if (plan.Count == 0) return false;
        var blocked = plan.Count(p => p.Value is null);
        if (blocked > 0)
        {
            StatusText = $"놓지 않았습니다 — {blocked}대가 갈 점이 없거나 이미 센서가 있습니다(모두 갈 수 있을 때만 함께 옮깁니다).";
            return false;
        }
        if (plan.All(p => p.Value == FenceLayoutMath.Normalize(_board.FenceLayout.MountOf(p.Key)!, _board.FenceLayout.Panels)))
        {
            StatusText = "제자리 — 바뀐 것이 없습니다.";
            return false;
        }
        var from = FenceGridCellOf(grabbedKey)!.Value;
        var moving = plan.Keys.ToList();
        var rest = _board.Chain.Keys.Where(k => !moving.Contains(k)).ToList();
        var tie = target.Gx >= from.Gx ? rest.Concat(moving).ToList() : moving.Concat(rest).ToList();
        var ok = EditFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => plan.TryGetValue(p.Key, out var next) && next is not null ? next : p.Value)), tie);
        StatusText = ok ? $"옮김 — {(moving.Count > 1 ? $"{moving.Count}대" : _board.Find(grabbedKey)?.Display)}: {MountTextOf(grabbedKey)} · Ctrl+Z 로 되돌립니다"
                        : "제자리 — 바뀐 것이 없습니다.";
        return ok;
    }

    /// <summary>
    /// Ctrl+←/→ — 고른 센서를 저마다 다음 격자 점으로(망 오른쪽 열 다음은 다음 기둥 · 다음 망). 되돌리기 한 걸음.
    /// </summary>
    public bool FenceStepGrid(IReadOnlyList<int> keys, int direction)
    {
        if (IsBusy || !_board.FenceLayout.IsActive || direction == 0) return false;
        var layout = _board.FenceLayout;
        var targets = (keys ?? Array.Empty<int>()).Where(k => layout.MountOf(k) is not null).Distinct().ToList();
        if (targets.Count == 0) return false;
        var rest = _board.Chain.Keys.Where(k => !targets.Contains(k)).ToList();
        var tie = direction > 0 ? rest.Concat(targets).ToList() : targets.Concat(rest).ToList();
        var ok = EditFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => targets.Contains(p.Key) ? FenceLayoutMath.StepGridX(FenceLayoutMath.Normalize(p.Value, l.Panels), direction, _board.CategoryOf(p.Key), l.Panels) : p.Value)), tie);
        StatusText = ok ? $"옮김 — {(targets.Count > 1 ? $"{targets.Count}대" : _board.Find(targets[0])?.Display)}: {MountTextOf(targets[0])} · Ctrl+Z 로 되돌립니다"
                        : "더 옮길 점이 없습니다 — 끝입니다.";
        return ok;
    }

    #region - 3×3 picker (속성 칸) -
    /// <summary>
    /// 3×3 고르개 — 고른 센서를 저마다 <b>제 망</b>(기둥 센서는 오른쪽 망 · 끝 기둥이면 왼쪽 망)의 그 점으로. 갈 수 없는 센서(갈래)는 건너뛴다. 되돌리기 한 걸음.
    /// </summary>
    /// <param name="row">0 = 하단 · 1 = 중단 · 2 = 상단.</param>
    public bool FenceSetGridPoint(FenceColumn column, int row)
    {
        var targets = MountTargets();
        if (IsBusy || targets.Count == 0 || row is < 0 or > 2) return false;
        var layout = _board.FenceLayout;
        var n = layout.Panels.Count;
        if (n == 0) return false;
        var next = new Dictionary<int, SensorMountSpec>();
        foreach (var key in targets)
        {
            var m = FenceLayoutMath.Normalize(layout.MountOf(key)!, layout.Panels);
            var panel = m.IsPostSpot ? Math.Min(m.Panel, n - 1) : m.Panel;
            var gx = FenceLayoutMath.GRID_PER_PANEL * panel + 1 + (column switch { FenceColumn.Left => 0, FenceColumn.Right => 2, _ => 1 });
            if (FenceLayoutMath.MountAt(new FenceGridCell(gx, row), m, _board.CategoryOf(key), layout.Panels) is { } placed) next[key] = placed;
        }
        if (next.Count == 0)
        {
            StatusText = "그 점에 놓을 수 있는 센서가 없습니다.";
            return false;
        }
        var ok = EditFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key, p => next.TryGetValue(p.Key, out var v) ? v : p.Value)));
        StatusText = ok ? $"설치 위치 — {(next.Count > 1 ? $"{next.Count}대" : _board.Find(next.Keys.First())?.Display)}: {MountTextOf(next.Keys.First())} · Ctrl+Z 로 되돌립니다"
                        : "바뀐 것이 없습니다.";
        return ok;
    }

    /// <summary>고른 첫 센서의 격자 점(망 칸 안일 때) — 고르개의 켜짐 표시.</summary>
    private (FenceColumn Column, int Row)? SelectedGridPoint
        => MountTargets().FirstOrDefault() is var k && _board.FenceLayout.MountOf(k) is { } m && FenceLayoutMath.Normalize(m, _board.FenceLayout.Panels) is { IsPanelSpot: true } p
           && p.Spot != FenceMountSpot.RazorCoil
            ? (p.Column, FenceLayoutMath.RowOf(p.Spot))
            : null;

    private bool IsGrid(FenceColumn column, int row) => SelectedGridPoint is { } g && g.Column == column && g.Row == row;

    public bool IsGridLeftTop => IsGrid(FenceColumn.Left, 2);
    public bool IsGridCenterTop => IsGrid(FenceColumn.Center, 2);
    public bool IsGridRightTop => IsGrid(FenceColumn.Right, 2);
    public bool IsGridLeftMiddle => IsGrid(FenceColumn.Left, 1);
    public bool IsGridCenterMiddle => IsGrid(FenceColumn.Center, 1);
    public bool IsGridRightMiddle => IsGrid(FenceColumn.Right, 1);
    public bool IsGridLeftBottom => IsGrid(FenceColumn.Left, 0);
    public bool IsGridCenterBottom => IsGrid(FenceColumn.Center, 0);
    public bool IsGridRightBottom => IsGrid(FenceColumn.Right, 0);

    private void RaiseGridPicker()
    {
        foreach (var name in new[]
        {
            nameof(IsGridLeftTop), nameof(IsGridCenterTop), nameof(IsGridRightTop), nameof(IsGridLeftMiddle), nameof(IsGridCenterMiddle),
            nameof(IsGridRightMiddle), nameof(IsGridLeftBottom), nameof(IsGridCenterBottom), nameof(IsGridRightBottom),
        }) NotifyOfPropertyChange(name);
    }
    #endregion
}
