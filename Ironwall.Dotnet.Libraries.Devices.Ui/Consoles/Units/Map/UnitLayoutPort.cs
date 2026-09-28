using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 공유 배치의 좁은 창구 — 포트 · 서비스 어댑터 · 세션 전용 저장소 (FR-10 · FR-51 · NFR-14)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

// ─────────────────────────────────────────────────────────────────────────────
//  관계도 뷰모델 ──IUnitLayoutApi──▶ UnitLayoutApiAdapter ──▶ IUnitLayoutApiService(Devices.Api) ──▶ 서버 S-1
//                 └─(미지원 판정 뒤)──▶ SessionOnlyUnitLayoutStore(메모리 전용 — 디스크 · 서버 0)
//
//  ⚠ 이 파일은 파일 시스템을 쓰지 않는다(NFR-14 — File · FileStream · IsolatedStorage 0). 공유 배치를
//    디스크에 남기는 순간 "숨은 대체 저장소"가 된다(PRD R-24).
// ─────────────────────────────────────────────────────────────────────────────

#region - 포트 모델 -
/// <summary>
/// 공유 배치 문서의 메모리 사본 — 버전 · 판 · 마지막 변경 · 부대별 Δ(월드 단위).
/// </summary>
/// <param name="Version">문서 버전(세션 전용이면 언제나 0 — 버전을 흉내 내지 않는다).</param>
/// <param name="LayoutVersion">문서가 가정한 자동 배치 판(<see cref="UnitMapLayout.LayoutVersion"/> 과 다르면 Δ 미적용).</param>
/// <param name="UpdatedAt">마지막 변경 시각(없으면 <c>null</c>).</param>
/// <param name="UpdatedByName">마지막 변경자 이름(없으면 <c>null</c>).</param>
/// <param name="Deltas">Δ 가 있는 부대만.</param>
public sealed record UnitLayoutSnapshot(
    long Version,
    int LayoutVersion,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName,
    IReadOnlyDictionary<int, Vector> Deltas)
{
    /// <summary>빈 문서(아무도 옮기지 않음).</summary>
    public static UnitLayoutSnapshot Empty(int layoutVersion = UnitMapLayout.LayoutVersion)
        => new(0, layoutVersion, null, null, new Dictionary<int, Vector>());

    /// <summary>그 부대의 Δ — 없으면 <c>null</c>(자동 배치 그대로).</summary>
    public Vector? DeltaOf(int unitId) => Deltas.TryGetValue(unitId, out var delta) ? delta : null;
}

/// <summary>
/// 한 번의 확정 = 쓰기 한 건(FR-10). 서버는 <c>clear_all</c> → <c>clear</c> → <c>set</c> 순으로 적용한다(S-1 ③).
/// </summary>
public sealed record UnitLayoutChange(IReadOnlyDictionary<int, Vector> Set, IReadOnlyCollection<int> Clear, bool ClearAll)
{
    /// <summary>위치 한 번(끈 부대 하나 — 예하는 Δ 전파로 따라온다).</summary>
    public static UnitLayoutChange SetOne(int unitId, Vector delta)
        => new(new Dictionary<int, Vector> { [unitId] = delta }, Array.Empty<int>(), ClearAll: false);

    /// <summary>그 부대 배치 초기화(상세 [이 부대 배치 초기화] · 상위 변경 뒤 정리 FR-08).</summary>
    public static UnitLayoutChange ClearOne(int unitId)
        => new(new Dictionary<int, Vector>(), new[] { unitId }, ClearAll: false);

    /// <summary>전체 배치 초기화(툴바 [배치 초기화] — FR-09).</summary>
    public static UnitLayoutChange ClearEverything()
        => new(new Dictionary<int, Vector>(), Array.Empty<int>(), ClearAll: true);

    /// <summary>이 변경이 손대는 부대 id(<c>clear_all</c> 은 포함하지 않는다 — 문서 전체다).</summary>
    public IEnumerable<int> UnitIds => Clear.Concat(Set.Keys).Distinct().OrderBy(id => id);

    /// <summary>
    /// 문서에 적용한 결과(순수) — <c>clear_all</c> 먼저, 그다음 <c>clear</c>, 마지막 <c>set</c>. 버전 · 변경자는 그대로 둔다.
    /// </summary>
    public UnitLayoutSnapshot ApplyTo(UnitLayoutSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var deltas = ClearAll ? new Dictionary<int, Vector>() : new Dictionary<int, Vector>(snapshot.Deltas);
        foreach (var id in Clear) deltas.Remove(id);
        foreach (var (id, delta) in Set) deltas[id] = delta;
        return snapshot with { Deltas = deltas };
    }
}

/// <summary>배치 읽기 결과(포트).</summary>
public abstract record UnitLayoutRead
{
    private UnitLayoutRead() { }

