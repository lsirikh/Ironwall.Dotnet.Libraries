using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 두 줄(레인) 결선 형상(fence-wiring-editor v0.3 §1-0b · FR-18 ~ FR-21) — 센서 줄 · 제어기 위치 · VBus 표지 · 개념도의 줄 안 · 줄 사이 옮기기.
/// </summary>
/// <remarks>
/// 판정은 순수 함수(<see cref="FenceLayoutMath.ChainOrder"/> · <see cref="FenceLayoutMath.NumberingOrder"/> · <see cref="FenceLayoutMath.PlaceBetween"/>)와
/// 보드(<see cref="WiringBoard.SetLanes"/> · <see cref="WiringBoard.SetControllerEnd"/>)에 있다 — 여기서는 되돌리기 한 걸음을 찍고 상태 줄을 쓴다.
/// </remarks>
public sealed partial class WiringViewModel
{
    /// <summary>줄 이름 — "아래 줄" · "위 줄".</summary>
    public static string LaneText(FenceLane lane) => lane == FenceLane.Upper ? "위 줄" : "아래 줄";

    /// <summary>제어기 위치 이름 — "왼쪽 끝" · "오른쪽 끝".</summary>
    public static string ControllerEndText(FenceControllerEnd end) => end == FenceControllerEnd.Right ? "오른쪽 끝" : "왼쪽 끝";

    #region - Lane pane (FR-18) -
    /// <summary>고른 센서의 줄이 모두 같으면 그것.</summary>
    public FenceLane? SelectedLane
        => MountTargets().Select(_board.FenceLayout.LaneOf).Distinct().ToList() is { Count: 1 } one ? one[0] : null;

    public bool IsLaneLower => SelectedLane == FenceLane.Lower;
    public bool IsLaneUpper => SelectedLane == FenceLane.Upper;

    /// <summary>[아래 줄] — 고른 센서 모두(되돌리기 한 걸음).</summary>
    public bool FenceSetLaneLower() => FenceSetLane(MountTargets(), FenceLane.Lower);

    /// <summary>[위 줄] — 고른 센서 모두(되돌리기 한 걸음).</summary>
    public bool FenceSetLaneUpper() => FenceSetLane(MountTargets(), FenceLane.Upper);

    /// <summary>
    /// 센서들의 줄을 바꾼다(속성 칸 · 메뉴 "위 줄로 / 아래 줄로" · 개념도 Alt+↑/↓) — 펜스 위 자리(망 · 기둥)는 그대로, 사슬 · 번호가 따라간다.
    /// </summary>
    public bool FenceSetLane(IReadOnlyList<int> keys, FenceLane lane)
    {
        if (IsBusy || !_board.FenceLayout.IsActive) return false;
        var targets = (keys ?? Array.Empty<int>()).Where(k => _board.FenceLayout.MountOf(k) is not null).Distinct().ToList();
        if (targets.Count == 0) return false;
        if (targets.All(k => _board.FenceLayout.LaneOf(k) == lane))
        {
            StatusText = $"이미 {LaneText(lane)}입니다 — 바뀐 것이 없습니다.";
            return false;
        }
        _board.PushUndo();
        if (!_board.SetLanes(targets, lane))
        {
            _board.Undo();
            StatusText = "바뀐 것이 없습니다.";
            return false;
        }
        SyncAll();
        StatusText = $"{(targets.Count > 1 ? $"{targets.Count}대" : _board.Find(targets[0])?.Display)} → {LaneText(lane)} · Ctrl+Z 로 되돌립니다";
        return true;
    }
    #endregion

    #region - Controller end (FR-19) -
    /// <summary>제어기(<c>C</c>)가 펜스 어느 끝에 있는가.</summary>
    public FenceControllerEnd FenceControllerEnd => _board.FenceLayout.ControllerEnd;

    public bool IsControllerLeft => FenceControllerEnd == FenceControllerEnd.Left;
    public bool IsControllerRight => FenceControllerEnd == FenceControllerEnd.Right;

    /// <summary>[왼쪽 끝] — 속성 칸 「제어기 위치」.</summary>
    public bool ChooseControllerLeft() => SetFenceControllerEnd(FenceControllerEnd.Left);

