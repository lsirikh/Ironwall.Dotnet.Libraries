using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 3D 를 떠나지 않고 편집(edit3d-contract, 2026-10-21 사용자 요청 — "3D 에서 수정 들어가면 바로 2D 로 풀린다").
                  「건물 만들기」 · 「꾸미기」 세션을 3D 위에서 연다(_ubIn3D · _decoIn3D) — 판은 같은 판(3D 위 캔버스층), 2D 지도 표면은 붙이지 않는다.
   Note         : ★ 0단계 = 지도 편집 모드에서만(2026-10-21 — 꺼짐 = 보기 전용: 고르기 무시 · 고치기 입구 막음, 끄면 열린 판 닫기 = CloseMap3DEditPanelsForViewMode).
                  ★ 1단계 = 3D 에서 고르기(userbuilding.pick · building+user_uid · decoration.pick) → 3D 위 판. [2D 에서 윤곽 고치기] 로만 2D.
                  ★ 2단계 = 판 도구 단추가 draw.start(점 · 선 · 다각형 · 사각형 · 원) → draw.point · draw.done 으로 받은 점을 기존 PUT 저장.
                    draw.end(3D 에서 Esc) = 도구 내려놓기. 판 닫기 · 판 안 Esc = draw.cancel.
                  ★ 3단계 = 고른 항목에 edit.start → 3D 손잡이 · 키로 고친 edit.commit 을 PUT(편집 기록 1단위). 실패 = edit.revert + 판에 서버 문구 + 목록 다시 받기.
                    3D 의 Esc 는 고친 게 남았으면 원위치(메시지 없음 · 서버 무호출), 없으면 edit.end(→ 판 고름 풀기).
                    판 단추 = edit.key 로 뷰어 키보드와 같게(화면 기준 ↑↓←→ · ↺↻ · 꼭짓점 이전/다음/지우기 · ✓ 확정 = Enter → edit.commit). 판 안 Esc 도 edit.key 로 넘긴다.
                  ★ 네이티브 창 3D(hwnd)는 WPF 판을 덮으므로 예전대로 2D 에서 고친다. 3D 를 나가면(Leave3D) 3D 세션은 끝난다.
   Created On   : 2026-10-21 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private bool _ubIn3D, _decoIn3D;
    /// <summary>GIS 가 켠 3D 그리기 방식(draw.start) — 꺼져 있으면 null.</summary>
    private string? _draw3DMode;
    /// <summary>3D 손잡이를 띄운 대상(edit.start) — 없으면 null.</summary>
    private (string Kind, string Uid)? _edit3DTarget;

    // ── 바인딩 ──

    /// <summary>「건물 만들기」 · 「꾸미기」 판이 3D 위에서 열려 있는가(판의 3D 칸 · [2D 에서 윤곽 고치기] 보임).</summary>
    public bool IsEdit3DMode => (_ubIn3D && IsUserBuildingDrawing) || (_decoIn3D && IsDecorating);
    /// <summary>3D 손잡이가 떠 있는가(판의 옮기기 · 확정 단추 보임).</summary>
    public bool IsEdit3DTargetActive => Edit3DTarget != null;
    public bool IsEdit3DVertexVisible => Edit3DTarget is { } t && t.Geometry != Map3DDecoration.GeoPoint;
    /// <summary>돌리기 단추 — 소품 · 넣은 모델만(엔진은 이 둘에만 heading_deg 를 붙인다).</summary>
    public bool IsEdit3DRotateVisible => _decoIn3D && IsEdit3DTargetActive && (DecoSelected?.Kind == Map3DDecoration.KindProp || ModelSelected != null);
    /// <summary>크기 단추(+ −) — 넣은 모델만.</summary>
    public bool IsEdit3DScaleVisible => _decoIn3D && IsEdit3DTargetActive && ModelSelected != null;
    /// <summary>지금 켠 3D 그리기 방식(시험 · 계측).</summary>
    public string? Edit3DDrawMode => _draw3DMode;

    /// <summary>손잡이를 띄운 대상의 도형 — 3D 에서 고친 직접 그린 건물 · 고른 꾸미기 항목.</summary>
    private (string Kind, string Uid, string Geometry)? Edit3DTarget
        => _edit3DTarget is null ? null
         : _ubIn3D && IsUserBuildingDrawing && _ubEditing is { } e ? (Map3DEdit.TargetUserBuilding, e.Uid, Map3DDecoration.GeoPolygon)
         : _decoIn3D && IsDecorating && DecoSelected is { } s ? (Map3DEdit.TargetDecoration, s.Uid, s.Geometry)
         : _decoIn3D && IsDecorating && ModelSelected is { } m ? (Map3DEdit.TargetModel, m.Uid, Map3DDecoration.GeoPoint)
         : null;

    /// <summary>3D 에서 고른 것을 3D 에 머문 채 고치는가 — 3D 표시 중이고 WPF 판이 3D 위에 보이는 방식(네이티브 창 3D 아님).</summary>
    private bool ShouldEditIn3D => _map3DState == Map3DState.Shown && !Map3DGuide.EditModeLeaves3D(Map3DHost.HostMode);

    private bool Edit3DBusy => _ubBusy || _decoBusy || _modelBusy;

    /// <summary>편집 모드가 꺼진 채 고치기 입구(건물 그리기 · 꾸미기 · 영역 배지 메뉴)를 눌렀을 때 안내.</summary>
    public const string EditModeRequiredText = "편집 모드에서 쓸 수 있습니다 — 툴바의 「편집」을 켜세요";

    /// <summary>
    /// 건물 고르기(숨기기 · 외관 · 높이) · 건물 그리기 · 꾸미기는 지도 편집 모드에서만(2026-10-21 사용자 결정 — 꺼짐 = 보기 전용, 2D · 3D 공통).
    /// 편집 모드는 지도 편집 권한이 있어야 켜지므로 권한 없는 계정은 여기서 함께 막힌다. 막히면 false(notify 면 안내 한 줄).
    /// </summary>
    private bool RequireMapEditMode(bool notify)
    {
        if (IsEditModeEnabled) return true;
        if (notify) SetAimStatus(EditModeRequiredText, autoHide: true);
        return false;
    }

    /// <summary>
    /// 편집 모드를 끈다 → 열린 고치기 판을 닫는다(진행 중 3D 그리기 = draw.cancel · 손잡이 = edit.stop — Exit* 안 EndEdit3D).
    /// 저장 안 한 값은 2D 편집 모드 끄기와 같게 묻지 않고 버린다(이미지 자르기 판 취소 · 선 그리기 취소와 같은 규칙). 「건물 높이」 미리보기는 원래대로.
    /// </summary>
    private void CloseMap3DEditPanelsForViewMode()
    {
        CloseBuildingHeightPanel(clearUndo: false);
        if (IsUserBuildingDrawing) ExitUserBuildingDraw("edit-mode-off");
        if (IsDecorating) ExitDecoration("edit-mode-off");
    }

    private RelayCommand? _edit3DKeyCommand;
    /// <summary>
    /// 판 단추 → 뷰어 키(edit.key) — 매개변수 = 키 이름(ArrowUp · ArrowDown · ArrowLeft · ArrowRight · [ · ] · Tab · Shift+Tab · Delete · Enter).
    /// 뷰어 키보드와 똑같다: 방향키 = 화면 기준 0.5 m(Shift = 5 m) · [ ] = 15°(Shift = 1°) · Tab = 다음 꼭짓점 · Delete = 그 꼭짓점 지우기는 미리보기만,
    /// Enter(✓ 확정)에서 뷰어가 edit.commit 을 보낸다(저장 · 편집 기록은 그 뒤 GIS).
    /// </summary>
    public RelayCommand Edit3DKeyCommand => _edit3DKeyCommand ??= new RelayCommand(p =>
    {
        if (Edit3DTarget == null || Map3DEdit.EditKeyJson(p?.ToString(), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) is not { } json) return;
        PostEdit3D(json);
    }, _ => IsEdit3DTargetActive && !Edit3DBusy);

    private RelayCommand? _editIn2DCommand;
    /// <summary>[2D 에서 윤곽 고치기] — 같은 판 · 같은 고름 그대로 2D 지도로 옮긴다(3D 를 나가고, 곧바로 다시 들어가지 않는다).</summary>
    public RelayCommand EditIn2DCommand => _editIn2DCommand ??= new RelayCommand(_ =>
    {
        if (!IsEdit3DMode || MainMap == null) return;
        EndEdit3D();
        if (_ubIn3D)
        {
            RevertUserBuildingPreview();   // 2D 그리기는 3D 를 접는다 — 저장 안 한 값으로 그린 상자를 남기지 않는다
            _ubIn3D = false;
            DetachUserBuildingSurface();
            AttachUserBuildingSurface();
            RefreshUserBuildingPanel();
        }
        else
        {
            _decoIn3D = false;
            DetachDecorationSurface();
            AttachDecorationSurface();
            RefreshDecorationPanel();
        }
        _log?.Info("[Edit3D] 2D 에서 고치기로");
        MainMap.Focus();
        SyncMap3DWithInputModes();   // 이제 2D 입력 모드 — 3D 를 접는다(진입 보류)
    }, _ => IsEdit3DMode);

    // ── GIS → 3D ──

    /// <summary>3D 세션 영역(job) — 없으면 null.</summary>
    private string? Edit3DJob => _ubIn3D ? _ubArea?.Id : _decoIn3D ? _decoArea?.Id : null;

    private void PostEdit3D(string json)
    {
        if (Edit3DJob is { } job) PostUserBuilding3D(job, json);
    }

    /// <summary>3D 그리기 모드 켜기 — 손잡이는 내린다(그리기와 손잡이는 함께 쓰지 않는다).</summary>
    private void StartDraw3D(string mode, string kind, JObject? preview)
    {
        StopEdit3D();
        _draw3DMode = mode;
        PostEdit3D(Map3DEdit.DrawStartJson(mode, kind, preview));
        _log?.Info($"[Edit3D] 그리기 시작 mode={mode} kind={kind}");
    }

    /// <summary>3D 그리기 모드 끄기 — 켜져 있었으면 true.</summary>
    private bool CancelDraw3D()
    {
        if (_draw3DMode == null) return false;
        _draw3DMode = null;
        PostEdit3D(Map3DEdit.DrawCancelJson());
        return true;
    }

    /// <summary>고른 항목에 3D 손잡이(같은 대상이어도 다시 보낸다 — 저장 · 되돌리기 뒤 새 모양으로 손잡이를 다시 세운다).</summary>
    private void StartEdit3D(string kind, string uid)
    {
        CancelDraw3D();
        _edit3DTarget = (kind, uid);
        PostEdit3D(Map3DEdit.EditStartJson(kind, uid));
    }

    private void StopEdit3D()
    {
        if (_edit3DTarget == null) return;
        _edit3DTarget = null;
        PostEdit3D(Map3DEdit.EditStopJson());
    }

    /// <summary>그리기 · 손잡이 모두 내린다(세션 끝 · 2D 로 옮기기).</summary>
    private void EndEdit3D()
    {
        CancelDraw3D();
        StopEdit3D();
    }

    /// <summary>3D 를 나간다(Leave3D) — 3D 위에서 열었던 판은 닫는다(2D 로 이어 고치려면 [2D 에서 윤곽 고치기]).</summary>
    private void EndEdit3DSessions(string cause)
    {
        if (_ubIn3D && IsUserBuildingDrawing) ExitUserBuildingDraw(cause);
        if (_decoIn3D && IsDecorating) ExitDecoration(cause);
    }

    // ── 3D → GIS ──

    /// <summary>draw.point · draw.done · draw.end · edit.commit · edit.end.</summary>
    private void OnMap3DEditMessage(Map3DMessage msg)
    {
        if (msg.EditCommit is { } c)
        {
            _log?.Info($"[Edit3D] edit.commit {c.Kind} uid={c.Uid} {c.Geometry} 점={c.Points.Count} 방향={c.HeadingDeg?.ToString() ?? "-"}");
            _ = CommitEdit3DAsync(c);
            return;
        }
        if (msg.Type == "edit.end")
        {
            // 바뀐 것 없이 3D 에서 Esc 로 편집이 끝났다 — 3D 가 이미 손잡이를 내렸다(edit.stop 을 되보내지 않는다). 판의 고름도 푼다
            _log?.Info($"[Edit3D] 3D 에서 편집 끝 reason={msg.Reason ?? "-"}");
            if (_edit3DTarget == null) return;
            _edit3DTarget = null;
            if (_decoIn3D && IsDecorating) { if (_modelSelectedUid != null) SelectModel(null); else SelectDecoration(null); }
            else if (_ubIn3D && IsUserBuildingDrawing) ResetUserBuildingDraft(null);
            return;
        }
        if (msg.Draw is not { } d) return;
        if (_draw3DMode == null) { _log?.Info($"[Edit3D] {msg.Type} — 그리기 중 아님(무시)"); return; }   // GIS 가 켜지 않았거나 이미 끈 그리기
        if (msg.Type == "draw.end")
        {
            _log?.Info($"[Edit3D] 3D 에서 그리기 끝 reason={d.Reason ?? "-"}");
            _draw3DMode = null;   // 3D 가 이미 껐다 — draw.cancel 을 다시 보내지 않는다
            if (_decoIn3D && IsDecorating) SetDecorationTool(null);
            else if (_ubIn3D && IsUserBuildingDrawing) RefreshUserBuildingPanel();
            return;
        }
        if (_decoIn3D && IsDecorating) OnDecorationDrawn3D(msg.Type, d);
        else if (_ubIn3D && IsUserBuildingDrawing) OnUserBuildingDrawn3D(msg.Type, d);
    }

    /// <summary>꾸미기 — 점 도구는 draw.point 마다 하나씩 저장(모드 유지), 선 · 영역은 draw.done 의 점으로 저장. 실패는 판에 이유 · 그리기는 그대로.</summary>
    private void OnDecorationDrawn3D(string type, Map3DDrawEvent d)
    {
        if (_decoTool is not { } tool || _decoBusy) return;
        bool point = Map3DDecoration.GeometryOf(tool) == Map3DDecoration.GeoPoint;
        if (point != (type == "draw.point")) return;
        DecoPoints.Clear();
        DecoPoints.AddRange(d.Points.Select(Map3DUserBuilding.Round));
        _ = PlaceDecorationAsync();
    }

    /// <summary>직접 그린 건물 — draw.done 의 윤곽을 판에 열어 높이 · 층수 기본값으로 저장을 기다린다.</summary>
    private void OnUserBuildingDrawn3D(string type, Map3DDrawEvent d)
    {
        if (type != "draw.done" || _ubBusy) return;
        if (_ubEditing != null) ResetUserBuildingDraft(null);
        if (_ubDraft is not { } draft) return;
        draft.Load(d.Points.Select(Map3DUserBuilding.Round));
        SetUserBuildingError(draft.Validate());
        RefreshUserBuildingPanel();
    }

    /// <summary>3D 손잡이 · 키(판 단추 포함, Enter 확정)로 고친 모양 저장. 실패면 3D 모양을 원위치(edit.revert).</summary>
    private async Task CommitEdit3DAsync(Map3DEditCommit c)
    {
        string? job = Edit3DJob;
        bool ok;
        try
        {
            ok = c.Kind == Map3DEdit.TargetUserBuilding ? await CommitUserBuildingShapeAsync(c)
               : c.Kind == Map3DEdit.TargetModel ? await CommitModelPlaceAsync(c)   // 넣은 모델(MapViewModel.Model3D)
               : await CommitDecorationShapeAsync(c);
        }
        catch (Exception ex) { _log?.Error($"[Edit3D] 모양 저장 실패(격리): {ex.Message}"); ok = false; }
        if (ok || job == null) return;
        PostUserBuilding3D(job, Map3DEdit.EditRevertJson(c.Kind, c.Uid));
        _log?.Info($"[Edit3D] edit.revert {c.Kind} uid={c.Uid}");
    }

    private async Task<bool> CommitUserBuildingShapeAsync(Map3DEditCommit c)
    {
        if (!_ubIn3D || _ubArea is not { } area || UserBuildingItems.FirstOrDefault(i => i.Uid == c.Uid) is not { } item) return false;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return false; }
        if (c.Geometry != Map3DDecoration.GeoPolygon) return false;
        if (Map3DUserBuilding.Validate(c.Points, p => area.Area.Contains(p.X, p.Y)) is { } why) { SetUserBuildingError(why); return false; }
        var before = StateOf(item);
        var after = before with { Outline = c.Points.ToList() };
        if (!await SaveUserBuildingAsync(area.Id, c.Uid, after))
        {
            if (_ubArea?.Id == area.Id) await LoadUserBuildingsAsync(area.Id);   // 다른 곳에서 바뀌었을 수 있다 — 서버 것으로
            return false;
        }
        _undoService?.Push(new UserBuildingCommand($"{Map3DUserBuilding.UndoPrefix} 모양 고치기", area.Id, c.Uid, before, after,
                                                   ApplyUserBuildingStateAsync, ResolveUserBuildingJob));
        if (_ubEditing?.Uid == c.Uid && UserBuildingItems.FirstOrDefault(i => i.Uid == c.Uid) is { } now)
        {
            _ubEditing = now;            // 판에 넣던 높이 · 이름 등은 그대로 두고 윤곽만 새 것으로
            _ubDraft?.Load(now.Outline);
            StartEdit3D(Map3DEdit.TargetUserBuilding, c.Uid);
        }
        SetAimStatus("건물 모양을 고쳤습니다", autoHide: true);
        RefreshUserBuildingPanel();
        return true;
    }

    private async Task<bool> CommitDecorationShapeAsync(Map3DEditCommit c)
    {
        if (!_decoIn3D || _decoArea is not { } area || DecorationItems.FirstOrDefault(i => i.Uid == c.Uid) is not { } item) return false;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return false; }
        if (c.Geometry != item.Geometry) return false;
        if (Map3DDecoration.Validate(item.Tool, c.Points, p => area.Area.Contains(p.X, p.Y)) is { } why) { SetDecorationError(why); return false; }
        var props = (JObject)item.Props.DeepClone();
        if (c.HeadingDeg is double h && item.Kind == Map3DDecoration.KindProp) props["heading_deg"] = h;
        if (await SaveDecorationAsync(area.Id, c.Uid, item with { Points = c.Points.ToList(), Props = props }) is not { } saved)
        {
            var error = _decoError;
            if (_decoArea?.Id == area.Id) await LoadDecorationsAsync(area.Id);   // 서버 것으로(다시 고르며 지운 이유 줄은 되살린다)
            SetDecorationError(error);
            return false;
        }
        _undoService?.Push(new DecorationCommand($"{Map3DDecoration.UndoPrefix} 옮기기", area.Id, c.Uid, item, saved, ApplyDecorationStateAsync, ResolveUserBuildingJob));
        if (_decoSelectedUid == c.Uid) SelectDecoration(c.Uid);   // 값 칸(방향) · 손잡이를 새 모양으로
        SetAimStatus("꾸미기 항목을 옮겼습니다", autoHide: true);
        return true;
    }

    /// <summary>
    /// 판 안 Esc 를 GIS 가 먼저 받았을 때 손잡이가 떠 있으면 뷰어에 넘긴다(edit.key Escape) — 고친 게 남았으면 뷰어가 원위치,
    /// 없으면 뷰어가 편집을 끝내고 edit.end 를 보낸다. 넘겼으면 true.
    /// </summary>
    private bool ForwardEscapeToEdit3D()
    {
        if (Edit3DTarget == null) return false;
        PostEdit3D(Map3DEdit.EditKeyJson("Escape", false)!);
        return true;
    }

    private void RefreshEdit3D()
    {
        foreach (var name in new[] { nameof(IsEdit3DMode), nameof(IsEdit3DTargetActive), nameof(IsEdit3DVertexVisible), nameof(IsEdit3DRotateVisible), nameof(IsEdit3DScaleVisible) })
            NotifyOfPropertyChange(name);
        foreach (var c in new[] { Edit3DKeyCommand, EditIn2DCommand })
            c.RaiseCanExecuteChanged();
    }

    /// <summary>키가 판에서 왔는가 — 3D 위 판은 판 안 키만 받는다(3D 화면의 키는 3D 몫: Esc · Enter · Backspace 등).</summary>
    private static bool IsPanelKey(KeyEventArgs e, System.Windows.Controls.Control? panel)
        => panel != null && IsInImageCropPanel(panel, e.OriginalSource ?? Keyboard.FocusedElement);
}
