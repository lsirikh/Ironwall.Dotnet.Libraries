namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : FromLocalToLatLng(int) 정수픽셀 양자화를 서브픽셀 해상도로 보간 (WPF/GMap 무의존, 단위 테스트 가능)
   Created By   : GHLee
   Created On   : 7/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// GMap.NET <c>FromLocalToLatLng(int, int)</c>는 정수 코어픽셀에만 대응해, 프랙셔널 픽셀을 반올림하면
/// 위치가 정수 코어픽셀 격자에 양자화된다. 디지털 줌(1 코어픽셀 = 여러 화면픽셀)에서는 이 격자가 화면상
/// 굵어져 심볼 드래그가 계단처럼 "뻑뻑"하게 이동한다.
/// <para>인접 4개 정수픽셀의 위경도를 이중선형 보간해 프랙셔널 위치의 위경도를 구하면, 어느 디지털 줌에서도
/// 매끄럽게 이동한다. 순수 산술이라 헤드리스 단위 테스트가 가능하다.</para>
/// </summary>
internal static class SubPixelGeo
{
    /// <summary>
    /// 인접 4개 정수픽셀의 위경도를 이중선형 보간한다.
    /// p00=(x0,y0), p10=(x0+1,y0), p01=(x0,y0+1), p11=(x0+1,y0+1). fx,fy는 [0,1] 프랙셔널.
    /// </summary>
    internal static (double lat, double lng) Bilinear(
        (double lat, double lng) p00, (double lat, double lng) p10,
        (double lat, double lng) p01, (double lat, double lng) p11,
        double fx, double fy)
        => (Lerp2(p00.lat, p10.lat, p01.lat, p11.lat, fx, fy),
            Lerp2(p00.lng, p10.lng, p01.lng, p11.lng, fx, fy));

    /// <summary>이중선형: x축(y0)·x축(y1) 각각 보간 후 y축 보간.</summary>
    private static double Lerp2(double v00, double v10, double v01, double v11, double fx, double fy)
    {
        double top = v00 + (v10 - v00) * fx;   // y0 행에서 x 보간
        double bot = v01 + (v11 - v01) * fx;   // y1 행에서 x 보간
        return top + (bot - top) * fy;         // 두 행을 y 보간
    }
}
