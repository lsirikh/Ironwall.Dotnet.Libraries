using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
/****************************************************************************
   Purpose      : PIDS 마커 우클릭 상세보기 — URL 생성 및 Chrome 실행 서비스
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public interface IDeviceDetailUrlService
{
    /// <summary>
    /// 장비 타입과 ID로 SSW-SVMS URL을 생성한다.
    /// panel=null  → 목록: http://{host}/ssw-svms?node=...&amp;device=...
    /// panel=detail → 상세: ...&amp;panel=detail&amp;panelId={id}
    /// panel=edit   → 수정: ...&amp;panel=edit&amp;panelId={id}
    /// </summary>
    string BuildUrl(EnumDeviceType deviceType, int deviceId, string? panel = "detail");

    /// <summary>
    /// 주어진 URL을 Chrome으로 연다. Chrome 미설치 시 기본 브라우저로 폴백.
    /// </summary>
    void OpenInChrome(string url);
}
