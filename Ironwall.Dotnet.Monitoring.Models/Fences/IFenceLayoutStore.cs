using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>저장 결과의 갈래.</summary>
public enum FenceLayoutSaveStatus
{
    /// <summary>저장했다 — <see cref="FenceLayoutSaveResult.Revision"/> 이 새 판.</summary>
    Saved = 0,
    /// <summary>그사이 다른 GIS 가 먼저 저장했다(판이 어긋났다) — 아무것도 쓰지 않았다(다시 불러와야 한다).</summary>
    Conflict = 1,
    /// <summary>저장소에 닿지 못했다 — 아무것도 쓰지 않았다.</summary>
    Failed = 2,
}

/// <summary>저장 한 번의 결과.</summary>
public sealed record FenceLayoutSaveResult(FenceLayoutSaveStatus Status, int Revision, string Message)
{
    public bool IsSaved => Status == FenceLayoutSaveStatus.Saved;
}

/// <summary>저장 방식.</summary>
public enum FenceLayoutSaveMode
{
    /// <summary>불러온 판이 저장소의 판과 같을 때만 쓴다(처음이면 판 0 → 새 행).</summary>
    Normal = 0,
    /// <summary>
    /// 사람이 확인한 덮어쓰기 — 읽지 못한 행(본문 손상 · 읽기 실패)을 판과 상관없이 덮는다. 덮기 전 본문은 행의 보관 칸에 남긴다.
    /// </summary>
    Overwrite = 1,
}

/// <summary>불러오기 결과의 갈래 — "행이 없다" 와 "읽지 못했다" 를 가른다(없는 것으로 여기면 저장이 충돌로 오보된다).</summary>
public enum FenceLayoutLoadStatus
{
    /// <summary>이 서버 · 제어기의 행이 없다(처음).</summary>
    NotFound = 0,
    /// <summary>읽었다.</summary>
    Loaded = 1,
    /// <summary>행은 있는데 본문을 읽을 수 없다(손상) — 행 판은 안다.</summary>
    Unreadable = 2,
    /// <summary>저장소에 닿지 못했다(DB 예외 · 시간 초과) — 행이 있는지도 모른다.</summary>
    Failed = 3,
}

/// <summary>불러오기 한 번의 결과.</summary>
/// <param name="RowRevision">저장소 행의 판 — 본문을 못 읽었어도 행을 봤으면 안다(덮어쓰기에 쓴다). 모르면 <c>null</c>.</param>
public sealed record FenceLayoutLoadResult(FenceLayoutLoadStatus Status, FenceLayoutDocument? Document = null, int? RowRevision = null, string? Detail = null)
{
    public static FenceLayoutLoadResult NotFound { get; } = new(FenceLayoutLoadStatus.NotFound);

    public static FenceLayoutLoadResult Loaded(FenceLayoutDocument document) => new(FenceLayoutLoadStatus.Loaded, document, document.Revision);

    public static FenceLayoutLoadResult Failed(string? detail = null) => new(FenceLayoutLoadStatus.Failed, null, null, detail);

    /// <summary>행이 있을 수도 있는데 읽지 못했다 — 저장은 사람이 확인한 덮어쓰기로만 한다.</summary>
    public bool IsReadFailure => Status is FenceLayoutLoadStatus.Unreadable or FenceLayoutLoadStatus.Failed;
}

/// <summary>
/// 저장소의 열쇠 — <b>어느 서버의</b> 어느 제어기인가. 같은 PC 의 로컬 DB 를 운영 서버와 시험 서버(루프백 · <c>IRONWALL_UITEST_SERVER</c>)가
/// 함께 쓰므로 제어기 id 만으로는 서로 덮는다(id 가 겹친다).
/// </summary>
/// <param name="Server">서버 식별 — API 주소의 <c>host:port</c>(<see cref="FenceLayoutRows.ServerKeyOf"/>).</param>
public readonly record struct FenceLayoutKey(string Server, int ControllerId)
{
    public bool IsValid => ControllerId > 0 && !string.IsNullOrWhiteSpace(Server);

    public override string ToString() => $"{Server}#{ControllerId}";
}

/// <summary>
/// 펜스 구성 로컬 저장소(fence-wiring-editor FR-11 · FR-16) — (서버, 제어기) 가 열쇠. 구현은 지도와 같은 로컬 DB(<c>GMaps.Db</c>)에 있고,
/// 결선 창은 이 인터페이스만 안다(없으면 로컬 저장 칸만 숨긴다).
/// </summary>
public interface IFenceLayoutStore
{
    /// <summary>그 서버 · 제어기의 구성 — 없음 · 읽음 · 손상 · 실패를 가른다. 예외를 던지지 않는다(로그 한 줄).</summary>
    Task<FenceLayoutLoadResult> LoadAsync(FenceLayoutKey key, CancellationToken token = default);

    /// <summary>
    /// 저장한다 — <see cref="FenceLayoutSaveMode.Normal"/> 은 <see cref="FenceLayoutDocument.Revision"/>(불러온 판)이 저장소의 판과 같을 때만 쓴다
    /// (아니면 <see cref="FenceLayoutSaveStatus.Conflict"/>). 처음 저장은 판 0. <see cref="FenceLayoutSaveMode.Overwrite"/> 는 판을 보지 않는다.
    /// </summary>
    Task<FenceLayoutSaveResult> SaveAsync(FenceLayoutKey key, FenceLayoutDocument document, FenceLayoutSaveMode mode = FenceLayoutSaveMode.Normal,
                                          CancellationToken token = default);
}
