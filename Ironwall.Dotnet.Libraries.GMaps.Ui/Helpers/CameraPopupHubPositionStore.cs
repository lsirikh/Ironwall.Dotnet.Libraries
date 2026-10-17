using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : ICameraPopupHubPositionStore GMapDb 구현 (CameraPopup_ControlHub FR-08)
   Created By   : Claude Code
   Created On   : 2026-07-27
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="ICameraPopupHubPositionStore"/> 의 GMapDb(MariaDB) 구현 — <see cref="CameraPopupPositionStore"/> 패턴 답습.
/// <see cref="SemaphoreSlim"/> 직렬화 + 인메모리 캐시(낙관적). DB 실패/오프라인 시 인메모리로 유지(UI 비블로킹, NFR-02).
/// </summary>
public sealed class CameraPopupHubPositionStore : ICameraPopupHubPositionStore
{
    private readonly IGMapDbService _db;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private (double X, double Y)? _cache;
    private bool _loaded;

    public CameraPopupHubPositionStore(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public async Task<(double X, double Y)?> TryGetAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_loaded) return _cache;   // 1회 로드 후 인메모리(허브는 세션 내 자기 드래그로만 갱신)
            try
            {
                var dto = await _db.GetCameraPopupHubPositionAsync(token).ConfigureAwait(false);
                _cache = dto == null ? null : (dto.X, dto.Y);
            }
            catch (Exception ex)
            {
                _log?.Warning($"[CameraPopupHubPositionStore] 조회 실패(기본 위치 사용): {ex.Message}");
                _cache = null;
            }
            _loaded = true;
            return _cache;
        }
        finally { _gate.Release(); }
    }

    public async Task SaveAsync(double x, double y, CancellationToken token = default)
    {
        // 인메모리 즉시 반영(낙관적) — DB 실패해도 세션 내 위치 유지
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { _cache = (x, y); _loaded = true; }
        finally { _gate.Release(); }

        try
        {
            await _db.UpsertCameraPopupHubPositionAsync(x, y, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.Warning($"[CameraPopupHubPositionStore] 저장 실패(인메모리 유지): {ex.Message}");
        }
    }
}
