using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>저장 결과의 갈래.</summary>
public enum FenceLayoutSaveStatus
{
    /// <summary>저장했다 — <see cref="FenceLayoutSaveResult.Revision"/> 이 새 판.</summary>
    Saved = 0,
    /// <summary>그사이 다른 GIS 가 먼저 저장했다 — 아무것도 쓰지 않았다(다시 불러와야 한다).</summary>
    Conflict = 1,
    /// <summary>저장소에 닿지 못했다 — 아무것도 쓰지 않았다.</summary>
    Failed = 2,
}

/// <summary>저장 한 번의 결과.</summary>
public sealed record FenceLayoutSaveResult(FenceLayoutSaveStatus Status, int Revision, string Message)
{
    public bool IsSaved => Status == FenceLayoutSaveStatus.Saved;
}

/// <summary>
/// 펜스 구성 로컬 저장소(fence-wiring-editor FR-11 · FR-16) — 제어기 id 가 키. 구현은 지도와 같은 로컬 DB(<c>GMaps.Db</c>)에 있고,
/// 결선 창은 이 인터페이스만 안다(없으면 로컬 저장 칸만 숨긴다).
/// </summary>
public interface IFenceLayoutStore
{
    /// <summary>그 제어기의 구성. 없으면 <c>null</c>. 읽지 못하면 예외 대신 <c>null</c> 을 돌려주고 로그를 남긴다.</summary>
    Task<FenceLayoutDocument?> LoadAsync(int controllerId, CancellationToken token = default);

    /// <summary>
    /// 저장한다 — <see cref="FenceLayoutDocument.Revision"/>(불러온 판)이 저장소의 판과 같을 때만 쓴다(아니면 <see cref="FenceLayoutSaveStatus.Conflict"/>).
    /// 처음 저장은 판 0.
    /// </summary>
    Task<FenceLayoutSaveResult> SaveAsync(FenceLayoutDocument document, CancellationToken token = default);
}
