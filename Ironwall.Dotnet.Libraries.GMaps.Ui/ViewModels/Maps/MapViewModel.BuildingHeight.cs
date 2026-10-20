using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
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
   Purpose      : 「건물 높이」 판(MapView.xaml "Map.BuildingHeight.Panel") — 3D 에서 고른 건물의 높이 · 층수를 보고 직접 넣는다.
                  보안 구역이라 공개 건물 자료(층수)를 못 구할 때 운영자가 아는 층수를 넣는다(2026-10-19).
   Note         : ★ 판에서 바꾸면 3D 가 그 건물만 즉시 미리보기(building.preview — 다시 굽지 않고 세로로 늘임). [취소] · 닫기 · Esc · 다른 건물 고르기 ·
                    3D 나가기 = 미리보기를 원래대로. [적용] = map-3d 에 저장(PUT …/buildings/{id}/height) → 3D 에 저장된 줄(building.commit).
                  ★ map-3d 가 영역 단위 · 건물 자리로 저장해 다시 굽기에도 유지한다(heights.py). [자동으로 되돌리기] = DELETE.
                  ★ 되돌리기: GIS Ctrl+Z(UndoCommand)는 편집 모드 ∧ 권한에서만 켜지고 3D 는 편집 모드에서 열리지 않는다 →
                    판 안 [되돌리기](Ctrl+Z, 판 포커스) — 이 3D 동안 적용 · 자동으로 되돌리기를 하나씩 거꾸로(3D 를 나가면 비움).
                  ★ 지도 편집 권한(CanEditMap)이 없으면 보기만(입력 비활성 + 이유 한 줄). 규칙 · 글은 순수 함수 Map3DBuilding(헤드리스 시험).
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel : IBuildingHeightPanelHost, IHandle<CallResetMap3DHeightsProcessMessageModel>
{
    /// <summary>판 안 되돌리기 한 칸 — 그 장면 · 건물을 Before 높이로(null = 자동).</summary>
    private sealed record BuildingHeightUndoStep(string Job, int Id, double? Before, int? BeforeFloors);

    private Map3DBuildingPick? _bhBuilding;   // 고른 건물(저장된 값 = 취소하면 돌아갈 값)
    private string? _bhJob;
    private double _bhPreview;
    private double _bhFloorHeightValue = Map3DBuilding.DefaultFloorHeight;
    /// <summary>층고(m) — 값이 없으면(생성자 없이 만든 VM) 기본 3.0.</summary>
    private double BhFloorHeight { get => _bhFloorHeightValue > 0 ? _bhFloorHeightValue : Map3DBuilding.DefaultFloorHeight; set => _bhFloorHeightValue = value; }
    private string _bhFloorsText = "";
    private string _bhHeightText = "";
    private string _bhFloorHeightText = Map3DBuilding.FormatMeters(Map3DBuilding.DefaultFloorHeight);
    private string? _bhError;
    /// <summary>지금 오류 줄이 입력칸의 잘못된 값 때문인가 — 그동안은 [적용] 을 막는다(앞 미리보기 값이 저장되지 않게). 저장 실패 줄은 다시 [적용] 할 수 있다.</summary>
    private bool _bhInputError;
    private bool _bhBusy;
    /// <summary>3D 세션 번호 — 3D 를 나갈 때(되돌리기 칸 비움) 하나 오른다. 나간 뒤 도착한 저장 응답은 칸에 쌓지 않는다.</summary>
    private int _bhSession;
    private double? _bhDragStart;
    private List<BuildingHeightUndoStep>? _bhUndoSteps;
    /// <summary>판 안 되돌리기 칸들(늦게 만든다 — 생성자 없이 만든 VM(시험 장치)에서도 3D 나가기가 깨지지 않게).</summary>
    private List<BuildingHeightUndoStep> BhUndo => _bhUndoSteps ??= new();
    private Map3DAreaItem? _pendingMap3DHeightsReset;
    /// <summary>3D 로 건물 메시지 보내기(기본 = Map3DHost.PostJson — 시험은 가짜로 바꿔 보낸 것을 본다).</summary>
    private Action<string>? _map3DBuildingPost;
    private void PostBuilding3D(string json) => (_map3DBuildingPost ?? Map3DHost.PostJson)(json);

    private BuildingHeightPanelControl? _bhPanel;
    /// <summary>「건물 높이」 판(OverlayWindow 패턴 Control) — DataContext=this, 머리 띠 ✕ = 취소.</summary>
    public BuildingHeightPanelControl BuildingHeightPanel => _bhPanel ??= new BuildingHeightPanelControl
    {
        DataContext = this,
        CloseCommand = CancelBuildingHeightCommand,
        Host = this,
    };

    /// <summary>판이 열려 있는가 = 3D 에서 건물을 골랐는가.</summary>
    public bool IsBuildingHeightPanelOpen => _bhBuilding != null;
    /// <summary>고른 건물(시험 · 계측용).</summary>
    public Map3DBuildingPick? BuildingHeightBuilding => _bhBuilding;
    /// <summary>미리보기 높이(m) — 3D 에 보이는 높이.</summary>
    public double BuildingHeightPreview => _bhPreview;
    /// <summary>"지금 12.0 m · 4층(층고 3.0 m)" — 저장된 높이.</summary>
    public string BuildingHeightCurrentText => _bhBuilding is { } b ? Map3DBuilding.CurrentText(b.Height, BhFloorHeight) : "";
    /// <summary>높이 출처 쉬운 말(지도 자료 · 국가 자료 · 사진에서 잼 · 그림자로 잼 · 모름(기본값) · 직접 입력).</summary>
    public string BuildingHeightSourceText => _bhBuilding is { } b ? Map3DBuilding.SourceText(b.HeightFrom, b.Origin) : "";
    /// <summary>권한이 없어 보기만인가(이유 한 줄 보임).</summary>
    public bool IsBuildingHeightReadOnly => IsBuildingHeightPanelOpen && !CanEditMap();
    public string BuildingHeightReadOnlyText => Map3DBuilding.ReadOnlyReason;
    /// <summary>입력 · 손잡이를 쓸 수 있는가 — 권한이 있고 저장 중이 아닐 때.</summary>
    public bool IsBuildingHeightEditable => IsBuildingHeightPanelOpen && CanEditMap() && !_bhBusy;
    /// <summary>미리보기가 저장된 높이와 다른가.</summary>
    public bool IsBuildingHeightChanged => _bhBuilding is { } b && Math.Abs(_bhPreview - b.Height) > 1e-9;
    /// <summary>판 안 되돌리기 칸 수(이 3D 동안).</summary>
    public int BuildingHeightUndoCount => BhUndo.Count;

    /// <summary>적용할 내용 한 줄 — "적용하면 12.0 m → 30.0 m(10층)" · 바꾸지 않았으면 그렇게.</summary>
    public string BuildingHeightSummary => _bhBuilding is not { } b ? ""
        : _bhBusy ? "저장하는 중… 끝날 때까지 닫을 수 없습니다"
        : IsBuildingHeightChanged
            ? $"적용하면 {Map3DBuilding.FormatMeters(b.Height)} m → {Map3DBuilding.FormatMeters(_bhPreview)} m({Map3DBuilding.FloorsFromHeight(_bhPreview, BhFloorHeight)}층)"
            : b.Manual ? "직접 입력한 높이입니다 — 바꾸려면 층수나 높이를 넣으세요" : "아직 바꾸지 않았습니다";

    /// <summary>층수 입력칸(정수 1~200) — 넣으면 높이 = 층수 × 층고로 미리보기.</summary>
    public string BuildingHeightFloorsText
    {
        get => _bhFloorsText;
        set
        {
            _bhFloorsText = value ?? "";
            if (!IsBuildingHeightEditable) { NotifyOfPropertyChange(); return; }
            if (!Map3DBuilding.TryParseFloors(value, out var floors, out var err)) { SetBuildingHeightInputError(err); NotifyOfPropertyChange(); return; }
            SetBuildingHeightPreview(Map3DBuilding.HeightFromFloors(floors, BhFloorHeight));
        }
    }

    /// <summary>높이 입력칸(m, 소수 1자리, 1~600) — 넣으면 그 높이로 미리보기.</summary>
    public string BuildingHeightText
    {
        get => _bhHeightText;
        set
        {
            _bhHeightText = value ?? "";
            if (!IsBuildingHeightEditable) { NotifyOfPropertyChange(); return; }
            if (!Map3DBuilding.TryParseHeight(value, out var h, out var err)) { SetBuildingHeightInputError(err); NotifyOfPropertyChange(); return; }
            SetBuildingHeightPreview(h);
        }
    }

    /// <summary>층고 입력칸(m, 2~10, 기본 3.0) — 높이는 그대로, 층수 표시가 따라 바뀐다(보기만이어도 바꿀 수 있다).</summary>
    public string BuildingHeightFloorHeightText
    {
        get => _bhFloorHeightText;
        set
        {
            _bhFloorHeightText = value ?? "";
            if (!Map3DBuilding.TryParseFloorHeight(value, out var fh, out var err)) { SetBuildingHeightInputError(err); NotifyOfPropertyChange(); return; }
            BhFloorHeight = fh;
            _bhError = null;
            _bhInputError = false;
            RefreshBuildingHeightPanel();
        }
    }

    public string? BuildingHeightError => _bhError;
    public bool HasBuildingHeightError => _bhError != null;

    private RelayCommand? _stepBuildingFloorCommand;
    /// <summary>±1층 단추 — CommandParameter = "1" · "-1".</summary>
    public RelayCommand StepBuildingFloorCommand => _stepBuildingFloorCommand ??= new RelayCommand(p =>
    {
        if (!IsBuildingHeightEditable || !int.TryParse(p?.ToString(), out var delta)) return;
        SetBuildingHeightPreview(Map3DBuilding.StepFloors(_bhPreview, BhFloorHeight, delta));
    }, _ => IsBuildingHeightEditable);

    private RelayCommand? _applyBuildingHeightCommand;
    /// <summary>「적용」 — map-3d 에 저장.</summary>
    public RelayCommand ApplyBuildingHeightCommand => _applyBuildingHeightCommand ??= new RelayCommand(
        _ => _ = ApplyBuildingHeightAsync(), _ => IsBuildingHeightEditable && IsBuildingHeightChanged && !_bhInputError);

    private RelayCommand? _cancelBuildingHeightCommand;
    /// <summary>「취소」 · 닫기(✕) · Esc — 미리보기를 원래대로 하고 판을 닫는다. 저장 중에는 막는다(취소했는데 저장되지 않게 — 단추가 흐려진다).</summary>
    public RelayCommand CancelBuildingHeightCommand => _cancelBuildingHeightCommand ??= new RelayCommand(_ =>
    {
        if (_bhBusy) { SetAimStatus(BuildingHeightSavingText, autoHide: true); return; }
        CloseBuildingHeightPanel(clearUndo: false);
    }, _ => !_bhBusy);

    /// <summary>저장 중 [취소] · ✕ · Esc 를 눌렀을 때 한 줄.</summary>
    private const string BuildingHeightSavingText = "건물 높이를 저장하는 중입니다 — 끝난 뒤 닫을 수 있습니다";

    private RelayCommand? _resetBuildingHeightCommand;
    /// <summary>「자동으로 되돌리기」 — 직접 입력을 지우고 굽기 때 자동 추정 높이로.</summary>
    public RelayCommand ResetBuildingHeightCommand => _resetBuildingHeightCommand ??= new RelayCommand(
        _ => _ = ResetBuildingHeightAsync(), _ => IsBuildingHeightEditable && _bhBuilding?.Manual == true);

    private RelayCommand? _undoBuildingHeightCommand;
    /// <summary>판 안 「되돌리기」 — 이 3D 동안 저장한 것을 하나씩 거꾸로.</summary>
    public RelayCommand UndoBuildingHeightCommand => _undoBuildingHeightCommand ??= new RelayCommand(
        _ => _ = UndoBuildingHeightAsync(), _ => IsBuildingHeightEditable && BhUndo.Any(u => u.Job == _bhJob));

    // ── 3D 에서 고르기 ──

    /// <summary>3D 에서 건물을 눌렀다(building 메시지) — 판을 그 건물로 연다. 다른 건물을 보던 중이면 그 미리보기를 먼저 원래대로.</summary>
    private void OnMap3DBuildingPicked(Map3DBuildingPick pick)
    {
        if (_map3DState != Map3DState.Shown || _map3DJob == null) return;
        if (Map3DHost.HostMode == "hwnd")
        {
            // 네이티브 창 3D 는 WPF 판을 덮는다 — 안 보이는 판이 첫 Esc 를 가로채 3D 를 못 나가게 되므로 열지 않고 알린다(3D 가 그린 윤곽은 지운다)
            _log?.Info($"[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음 id={pick.Id} job={_map3DJob}");
            try { PostBuilding3D(Map3DBuilding.SelectMessageJson(null)); }
            catch (Exception ex) { _log?.Error($"[Map3D] 건물 윤곽 지우기 — 3D 로 보내기 실패(격리): {ex.Message}"); }
            SetAimStatus("네이티브 창 3D 에서는 건물 높이 판을 쓸 수 없습니다", autoHide: true);
            return;
        }
        if (_bhBuilding is { } old)
        {
            if (old.Id == pick.Id && _bhJob == _map3DJob) return;   // 같은 건물 — 넣던 값 그대로
            if (IsBuildingHeightChanged) PostBuilding3D(Map3DBuilding.PreviewMessageJson(old.Id, null));
        }
        _bhDragStart = null;
        _bhBuilding = pick;
        _bhJob = _map3DJob;
        _bhPreview = pick.Height;
        _bhError = null;
        _bhInputError = false;
        _log?.Info($"[Map3D] 건물 고름 id={pick.Id} 높이={pick.Height} 출처={pick.HeightFrom ?? "-"} job={_map3DJob}");
        RefreshBuildingHeightPanel();
    }

    /// <summary>판 닫기 — 미리보기 원래대로 · 강조 지움. clearUndo = 3D 를 나갈 때(판 안 되돌리기 비움).</summary>
    private void CloseBuildingHeightPanel(bool clearUndo)
    {
        if (clearUndo) { BhUndo.Clear(); _bhSession++; }   // 3D 세션이 바뀐다 — 그 뒤 도착한 저장 응답은 칸에 쌓지 않는다
        if (_bhBuilding is not { } b) return;
        // 3D 로 보내기(WebView2)가 실패해도 판은 닫는다 — 3D 나가기 도중 예외로 나머지 복귀가 끊기지 않게
        try
        {
            if (IsBuildingHeightChanged) PostBuilding3D(Map3DBuilding.PreviewMessageJson(b.Id, null));
            PostBuilding3D(Map3DBuilding.SelectMessageJson(null));
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 「건물 높이」 판 닫기 — 3D 로 보내기 실패(격리): {ex.Message}"); }
        _bhBuilding = null;
        _bhDragStart = null;
        _bhError = null;
        _bhInputError = false;
        RefreshBuildingHeightPanel();
    }

    private void SetBuildingHeightPreview(double height)
    {
        if (_bhBuilding is not { } b) return;
        double h = Map3DBuilding.ClampHeight(height);
        _bhError = null;
        _bhInputError = false;
        if (Math.Abs(h - _bhPreview) > 1e-9)
        {
            _bhPreview = h;
            PostBuilding3D(Map3DBuilding.PreviewMessageJson(b.Id, Math.Abs(h - b.Height) > 1e-9 ? h : null));
        }
        RefreshBuildingHeightPanel();
    }

    private void SetBuildingHeightError(string? error)
    {
        _bhError = error;
        NotifyOfPropertyChange(nameof(BuildingHeightError));
        NotifyOfPropertyChange(nameof(HasBuildingHeightError));
    }

    /// <summary>입력칸 값이 잘못됐다 — 오류 줄 + [적용] 막기(바른 값이 들어오거나 판을 닫을 때까지).</summary>
    private void SetBuildingHeightInputError(string? error)
    {
        _bhInputError = true;
        SetBuildingHeightError(error);
        ApplyBuildingHeightCommand.RaiseCanExecuteChanged();
    }

    /// <summary>입력칸 글을 미리보기 값으로 덮고 판 전체를 다시 알린다.</summary>
    private void RefreshBuildingHeightPanel()
    {
        _bhHeightText = _bhBuilding == null ? "" : Map3DBuilding.FormatMeters(_bhPreview);
        _bhFloorsText = _bhBuilding == null ? "" : Map3DBuilding.FloorsFromHeight(_bhPreview, BhFloorHeight).ToString(System.Globalization.CultureInfo.InvariantCulture);
        _bhFloorHeightText = Map3DBuilding.FormatMeters(BhFloorHeight);
        foreach (var name in new[]
                 {
                     nameof(IsBuildingHeightPanelOpen), nameof(BuildingHeightBuilding), nameof(BuildingHeightPreview), nameof(BuildingHeightCurrentText),
                     nameof(BuildingHeightSourceText), nameof(IsBuildingHeightReadOnly), nameof(IsBuildingHeightEditable), nameof(IsBuildingHeightChanged),
                     nameof(BuildingHeightSummary), nameof(BuildingHeightFloorsText), nameof(BuildingHeightText), nameof(BuildingHeightFloorHeightText),
                     nameof(BuildingHeightError), nameof(HasBuildingHeightError), nameof(BuildingHeightUndoCount),
                 })
            NotifyOfPropertyChange(name);
        StepBuildingFloorCommand.RaiseCanExecuteChanged();
        ApplyBuildingHeightCommand.RaiseCanExecuteChanged();
        ResetBuildingHeightCommand.RaiseCanExecuteChanged();
        UndoBuildingHeightCommand.RaiseCanExecuteChanged();
        CancelBuildingHeightCommand.RaiseCanExecuteChanged();
    }

    private void SetBuildingHeightBusy(bool busy)
    {
        _bhBusy = busy;
        RefreshBuildingHeightPanel();
    }

    // ── 저장 · 되돌리기(map-3d) ──

    private async Task ApplyBuildingHeightAsync()
    {
        if (_bhBuilding is not { } b || _bhJob is not { } job || _bhBusy || !IsBuildingHeightChanged) return;
        if (_bhInputError) return;   // 잘못된 입력이 남았다 — 앞 미리보기 값을 저장하지 않는다(오류 줄 그대로)
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        double target = _bhPreview;
        int floors = Map3DBuilding.FloorsFromHeight(target, BhFloorHeight);
        // 직접 그린 건물(origin user)은 자동 높이가 없다 — 되돌리기는 "자동으로"가 아니라 앞 높이로 다시 저장
        bool keep = b.Manual || b.Origin == "user";
        await SaveBuildingHeightAsync(job, b.Id, target, floors, new BuildingHeightUndoStep(job, b.Id, keep ? b.Height : null, keep ? b.Levels : null),
                                      () => Map3DBuilding.SavedText(target, floors));
    }

    private async Task ResetBuildingHeightAsync()
    {
        if (_bhBuilding is not { Manual: true } b || _bhJob is not { } job || _bhBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        await SaveBuildingHeightAsync(job, b.Id, null, null, new BuildingHeightUndoStep(job, b.Id, b.Height, b.Levels),
                                      () => "자동으로 잰 높이로 되돌렸습니다");
    }

    private async Task UndoBuildingHeightAsync()
    {
        if (_bhBusy || _bhJob is not { } job) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        int index = BhUndo.FindLastIndex(u => u.Job == job);
        if (index < 0) return;
        var step = BhUndo[index];
        BhUndo.RemoveAt(index);
        int session = _bhSession;
        bool ok = await SaveBuildingHeightAsync(step.Job, step.Id, step.Before, step.BeforeFloors, null, () => "되돌렸습니다");
        if (!ok && session == _bhSession) BhUndo.Insert(Math.Min(index, BhUndo.Count), step);   // 실패면 다시 할 수 있게 남긴다(그 사이 3D 를 나갔으면 비운 채로)
        RefreshBuildingHeightPanel();
    }

    /// <summary>
    /// 저장(height) 또는 자동으로(null) — 서버 응답 줄을 3D 에 보내고(building.commit) 판의 저장된 값을 바꾼다. 성공하면 undo 칸을 쌓는다.
    /// 실패는 판 오류 줄에(미리보기는 그대로 — 다시 [적용] 할 수 있게).
    /// </summary>
    private async Task<bool> SaveBuildingHeightAsync(string job, int id, double? height, int? floors, BuildingHeightUndoStep? undo, Func<string> done)
    {
        if (_map3DClient == null) return false;
        _bhError = null;   // 앞 실패의 이유 줄은 새 시도에서 지운다
        _bhInputError = false;
        int session = _bhSession;
        SetBuildingHeightBusy(true);
        try
        {
            var saved = height is double h
                ? await _map3DClient.SetBuildingHeightAsync(job, id, h, floors)
                : await _map3DClient.ResetBuildingHeightAsync(job, id);
            // 응답 전에 3D 를 나갔으면(칸을 비웠다) 쌓지 않는다 — 다음 3D 에서 앞 세션 저장을 되돌리지 않게. 서버 저장 · 3D commit 은 그대로
            if (undo != null && session == _bhSession) BhUndo.Add(undo);
            else if (undo != null) _log?.Info($"[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음 id={id} job={job}");
            OnBuildingHeightSaved(job, saved);
            _log?.Info($"[Map3D] 건물 높이 저장 id={id} → {saved.Pick.Height} ({saved.Pick.HeightFrom ?? "-"}) job={job}");
            SetAimStatus(done(), autoHide: true);
            _ = RefreshMap3DAreasAsync();   // 영역 목록 「직접 입력한 건물 높이 n개」
            return true;
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 건물 높이 저장 — 서버 닿지 않음: {ex.Message}");
            SetBuildingHeightError(Map3DUnavailableText);
            NoteMap3DEngineDown("building-height");
        }
        catch (Map3DRequestException ex) { SetBuildingHeightError($"저장하지 못했습니다: {ex.Message}"); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 건물 높이 저장 실패: {ex.Message}");
            SetBuildingHeightError("저장하지 못했습니다.");
        }
        finally { SetBuildingHeightBusy(false); }
        return false;
    }

    /// <summary>서버가 고친 건물 줄 — 3D 에 저장된 모양으로 알리고, 판이 그 건물이면 저장된 값 · 미리보기를 서버 값으로.</summary>
    private void OnBuildingHeightSaved(string job, Map3DBuildingSaved saved)
    {
        if (Map3DHost.Job == job) PostBuilding3D(Map3DBuilding.CommitMessageJson(saved.Building));
        if (_bhBuilding?.Id == saved.Pick.Id && _bhJob == job)
        {
            _bhBuilding = saved.Pick;
            _bhPreview = saved.Pick.Height;
        }
        RefreshBuildingHeightPanel();
    }

    // ── 판 컨트롤(IBuildingHeightPanelHost) ──

    void IBuildingHeightPanelHost.BeginHeightDrag()
    {
        if (IsBuildingHeightEditable) _bhDragStart = _bhPreview;
    }

    void IBuildingHeightPanelHost.DragHeight(double totalDy)
    {
        if (_bhDragStart is double start && IsBuildingHeightEditable) SetBuildingHeightPreview(Map3DBuilding.DragHeight(start, totalDy));
    }

    void IBuildingHeightPanelHost.EndHeightDrag(bool canceled)
    {
        if (_bhDragStart is double start && canceled) SetBuildingHeightPreview(start);
        _bhDragStart = null;
    }

    bool IBuildingHeightPanelHost.StepHeightByKey(bool up, bool shift)
    {
        if (!IsBuildingHeightEditable) return false;
        SetBuildingHeightPreview(Map3DBuilding.KeyStep(_bhPreview, BhFloorHeight, up, shift));
        return true;
    }

    bool IBuildingHeightPanelHost.UndoHeight()
    {
        if (!UndoBuildingHeightCommand.CanExecute(null)) return false;
        UndoBuildingHeightCommand.Execute(null);
        return true;
    }

    /// <summary>지도 창 Esc(TryHandleMap3DKey) — 판이 열려 있으면 3D 를 나가지 않고 판만: 끄는 중이면 끌기 취소, 아니면 판 취소.</summary>
    private bool TryCancelBuildingHeightByEscape()
    {
        if (!IsBuildingHeightPanelOpen) return false;
        if (_bhPanel?.CancelHandleDrag() == true) return true;
        if (_bhDragStart != null) { ((IBuildingHeightPanelHost)this).EndHeightDrag(canceled: true); return true; }
        CancelBuildingHeightCommand.Execute(null);   // 저장 중이면 닫지 않고 알린다(3D 도 나가지 않는다)
        return true;
    }

    // ── 3D 영역 목록 「모두 자동으로…」 ──

    /// <summary>「모두 자동으로…」 — 표준 확인 팝업(EventAggregator). "확인" → HandleAsync(CallResetMap3DHeightsProcessMessageModel).</summary>
    private async Task RequestResetMap3DHeightsAsync(Map3DAreaItem item)
    {
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        try
        {
            _pendingMap3DHeightsReset = item;
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
            {
                Title = "건물 높이 모두 자동으로",
                Explain = $"3D 영역 '{item.Title}' 의 {Map3DBuilding.ManualCountText(item.Scene.ManualHeights)}를 모두 지우고 자동으로 잰 높이로 되돌리시겠습니까?\n"
                          + "다시 만들어도 직접 입력이 다시 붙지 않습니다.",
                MessageModel = new CallResetMap3DHeightsProcessMessageModel(),
            });
        }
        catch (Exception ex) { _log?.Error($"[Map3D] 건물 높이 모두 자동으로 확인 창 실패: {ex.Message}"); }
    }

    /// <summary>「모두 자동으로」 확인 콜백 — 서버에서 지우고, 그 장면을 띄워 둔 3D 에는 되돌린 줄을 바로 반영한다.</summary>
    public async Task HandleAsync(CallResetMap3DHeightsProcessMessageModel message, CancellationToken cancellationToken)
    {
        var item = _pendingMap3DHeightsReset;
        _pendingMap3DHeightsReset = null;
        try
        {
            if (item == null || _map3DClient == null) return;
            if (_bhJob == item.Id) CloseBuildingHeightPanel(clearUndo: false);
            var rows = await _map3DClient.ResetAllBuildingHeightsAsync(item.Id, cancellationToken);
            BhUndo.RemoveAll(u => u.Job == item.Id);   // 지운 값으로 되돌리지 않게
            foreach (var row in rows) OnBuildingHeightSaved(item.Id, row);
            _log?.Info($"[Map3D] 건물 높이 모두 자동으로 id={item.Id} 되돌림={rows.Count}");
            SetAimStatus($"건물 {rows.Count}개를 자동으로 잰 높이로 되돌렸습니다", autoHide: true);
            _ = RefreshMap3DAreasAsync();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Map3D] 건물 높이 모두 자동으로 — 서버 닿지 않음: {ex.Message}");
            SetAimStatus(Map3DUnavailableText, autoHide: true);
            NoteMap3DEngineDown("building-height-reset");
        }
        catch (Map3DRequestException ex) { SetAimStatus($"건물 높이를 되돌릴 수 없습니다: {ex.Message}", autoHide: true); }
        catch (Exception ex)
        {
            _log?.Error($"[Map3D] 건물 높이 모두 자동으로 실패: {ex.Message}");
            SetAimStatus("건물 높이 되돌리기에 실패했습니다.", autoHide: true);
        }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        }
    }

    /// <summary>다시 굽기에서 못 붙인 직접 입력 — 영역마다 그 수가 처음 보일 때(또는 바뀔 때) 한 번 알린다.</summary>
    private Dictionary<string, int>? _map3DUnmatchedSeen;
    private Dictionary<string, int> Map3DUnmatchedNotified => _map3DUnmatchedSeen ??= new();

    private void NotifyMap3DUnmatchedHeights(IEnumerable<Map3DAreaItem> items)
    {
        foreach (var item in items)
        {
            int n = item.Scene.ManualUnmatched;
            if (n <= 0 || (Map3DUnmatchedNotified.TryGetValue(item.Id, out var seen) && seen == n)) continue;
            Map3DUnmatchedNotified[item.Id] = n;
            _log?.Warning($"[Map3D] 다시 붙이지 못한 직접 입력 id={item.Id} n={n}");
            SetAimStatus(Map3DBuilding.UnmatchedText(item.Title, n), autoHide: true);
        }
    }
}
