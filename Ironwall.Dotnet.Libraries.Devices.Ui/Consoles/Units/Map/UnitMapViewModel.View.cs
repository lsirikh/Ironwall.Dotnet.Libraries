using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Text.Json;
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
    /// <summary>개인 뷰(배율 · 가운데 월드 점) — <see cref="ConsolePrefEntry.Extra"/> 키.</summary>
    public const string ViewPrefKey = "unitMap.view";

    /// <summary>레이어 토글 — <see cref="ConsolePrefEntry.Extra"/> 키.</summary>
    public const string LayersPrefKey = "unitMap.layers";

    /// <summary>내 부대를 가운데 둘 때의 배율(FR-15).</summary>
    public const double MyUnitScale = 0.5;

    private UnitMapLayers _layers;
    private EnumUnitEchelon? _highlight;
    private bool _viewRestored;

    #region - 첫 화면 · 개인 뷰 (FR-15) -
    /// <summary>
    /// 캔버스가 붙고 편제가 있으면 한 번 — 저장된 개인 뷰 → 없으면 내 부대 중심 50% → 없으면 전체 보기.
    /// 저장값이 깨졌으면(형식 · 수 아님) 없는 것으로 보고, 배율은 범위로 자른다(SIM-V083 · V087).
    /// </summary>
    private void TryRestoreView()
    {
        if (_viewRestored || _surface is not { } surface || _tree.Count == 0) return;
        _viewRestored = true;

        if (ReadViewPref() is { } view)
        {
            surface.SetView(GraphViewport.ClampScale(view.Scale), view.Center);
            return;
        }
        if (_options.MyUnitId is int mine && _tree.Find(mine) is not null)
        {
            surface.CenterOn(mine, MyUnitScale);
            return;
        }
        surface.Fit();
    }

    /// <summary>
    /// 개인 뷰 · 레이어를 저장한다(창 닫을 때 · 콘솔이 부른다). <b>공유 배치(Δ)는 쓰지 않는다</b>(NFR-14) — 키는 뷰 · 레이어 둘뿐.
    /// </summary>
    public void SaveView()
    {
        if (_options.Prefs is not { } prefs) return;
        if (_surface is { } surface && double.IsFinite(surface.Scale) && double.IsFinite(surface.CenterWorld.X) && double.IsFinite(surface.CenterWorld.Y))
        {
            prefs.Extra ??= new Dictionary<string, JsonElement>();
            prefs.Extra[ViewPrefKey] = JsonSerializer.SerializeToElement(new { scale = surface.Scale, x = surface.CenterWorld.X, y = surface.CenterWorld.Y });
        }
        WriteLayersPref(prefs);
        _options.SavePrefs?.Invoke();
    }

    private (double Scale, Point Center)? ReadViewPref()
    {
        if (_options.Prefs?.Extra is not { } extra || !extra.TryGetValue(ViewPrefKey, out var element)) return null;
        try
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            var scale = element.GetProperty("scale").GetDouble();
            var x = element.GetProperty("x").GetDouble();
            var y = element.GetProperty("y").GetDouble();
            return double.IsFinite(scale) && scale > 0 && double.IsFinite(x) && double.IsFinite(y) ? (scale, new Point(x, y)) : null;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
    #endregion

    #region - 레이어 (FR-26) -
    /// <summary>레이어 토글 — 계층선 · 인접선 · 장비 배지. 개인 표시 설정(공유 배치 아님).</summary>
    public UnitMapLayers Layers => _layers;

    /// <summary>레이어를 바꾸고 장면에 싣고 기억한다.</summary>
    public void SetLayers(UnitMapLayers layers)
    {
        ArgumentNullException.ThrowIfNull(layers);
        if (_layers == layers) return;
        _layers = layers;
        NotifyOfPropertyChange(nameof(Layers));
        RebuildScene();
        if (_options.Prefs is { } prefs)
        {
            WriteLayersPref(prefs);
            _options.SavePrefs?.Invoke();
        }
    }

    private UnitMapLayers ReadLayersPref()
    {
        if (_options.Prefs?.Extra is not { } extra || !extra.TryGetValue(LayersPrefKey, out var element) || element.ValueKind != JsonValueKind.Object)
            return UnitMapLayers.All;
        return new UnitMapLayers(Flag(element, "hierarchy"), Flag(element, "adjacency"), Flag(element, "devices"));

        static bool Flag(JsonElement e, string name)
            => !e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.False;
    }

    private void WriteLayersPref(ConsolePrefEntry prefs)
    {
        prefs.Extra ??= new Dictionary<string, JsonElement>();
        prefs.Extra[LayersPrefKey] = JsonSerializer.SerializeToElement(new { hierarchy = _layers.Hierarchy, adjacency = _layers.Adjacency, devices = _layers.DeviceBadges });
    }
    #endregion

    #region - 검색 (FR-16 · 필수 항목 2) -
    /// <summary>
    /// 툴바 검색의 <c>Enter</c> · [다음] — 지금 선택 뒤의 첫 일치(이름 · 코드, 대소문자 무시)를 고르고 가운데로(배율 유지, L0 이면 L1 경계 0.40).
    /// 글자마다 부르지 않는다(선택 · 상세 GET 폭주 방지). 끝에 닿으면 처음부터. 일치가 없으면 선택을 두고 분명히 말한다.
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
