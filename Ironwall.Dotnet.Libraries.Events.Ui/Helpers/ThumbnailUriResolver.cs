using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Models;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 탐지 썸네일(detail.thumbnail) 경로를 바인딩 가능한 절대 URI로 변환
   Created By   : GHLee
   Created On   : 7/31/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 썸네일 원본 경로(detail.thumbnail)를 화면 바인딩용 절대 URI로 변환하는 공용 헬퍼.
/// DataGrid 행(DetectionEventViewModel)·속성 편집기(DetectionSelectionViewModel)가 공유.
/// - 절대 URL이면 host는 버리고 '경로'만 취해 API 서버 주소(scheme+authority)로 재조합
///   (서버가 detail.thumbnail을 내부주소 https://192.168.x:8000 로 박아 관제 클라가 못 붙는 문제 대응)
/// - 상대 경로(/api/thumbnails/…)면 API 서버 주소(scheme+authority)와 결합
/// - 부재/조합 실패 시 null → 뷰에서 기본 이미지로 폴백
/// 실제 이미지 로드 실패(자체서명 인증서 등)는 URI가 유효해도 발생할 수 있으며,
/// 이는 뷰의 겹침 레이어(뒤 default 이미지)가 처리한다.
/// </summary>
public static class ThumbnailUriResolver
{
    /// <summary>썸네일 원본 경로 → 절대 URI. 없거나 조합 실패면 null.</summary>
    public static Uri? Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // 클라가 실제로 붙는 API 서버 주소(scheme+authority) — 썸네일 URI의 기준은 항상 여기.
        var baseUrl = ResolveApiBaseUrl();

        // 절대 URL: 서버가 박은 host는 서버 내부주소(예: https://192.168.202.151:8000)라
        // 관제 클라에서 라우팅 불가 → host는 버리고 '경로'(PathAndQuery)만 취해 API 서버 주소로 재조합.
        // (썸네일 경로 /api/thumbnails/images/{file} 는 서버·클라 동일 계약이므로 재조합이 정합)
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs))
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return abs;   // base 미조달 시에만 원본 유지(무회귀)
            return Uri.TryCreate(new Uri(baseUrl!), abs.PathAndQuery, out var rebased) ? rebased : abs;
        }

        // 상대경로면 API 서버 주소와 결합 (base 미조달 시 null → default)
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return Uri.TryCreate(new Uri(baseUrl!), raw, out var combined) ? combined : null;
    }

    /// <summary>appsettings Url(예: https://host:8136/api)에서 scheme+authority 추출. IoC 미등록/해석 실패 시 null.</summary>
    private static string? ResolveApiBaseUrl()
    {
        try
        {
            var setup = IoC.Get<IApiSetupModel>();
            if (setup?.Url is not string url || string.IsNullOrWhiteSpace(url)) return null;
            return Uri.TryCreate(url, UriKind.Absolute, out var u) ? $"{u.Scheme}://{u.Authority}" : null;
        }
        catch { return null; }   // IoC 미등록/해석 실패 — 상대 썸네일은 default로 폴백
    }
}
