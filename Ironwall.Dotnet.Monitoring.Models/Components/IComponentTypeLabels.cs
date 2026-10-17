namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>
/// 서버 카탈로그(<c>GET /api/devices/spec</c> 의 <c>component_type</c> 어휘)가 주는 부품 유형 한글 이름.
/// </summary>
/// <remarks>
/// <para>장비 콘솔의 카탈로그 캐시(<c>Devices.Ui CatalogService</c>)가 구현한다. 지도는 이 인터페이스만 알고,
/// 없거나(미등록 · 6.3) 읽지 못하면 <see cref="ComponentDisplay"/> 의 내장 사전으로 떨어진다(FR-07).</para>
/// <para>좁은 인터페이스로 따로 둔 이유: 카탈로그 서비스 인터페이스를 넓히면 시험의 가짜 구현이 한꺼번에 깨진다.</para>
/// </remarks>
public interface IComponentTypeLabels
{
    /// <summary>유형 코드의 한글 이름. 모르면(카탈로그 미적재 · 없는 코드) <c>null</c> — 코드를 그대로 돌려주지 않는다.</summary>
    string? TypeLabel(string? type);
}
