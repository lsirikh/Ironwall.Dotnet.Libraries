namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;

/// <summary>
/// 페이지네이션 결과를 담는 범용 모델
/// <para>API 응답의 pagination 정보를 포함하여 다음 페이지 존재 여부를 판단</para>
/// </summary>
public class PagedResult<T>
{
    /// <summary>
    /// API 조회 성공 여부. (EA2) false = 서버/네트워크 실패 → 호출부는 기존 컬렉션을 보존해야 한다.
    /// true + Items 비어있음 = 정상적인 빈 결과(해당 기간 이벤트 없음) → 클리어 정상.
    /// </summary>
    public bool Success { get; set; } = true;

    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int Total { get; set; }

    public bool HasNextPage => Page < TotalPages;
}
