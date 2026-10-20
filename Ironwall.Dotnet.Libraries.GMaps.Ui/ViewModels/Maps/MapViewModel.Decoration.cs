using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 「꾸미기」(2026-10-21 사용자 요청 — 층만 올리는 게 아니라 디지털 트윈처럼 꾸미는 도구) — 3D 영역 안 2D 지도 위에
                  나무(한 그루 · 줄 · 영역) · 바닥(영역) · 소품(클릭) · 담장(선)을 놓고 값을 바꾼다. map-3d 는 굽지 않고 뷰어가 바로 그린다(twin-contract C).
   Note         : ★ 들어가기 = 툴바(Map.Decoration.Start — 화면 가운데 3D 영역) · 3D 의 decoration.pick(그 항목을 판에서 고름). 지도 편집 권한이 있어야 한다.
                    꾸미는 동안은 독점 입력 모드라 3D 에 들어가지 않는다(IsMap3DBlockedByInputMode) — 「건물 그리기」와 같은 관용구.
                  ★ 놓기 = 도구를 고르고 지도 클릭(점 도구는 한 번에 저장, 선 · 영역은 Enter · 두 번 누르기 · [마치기]). 옮기기는 [다시 놓기](지도 위 새 끌기 없음 —
                    drag-first-ux Must Never). 소품 방향은 판의 값 칸 · ↺/↻ · 판 안 ↑/↓(Shift = 1°).
                  ★ 저장 = PUT …/decorations/{uid} → 목록 다시 받기 · 3D 에 decorations.reload + GIS 편집 기록(DecorationCommand, 되돌리기 · 다시 하기).
                  ★ 엔진이 옛 판(GET 404)이면 "엔진이 옛 판이라 꾸미기를 쓸 수 없습니다" + 도구 끔.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IDecorationHost, IHandle<CallClearDecorationsProcessMessageModel>
{
    private Map3DAreaItem? _decoArea;
    private IReadOnlyList<Map3DDecorationItem>? _decoItemList;
    private string? _decoSelectedUid, _decoReplaceUid, _decoPendingSelect;
    private DecoTool? _decoTool;
    private List<UbPoint>? _decoPointList;
    private List<UbPoint> DecoPoints => _decoPointList ??= new();
    private JObject? _decoProps;
    private Dictionary<DecoTool, JObject>? _decoLastPropsMap;
    private Dictionary<DecoTool, JObject> DecoLastProps => _decoLastPropsMap ??= new();
    private string _decoName = "";
    private bool _decoUnsupported, _decoBusy, _decoInputError;
    private string? _decoError;
    private DecorationAdorner? _decoAdorner;
    private System.Windows.Window? _decoKeyWindow;
    private Map3DAreaItem? _pendingDecorationsClear;

    // ── 바인딩 ──

    public bool IsDecorating => _decoArea != null;
    public Map3DAreaItem? DecorationArea => _decoArea;
    public IReadOnlyList<Map3DDecorationItem> DecorationItems => _decoItemList ?? Array.Empty<Map3DDecorationItem>();
    public DecoTool? DecorationTool => _decoTool;
    public IReadOnlyList<UbPoint> DecorationDraftPoints => DecoPoints;
    private Map3DDecorationItem? DecoSelected => DecorationItems.FirstOrDefault(i => i.Uid == _decoSelectedUid);

    /// <summary>판 목록에서 고른 항목(uid) — 고르면 값 칸이 그 항목 값으로 바뀐다.</summary>
    public string? DecorationSelectedUid
    {
        get => _decoSelectedUid;
        set { if (value != _decoSelectedUid) SelectDecoration(value); }
    }

    public bool HasDecorationSelection => DecoSelected != null;
    public bool IsDecorationToolsEnabled => IsDecorating && !_decoUnsupported;
    public bool IsDecorationUnsupported => IsDecorating && _decoUnsupported;
    public string DecorationUnsupportedText => Map3DDecoration.OldEngineText;
    public string DecorationTitle => DecoSelected is { } s ? $"꾸미기 · {s.Text}" : "꾸미기";
    public string DecorationStatusText => _decoReplaceUid != null && _decoTool is { } t && Map3DDecoration.GeometryOf(t) == Map3DDecoration.GeoPoint
        ? "지도를 눌러 새 자리에 놓으세요" : Map3DDecoration.StatusText(_decoTool, DecoPoints.Count);
    public string DecorationCountText => _decoArea is { } a ? $"3D 영역 '{a.Title}' · {Map3DDecoration.CountText(DecorationItems.Count)}" : "";
    public string? DecorationError => _decoError;
    public bool HasDecorationError => _decoError != null;
    public bool IsDecorationFinishVisible => _decoTool is { } t && Map3DDecoration.GeometryOf(t) != Map3DDecoration.GeoPoint && DecoPoints.Count > 0;

    public bool IsDecorationToolTreePoint => _decoTool == DecoTool.TreePoint;
    public bool IsDecorationToolTreeLine => _decoTool == DecoTool.TreeLine;
    public bool IsDecorationToolTreeArea => _decoTool == DecoTool.TreeArea;
    public bool IsDecorationToolGround => _decoTool == DecoTool.Ground;
    public bool IsDecorationToolProp => _decoTool == DecoTool.Prop;
    public bool IsDecorationToolWall => _decoTool == DecoTool.Wall;

    // ── 값 칸(지금 값 도구 = 고른 항목의 도구, 없으면 고른 도구) ──

    private DecoTool? DecoValueTool => DecoSelected?.Tool ?? _decoTool;
    public bool HasDecorationValues => DecoValueTool != null && IsDecorationToolsEnabled;
    public bool IsDecorationTreeValues => DecoValueTool is DecoTool.TreePoint or DecoTool.TreeLine or DecoTool.TreeArea;
    public bool IsDecorationSpacingVisible => DecoValueTool == DecoTool.TreeLine;
    public bool IsDecorationDensityVisible => DecoValueTool == DecoTool.TreeArea;
    public bool IsDecorationGroundValues => DecoValueTool == DecoTool.Ground;
    public bool IsDecorationPropValues => DecoValueTool == DecoTool.Prop;
    public bool IsDecorationWallValues => DecoValueTool == DecoTool.Wall;
    public bool IsDecorationHeightVisible => IsDecorationTreeValues || IsDecorationWallValues;

    public IReadOnlyList<KeyValuePair<string, string>> DecorationSpeciesList => Map3DDecoration.Species;
    public IReadOnlyList<KeyValuePair<string, string>> DecorationSurfaces => Map3DDecoration.Surfaces;
    public IReadOnlyList<KeyValuePair<string, string>> DecorationPropTypes => Map3DDecoration.PropTypes;
    public IReadOnlyList<KeyValuePair<string, string>> DecorationWallTypes => Map3DDecoration.WallTypes;
    public IReadOnlyList<KeyValuePair<string, string>> DecorationColors => Map3DLook.Colors;

    private JObject DecoProps => _decoProps ??= DecoValueTool is { } t ? Map3DDecoration.DefaultProps(t) : new JObject();
    private string Prop(string key) => DecoProps[key] is JValue { Value: not null } v ? Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "" : "";

    // 콤보 값은 그 종류일 때만 받는다(숨은 콤보가 비운 값(null)을 되써서 다른 종류 props 에 키를 더하지 않게)
    public string DecorationSpecies { get => Prop("species"); set { if (IsDecorationTreeValues && value != null) SetDecoProp("species", value); } }
    public string DecorationSurface { get => Prop("surface"); set { if (IsDecorationGroundValues && value != null) SetDecoProp("surface", value); } }
    public string DecorationPropType { get => IsDecorationPropValues ? Prop("type") : ""; set { if (IsDecorationPropValues && value != null) SetDecoProp("type", value); } }
    public string DecorationWallType { get => IsDecorationWallValues ? Prop("type") : ""; set { if (IsDecorationWallValues && value != null) SetDecoProp("type", value); } }
    /// <summary>바닥 · 담장 색("" = 재질 기본).</summary>
    public string DecorationColor
    {
        get => Prop("color");
        set { if ((IsDecorationGroundValues || IsDecorationWallValues) && value != null) SetDecoProp("color", Map3DLook.NormalizeColor(value)); }
    }
    public string DecorationHeightText
    {
        get => Prop("height_m");
        set => SetDecoNumber("height_m", value, IsDecorationWallValues ? Map3DDecoration.WallHeight : Map3DDecoration.TreeHeight, "높이");
    }
    public string DecorationSpacingText { get => Prop("spacing_m"); set => SetDecoNumber("spacing_m", value, Map3DDecoration.Spacing, "간격"); }
    public string DecorationDensityText { get => Prop("density"); set => SetDecoNumber("density", value, Map3DDecoration.Density, "밀도", digits: 2); }
    public string DecorationHeadingText { get => Prop("heading_deg"); set => SetDecoNumber("heading_deg", value, Map3DDecoration.Heading, "방향"); }
    public string DecorationScaleText { get => Prop("scale"); set => SetDecoNumber("scale", value, Map3DDecoration.Scale, "크기"); }

    public string DecorationName
    {
        get => _decoName;
        set { _decoName = (value ?? "").Trim(); NotifyOfPropertyChange(); }
    }

    private void SetDecoProp(string key, string? value)
    {
        if (DecoValueTool is not { } t) return;
        DecoProps[key] = value is null ? JValue.CreateNull() : value;
        if (DecoSelected == null) DecoLastProps[t] = (JObject)DecoProps.DeepClone();   // 다음에 놓을 것도 같은 값으로
        RefreshDecorationPanel();
    }

    private void SetDecoNumber(string key, string? text, (double Min, double Max, double Default) range, string name, int digits = 1)
    {
        if (DecoValueTool is not { } t) return;
        if (!Map3DDecoration.TryParse(text, range, name, out var v, out var err, digits))
        {
            _decoInputError = true;
            SetDecorationError(err);
            ApplyDecorationCommand.RaiseCanExecuteChanged();
            return;
        }
        _decoInputError = false;
        _decoError = null;
        DecoProps[key] = v;
        if (DecoSelected == null) DecoLastProps[t] = (JObject)DecoProps.DeepClone();
        RefreshDecorationPanel();
    }

    // ── 판 ──

    private DecorationPanelControl? _decoPanel;
    /// <summary>「꾸미기」 판 — DataContext=this, 머리 띠 ✕ = 꾸미기 끝내기, 판 안 ↑/↓ = 소품 방향.</summary>
    public DecorationPanelControl DecorationPanel => _decoPanel ??= new DecorationPanelControl
    {
        DataContext = this,
        CloseCommand = ExitDecorationCommand,
        Host = new DecorationPanelHost(this),
    };

    private RelayCommand? _decorateCommand;
    /// <summary>툴바 「꾸미기」 — 화면 가운데 3D 영역에서 시작(꾸미는 중이면 끝낸다).</summary>
    public RelayCommand DecorateCommand => _decorateCommand ??= new RelayCommand(p =>
    {
        if (IsDecorating) { ExitDecoration("toggle"); return; }
        var item = p as Map3DAreaItem;
        if (item == null && MainMap != null)
        {
            var c = MainMap.Position;
            item = Map3DAreas.FirstOrDefault(a => a.Area.Contains(c.Lng, c.Lat));
        }
        if (item == null) { SetAimStatus("3D 영역 안에서 시작하세요 — 화면 가운데를 3D 영역 안에 두고 다시 누르세요", autoHide: true); return; }
        StartDecoration(item);
    });

    private RelayCommand? _exitDecorationCommand;
    public RelayCommand ExitDecorationCommand => _exitDecorationCommand ??= new RelayCommand(_ => ExitDecoration("close"));

    private RelayCommand? _selectDecorationToolCommand;
    /// <summary>도구 고르기(매개변수 = DecoTool 이름) — 찍던 점은 버린다. 같은 도구를 한 번 더 = 도구 내려놓기(고르기).</summary>
    public RelayCommand SelectDecorationToolCommand => _selectDecorationToolCommand ??= new RelayCommand(p =>
    {
        if (!IsDecorationToolsEnabled || !Enum.TryParse<DecoTool>(p?.ToString(), out var tool)) return;
        SetDecorationTool(_decoTool == tool && _decoReplaceUid == null ? null : tool);
    }, _ => IsDecorationToolsEnabled && !_decoBusy);

    private RelayCommand? _finishDecorationCommand;
    /// <summary>[마치기] — 선 · 영역 저장(Enter · 두 번 누르기와 같음).</summary>
    public RelayCommand FinishDecorationCommand => _finishDecorationCommand ??= new RelayCommand(_ => _ = PlaceDecorationAsync(), _ => IsDecorationFinishVisible && !_decoBusy);

    private RelayCommand? _applyDecorationCommand;
    /// <summary>[적용] — 고른 항목의 값 · 이름 저장.</summary>
    public RelayCommand ApplyDecorationCommand => _applyDecorationCommand ??= new RelayCommand(
        _ => _ = ApplyDecorationAsync(), _ => DecoSelected != null && !_decoBusy && !_decoInputError && IsDecorationToolsEnabled);

    private RelayCommand? _replaceDecorationCommand;
    /// <summary>[다시 놓기] — 고른 항목을 지도에 새로 놓은 자리 · 모양으로 옮긴다(값은 그대로).</summary>
    public RelayCommand ReplaceDecorationCommand => _replaceDecorationCommand ??= new RelayCommand(_ =>
    {
        if (DecoSelected is not { } s) return;
        _decoReplaceUid = s.Uid;
        _decoTool = s.Tool;
        DecoPoints.Clear();
        _decoAdorner?.InvalidateVisual();
        RefreshDecorationPanel();
    }, _ => DecoSelected != null && !_decoBusy && IsDecorationToolsEnabled);

    private RelayCommand? _deleteDecorationCommand;
    public RelayCommand DeleteDecorationCommand => _deleteDecorationCommand ??= new RelayCommand(
        _ => _ = DeleteDecorationAsync(), _ => DecoSelected != null && !_decoBusy && IsDecorationToolsEnabled);

    private RelayCommand? _undoDecorationCommand;
    public RelayCommand UndoDecorationCommand => _undoDecorationCommand ??= new RelayCommand(_ => _ = UndoDecorationAsync(redo: false), _ => IsDecorating && !_decoBusy);

    private RelayCommand? _redoDecorationCommand;
    public RelayCommand RedoDecorationCommand => _redoDecorationCommand ??= new RelayCommand(_ => _ = UndoDecorationAsync(redo: true), _ => IsDecorating && !_decoBusy);

    private RelayCommand? _clearDecorationsCommand;
    /// <summary>[모두 지우기…] — 확인 뒤 그 영역의 꾸미기 항목을 모두 지운다.</summary>
    public RelayCommand ClearDecorationsCommand => _clearDecorationsCommand ??= new RelayCommand(
        _ => _ = RequestClearDecorationsAsync(), _ => IsDecorationToolsEnabled && !_decoBusy && DecorationItems.Count > 0);

    private RelayCommand? _stepDecorationHeadingCommand;
    /// <summary>↺ / ↻ — 소품 방향 ±15°(매개변수 = 도).</summary>
    public RelayCommand StepDecorationHeadingCommand => _stepDecorationHeadingCommand ??= new RelayCommand(p =>
    {
        if (double.TryParse(p?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) StepDecorationHeading(d);
    }, _ => IsDecorationPropValues);

    private bool StepDecorationHeading(double delta)
    {
        if (!IsDecorationPropValues) return false;
        double now = DecoProps["heading_deg"]?.Type is JTokenType.Float or JTokenType.Integer ? DecoProps.Value<double>("heading_deg") : 0;
        DecorationHeadingText = Map3DDecoration.Num(Map3DDecoration.StepHeading(now, delta));
        return true;
    }

    // ── 들어가기 · 나가기 ──

    private void StartDecoration(Map3DAreaItem item)
    {
        if (MainMap == null) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (item.IsBaking) { SetAimStatus("이 영역은 다시 만드는 중입니다 — 다 만든 뒤 꾸미세요", autoHide: true); return; }
        if (IsDecorating) ExitDecoration("switch");
        _groupSelection?.EndRotation(commit: true);
        CancelConflictingModesForMeasure();   // 「건물 그리기」 · 측정 · 조준 · 배치 · 선 그리기 끝내기
        if (MainMap.IsMeasuring) MainMap.StopMeasure();
        _decoArea = item;
        _decoItemList = null;
        _decoUnsupported = false;
        _decoError = null;
        SetDecorationTool(null);
        AttachDecorationSurface();
        MainMap.Focus();
        SyncMap3DWithInputModes();   // 3D 중이면 2D 로(꾸미기는 2D)
        _log?.Info($"[Decoration] 시작 area={item.Id}");
        _ = LoadDecorationsAsync(item.Id);
    }

    private void ExitDecoration(string cause)
    {
        if (_decoArea is not { } area) return;
        _log?.Info($"[Decoration] 끝 area={area.Id} cause={cause}");
        if (_decoSelectedUid != null) PostUserBuilding3D(area.Id, Map3DDecoration.SelectMessageJson(null));
        _decoArea = null;
        _decoItemList = null;
        _decoSelectedUid = _decoReplaceUid = _decoPendingSelect = null;
        _decoTool = null;
        DecoPoints.Clear();
        _decoProps = null;
        _decoError = null;
        _decoInputError = false;
        DetachDecorationSurface();
        RefreshDecorationPanel();
        SyncMap3DWithInputModes();
    }

    /// <summary>3D 에서 꾸미기 항목을 눌렀다 — 그 3D 영역에서 「꾸미기」를 시작하고(2D 로 바뀐다) 목록을 받으면 그 항목을 판에서 고른다.</summary>
    private void OnMap3DDecorationPicked(string uid)
    {
        if (_map3DState != Map3DState.Shown || _map3DJob is not { } job || Map3DAreas.FirstOrDefault(a => a.Id == job) is not { } area) return;
        _log?.Info($"[Decoration] 3D 에서 고름 uid={uid} job={job}");
        CloseBuildingHeightPanel(clearUndo: false);
        StartDecoration(area);
        if (_decoArea?.Id != job) return;
        if (DecorationItems.Any(i => i.Uid == uid)) SelectDecoration(uid);   // 목록을 이미 받았다
        else _decoPendingSelect = uid;
    }

    private void SetDecorationTool(DecoTool? tool)
    {
        _decoTool = tool;
        _decoReplaceUid = null;
        DecoPoints.Clear();
        _decoInputError = false;
        _decoError = null;
        if (tool is { } t)
        {
            if (_decoSelectedUid != null && _decoArea is { } a) PostUserBuilding3D(a.Id, Map3DDecoration.SelectMessageJson(null));
            _decoSelectedUid = null;
            _decoProps = DecoLastProps.TryGetValue(t, out var last) ? (JObject)last.DeepClone() : Map3DDecoration.DefaultProps(t);
            _decoName = "";
        }
        else if (_decoSelectedUid == null) _decoProps = null;
        _decoAdorner?.InvalidateVisual();
        RefreshDecorationPanel();
    }

    /// <summary>항목 고르기(null = 고름 풀기) — 값 칸 · 이름을 그 항목 값으로, 3D 강조(decoration.select).</summary>
    private void SelectDecoration(string? uid)
    {
        var item = DecorationItems.FirstOrDefault(i => i.Uid == uid);
        _decoSelectedUid = item?.Uid;
        _decoTool = null;
        _decoReplaceUid = null;
        DecoPoints.Clear();
        _decoInputError = false;
        _decoError = null;
        if (item != null)
        {
            var props = Map3DDecoration.DefaultProps(item.Tool);
            props.Merge(item.Props, Map3DLook.KeepNulls);
            _decoProps = props;
            _decoName = item.Name ?? "";
        }
        else _decoProps = null;
        if (_decoArea is { } a) PostUserBuilding3D(a.Id, Map3DDecoration.SelectMessageJson(item?.Uid));
        _decoAdorner?.InvalidateVisual();
        RefreshDecorationPanel();
    }

    private void AttachDecorationSurface()
    {
        if (MainMap == null) return;
        try
        {
            var layer = AdornerLayer.GetAdornerLayer(MainMap);
            if (layer != null) { _decoAdorner = new DecorationAdorner(MainMap, this, _log); layer.Add(_decoAdorner); }
            else _log?.Warning("[Decoration] AdornerLayer 없음 — 지도 표면 없이(키 · 판만)");
            _decoKeyWindow = System.Windows.Window.GetWindow(MainMap);
            if (_decoKeyWindow != null) _decoKeyWindow.PreviewKeyDown += OnDecorationKeyDown;
        }
        catch (Exception ex) { _log?.Warning($"[Decoration] 표면 붙이기 경고: {ex.Message}"); }
    }

    private void DetachDecorationSurface()
    {
        try
        {
            if (_decoAdorner != null && MainMap != null) AdornerLayer.GetAdornerLayer(MainMap)?.Remove(_decoAdorner);
            _decoAdorner?.Dispose();
            if (_decoKeyWindow != null) _decoKeyWindow.PreviewKeyDown -= OnDecorationKeyDown;
        }
        catch (Exception ex) { _log?.Warning($"[Decoration] 표면 떼기 경고: {ex.Message}"); }
        _decoAdorner = null;
        _decoKeyWindow = null;
    }

    private async Task LoadDecorationsAsync(string job)
    {
        if (_map3DClient == null) return;
        try
        {
            var items = await _map3DClient.ListDecorationsAsync(job);
            if (_decoArea?.Id != job) return;
            _decoUnsupported = items == null;
            _decoItemList = items ?? Array.Empty<Map3DDecorationItem>();
            if (_decoUnsupported) { _decoTool = null; DecoPoints.Clear(); _log?.Warning($"[Decoration] 옛 엔진(404) job={job}"); }
            if (_decoPendingSelect is { } pending) { _decoPendingSelect = null; _decoSelectedUid = pending; }
            if (_decoSelectedUid != null) SelectDecoration(DecorationItems.Any(i => i.Uid == _decoSelectedUid) ? _decoSelectedUid : null);   // 없어진 항목은 고름에서 뺀다
            _decoAdorner?.InvalidateVisual();
            RefreshDecorationPanel();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Decoration] 목록 — 서버 닿지 않음: {ex.Message}");
            SetDecorationError(Map3DUnavailableText);
            NoteMap3DEngineDown("decoration-list");
        }
        catch (Exception ex) { _log?.Warning($"[Decoration] 목록 실패: {ex.Message}"); }
    }

    // ── 지도 표면(IDecorationHost) ──

    void IDecorationHost.DecorationPointerDown(UbPoint screen, int clickCount)
    {
        if (_decoArea is not { } area || MainMap is not { } map || _decoBusy || _decoUnsupported) return;
        if (_decoTool is not { } tool)
        {
            var hit = Map3DDecoration.HitTest(DecorationItems, screen, ToScreen);
            SelectDecoration(hit);
            return;
        }
        if (clickCount >= 2) { if (Map3DDecoration.GeometryOf(tool) != Map3DDecoration.GeoPoint) _ = PlaceDecorationAsync(); return; }   // 첫 누르기가 이미 점을 놓았다
        var g = map.FromLocalToLatLng((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
        var geo = Map3DUserBuilding.Round(new UbPoint(g.Lng, g.Lat));
        if (!area.Area.Contains(geo.X, geo.Y)) { SetDecorationError(Map3DUserBuilding.OutsideText); return; }
        DecoPoints.Add(geo);
        _decoError = null;
        _decoAdorner?.InvalidateVisual();
        RefreshDecorationPanel();
        if (Map3DDecoration.GeometryOf(tool) == Map3DDecoration.GeoPoint) _ = PlaceDecorationAsync();
    }

    void IDecorationHost.DecorationRightClick(UbPoint screen) => RemoveLastDecorationPoint();

    private UbPoint ToScreen(UbPoint geo)
    {
        var l = MainMap!.FromLatLngToLocal(new PointLatLng(geo.Y, geo.X));
        return new UbPoint(l.X, l.Y);
    }

    private bool RemoveLastDecorationPoint()
    {
        if (DecoPoints.Count == 0) return false;
        DecoPoints.RemoveAt(DecoPoints.Count - 1);
        _decoAdorner?.InvalidateVisual();
        RefreshDecorationPanel();
        return true;
    }

    // ── 키 ──

    private bool IsDecorationKey(KeyEventArgs e) => IsDecorating && IsMapAreaKey(e, _decoPanel);

    private void OnDecorationKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsDecorationKey(e)) return;
        e.Handled = HandleDecorationKey(e.Key, Keyboard.Modifiers, e.OriginalSource is TextBoxBase);
    }

    /// <summary>꾸미기 키 — Esc 단계(점 → 다시 놓기 · 도구 → 고름 → 끝내기) · Enter 마치기 · Backspace · Delete · Ctrl+Z/Y. 입력칸 안에서는 Esc 만.</summary>
    internal bool HandleDecorationKey(Key key, ModifierKeys modifiers, bool inTextBox)
    {
        if (!IsDecorating) return false;
        if (key == Key.Escape)
        {
            if (DecoPoints.Count > 0) { DecoPoints.Clear(); _decoAdorner?.InvalidateVisual(); RefreshDecorationPanel(); }
            else if (_decoTool != null) SetDecorationTool(null);
            else if (_decoSelectedUid != null) SelectDecoration(null);
            else ExitDecoration("escape");
            return true;
        }
        if (inTextBox) return false;
        if (modifiers == ModifierKeys.Control && key is Key.Z or Key.Y)
        {
            if (key == Key.Z && RemoveLastDecorationPoint()) return true;
            _ = UndoDecorationAsync(redo: key == Key.Y);
            return true;
        }
        if (modifiers != ModifierKeys.None) return false;
        switch (key)
        {
            case Key.Enter:
                if (FinishDecorationCommand.CanExecute(null)) _ = PlaceDecorationAsync();
                return true;
            case Key.Back: RemoveLastDecorationPoint(); return true;
            case Key.Delete:
                if (DeleteDecorationCommand.CanExecute(null)) _ = DeleteDecorationAsync();
                return true;
            default: return false;
        }
    }

    // ── 저장 · 지우기 · 되돌리기 ──

    /// <summary>찍은 점으로 놓기 — 새 항목 또는 [다시 놓기] 중인 항목. 저장되면 편집 기록 1단위 · 같은 도구로 계속(다시 놓기면 그 항목을 고른 채로).</summary>
    private async Task PlaceDecorationAsync()
    {
        if (_decoArea is not { } area || _decoTool is not { } tool || _decoBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        var pts = DecoPoints.ToList();
        if (Map3DDecoration.Validate(tool, pts, p => area.Area.Contains(p.X, p.Y)) is { } why) { SetDecorationError(why); return; }
        var before = _decoReplaceUid is { } r ? DecorationItems.FirstOrDefault(i => i.Uid == r) : null;
        string uid = before?.Uid ?? Map3DUserBuilding.NewUid();
        var item = new Map3DDecorationItem(uid, Map3DDecoration.KindOf(tool), Map3DDecoration.GeometryOf(tool), pts, (JObject)DecoProps.DeepClone(),
                                           before != null ? before.Name : string.IsNullOrWhiteSpace(_decoName) ? null : _decoName);
        if (await SaveDecorationAsync(area.Id, uid, item) is not { } saved) return;
        _undoService?.Push(new DecorationCommand($"{Map3DDecoration.UndoPrefix} {(before == null ? "넣기" : "다시 놓기")}", area.Id, uid, before, saved,
                                                 ApplyDecorationStateAsync, ResolveUserBuildingJob));
        DecoPoints.Clear();
        if (before != null) SelectDecoration(uid);   // 옮긴 항목을 고른 채로
        else _decoName = "";
        SetAimStatus($"{Map3DDecoration.ToolText(tool)}을(를) {(before == null ? "놓았습니다" : "옮겼습니다")}", autoHide: true);
        _decoAdorner?.InvalidateVisual();
        RefreshDecorationPanel();
    }

    private async Task ApplyDecorationAsync()
    {
        if (_decoArea is not { } area || DecoSelected is not { } s || _decoBusy || _decoInputError) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        var item = s with { Props = (JObject)DecoProps.DeepClone(), Name = string.IsNullOrWhiteSpace(_decoName) ? null : _decoName };
        if (await SaveDecorationAsync(area.Id, s.Uid, item) is not { } saved) return;
        _undoService?.Push(new DecorationCommand($"{Map3DDecoration.UndoPrefix} 고치기", area.Id, s.Uid, s, saved, ApplyDecorationStateAsync, ResolveUserBuildingJob));
        SelectDecoration(s.Uid);
        SetAimStatus("꾸미기 항목을 저장했습니다", autoHide: true);
    }

    private async Task DeleteDecorationAsync()
    {
        if (_decoArea is not { } area || DecoSelected is not { } s || _decoBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (await SaveDecorationAsync(area.Id, s.Uid, null) is null && _decoError != null) return;
        _undoService?.Push(new DecorationCommand($"{Map3DDecoration.UndoPrefix} 지우기", area.Id, s.Uid, s, null, ApplyDecorationStateAsync, ResolveUserBuildingJob));
        SelectDecoration(null);
        SetAimStatus("꾸미기 항목을 지웠습니다", autoHide: true);
    }

    /// <summary>GIS 편집 기록 적용 — 그 상태로 다시 저장(null = 지움). 실패는 예외(편집 기록이 그 칸을 남긴다).</summary>
    private async Task ApplyDecorationStateAsync(string job, string uid, Map3DDecorationItem? state, CancellationToken ct)
    {
        if (await SaveDecorationAsync(job, uid, state) is null && _decoError != null) throw new InvalidOperationException(_decoError);
        if (_decoSelectedUid == uid && state == null) SelectDecoration(null);
        else if (_decoSelectedUid == uid) SelectDecoration(uid);
    }

    /// <summary>되돌리기 · 다시 하기 — GIS 편집 기록 맨 위가 이 기능 기록일 때만.</summary>
    private async Task UndoDecorationAsync(bool redo)
    {
        if (_decoBusy || _undoService == null) return;
        var next = redo ? _undoService.NextRedoDescription : _undoService.NextUndoDescription;
        if (next?.StartsWith(Map3DDecoration.UndoPrefix, StringComparison.Ordinal) != true)
        {
            SetAimStatus(redo ? "다시 할 것이 없습니다" : "되돌릴 것이 없습니다", autoHide: true);
            return;
        }
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try { if (redo) await _undoService.RedoAsync(); else await _undoService.UndoAsync(); }
        catch (Exception ex) { _log?.Error($"[Decoration] {(redo ? "다시 하기" : "되돌리기")} 실패: {ex.Message}"); }
    }

    /// <summary>
    /// map-3d 에 저장(item) 또는 지움(null) → 목록 다시 받기 · 3D 에 decorations.reload. 실패는 판 오류 줄(범위 밖 · 개수 상한 = 서버 문구).
    /// 저장이면 서버가 저장한 항목, 지움이면 null(실패도 null — 그때는 _decoError 가 있다).
    /// </summary>
    private async Task<Map3DDecorationItem?> SaveDecorationAsync(string job, string uid, Map3DDecorationItem? item)
    {
        if (_map3DClient == null) { _decoError = "3D 서버 설정이 없습니다."; return null; }
        _decoBusy = true;
        _decoError = null;
        RefreshDecorationPanel();
        try
        {
            Map3DDecorationItem? saved = null;
            if (item != null) saved = await _map3DClient.PutDecorationAsync(job, item);
            else await _map3DClient.DeleteDecorationAsync(job, uid);
            _log?.Info($"[Decoration] {(item == null ? "지움" : "저장")} uid={uid} kind={item?.Kind ?? "-"} job={job}");
            PostUserBuilding3D(job, Map3DDecoration.ReloadMessageJson());   // 3D 가 열려 있으면 바로 다시 그린다
            if (_decoArea?.Id == job) await LoadDecorationsAsync(job);
            return saved;
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Decoration] 저장 — 서버 닿지 않음: {ex.Message}");
            _decoError = Map3DUnavailableText;
            NoteMap3DEngineDown("decoration-save");
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")   // 옛 map-3d — 그 주소가 없다
        {
            _decoUnsupported = true;
            _decoTool = null;
            DecoPoints.Clear();
            _decoError = Map3DDecoration.OldEngineText;
        }
        catch (Map3DRequestException ex) { _decoError = ex.Message; }   // 값 범위 · 모양 불일치 · 개수 상한 — 서버의 한국어 문구
        catch (Exception ex)
        {
            _log?.Error($"[Decoration] 저장 실패: {ex.Message}");
            _decoError = "저장하지 못했습니다.";
        }
        finally
        {
            _decoBusy = false;
            RefreshDecorationPanel();
        }
        return null;
    }

    private void SetDecorationError(string? error)
    {
        _decoError = error;
        NotifyOfPropertyChange(nameof(DecorationError));
        NotifyOfPropertyChange(nameof(HasDecorationError));
    }

    private void RefreshDecorationPanel()
    {
        foreach (var name in new[]
                 {
                     nameof(IsDecorating), nameof(DecorationArea), nameof(DecorationItems), nameof(DecorationTool), nameof(DecorationSelectedUid),
                     nameof(HasDecorationSelection), nameof(IsDecorationToolsEnabled), nameof(IsDecorationUnsupported), nameof(DecorationTitle),
                     nameof(DecorationStatusText), nameof(DecorationCountText), nameof(DecorationError), nameof(HasDecorationError), nameof(IsDecorationFinishVisible),
                     nameof(IsDecorationToolTreePoint), nameof(IsDecorationToolTreeLine), nameof(IsDecorationToolTreeArea), nameof(IsDecorationToolGround),
                     nameof(IsDecorationToolProp), nameof(IsDecorationToolWall), nameof(HasDecorationValues), nameof(IsDecorationTreeValues),
                     nameof(IsDecorationSpacingVisible), nameof(IsDecorationDensityVisible), nameof(IsDecorationGroundValues), nameof(IsDecorationPropValues),
                     nameof(IsDecorationWallValues), nameof(IsDecorationHeightVisible), nameof(DecorationSpecies), nameof(DecorationSurface),
                     nameof(DecorationPropType), nameof(DecorationWallType), nameof(DecorationColor), nameof(DecorationHeightText), nameof(DecorationSpacingText),
                     nameof(DecorationDensityText), nameof(DecorationHeadingText), nameof(DecorationScaleText), nameof(DecorationName),
                 })
            NotifyOfPropertyChange(name);
        foreach (var c in new[] { SelectDecorationToolCommand, FinishDecorationCommand, ApplyDecorationCommand, ReplaceDecorationCommand, DeleteDecorationCommand,
                                  UndoDecorationCommand, RedoDecorationCommand, ClearDecorationsCommand, StepDecorationHeadingCommand })
            c.RaiseCanExecuteChanged();
    }

    /// <summary>판 안 키(IBuildingHeightPanelHost 규약) — ↑/↓ = 소품 방향 ±15°(Shift = ±1°), Ctrl+Z = 되돌리기. 손잡이는 없다.</summary>
    private sealed class DecorationPanelHost : IBuildingHeightPanelHost
    {
        private readonly MapViewModel _vm;
        public DecorationPanelHost(MapViewModel vm) => _vm = vm;
        public void BeginHeightDrag() { }
        public void DragHeight(double totalDy) { }
        public void EndHeightDrag(bool canceled) { }
        public bool StepHeightByKey(bool up, bool shift) => _vm.StepDecorationHeading((up ? 1 : -1) * (shift ? 1 : 15));
        public bool UndoHeight()
        {
            if (!_vm.UndoDecorationCommand.CanExecute(null)) return false;
            _ = _vm.UndoDecorationAsync(redo: false);
            return true;
        }
    }

    // ── 「모두 지우기…」 ──

    private async Task RequestClearDecorationsAsync()
    {
        if (_decoArea is not { } item) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingDecorationsClear = item;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "꾸미기 모두 지우기",
                Explain = $"3D 영역 '{item.Title}' 의 {Map3DDecoration.CountText(DecorationItems.Count)}을 모두 지우시겠습니까?\n되돌릴 수 없습니다.",
                MessageModel = new CallClearDecorationsProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[Decoration] 모두 지우기 확인 창 실패: {ex.Message}"); }
    }

    public async Task HandleAsync(CallClearDecorationsProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingDecorationsClear;
        _pendingDecorationsClear = null;
        try
        {
            if (item == null || _map3DClient == null) return;
            await _map3DClient.DeleteAllDecorationsAsync(item.Id, cancellationToken);
            PostUserBuilding3D(item.Id, Map3DDecoration.ReloadMessageJson());
            _log?.Info($"[Decoration] 모두 지움 id={item.Id}");
            SetAimStatus("꾸미기 항목을 모두 지웠습니다", autoHide: true);
            if (_decoArea?.Id == item.Id) { SelectDecoration(null); await LoadDecorationsAsync(item.Id); }
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Decoration] 모두 지우기 — 서버 닿지 않음: {ex.Message}");
            SetDecorationError(Map3DUnavailableText);
            NoteMap3DEngineDown("decoration-clear");
        }
        catch (Map3DRequestException ex) { SetDecorationError($"지우지 못했습니다: {ex.Message}"); }
        catch (Exception ex)
        {
            _log?.Error($"[Decoration] 모두 지우기 실패: {ex.Message}");
            SetDecorationError("지우지 못했습니다.");
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }
}
