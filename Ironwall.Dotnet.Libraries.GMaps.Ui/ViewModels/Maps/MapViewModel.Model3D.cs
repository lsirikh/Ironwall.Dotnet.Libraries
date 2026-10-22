using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Map3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo.Commands;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : 「꾸미기」 판의 넣은 모델(2026-10-22 사용자 요청 — "3D 로 나오는 모습을 … 다른 곳에서 주입", 엔진 8cacc6e models.py) —
                  [모델 넣기…](GLB · OBJ · FBX 고르기 → POST …/models) · 목록 · 고르기 · 이름 · 지우기 · 되돌리기(GIS 편집 기록, ModelCommand).
   Note         : ★ 자리 · 방향 · 크기는 3D 손잡이(edit.start target kind "model" — 옮기기 원판 · 돌리기 원호 · 크기 고리)와 판 단추(edit.key ↑↓←→ · [ ] · + −)로,
                    edit.commit(geometry Point · heading_deg · scale)을 PUT …/models/{uid} 로 저장한다(MapViewModel.Edit3D). 2D 지도에는 모델을 그리지 않는다.
                  ★ 꾸미기와 같은 판 · 같은 규칙: 편집 모드 · 지도 편집 권한 · 3D 에서 누르면(model.pick) 3D 에 머문 채 판이 열리고 그 모델을 고른다.
                    꾸미기 항목 고름 · 도구와 모델 고름은 서로 배타(3D 손잡이는 하나).
                  ★ 지운 모델을 되돌리려면 파일이 있어야 한다 — 지우기 전에 원본(GET …/file)을 받아 편집 기록에 둔다. 다시 올리면 엔진이 새 uid 를 주므로
                    옛 uid → 새 uid 를 기억해(ResolveModelUid) 앞뒤 기록이 같은 모델을 가리키게 한다.
                  ★ 엔진이 옛 판(GET …/models 404)이면 "엔진이 옛 판이라 모델 넣기를 쓸 수 없습니다" + [모델 넣기…] 끔. 실패해도 GIS 는 멈추지 않는다(판에 이유 한 줄).
   Created On   : 2026-10-22 · Sensorway Co., Ltd.
 ****************************************************************************/
public partial class MapViewModel
{
    private IReadOnlyList<Map3DModelItem>? _modelItemList;
    private string? _modelSelectedUid, _modelPendingSelect, _modelStatus;
    private string _modelName = "";
    private bool _modelUnsupported, _modelBusy;
    private Dictionary<string, string>? _modelUidAliasMap;
    /// <summary>되살리기로 바뀐 uid(옛 → 새) — 지연 초기화(생성자 없는 시험 VM).</summary>
    private Dictionary<string, string> ModelUidAlias => _modelUidAliasMap ??= new();
    /// <summary>파일 고르기(시험이 바꿔 끼운다) — 없으면 열기 대화상자.</summary>
    private Func<string?>? _modelFilePicker;

    // ── 바인딩 ──

    public IReadOnlyList<Map3DModelItem> ModelItems => _modelItemList ?? Array.Empty<Map3DModelItem>();
    private Map3DModelItem? ModelSelected => ModelItems.FirstOrDefault(m => m.Uid == _modelSelectedUid);
    public bool HasModelSelection => ModelSelected != null;
    public string ModelCountText => Map3DModels.CountText(ModelItems.Count);
    public string? ModelStatusText => _modelUnsupported ? Map3DModels.OldEngineText : _modelStatus;
    public bool HasModelStatus => ModelStatusText != null;

    /// <summary>판 목록에서 고른 모델(uid) — 고르면 3D 강조(model.select) · 3D 위 판이면 손잡이.</summary>
    public string? ModelSelectedUid
    {
        get => _modelSelectedUid;
        set { if (value != _modelSelectedUid) SelectModel(value); }
    }

    /// <summary>고른 모델 이름 칸([이름 저장] 으로 저장).</summary>
    public string ModelName
    {
        get => _modelName;
        set { _modelName = value ?? ""; NotifyOfPropertyChange(); RenameModelCommand.RaiseCanExecuteChanged(); }
    }

