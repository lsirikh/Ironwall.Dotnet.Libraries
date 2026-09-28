using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 뷰모델 — 첫 화면 · 개인 뷰 · 레이어 기억 · 검색 · 제대 칩 강조 (FR-15 · FR-16 · FR-26 · NFR-14)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public sealed partial class UnitMapViewModel
{
    /// <summary>개인 표시 설정의 <b>한 키</b>(ISSUE-53) — <see cref="ConsolePrefEntry.Extra"/> 안 <c>unitMap = {v, scale, cx, cy, layers}</c>.</summary>
    public const string PrefKey = "unitMap";

    /// <summary><see cref="PrefKey"/> 형식의 판. 다르면 읽지 않고 폴백한다(다음 저장이 덮는다).</summary>
    public const int PrefVersion = 1;

    /// <summary>내 부대를 가운데 둘 때의 배율(FR-15).</summary>
    public const double MyUnitScale = 0.5;

    /// <summary>뷰가 멈춘 뒤 저장하기까지 기다리는 창 — 팬 프레임마다 파일을 쓰지 않는다.</summary>
    public static readonly TimeSpan ViewSaveIdle = TimeSpan.FromSeconds(1);

    private UnitMapLayers _layers;
    private EnumUnitEchelon? _highlight;
    private bool _viewRestored;
    private readonly CoalescingTrigger _viewSaveTrigger;

    /// <summary>저장된 개인 설정(한 키) — 읽기 · 쓰기 모두 이 모양 하나.</summary>
    private sealed record PersonalView(int V, double? Scale, double? Cx, double? Cy, UnitMapLayers? Layers);

    #region - 첫 화면 · 개인 뷰 (FR-15) -
    /// <summary>
    /// 캔버스가 붙고 편제가 있으면 한 번 — 저장된 개인 뷰 → 없으면 내 부대 중심 50% → 없으면 전체 보기.
    /// 저장값이 깨졌거나 판(<c>v</c>)이 다르면 없는 것으로 보고, 배율은 범위로 자른다(중심은 캔버스의 팬 한계가 자른다 — SIM-V083 · V087).
    /// </summary>
    private void TryRestoreView()
    {
        if (_viewRestored || _surface is not { } surface) return;
        if (_tree.Count == 0) { StatusText = UnitMapText.EmptyMapStatus; return; }
        _viewRestored = true;

        if (_pendingReveal is int reveal && _tree.Find(reveal) is not null)
        {
            // 지도에서 [관계도에서 보기]로 열렸다 — 저장된 뷰보다 그 부대가 먼저(FR-46).
            _pendingReveal = null;
            surface.CenterOn(reveal, MyUnitScale);
            return;
        }

        if (ReadPref() is { Scale: double scale, Cx: double cx, Cy: double cy })
        {
            surface.SetView(GraphViewport.ClampScale(scale), new Point(cx, cy));
            return;
        }
        if (MyUnitId is int mine && _tree.Find(mine) is not null)
        {
            surface.CenterOn(mine, MyUnitScale);
            return;
        }
        surface.Fit();
    }

    private void OnSurfaceViewChanged(object? sender, EventArgs e) => _ = _viewSaveTrigger.Pulse();

    private Task OnViewIdleAsync(CancellationToken token)
    {
        SaveView();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 개인 뷰 · 레이어를 한 키로 저장한다(뷰가 멈췄을 때 · 창 닫을 때). <b>공유 배치(Δ)는 쓰지 않는다</b>(NFR-14).
    /// 캔버스가 아직 없으면 저장된 뷰는 그대로 두고 레이어만 바꾼다.
    /// </summary>
    public void SaveView()
    {
        if (_options.Prefs is not { } prefs) return;
        var old = ReadPref();
        double? scale = old?.Scale, cx = old?.Cx, cy = old?.Cy;
        if (_surface is { } surface && _viewRestored
            && double.IsFinite(surface.Scale) && double.IsFinite(surface.CenterWorld.X) && double.IsFinite(surface.CenterWorld.Y))
            (scale, cx, cy) = (surface.Scale, surface.CenterWorld.X, surface.CenterWorld.Y);

        prefs.Extra ??= new Dictionary<string, JsonElement>();
        prefs.Extra[PrefKey] = JsonSerializer.SerializeToElement(new
        {
            v = PrefVersion,
            scale,
            cx,
            cy,
            layers = new { hierarchy = _layers.Hierarchy, adjacency = _layers.Adjacency, devices = _layers.DeviceBadges },
        });
        _options.SavePrefs?.Invoke();
    }

    private PersonalView? ReadPref()
    {
        if (_options.Prefs?.Extra is not { } extra || !extra.TryGetValue(PrefKey, out var e) || e.ValueKind != JsonValueKind.Object) return null;
        try
        {
            if (!e.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || v.GetInt32() != PrefVersion) return null;
            var scale = Number(e, "scale");
            var cx = Number(e, "cx");
            var cy = Number(e, "cy");
            if (scale is <= 0) scale = null;
            UnitMapLayers? layers = null;
            if (e.TryGetProperty("layers", out var l) && l.ValueKind == JsonValueKind.Object)
                layers = new UnitMapLayers(Flag(l, "hierarchy"), Flag(l, "adjacency"), Flag(l, "devices"));
            return new PersonalView(PrefVersion, scale, cx, cy, layers);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return null;
        }

        static double? Number(JsonElement e, string name)
            => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && double.IsFinite(p.GetDouble()) ? p.GetDouble() : null;

        static bool Flag(JsonElement e, string name)
            => !e.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.False;
    }
    #endregion

    #region - 레이어 (FR-26) -
    /// <summary>레이어 토글 — 계층선 · 인접선 · 장비 배지. 개인 표시 설정(공유 배치 아님).</summary>
    public UnitMapLayers Layers => _layers;

    /// <summary>레이어를 바꾸고 장면에 싣는다. 저장은 뷰가 멈춘 뒤 한 번(<see cref="ViewSaveIdle"/>).</summary>
    public void SetLayers(UnitMapLayers layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (_layers == layers) return;
        _layers = layers;
        NotifyOfPropertyChange(nameof(Layers));
        NotifyLayerFlags();
        if (layers.Adjacency && _barAction is not null) { _barAction = null; NotifyBar(); }
        RebuildScene();
        _ = _viewSaveTrigger.Pulse();
    }

    private UnitMapLayers ReadLayersPref() => ReadPref()?.Layers ?? UnitMapLayers.All;

    /// <summary>[계층선] 토글(<c>Units.Map.Layer.Hierarchy</c>) — 한 칸만 바꾼다(저장은 <see cref="SetLayers"/> 와 같이 멈춘 뒤 한 번).</summary>
    public bool ShowHierarchyLayer
    {
        get => _layers.Hierarchy;
        set => SetLayers(_layers with { Hierarchy = value });
    }

    /// <summary>[인접선] 토글(<c>Units.Map.Layer.Adjacency</c>).</summary>
    public bool ShowAdjacencyLayer
    {
        get => _layers.Adjacency;
        set => SetLayers(_layers with { Adjacency = value });
    }

    /// <summary>[장비 배지] 토글(<c>Units.Map.Layer.Devices</c>).</summary>
    public bool ShowDeviceBadgesLayer
    {
        get => _layers.DeviceBadges;
        set => SetLayers(_layers with { DeviceBadges = value });
    }

    private void NotifyLayerFlags()
    {
        NotifyOfPropertyChange(nameof(ShowHierarchyLayer));
        NotifyOfPropertyChange(nameof(ShowAdjacencyLayer));
        NotifyOfPropertyChange(nameof(ShowDeviceBadgesLayer));
    }
    #endregion

    #region - 초기화 단추 상태 (FR-09 — 숨기지 않고 비활성 + 사유) -
    /// <summary>툴바 [배치 초기화](<c>Units.Map.ResetLayout</c>)를 누를 수 있다.</summary>
    public bool CanResetLayout => ResetBlockedReason() is null;

    /// <summary>[배치 초기화]를 누를 수 없는 까닭(툴팁) — 권한 · 판 불일치 · 읽기 실패 · 읽는 중.</summary>
    public string? ResetLayoutDisabledReason => ResetBlockedReason();

    /// <summary>상세 [이 부대 배치 초기화](<c>Units.Detail.ResetNodeLayout</c>)를 누를 수 있다 — 고른 부대를 옮긴 적이 있다.</summary>
    public bool CanResetSelectedNodeLayout => ResetSelectedNodeLayoutDisabledReason is null;

    /// <summary>[이 부대 배치 초기화]를 누를 수 없는 까닭 — 고른 부대 없음 · 초기화 불가 사유 · 옮긴 적 없음.</summary>
    public string? ResetSelectedNodeLayoutDisabledReason
    {
        get
        {
            if (_selected is not int unit || _tree.Find(unit) is null) return UnitMapText.MoveModeNeedsSelection;
            if (ResetBlockedReason() is { } blocked) return blocked;
            return DisplayDeltaOf(unit) is null ? UnitMapText.NodeNotMovedStatus(NameOf(unit)) : null;
        }
    }

    /// <summary>상세 [이 부대 배치 초기화] — 고른 부대에 <see cref="ResetNodeLayoutAsync"/>.</summary>
    public Task ResetSelectedNodeLayoutAsync()
        => _selected is int unit ? ResetNodeLayoutAsync(unit) : Task.CompletedTask;

    private void NotifyResetState()
    {
        NotifyOfPropertyChange(nameof(CanResetLayout));
        NotifyOfPropertyChange(nameof(ResetLayoutDisabledReason));
        NotifyOfPropertyChange(nameof(CanResetSelectedNodeLayout));
        NotifyOfPropertyChange(nameof(ResetSelectedNodeLayoutDisabledReason));
    }
    #endregion

    #region - 검색 (FR-16 · 필수 항목 2) -
    /// <summary>
    /// 툴바 검색의 <c>Enter</c> · [다음] — 지금 선택 뒤의 첫 일치(이름 · 코드, 대소문자 무시)를 고르고 가운데로(배율 유지, L0 이면 L1 경계 0.40).
    /// 글자마다 부르지 않는다(선택 · 상세 GET 폭주 방지). 끝에 닿으면 처음부터. 일치가 없으면 선택 · 뷰를 두고 분명히 말한다.
    /// </summary>
    /// <returns>옮겼으면 <c>true</c>.</returns>
    public bool SearchNext(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _pending is not null || _tree.Count == 0) return false;
        var text = query.Trim();

        var count = _tree.Ordered.Count;
        var start = _selected is int current ? IndexInOrder(current) + 1 : 0;
        for (var step = 0; step < count; step++)
        {
            var node = _tree.Ordered[(start + step) % count];
            if (!node.Name.Contains(text, StringComparison.OrdinalIgnoreCase) && !node.Code.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;

            RequestSelect(node.Id);
            if (_surface is { } surface)
            {
                if (surface.Level == UnitMapLevel.L0) surface.CenterOn(node.Id, UnitMapLod.EntryScale(UnitMapLevel.L1));
                else surface.CenterOn(node.Id);
            }
            StatusText = null;
            return true;
        }

        StatusText = UnitMapText.SearchNoMatch(text);
        return false;
    }
    #endregion

    #region - 제대 칩 강조 (FR-16 · R-19) -
    /// <summary>제대 칩 — 걸러내지 않고 강조만(해당 외 50% 흐림). 노드 수는 그대로다(트리 구조가 끊기지 않게).</summary>
    public EnumUnitEchelon? EchelonHighlight => _highlight;

    /// <summary>제대 칩을 바꾼다(<c>null</c> = 전체).</summary>
    public void SetEchelonHighlight(EnumUnitEchelon? echelon)
    {
        if (_highlight == echelon) return;
        _highlight = echelon;
        NotifyOfPropertyChange(nameof(EchelonHighlight));
        RebuildScene();
    }
    #endregion
}
