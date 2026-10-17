namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// 서버 프로필 사진 주소의 두 얼굴 — <b>서버가 저장·반환하는 모양</b>(상대 경로 <c>/api/users/photo/{파일}</c>)과
/// <b>화면이 그릴 수 있는 모양</b>(접속한 API 주소 기준 절대 URL).
/// </summary>
/// <remarks>
/// <para>서버 8.0 은 업로드 요청의 Host 로 절대 URL 을 만들던 것을 버리고 <b>상대 경로만</b> 저장한다
/// ("클라는 자신이 접속한 base_url 과 조합해 GET /api/users/photo/{name} 을 부른다" — <c>users.py</c> PHOTO_URL_PREFIX 주석).
/// 그런데 화면의 <c>ImageConverter</c> 는 http(s) 만 원격으로 그리고 나머지는 로컬 파일 이름으로 보므로,
/// 상대 경로가 그대로 오면 <b>모든 계정이 기본 아바타</b>로 보였다(라이브 실측 2026-09-24).</para>
/// <para>그래서 서버 → 화면 경계(<c>ApiAccountGateway</c>)에서 <see cref="ToDisplay"/> 로 절대 URL 을 만들고,
/// 화면 → 서버 경계(<c>AccountDtoMapper</c>)에서 <see cref="ToServer"/> 로 다시 상대 경로로 돌린다 — DB 에 호스트를 심지 않는다.</para>
/// </remarks>
public static class ServerPhotoUrl
{
    /// <summary>서버 사진 서빙 경로의 접두(서버 검증기가 받는 세 형태 중 하나).</summary>
    public const string PathPrefix = "/api/users/photo/";

    /// <summary>
    /// 서버 값 → 화면 값. 상대 경로(<c>/…</c>)면 설정된 API 주소 기준 절대 URL 로 만든다.
    /// 절대 URL · 로컬 파일 이름 · 빈 값 · 주소 미설정은 그대로 돌려준다(호스트를 지어내지 않는다).
    /// </summary>
    /// <param name="photoUrl">서버가 준 <c>photo_url</c>.</param>
    /// <param name="apiBaseUrl">클라이언트가 접속한 API 주소(예: <c>https://host:8000/api</c>).</param>
    public static string? ToDisplay(string? photoUrl, string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(photoUrl) || !photoUrl.StartsWith('/') || photoUrl.StartsWith("//", StringComparison.Ordinal))
            return photoUrl;
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            return photoUrl;

        // 서버 경로는 API 마운트("/api/…") 기준이다. 설정 주소가 "…/api" 로 끝나면 그 앞(리버스 프록시 하위 경로 등)을 살려
        // "/api" 가 두 번 붙지 않게 잇는다. 그 밖에는 호스트 루트에 붙인다.
        var basePath = baseUri.AbsolutePath.TrimEnd('/');
        var path = photoUrl;
        if (basePath.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            && photoUrl.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            path = basePath[..^"/api".Length] + photoUrl;

        return new Uri(baseUri, path).ToString();
    }

    /// <summary>
    /// 화면 값 → 서버에 보낼 값. 서버 사진 경로를 가리키는 절대 URL 은 상대 경로로 되돌린다(호스트 제거).
    /// 서버 상대 경로는 그대로, 외부 http(s) URL 도 그대로, 그 밖(로컬 파일 경로 · 빈 값)은 null(미전송).
    /// </summary>
    public static string? ToServer(string? image)
    {
        if (string.IsNullOrEmpty(image)) return null;
        if (image.StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase)) return image;
        if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var at = image.IndexOf(PathPrefix, StringComparison.OrdinalIgnoreCase);
            return at >= 0 ? image[at..] : image;
        }
        return null;
    }
}
