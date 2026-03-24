namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;

/// <summary>
/// 페이지네이션 결과를 담는 범용 모델
/// <para>API 응답의 pagination 정보를 포함하여 다음 페이지 존재 여부를 판단</para>
/// </summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int Total { get; set; }

    public bool HasNextPage => Page < TotalPages;
}
