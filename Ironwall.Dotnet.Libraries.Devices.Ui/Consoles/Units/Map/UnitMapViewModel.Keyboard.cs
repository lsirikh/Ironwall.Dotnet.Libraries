using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 뷰모델 — 키보드 폴백(선택 이동 · M 위치 이동 · Alt+↑/↓ · Ctrl+Z · L) · 지도에서 보기 (FR-36~39 · FR-44)
   Created By   : Claude
   Created On   : 2026-09-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public sealed partial class UnitMapViewModel
{
    /// <summary>M 모드 화살표 한 번(월드 단위).</summary>
    public const double MoveStep = 20;

    /// <summary>M 모드 <c>Shift</c>+화살표 한 번(월드 단위).</summary>
    public const double MoveFineStep = 5;

    private readonly CoalescingTrigger _selectTrigger;
    private int? _pendingSelect;
    private bool _moveMode;
    private int? _moveUnit;
    private Vector _moveOffset;
    private bool _locateIncludeDescendants = true;
    private (Guid RequestId, string Title)? _pendingLocate;

    #region - 키 표 (FR-36~39) -
    /// <summary>
    /// 캔버스가 해석한 키 하나(<c>Key.System</c> · <c>ImeProcessed</c> 해석은 캔버스 몫 — 한국어 IME 에서 M · L 이 <c>ImeProcessedKey</c> 로 온다, #33).
    /// Esc 우선순위 = 확인 오버레이 &gt; (끌기는 캔버스가 먼저 소비) &gt; M 모드 &gt; 처리하지 않음.
    /// </summary>
    /// <returns>처리(또는 오버레이 · M 모드가 삼킴)했으면 <c>true</c>.</returns>
    public bool HandleKey(UnitMapKeyCommand command, bool shift)
    {
        if (_pending is not null) return HandleConfirmKey(command);
        if (_moveMode) return HandleMoveModeKey(command, shift);

        switch (command)
        {
            case UnitMapKeyCommand.Up:
            case UnitMapKeyCommand.Down:
            case UnitMapKeyCommand.Left:
            case UnitMapKeyCommand.Right:
            case UnitMapKeyCommand.Home:
                MoveSelection(command);
                return true;
            case UnitMapKeyCommand.Enter:
                if (_selected is null) return false;
                RequestFocus(UnitMapFocusTarget.DetailFirstField);
                return true;
            case UnitMapKeyCommand.MoveMode:
                EnterMoveMode();
                return true;
            case UnitMapKeyCommand.ParentUp:
                ProposeParentUp();
                return true;
            case UnitMapKeyCommand.ParentDown:
                ProposeParentDown();
                return true;
            case UnitMapKeyCommand.Undo:
                _ = UndoAsync();
                return true;
            case UnitMapKeyCommand.LocateOnMap:
                _ = LocateOnMapAsync();
                return true;
            default:
                return false;     // Esc — 캔버스 밖 기존 처리(선택 해제 등)에 맡긴다
        }
    }

    /// <summary>오버레이가 떠 있는 동안: Enter = 확정 · Esc = 취소 · 그 밖은 삼킨다(콘솔 전체 모달 — #49).</summary>
    private bool HandleConfirmKey(UnitMapKeyCommand command)
    {
        if (command == UnitMapKeyCommand.Enter) _ = ConfirmAsync();
        else if (command == UnitMapKeyCommand.Escape) CancelConfirm();
        return true;
    }
    #endregion

    #region - 선택 이동 (FR-36 · 필수 항목 2) -
    /// <summary>
    /// 화살표 — 선택 표시는 즉시 옮기고(보이지 않으면 팬), 콘솔 선택(상세 GET · 가드)은 키가 멈춘 뒤 한 번만 확정한다.
    /// 자동 반복 키가 상세 GET 을 쏟아내지 않게(필수 항목 2).
    /// </summary>
    private void MoveSelection(UnitMapKeyCommand command)
    {
        var next = UnitMapNavigation.Next(_tree, _scene.Positions, _selected, command, MyUnitId);
        if (next is not int id || id == _selected) return;

        SelectedUnitId = id;
        _pendingSelect = id;
        if (_surface is { } surface && !surface.IsInView(id)) surface.CenterOn(id);
        _ = _selectTrigger.Pulse();
    }

    private Task OnSelectionSettledAsync(CancellationToken token)
    {
        if (_pendingSelect is not int id) return Task.CompletedTask;
        _pendingSelect = null;
        if (_pending is not null || _tree.Find(id) is null) { SelectedUnitId = _commands.SelectedUnitId; return Task.CompletedTask; }
        SelectedUnitId = _commands.TrySelect(id) ? id : _commands.SelectedUnitId;
        return Task.CompletedTask;
    }
    #endregion

    #region - M 위치 이동 모드 (FR-37) -
    /// <summary>M 모드 중인가(<c>Units.Map.MoveMode</c>).</summary>
    public bool IsMoveMode => _moveMode;

    /// <summary>M 모드 안내(캔버스 <c>MoveModeText</c> · <c>Units.Map.MoveMode</c>) — M 모드가 아니면 <c>null</c>.</summary>
    public string? MoveModeText => _moveMode && _moveUnit is int unit ? UnitMapText.MoveModeStatus(NameOf(unit)) : null;

    /// <summary>M 모드에서 쌓인 이동량(월드).</summary>
    public Vector MoveModeOffset => _moveOffset;

    private void EnterMoveMode()
    {
        if (_selected is not int unit) { StatusText = UnitMapText.MoveModeNeedsSelection; return; }   // FR-37 경계 표
        var decision = UnitMapDropClassifier.Classify(_tree, unit, null, ctrl: true, Policy);
        if (!decision.IsAllowed)
        {
            StatusText = KoreanParticles.Resolve(decision.Reason ?? string.Empty);
            return;
        }
        _moveMode = true;
        _moveUnit = unit;
        _moveOffset = default;
        StatusText = UnitMapText.MoveModeStatus(NameOf(unit));
        NotifyMoveMode();
    }

    private bool HandleMoveModeKey(UnitMapKeyCommand command, bool shift)
    {
        var step = shift ? MoveFineStep : MoveStep;
        switch (command)
        {
            case UnitMapKeyCommand.Up: Nudge(0, -step); break;
            case UnitMapKeyCommand.Down: Nudge(0, step); break;
            case UnitMapKeyCommand.Left: Nudge(-step, 0); break;
            case UnitMapKeyCommand.Right: Nudge(step, 0); break;
            case UnitMapKeyCommand.Enter:
                var (unit, offset) = (_moveUnit!.Value, _moveOffset);
                var target = DisplayDeltaOf(unit) ?? default;  // 미리보기가 입힌 Δ(자기 Δ + 쌓인 이동량) 그대로 확정
                // M 모드 동안 본 배치 — M 을 끝내면 미룬 배치 알림의 재조회가 곧바로 돌 수 있다(FR-53). If-Match 기준은 운영자가 본 그림이다
                // (TEST-43 r11b H-10 — 종전엔 그 재조회의 최신 판으로 보내 다른 운영자의 Δ 를 말없이 덮었다).
                var view = CaptureView();
                ExitMoveMode(focusCanvas: true);
                if (offset.X != 0 || offset.Y != 0) _ = WritePositionAsync(unit, target, view);
                break;
            case UnitMapKeyCommand.Escape:
                ExitMoveMode(focusCanvas: true);        // 원위치 · 서버 0
                break;
        }
        return true;                                    // M 모드 동안 다른 키는 삼킨다
    }

    private void Nudge(double dx, double dy)
    {
        _moveOffset += new Vector(dx, dy);
        NotifyOfPropertyChange(nameof(MoveModeOffset));
        RebuildScene();
        // FR-37 경계 표 — 옮기는 노드가 뷰 밖으로 나가면 보이게 팬
        if (_moveUnit is int unit && _surface is { } surface && !surface.IsInView(unit)) surface.CenterOn(unit);
    }

    private void ExitMoveMode(bool focusCanvas)
    {
        _moveMode = false;
        _moveUnit = null;
        _moveOffset = default;
        StatusText = null;
        NotifyMoveMode();
        RebuildScene();
        if (focusCanvas) RequestFocus(UnitMapFocusTarget.Canvas);
    }

    private void NotifyMoveMode()
    {
        NotifyOfPropertyChange(nameof(IsMoveMode));
        NotifyOfPropertyChange(nameof(MoveModeOffset));
        NotifyOfPropertyChange(nameof(MoveModeText));
        UpdateBusy();
    }
    #endregion

    #region - Alt+↑/↓ 상위 바꾸기 (FR-38 · #34) -
    /// <summary>Alt+↑ — 상위의 상위 밑으로. 트리와 같은 판정(<see cref="UnitMovePlanner.PlanMoveUp"/>)이지만 관계도에서는 확인 오버레이를 거친다(결정 #2).</summary>
    private void ProposeParentUp()
    {
        if (_selected is not int unit || _tree.Find(unit) is not { } node) return;
        var plan = UnitMovePlanner.PlanMoveUp(_tree, unit);
        if (!plan.IsAllowed) { StatusText = KoreanParticles.Resolve(plan.BlockedReason!); return; }
        if (plan.ToRoot || plan.NewParentId is not int target) { StatusText = UnitMapText.NoGrandParentStatus(node.Name); return; }
        ProposeReparent(unit, target);
    }

    /// <summary>
    /// Alt+↓ — 편제 트리 순서(<see cref="UnitTreeModel.Ordered"/>)에서 바로 앞쪽의, 받을 수 있는 첫 상위 밑으로
    /// (<see cref="UnitMovePlanner.PlanMoveDown"/> — #34, 트리 레일의 접힘 · 필터와 무관).
    /// </summary>
    private void ProposeParentDown()
    {
        if (_selected is not int unit || _tree.Find(unit) is null) return;
        var plan = UnitMovePlanner.PlanMoveDown(_tree, unit);
        if (!plan.IsAllowed || plan.NewParentId is not int target) { StatusText = KoreanParticles.Resolve(plan.BlockedReason ?? string.Empty); return; }
        ProposeReparent(unit, target);
    }

    private int IndexInOrder(int unitId)
    {
        for (var i = 0; i < _tree.Ordered.Count; i++)
            if (_tree.Ordered[i].Id == unitId) return i;
        return -1;
    }

    private void ProposeReparent(int unit, int target)
    {
        var decision = Classify(unit, target, ctrl: false);
        if (decision.Kind != UnitMapDropKind.Reparent)
        {
            StatusText = KoreanParticles.Resolve(decision.Reason ?? string.Empty);
            return;
        }
        OpenPending(UnitMapConfirmKind.Reparent, unit, target, UnitMapText.ConfirmReparent(_tree, unit, target, showParentPath: !_layers.Hierarchy));
    }
    #endregion

    #region - 지도에서 보기 (FR-44) -
    /// <summary>[예하 포함] 토글(<c>Units.Map.IncludeSubordinates</c>, 기본 켬).</summary>
    public bool LocateIncludeDescendants
    {
        get => _locateIncludeDescendants;
        set
        {
            if (_locateIncludeDescendants == value) return;
            _locateIncludeDescendants = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanLocateOnMap));
            NotifyOfPropertyChange(nameof(LocateDisabledReason));
        }
    }

    /// <summary>[지도에서 보기]를 누를 수 있다 — 선택 부대(+예하)에 장비가 있다.</summary>
    public bool CanLocateOnMap => LocateSet() is { IsEmpty: false };

    /// <summary>누를 수 없는 까닭(툴팁) — "이 부대에 속한 장비가 없습니다".</summary>
    public string? LocateDisabledReason => LocateSet() is { IsEmpty: true } set ? set.DisabledReason : null;

    private UnitMapLocateSet? LocateSet()
        => _selected is int unit ? UnitMapLocate.CollectDeviceIds(_tree, _devices, unit, _locateIncludeDescendants) : null;

    /// <summary>
    /// [지도에서 보기] · <c>L</c> — 선택 부대(+예하)의 장비 id 를 모아 <see cref="MapLocateRequest"/> 를 보낸다. 장비 0 이면 보내지 않고 까닭을 말한다.
    /// </summary>
    public async Task LocateOnMapAsync()
    {
        if (_selected is not int unit) return;
        var set = UnitMapLocate.CollectDeviceIds(_tree, _devices, unit, _locateIncludeDescendants);
        if (set.IsEmpty) { StatusText = set.DisabledReason; return; }

        var title = NameOf(unit);
        if (MapLocateRequest.For(title, set.DeviceIds) is not { } request || _options.Events is not { } events) return;
        _pendingLocate = (request.RequestId, title);
        await events.PublishOnUIThreadAsync(request).ConfigureAwait(true);
    }

    /// <summary>지도의 회신 — 내가 보낸 요청(<c>RequestId</c>)만 상태 문구로 옮긴다.</summary>
    public Task HandleAsync(MapLocateResult message, CancellationToken cancellationToken)
    {
        if (message is not null && _pendingLocate is { } pending && pending.RequestId == message.RequestId)
        {
            _pendingLocate = null;
            StatusText = UnitMapText.MapLocateResult(pending.Title, message.Shown, message.Missing, message.Hidden, message.OutsideAnchor);
        }
        return Task.CompletedTask;
    }
    #endregion
}
