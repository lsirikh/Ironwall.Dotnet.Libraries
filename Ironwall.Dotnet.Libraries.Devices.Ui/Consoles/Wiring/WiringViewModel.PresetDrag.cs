using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 결선 펜스 보기의 붙는 점 하나(sensor-mount-preset W4) — 칸 · 점 · 붙을 수 있나 · 펜스를 따라 잰 가로(m).
/// </summary>
/// <param name="Bay">칸(망) 번호.</param>
/// <param name="Point">붙는 점(이름 · 갈래 · 칸 안 몫 · 높이 · 펜스와 거리).</param>
/// <param name="Allowed">이 센서가 붙을 수 있나(모델 허용 갈래 · 지중 여부) — 아니면 흐리게 · 붙지 않음.</param>
/// <param name="XM">펜스 시작에서 잰 가로(m).</param>
public sealed record FencePresetSnapPoint(int Bay, SensorSnapPoint Point, bool Allowed, double XM)
{
    /// <summary>"기둥 B · 중간 · 1.8 m 에 붙음".</summary>
    public string AttachText => Point.AttachText;
}

/// <summary>
/// 결선 펜스 보기에서 위치 프리셋으로 서는 센서를 끌기 · 키보드로 옮긴다(sensor-mount-preset W4 · 규칙표 "놓일 자리 표시").
/// 끄는 동안 그 칸의 붙는 점을 보이고 가장 가까운 허용 점에 붙는다 — 놓은 자리는 "따로 고친 값"으로 남고 [프리셋으로] 가 지운다.
/// 판단은 순수 함수(<see cref="WiringPresetPlan.MoveTo"/> · <see cref="WiringPresetPlan.SnapChoices"/>)에 있고 여기서는 되돌리기 한 걸음 · 상태 줄을 쓴다.
/// </summary>
public sealed partial class WiringViewModel
{
    /// <summary>위치 프리셋으로 서는 센서인가 — 끌면 9점 격자 대신 붙는 점을 쓴다.</summary>
    public bool IsPresetMounted(int key)
        => HasSensorPresets && _board.FenceLayout.IsActive && _board.FenceLayout.MountOf(key) is { MountPresetId: { } id } && MountPresetById(id) is not null;

    private SensorMountPreset? PresetOfKey(int key)
        => _board.FenceLayout.MountOf(key) is { MountPresetId: { } id } ? MountPresetById(id) : null;

    /// <summary>센서가 지금 그려지는 칸(망 번호) — 프리셋으로 서지 않으면 <c>null</c>.</summary>
    public int? FencePresetBayOf(int key)
    {
        if (!IsPresetMounted(key) || _board.FenceLayout.MountOf(key) is not { } mount || PresetOfKey(key) is not { } preset) return null;
        return SensorMountMigration.BayIndexOf(mount, _board.FenceLayout.Panels.Count, preset.AlongRatio);
    }

    /// <summary>펜스를 따라 잰 가로(m)가 드는 칸 — 펜스 밖이면 가장 가까운 끝 칸. 펜스 구성이 없으면 <c>null</c>.</summary>
    public int? FenceBayAt(double metres)
    {
        if (!_board.FenceLayout.IsActive || _board.FenceLayout.Geometry is not { Panels.Count: > 0 } geometry) return null;
        var panels = geometry.Panels;
        if (metres <= panels[0].StartM) return 0;
        for (var i = 0; i < panels.Count; i++)
            if (metres < panels[i].StartM + panels[i].SpanM) return i;
        return panels.Count - 1;
    }