    /// <summary>서버가 공유 배치를 지원한다.</summary>
    public sealed record Supported(UnitLayoutSnapshot Snapshot) : UnitLayoutRead;

    /// <summary>이 서버는 배치 저장을 지원하지 않는다 → 세션 전용(FR-51).</summary>
    public sealed record Unsupported(string Reason) : UnitLayoutRead;

    /// <summary>읽지 못했다 → 자동 배치 · 쓰기 금지 · [다시 시도].</summary>
    public sealed record Failed(UnitLayoutFailureKind Kind, string Reason) : UnitLayoutRead;
}

/// <summary>배치 쓰기 결과(포트).</summary>
public abstract record UnitLayoutWrite
{
    private UnitLayoutWrite() { }

    /// <summary>저장됐다 — 새 문서.</summary>
    public sealed record Saved(UnitLayoutSnapshot Snapshot) : UnitLayoutWrite;

    /// <summary>412 — 그 사이 누가 썼다(쓰지 않았다). GET 뒤 <see cref="Model.UnitMapLayoutSync.Resolve"/> 로 판정.</summary>
    public sealed record Conflict(long? CurrentVersion) : UnitLayoutWrite;

    /// <summary>422 — 서버 규칙 위반. 재시도 유도 금지.</summary>
    public sealed record Rejected(string Reason) : UnitLayoutWrite;

    /// <summary>경로가 사라졌다 → 세션 전용 전환.</summary>
    public record Unsupported(string Reason) : UnitLayoutWrite;

    /// <summary>지원 중이던 경로가 사라졌다(404 · 405 · 410) → 세션 전용 전환(TEST-65). <see cref="Unsupported"/> 의 하위.</summary>
    public sealed record EndpointGone(string Reason) : Unsupported(Reason);

    /// <summary>그 밖의 실패(시간 초과면 반영됐을 수 있다 — 다시 읽어 확인).</summary>
    public record Failed(UnitLayoutFailureKind Kind, string Reason) : UnitLayoutWrite;

    /// <summary>428 — If-Match 누락(클라 결함 신호) → 배치를 다시 읽는다. <see cref="Failed"/> 의 하위.</summary>
    public sealed record PreconditionRequired(string Reason) : Failed(UnitLayoutFailureKind.PreconditionRequired, Reason);

    /// <summary>504 — 반영됐을 수 있다 → 다시 읽어 결과를 확정한다. <see cref="Failed"/> 의 하위(Kind = Timeout).</summary>
    public sealed record Unknown(string Reason) : Failed(UnitLayoutFailureKind.Timeout, Reason);
}
#endregion

#region - 포트 -
/// <summary>
/// 관계도가 공유 배치를 보는 <b>좁은 창구</b>(Units). <see cref="IUnitGraphApi"/> · <see cref="IUnitDeviceApi"/> 를 넓히지 않는
/// 별도 인터페이스다(NFR-10). 시험은 <c>FakeUnitLayoutApi</c> 로 서버 없이 돈다.
/// </summary>
public interface IUnitLayoutApi
{
    /// <summary>문서 읽기 — 창을 열 때 1회(지원 판정 겸) · 알림 뒤 · 412 뒤.</summary>
    Task<UnitLayoutRead> ReadAsync(CancellationToken token = default);

    /// <summary>일괄 쓰기 1회 — <paramref name="ifMatchVersion"/> 는 마지막으로 받은 문서 버전.</summary>
    Task<UnitLayoutWrite> WriteAsync(long ifMatchVersion, UnitLayoutChange change, CancellationToken token = default);
}

/// <summary><see cref="IUnitLayoutApiService"/> 위의 얇은 어댑터 — 서비스 결과를 포트 모델로 옮긴다.</summary>
public sealed class UnitLayoutApiAdapter : IUnitLayoutApi
{
    private readonly IUnitLayoutApiService? _service;
    private readonly int _clientLayoutVersion;

    /// <param name="service">공유 배치 서비스. <b>선택 주입</b> — 없으면(등록 안 된 호스트) 미지원 = 세션 전용.</param>
    /// <param name="clientLayoutVersion">이 클라의 자동 배치 판 — 쓰기 본문 <c>layout_version</c>.</param>
    public UnitLayoutApiAdapter(IUnitLayoutApiService? service, int clientLayoutVersion = UnitMapLayout.LayoutVersion)
    {
        _service = service;
        _clientLayoutVersion = clientLayoutVersion;
    }

    public async Task<UnitLayoutRead> ReadAsync(CancellationToken token = default)
    {
        if (_service is null) return new UnitLayoutRead.Unsupported(NoService);

        var result = await _service.GetAsync(token).ConfigureAwait(false);
        return result switch
        {
            UnitLayoutReadResult.Supported s => new UnitLayoutRead.Supported(ToSnapshot(s.Document)),
            UnitLayoutReadResult.Unsupported u => new UnitLayoutRead.Unsupported(u.Reason),
            UnitLayoutReadResult.ReadFailed f => new UnitLayoutRead.Failed(f.Kind, f.Reason),
            _ => new UnitLayoutRead.Failed(UnitLayoutFailureKind.Other, "알 수 없는 응답"),
        };
    }

