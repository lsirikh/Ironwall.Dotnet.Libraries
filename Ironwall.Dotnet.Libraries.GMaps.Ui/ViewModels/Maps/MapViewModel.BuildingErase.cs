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
   Purpose      : 「건물 높이」 판의 지우기 · 다시 만들기(2026-10-22 사용자 — "기존에 만들어진 건물을 지우고 내가 다시 만들 수 있으면 좋겠어 · 삭제가 안 되는데?",
                  "안양발전소 이거 너가 미리 만들어 놓은 건 진짜 엉망이거든").
                  [지우고 이 자리에 새로 그리기] = 그 건물을 지우고(숨김) 그 바닥 윤곽으로 직접 그린 건물 판을 3D 위에 연다,
                  여러 개 고르기 = 2D 마커와 같은 규칙(2026-10-22 사용자 — "건물을 고르는 게 힘들다 · 2D 에서 Marker 선택할 때와 같은 방식으로"):
                  클릭 = 하나만 · Ctrl+클릭 = 넣기 · 빼기 · Shift+끌기 = 상자 안 모두(building.box, Ctrl+Shift = 더함) · 빈 곳 클릭 · Esc = 풀기
                  → 2개 이상이면 [고른 n개 지우기](확인). 옛 [여러 개 고르기] 모드 단추는 없앴다.
                  [이 영역 자동 건물 모두 지우기…](확인) = 그 장면의 자동 건물(지도 · 국가 자료 · 사진)을 모두 지운다(직접 그린 건물 · 꾸미기는 그대로).
   Note         : ★ 지도 건물 「지우기」 = 실제로는 숨김(PUT …/buildings/{bid}/look hidden=true → building.look, twin-contract A) — 다시 만들어도 숨긴 채,
                    「숨긴 건물 n개」 목록의 [되살리기] · [모두 되살리기] 로 되돌린다. 여러 개 지우기는 판 안 되돌리기 1칸(BuildingHeightUndoStep.Group).
                  ★ 고름 메시지(building)에는 바닥 윤곽이 없어 장면 파일(GET …/files/scene.json 의 buildings.footprint — 뷰어가 장면을 불러올 때 받는 그 파일)에서 찾는다.
                  ★ 고른 건물 모두 강조 = building.select {id(판의 건물), ids} — ids 를 모르는 옛 엔진은 판의 건물만 강조한다.
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private List<Map3DBuildingPick>? _bhSelectionList;
    /// <summary>3D 에서 고른 지도 건물(고른 순서, 판의 건물 포함 · 직접 그린 건물 · 숨긴 건물 빼고) — 지연 초기화(생성자 없이 만든 시험 VM 에서도).</summary>
    private List<Map3DBuildingPick> BhSelection => _bhSelectionList ??= new();
    private IReadOnlyList<(int Id, Map3DLookState Look)>? _pendingBuildingsHide;
    private string? _pendingBuildingsHideJob;

    /// <summary>여러 개 골랐나(2개 이상 — 판 「고른 건물 n개」 줄 · [고른 n개 지우기] 보임).</summary>
    public bool IsBuildingMultiSelect => IsBuildingHeightPanelOpen && BhSelection.Count > 1;
    public int BuildingMultiCount => BhSelection.Count;
    /// <summary>고른 건물 id(고른 순서 — 시험 · 계측용).</summary>
    public IReadOnlyList<int> BuildingSelectionIds => BhSelection.Select(b => b.Id).ToList();
    public string BuildingMultiSelectText => $"고른 건물 {BhSelection.Count}개";
    public string BuildingMultiDeleteText => $"고른 {BhSelection.Count}개 지우기";

    private bool CanEraseMapBuildings => IsBuildingHeightEditable && IsBuildingLookAvailable;

    private RelayCommand? _hideSelectedBuildingsCommand;
    /// <summary>[고른 n개 지우기] — 확인 한 번. 외관은 받아 둔 목록 값으로(되돌리기가 지우기 전 모양으로 돌아가게).</summary>
    public RelayCommand HideSelectedBuildingsCommand => _hideSelectedBuildingsCommand ??= new RelayCommand(
        _ => _ = RequestHideBuildingsAsync(BhSelection.Select(b => (b.Id, WithSavedLook(b).Look)).ToList(), "고른 건물 지우기",
                                           $"고른 건물 {BhSelection.Count}개를 3D 에서 지우시겠습니까?\n다시 만들어도 지운 채로 남고, 「숨긴 건물」 목록에서 되살릴 수 있습니다."),
        _ => CanEraseMapBuildings && IsBuildingMultiSelect);

    private RelayCommand? _hideAllAutoBuildingsCommand;
    /// <summary>[이 영역 자동 건물 모두 지우기…] — 장면 파일의 자동 건물 목록 → 확인 한 번.</summary>
    public RelayCommand HideAllAutoBuildingsCommand => _hideAllAutoBuildingsCommand ??= new RelayCommand(
        _ => _ = RequestHideAllAutoBuildingsAsync(), _ => CanEraseMapBuildings);

    private RelayCommand? _hideAndRedrawBuildingCommand;
    /// <summary>[지우고 이 자리에 새로 그리기] — 지도 건물만(직접 그린 건물은 그 판에서 바로 고친다).</summary>
    public RelayCommand HideAndRedrawBuildingCommand => _hideAndRedrawBuildingCommand ??= new RelayCommand(
        _ => _ = HideAndRedrawBuildingAsync(), _ => CanEraseMapBuildings && !IsBuildingUserDrawn && !IsBuildingHidden);

    /// <summary>고름에 넣을 수 있는 지도 건물인가 — 직접 그린 건물(그 판에서 고친다) · 숨긴 건물은 뺀다.</summary>
    private static bool IsSelectableMapBuilding(Map3DBuildingPick b) => b.Origin != "user" && b.UserUid == null && !b.Look.Hidden;

    /// <summary>고른 건물을 3D 에 모두 강조(판의 건물 = id) — 고름이 바뀔 때마다.</summary>
    private void PostBuildingSelection()
        => PostBuilding3D(Map3DBuilding.SelectMessageJson(_bhBuilding?.Id, BhSelection.Select(b => b.Id)));

    /// <summary>
    /// Ctrl+클릭(2D :1204 와 같다) — 그 지도 건물을 고름에 넣거나 뺀다. 넣으면 판이 그 건물로, 판의 건물을 빼면 남은 것 중 마지막으로,
    /// 모두 빼면 판을 닫는다(고름 풀림 — DECISIONS 3). 판이 닫혀 있거나 넣을 수 없는 건물이면 false(일반 클릭으로).
    /// </summary>
    private bool ToggleBuildingSelection(Map3DBuildingPick pick)
    {
        if (!IsBuildingHeightPanelOpen || _bhJob != _map3DJob) return false;
        var b = WithSavedLook(pick);
        if (!IsSelectableMapBuilding(b)) return false;
        int at = BhSelection.FindIndex(s => s.Id == b.Id);
        if (at >= 0)
        {
            BhSelection.RemoveAt(at);
            _log?.Info($"[Map3D] 건물 고름에서 뺌 id={b.Id} → {BhSelection.Count}개");
            if (BhSelection.Count == 0) { CloseBuildingHeightPanel(clearUndo: false); return true; }   // 강조 지움(building.select null)
            if (_bhBuilding?.Id == b.Id) OpenBuildingHeightPanel(BhSelection[^1]);
        }
        else
        {
            BhSelection.Add(b);
            _log?.Info($"[Map3D] 건물 고름에 넣음 id={b.Id} → {BhSelection.Count}개");
            OpenBuildingHeightPanel(b);
        }
        PostBuildingSelection();
        RefreshBuildingHeightPanel();
        return true;
    }

    /// <summary>
    /// Shift+끌기 상자(building.box — 2D :1165 러버밴드와 같다): add = false 면 상자 안 건물로 새로 고르고(빈 상자 = 고름 풀기),
    /// add = true(Ctrl+Shift) 면 지금 고름에 더한다(DECISIONS 1). 판은 지금 건물이 고름에 남아 있으면 그대로, 아니면 고른 것 중 첫 건물로.
    /// 직접 그린 건물 · 숨긴 건물은 빼고, 편집 모드가 아니면 무시한다(보기 전용).
    /// </summary>
    private void OnMap3DBuildingBox(IReadOnlyList<Map3DBuildingPick> items, bool add)
    {
        if (_map3DState != Map3DState.Shown || _map3DJob == null) return;
        if (!CanPickMap3DBuildings(null)) return;
        if (!add || !IsBuildingHeightPanelOpen || _bhJob != _map3DJob) BhSelection.Clear();
        var known = BhSelection.Select(s => s.Id).ToHashSet();
        foreach (var b in items.Select(i => _bhJob == _map3DJob ? WithSavedLook(i) : i))
            if (IsSelectableMapBuilding(b) && known.Add(b.Id)) BhSelection.Add(b);
        _log?.Info($"[Map3D] 건물 상자 고르기 받음={items.Count} add={add} → {BhSelection.Count}개 job={_map3DJob}");
        if (BhSelection.Count == 0)
        {
            if (IsBuildingHeightPanelOpen) CloseBuildingHeightPanel(clearUndo: false);   // 강조도 지운다
            else PostBuilding3D(Map3DBuilding.SelectMessageJson(null));
            return;
        }
        if (_bhJob != _map3DJob || BhSelection.All(s => s.Id != _bhBuilding?.Id))
            OpenBuildingHeightPanel(BhSelection[0]);
        PostBuildingSelection();
        RefreshBuildingHeightPanel();
    }

    private async Task RequestHideAllAutoBuildingsAsync()
    {
        if (_bhJob is not { } job || _map3DClient == null || _bhBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        IReadOnlyList<Map3DSceneBuilding>? rows;
        try { rows = await _map3DClient.GetSceneBuildingsAsync(job); }
        catch (Exception ex) { _log?.Warning($"[Map3D] 장면 건물 목록 실패 job={job}: {ex.Message}"); rows = null; }
        if (rows == null) { SetBuildingHeightError("이 영역의 건물 목록을 받지 못했습니다."); return; }
        var hidden = (_bhLooks ?? Array.Empty<Map3DLookItem>()).Where(l => l.State.Hidden && l.Id != null).Select(l => l.Id!.Value).ToHashSet();
        var targets = rows.Where(r => r.IsAuto && !hidden.Contains(r.Building.Id))
                          .Select(r => (r.Building.Id, (_bhLooks?.FirstOrDefault(l => l.Id == r.Building.Id)?.State ?? r.Building.Look))).ToList();
        if (targets.Count == 0) { SetAimStatus("이 영역에 지울 자동 건물이 없습니다", autoHide: true); return; }
        string title = Map3DAreas.FirstOrDefault(a => a.Id == job)?.Title ?? "이 영역";
        await RequestHideBuildingsAsync(targets, "자동 건물 모두 지우기",
            $"3D 영역 '{title}' 에서 자동으로 만든 건물 {targets.Count}개를 모두 지우시겠습니까?\n"
            + "직접 그린 건물 · 꾸미기는 그대로 둡니다. 「숨긴 건물」 목록의 [모두 되살리기] 로 한 번에 되살릴 수 있습니다.");
    }

    private async Task RequestHideBuildingsAsync(IReadOnlyList<(int Id, Map3DLookState Look)> targets, string title, string explain)
    {
        if (_bhJob is not { } job || _bhBusy || targets.Count == 0) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingBuildingsHide = targets;
            _pendingBuildingsHideJob = job;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = title, Explain = explain, MessageModel = new CallHideMap3DBuildingsProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 건물 여러 개 지우기 확인 창 실패: {ex.Message}"); }
    }

    /// <summary>[고른 n개 지우기] · [이 영역 자동 건물 모두 지우기] 확인 콜백.</summary>
    public async Task HandleAsync(CallHideMap3DBuildingsProcessMessageModel message, CancellationToken cancellationToken)
    {
        var targets = _pendingBuildingsHide;
        var job = _pendingBuildingsHideJob;
        _pendingBuildingsHide = null;
        _pendingBuildingsHideJob = null;
        try
        {
            if (targets == null || job == null || _bhJob != job) return;   // 그 사이 다른 영역으로
            await HideBuildingsAsync(job, targets);   // 지운 건물은 고름에서도 뺀다(HideBuildingsAsync)
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    /// <summary>
    /// 여러 건물 지우기 — 건물마다 PUT hidden=true → 3D 에 building.look(바로 사라짐) → 판 안 되돌리기 1칸(지운 것까지만). 고른 건물이 판의 건물이면 강조도 지운다.
    /// 중간에 실패하면 그때까지 지운 것은 남기고(되돌리기 칸에 쌓음) 판에 이유 한 줄. 모두 지웠으면 true.
    /// </summary>
    private async Task<bool> HideBuildingsAsync(string job, IReadOnlyList<(int Id, Map3DLookState Look)> targets)
    {
        if (_map3DClient == null || targets.Count == 0) return false;
        _bhError = null;
        _bhInputError = false;
        int session = _bhSession;
        var done = new List<(int Id, Map3DLookState Look)>();
        SetBuildingHeightBusy(true);
        try
        {
            foreach (var (id, look) in targets)
            {
                var saved = await _map3DClient.PutBuildingLookAsync(job, id, Map3DLook.PutBody(true, null));
                done.Add((id, look));
                if (!saved.NeedsRebake) PostUserBuilding3D(job, Map3DLook.LookMessageJson(id, saved.State));   // 그 장면을 띄워 둔 3D 에만
                if (_bhBuilding?.Id == id && _bhJob == job) _bhBuilding = _bhBuilding with { Look = saved.State };
            }
            if (_bhBuilding is { } b && _bhJob == job && done.Any(d => d.Id == b.Id)) PostBuilding3D(Map3DBuilding.SelectMessageJson(null));
            SetAimStatus($"건물 {done.Count}개를 지웠습니다 — 「숨긴 건물」 목록에서 되살릴 수 있습니다", autoHide: true);
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 건물 여러 개 지우기 — 서버 닿지 않음: {ex.Message}");
            SetBuildingHeightError(Map3DUnavailableText);
            NoteMap3DEngineDown("building-hide-many");
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")
        {
            _bhLooksUnsupported = true;
            SetBuildingHeightError(Map3DLook.OldEngineText);
        }
        catch (Map3DRequestException ex) { SetBuildingHeightError($"{done.Count}개를 지운 뒤 멈췄습니다: {ex.Message}"); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 건물 여러 개 지우기 실패: {ex.Message}");
            SetBuildingHeightError($"{done.Count}개를 지운 뒤 멈췄습니다.");
        }
        finally
        {
            if (done.Count > 0 && session == _bhSession) BhUndo.Add(new BuildingHeightUndoStep(job, done[0].Id, null, null) { Group = done });
            if (_bhJob == job) BhSelection.RemoveAll(s => done.Any(d => d.Id == s.Id));   // 사라진 건물은 고름에서도(중간 실패면 남은 것만 고른 채)
            SetBuildingHeightBusy(false);
        }
        _log?.Info($"[Map3D] 건물 여러 개 지움 {done.Count}/{targets.Count} job={job}");
        if (done.Count > 0) await LoadBuildingLooksAsync(job);
        return done.Count == targets.Count;
    }

    /// <summary>판 안 되돌리기(여러 개 지운 한 칸) — 그 건물들을 지우기 전 모양으로 다시 저장 → 3D 에 바로. 모두 되살렸으면 true.</summary>
    private async Task<bool> RestoreBuildingsAsync(string job, IReadOnlyList<(int Id, Map3DLookState Look)> group)
    {
        if (_map3DClient == null) return false;
        _bhError = null;
        SetBuildingHeightBusy(true);
        int n = 0;
        try
        {
            foreach (var (id, look) in group)
            {
                var before = look with { Hidden = false };
                var saved = await _map3DClient.PutBuildingLookAsync(job, id, Map3DLook.PutBody(false, before));
                n++;
                if (!saved.NeedsRebake) PostUserBuilding3D(job, Map3DLook.LookMessageJson(id, saved.State));
                if (_bhBuilding?.Id == id && _bhJob == job) _bhBuilding = _bhBuilding with { Look = saved.State };
            }
            SetAimStatus($"건물 {n}개를 되살렸습니다", autoHide: true);
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 건물 되살리기 — 서버 닿지 않음: {ex.Message}");
            SetBuildingHeightError(Map3DUnavailableText);
            NoteMap3DEngineDown("building-restore-many");
        }
        catch (Exception ex) { SetBuildingHeightError($"{n}개를 되살린 뒤 멈췄습니다: {ex.Message}"); }
        finally { SetBuildingHeightBusy(false); }
        await LoadBuildingLooksAsync(job);
        return n == group.Count;
    }

    /// <summary>
    /// [지우고 이 자리에 새로 그리기] — 그 건물의 바닥 윤곽(장면 파일)을 먼저 찾고 → 지우고(숨김 · 판 안 되돌리기 1칸) → 같은 3D 영역에서 직접 그린 건물 판을
    /// 그 윤곽 · 그 높이로 연다(3D 에 머문다 — 판 값을 바꾸면 3D 상자가 바로 바뀐다, [저장] 해야 남는다). 윤곽을 못 받으면 빈 그리기로 열고 찍게 한다.
    /// </summary>
    private async Task HideAndRedrawBuildingAsync()
    {
        if (_bhBuilding is not { } b || _bhJob is not { } job || _bhBusy || _map3DClient == null) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (Map3DAreas.FirstOrDefault(a => a.Id == job) is not { } area) return;
        IReadOnlyList<UbPoint>? footprint = null;
        try { footprint = (await _map3DClient.GetSceneBuildingsAsync(job))?.FirstOrDefault(r => r.Building.Id == b.Id)?.Footprint; }
        catch (Exception ex) { _log?.Warning($"[Map3D] 장면 건물 윤곽 실패 id={b.Id} job={job}: {ex.Message}"); }
        if (!await HideBuildingsAsync(job, new[] { (b.Id, b.Look) })) return;
        _log?.Info($"[Map3D] 지우고 새로 그리기 id={b.Id} 윤곽점={footprint?.Count ?? 0} job={job}");
        CloseBuildingHeightPanel(clearUndo: false);
        StartUserBuildingDraw(area, in3D: ShouldEditIn3D && area.Id == _map3DJob);
        if (_ubArea?.Id != job || _ubDraft is not { } draft) return;
        if (footprint is { Count: >= Map3DUserBuilding.MinPoints } pts
            && Map3DUserBuilding.Validate(pts.Select(Map3DUserBuilding.Round).ToList(), p => area.Area.Contains(p.X, p.Y)) == null)
        {
            draft.Load(pts.Select(Map3DUserBuilding.Round));
            _ubHeight = Map3DBuilding.ClampHeight(b.Height);
            RefreshUserBuildingPanel();   // 3D 위 판이면 그 윤곽 · 높이로 상자가 바로 보인다
            SetAimStatus("지운 건물 자리에 같은 모양으로 열었습니다 — 층수 · 높이 · 지붕을 고치고 [저장] 하세요", autoHide: true);
        }
        else SetAimStatus("지운 건물의 윤곽을 받지 못했습니다 — 판의 도구로 그 자리에 그리세요", autoHide: true);
    }
}
