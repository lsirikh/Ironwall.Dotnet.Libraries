using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Monitoring.Models.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>
/// 카메라별 RTSP 팝업 위치(Geo 앵커) 영속 추상화.
/// 저장소 교체점(DB↔로컬) 격리 — 현재 구현은 GMapDb(MariaDB) 위임 + 인메모리 폴백.
/// </summary>
public interface ICameraPopupPositionStore
{
    /// <summary>앱 시작 시 전체 위치를 인메모리로 미리 적재(선택).</summary>
    Task PreloadAsync(CancellationToken token = default);

    /// <summary>카메라 Id로 저장된 팝업 앵커 위치를 조회. 없으면 null(→ 최초 위치=우상단 기본).</summary>
    Task<ICameraPopupPositionModel?> TryGetPositionAsync(int cameraId, CancellationToken token = default);

    /// <summary>드래그 완료 위치(팝업 좌상단 위경도)를 카메라별로 저장(Upsert).</summary>
    Task SavePositionAsync(int cameraId, PointLatLng anchorGeo, CancellationToken token = default);

    /// <summary>카메라의 저장된 팝업 위치를 삭제.</summary>
    Task<bool> RemoveAsync(int cameraId, CancellationToken token = default);
}