    /// <summary>[오른쪽 끝] — 속성 칸 「제어기 위치」.</summary>
    public bool ChooseControllerRight() => SetFenceControllerEnd(FenceControllerEnd.Right);

    /// <summary>제어기를 반대쪽 끝으로(개념도에서 <c>C</c> 를 끌어 놓을 때).</summary>
    public bool FlipControllerEnd() => SetFenceControllerEnd(IsControllerLeft ? FenceControllerEnd.Right : FenceControllerEnd.Left);

    /// <summary>
    /// 제어기 위치를 바꾼다 — 자리는 그대로, 줄마다 사슬이 가는 쪽이 뒤집히고 번호가 다시 매겨진다(대역이 있으면 · Draft · 되돌리기 한 걸음).
    /// </summary>
    public bool SetFenceControllerEnd(FenceControllerEnd end)
    {
        if (IsBusy || !_board.FenceLayout.IsActive) return false;
        if (_board.FenceLayout.ControllerEnd == end)
        {
            StatusText = $"제어기는 이미 {ControllerEndText(end)}에 있습니다.";
            return false;
        }
        _board.PushUndo();
        if (!_board.SetControllerEnd(end))
        {
            _board.Undo();
            return false;
        }
        SyncAll();
        StatusText = $"제어기 위치 — {ControllerEndText(end)} · 사슬은 {ControllerEndText(end)}에서 아래 줄로 나가 위 줄로 돌아옵니다 · Ctrl+Z 로 되돌립니다";
        return true;
    }
    #endregion

    #region - VBus (FR-21) -
    /// <summary>VBus 표지를 그리는가 — 스마트 복합센서2 링(센서 2대 이상)만.</summary>
    public bool HasVbus => _board.FenceLayout.IsActive && _board.Chain.Count >= 2
                           && _board.Chain.Keys.Any(k => WiringTopology.ParseSensorType(_board.Find(k)?.Facts.TypeText) is EnumDeviceType.SmartSensor2 or EnumDeviceType.SmartMultisensor2);

    /// <summary>VBus 표지 틈(사슬 · k = k번째 센서 뒤).</summary>
    public int VbusGap => _board.VbusGap;

    /// <summary>VBus 표지를 옮긴다(표시 전용 · 서버와 무관 · 되돌리기 한 걸음).</summary>
    public bool SetVbusGap(int gap)
    {
        if (IsBusy || !HasVbus) return false;
        _board.PushUndo();
        if (!_board.SetVbusGap(gap))
        {
            _board.Undo();
            return false;
        }
        SyncAll();
        StatusText = $"VBus 표지를 {VbusGap}번째 센서 뒤로 옮겼습니다 — 표시 전용(서버에 싣지 않음) · Ctrl+Z 로 되돌립니다";
        return true;
    }
    #endregion

    #region - Concept lane moves (FR-20) -
    /// <summary>그 줄의 센서 — 왼쪽 → 오른쪽(펜스 위 위치 순).</summary>
    public IReadOnlyList<int> LaneKeysLeftToRight(FenceLane lane)
    {
        var keys = _board.Chain.Keys.Where(k => _board.FenceLayout.LaneOf(k) == lane).ToList();
        if (FenceLayoutMath.LaneDirection(lane, _board.FenceLayout.ControllerEnd) < 0) keys.Reverse();
        return keys;
    }