    private RelayCommand? _addModelCommand;
    /// <summary>[모델 넣기…] — 파일을 골라 화면 가운데(영역 밖이면 영역 가운데)에 넣는다.</summary>
    public RelayCommand AddModelCommand => _addModelCommand ??= new RelayCommand(_ => _ = AddModelAsync(), _ => IsDecorating && !_modelUnsupported && !_modelBusy);

    private RelayCommand? _renameModelCommand;
    public RelayCommand RenameModelCommand => _renameModelCommand ??= new RelayCommand(
        _ => _ = RenameModelAsync(), _ => ModelSelected is { } m && !_modelBusy && (m.Name ?? "") != _modelName.Trim());

    private RelayCommand? _deleteModelCommand;
    public RelayCommand DeleteModelCommand => _deleteModelCommand ??= new RelayCommand(_ => _ = DeleteModelAsync(), _ => ModelSelected != null && !_modelBusy);

    // ── 판 세션(꾸미기 시작 · 끝에 함께) ──

    /// <summary>꾸미기 시작 — 목록을 새로 받는다.</summary>
    private void StartModelSession(string job)
    {
        _modelItemList = null;
        _modelSelectedUid = _modelPendingSelect = _modelStatus = null;
        _modelName = "";
        _modelUnsupported = false;
        _ = LoadModelsAsync(job);
    }

    /// <summary>꾸미기 끝 — 3D 강조를 지우고 비운다(손잡이는 ExitDecoration 의 EndEdit3D 가 내린다).</summary>
    private void EndModelSession(string job)
    {
        if (_modelSelectedUid != null) PostUserBuilding3D(job, Map3DModels.SelectMessageJson(null));
        _modelItemList = null;
        _modelSelectedUid = _modelPendingSelect = _modelStatus = null;
        _modelName = "";
        _modelUnsupported = false;
    }

