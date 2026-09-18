using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Api.Services;

/// <summary>
/// 서버가 어느 <b>계약 세대</b>인지. 버전 문자열이 아니라 <b>우리가 분기해야 하는 단위</b>로 정규화한다.
/// </summary>
/// <remarks>
/// 실측(2026-09-18): 원격 운영 = <c>6.3.2</c> · 로컬 개발 = <c>8.0.1</c>(같은 날 7.0.1 → 8.0.1 로 올랐다) · 명세 = <c>v8.0</c>.
/// 세 판본의 계약이 서로 다르고, 특히 장비 쓰기는 <b>한 본문으로 양립 불가</b>다 —
/// 6.3 은 <c>type_device</c> 를 <b>요구</b>하고 7.0 은 <c>additionalProperties:false</c> 로 <b>금지</b>한다.
/// </remarks>
public enum EnumServerContract
{
    /// <summary>
    /// API 6.3.x — <b>현재 운영 서버</b>. 장비 쓰기에 <c>type_device</c>·<c>status</c>·<c>mode</c> 필수,
    /// 목록은 <c>include_sensors</c>/<c>include_controller</c>, 권한 모듈 12종.
    /// <para>⚠ <b>판정 실패 시의 기본값</b>이다 — 틀렸을 때 손해가 가장 작다(§NFR-01).</para>
    /// </summary>
    V6_3 = 0,

    /// <summary>
    /// API 7.0.x — 축(axis) 전환판. 장비 쓰기에 <c>type_camera</c>/<c>type_sensor</c>/<c>type_controller</c> +
    /// <c>connection</c> 필수이고 <c>type_device</c> 는 <b>금지</b>. 목록은 <c>?include=</c>·<c>?view=full</c>,
    /// 권한 모듈 15종. 제거된 필터 7종과 410 묘비 경로가 생겼다.
    /// </summary>
    V7_0 = 1,

    /// <summary>
    /// API 8.0+ — 부대 편제(<c>/api/units</c>·<c>unit_id</c>) 판.
    /// <para><b>2026-09-18 갱신</b>: 로컬 개발 서버가 <c>8.0.1</c> 로 올라 <c>/api/units</c>·<c>/api/units/graph</c>·
    /// <c>/api/units/{unit_id}</c> 3경로가 <b>배포됐다</b>(GET 200 실측). <b>원격 운영은 여전히 6.3.2</b> 다.</para>
    /// <para>⚠ 이 값이 관측되기 전에는 <c>unit_id</c> 를 <b>절대 보내지 않는다</b> —
    /// 6.3/7.0 쓰기 스키마가 <c>extra="forbid"</c> 라 즉시 422 다(실측).</para>
    /// </summary>
    V8_0 = 2,
}

/// <summary>
/// 서버 계약 세대를 1회 확보해 캐시하는 프로브.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 우리는 서버 두 판본(운영 6.3.2 / 개발 8.0.1)을 동시에 상대한다.
/// 페이로드 성형만으로는 양쪽을 만족시킬 수 없어(장비 쓰기 required 집합이 교차한다)
/// 런타임 분기가 <b>구조적으로</b> 필요하다.</para>
/// <para><b>안전 방향</b> — 판정에 실패하거나 알 수 없으면 <see cref="EnumServerContract.V6_3"/> 으로 간주한다.
/// 운영이 6.3.2 이므로 틀렸을 때 손해가 작은 쪽이다.</para>
/// </remarks>
public interface IServerContractProbe
{
    /// <summary>현재 확보된 계약 세대. 미확보면 <see cref="EnumServerContract.V6_3"/>(보수적 기본값).</summary>
    EnumServerContract Contract { get; }

    /// <summary>서버가 보고한 원본 버전 문자열(진단·로그용). 미확보면 <c>null</c>.</summary>
    string? RawVersion { get; }

    /// <summary>1회라도 성공적으로 확보했는가. <c>false</c> 면 <see cref="Contract"/> 는 폴백값이다.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// 서버 계약 세대를 확보한다(멱등 — 이미 확보했으면 재조회하지 않는다).
    /// 실패해도 <b>예외를 던지지 않는다</b> — 폴백값을 유지하고 <c>false</c> 를 돌려준다.
    /// </summary>
    Task<bool> ResolveAsync(CancellationToken token = default);

    /// <summary>캐시를 버리고 다시 확보한다(서버 업그레이드 후 재기동 없이 반영할 때).</summary>
    Task<bool> RefreshAsync(CancellationToken token = default);
}