    public async Task<UnitLayoutWrite> WriteAsync(long ifMatchVersion, UnitLayoutChange change, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_service is null) return new UnitLayoutWrite.Unsupported(NoService);

        var result = await _service.PatchAsync(ifMatchVersion, ToPatch(change), token).ConfigureAwait(false);
        return result switch
        {
            UnitLayoutWriteResult.Ok ok => new UnitLayoutWrite.Saved(ToSnapshot(ok.Document)),
            UnitLayoutWriteResult.Conflict c => new UnitLayoutWrite.Conflict(c.CurrentVersion),
            UnitLayoutWriteResult.Rejected r => new UnitLayoutWrite.Rejected(r.Reason),
            UnitLayoutWriteResult.EndpointGone g => new UnitLayoutWrite.EndpointGone(g.Reason),          // 하위 갈래를 먼저
            UnitLayoutWriteResult.Unsupported u => new UnitLayoutWrite.Unsupported(u.Reason),
            UnitLayoutWriteResult.PreconditionRequired p => new UnitLayoutWrite.PreconditionRequired(p.Reason),
            UnitLayoutWriteResult.Unknown k => new UnitLayoutWrite.Unknown(k.Reason),
            UnitLayoutWriteResult.Failed f => new UnitLayoutWrite.Failed(f.Kind, f.Reason),
            _ => new UnitLayoutWrite.Failed(UnitLayoutFailureKind.Other, "알 수 없는 응답"),
        };
    }

    /// <summary>서버 문서 → 메모리 사본. 같은 부대가 두 번 오면 뒤의 것이 이긴다(서버는 422 로 막지만 받는 쪽은 죽지 않는다).</summary>
    internal static UnitLayoutSnapshot ToSnapshot(UnitLayoutDocumentDto document)
    {
        var deltas = new Dictionary<int, Vector>();
        foreach (var item in document.Items ?? new List<UnitLayoutItemDto>())
        {
            if (item is null || item.UnitId <= 0) continue;
            if (!double.IsFinite(item.Dx) || !double.IsFinite(item.Dy)) continue;
            deltas[item.UnitId] = new Vector(item.Dx, item.Dy);
        }

        DateTimeOffset? updatedAt = DateTimeOffset.TryParse(document.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : null;
        var by = string.IsNullOrWhiteSpace(document.UpdatedBy?.Name) ? null : document.UpdatedBy!.Name;
        return new UnitLayoutSnapshot(document.Version, document.LayoutVersion, updatedAt, by, deltas);
    }

    /// <summary>변경 → 본문. 항목은 부대 id 오름차순(결정적 — 로그 · 시험 대조가 쉽다).</summary>
    private UnitLayoutPatchDto ToPatch(UnitLayoutChange change) => new()
    {
        LayoutVersion = _clientLayoutVersion,
        Set = change.Set.OrderBy(kv => kv.Key)
                        .Select(kv => new UnitLayoutItemDto { UnitId = kv.Key, Dx = kv.Value.X, Dy = kv.Value.Y })
                        .ToList(),
        Clear = change.Clear.Distinct().OrderBy(id => id).ToList(),
        ClearAll = change.ClearAll,
    };

    private const string NoService = "배치 저장 서비스가 없습니다.";
}
#endregion

#region - 세션 전용 저장소 -
/// <summary>
/// 서버가 배치를 지원하지 않을 때(FR-51)의 <b>메모리 전용</b> 배치 — 창을 닫으면 버려진다.
/// </summary>
public sealed class SessionOnlyUnitLayoutStore
{
    /// <param name="layoutVersion">이 클라의 자동 배치 판(판 불일치는 세션 전용에서 일어나지 않는다).</param>
    public SessionOnlyUnitLayoutStore(int layoutVersion = UnitMapLayout.LayoutVersion)
    {
        Snapshot = UnitLayoutSnapshot.Empty(layoutVersion);
    }

    /// <summary>지금 메모리 배치. <see cref="UnitLayoutSnapshot.Version"/> 은 언제나 0 — 버전을 흉내 내지 않는다(서버와 섞이지 않게).</summary>
    public UnitLayoutSnapshot Snapshot { get; private set; }

    /// <summary>변경을 메모리에만 적용한다 — 서버 · 디스크 0(FR-51 · NFR-14). UI 스레드 전용.</summary>
    public void Apply(UnitLayoutChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Snapshot = change.ApplyTo(Snapshot);
    }
}
#endregion
