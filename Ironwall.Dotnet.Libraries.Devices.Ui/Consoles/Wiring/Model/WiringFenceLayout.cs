using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>
/// 결선 창이 편집하는 펜스 구성(fence-wiring-editor FR-01 · FR-07 · FR-10) — 망 목록 · 센서 자리(보드 키 → 자리) · 번호 대역. <b>불변</b>이라
/// 되돌리기 장면에 그대로 담긴다. 자리의 <see cref="SensorMountSpec.FacesBack"/> 은 편집 중 늘 거짓이다(보는 쪽의 정본은 보드 줄 · 서버).
/// </summary>
public sealed class WiringFenceLayout
{
    /// <summary>펜스 모델이 꺼진 보드(시험 · 옛 경로) — 체인 편집이 자리를 건드리지 않는다.</summary>
    public static readonly WiringFenceLayout None = new(Array.Empty<FencePanelSpec>(), new Dictionary<int, SensorMountSpec>(), null, false, false,
                                                        FenceControllerEnd.Left, null);

    private FenceGeometry? _geometry;

    private WiringFenceLayout(IReadOnlyList<FencePanelSpec> panels, IReadOnlyDictionary<int, SensorMountSpec> mounts, NumberBandSet? bands,
                              bool isActive, bool isProposed, FenceControllerEnd controllerEnd, int? vbusGap)
    {
        Panels = panels;
        Mounts = mounts;
        Bands = bands;
        IsActive = isActive;
        IsProposed = isProposed;
        ControllerEnd = controllerEnd;
        VbusGap = vbusGap;
    }

    /// <summary>켜진 구성을 만든다 — 망 값은 범위 안으로, 자리는 망에 맞춘다.</summary>
    public static WiringFenceLayout Create(IEnumerable<FencePanelSpec> panels, IReadOnlyDictionary<int, SensorMountSpec> mounts, NumberBandSet? bands,
                                           bool isProposed = false, FenceControllerEnd controllerEnd = FenceControllerEnd.Left, int? vbusGap = null)
    {
        var list = (panels ?? Enumerable.Empty<FencePanelSpec>()).Where(p => p is not null).Select(p => p.Normalized()).ToList();
        return new WiringFenceLayout(list, NormalizeMounts(mounts, list), bands, true, isProposed, controllerEnd, vbusGap);
    }

    /// <summary>제어기(<c>C</c>)가 펜스 어느 끝에 있는가(FR-19) — 줄마다 사슬이 가는 쪽 · 번호 방향을 정한다.</summary>
    public FenceControllerEnd ControllerEnd { get; }

    /// <summary>VBus 표지 틈(FR-21 · 스마트 복합센서2 링만) — 저장한 값. 없으면 기본(가운데).</summary>
    public int? VbusGap { get; }

    /// <summary>그 센서의 줄(자리가 없으면 아래 줄).</summary>
    public FenceLane LaneOf(int key) => MountOf(key)?.Lane ?? FenceLane.Lower;

    /// <summary>위 줄에 센서가 있는가 — 없으면 위 줄은 리턴선뿐(형상 ②).</summary>
    public bool HasUpperSensors => Mounts.Values.Any(m => m.Lane == FenceLane.Upper);

    /// <summary>펜스 모델이 켜져 있는가.</summary>
    public bool IsActive { get; }

    /// <summary>저장된 구성이 없어 지금 배치에서 <b>제안</b>한 것인가(저장하면 걷힌다).</summary>
    public bool IsProposed { get; }

    /// <summary>망 목록 — Ch1(A) 쪽 끝이 첫 칸.</summary>
    public IReadOnlyList<FencePanelSpec> Panels { get; }

    /// <summary>체인 센서의 자리(보드 키 → 자리). 미배치 센서는 없다.</summary>
    public IReadOnlyDictionary<int, SensorMountSpec> Mounts { get; }

    /// <summary>번호 대역 — 없으면 번호를 자동으로 매기지 않는다.</summary>
    public NumberBandSet? Bands { get; }

    /// <summary>펼친 모양(m) — 한 번 계산해 둔다.</summary>
    public FenceGeometry Geometry => _geometry ??= FenceLayoutMath.Geometry(Panels);

    /// <summary>그 센서의 자리(없으면 <c>null</c>).</summary>
    public SensorMountSpec? MountOf(int key) => Mounts.TryGetValue(key, out var m) ? m : null;

    #region - With -
    public WiringFenceLayout WithPanels(IEnumerable<FencePanelSpec> panels) => IsActive ? Create(panels, Mounts, Bands, IsProposed, ControllerEnd, VbusGap) : this;

    public WiringFenceLayout WithMounts(IReadOnlyDictionary<int, SensorMountSpec> mounts) => IsActive ? Create(Panels, mounts, Bands, IsProposed, ControllerEnd, VbusGap) : this;

    public WiringFenceLayout With(IEnumerable<FencePanelSpec> panels, IReadOnlyDictionary<int, SensorMountSpec> mounts)
        => IsActive ? Create(panels, mounts, Bands, IsProposed, ControllerEnd, VbusGap) : this;

    public WiringFenceLayout WithBands(NumberBandSet? bands) => IsActive ? new WiringFenceLayout(Panels, Mounts, bands, true, IsProposed, ControllerEnd, VbusGap) : this;

    /// <summary>제어기 위치를 바꾼다(자리는 그대로 — 사슬 방향이 뒤집힌다).</summary>
    public WiringFenceLayout WithControllerEnd(FenceControllerEnd end) => IsActive ? new WiringFenceLayout(Panels, Mounts, Bands, true, IsProposed, end, VbusGap) : this;

    /// <summary>VBus 표지 틈을 바꾼다(<c>null</c> = 기본).</summary>
    public WiringFenceLayout WithVbusGap(int? gap) => IsActive ? new WiringFenceLayout(Panels, Mounts, Bands, true, IsProposed, ControllerEnd, gap) : this;

    /// <summary>제안 표지를 걷는다(저장했다).</summary>
    public WiringFenceLayout Accepted() => IsProposed ? new WiringFenceLayout(Panels, Mounts, Bands, IsActive, false, ControllerEnd, VbusGap) : this;
    #endregion

    /// <summary>
    /// 내용이 같은가(망 · 자리 · 대역) — "로컬 저장할 것이 있나"의 판정. 제안 표지는 보지 않는다.
    /// </summary>
    public bool SameContent(WiringFenceLayout? other)
        => other is not null
           && IsActive == other.IsActive
           && Panels.SequenceEqual(other.Panels)
           && Mounts.Count == other.Mounts.Count
           && Mounts.All(p => other.Mounts.TryGetValue(p.Key, out var m) && m == p.Value)
           && Equals(Bands, other.Bands)
           && ControllerEnd == other.ControllerEnd
           && VbusGap == other.VbusGap;

    private static IReadOnlyDictionary<int, SensorMountSpec> NormalizeMounts(IReadOnlyDictionary<int, SensorMountSpec>? mounts, IReadOnlyList<FencePanelSpec> panels)
        => (mounts ?? new Dictionary<int, SensorMountSpec>())
            .Where(p => p.Value is not null)
            .ToDictionary(p => p.Key, p => FenceLayoutMath.Normalize(p.Value with { FacesBack = false }, panels));
}