    private async Task LoadModelsAsync(string job)
    {
        if (_map3DClient == null) return;
        try
        {
            var items = await _map3DClient.ListModelsAsync(job);
            if (_decoArea?.Id != job) return;
            _modelUnsupported = items == null;
            _modelItemList = items ?? Array.Empty<Map3DModelItem>();
            if (_modelUnsupported) _log?.Warning($"[Model3D] 옛 엔진(404) job={job}");
            if (_modelPendingSelect is { } pending) { _modelPendingSelect = null; SelectModel(ModelItems.Any(m => m.Uid == pending) ? pending : null); }
            else if (_modelSelectedUid != null && ModelSelected == null) SelectModel(null);   // 없어진 모델은 고름에서 뺀다
            RefreshDecorationPanel();
        }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Model3D] 목록 — 서버 닿지 않음: {ex.Message}");
            SetModelStatus(Map3DUnavailableText);
            NoteMap3DEngineDown("model-list");
        }
        catch (Exception ex) { _log?.Warning($"[Model3D] 목록 실패: {ex.Message}"); }
    }

    /// <summary>모델 고르기(null = 풀기) — 꾸미기 항목 고름 · 도구는 내려놓는다(3D 손잡이는 하나).</summary>
    private void SelectModel(string? uid)
    {
        var item = ModelItems.FirstOrDefault(m => m.Uid == uid);
        if (item != null)
        {
            if (_decoTool != null) SetDecorationTool(null);
            if (_decoSelectedUid != null) SelectDecoration(null);
        }
        _modelSelectedUid = item?.Uid;
        _modelName = item?.Name ?? "";
        if (_decoArea is { } a) PostUserBuilding3D(a.Id, Map3DModels.SelectMessageJson(item?.Uid));
        if (_decoIn3D)
        {
            if (item != null) StartEdit3D(Map3DEdit.TargetModel, item.Uid);
            else if (_edit3DTarget?.Kind == Map3DEdit.TargetModel) StopEdit3D();
        }
        RefreshDecorationPanel();
    }

    /// <summary>꾸미기 항목 · 도구를 고를 때 모델 고름만 푼다(3D 손잡이는 그쪽이 다시 세운다).</summary>
    private void ClearModelSelection()
    {
        if (_modelSelectedUid == null) return;
        _modelSelectedUid = null;
        _modelName = "";
        if (_decoArea is { } a) PostUserBuilding3D(a.Id, Map3DModels.SelectMessageJson(null));
    }

    /// <summary>
    /// 3D 에서 넣은 모델을 눌렀다 — 그 영역에서 「꾸미기」를 3D 위 판으로 열고(꾸미기 항목 고름과 같은 규칙) 목록을 받으면 그 모델을 고른다.
    /// 보기 전용(편집 모드 꺼짐)이면 판 없이 강조만 지운다.
    /// </summary>
    private void OnMap3DModelPicked(string uid)
    {
        if (_map3DState != Map3DState.Shown || _map3DJob is not { } job || Map3DAreas.FirstOrDefault(a => a.Id == job) is not { } area) return;
        if (!RequireMapEditMode(notify: false))
        {
            _log?.Info($"[Model3D] 3D 에서 고름 — 편집 모드 아님(무시) uid={uid}");
            PostUserBuilding3D(job, Map3DModels.SelectMessageJson(null));
            return;
        }
        _log?.Info($"[Model3D] 3D 에서 고름 uid={uid} job={job}");
        CloseBuildingHeightPanel(clearUndo: false);
        bool in3D = ShouldEditIn3D;
        bool same = in3D && _decoIn3D && _decoArea?.Id == job;
        if (!same) StartDecoration(area, in3D);
        if (_decoArea?.Id != job) return;
        if (ModelItems.Any(m => m.Uid == uid)) SelectModel(uid);
        else
        {
            _modelPendingSelect = uid;
            if (same) _ = LoadModelsAsync(job);
        }
    }

    // ── 넣기 · 고치기 · 지우기 ──

    private async Task AddModelAsync()
    {
        if (_decoArea is not { } area || _modelBusy || _modelUnsupported || _map3DClient == null) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        string? path;
        try { path = (_modelFilePicker ?? ShowModelFileDialog)(); }
        catch (Exception ex) { _log?.Error($"[Model3D] 파일 고르기 실패(격리): {ex.Message}"); return; }
        if (string.IsNullOrEmpty(path)) return;
        byte[] data;
        try
        {
            if (Map3DModels.CheckFile(path, new FileInfo(path).Length) is { } why) { SetModelStatus(why); return; }
            data = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Warning($"[Model3D] 파일 읽기 실패: {ex.Message}");
            SetModelStatus("파일을 읽지 못했습니다 — 다른 프로그램이 쓰고 있는지 확인하세요");
            return;
        }
        // 화면 가운데가 영역 안이면 거기(3D 도 같은 자리를 본다), 아니면 엔진이 영역 가운데에 둔다
        double? lon = null, lat = null;
        if (MainMap?.Position is { } c && area.Area.Contains(c.Lng, c.Lat)) { lon = Math.Round(c.Lng, 7); lat = Math.Round(c.Lat, 7); }
        string fileName = Path.GetFileName(path);
        bool convert = !fileName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
        var made = await RunModelAsync(convert ? "모델을 GLB 로 바꿔 넣는 중…(OBJ · FBX 는 1~3분 걸릴 수 있습니다)" : "모델을 넣는 중…",
                                       () => _map3DClient.UploadModelAsync(area.Id, data, fileName, lon, lat));
        if (made == null) return;
        _log?.Info($"[Model3D] 넣음 uid={made.Uid} file={fileName} {data.Length}B job={area.Id}");
        _undoService?.Push(new ModelCommand($"{Map3DModels.UndoPrefix} 넣기", area.Id, made.Uid, null, new Map3DModelState(made, fileName, data),
                                            ApplyModelStateAsync, ResolveUserBuildingJob));
        await AfterModelChangedAsync(area.Id, made.Uid);
        SetModelStatus(_decoIn3D ? Map3DModels.PlaceHintText : "넣었습니다 — 3D 로 보며 손잡이로 자리 · 방향 · 크기를 맞추세요");
    }

    private async Task RenameModelAsync()
    {
        if (_decoArea is not { } area || ModelSelected is not { } before || _modelBusy) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        var after = before with { Name = string.IsNullOrWhiteSpace(_modelName) ? null : _modelName.Trim() };
        if (await RunModelAsync(null, () => _map3DClient!.PutModelAsync(area.Id, after)) is not { } saved) return;
        _undoService?.Push(new ModelCommand($"{Map3DModels.UndoPrefix} 이름", area.Id, before.Uid, new Map3DModelState(before, "", null),
                                            new Map3DModelState(saved, "", null), ApplyModelStateAsync, ResolveUserBuildingJob));
        await AfterModelChangedAsync(area.Id, saved.Uid);
        SetAimStatus("모델 이름을 저장했습니다", autoHide: true);
    }

    private async Task DeleteModelAsync()
    {
        if (_decoArea is not { } area || ModelSelected is not { } before || _modelBusy || _map3DClient == null) return;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return; }
        // 되돌리기로 되살리려면 원본이 있어야 한다 — 지우기 전에 받아 둔다(못 받으면 지우되 되돌리기 기록은 남기지 않는다)
        byte[]? data = await RunModelAsync("모델을 지우는 중…", () => _map3DClient.GetModelFileAsync(area.Id, before.Uid), keepBusy: true);
        if (await RunModelAsync(null, async () => (object?)await _map3DClient.DeleteModelAsync(area.Id, before.Uid)) is null) return;
        if (data != null)
            _undoService?.Push(new ModelCommand($"{Map3DModels.UndoPrefix} 지우기", area.Id, before.Uid, new Map3DModelState(before, (before.Name ?? "model") + ".glb", data),
                                                null, ApplyModelStateAsync, ResolveUserBuildingJob));
        _log?.Info($"[Model3D] 지움 uid={before.Uid} 되돌리기={(data != null)} job={area.Id}");
        await AfterModelChangedAsync(area.Id, null);
        SetModelStatus(data != null ? null : "지웠습니다 — 원본을 받지 못해 되돌릴 수 없습니다");
        SetAimStatus("모델을 지웠습니다", autoHide: true);
    }

    /// <summary>3D 손잡이 · 판 단추로 고친 자리 · 방향 · 크기 저장(edit.commit target kind "model"). 실패면 false(호출자가 edit.revert).</summary>
    private async Task<bool> CommitModelPlaceAsync(Map3DEditCommit c)
    {
        if (!_decoIn3D || _decoArea is not { } area || ModelItems.FirstOrDefault(m => m.Uid == c.Uid) is not { } before || c.Points.Count != 1) return false;
        if (!CanEditMap()) { ShowNoMapEditPermissionInfo(); return false; }
        var p = c.Points[0];
        if (!area.Area.Contains(p.X, p.Y)) { SetModelStatus(Map3DUserBuilding.OutsideText); return false; }
        var after = before with { Lon = p.X, Lat = p.Y, HeadingDeg = c.HeadingDeg ?? before.HeadingDeg, Scale = c.Scale ?? before.Scale };
        if (await RunModelAsync(null, () => _map3DClient!.PutModelAsync(area.Id, after)) is not { } saved)
        {
            var error = _modelStatus;
            if (_decoArea?.Id == area.Id) await LoadModelsAsync(area.Id);   // 서버 것으로
            SetModelStatus(error);
            return false;
        }
        _undoService?.Push(new ModelCommand($"{Map3DModels.UndoPrefix} 옮기기", area.Id, c.Uid, new Map3DModelState(before, "", null),
                                            new Map3DModelState(saved, "", null), ApplyModelStateAsync, ResolveUserBuildingJob));
        await AfterModelChangedAsync(area.Id, saved.Uid);
        SetAimStatus("모델을 옮겼습니다", autoHide: true);
        return true;
    }

    /// <summary>
    /// GIS 편집 기록 적용 — 그 상태로 다시 저장(null = 지움). 지금 없는 모델이면 상태의 파일을 다시 올리고 새 uid 를 기억한다.
    /// 실패는 판에 이유를 남기고 예외(편집 기록이 그 칸을 남긴다).
    /// </summary>
    private async Task ApplyModelStateAsync(string job, string uid, Map3DModelState? state, CancellationToken ct)
    {
        if (_map3DClient == null) throw new InvalidOperationException("3D 서버 설정이 없습니다.");
        string real = ResolveModelUid(uid);
        string? keep;
        try
        {
            if (state == null)
            {
                await _map3DClient.DeleteModelAsync(job, real, ct);
                keep = null;
            }
            else
            {
                var now = await _map3DClient.ListModelsAsync(job, ct) ?? throw new Map3DRequestException(System.Net.HttpStatusCode.NotFound, Map3DModels.OldEngineText);
                if (now.Any(m => m.Uid == real)) keep = (await _map3DClient.PutModelAsync(job, state.Item with { Uid = real }, ct)).Uid;
                else if (state.Data is { } data)
                {
                    var s = state.Item;
                    var made = await _map3DClient.UploadModelAsync(job, data, state.FileName, s.Lon, s.Lat, s.AltM, s.HeadingDeg, s.Scale, s.Name, ct);
                    ModelUidAlias[uid] = made.Uid;
                    if (real != uid) ModelUidAlias[real] = made.Uid;
                    keep = made.Uid;
                }
                else throw new InvalidOperationException("그 모델이 없어 되돌릴 수 없습니다");
            }
        }
        catch (Exception ex) when (ex is Map3DRequestException or Map3DServerUnavailableException or InvalidOperationException)
        {
            SetModelStatus(ex is Map3DServerUnavailableException ? Map3DUnavailableText : $"되돌리지 못했습니다: {ex.Message}");
            throw;
        }
        _log?.Info($"[Model3D] 기록 적용 uid={uid}→{keep ?? "(지움)"} job={job}");
        await AfterModelChangedAsync(job, _modelSelectedUid == real ? keep : _modelSelectedUid);
    }

    /// <summary>되살리기로 바뀐 uid 를 끝까지 따라간다.</summary>
    private string ResolveModelUid(string uid)
    {
        for (int i = 0; i < 32 && ModelUidAlias.TryGetValue(uid, out var next); i++) uid = next;
        return uid;
    }

    /// <summary>저장 뒤 — 3D 다시 그리기(models.reload) · 목록 다시 받기 · 고름(select = 고를 uid, null = 풀기).</summary>
    private async Task AfterModelChangedAsync(string job, string? select)
    {
        PostUserBuilding3D(job, Map3DModels.ReloadMessageJson());
        if (_decoArea?.Id != job) return;
        _modelPendingSelect = select;
        if (select == null && _modelSelectedUid != null) SelectModel(null);
        await LoadModelsAsync(job);
    }

    /// <summary>
    /// 엔진 호출 한 번 — 바쁨 표시 · 오류를 판 한 줄로(닿지 않음 · 옛 엔진 · 엔진 문구). 실패면 null. keepBusy = 이어서 또 부를 때(바쁨을 풀지 않음).
    /// </summary>
    private async Task<T?> RunModelAsync<T>(string? progress, Func<Task<T>> call, bool keepBusy = false) where T : class
    {
        _modelBusy = true;
        _modelStatus = progress;
        RefreshDecorationPanel();
        try { return await call(); }
        catch (Map3DServerUnavailableException ex)
        {
            _log?.Warning($"[Model3D] 서버 닿지 않음: {ex.Message}");
            _modelStatus = ex.IsTimeout ? "엔진 응답이 너무 늦습니다 — 잠시 뒤 목록을 확인하세요" : Map3DUnavailableText;
            if (!ex.IsTimeout) NoteMap3DEngineDown("model");
        }
        catch (Map3DRequestException ex) when (ex.Message is "Not Found" or "Method Not Allowed")   // 옛 map-3d — 그 주소가 없다
        {
            _modelUnsupported = true;
            _modelStatus = null;
        }
        catch (Map3DRequestException ex) { _modelStatus = ex.Message; }   // 형식 · 크기 · 삼각형 · 개수 상한 · 영역 밖 — 엔진의 쉬운 말
        catch (Exception ex)
        {
            _log?.Error($"[Model3D] 실패(격리): {ex.Message}");
            _modelStatus = "모델을 저장하지 못했습니다.";
        }
        finally
        {
            if (!keepBusy || _modelStatus != progress) _modelBusy = false;
            RefreshDecorationPanel();
        }
        return null;
    }

    private void SetModelStatus(string? text)
    {
        _modelStatus = text;
        _modelBusy = false;
        RefreshDecorationPanel();
    }

    private static string? ShowModelFileDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "넣을 3D 모델 고르기", Filter = Map3DModels.FileFilter, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void RefreshModelPanel()
    {
        foreach (var name in new[] { nameof(ModelItems), nameof(ModelSelectedUid), nameof(HasModelSelection), nameof(ModelCountText), nameof(ModelStatusText),
                                     nameof(HasModelStatus), nameof(ModelName) })
            NotifyOfPropertyChange(name);
        foreach (var c in new[] { AddModelCommand, RenameModelCommand, DeleteModelCommand }) c.RaiseCanExecuteChanged();
    }
}
