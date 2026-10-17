using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>
/// <see cref="ICameraPopupPositionStore"/> 의 DB 구현 — GMapDb(MariaDB) CameraPopupPositions 위임.
/// SemaphoreSlim 직렬화 + 인메모리 캐시(폴백). DB 실패/오프라인 시 인메모리로 유지(UI 비블로킹).
/// </summary>
public sealed class CameraPopupPositionStore : ICameraPopupPositionStore
{
    private readonly IGMapDbService _db;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<int, CameraPopupPositionModel> _cache = new();

    public CameraPopupPositionStore(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public async Task PreloadAsync(CancellationToken token = default)
    {
        try
        {
            var rows = await _db.FetchCameraPopupPositionsAsync(token).ConfigureAwait(false);
            if (rows == null) return;

            await _gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                _cache.Clear();
                foreach (var r in rows)
                    _cache[r.CameraId] = new CameraPopupPositionModel(r);
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopupPositionStore] Preload 실패(무시): {ex.Message}");
        }
    }

    public async Task<ICameraPopupPositionModel?> TryGetPositionAsync(int cameraId, CancellationToken token = default)
    {
        // 인메모리 우선
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(cameraId, out var cached))
                return cached;
        }
        finally { _gate.Release(); }

        try
        {
            var model = await _db.GetCameraPopupPositionAsync(cameraId, token).ConfigureAwait(false);
            if (model != null)
            {
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try { _cache[cameraId] = new CameraPopupPositionModel(model); }
                finally { _gate.Release(); }
            }
            return model;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopupPositionStore] 조회 실패(폴백) CameraId={cameraId}: {ex.Message}");
            return null;
        }
    }

    public async Task SavePositionAsync(int cameraId, PointLatLng anchorGeo, CancellationToken token = default)
    {
        var model = new CameraPopupPositionModel(cameraId, anchorGeo.Lat, anchorGeo.Lng);

        // 인메모리 즉시 반영(낙관적) → DB 실패해도 세션 내 위치 유지
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { _cache[cameraId] = model; }
        finally { _gate.Release(); }

        try
        {
            await _db.UpsertCameraPopupPositionAsync(model, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopupPositionStore] 저장 실패(인메모리 유지) CameraId={cameraId}: {ex.Message}");
        }
    }

    public async Task<bool> RemoveAsync(int cameraId, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { _cache.Remove(cameraId); }
        finally { _gate.Release(); }

        try
        {
            return await _db.DeleteCameraPopupPositionAsync(cameraId, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopupPositionStore] 삭제 실패 CameraId={cameraId}: {ex.Message}");
            return false;
        }
    }
}
