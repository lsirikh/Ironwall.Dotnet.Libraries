using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Ptz;

/// <summary>
/// PTZ 프리셋 로컬 DB 스토어(CameraPopup_PTZ_Control). GMapDb(MariaDB) <c>CameraPtzPresets</c> 위임.
/// <see cref="Helpers.CameraPopupPositionStore"/> 패턴 답습 — DB 실패 시 빈 목록/false로 UI 비블로킹.
/// </summary>
public interface IPtzPresetStore
{
    /// <summary>카메라의 프리셋 목록(Home 우선·이름 순). 실패 시 빈 목록.</summary>
    Task<List<IPtzPresetModel>> GetPresetsAsync(int cameraId, CancellationToken token = default);
    /// <summary>프리셋 저장(Upsert, 동일 이름 덮어쓰기).</summary>
    Task<bool> SaveAsync(IPtzPresetModel preset, CancellationToken token = default);
    /// <summary>프리셋 삭제(Id).</summary>
    Task<bool> DeleteAsync(int presetId, CancellationToken token = default);
    /// <summary>Home 프리셋 지정(presetId만 IsHome=1).</summary>
    Task<bool> SetHomeAsync(int cameraId, int presetId, CancellationToken token = default);
}

/// <inheritdoc cref="IPtzPresetStore"/>
public sealed class PtzPresetStore : IPtzPresetStore
{
    private readonly IGMapDbService _db;
    private readonly ILogService? _log;

    public PtzPresetStore(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public async Task<List<IPtzPresetModel>> GetPresetsAsync(int cameraId, CancellationToken token = default)
    {
        try { return await _db.FetchPtzPresetsAsync(cameraId, token).ConfigureAwait(false) ?? new List<IPtzPresetModel>(); }
        catch (Exception ex) { _log?.Warning($"[PtzPresetStore] 조회 실패 cam={cameraId}: {ex.Message}"); return new List<IPtzPresetModel>(); }
    }

    public async Task<bool> SaveAsync(IPtzPresetModel preset, CancellationToken token = default)
    {
        try { return await _db.UpsertPtzPresetAsync(preset, token).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Error($"[PtzPresetStore] 저장 실패(cam={preset.CameraId}, {preset.PresetName}): {ex.Message}"); return false; }
    }

    public async Task<bool> DeleteAsync(int presetId, CancellationToken token = default)
    {
        try { return await _db.DeletePtzPresetAsync(presetId, token).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Error($"[PtzPresetStore] 삭제 실패(id={presetId}): {ex.Message}"); return false; }
    }

    public async Task<bool> SetHomeAsync(int cameraId, int presetId, CancellationToken token = default)
    {
        try { return await _db.SetHomePtzPresetAsync(cameraId, presetId, token).ConfigureAwait(false); }
        catch (Exception ex) { _log?.Error($"[PtzPresetStore] Home 설정 실패(cam={cameraId}, id={presetId}): {ex.Message}"); return false; }
    }
}
