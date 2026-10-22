using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 「건물 만들기 / 고치기」 판 복사 · 붙여넣기(2026-10-22 사용자 — "속성 복제도 안 되고 건물 카피 페이스트 안 된다", 2D · 3D 공통).
                  ① 건물 복사(Ctrl+C · [건물 복사]) = 고친 건물 또는 고른 건물들(저장된 모양 그대로) → [건물 붙여넣기](Ctrl+V) = 새 uid 로
                    묶음 폭 + 3 m 옆에(겹치지 않게, 다시 붙이면 한 칸 더 — Map3DUserBuilding.PasteOutlines), 편집 기록 1단위. 붙인 것을 바로 고른다.
                  ② 속성 복사(Ctrl+Shift+C · [속성 복사]) = 판에 보이는 높이 · 층수 · 지붕 · 외벽 성격 · 외벽 무늬 · 외벽 색 · 지붕 색 · 기울기(윤곽 · 이름 빼고)
                    → [속성 붙여넣기](Ctrl+Shift+V) = 고친 건물 또는 고른 건물 모두에 저장(편집 기록 1단위). 아직 저장하지 않은 새 윤곽이면 판 칸에만 넣는다.
   Note         : 키는 판 · 2D 지도에서 온 것(HandleUserBuildingKey)과 3D 화면에서 온 것(지도 창 PreviewKeyDown — OnMapPreviewKeyDownForGroup)을 함께 받는다.
                  복사할 것이 없으면 키를 넘긴다(심볼 복사 · 붙여넣기가 받는다). 복사본은 그리기를 끝내도 프로그램이 도는 동안 남는다(심볼 복사 버퍼처럼).
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    /// <summary>복사한 건물(저장된 상태) — 붙일 때마다 새 uid.</summary>
    private IReadOnlyList<UserBuildingState>? _ubClipboard;
    /// <summary>같은 복사본을 붙인 횟수 — 붙일 때마다 한 칸 더 옆으로(앞서 붙인 것과도 겹치지 않게).</summary>
    private int _ubPasteCount;
    /// <summary>복사한 속성(윤곽 · 이름 빼고).</summary>
    private UbStyle? _ubStyle;

    private sealed record UbStyle(double Height, int Floors, string Roof, string Use, Map3DLookState Look, double TaperDeg);

    /// <summary>복사할 건물 — 고른 건물들, 없으면 고치는 건물.</summary>
    private List<string> UserBuildingCopySources()
        => UbSelected.Count > 0 ? UbSelected.ToList() : _ubEditing is { } e ? new List<string> { e.Uid } : new List<string>();

    private RelayCommand? _copyUserBuildingsCommand;
    /// <summary>[건물 복사](Ctrl+C).</summary>
    public RelayCommand CopyUserBuildingsCommand => _copyUserBuildingsCommand ??= new RelayCommand(
        _ => CopyUserBuildings(), _ => IsUserBuildingDrawing && !_ubBusy && UserBuildingCopySources().Count > 0);

    private RelayCommand? _pasteUserBuildingsCommand;
    /// <summary>[건물 붙여넣기](Ctrl+V).</summary>
    public RelayCommand PasteUserBuildingsCommand => _pasteUserBuildingsCommand ??= new RelayCommand(
        _ => _ = PasteUserBuildingsAsync(), _ => IsUserBuildingDrawing && !_ubBusy && _ubClipboard is { Count: > 0 });

    private RelayCommand? _copyUserBuildingStyleCommand;
    /// <summary>[속성 복사](Ctrl+Shift+C) — 판 하나(여러 개 함께 고치기 중이 아닐 때) · 닫힌 윤곽.</summary>
    public RelayCommand CopyUserBuildingStyleCommand => _copyUserBuildingStyleCommand ??= new RelayCommand(
        _ => CopyUserBuildingStyle(), _ => IsUserBuildingDrawing && !_ubBusy && !_ubInputError && !IsUserBuildingGroup && _ubDraft is { IsClosed: true });

    private RelayCommand? _pasteUserBuildingStyleCommand;
    /// <summary>[속성 붙여넣기](Ctrl+Shift+V) — 고른 건물들 · 고치는 건물 · 새 윤곽(판 칸만).</summary>
    public RelayCommand PasteUserBuildingStyleCommand => _pasteUserBuildingStyleCommand ??= new RelayCommand(
        _ => _ = PasteUserBuildingStyleAsync(), _ => IsUserBuildingDrawing && !_ubBusy && _ubStyle != null && (UbSelected.Count > 0 || _ubDraft is { IsClosed: true }));

    /// <summary>Ctrl+C/V = 건물, Ctrl+Shift+C/V = 속성 — 그 명령을 쓸 수 있을 때만 처리(true). 아니면 키를 넘긴다.</summary>
    internal bool HandleUserBuildingClipboardKey(Key key, ModifierKeys modifiers)
    {
        if (!IsUserBuildingDrawing) return false;
        RelayCommand? command = (key, modifiers) switch
        {
            (Key.C, ModifierKeys.Control) => CopyUserBuildingsCommand,
            (Key.V, ModifierKeys.Control) => PasteUserBuildingsCommand,
            (Key.C, ModifierKeys.Control | ModifierKeys.Shift) => CopyUserBuildingStyleCommand,
            (Key.V, ModifierKeys.Control | ModifierKeys.Shift) => PasteUserBuildingStyleCommand,
            _ => null,
        };
        if (command?.CanExecute(null) != true) return false;
        command.Execute(null);
        return true;
    }

    private void CopyUserBuildings()
    {
        var items = UserBuildingCopySources().Select(uid => UserBuildingItems.FirstOrDefault(i => i.Uid == uid)).OfType<Map3DUserBuildingItem>().ToList();
        if (items.Count == 0) return;
        _ubClipboard = items.Select(StateOf).ToList();
        _ubPasteCount = 0;
        _log?.Info($"[UserBuilding] 복사 {items.Count}개 {string.Join(",", items.Select(i => i.Uid))}");
        SetAimStatus(Map3DUserBuilding.CopiedText(items.Count), autoHide: true);
        RefreshUserBuildingPanel();
    }

    private async Task PasteUserBuildingsAsync()
    {
        if (_ubArea is not { } area || _ubBusy || _ubClipboard is not { Count: > 0 } clip) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        if (Map3DUserBuilding.PasteOutlines(clip.Select(s => s.Outline).ToList(), _ubPasteCount + 1, p => area.Area.Contains(p.X, p.Y)) is not { } outlines)
        {
            SetUserBuildingError(Map3DUserBuilding.NoPasteRoomText);
            return;
        }
        var batch = clip.Select((s, i) => (Map3DUserBuilding.NewUid(), (UserBuildingState?)null, s with { Outline = outlines[i] })).ToList();
        int n = await SaveUserBuildingBatchAsync(area.Id, batch, k => $"{Map3DUserBuilding.UndoPrefix} {k}개 붙여넣기");
        _log?.Info($"[UserBuilding] 붙여넣기 {n}/{batch.Count} 칸={_ubPasteCount + 1} job={area.Id}");
        if (n == 0) return;
        _ubPasteCount++;
        SetAimStatus(Map3DUserBuilding.PastedText(n), autoHide: true);
        if (_ubArea?.Id != area.Id) return;
        // 붙인 것을 바로 고른다 — 하나면 그 건물 고치기(손잡이로 옮기기), 여러 개면 함께 고르기
        var pasted = batch.Take(n).Select(b => b.Item1).Where(uid => UserBuildingItems.Any(i => i.Uid == uid)).ToList();
        if (pasted.Count == 1) { ResetUserBuildingDraft(UserBuildingItems.First(i => i.Uid == pasted[0])); return; }
        ResetUserBuildingDraft(null);
        UbSelected.AddRange(pasted);
        PostUserBuildingSelection3D();
        LoadUserBuildingGroupFields();
        _ubAdorner?.InvalidateVisual();
        RefreshUserBuildingPanel();
    }

    private void CopyUserBuildingStyle()
    {
        _ubStyle = new UbStyle(_ubHeight, Map3DBuilding.FloorsFromHeight(_ubHeight, UbFloorHeight), UserBuildingRoof, UserBuildingUse, UserBuildingLook, _ubTaper);
        _log?.Info($"[UserBuilding] 속성 복사 {_ubStyle}");
        SetAimStatus(Map3DUserBuilding.StyleCopiedText, autoHide: true);
        RefreshUserBuildingPanel();
    }

    private async Task PasteUserBuildingStyleAsync()
    {
        if (_ubArea is not { } area || _ubBusy || _ubStyle is not { } st) return;
        var targets = UserBuildingCopySources();
        if (targets.Count == 0)
        {
            // 아직 저장하지 않은 새 윤곽 — 판 칸에만(3D 위 판은 상자 미리보기, 저장은 [적용])
            _ubHeight = st.Height;
            if (st.Height / st.Floors is >= Map3DBuilding.MinFloorHeight and <= Map3DBuilding.MaxFloorHeight) _ubFloorHeightValue = st.Height / st.Floors;
            _ubRoof = st.Roof; _ubRoofChosen = true; _ubUse = st.Use;
            _ubWallColor = st.Look.WallColor; _ubRoofColor = st.Look.RoofColor; _ubFacade = st.Look.Facade; _ubTaper = st.TaperDeg;
            _ubError = null;
            _ubInputError = false;
            RefreshUserBuildingPanel();
            SetAimStatus(Map3DUserBuilding.StyleToPanelText, autoHide: true);
            return;
        }
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        var batch = new List<(string Uid, UserBuildingState? Before, UserBuildingState After)>();
        foreach (var uid in targets)
        {
            if (UserBuildingItems.FirstOrDefault(i => i.Uid == uid) is not { } item) continue;
            var before = StateOf(item);
            var after = before with { Height = st.Height, Floors = st.Floors, Roof = st.Roof, Use = st.Use, Look = st.Look, TaperDeg = st.TaperDeg };
            if (after != before) batch.Add((uid, before, after));
        }
        if (batch.Count == 0) { SetAimStatus(Map3DUserBuilding.StyleSameText, autoHide: true); return; }
        int n = await SaveUserBuildingBatchAsync(area.Id, batch, k => $"{Map3DUserBuilding.UndoPrefix} {k}개 속성 붙여넣기");
        _log?.Info($"[UserBuilding] 속성 붙여넣기 {n}/{batch.Count} job={area.Id}");
        if (n > 0) SetAimStatus(Map3DUserBuilding.StylePastedText(n), autoHide: true);
        if (_ubArea?.Id != area.Id) return;
        // 고치던 건물 — 붙인 값으로 판을 다시 연다(층고도 그 건물 높이 ÷ 층수로), 여러 개 — 판 칸을 고른 건물 값으로
        if (_ubEditing is { } e && UserBuildingItems.FirstOrDefault(i => i.Uid == e.Uid) is { } now) ResetUserBuildingDraft(now);
        else { LoadUserBuildingGroupFields(); RefreshUserBuildingPanel(); }
    }
}
