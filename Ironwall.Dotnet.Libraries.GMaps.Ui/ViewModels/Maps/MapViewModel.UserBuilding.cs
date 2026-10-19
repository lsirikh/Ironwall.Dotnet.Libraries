using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
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
   Purpose      : 「건물 그리기」(2026-10-19 사용자 요청) — 3D 영역 안 지도 위에 위에서 본 건물 윤곽을 그리고 높이를 넣어 들어올린다.
                  map-3d 가 다시 만들기에서 그 건물을 출처 "user" 로 우선 굽고(겹친 자동 건물 뺌) 옥상 · 외벽을 사진으로 꾸민다(userbuildings.py).
   Note         : ★ 들어가기 = 툴바(Map.UserBuilding.Draw — 화면 가운데 영역) · 영역 배지 우클릭 · 「3D 영역 목록」의 「건물 그리기」.
                    지도 편집 권한(CanEditMap)이 없으면 들어가지 않는다. 그리는 동안은 독점 입력 모드라 3D 에 들어가지 않는다(IsMap3DBlockedByInputMode).
                  ★ 그리기 = 지도 표면(UserBuildingAdorner) → 상태 기계(Map3DUserBuildingDraft): 클릭 = 점 · 첫 점/더블클릭/Enter = 닫기 ·
                    꼭짓점 끌기 · 선분 가운데 손잡이 끌기 = 점 넣기 · 오른쪽 클릭/Delete = 지우기 · Backspace · Esc 두 단계(그리던 점 → 그리기 끝내기) ·
                    Ctrl+Z(윤곽 → 그다음은 GIS 편집 기록의 이 기능 기록). 저장된 건물은 흐린 점선 — 빈 윤곽에서 그 안을 누르면 고치기.
                  ★ 들어올리기 = 판(UserBuildingPanelControl) 층수 · 높이 · 층고 · ±1층 · 손잡이 끌기 · ↑/↓. [저장] = PUT → 3D 상자 미리보기
                    (userbuilding.preview — 다시 굽지 않음) + GIS 편집 기록(UserBuildingCommand). 3D 에서는 다시 만든 뒤 「건물 높이」 판으로 끌어 올린다.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IUserBuildingDrawHost, IHandle<CallClearUserBuildingsProcessMessageModel>
{
    private Map3DAreaItem? _ubArea;
    private Map3DUserBuildingDraft? _ubDraft;
    private Map3DUserBuildingItem? _ubEditing;
    private IReadOnlyList<Map3DUserBuildingItem> _ubItems = Array.Empty<Map3DUserBuildingItem>();
    private double _ubHeight = 9.0;
    private double _ubFloorHeightValue = Map3DBuilding.DefaultFloorHeight;
    private double UbFloorHeight { get => _ubFloorHeightValue > 0 ? _ubFloorHeightValue : Map3DBuilding.DefaultFloorHeight; set => _ubFloorHeightValue = value; }
    private string _ubName = "";
    private string _ubFloorsText = "", _ubHeightText = "", _ubFloorHeightText = "";
    private string? _ubError;
    private bool _ubInputError;
    private bool _ubBusy;
    private double? _ubDragStart;
    private UserBuildingAdorner? _ubAdorner;
    private System.Windows.Window? _ubKeyWindow;
    private Map3DAreaItem? _pendingUserBuildingsClear;

    /// <summary>새 건물 기본 높이(3층).</summary>
    public const double UserBuildingDefaultHeight = 9.0;

    // ── 바인딩 ──

    /// <summary>그리는 중인가(독점 입력 모드).</summary>
    public bool IsUserBuildingDrawing => _ubArea != null;
    /// <summary>그리는 영역(시험 · 계측).</summary>
    public Map3DAreaItem? UserBuildingArea => _ubArea;
    public Map3DUserBuildingDraft? UserBuildingDraft => _ubDraft;
    public IReadOnlyList<Map3DUserBuildingItem> UserBuildingItems => _ubItems ?? Array.Empty<Map3DUserBuildingItem>();
    public string? UserBuildingEditingUid => _ubEditing?.Uid;
    /// <summary>저장된 건물을 고치는 중인가(「이 건물 지우기」 보임).</summary>
    public bool IsUserBuildingEditingSaved => _ubEditing != null;

    private UserBuildingPanelControl? _ubPanel;
    /// <summary>「건물 그리기」 판 — DataContext=this, 머리 띠 ✕ = 그리기 끝내기.</summary>
    public UserBuildingPanelControl UserBuildingPanel => _ubPanel ??= new UserBuildingPanelControl
    {
        DataContext = this,
        CloseCommand = ExitUserBuildingDrawCommand,
        Host = new UserBuildingPanelHost(this),
        ToolCommand = SelectUserBuildingToolCommand,
    };

    public string UserBuildingTitle => _ubEditing is { } e ? (e.Name is { } n ? $"건물 고치기 · {n}" : "건물 고치기") : "건물 만들기";

    // ── 형상 도구 · 지붕 · 외벽 ──

    private UbTool _ubTool;
    private int _ubCircleSides;
    private string? _ubRoof, _ubUse;
    private bool _ubRoofChosen;

    /// <summary>지금 형상 도구(다각형 · 사각형 · 원 · 타원).</summary>
    public UbTool UserBuildingTool => _ubTool;
    public bool IsUserBuildingToolRect => _ubTool == UbTool.Rect;
    public bool IsUserBuildingToolCircle => _ubTool == UbTool.Circle;
    public bool IsUserBuildingToolEllipse => _ubTool == UbTool.Ellipse;
    public bool IsUserBuildingToolPolygon => _ubTool == UbTool.Polygon;
    private int UbCircleSides => _ubCircleSides > 0 ? _ubCircleSides : Map3DUserBuilding.DefaultCircleSides;

    /// <summary>원 · 타원 꼭짓점 수(8~128, 기본 32).</summary>
    public string UserBuildingCircleSidesText
    {
        get => UbCircleSides.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (!int.TryParse(value, out var n) || n < Map3DUserBuilding.MinCircleSides || n > Map3DUserBuilding.MaxCircleSides)
            {
                SetUserBuildingError($"원 꼭짓점 수는 {Map3DUserBuilding.MinCircleSides} ~ {Map3DUserBuilding.MaxCircleSides} 사이로 넣어 주세요.");
                NotifyOfPropertyChange();
                return;
            }
            _ubCircleSides = n;
            if (_ubDraft != null) _ubDraft.CircleSides = n;
            SetUserBuildingError(null);
            NotifyOfPropertyChange();
        }
    }

    public IReadOnlyList<KeyValuePair<string, string>> UserBuildingRoofs => Map3DUserBuilding.Roofs;
    public IReadOnlyList<KeyValuePair<string, string>> UserBuildingUses => Map3DUserBuilding.Uses;

    /// <summary>지붕 모양(flat · gable · cone · dome) — 원으로 만들면 돔을 제안한다(고른 적 없을 때).</summary>
    public string UserBuildingRoof
    {
        get => _ubRoof ?? Map3DUserBuilding.RoofFlat;
        set { _ubRoof = value ?? Map3DUserBuilding.RoofFlat; _ubRoofChosen = true; NotifyOfPropertyChange(); }
    }

    /// <summary>외벽 성격(auto · commercial · industrial · residential).</summary>
    public string UserBuildingUse
    {
        get => _ubUse ?? Map3DUserBuilding.UseAuto;
        set { _ubUse = value ?? Map3DUserBuilding.UseAuto; NotifyOfPropertyChange(); }
    }

    private RelayCommand? _selectUserBuildingToolCommand;
    /// <summary>형상 도구 고르기(매개변수 "Rect" · "Circle" · "Ellipse" · "Polygon") — 그리던 윤곽이 있으면 버리고 새로.</summary>
    public RelayCommand SelectUserBuildingToolCommand => _selectUserBuildingToolCommand ??= new RelayCommand(p =>
    {
        if (!IsUserBuildingDrawing || !Enum.TryParse<UbTool>(p?.ToString(), out var tool)) return;
        _ubTool = tool;
        if (_ubDraft is { } d && !d.SetTool(tool)) ResetUserBuildingDraft(null);   // 그리던 윤곽 → 새 도구로 새로
        _log?.Info($"[UserBuilding] 도구 {tool}");
        RefreshUserBuildingPanel();
    }, _ => IsUserBuildingDrawing);
    public string UserBuildingPointCountText => _ubDraft is { } d ? Map3DUserBuilding.PointCountText(d.Points.Count, d.IsClosed) : "";
    public string UserBuildingAreaText
        => _ubArea is not { } a ? ""
         : _ubDraft is { IsClosed: true } d ? $"3D 영역 '{a.Title}' · 바닥 {Map3DUserBuilding.AreaSquareMeters(d.Points):0} m²"
         : $"3D 영역 '{a.Title}' · {(UserBuildingItems.Count > 0 ? Map3DUserBuilding.CountText(UserBuildingItems.Count) : "직접 그린 건물 없음")}";
    /// <summary>들어올릴 높이(m).</summary>
    public double UserBuildingHeight => _ubHeight;
    public string UserBuildingSummary
        => _ubDraft is not { IsClosed: true } ? "윤곽을 닫으면 저장할 수 있습니다"
         : $"저장하면 {Map3DBuilding.FormatMeters(_ubHeight)} m({Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight)}층) 건물로 들어올립니다";

    public string UserBuildingName
    {
        get => _ubName;
        set { _ubName = (value ?? "").Trim(); NotifyOfPropertyChange(); }
    }

    /// <summary>층수 입력칸(1~200) — 높이 = 층수 × 층고.</summary>
    public string UserBuildingFloorsText
    {
        get => _ubFloorsText;
        set
        {
            _ubFloorsText = value ?? "";
            if (!Map3DBuilding.TryParseFloors(value, out var floors, out var err)) { SetUserBuildingInputError(err); NotifyOfPropertyChange(); return; }
            SetUserBuildingHeight(Map3DBuilding.HeightFromFloors(floors, UbFloorHeight));
        }
    }

    /// <summary>높이 입력칸(m, 1~600).</summary>
    public string UserBuildingHeightText
    {
        get => _ubHeightText;
        set
        {
            _ubHeightText = value ?? "";
            if (!Map3DBuilding.TryParseHeight(value, out var h, out var err)) { SetUserBuildingInputError(err); NotifyOfPropertyChange(); return; }
            SetUserBuildingHeight(h);
        }
    }

    /// <summary>층고 입력칸(m, 2~10) — 높이는 그대로, 층수가 따라 바뀐다.</summary>
    public string UserBuildingFloorHeightText
    {
        get => _ubFloorHeightText;
        set
        {
            _ubFloorHeightText = value ?? "";
            if (!Map3DBuilding.TryParseFloorHeight(value, out var fh, out var err)) { SetUserBuildingInputError(err); NotifyOfPropertyChange(); return; }
            UbFloorHeight = fh;
            _ubInputError = false;
            _ubError = null;
            RefreshUserBuildingPanel();
        }
    }

    public string? UserBuildingError => _ubError ?? _ubDraft?.LastError;
    public bool HasUserBuildingError => UserBuildingError != null;

    private RelayCommand? _drawUserBuildingCommand;
    /// <summary>툴바 「건물 그리기」 — 화면 가운데 3D 영역에서 시작(그리는 중이면 끝낸다). 매개변수로 영역(Map3DAreaItem)을 받을 수도 있다.</summary>
    public RelayCommand DrawUserBuildingCommand => _drawUserBuildingCommand ??= new RelayCommand(p =>
    {
        if (IsUserBuildingDrawing) { ExitUserBuildingDraw("toggle"); return; }
        var item = p as Map3DAreaItem;
        if (item == null && MainMap != null)
        {
            var c = MainMap.Position;
            item = Map3DAreas.FirstOrDefault(a => a.Area.Contains(c.Lng, c.Lat));
        }
        if (item == null) { SetAimStatus("3D 영역 안에서 시작하세요 — 화면 가운데를 3D 영역 안에 두고 다시 누르세요", autoHide: true); return; }
        StartUserBuildingDraw(item);
    });

    private RelayCommand? _exitUserBuildingDrawCommand;
    public RelayCommand ExitUserBuildingDrawCommand => _exitUserBuildingDrawCommand ??= new RelayCommand(_ => ExitUserBuildingDraw("close"));

    private RelayCommand? _stepUserBuildingFloorCommand;
    public RelayCommand StepUserBuildingFloorCommand => _stepUserBuildingFloorCommand ??= new RelayCommand(p =>
    {
        if (!IsUserBuildingDrawing || !int.TryParse(p?.ToString(), out var delta)) return;
        SetUserBuildingHeight(Map3DBuilding.StepFloors(_ubHeight, UbFloorHeight, delta));
    });

    private RelayCommand? _applyUserBuildingCommand;
    /// <summary>「저장」 — 닫힌 윤곽 · 바른 높이일 때.</summary>
    public RelayCommand ApplyUserBuildingCommand => _applyUserBuildingCommand ??= new RelayCommand(
        _ => _ = ApplyUserBuildingAsync(), _ => IsUserBuildingDrawing && !_ubBusy && !_ubInputError && _ubDraft is { IsClosed: true });

    private RelayCommand? _cancelUserBuildingCommand;
    /// <summary>「취소」 — 그리던(고치던) 윤곽을 버리고 빈 윤곽으로(그리기는 그대로).</summary>
    public RelayCommand CancelUserBuildingCommand => _cancelUserBuildingCommand ??= new RelayCommand(_ => ResetUserBuildingDraft(null));

    private RelayCommand? _deleteUserBuildingCommand;
    /// <summary>「이 건물 지우기」 — 고치는 중인 저장된 건물.</summary>
    public RelayCommand DeleteUserBuildingCommand => _deleteUserBuildingCommand ??= new RelayCommand(
        _ => _ = DeleteUserBuildingAsync(), _ => IsUserBuildingDrawing && !_ubBusy && _ubEditing != null);

    private RelayCommand? _undoUserBuildingCommand;
    /// <summary>「되돌리기」(Ctrl+Z) — 윤곽 편집부터, 없으면 GIS 편집 기록의 이 기능 기록.</summary>
    public RelayCommand UndoUserBuildingCommand => _undoUserBuildingCommand ??= new RelayCommand(_ => _ = UndoUserBuildingAsync(), _ => IsUserBuildingDrawing && !_ubBusy);

    // ── 들어가기 · 나가기 ──

    /// <summary>그 영역에서 그리기 시작 — 권한 · 만드는 중 · 영역 확인, 충돌 모드 끝내기, 표면 · 키 · 저장된 건물 불러오기.</summary>
    private void StartUserBuildingDraw(Map3DAreaItem item)
    {
        if (MainMap == null) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (item.IsBaking) { SetAimStatus("이 영역은 다시 만드는 중입니다 — 다 만든 뒤 그리세요", autoHide: true); return; }
        if (IsUserBuildingDrawing) ExitUserBuildingDraw("switch");
        CancelConflictingModesForMeasure();
        if (MainMap.IsMeasuring) MainMap.StopMeasure();
        _ubArea = item;
        _ubItems = Array.Empty<Map3DUserBuildingItem>();
        _ubFloorHeightValue = Map3DBuilding.DefaultFloorHeight;
        ResetUserBuildingDraft(null);
        AttachUserBuildingSurface();
        SyncMap3DWithInputModes();   // 3D 중이면 2D 로(그리기는 2D)
        _log?.Info($"[UserBuilding] 그리기 시작 area={item.Id}");
        SetAimStatus("지도를 눌러 건물 윤곽(위에서 본 모양)을 그리세요 — 첫 점을 누르거나 Enter 로 닫기", autoHide: true);
        _ = LoadUserBuildingsAsync(item.Id);
    }

    /// <summary>그리기 끝내기 — 저장하지 않은 윤곽은 버린다.</summary>
    private void ExitUserBuildingDraw(string cause)
    {
        if (_ubArea == null) return;
        _log?.Info($"[UserBuilding] 그리기 끝 area={_ubArea.Id} cause={cause}");
        _ubArea = null;
        _ubDraft = null;
        _ubEditing = null;
        _ubDragStart = null;
        _ubItems = Array.Empty<Map3DUserBuildingItem>();
        DetachUserBuildingSurface();
        RefreshUserBuildingPanel();
        SyncMap3DWithInputModes();
    }

    /// <summary>빈 윤곽(새 건물) 또는 저장된 건물 고치기로 판을 맞춘다.</summary>
    private void ResetUserBuildingDraft(Map3DUserBuildingItem? edit)
    {
        if (_ubArea is not { } area || MainMap == null) return;
        if (_ubDraft != null) _ubDraft.Changed -= OnUserBuildingDraftChanged;
        var map = MainMap;
        _ubDraft = new Map3DUserBuildingDraft(
            geo => { var l = map.FromLatLngToLocal(new PointLatLng(geo.Y, geo.X)); return new UbPoint(l.X, l.Y); },
            s => { var g = map.FromLocalToLatLng((int)Math.Round(s.X), (int)Math.Round(s.Y)); return Map3DUserBuilding.Round(new UbPoint(g.Lng, g.Lat)); },
            geo => area.Area.Contains(geo.X, geo.Y));
        _ubDraft.CircleSides = UbCircleSides;
        if (edit == null) _ubDraft.SetTool(_ubTool);
        _ubDraft.Changed += OnUserBuildingDraftChanged;
        _ubEditing = edit;
        _ubRoof = edit?.Roof ?? Map3DUserBuilding.RoofFlat;
        _ubUse = edit?.Use ?? Map3DUserBuilding.UseAuto;
        _ubRoofChosen = edit != null;
        _ubDragStart = null;
        _ubError = null;
        _ubInputError = false;
        if (edit != null)
        {
            _ubDraft.Load(edit.Outline);
            _ubHeight = Map3DBuilding.ClampHeight(edit.Height);
            _ubName = edit.Name ?? "";
        }
        else
        {
            _ubHeight = UserBuildingDefaultHeight;
            _ubName = "";
        }
        RefreshUserBuildingPanel();
    }

    private void OnUserBuildingDraftChanged()
    {
        // 원으로 만든 윤곽 — 지붕을 고른 적 없으면 돔 제안(탱크처럼, map-3d 의 둥근 윤곽 규칙)
        if (_ubDraft is { IsClosed: true, CompletedBy: UbTool.Circle } && !_ubRoofChosen && UserBuildingRoof == Map3DUserBuilding.RoofFlat)
            _ubRoof = Map3DUserBuilding.RoofDome;
        _ubAdorner?.InvalidateVisual();
        RefreshUserBuildingPanel();
    }

    private void AttachUserBuildingSurface()
    {
        if (MainMap == null) return;
        try
        {
            var layer = AdornerLayer.GetAdornerLayer(MainMap);
            if (layer != null) { _ubAdorner = new UserBuildingAdorner(MainMap, this, _log); layer.Add(_ubAdorner); }
            else _log?.Warning("[UserBuilding] AdornerLayer 없음 — 지도 표면 없이(키 · 판만)");
            _ubKeyWindow = System.Windows.Window.GetWindow(MainMap);
            if (_ubKeyWindow != null) _ubKeyWindow.PreviewKeyDown += OnUserBuildingKeyDown;
        }
        catch (Exception ex) { _log?.Warning($"[UserBuilding] 표면 붙이기 경고: {ex.Message}"); }
    }

    private void DetachUserBuildingSurface()
    {
        try
        {
            if (_ubAdorner != null && MainMap != null) AdornerLayer.GetAdornerLayer(MainMap)?.Remove(_ubAdorner);
            _ubAdorner?.Dispose();
            if (_ubKeyWindow != null) _ubKeyWindow.PreviewKeyDown -= OnUserBuildingKeyDown;
        }
        catch (Exception ex) { _log?.Warning($"[UserBuilding] 표면 떼기 경고: {ex.Message}"); }
        _ubAdorner = null;
        _ubKeyWindow = null;
    }

    private async Task LoadUserBuildingsAsync(string job)
    {
        if (_map3DClient == null) return;
        try
        {
            var items = await _map3DClient.ListUserBuildingsAsync(job);
            if (_ubArea?.Id != job) return;   // 그 사이 나갔거나 다른 영역
            _ubItems = items;
            _ubAdorner?.InvalidateVisual();
            RefreshUserBuildingPanel();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[UserBuilding] 목록 — 서버 닿지 않음: {ex.Message}");
            SetUserBuildingError(Map3DUnavailableText);
        }
        catch (Exception ex) { _log?.Warning($"[UserBuilding] 목록 실패: {ex.Message}"); }
    }

    // ── 지도 표면(IUserBuildingDrawHost) ──

    void IUserBuildingDrawHost.UserBuildingPointerDown(UbPoint screen, int clickCount)
    {
        if (_ubDraft is not { } d || _ubBusy) return;
        if (clickCount >= 2) { d.DoubleClick(); return; }
        // 빈 윤곽에서 저장된 건물 안을 누르면 그 건물 고치기(형상 도구로 찍던 점이 있으면 그리기 계속)
        if (d.Points.Count == 0 && d.Anchors.Count == 0 && MainMap != null)
        {
            var g = MainMap.FromLocalToLatLng((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
            var hit = UserBuildingItems.LastOrDefault(it => Map3DUserBuilding.Contains(it.Outline, new UbPoint(g.Lng, g.Lat)));
            if (hit != null)
            {
                _log?.Info($"[UserBuilding] 고치기 uid={hit.Uid}");
                ResetUserBuildingDraft(hit);
                return;
            }
        }
        d.PointerDown(screen);
    }

    void IUserBuildingDrawHost.UserBuildingPointerMove(UbPoint screen, bool shift) => _ubDraft?.PointerMove(screen, shift);
    void IUserBuildingDrawHost.UserBuildingPointerUp(UbPoint screen, bool shift) => _ubDraft?.PointerUp(screen, shift);
    void IUserBuildingDrawHost.UserBuildingRightClick(UbPoint screen) => _ubDraft?.RightClick(screen);
    void IUserBuildingDrawHost.UserBuildingPointerCancel() => _ubDraft?.PointerCancel();

    // ── 키 ──

    private void OnUserBuildingKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsUserBuildingDrawing) return;
        bool inBox = e.OriginalSource is TextBoxBase;
        e.Handled = HandleUserBuildingKey(e.Key, Keyboard.Modifiers, inBox);
    }

    /// <summary>그리기 키 — Esc 두 단계 · Enter 닫기 · Backspace · Delete · Ctrl+Z. 입력칸 안에서는 Esc 만. 처리했으면 true.</summary>
    internal bool HandleUserBuildingKey(Key key, ModifierKeys modifiers, bool inTextBox)
    {
        if (_ubDraft is not { } d) return false;
        if (key == Key.Escape)
        {
            if (_ubPanel?.CancelHandleDrag() == true) return true;
            if (d.PointerCancel()) return true;   // 꼭짓점 끄는 중 → 끌기 전으로
            if (d.IsClosed && (d.CanUndo || _ubEditing != null)) { ResetUserBuildingDraft(null); return true; }   // 닫은(고치던) 윤곽 → 빈 윤곽
            if (d.Escape() == UbEscapeResult.Exit) ExitUserBuildingDraw("escape");
            return true;
        }
        if (inTextBox) return false;
        if (modifiers == ModifierKeys.Control && key == Key.Z) { _ = UndoUserBuildingAsync(); return true; }
        if (modifiers != ModifierKeys.None) return false;
        switch (key)
        {
            case Key.Enter:
                if (!d.IsClosed) d.Close();
                else if (ApplyUserBuildingCommand.CanExecute(null)) _ = ApplyUserBuildingAsync();
                return true;
            case Key.Back: d.RemoveLast(); return true;
            case Key.Delete: d.RemoveSelected(); return true;
            default: return false;
        }
    }

    // ── 높이 ──

    private void SetUserBuildingHeight(double height)
    {
        _ubHeight = Map3DBuilding.ClampHeight(height);
        _ubError = null;
        _ubInputError = false;
        RefreshUserBuildingPanel();
    }

    private void SetUserBuildingError(string? error)
    {
        _ubError = error;
        NotifyOfPropertyChange(nameof(UserBuildingError));
        NotifyOfPropertyChange(nameof(HasUserBuildingError));
    }

    private void SetUserBuildingInputError(string? error)
    {
        _ubInputError = true;
        SetUserBuildingError(error);
        ApplyUserBuildingCommand.RaiseCanExecuteChanged();
    }

    private void RefreshUserBuildingPanel()
    {
        if (!_ubInputError)
        {
            _ubHeightText = IsUserBuildingDrawing ? Map3DBuilding.FormatMeters(_ubHeight) : "";
            _ubFloorsText = IsUserBuildingDrawing ? Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight).ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
        }
        _ubFloorHeightText = Map3DBuilding.FormatMeters(UbFloorHeight);
        foreach (var name in new[]
                 {
                     nameof(IsUserBuildingDrawing), nameof(UserBuildingArea), nameof(UserBuildingDraft), nameof(IsUserBuildingEditingSaved),
                     nameof(UserBuildingTitle), nameof(UserBuildingPointCountText), nameof(UserBuildingAreaText), nameof(UserBuildingHeight),
                     nameof(UserBuildingSummary), nameof(UserBuildingName), nameof(UserBuildingFloorsText), nameof(UserBuildingHeightText),
                     nameof(UserBuildingFloorHeightText), nameof(UserBuildingError), nameof(HasUserBuildingError),
                     nameof(UserBuildingTool), nameof(IsUserBuildingToolRect), nameof(IsUserBuildingToolCircle), nameof(IsUserBuildingToolEllipse),
                     nameof(IsUserBuildingToolPolygon), nameof(UserBuildingCircleSidesText), nameof(UserBuildingRoof), nameof(UserBuildingUse),
                 })
            NotifyOfPropertyChange(name);
        ApplyUserBuildingCommand.RaiseCanExecuteChanged();
        DeleteUserBuildingCommand.RaiseCanExecuteChanged();
        UndoUserBuildingCommand.RaiseCanExecuteChanged();
    }

    /// <summary>판 손잡이 · 키(IBuildingHeightPanelHost 규약) — 「건물 높이」 판과 같은 수식(10 px = 1 m · ↑/↓ = ±1층 · Shift = 0.1 m).</summary>
    private sealed class UserBuildingPanelHost : IBuildingHeightPanelHost
    {
        private readonly MapViewModel _vm;
        public UserBuildingPanelHost(MapViewModel vm) => _vm = vm;
        public void BeginHeightDrag() { if (_vm.IsUserBuildingDrawing) _vm._ubDragStart = _vm._ubHeight; }
        public void DragHeight(double totalDy) { if (_vm._ubDragStart is double s) _vm.SetUserBuildingHeight(Map3DBuilding.DragHeight(s, totalDy)); }
        public void EndHeightDrag(bool canceled)
        {
            if (_vm._ubDragStart is double s && canceled) _vm.SetUserBuildingHeight(s);
            _vm._ubDragStart = null;
        }
        public bool StepHeightByKey(bool up, bool shift)
        {
            if (!_vm.IsUserBuildingDrawing) return false;
            _vm.SetUserBuildingHeight(Map3DBuilding.KeyStep(_vm._ubHeight, _vm.UbFloorHeight, up, shift));
            return true;
        }
        public bool UndoHeight()
        {
            if (!_vm.UndoUserBuildingCommand.CanExecute(null)) return false;
            _ = _vm.UndoUserBuildingAsync();
            return true;
        }
    }

    // ── 저장 · 지우기 · 되돌리기 ──

    private async Task ApplyUserBuildingAsync()
    {
        if (_ubArea is not { } area || _ubDraft is not { } d || _ubBusy || _ubInputError) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (d.Validate() is { } why) { SetUserBuildingError(why); return; }
        string uid = _ubEditing?.Uid ?? Map3DUserBuilding.NewUid();
        var before = _ubEditing is { } e ? StateOf(e) : null;
        int floors = Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight);
        var after = new UserBuildingState(d.Points.ToList(), _ubHeight, floors, string.IsNullOrWhiteSpace(_ubName) ? null : _ubName)
                    { Roof = UserBuildingRoof, Use = UserBuildingUse };
        if (await SaveUserBuildingAsync(area.Id, uid, after))
        {
            _undoService?.Push(new UserBuildingCommand($"{Map3DUserBuilding.UndoPrefix} {(before == null ? "넣기" : "고치기")}", area.Id, uid, before, after,
                                                       ApplyUserBuildingStateAsync));
            SetAimStatus(Map3DUserBuilding.SavedText(after.Height, floors), autoHide: true);
            ResetUserBuildingDraft(null);   // 다음 건물을 바로 그릴 수 있게
        }
    }

    private async Task DeleteUserBuildingAsync()
    {
        if (_ubArea is not { } area || _ubEditing is not { } e || _ubBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (await SaveUserBuildingAsync(area.Id, e.Uid, null))
        {
            _undoService?.Push(new UserBuildingCommand($"{Map3DUserBuilding.UndoPrefix} 지우기", area.Id, e.Uid,
                                                       StateOf(e), null, ApplyUserBuildingStateAsync));
            SetAimStatus("건물을 지웠습니다 — 3D 를 다시 만들면 원래 건물로 돌아갑니다", autoHide: true);
            ResetUserBuildingDraft(null);
        }
    }

    private static UserBuildingState StateOf(Map3DUserBuildingItem e)
        => new(e.Outline, e.Height, e.Floors, e.Name) { Roof = e.Roof, Use = e.Use };

    /// <summary>되돌리기 — 윤곽 편집 칸이 있으면 그것, 없으면 GIS 편집 기록 맨 위가 이 기능 기록일 때만 그것.</summary>
    private async Task UndoUserBuildingAsync()
    {
        if (_ubBusy) return;
        if (_ubDraft?.Undo() == true) return;
        if (_undoService?.NextUndoDescription?.StartsWith(Map3DUserBuilding.UndoPrefix, StringComparison.Ordinal) != true)
        {
            SetAimStatus("되돌릴 것이 없습니다", autoHide: true);
            return;
        }
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try { await _undoService.UndoAsync(); }
        catch (Exception ex) { _log?.Error($"[UserBuilding] 되돌리기 실패: {ex.Message}"); }
    }

    /// <summary>GIS 편집 기록 적용(UserBuildingCommand) — 그 상태로 다시 저장(null = 지움). 실패는 예외(편집 기록이 그 칸을 남긴다).</summary>
    private async Task ApplyUserBuildingStateAsync(string job, string uid, UserBuildingState? state, CancellationToken ct)
    {
        if (!await SaveUserBuildingAsync(job, uid, state)) throw new InvalidOperationException(_ubError ?? "직접 그린 건물 저장 실패");
        if (_ubEditing?.Uid == uid) ResetUserBuildingDraft(null);
        SetAimStatus(state == null ? "직접 그린 건물을 지웠습니다(되돌리기)" : "직접 그린 건물을 되돌렸습니다", autoHide: true);
    }

    /// <summary>map-3d 에 저장(state) 또는 지움(null) → 3D 상자 미리보기 · 목록 갱신. 실패는 판 오류 줄. 성공이면 true.</summary>
    private async Task<bool> SaveUserBuildingAsync(string job, string uid, UserBuildingState? state)
    {
        if (_map3DClient == null) return false;
        _ubBusy = true;
        _ubError = null;
        RefreshUserBuildingPanel();
        try
        {
            if (state != null)
            {
                var saved = await _map3DClient.PutUserBuildingAsync(job, uid, state.Outline, state.Height, state.Floors, state.Name, state.Roof, state.Use);
                PostUserBuilding3D(job, Map3DUserBuilding.PreviewMessageJson(uid, saved.Outline, saved.Height));
                _log?.Info($"[UserBuilding] 저장 uid={uid} 점={saved.Outline.Count} 높이={saved.Height} job={job}");
            }
            else
            {
                await _map3DClient.DeleteUserBuildingAsync(job, uid);
                PostUserBuilding3D(job, Map3DUserBuilding.PreviewMessageJson(uid, null, null));
                _log?.Info($"[UserBuilding] 지움 uid={uid} job={job}");
            }
            if (_ubArea?.Id == job) await LoadUserBuildingsAsync(job);
            _ = RefreshMap3DAreasAsync();   // 영역 목록 「직접 그린 건물 n개」
            return true;
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[UserBuilding] 저장 — 서버 닿지 않음: {ex.Message}");
            SetUserBuildingError(Map3DUnavailableText);
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")   // 옛 map-3d — 그 주소가 없다(FastAPI 기본 문구)
        {
            _log?.Warning($"[UserBuilding] 저장 — 옛 서버({(int)ex.StatusCode} {ex.Message})");
            SetUserBuildingError(Map3DUserBuilding.OldServerText);
        }
        catch (Map3DRequestException ex) { SetUserBuildingError($"저장하지 못했습니다: {ex.Message}"); }
        catch (Exception ex)
        {
            _log?.Error($"[UserBuilding] 저장 실패: {ex.Message}");
            SetUserBuildingError("저장하지 못했습니다.");
        }
        finally
        {
            _ubBusy = false;
            RefreshUserBuildingPanel();
        }
        return false;
    }

    /// <summary>그 장면을 띄워 둔 3D 에만 상자 미리보기를 보낸다(다른 장면 · 3D 없음은 다음 불러오기에서 서버 목록으로 그린다).</summary>
    private void PostUserBuilding3D(string job, string json)
    {
        if (_map3DBuildingPost == null && Map3DHost.Job != job) return;
        try { PostBuilding3D(json); }
        catch (Exception ex) { _log?.Error($"[UserBuilding] 3D 로 보내기 실패(격리): {ex.Message}"); }
    }

    // ── 영역 목록 「모두 지우기…」 ──

    /// <summary>「직접 그린 건물 n개 — 모두 지우기…」 — 표준 확인 팝업. "확인" → HandleAsync(CallClearUserBuildingsProcessMessageModel).</summary>
    private async Task RequestClearUserBuildingsAsync(Map3DAreaItem item)
    {
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingUserBuildingsClear = item;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "직접 그린 건물 모두 지우기",
                Explain = $"3D 영역 '{item.Title}' 의 {Map3DUserBuilding.CountText(item.Scene.UserBuildings)}을 모두 지우시겠습니까?\n"
                          + "다시 만들면 그 자리는 자동으로 찾은 건물로 돌아갑니다.",
                MessageModel = new CallClearUserBuildingsProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[UserBuilding] 모두 지우기 확인 창 실패: {ex.Message}"); }
    }

    public async Task HandleAsync(CallClearUserBuildingsProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingUserBuildingsClear;
        _pendingUserBuildingsClear = null;
        try
        {
            if (item == null || _map3DClient == null) return;
            int n = await _map3DClient.DeleteAllUserBuildingsAsync(item.Id, cancellationToken);
            PostUserBuilding3D(item.Id, Map3DUserBuilding.ClearMessageJson());
            if (_ubArea?.Id == item.Id) { ResetUserBuildingDraft(null); await LoadUserBuildingsAsync(item.Id); }
            _log?.Info($"[UserBuilding] 모두 지움 id={item.Id} n={n}");
            SetAimStatus($"직접 그린 건물 {n}개를 지웠습니다", autoHide: true);
            _ = RefreshMap3DAreasAsync();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[UserBuilding] 모두 지우기 — 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
        }
        catch (Map3DRequestException ex) { SetAimStatus($"직접 그린 건물을 지울 수 없습니다: {ex.Message}", autoHide: true); }
        catch (Exception ex)
        {
            _log?.Error($"[UserBuilding] 모두 지우기 실패: {ex.Message}");
            SetAimStatus("직접 그린 건물 지우기에 실패했습니다.", autoHide: true);
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }
}