    /// <summary>
    /// 개념도에서 끌어 놓기 — 센서를 <paramref name="lane"/> 의 왼쪽에서 <paramref name="index"/> 번째 틈(끄는 센서를 뺀 목록 기준)으로.
    /// 이웃 사이 펜스 자리로 옮기고(자리 종류 · 높이 · 방향은 센서의 것) 사슬 · 번호가 따라간다. 줄이 다르면 줄도 바뀐다. 되돌리기 한 걸음.
    /// </summary>
    public bool ConceptLaneDrop(IReadOnlyList<int> keys, FenceLane lane, int index)
    {
        if (IsBusy || !_board.FenceLayout.IsActive || keys is null || keys.Count == 0) return false;
        var moving = keys.Where(k => _board.Find(k) is not null).Distinct().ToList();
        if (moving.Count == 0) return false;
        var layout = _board.FenceLayout;
        var others = LaneKeysLeftToRight(lane).Where(k => !moving.Contains(k)).ToList();
        index = Math.Clamp(index, 0, others.Count);

        var mounts = new Dictionary<int, SensorMountSpec>(layout.Mounts);
        IReadOnlyList<FencePanelSpec> panels = layout.Panels;
        SensorMountSpec? left = index > 0 ? mounts[others[index - 1]] : null;
        SensorMountSpec? right = index < others.Count ? mounts[others[index]] : null;
        foreach (var key in moving)
        {
            var category = _board.CategoryOf(key);
            var own = (mounts.TryGetValue(key, out var m) ? m : new SensorMountSpec(0, FenceLayoutMath.DefaultSpotFor(category))) with { Lane = lane };
            var (placed, grown) = FenceLayoutMath.PlaceBetween(own, left, right, category, panels);
            panels = grown;
            mounts[key] = placed;
            left = placed;
        }

        // 같은 자리끼리의 차례 — 그 줄은 원하는 왼쪽 → 오른쪽 순서, 다른 줄은 지금 그대로, 사슬 방향으로 엮는다.
        var spatial = others.Take(index).Concat(moving).Concat(others.Skip(index)).ToList();
        var lower = lane == FenceLane.Lower ? spatial : LaneKeysLeftToRight(FenceLane.Lower).Where(k => !moving.Contains(k)).ToList();
        var upper = lane == FenceLane.Upper ? spatial : LaneKeysLeftToRight(FenceLane.Upper).Where(k => !moving.Contains(k)).ToList();
        var end = layout.ControllerEnd;
        var tie = Directed(lower, FenceLane.Lower).Concat(Directed(upper, FenceLane.Upper)).ToList();
        var name = moving.Count > 1 ? $"{moving.Count}대" : _board.Find(moving[0])?.Display;
        var ok = EditFence(l => l.With(panels, mounts), tie);
        StatusText = ok ? $"옮김 — {name}: {LaneText(lane)} {index + 1}번째 자리 · {MountTextOf(moving[0])} · Ctrl+Z 로 되돌립니다" : "제자리 — 바뀐 것이 없습니다.";
        return ok;

        IEnumerable<int> Directed(List<int> leftToRight, FenceLane l)
            => FenceLayoutMath.LaneDirection(l, end) > 0 ? leftToRight : Enumerable.Reverse(leftToRight);
    }

    /// <summary>끄는 동안 알약 글자 — "위 줄 3번째 자리".</summary>
    public string ConceptLaneDropLabel(IReadOnlyList<int> keys, FenceLane lane, int index)
        => $"{LaneText(lane)} {Math.Max(0, index) + 1}번째 자리" + (keys is { Count: > 1 } ? $" · {keys.Count}대" : string.Empty);

    /// <summary>
    /// 키보드 대신(Alt+←/→) — 센서를 그 줄 안에서 왼쪽 · 오른쪽 이웃 너머로 한 칸. 끝이면 <c>false</c>.
    /// </summary>
    public bool ConceptLaneStep(int key, int direction)
    {
        if (direction == 0 || _board.FenceLayout.MountOf(key) is not { } mount) return false;
        var lane = mount.Lane;
        var row = LaneKeysLeftToRight(lane).ToList();
        var at = row.IndexOf(key);
        var target = direction < 0 ? at - 1 : at + 1;
        if (at < 0 || target < 0 || target > row.Count - 1)
        {
            StatusText = "더 옮길 자리가 없습니다 — 줄의 끝입니다.";
            return false;
        }
        return ConceptLaneDrop(new[] { key }, lane, target);
    }

    /// <summary>키보드 대신(Alt+↑/↓) — 위 줄 · 아래 줄로(자리는 그대로).</summary>
    public bool ConceptLaneChange(int key, FenceLane lane) => FenceSetLane(FenceDragKeys(key), lane);
    #endregion

    private void RaiseLanePane()
    {
        foreach (var name in new[]
        {
            nameof(SelectedLane), nameof(IsLaneLower), nameof(IsLaneUpper), nameof(FenceControllerEnd), nameof(IsControllerLeft), nameof(IsControllerRight),
            nameof(HasVbus), nameof(VbusGap),
        }) NotifyOfPropertyChange(name);
    }
}