    /// <summary>
    /// 칸 <paramref name="bay"/> 의 붙는 점 — 모두(붙을 수 없는 점은 <see cref="FencePresetSnapPoint.Allowed"/> = 거짓 · 흐리게). 철조망 없는 칸엔 철조망 점이 없다.
    /// 프리셋으로 서지 않는 센서 · 범위 밖 칸이면 빈 목록.
    /// </summary>
    public IReadOnlyList<FencePresetSnapPoint> FencePresetSnapPoints(int key, int bay)
    {
        if (!IsPresetMounted(key) || PresetOfKey(key) is not { } preset || _board.FenceLayout.Geometry is not { Panels.Count: > 0 } geometry
            || bay < 0 || bay >= geometry.Panels.Count) return Array.Empty<FencePresetSnapPoint>();
        var panel = geometry.Panels[bay];
        var model = _presetModels.FirstOrDefault(m => m.Id == ModelIdOfKey(key));
        return WiringPresetPlan.SnapChoices(FenceSampleBay.FromPanel(_board.FenceLayout.Panels[bay]), model, preset)
            .Select(c => new FencePresetSnapPoint(bay, c.Point, c.Allowed, panel.StartM + panel.SpanM * c.Point.AlongRatio))
            .ToList();
    }

    /// <summary>
    /// 놓기 — 센서를 칸 <paramref name="bay"/> 의 붙는 점 <paramref name="pointName"/> 에 붙인다(붙을 수 없는 점이면 아무것도 바꾸지 않는다). 되돌리기 한 걸음.
    /// </summary>
    public bool FenceSnapPreset(int key, int bay, string pointName)
    {
        if (IsBusy || FencePresetSnapPoints(key, bay).FirstOrDefault(p => p.Point.Name == pointName) is not { } target) return false;
        if (!target.Allowed)
        {
            StatusText = $"이 모델은 {target.Point.Label} 에 붙지 않습니다 — 진한 점에 놓아 주세요.";
            return false;
        }
        return MovePreset(key, bay, target.Point.AlongRatio, target.Point.HeightM, target.Point.Kind == SensorAnchorKind.Ground ? target.Point.DepthM : null,
                          target.AttachText);
    }

    /// <summary>
    /// 자유 위치(Alt 끌기) — 붙지 않고 칸 안 가로 <paramref name="alongM"/>(m) · 높이 <paramref name="heightM"/>(m) 로, 1 cm 단위. 되돌리기 한 걸음.
    /// </summary>
    public bool FenceMovePresetFree(int key, int bay, double alongM, double heightM)
    {
        if (IsBusy || !IsPresetMounted(key) || bay < 0 || bay >= _board.FenceLayout.Panels.Count) return false;
        var sample = FenceSampleBay.FromPanel(_board.FenceLayout.Panels[bay]).Normalized();
        var along = Math.Round(alongM * 100) / 100 / sample.PanelWidthM;
        var height = Math.Round(heightM * 100) / 100;
        return MovePreset(key, bay, along, height, null, $"자유 위치 · 가로 {Math.Clamp(along, 0, 1) * sample.PanelWidthM:0.00} m · 높이 {height:0.00} m");
    }

    /// <summary>
    /// 키보드 대안(Ctrl+←/→ · 끄는 중 Tab) — 다음 · 이전 허용 붙는 점으로. 칸 끝 점 다음은 옆 칸의 첫 점(펜스 끝이면 멈춘다). 되돌리기 한 걸음.
    /// </summary>
    public bool FenceStepPresetSnap(int key, int direction)
    {
        if (IsBusy || direction == 0 || FencePresetBayOf(key) is not { } bay || FencePoseOf(key) is not { } pose) return false;
        var next = NextPresetSnap(key, bay, pose.AlongRatio, pose.HeightM, direction);
        if (next is null)
        {
            StatusText = "더 옮길 붙는 점이 없습니다 — 끝입니다.";
            return false;
        }
        return FenceSnapPreset(key, next.Bay, next.Point.Name);
    }

