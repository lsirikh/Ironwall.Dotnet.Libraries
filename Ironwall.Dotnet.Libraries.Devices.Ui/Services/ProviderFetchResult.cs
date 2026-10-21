namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;

/// <summary>단건 재조회의 결말(GOP API 8.0.15 대조 G39).</summary>
public enum ProviderFetchStatus
{
    /// <summary>받아서 캐시를 갱신했다(없던 것이면 넣었다).</summary>
    Updated = 0,

    /// <summary>서버가 404 — 그 id 가 서버에 없다(지워졌거나 이 계정의 범위 밖). 캐시는 건드리지 않았다 — 지울지는 부른 쪽이 정한다.</summary>
    NotFound = 1,

    /// <summary>그 밖의 실패(500 · 연결 실패 · 못 읽는 응답 · 모르는 종류). 캐시는 그대로다 — 나중에 다시 시도할 일이다.</summary>
    Failed = 2,
}

/// <summary>
/// 단건 재조회 결과 — <see cref="ProviderFetchStatus.NotFound"/>(404)를 다른 실패와 갈라 돌려준다.
/// </summary>
/// <param name="Status">결말.</param>
/// <param name="Item">갱신된 모델(<see cref="ProviderFetchStatus.Updated"/> 일 때만).</param>
/// <param name="StatusCode">서버 HTTP 상태(모르면 0).</param>
/// <param name="ErrorCode">서버 · 로컬 오류 코드(진단용 — 화면에 쓰지 않는다).</param>
public sealed record ProviderFetchResult<T>(ProviderFetchStatus Status, T? Item, int StatusCode = 0, string? ErrorCode = null) where T : class
{
    public bool IsUpdated => Status == ProviderFetchStatus.Updated;
    public bool IsNotFound => Status == ProviderFetchStatus.NotFound;
    public bool IsFailed => Status == ProviderFetchStatus.Failed;

    public static ProviderFetchResult<T> Updated(T item, int statusCode = 200) => new(ProviderFetchStatus.Updated, item, statusCode);
    public static ProviderFetchResult<T> NotFound(int statusCode = 404, string? errorCode = null) => new(ProviderFetchStatus.NotFound, null, statusCode, errorCode);
    public static ProviderFetchResult<T> Failed(int statusCode = 0, string? errorCode = null) => new(ProviderFetchStatus.Failed, null, statusCode, errorCode);
}
