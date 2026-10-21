namespace Ironwall.Dotnet.Monitoring.Models.Components;

/// <summary>
/// 서버 카탈로그(<c>GET /api/devices/spec</c> 의 <c>component_state</c> 어휘)가 주는 부품 상태 한글 이름.
/// </summary>
/// <remarks>
/// <para>장비 콘솔의 카탈로그 캐시(<c>Devices.Ui CatalogService</c>)가 구현한다. <see cref="ComponentDisplay.StateName"/> 은
/// 내장 사전(화면 말투 정본)에 없는 상태일 때만 이 이름을 쓴다 — 서버가 새 상태를 보내도 "알 수 없음" 으로 뭉개지지 않게.</para>
/// <para><see cref="IComponentTypeLabels"/> 를 넓히지 않고 따로 둔 이유: 그 인터페이스의 가짜 구현이 한꺼번에 깨진다.</para>
/// </remarks>
public interface IComponentStateLabels
{
    /// <summary>상태 코드의 한글 이름. 모르면(카탈로그 미적재 · 없는 코드 · 라벨이 코드와 같음) <c>null</c>.</summary>
    string? StateLabel(string? state);
}
