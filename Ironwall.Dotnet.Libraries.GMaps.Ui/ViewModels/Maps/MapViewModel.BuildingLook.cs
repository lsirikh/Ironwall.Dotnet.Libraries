using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 「건물 높이」 판의 외관 칸 · 숨기기(2026-10-21 사용자 요청 — 오버레이 이미지로 이상하게 생긴 건물을 지울 수 없다 · 더 꾸미고 싶다).
                  외벽 무늬 · 외벽 색 · 지붕 색(빈 값 = 자동) → [외관 적용] = PUT …/buildings/{bid}/look, [이 건물 숨기기](확인 한 번) = hidden=true,
                  「숨긴 건물 N개」 목록 + [되살리기] · [모두 되살리기](확인 — DELETE …/looks).
   Note         : ★ map-3d 가 건물 자리(바닥 윤곽)로 영역 단위 저장해 다시 만들어도 다시 붙는다(twin-contract A). 저장 뒤 3D 에 building.look 을 보내
                    다시 불러오지 않고 바로 보이게 한다. ★ 판 안 되돌리기 칸(BhUndo)에 외관 칸도 쌓는다(되돌리기 = 숨긴 것 되살리기 · 앞 색으로).
                  ★ 직접 그린 건물(origin user)은 숨기지 않고 [이 건물 지우기] = user-buildings DELETE(uid 를 아는 경우).
                  ★ 엔진이 옛 판(GET …/looks 404)이면 외관 칸 · 숨기기를 끄고 이유 한 줄.
                  ★ 외벽 무늬 목록(2026-10-22, facade-package-contract A) = 엔진 GET /api/facades(영역 목록을 받을 때 한 번 — 옛 엔진 · 실패면 내장 22개),
                    「건물 높이」 · 「건물 만들기」 판 콤보에 분류 머리글(공장 · 발전소 · 군사 · 일반)로 묶어 보인다.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private Map3DLookState? _bhLookEdit;   // 판에서 고친 외관(null = 저장된 값 그대로)
    private string? _bhLookNotice;          // "다시 만들면 보입니다" 류 안내(판 한 줄)
    /// <summary>외관 저장 뒤 판 안내 — 다시 만들어야 보이는 것(되살린 건물 · 외벽 무늬).</summary>
    public string? BuildingLookNotice => IsBuildingHeightPanelOpen ? _bhLookNotice : null;
    public bool HasBuildingLookNotice => BuildingLookNotice != null;
    private IReadOnlyList<Map3DLookItem>? _bhLooks;
    private bool _bhLooksUnsupported;
    private (string Job, Map3DBuildingPick Building)? _pendingBuildingHide;
    private string? _pendingLooksReset;

    public System.ComponentModel.ICollectionView BuildingFacades => _bhFacadeView ??= NewFacadeView();
    public IReadOnlyList<KeyValuePair<string, string>> BuildingColors => Map3DLook.ColorsWith(_bhBuilding?.Look.WallColor, _bhBuilding?.Look.RoofColor);
    private Map3DLookState BhLook => _bhLookEdit ?? _bhBuilding?.Look ?? Map3DLookState.Auto;

    /// <summary>외벽 무늬(Map3DLook.FacadeCatalog · 엔진 목록의 키).</summary>
    public string BuildingFacade
    {
        get => BhLook.Facade;
        set => SetBuildingLookEdit(BhLook with { Facade = Map3DLook.NormalizeFacade(value) }, value);
    }

    /// <summary>외벽 색("#rrggbb", "" = 자동).</summary>
    public string BuildingWallColor
    {
        get => BhLook.WallColor ?? "";
        set => SetBuildingLookEdit(BhLook with { WallColor = Map3DLook.NormalizeColor(value) }, value);
    }

    /// <summary>지붕 색("#rrggbb", "" = 자동).</summary>
    public string BuildingRoofColor
    {
        get => BhLook.RoofColor ?? "";
        set => SetBuildingLookEdit(BhLook with { RoofColor = Map3DLook.NormalizeColor(value) }, value);
    }

    /// <summary>판의 외관이 저장된 값과 다른가([외관 적용]).</summary>
    public bool IsBuildingLookChanged => _bhBuilding is { } b && _bhLookEdit is { } e && e with { Hidden = b.Look.Hidden } != b.Look;
    /// <summary>외관 칸 · 숨기기를 쓸 수 있는 엔진인가.</summary>
    public bool IsBuildingLookAvailable => IsBuildingHeightPanelOpen && !_bhLooksUnsupported;
    public bool IsBuildingLookUnsupported => IsBuildingHeightPanelOpen && _bhLooksUnsupported;
    public string BuildingLookUnsupportedText => Map3DLook.OldEngineText;
    /// <summary>고른 건물이 숨겨져 있는가(숨긴 뒤 · 되돌리기 전).</summary>
    public bool IsBuildingHidden => _bhBuilding?.Look.Hidden == true;
    private bool IsBuildingUserDrawn => _bhBuilding?.Origin == "user";
    /// <summary>
    /// 사용자가 찾는 말로 [이 건물 지우기](2026-10-22 사용자 "삭제가 안 되는데?") — 지도 건물은 실제로는 숨김(되살리기 가능, "?" 말풍선),
    /// 직접 그린 건물은 user-buildings DELETE.
    /// </summary>
    public string BuildingHideButtonText => "이 건물 지우기";

    public IReadOnlyList<Map3DLookItem> BuildingHiddenItems => (_bhLooks ?? Array.Empty<Map3DLookItem>()).Where(l => l.State.Hidden).ToList();
    public string BuildingHiddenCountText => Map3DLook.HiddenCountText(BuildingHiddenItems.Count);
    public bool HasBuildingHidden => BuildingHiddenItems.Count > 0;

    private RelayCommand? _applyBuildingLookCommand;
    /// <summary>[외관 적용] — 외벽 무늬 · 외벽 색 · 지붕 색 저장.</summary>
    public RelayCommand ApplyBuildingLookCommand => _applyBuildingLookCommand ??= new RelayCommand(
        _ => _ = ApplyBuildingLookAsync(), _ => IsBuildingHeightEditable && IsBuildingLookAvailable && IsBuildingLookChanged);

    private RelayCommand? _hideBuildingCommand;
    /// <summary>[이 건물 숨기기] · [이 건물 지우기](직접 그린 건물) — 확인 한 번.</summary>
    public RelayCommand HideBuildingCommand => _hideBuildingCommand ??= new RelayCommand(
        _ => _ = RequestHideBuildingAsync(),
        _ => IsBuildingHeightEditable && !IsBuildingHidden && (IsBuildingUserDrawn ? _bhBuilding?.UserUid != null : IsBuildingLookAvailable));

    private RelayCommand? _restoreHiddenBuildingCommand;
    /// <summary>목록의 [되살리기] — 매개변수 = Map3DLookItem(지금 장면에 붙은 것만).</summary>
    public RelayCommand RestoreHiddenBuildingCommand => _restoreHiddenBuildingCommand ??= new RelayCommand(
        p => { if (p is Map3DLookItem { Id: int id } item && _bhJob is { } job) _ = SaveBuildingLookAsync(job, id, false, null, item.State, "건물을 되살렸습니다"); },
        p => IsBuildingHeightEditable && p is Map3DLookItem { Id: not null });

    private RelayCommand? _restoreAllHiddenBuildingsCommand;
    /// <summary>[모두 되살리기] — 그 영역의 덮어쓰기를 모두 지움(확인 뒤).</summary>
    public RelayCommand RestoreAllHiddenBuildingsCommand => _restoreAllHiddenBuildingsCommand ??= new RelayCommand(
        _ => _ = RequestRestoreAllLooksAsync(), _ => IsBuildingHeightEditable && IsBuildingLookAvailable && _bhLooks is { Count: > 0 });

    // ── 외벽 무늬 목록(GET /api/facades) ──

    private IReadOnlyList<Map3DFacade>? _map3DFacadeList;   // 엔진에서 받은 목록(옛 엔진이면 내장) — null = 아직 못 받음(다음 영역 목록 때 다시)
    private bool _map3DFacadeLoading;
    private System.ComponentModel.ICollectionView? _bhFacadeView, _ubFacadeView;

    /// <summary>
    /// 판 콤보의 외벽 무늬 항목(값, 쉬운 말) — 분류 머리글로 묶는다. 판마다 따로 만든다: 한 보기를 두 콤보가 같이 쓰면
    /// 보기의 "지금 항목"을 서로 끌어당긴다(콤보는 IsSynchronizedWithCurrentItem=False 로도 막는다).
    /// </summary>
    private System.ComponentModel.ICollectionView NewFacadeView()
    {
        var list = _map3DFacadeList ?? Map3DLook.FacadeCatalog;
        var view = new System.Windows.Data.ListCollectionView(Map3DLook.Choices(list).ToList());
        view.GroupDescriptions.Add(new FacadeGroupDescription(list.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => Map3DLook.GroupText(g.First().Group))));
        return view;
    }

    /// <summary>항목(값, 쉬운 말) → 분류 머리글(자동은 "" — 머리글 없이 맨 위).</summary>
    private sealed class FacadeGroupDescription : System.ComponentModel.GroupDescription
    {
        private readonly IReadOnlyDictionary<string, string> _groups;
        public FacadeGroupDescription(IReadOnlyDictionary<string, string> groups) => _groups = groups;
        public override object GroupNameFromItem(object item, int level, System.Globalization.CultureInfo culture)
            => item is KeyValuePair<string, string> p && _groups.TryGetValue(p.Key, out var g) ? g : "";
    }

    /// <summary>
    /// 엔진의 외벽 무늬 목록을 한 번 받는다(영역 목록을 받은 뒤). 옛 엔진(404) · 형식 틀림 = 내장 목록으로 확정, 닿지 않음 = 다음에 다시.
    /// 받으면 두 판 콤보 항목을 새로 만들고 지금 값을 다시 알린다(항목을 갈 때 콤보가 비운 값은 세터가 무시한다).
    /// </summary>
    private async Task LoadMap3DFacadesAsync()
    {
        if (_map3DClient == null || _map3DFacadeList != null || _map3DFacadeLoading) return;
        _map3DFacadeLoading = true;
        try
        {
            var list = await _map3DClient.ListFacadesAsync().ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                if (list != null) Map3DLook.UseEngineFacades(list);
                _map3DFacadeList = list ?? Map3DLook.FacadeCatalog;
                _bhFacadeView = _ubFacadeView = null;
                foreach (var name in new[] { nameof(BuildingFacades), nameof(UserBuildingFacades), nameof(BuildingFacade), nameof(UserBuildingFacade) })
                    NotifyOfPropertyChange(name);
                _log?.Info($"[Map3D] 외벽 무늬 목록 {(list == null ? "내장(엔진이 모름)" : "엔진")} {_map3DFacadeList.Count}개");
            }).ConfigureAwait(false);
        }
        catch (Exception ex) { _log?.Warning($"[Map3D] 외벽 무늬 목록 실패(내장 목록 그대로): {ex.Message}"); }
        finally { _map3DFacadeLoading = false; }
    }

    private void SetBuildingLookEdit(Map3DLookState look, string? raw)
    {
        if (raw == null) return;   // 콤보가 목록에 없는 값(엔진이 넣은 색)을 비우며 되쓴 null — 저장된 값을 지우지 않는다("" = 자동)
        if (!IsBuildingHeightEditable) { RefreshBuildingHeightPanel(); return; }
        _bhLookEdit = look;
        RefreshBuildingHeightPanel();
    }

    /// <summary>
    /// 저장된 외관(GET …/looks 의 그 건물 줄)을 고른 건물에 — 뷰어의 building 메시지에는 외관 키가 없다(buildingedit.js buildingMessage).
    /// 이게 없으면 외관을 저장한 건물을 다시 고를 때 판이 「자동」으로 보이고, 지붕 색만 바꿔 [외관 적용]하면 저장한 외벽 무늬 · 색까지 자동으로 지워졌다(2026-10-22).
    /// </summary>
    private Map3DBuildingPick WithSavedLook(Map3DBuildingPick b)
        => _bhLooks?.FirstOrDefault(l => l.Id == b.Id) is { } saved ? b with { Look = saved.State } : b;

    /// <summary>지금 장면의 덮어쓰기 목록 — 옛 엔진(404)이면 외관 칸을 끈다. 닿지 않음 · 그 밖의 실패는 기록만(목록을 비운 채로).</summary>
    private async Task LoadBuildingLooksAsync(string job)
    {
        if (_map3DClient == null) return;
        try
        {
            var list = await _map3DClient.ListLooksAsync(job);
            if (_bhJob != job) return;
            _bhLooksUnsupported = list == null;
            _bhLooks = list;
            if (_bhBuilding is { } b && _bhLookEdit == null) _bhBuilding = WithSavedLook(b);
            RefreshBuildingHeightPanel();
        }
        catch (Exception ex) { _log?.Warning($"[Map3D] 건물 외관 목록 실패 job={job}: {ex.Message}"); }
    }

    private async Task ApplyBuildingLookAsync()
    {
        if (_bhBuilding is not { } b || _bhJob is not { } job || _bhBusy || !IsBuildingLookChanged) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        await SaveBuildingLookAsync(job, b.Id, null, BhLook, b.Look, "건물 외관을 저장했습니다");
    }

    /// <summary>
    /// 모양 저장(hidden null = 그대로, look null = 색 · 무늬 그대로) → 3D 에 building.look · 판의 저장된 값 · 목록 갱신.
    /// undoBefore 가 있으면 판 안 되돌리기 칸에 쌓는다. 실패는 판 오류 줄(직접 그린 건물 409 = 서버 문구, 옛 엔진 = 외관 칸 끔). 성공이면 true.
    /// </summary>
    private async Task<bool> SaveBuildingLookAsync(string job, int id, bool? hidden, Map3DLookState? look, Map3DLookState? undoBefore, string done)
    {
        if (_map3DClient == null) return false;
        _bhError = null;
        _bhInputError = false;
        int session = _bhSession;
        SetBuildingHeightBusy(true);
        try
        {
            var prev = _bhBuilding?.Id == id && _bhJob == job ? _bhBuilding.Look : undoBefore;
            var saved = await _map3DClient.PutBuildingLookAsync(job, id, Map3DLook.PutBody(hidden, look));
            if (undoBefore != null && session == _bhSession) BhUndo.Add(new BuildingHeightUndoStep(job, id, null, null) { Look = undoBefore });
            // 바로 반영은 숨김 · 색만 — 장면에 없는 건물을 되살렸으면(needs_rebake) 3D 에 보이게 하려 하지 않고, 외벽 무늬는 다시 만들 때 바뀐다
            if (!saved.NeedsRebake) PostUserBuilding3D(job, Map3DLook.LookMessageJson(id, saved.State));   // 그 장면을 띄워 둔 3D 에만
            bool facadeChanged = look != null && prev != null && prev.Facade != saved.State.Facade;
            _bhLookNotice = saved.NeedsRebake ? Map3DLook.NeedsRebakeText : facadeChanged && !saved.FacadeLive ? Map3DLook.FacadeRebakeText : null;
            if (_bhLookNotice != null) done = _bhLookNotice;
            if (_bhBuilding?.Id == id && _bhJob == job)
            {
                if (saved.State.Hidden && !_bhBuilding.Look.Hidden) PostBuilding3D(Map3DBuilding.SelectMessageJson(null));   // 사라진 건물의 강조 윤곽도 지운다
                _bhBuilding = _bhBuilding with { Look = saved.State };
                _bhLookEdit = null;
            }
            _log?.Info($"[Map3D] 건물 외관 저장 id={id} 숨김={saved.State.Hidden} 벽={saved.State.WallColor ?? "-"} 지붕={saved.State.RoofColor ?? "-"} 무늬={saved.State.Facade} job={job}");
            SetAimStatus(done, autoHide: true);
            await LoadBuildingLooksAsync(job);
            return true;
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 건물 외관 저장 — 서버 닿지 않음: {ex.Message}");
            SetBuildingHeightError(Map3DUnavailableText);
            NoteMap3DEngineDown("building-look");
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")   // 옛 map-3d — 그 주소가 없다(FastAPI 기본 문구)
        {
            _bhLooksUnsupported = true;
            SetBuildingHeightError(Map3DLook.OldEngineText);
        }
        catch (Map3DRequestException ex) { SetBuildingHeightError(ex.Message); }   // 직접 그린 건물 409 · 범위 밖 422 — 서버의 한국어 문구
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 건물 외관 저장 실패: {ex.Message}");
            SetBuildingHeightError("저장하지 못했습니다.");
        }
        finally { SetBuildingHeightBusy(false); }
        return false;
    }

    // ── 숨기기 · 지우기(확인 한 번) ──

    private async Task RequestHideBuildingAsync()
    {
        if (_bhBuilding is not { } b || _bhJob is not { } job || _bhBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingBuildingHide = (job, b);
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = IsBuildingUserDrawn ? "직접 그린 건물 지우기" : "건물 지우기",
                Explain = IsBuildingUserDrawn
                    ? "직접 그린 이 건물을 지우시겠습니까?\n다시 만들면 그 자리는 자동으로 찾은 건물로 돌아갑니다."
                    : "이 건물을 3D 에서 지우시겠습니까?\n다시 만들어도 지운 채로 남고, 「숨긴 건물」 목록의 [되살리기] 로 되돌릴 수 있습니다.",
                MessageModel = new CallHideMap3DBuildingProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 건물 숨기기 확인 창 실패: {ex.Message}"); }
    }

    /// <summary>[이 건물 숨기기] · [이 건물 지우기] 확인 콜백.</summary>
    public async Task HandleAsync(CallHideMap3DBuildingProcessMessageModel message, CancellationToken cancellationToken)
    {
        var pending = _pendingBuildingHide;
        _pendingBuildingHide = null;
        try
        {
            if (pending is not { } p || _bhJob != p.Job || _bhBuilding?.Id != p.Building.Id) return;   // 그 사이 판을 닫았거나 다른 건물
            if (p.Building.Origin == "user" && p.Building.UserUid is { } uid)
            {
                if (await SaveUserBuildingAsync(p.Job, uid, null))
                {
                    PostBuilding3D(Map3DLook.LookMessageJson(p.Building.Id, p.Building.Look with { Hidden = true }));   // 다시 만들기 전까지 구운 그 건물도 바로 안 보이게
                    SetAimStatus("직접 그린 건물을 지웠습니다 — 3D 를 다시 만들면 원래 건물로 돌아갑니다", autoHide: true);
                    CloseBuildingHeightPanel(clearUndo: false);
                }
                else SetBuildingHeightError(_ubError ?? "지우지 못했습니다.");
                return;
            }
            await SaveBuildingLookAsync(p.Job, p.Building.Id, true, null, p.Building.Look, "건물을 지웠습니다 — 「숨긴 건물」 목록에서 되살릴 수 있습니다");
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    // ── 모두 되살리기(확인) ──

    private async Task RequestRestoreAllLooksAsync()
    {
        if (_bhJob is not { } job || _bhBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingLooksReset = job;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "건물 모두 되살리기",
                Explain = $"이 3D 영역에서 {BuildingHiddenCountText}를 모두 되살리고, 바꾼 외벽 · 지붕 색과 무늬도 모두 자동으로 되돌리시겠습니까?",
                MessageModel = new CallRestoreMap3DLooksProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 모두 되살리기 확인 창 실패: {ex.Message}"); }
    }

    /// <summary>[모두 되살리기] 확인 콜백 — 서버에서 지우고, 그 장면을 띄워 둔 3D 에는 되돌린 건물을 바로 반영한다.</summary>
    public async Task HandleAsync(CallRestoreMap3DLooksProcessMessageModel message, CancellationToken cancellationToken)
    {
        var job = _pendingLooksReset;
        _pendingLooksReset = null;
        try
        {
            if (job == null || _map3DClient == null) return;
            var known = (_bhJob == job ? _bhLooks : null) ?? Array.Empty<Map3DLookItem>();
            int n = await _map3DClient.ResetAllLooksAsync(job, cancellationToken);
            foreach (var it in known)
                if (it.Id is int id) PostBuilding3D(Map3DLook.LookMessageJson(id, Map3DLookState.Auto));
            BhUndo.RemoveAll(u => u.Job == job && (u.Look != null || u.Group != null));   // 지운 값으로 되돌리지 않게
            if (_bhJob == job && _bhBuilding is { } b) { _bhBuilding = b with { Look = Map3DLookState.Auto }; _bhLookEdit = null; }
            _log?.Info($"[Map3D] 건물 외관 모두 되돌림 job={job} n={n}");
            SetAimStatus($"건물 {n}개를 되살리고 외관을 자동으로 되돌렸습니다", autoHide: true);
            await LoadBuildingLooksAsync(job);
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 모두 되살리기 — 서버 닿지 않음: {ex.Message}");
            SetBuildingHeightError(Map3DUnavailableText);
            NoteMap3DEngineDown("building-look-reset");
        }
        catch (Map3DRequestException ex) { SetBuildingHeightError($"되살리지 못했습니다: {ex.Message}"); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 모두 되살리기 실패: {ex.Message}");
            SetBuildingHeightError("되살리지 못했습니다.");
        }
        finally
        {
            RefreshBuildingHeightPanel();
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }
}
