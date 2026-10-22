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
                    지도 편집 모드가 아니거나(꺼짐 = 보기 전용, 2026-10-21) 지도 편집 권한(CanEditMap)이 없으면 들어가지 않는다. 그리는 동안은 독점 입력 모드라 3D 에 들어가지 않는다(IsMap3DBlockedByInputMode).
                    단, 3D 중에 시작하면(3D 에서 고름 · 툴바) 3D 위 판으로 3D 에 머문다(그리기 · 손잡이 = draw.* · edit.*, MapViewModel.Edit3D).
                  ★ 그리기 = 지도 표면(UserBuildingAdorner) → 상태 기계(Map3DUserBuildingDraft): 클릭 = 점 · 첫 점/더블클릭/Enter = 닫기 ·
                    꼭짓점 끌기 · 선분 가운데 손잡이 끌기 = 점 넣기 · 오른쪽 클릭/Delete = 지우기 · Backspace · Esc 두 단계(그리던 점 → 그리기 끝내기) ·
                    Ctrl+Z(윤곽 → 그다음은 GIS 편집 기록의 이 기능 기록). 저장된 건물은 흐린 점선 — 빈 윤곽에서 그 안을 누르면 고치기.
                  ★ 들어올리기 = 판(UserBuildingPanelControl) 층수 · 높이 · 층고 · ±1층 · 손잡이 끌기 · ↑/↓. [저장] = PUT → 3D 상자 미리보기
                    (userbuilding.preview — 다시 굽지 않음) + GIS 편집 기록(UserBuildingCommand). 3D 에서는 다시 만든 뒤 「건물 높이」 판으로 끌어 올린다.
                  ★ 합치기(2026-10-20) = 저장된 건물을 Ctrl+클릭으로 여러 개 고름(굵은 실선 + 네 모서리 네모로 표시 · Esc = 고름 풀기) →
                    판 [합치기] = POST …/user-buildings/merge → 합친 건물을 바로 고치기로 연다 + GIS 편집 기록(UserBuildingMergeCommand:
                    되돌리기 = 합친 것 지우고 원래 것들 그대로 다시 저장, 다시 하기 = 다시 합치기).
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
    /// <summary>마지막 저장을 서버가 400(값 · 모양 거절 문구)으로 거절했는가.</summary>
    private bool _ubLastSaveRejected;
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

    /// <summary>지금 형상 도구(다각형 · 사각형 · 원 · 타원 · 삼각형).</summary>
    public UbTool UserBuildingTool => _ubTool;
    public bool IsUserBuildingToolRect => _ubTool == UbTool.Rect;
    public bool IsUserBuildingToolTriangle => _ubTool == UbTool.Triangle;
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
        set
        {
            _ubRoof = value ?? Map3DUserBuilding.RoofFlat;
            _ubRoofChosen = true;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(UserBuildingRoofStatus));
            NotifyOfPropertyChange(nameof(HasUserBuildingRoofStatus));
            PreviewUserBuildingDraft();
        }
    }

    /// <summary>
    /// 고른 지붕(박공 · 원뿔 · 돔)이 닫은 윤곽으로는 안 되는 모양(Map3DUserBuilding.CanBuildRoof)이면 지붕 줄 밑 짧은 상태 글 — map-3d 가 평지붕으로 짓는다.
    /// 아니면 null(닫기 전에는 판정하지 않는다).
    /// </summary>
    public string? UserBuildingRoofStatus
        => _ubDraft is { IsClosed: true } d && !Map3DUserBuilding.CanBuildRoof(UserBuildingRoof, d.Points) ? Map3DUserBuilding.GableFlatOnlyText : null;
    public bool HasUserBuildingRoofStatus => UserBuildingRoofStatus != null;

    /// <summary>외벽 성격(auto · commercial · industrial · residential).</summary>
    public string UserBuildingUse
    {
        get => _ubUse ?? Map3DUserBuilding.UseAuto;
        set { _ubUse = value ?? Map3DUserBuilding.UseAuto; NotifyOfPropertyChange(); PreviewUserBuildingDraft(); }
    }

    // ── 외관(외벽 색 · 지붕 색 · 외벽 무늬, twin-contract B) ──

    private string? _ubWallColor, _ubRoofColor, _ubFacade;
    public System.ComponentModel.ICollectionView UserBuildingFacades => _ubFacadeView ??= NewFacadeView();   // 분류 머리글로 묶음(MapViewModel.BuildingLook)
    public IReadOnlyList<KeyValuePair<string, string>> UserBuildingColors => Map3DLook.ColorsWith(_ubWallColor, _ubRoofColor);

    /// <summary>외벽 무늬(Map3DLook.FacadeCatalog · 엔진 목록의 키 — null 은 콤보가 항목을 갈며 비운 것이라 무시).</summary>
    public string UserBuildingFacade
    {
        get => _ubFacade ?? Map3DLook.FacadeAuto;
        set { if (value == null) return; _ubFacade = Map3DLook.NormalizeFacade(value); NotifyOfPropertyChange(); PreviewUserBuildingDraft(); }
    }

    /// <summary>외벽 색("#rrggbb", "" = 자동 — null 은 콤보가 목록에 없는 값을 비우며 되쓴 것이라 무시).</summary>
    public string UserBuildingWallColor
    {
        get => _ubWallColor ?? "";
        set { if (value == null) return; _ubWallColor = Map3DLook.NormalizeColor(value); NotifyOfPropertyChange(); PreviewUserBuildingDraft(); }
    }

    /// <summary>지붕 색("#rrggbb", "" = 자동).</summary>
    public string UserBuildingRoofColor
    {
        get => _ubRoofColor ?? "";
        set { if (value == null) return; _ubRoofColor = Map3DLook.NormalizeColor(value); NotifyOfPropertyChange(); PreviewUserBuildingDraft(); }
    }

    private Map3DLookState UserBuildingLook => new(false, _ubWallColor, _ubRoofColor, UserBuildingFacade);

    // ── 위로 갈수록 좁게 · 넓게(taper_deg, 2026-10-22 사용자 — Fusion 360 Extrude 의 기울기처럼) ──

    private double _ubTaper;
    private string _ubTaperText = "0";

    /// <summary>기울기 막대(−30~+30°, 1° 단위) — + = 위로 갈수록 좁게, − = 넓게, 0 = 수직.</summary>
    public double UserBuildingTaperDeg
    {
        get => _ubTaper;
        set => SetUserBuildingTaper(value);
    }

    /// <summary>기울기 숫자 칸(정수 °, 앞의 + 허용).</summary>
    public string UserBuildingTaperText
    {
        get => _ubTaperText;
        set
        {
            _ubTaperText = value ?? "";
            if (!Map3DUserBuilding.TryParseTaper(value, out var deg, out var err)) { SetUserBuildingInputError(err); NotifyOfPropertyChange(); return; }
            SetUserBuildingTaper(deg);
        }
    }

    /// <summary>"수직" · "위로 갈수록 좁게 10°" 같은 쉬운 말 한 줄.</summary>
    public string UserBuildingTaperStatus => Map3DUserBuilding.TaperText(_ubTaper);

    private RelayCommand? _resetUserBuildingTaperCommand;
    /// <summary>[0] — 수직으로 되돌리기.</summary>
    public RelayCommand ResetUserBuildingTaperCommand => _resetUserBuildingTaperCommand ??= new RelayCommand(
        _ => SetUserBuildingTaper(0), _ => IsUserBuildingDrawing && !_ubBusy && _ubTaper != 0);

    private void SetUserBuildingTaper(double deg)
    {
        if (!IsUserBuildingDrawing) return;
        _ubTaper = Map3DUserBuilding.ClampTaper(deg);
        _ubError = null;
        _ubInputError = false;
        RefreshUserBuildingPanel();
    }

    // ── 3D 위 판 — 값을 바꾸는 즉시 3D 상자 미리보기(2026-10-22 사용자 "6층으로 했는데 그대로 3층") ──

    /// <summary>마지막으로 3D 에 보낸 이 판의 상자(userbuilding.preview) — 같은 내용은 다시 보내지 않는다.</summary>
    private string? _ubPreviewJson;
    /// <summary>저장하지 않은 판 값이 3D 상자에 보이는 중 — 취소 · 다른 건물 · 끝내기에서 저장된 모양으로 되돌린다.</summary>
    private bool _ubPreviewDirty;
    /// <summary>새 윤곽의 미리보기 uid — [저장] 때 그대로 쓴다(3D 에 미리 그린 상자가 그 건물이 되게).</summary>
    private string? _ubNewUid;

    private string UserBuildingDraftUid => _ubEditing?.Uid ?? (_ubNewUid ??= Map3DUserBuilding.NewUid());

    /// <summary>판 값 그대로의 건물(미리보기용).</summary>
    private Map3DUserBuildingItem UserBuildingDraftItem(Map3DUserBuildingDraft d)
        => new(UserBuildingDraftUid, d.Points.ToList(), _ubHeight, Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight),
               string.IsNullOrWhiteSpace(_ubName) ? null : _ubName, null)
           {
               Roof = UserBuildingRoof, Use = UserBuildingUse, WallColor = _ubWallColor, RoofColor = _ubRoofColor, Facade = UserBuildingFacade, TaperDeg = _ubTaper,
           };

    /// <summary>
    /// 3D 위 판이고 윤곽이 닫혀 있으면 판 값(높이 · 층수 · 지붕 · 외관 · 기울기)으로 3D 상자를 바로 그린다. 저장은 [저장] 에서만.
    /// 2D 그리기는 3D 를 접어 두므로 보내지 않는다(저장 뒤 미리보기만 — 예전대로).
    /// </summary>
    private void PreviewUserBuildingDraft()
    {
        if (!_ubIn3D || _ubArea is not { } area || _ubDraft is not { IsClosed: true } d || _ubInputError || _ubBusy || d.Validate() != null) return;
        var json = Map3DUserBuilding.PreviewMessageJson(UserBuildingDraftItem(d));
        if (json == _ubPreviewJson) return;
        _ubPreviewJson = json;
        _ubPreviewDirty = true;
        PostUserBuilding3D(area.Id, json);
    }

    /// <summary>저장하지 않은 미리보기를 걷는다 — 저장된 건물이면 그 모양, 새 윤곽이면 상자 지움.</summary>
    private void RevertUserBuildingPreview()
    {
        bool dirty = _ubPreviewDirty;
        _ubPreviewDirty = false;
        _ubPreviewJson = null;
        if (!dirty || _ubArea is not { } area) return;
        string uid = UserBuildingDraftUid;
        PostUserBuilding3D(area.Id, UserBuildingItems.FirstOrDefault(i => i.Uid == uid) is { } saved
            ? Map3DUserBuilding.PreviewMessageJson(saved) : Map3DUserBuilding.PreviewMessageJson(uid, null, null));
    }

    // ── 3D 에서 고르기 → 2D 고치기 판 ──

    /// <summary>3D 에서 고른 직접 그린 건물 — 목록을 받은 뒤 그 건물 고치기로 연다.</summary>
    private string? _ubPendingEditUid;

    /// <summary>
    /// 3D 에서 직접 그린 건물을 눌렀다(userbuilding.pick · user_uid 있는 building) — 그 3D 영역에서 「건물 그리기」를 3D 위 판으로 시작하고
    /// (3D 에 머문다, edit3d-contract 1단계 — 네이티브 창 3D 만 예전처럼 2D 로) 목록을 받으면 그 건물 고치기 판을 연다. 3D 의 그 상자에 고름 표시(userbuilding.select).
    /// </summary>
    private void OnMap3DUserBuildingPicked(string uid, bool ctrl = false)
    {
        if (_map3DState != Map3DState.Shown || _map3DJob is not { } job || Map3DAreas.FirstOrDefault(a => a.Id == job) is not { } area) return;
        if (!RequireMapEditMode(notify: false))
        {
            // 보기 전용 — 판 · 강조 없음. 엔진에 고르기를 끄는 메시지는 없어 GIS 가 무시하고, 엔진이 그렸을 수 있는 강조만 지운다
            _log?.Info($"[UserBuilding] 3D 에서 고름 — 편집 모드 아님(무시) uid={uid}");
            PostUserBuilding3D(job, Map3DUserBuilding.SelectMessageJson(null));
            return;
        }
        _log?.Info($"[UserBuilding] 3D 에서 고름 uid={uid} job={job}");
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        CloseBuildingHeightPanel(clearUndo: false);
        PostUserBuilding3D(job, Map3DUserBuilding.SelectMessageJson(uid));
        bool in3D = ShouldEditIn3D;
        bool same = in3D && _ubIn3D && _ubArea?.Id == job;   // 이미 3D 위 판 — 그 건물만 연다
        // Ctrl+클릭(엔진 키 · 옛 엔진은 호스트 키) = 고름에 넣기 · 빼기 — 2D 판의 Ctrl+클릭과 같다(고치던 건물부터 고름에 넣는다, 여러 개 함께 고치기)
        if (same && (ctrl || (Map3DModifiers() & System.Windows.Input.ModifierKeys.Control) != 0) && UserBuildingItems.Any(i => i.Uid == uid))
        {
            var seed = _ubEditing?.Uid;
            var keep = UbSelected.ToList();
            if (_ubEditing != null) ResetUserBuildingDraft(null);
            UbSelected.Clear();
            UbSelected.AddRange(keep);
            if (seed != null && !UbSelected.Contains(seed)) UbSelected.Add(seed);
            if (uid != seed && !UbSelected.Remove(uid)) UbSelected.Add(uid);
            _log?.Info($"[UserBuilding] 3D 에서 고름 {UbSelected.Count}개");
            LoadUserBuildingGroupFields();
            RefreshUserBuildingPanel();
            return;
        }
        if (!same) StartUserBuildingDraw(area, in3D);
        _ubPendingEditUid = uid;
        if (_ubArea?.Id != job) { _ubPendingEditUid = null; return; }   // 시작하지 못함(만드는 중 등)
        if (UserBuildingItems.FirstOrDefault(i => i.Uid == uid) is { } hit) { _ubPendingEditUid = null; ResetUserBuildingDraft(hit); }   // 목록을 이미 받았다
        else if (same) _ = LoadUserBuildingsAsync(job);
    }

    private RelayCommand? _selectUserBuildingToolCommand;
    /// <summary>형상 도구 고르기(매개변수 "Rect" · "Triangle" · "Circle" · "Ellipse" · "Polygon") — 그리던 윤곽이 있으면 버리고 새로.</summary>
    public RelayCommand SelectUserBuildingToolCommand => _selectUserBuildingToolCommand ??= new RelayCommand(p =>
    {
        if (!IsUserBuildingDrawing || !Enum.TryParse<UbTool>(p?.ToString(), out var tool)) return;
        _ubTool = tool;
        if (_ubDraft is { } d && !d.SetTool(tool)) ResetUserBuildingDraft(null);   // 그리던 윤곽 → 새 도구로 새로
        if (_ubIn3D)   // 3D 위 판 — 3D 에서 땅을 눌러 그린다(draw.done 으로 윤곽이 온다)
            StartDraw3D(Map3DEdit.ModeOf(tool), Map3DEdit.TargetUserBuilding,
                        new Newtonsoft.Json.Linq.JObject { ["height"] = _ubHeight, ["floors"] = Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight) });
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
         : _ubIn3D ? $"3D 에 {Map3DBuilding.FormatMeters(_ubHeight)} m({Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight)}층)로 바로 보입니다 — [저장] 해야 남습니다"
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
        if (item == null && ShouldEditIn3D) item = Map3DAreas.FirstOrDefault(a => a.Id == _map3DJob);   // 3D 중 — 보고 있는 영역에서 3D 위 판으로
        if (item == null && MainMap != null)
        {
            var c = MainMap.Position;
            item = Map3DAreas.FirstOrDefault(a => a.Area.Contains(c.Lng, c.Lat));
        }
        if (item == null) { SetAimStatus("3D 영역 안에서 시작하세요 — 화면 가운데를 3D 영역 안에 두고 다시 누르세요", autoHide: true); return; }
        StartUserBuildingDraw(item, in3D: ShouldEditIn3D && item.Id == _map3DJob);
    }, _ => IsEditModeEnabled || IsUserBuildingDrawing);   // 편집 모드에서만 시작(그리는 중이면 끝내기는 늘)

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
        _ => _ = ApplyUserBuildingAsync(), _ => IsUserBuildingDrawing && !_ubBusy && !_ubInputError && (_ubDraft is { IsClosed: true } || IsUserBuildingGroup));

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

    private RelayCommand? _redoUserBuildingCommand;
    /// <summary>「다시 하기」(Ctrl+Y) — GIS 편집 기록의 이 기능 기록을 다시.</summary>
    public RelayCommand RedoUserBuildingCommand => _redoUserBuildingCommand ??= new RelayCommand(_ => _ = RedoUserBuildingAsync(), _ => IsUserBuildingDrawing && !_ubBusy);

    // ── 여러 개 고르기 · 합치기 ──

    private List<string>? _ubSelectedList;
    /// <summary>Ctrl+클릭으로 고른 저장된 건물 uid(고른 순서) — 지연 초기화(생성자 없이 만든 시험 VM 에서도).</summary>
    private List<string> UbSelected => _ubSelectedList ??= new List<string>();

    /// <summary>고른 저장된 건물 uid(지도 표면이 굵은 실선 + 모서리 네모로 그린다).</summary>
    public IReadOnlyCollection<string> UserBuildingSelectedUids => UbSelected;
    public bool HasUserBuildingSelection => UbSelected.Count > 0;
    /// <summary>판 「도형 N개 선택」 줄.</summary>
    public string UserBuildingSelectionText => Map3DUserBuilding.SelectionText(UbSelected.Count);
    // ── 여러 개 함께 고치기(2026-10-22 사용자 "건물들 멀티플하게 선택해서 속성을 변경하면 같이 적용되게 우리 마커처럼") ──
    // 2D 마커 여러 개 속성창과 같다: 고르면 판 칸에 공통 값(다르면 첫 건물 값 + 「여러 값」), 손댄 칸만 고른 건물 모두에 [✓ 적용], 되돌리기 한 번에 모두.

    /// <summary>판 칸 값(높이 · 지붕 · 쓰임 · 외관 · 기울기) — 손댄 칸 판정용.</summary>
    private sealed record UbGroupFields(double Height, string Roof, string Use, string? WallColor, string? RoofColor, string Facade, double TaperDeg);
    private UbGroupFields? _ubGroupBase;
    private IReadOnlyList<string> _ubGroupMixed = Array.Empty<string>();

    /// <summary>저장된 건물을 2개 이상 골랐다(고치는 건물 없음) — 판 값을 고른 것 모두에 함께 적용.</summary>
    public bool IsUserBuildingGroup => _ubEditing == null && UbSelected.Count >= 2;
    /// <summary>판 「고른 n개에 함께 적용 · 여러 값: …」 줄.</summary>
    public string UserBuildingGroupText => !IsUserBuildingGroup ? ""
        : _ubGroupMixed.Count > 0 ? $"[✓ 적용] = 고른 {UbSelected.Count}개에 바꾼 칸만 · {Map3DBuilding.MixedText}: {string.Join(" · ", _ubGroupMixed)}"
        : $"[✓ 적용] = 고른 {UbSelected.Count}개에 바꾼 칸만";

    private UbGroupFields CurrentUbGroupFields() => new(_ubHeight, UserBuildingRoof, UserBuildingUse, _ubWallColor, _ubRoofColor, UserBuildingFacade, _ubTaper);

    /// <summary>고름이 바뀌면 판 칸을 고른 건물 값으로 — 모두 같으면 그 값, 다르면 첫 건물 값에 「여러 값」 표시(그 칸은 손대지 않으면 적용하지 않는다).</summary>
    private void LoadUserBuildingGroupFields()
    {
        var items = UbSelected.Select(uid => UserBuildingItems.FirstOrDefault(i => i.Uid == uid)).OfType<Map3DUserBuildingItem>().ToList();
        if (_ubEditing != null || items.Count < 2) { _ubGroupBase = null; _ubGroupMixed = Array.Empty<string>(); return; }
        var f = items[0];
        var mixed = new List<string>();
        void Check<T>(string name, Func<Map3DUserBuildingItem, T> get) { if (items.Any(i => !Equals(get(i), get(f)))) mixed.Add(name); }
        Check("높이", i => i.Height); Check("지붕", i => i.Roof); Check("쓰임", i => i.Use); Check("외벽 무늬", i => i.Facade);
        Check("외벽 색", i => i.WallColor); Check("지붕 색", i => i.RoofColor); Check("좁게 · 넓게", i => i.TaperDeg);
        _ubHeight = f.Height; _ubRoof = f.Roof; _ubUse = f.Use; _ubFacade = f.Facade; _ubWallColor = f.WallColor; _ubRoofColor = f.RoofColor; _ubTaper = f.TaperDeg;
        _ubGroupMixed = mixed;
        _ubGroupBase = CurrentUbGroupFields();
    }

    /// <summary>[✓ 적용](여러 개) — 손댄 칸만 건물마다 자기 값에 덮어 저장(윤곽 · 이름은 그대로) → 편집 기록 1단위(MacroCommand).</summary>
    private async Task ApplyUserBuildingGroupAsync(Map3DAreaItem area)
    {
        if (_ubGroupBase is not { } b) return;
        var c = CurrentUbGroupFields();
        bool height = c.Height != b.Height, roof = c.Roof != b.Roof, use = c.Use != b.Use, facade = c.Facade != b.Facade,
             wall = c.WallColor != b.WallColor, roofColor = c.RoofColor != b.RoofColor, taper = c.TaperDeg != b.TaperDeg;
        if (!(height || roof || use || facade || wall || roofColor || taper)) { SetAimStatus("바꾼 칸이 없습니다", autoHide: true); return; }
        int floors = Map3DBuilding.FloorsFromHeight(c.Height, UbFloorHeight);
        var commands = new List<Services.Undo.IUndoableCommand>();
        foreach (var uid in UbSelected.ToList())
        {
            if (UserBuildingItems.FirstOrDefault(i => i.Uid == uid) is not { } item) continue;
            var before = StateOf(item);
            var after = before with
            {
                Height = height ? c.Height : before.Height, Floors = height ? floors : before.Floors,
                Roof = roof ? c.Roof : before.Roof, Use = use ? c.Use : before.Use, TaperDeg = taper ? c.TaperDeg : before.TaperDeg,
                Look = before.Look with { Facade = facade ? c.Facade : before.Look.Facade, WallColor = wall ? c.WallColor : before.Look.WallColor,
                                          RoofColor = roofColor ? c.RoofColor : before.Look.RoofColor },
            };
            if (after == before) continue;
            if (!await SaveUserBuildingAsync(area.Id, uid, after)) break;   // 이유는 판 오류 줄 — 그때까지 바꾼 것만 기록
            commands.Add(new UserBuildingCommand($"{Map3DUserBuilding.UndoPrefix} 고치기", area.Id, uid, before, after, ApplyUserBuildingStateAsync, ResolveUserBuildingJob));
        }
        if (commands.Count > 0)
        {
            _undoService?.Push(new Services.Undo.Commands.MacroCommand($"{Map3DUserBuilding.UndoPrefix} {commands.Count}개 함께 고치기", commands, _log));
            SetAimStatus($"건물 {commands.Count}개를 함께 고쳤습니다", autoHide: true);
        }
        _log?.Info($"[UserBuilding] 함께 고치기 {commands.Count}/{UbSelected.Count}");
        LoadUserBuildingGroupFields();
        RefreshUserBuildingPanel();
    }

    /// <summary>[합치기] 보임 — 2개 이상 골랐을 때만.</summary>
    public bool IsUserBuildingMergeVisible => UbSelected.Count >= 2;

    private RelayCommand? _mergeUserBuildingsCommand;
    /// <summary>[합치기] — 고른 2개 이상을 map-3d 가 하나로.</summary>
    public RelayCommand MergeUserBuildingsCommand => _mergeUserBuildingsCommand ??= new RelayCommand(
        _ => _ = MergeUserBuildingsAsync(), _ => IsUserBuildingDrawing && !_ubBusy && UbSelected.Count >= 2);

    private RelayCommand? _clearUserBuildingSelectionCommand;
    /// <summary>[선택 풀기](Esc 와 같음).</summary>
    public RelayCommand ClearUserBuildingSelectionCommand => _clearUserBuildingSelectionCommand ??= new RelayCommand(
        _ => ClearUserBuildingSelection(), _ => UbSelected.Count > 0);

    /// <summary>고름 풀기 — 풀었으면 true.</summary>
    private bool ClearUserBuildingSelection()
    {
        if (UbSelected.Count == 0) return false;
        UbSelected.Clear();
        LoadUserBuildingGroupFields();
        _ubAdorner?.InvalidateVisual();
        RefreshUserBuildingPanel();
        return true;
    }

    /// <summary>
    /// Ctrl+클릭 — 저장된 건물을 고름에 넣거나 뺀다. 저장된 건물을 고치는 중이었으면 그것부터 고름에 넣고 고치기를 닫는다(누른 뒤 Ctrl+클릭 = 둘 다).
    /// 새 윤곽을 그리던 중이면(점 · 형상 클릭이 있음) 고르지 않는다(false — 보통 클릭으로).
    /// </summary>
    private bool ToggleUserBuildingSelection(UbPoint screen)
    {
        if (_ubDraft is not { } d || MainMap == null) return false;
        bool drawingNew = _ubEditing == null && (d.Points.Count > 0 || d.Anchors.Count > 0);
        if (drawingNew) return false;
        var g = MainMap.FromLocalToLatLng((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
        var hit = UserBuildingItems.LastOrDefault(it => Map3DUserBuilding.Contains(it.Outline, new UbPoint(g.Lng, g.Lat)));
        var seed = _ubEditing?.Uid;
        var keep = UbSelected.ToList();
        if (_ubEditing != null) ResetUserBuildingDraft(null);   // 고치던 건물 → 고름으로(저장하지 않은 고침은 버린다)
        UbSelected.Clear();
        UbSelected.AddRange(keep);
        if (seed != null && !UbSelected.Contains(seed)) UbSelected.Add(seed);
        if (hit != null && hit.Uid != seed && !UbSelected.Remove(hit.Uid)) UbSelected.Add(hit.Uid);
        _log?.Info($"[UserBuilding] 고름 {UbSelected.Count}개");
        LoadUserBuildingGroupFields();
        _ubAdorner?.InvalidateVisual();
        RefreshUserBuildingPanel();
        return true;
    }

    // ── 들어가기 · 나가기 ──

    /// <summary>그 영역에서 그리기 시작 — 권한 · 만드는 중 · 영역 확인, 충돌 모드 끝내기, 표면 · 키 · 저장된 건물 불러오기. in3D = 3D 위 판(2D 지도 표면 없음).</summary>
    private void StartUserBuildingDraw(Map3DAreaItem item, bool in3D = false)
    {
        if (MainMap == null) return;
        if (!RequireMapEditMode(notify: true)) return;   // 꺼짐 = 보기 전용(툴바 · 영역 배지 · 3D 고르기 모두 여기로)
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (item.IsBaking) { SetAimStatus("이 영역은 다시 만드는 중입니다 — 다 만든 뒤 그리세요", autoHide: true); return; }
        if (IsUserBuildingDrawing) ExitUserBuildingDraw("switch");
        _groupSelection?.EndRotation(commit: true);   // 다른 조작 = 열린 묶음 회전 먼저 확정(뒤의 Esc 가 그리기 대신 회전을 되돌리지 않게)
        CancelConflictingModesForMeasure();
        if (MainMap.IsMeasuring) MainMap.StopMeasure();
        _ubArea = item;
        _ubIn3D = in3D;
        _ubItems = Array.Empty<Map3DUserBuildingItem>();
        _ubFloorHeightValue = Map3DBuilding.DefaultFloorHeight;
        ResetUserBuildingDraft(null);
        AttachUserBuildingSurface(adorner: !in3D);
        if (!in3D) MainMap.Focus();   // 그리기 키는 지도 화면에서 온 것만 받는다(IsUserBuildingKey) — 메뉴 · 다른 화면에 남은 초점을 지도로(선 그리기와 같은 관용구)
        SyncMap3DWithInputModes();   // 2D 그리기면 3D 를 접는다(3D 위 판은 3D 에 머문다)
        _log?.Info($"[UserBuilding] 그리기 시작 area={item.Id} 3D={in3D}");
        SetAimStatus(in3D ? "판의 도구를 눌러 3D 위에 그리거나, 3D 에서 건물을 눌러 고치세요"
                          : "지도를 눌러 건물 윤곽(위에서 본 모양)을 그리세요 — 첫 점을 누르거나 Enter 로 닫기", autoHide: true);
        _ = LoadUserBuildingsAsync(item.Id);
    }

    /// <summary>그리기 끝내기 — 저장하지 않은 윤곽은 버린다.</summary>
    private void ExitUserBuildingDraw(string cause)
    {
        if (_ubArea == null) return;
        _log?.Info($"[UserBuilding] 그리기 끝 area={_ubArea.Id} cause={cause}");
        RevertUserBuildingPreview();
        _ubNewUid = null;
        if (_ubEditing != null) PostUserBuilding3D(_ubArea.Id, Map3DUserBuilding.SelectMessageJson(null));
        if (_ubIn3D) EndEdit3D();   // 3D 그리기 · 손잡이 내림
        _ubArea = null;
        _ubIn3D = false;
        _ubDraft = null;
        _ubEditing = null;
        _ubPendingEditUid = null;
        _ubDragStart = null;
        _ubItems = Array.Empty<Map3DUserBuildingItem>();
        UbSelected.Clear();
        DetachUserBuildingSurface();
        RefreshUserBuildingPanel();
        SyncMap3DWithInputModes();
    }

    /// <summary>빈 윤곽(새 건물) 또는 저장된 건물 고치기로 판을 맞춘다.</summary>
    private void ResetUserBuildingDraft(Map3DUserBuildingItem? edit)
    {
        if (_ubArea is not { } area || MainMap == null) return;
        if (_ubDraft != null) _ubDraft.Changed -= OnUserBuildingDraftChanged;
        RevertUserBuildingPreview();
        _ubNewUid = null;
        var map = MainMap;
        _ubDraft = new Map3DUserBuildingDraft(
            geo => { var l = map.FromLatLngToLocal(new PointLatLng(geo.Y, geo.X)); return new UbPoint(l.X, l.Y); },
            s => { var g = map.FromLocalToLatLng((int)Math.Round(s.X), (int)Math.Round(s.Y)); return Map3DUserBuilding.Round(new UbPoint(g.Lng, g.Lat)); },
            geo => area.Area.Contains(geo.X, geo.Y));
        _ubDraft.CircleSides = UbCircleSides;
        if (edit == null) _ubDraft.SetTool(_ubTool);
        _ubDraft.Changed += OnUserBuildingDraftChanged;
        if (edit?.Uid != _ubEditing?.Uid) PostUserBuilding3D(area.Id, Map3DUserBuilding.SelectMessageJson(edit?.Uid));   // 3D 상자 고름 표시
        _ubEditing = edit;
        if (_ubIn3D) { if (edit != null) StartEdit3D(Map3DEdit.TargetUserBuilding, edit.Uid); else StopEdit3D(); }   // 3D 손잡이
        UbSelected.Clear();   // 새 윤곽 · 고치기 = 여러 개 고름은 끝
        _ubRoof = edit?.Roof ?? Map3DUserBuilding.RoofFlat;
        _ubUse = edit?.Use ?? Map3DUserBuilding.UseAuto;
        _ubWallColor = edit?.WallColor;
        _ubRoofColor = edit?.RoofColor;
        _ubFacade = edit?.Facade ?? Map3DLook.FacadeAuto;
        _ubTaper = edit?.TaperDeg ?? 0;
        _ubPreviewJson = edit != null ? Map3DUserBuilding.PreviewMessageJson(edit) : null;   // 3D 에 이미 보이는 저장된 모양
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
        if (_ubDraft is { IsClosed: true, CompletedBy: UbTool.Circle } d && !_ubRoofChosen && UserBuildingRoof == Map3DUserBuilding.RoofFlat
            && Map3DUserBuilding.CanRoundRoof(d.Points))   // 꼭짓점이 적은 원(16 미만)은 map-3d 가 둥근 지붕으로 짓지 않는다
            _ubRoof = Map3DUserBuilding.RoofDome;
        _ubAdorner?.InvalidateVisual();
        RefreshUserBuildingPanel();
    }

    /// <summary>지도 표면(adorner = false 면 판 키만 — 3D 위 판).</summary>
    private void AttachUserBuildingSurface(bool adorner = true)
    {
        if (MainMap == null) return;
        try
        {
            var layer = AdornerLayer.GetAdornerLayer(MainMap);
            if (!adorner) { }
            else if (layer != null) { _ubAdorner = new UserBuildingAdorner(MainMap, this, _log); layer.Add(_ubAdorner); }
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
            UbSelected.RemoveAll(uid => items.All(i => i.Uid != uid));   // 없어진 건물은 고름에서 뺀다
            _ubAdorner?.InvalidateVisual();
            if (_ubPendingEditUid is { } pending)
            {
                _ubPendingEditUid = null;
                if (items.FirstOrDefault(i => i.Uid == pending) is { } hit) { ResetUserBuildingDraft(hit); return; }   // 3D 에서 고른 건물 고치기
            }
            RefreshUserBuildingPanel();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[UserBuilding] 목록 — 서버 닿지 않음: {ex.Message}");
            SetUserBuildingError(Map3DUnavailableText);
            NoteMap3DEngineDown("user-building-list");
        }
        catch (Exception ex) { _log?.Warning($"[UserBuilding] 목록 실패: {ex.Message}"); }
    }

    // ── 지도 표면(IUserBuildingDrawHost) ──

    void IUserBuildingDrawHost.UserBuildingPointerDown(UbPoint screen, int clickCount, bool ctrl)
    {
        if (_ubDraft is not { } d || _ubBusy) return;
        if (ctrl && clickCount < 2 && ToggleUserBuildingSelection(screen)) return;
        if (UbSelected.Count > 0)
        {
            // 고름이 있을 때 보통 클릭 = 고름 풀기(빈 곳이면 그것만 — 점을 찍지 않는다, 저장된 건물 안이면 아래에서 그 건물 고치기)
            ClearUserBuildingSelection();
            if (MainMap is { } m)
            {
                var g0 = m.FromLocalToLatLng((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
                if (!UserBuildingItems.Any(it => Map3DUserBuilding.Contains(it.Outline, new UbPoint(g0.Lng, g0.Lat)))) return;
            }
        }
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
        if (!IsUserBuildingKey(e)) return;   // 지도 화면 밖(그 위에 뜬 확인 창 등)의 키는 그쪽 몫 — Enter 로 저장되지 않게
        bool inBox = e.OriginalSource is TextBoxBase;
        e.Handled = HandleUserBuildingKey(e.Key, Keyboard.Modifiers, inBox);
    }

    /// <summary>
    /// 그리는 중이고 키가 지도 화면(지도를 품은 MapView — 지도 · 툴바 · 판, 또는 창 자체처럼 그 위 조상)에서 왔는가 — 「건물 그리기」 몫.
    /// 지도 키 처리기(Delete = 선택 심볼 삭제 · Ctrl+Z · 방향키 이동 · Esc 선택 해제)는 이 키를 건드리지 않는다(이미지 자르기 IsImageCropPanelKey 관용구).
    /// 지도 화면 밖(팝업층의 확인 창 · 다른 화면)에서 온 키는 그쪽 몫이다.
    /// </summary>
    private bool IsUserBuildingKey(KeyEventArgs e) => IsUserBuildingDrawing && (_ubIn3D ? IsPanelKey(e, _ubPanel) : IsMapAreaKey(e, _ubPanel));

    /// <summary>키가 지도 화면(지도를 품은 MapView · 그 위 조상) 또는 판(panel)에서 왔는가 — 「건물 그리기」 · 「꾸미기」 공용.</summary>
    private bool IsMapAreaKey(KeyEventArgs e, System.Windows.Controls.Control? panel)
    {
        if (MainMap is not { } map) return false;
        if ((e.OriginalSource ?? Keyboard.FocusedElement) is not System.Windows.DependencyObject d) return true;
        if (panel != null && IsInImageCropPanel(panel, d)) return true;   // 판(같은 Visual 안 판정)
        System.Windows.DependencyObject root = map;   // 지도를 품은 가장 가까운 UserControl(MapView), 없으면 맨 위 조상
        for (var p = System.Windows.Media.VisualTreeHelper.GetParent(map); p != null; p = System.Windows.Media.VisualTreeHelper.GetParent(p))
        {
            root = p;
            if (p is System.Windows.Controls.UserControl) break;
        }
        return d is System.Windows.Media.Visual v && root is System.Windows.Media.Visual r && (ReferenceEquals(v, r) || r.IsAncestorOf(v) || v.IsAncestorOf(r));
    }

    /// <summary>그리기 키 — Esc 두 단계 · Enter 닫기 · Backspace · Delete · Ctrl+Z. 입력칸 안에서는 Esc 만. 처리했으면 true.</summary>
    internal bool HandleUserBuildingKey(Key key, ModifierKeys modifiers, bool inTextBox)
    {
        if (_ubDraft is not { } d) return false;
        if (key == Key.Escape)
        {
            if (_ubPanel?.CancelHandleDrag() == true) return true;
            if (_ubIn3D && CancelDraw3D()) { RefreshUserBuildingPanel(); return true; }   // 3D 그리기 → 끄기
            if (_ubIn3D && ForwardEscapeToEdit3D()) return true;   // 3D 손잡이 → 뷰어가 원위치 또는 편집 끝(edit.end)
            if (d.PointerCancel()) return true;   // 꼭짓점 끄는 중 → 끌기 전으로
            if (ClearUserBuildingSelection()) return true;   // 여러 개 고름 → 고름 풀기(그리기는 그대로)
            if (d.IsClosed && (d.CanUndo || _ubEditing != null)) { ResetUserBuildingDraft(null); return true; }   // 닫은(고치던) 윤곽 → 빈 윤곽
            if (d.Escape() == UbEscapeResult.Exit) ExitUserBuildingDraw("escape");
            return true;
        }
        if (inTextBox) return false;
        if (modifiers == ModifierKeys.Control && key == Key.Z) { _ = UndoUserBuildingAsync(); return true; }
        if (modifiers == ModifierKeys.Control && key == Key.Y) { _ = RedoUserBuildingAsync(); return true; }
        if (modifiers != ModifierKeys.None) return false;
        if (_ubIn3D && key is Key.Back or Key.Delete) return false;   // 3D 위 판 — 꼭짓점은 3D 손잡이 · 판 단추로
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
            _ubTaperText = _ubTaper.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
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
                     nameof(UserBuildingRoofStatus), nameof(HasUserBuildingRoofStatus), nameof(IsUserBuildingToolTriangle),
                     nameof(UserBuildingSelectedUids), nameof(HasUserBuildingSelection), nameof(UserBuildingSelectionText), nameof(IsUserBuildingMergeVisible),
                     nameof(IsUserBuildingGroup), nameof(UserBuildingGroupText),
                     nameof(UserBuildingColors), nameof(UserBuildingFacade), nameof(UserBuildingWallColor), nameof(UserBuildingRoofColor),
                     nameof(UserBuildingTaperDeg), nameof(UserBuildingTaperText), nameof(UserBuildingTaperStatus),
                 })
            NotifyOfPropertyChange(name);
        ApplyUserBuildingCommand.RaiseCanExecuteChanged();
        DeleteUserBuildingCommand.RaiseCanExecuteChanged();
        UndoUserBuildingCommand.RaiseCanExecuteChanged();
        RedoUserBuildingCommand.RaiseCanExecuteChanged();
        RefreshEdit3D();
        MergeUserBuildingsCommand.RaiseCanExecuteChanged();
        ClearUserBuildingSelectionCommand.RaiseCanExecuteChanged();
        ResetUserBuildingTaperCommand.RaiseCanExecuteChanged();
        PreviewUserBuildingDraft();
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
        if (IsUserBuildingGroup) { await ApplyUserBuildingGroupAsync(area); return; }
        CommitPendingEdit3D();   // 3D 손잡이로 고친 것도 [✓ 적용] 한 번에(옛 [✓ 확정] 을 합침)
        if (d.Validate() is { } why) { SetUserBuildingError(why); return; }
        string uid = UserBuildingDraftUid;
        var before = _ubEditing is { } e ? StateOf(e) : null;
        int floors = Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight);
        var after = new UserBuildingState(d.Points.ToList(), _ubHeight, floors, string.IsNullOrWhiteSpace(_ubName) ? null : _ubName)
                    { Roof = UserBuildingRoof, Use = UserBuildingUse, Look = UserBuildingLook, TaperDeg = _ubTaper };
        if (!await SaveUserBuildingAsync(area.Id, uid, after))
        {
            // 서버가 기울기를 거절했다(너무 좁아져 윤곽이 뒤집힘 · 400 문구) — 문구는 판에 두고 기울기는 저장된 값으로 되돌린다(3D 상자도)
            double savedTaper = before?.TaperDeg ?? 0;
            if (_ubLastSaveRejected && _ubArea?.Id == area.Id && after.TaperDeg != savedTaper)
            {
                var rejected = _ubError;
                _ubTaper = savedTaper;
                RefreshUserBuildingPanel();
                SetUserBuildingError(rejected);
            }
        }
        else
        {
            _undoService?.Push(new UserBuildingCommand($"{Map3DUserBuilding.UndoPrefix} {(before == null ? "넣기" : "고치기")}", area.Id, uid, before, after,
                                                       ApplyUserBuildingStateAsync, ResolveUserBuildingJob));
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
                                                       StateOf(e), null, ApplyUserBuildingStateAsync, ResolveUserBuildingJob));
            SetAimStatus("건물을 지웠습니다 — 3D 를 다시 만들면 원래 건물로 돌아갑니다", autoHide: true);
            ResetUserBuildingDraft(null);
        }
    }

    private static UserBuildingState StateOf(Map3DUserBuildingItem e)
        => new(e.Outline, e.Height, e.Floors, e.Name) { Roof = e.Roof, Use = e.Use, Look = Map3DUserBuilding.LookOf(e), TaperDeg = e.TaperDeg };

    /// <summary>[합치기] — 고른 건물들(고른 순서)을 map-3d 가 하나로 → 목록 다시 받기 · 합친 건물 고치기로 열기 · 상태 글 · 편집 기록 1단위.</summary>
    private async Task MergeUserBuildingsAsync()
    {
        if (_ubArea is not { } area || _ubBusy || UbSelected.Count < 2) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        var members = UbSelected.Select(uid => UserBuildingItems.FirstOrDefault(i => i.Uid == uid)).OfType<Map3DUserBuildingItem>()
                                .Select(i => (i.Uid, StateOf(i))).ToList();
        if (members.Count < 2) return;
        if (await SendUserBuildingMergeAsync(area.Id, members.Select(m => m.Uid).ToList(), null) is not { } r) return;
        var first = r.Item;
        _undoService?.Push(new UserBuildingMergeCommand($"{Map3DUserBuilding.UndoPrefix} 합치기", area.Id, members, first.Uid,
            (job, uids, ct) => RedoUserBuildingMergeAsync(job, uids, first),
            ApplyUserBuildingUnmergeAsync, ResolveUserBuildingJob));
        if (_ubArea?.Id == area.Id)
            ResetUserBuildingDraft(UserBuildingItems.FirstOrDefault(i => i.Uid == first.Uid) ?? first);   // 높이 · 층수를 바로 고칠 수 있게
        SetAimStatus(Map3DUserBuilding.MergedText(members.Count, r.HolesFilled), autoHide: true);
    }

    /// <summary>편집 기록 다시 하기 — 같은 uid 들을 처음 합친 건물의 높이 · 층수 · 이름 · 외벽으로 다시 합친다. 실패는 예외. 합친 uid 를 돌려준다.</summary>
    private async Task<string> RedoUserBuildingMergeAsync(string job, IReadOnlyList<string> uids, Map3DUserBuildingItem like)
    {
        if (await SendUserBuildingMergeAsync(job, uids, like) is not { } r) throw new InvalidOperationException(_ubError ?? "직접 그린 건물 합치기 실패");
        if (_ubEditing != null && uids.Contains(_ubEditing.Uid)) ResetUserBuildingDraft(null);
        SetAimStatus(Map3DUserBuilding.MergedText(uids.Count, r.HolesFilled), autoHide: true);
        return r.Item.Uid;
    }

    /// <summary>편집 기록 되돌리기 — 합친 건물을 지우고 원래 건물들을 같은 uid · 상태로 다시 저장. 실패는 예외(편집 기록이 그 칸을 남긴다).</summary>
    private async Task ApplyUserBuildingUnmergeAsync(string job, string mergedUid, IReadOnlyList<(string Uid, UserBuildingState State)> members, CancellationToken ct)
    {
        if (_ubEditing?.Uid == mergedUid) ResetUserBuildingDraft(null);
        if (!await SaveUserBuildingAsync(job, mergedUid, null)) throw new InvalidOperationException(_ubError ?? "합친 건물 지우기 실패");
        foreach (var (uid, state) in members)
            if (!await SaveUserBuildingAsync(job, uid, state)) throw new InvalidOperationException(_ubError ?? "직접 그린 건물 저장 실패");
        SetAimStatus($"합친 건물을 원래 {members.Count}개로 되돌렸습니다", autoHide: true);
    }

    /// <summary>
    /// map-3d 합치기 → 3D 상자 미리보기(원래 것 지움 · 합친 것) · 목록 갱신. 실패는 판 오류 줄(떨어진 도형 400 · 없는 건물 404 = 서버 문구 그대로). 성공이면 응답.
    /// </summary>
    private async Task<Map3DUserBuildingMergeResult?> SendUserBuildingMergeAsync(string job, IReadOnlyList<string> uids, Map3DUserBuildingItem? like)
    {
        if (_map3DClient == null) return null;
        _ubBusy = true;
        _ubError = null;
        RefreshUserBuildingPanel();
        try
        {
            var r = like == null
                ? await _map3DClient.MergeUserBuildingsAsync(job, uids)
                : await _map3DClient.MergeUserBuildingsAsync(job, uids, like.Height, like.Floors, like.Name, like.Use, like.Uid,
                                                             look: Map3DUserBuilding.LookOf(like));   // 다시 하기 — 처음 합친 uid · 외관 그대로
            foreach (var uid in r.Merged.Count > 0 ? r.Merged : uids) PostUserBuilding3D(job, Map3DUserBuilding.PreviewMessageJson(uid, null, null));
            PostUserBuilding3D(job, Map3DUserBuilding.PreviewMessageJson(r.Item));
            _log?.Info($"[UserBuilding] 합침 {string.Join(",", uids)} → {r.Item.Uid} 빈곳={r.HolesFilled} job={job}");
            if (_ubArea?.Id == job) await LoadUserBuildingsAsync(job);
            _ = RefreshMap3DAreasAsync();
            return r;
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[UserBuilding] 합치기 — 서버 닿지 않음: {ex.Message}");
            SetUserBuildingError(Map3DUnavailableText);
            NoteMap3DEngineDown("user-building-merge");
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")   // 옛 map-3d — 합치기 주소가 없다
        {
            _log?.Warning($"[UserBuilding] 합치기 — 옛 서버({(int)ex.StatusCode} {ex.Message})");
            SetUserBuildingError(Map3DUserBuilding.OldServerText);
        }
        catch (Map3DRequestException ex)
        {
            _log?.Warning($"[UserBuilding] 합치기 거절({(int)ex.StatusCode}): {ex.Message}");
            SetUserBuildingError(ex.Message);   // 떨어진 도형 · 없는 건물 — 서버의 한국어 문구
            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound && _ubArea?.Id == job) await LoadUserBuildingsAsync(job);   // 다른 곳에서 지워졌다
        }
        catch (Exception ex)
        {
            _log?.Error($"[UserBuilding] 합치기 실패: {ex.Message}");
            SetUserBuildingError("합치지 못했습니다.");
        }
        finally
        {
            _ubBusy = false;
            RefreshUserBuildingPanel();
        }
        return null;
    }

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

    /// <summary>다시 하기 — GIS 편집 기록 맨 위(다시 하기 쪽)가 이 기능 기록일 때만.</summary>
    private async Task RedoUserBuildingAsync()
    {
        if (_ubBusy || _undoService == null) return;
        if (_undoService.NextRedoDescription?.StartsWith(Map3DUserBuilding.UndoPrefix, StringComparison.Ordinal) != true)
        {
            SetAimStatus("다시 할 것이 없습니다", autoHide: true);
            return;
        }
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try { await _undoService.RedoAsync(); }
        catch (Exception ex) { _log?.Error($"[UserBuilding] 다시 하기 실패: {ex.Message}"); }
    }

    /// <summary>
    /// 편집 기록이 잡은 job → 그 건물이 든 영역의 지금 job. 옛 job 이 아직 목록에 있으면 그대로, 「다시 만들기」 · 다른 곳에서 바뀌어 없어졌으면
    /// 건물 가운데를 덮는 지금 영역(가장 새것 — map-3d 도 장면 범위에 가운데가 든 건물을 그 장면 것으로 본다). 찾지 못하면 옛 job(서버가 판단).
    /// </summary>
    private string ResolveUserBuildingJob(string job, UbPoint center)
    {
        if (Map3DAreas.Any(a => a.Id == job)) return job;
        var now = Map3DAreas.Where(a => a.Area.Contains(center.X, center.Y)).OrderByDescending(a => a.Scene.Created ?? DateTime.MinValue).FirstOrDefault();
        if (now == null) return job;
        _log?.Info($"[UserBuilding] 편집 기록 영역 job {job} → {now.Id}");
        return now.Id;
    }

    /// <summary>GIS 편집 기록 적용(UserBuildingCommand) — 그 상태로 다시 저장(null = 지움). 실패는 예외(편집 기록이 그 칸을 남긴다).</summary>
    private async Task ApplyUserBuildingStateAsync(string job, string uid, UserBuildingState? state, CancellationToken ct)
    {
        if (!await SaveUserBuildingAsync(job, uid, state)) throw new InvalidOperationException(_ubError ?? "직접 그린 건물 저장 실패");
        if (_ubEditing?.Uid == uid)
        {
            // 3D 위 판 — 되돌린 모양으로 그 건물을 계속 고친다(손잡이도 다시), 2D · 지운 건물은 빈 윤곽으로
            if (_ubIn3D && state != null && UserBuildingItems.FirstOrDefault(i => i.Uid == uid) is { } now) ResetUserBuildingDraft(now);
            else ResetUserBuildingDraft(null);
        }
        SetAimStatus(state == null ? "직접 그린 건물을 지웠습니다(되돌리기)" : "직접 그린 건물을 되돌렸습니다", autoHide: true);
    }

    /// <summary>map-3d 에 저장(state) 또는 지움(null) → 3D 상자 미리보기 · 목록 갱신. 실패는 판 오류 줄. 성공이면 true.</summary>
    private async Task<bool> SaveUserBuildingAsync(string job, string uid, UserBuildingState? state)
    {
        if (_map3DClient == null) return false;
        _ubBusy = true;
        _ubError = null;
        _ubLastSaveRejected = false;
        RefreshUserBuildingPanel();
        try
        {
            if (state != null)
            {
                var saved = await _map3DClient.PutUserBuildingAsync(job, uid, state.Outline, state.Height, state.Floors, state.Name, state.Roof, state.Use, state.Look,
                                                                    state.TaperDeg);
                PostUserBuilding3D(job, Map3DUserBuilding.PreviewMessageJson(saved with { Uid = uid }));   // 층 띠 · 창 칸 · 색을 입힌 상자
                if (_ubArea?.Id == job && uid == UserBuildingDraftUid)
                {
                    // 3D 상자 = 저장된 모양 — 판 값 그대로 다시 보내지 않고, 끝낼 때 걷지도 않는다
                    _ubPreviewDirty = false;
                    _ubPreviewJson = _ubDraft is { IsClosed: true } d ? Map3DUserBuilding.PreviewMessageJson(UserBuildingDraftItem(d)) : null;
                }
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
            NoteMap3DEngineDown("user-building-save");
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")   // 옛 map-3d — 그 주소가 없다(FastAPI 기본 문구)
        {
            _log?.Warning($"[UserBuilding] 저장 — 옛 서버({(int)ex.StatusCode} {ex.Message})");
            SetUserBuildingError(Map3DUserBuilding.OldServerText);
        }
        catch (Map3DRequestException ex)
        {
            _ubLastSaveRejected = ex.StatusCode == System.Net.HttpStatusCode.BadRequest;
            SetUserBuildingError($"저장하지 못했습니다: {ex.Message}");
        }
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
        if (!RequireMapEditMode(notify: true)) return;   // 영역 배지 메뉴도 고치기 입구 — 편집 모드에서만
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
            NoteMap3DEngineDown("user-building-clear");
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