    /// <summary>
    /// 지금 자리(칸 · 몫 · 높이)에서 다음 허용 붙는 점 — 칸 안 차례(가로 → 높이)대로, 끝이면 옆 칸. 순수 계산(보드는 그대로).
    /// </summary>
    public FencePresetSnapPoint? NextPresetSnap(int key, int bay, double alongRatio, double heightM, int direction)
    {
        var count = _board.FenceLayout.Panels.Count;
        if (direction == 0 || count == 0) return null;
        var step = Math.Sign(direction);
        static (double, double) Order(SensorSnapPoint p) => (p.AlongRatio, p.HeightM);
        var here = (alongRatio, heightM);
        for (var b = bay; b >= 0 && b < count; b += step)
        {
            var allowed = FencePresetSnapPoints(key, b).Where(p => p.Allowed).OrderBy(p => Order(p.Point)).ToList();
            if (step < 0) allowed.Reverse();
            foreach (var p in allowed)
            {
                if (b != bay) return p;
                var cmp = Compare(Order(p.Point), here);
                if (step > 0 ? cmp > 0 : cmp < 0) return p;
            }
        }
        return null;

        static int Compare((double A, double H) x, (double A, double H) y)
        {
            if (Math.Abs(x.A - y.A) > 1e-4) return x.A.CompareTo(y.A);
            if (Math.Abs(x.H - y.H) > 0.005) return x.H.CompareTo(y.H);
            return 0;
        }
    }

    /// <summary>
    /// 높이를 <paramref name="deltaM"/>(m)만큼 — 위치 프리셋 센서의 Alt+↑/↓(10 cm) · Shift+Alt+↑/↓(1 cm). 따로 고친 값으로 남는다. 프리셋 센서가 없으면 거짓.
    /// </summary>
    public bool FenceNudgePresetHeight(IReadOnlyList<int> keys, double deltaM)
    {
        var targets = (keys ?? Array.Empty<int>()).Where(IsPresetMounted).Distinct().ToList();
        if (IsBusy || targets.Count == 0 || !double.IsFinite(deltaM) || deltaM == 0) return false;
        var ok = EditFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => targets.Contains(p.Key) && p.Value.MountPresetId is { } id && MountPresetById(id) is { } preset
                ? WiringPresetPlan.WithOverride(p.Value, preset, l.Panels, pose => pose with { HeightM = Math.Round((pose.HeightM + deltaM) * 100) / 100 })
                : p.Value)));
        StatusText = ok
            ? $"설치 위치 — 높이 {(deltaM > 0 ? "+" : "−")}{Math.Abs(deltaM) * 100:0} cm{(targets.Count == 1 && FencePoseOf(targets[0]) is { } pose ? $" ({pose.HeightM:0.00} m)" : $" · {targets.Count}대")} (프리셋에서 바뀜)"
            : "더 올리거나 내릴 수 없습니다 — 기둥 꼭대기 · 바닥이 끝입니다.";
        RaisePresetPane();
        return ok;
    }

    private bool MovePreset(int key, int bay, double alongRatio, double heightM, double? depthM, string what)
    {
        if (_board.FenceLayout.MountOf(key) is not { } before || PresetOfKey(key) is not { } preset) return false;
        var fromBay = FencePresetBayOf(key) ?? bay;
        var rest = _board.Chain.Keys.Where(k => k != key).ToList();
        var tie = bay >= fromBay ? rest.Append(key).ToList() : rest.Prepend(key).ToList();
        var ok = EditFence(l => l.WithMounts(l.Mounts.ToDictionary(p => p.Key,
            p => p.Key == key ? WiringPresetPlan.MoveTo(p.Value, preset, l.Panels, bay, alongRatio, heightM, depthM) : p.Value)), tie);
        StatusText = ok ? $"설치 위치 — {_board.Find(key)?.Display}: 망 {bay + 1} · {what} (프리셋에서 바뀜 · [프리셋으로] 로 되돌림)"
                        : "제자리 — 바뀐 것이 없습니다.";
        RaisePresetPane();
        return ok;
    }

    /// <summary>펜스 보기가 그릴 "권장 간격 넘음" 쌍(<see cref="PresetSpacingAlerts"/>).</summary>
    public IReadOnlyList<FenceSpacingAlert> FenceSpacingAlerts()
        => PresetSpacingAlerts().Select(a => new FenceSpacingAlert(a.Left, a.Right, a.Metres, a.Limit)).ToList();
}
