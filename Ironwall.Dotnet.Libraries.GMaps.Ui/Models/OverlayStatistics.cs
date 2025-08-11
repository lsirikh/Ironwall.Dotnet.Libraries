using System;
using System.Text;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/6/2025 1:46:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 확장된 오버레이 통계 정보
/// </summary>
public class OverlayStatistics
{
    public int TotalCount { get; set; }
    public int VisibleCount { get; set; }
    public int VisibleInViewCount { get; set; } // 현재 뷰에서 보이는 개수 (새로 추가)
    public int TifCount { get; set; }
    public int GeoReferencedCount { get; set; }
    public double AverageOpacity { get; set; }
    public long TotalMemoryUsage { get; set; }
    public double TotalScreenArea { get; set; } // 총 화면 점유 면적 (새로 추가)

    public string GetFormattedMemoryUsage()
    {
        if (TotalMemoryUsage == 0) return "0 B";

        string[] suffixes = { "B", "KB", "MB", "GB" };
        int counter = 0;
        decimal number = TotalMemoryUsage;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }

    /// <summary>
    /// 화면 점유 면적 포맷팅 (새로 추가)
    /// </summary>
    public string GetFormattedScreenArea()
    {
        if (TotalScreenArea <= 0) return "0 px²";

        if (TotalScreenArea < 1000000) // 1M 픽셀 미만
            return $"{TotalScreenArea:n0} px²";
        else
            return $"{TotalScreenArea / 1000000:n1} Mpx²";
    }

    /// <summary>
    /// 가시성 비율 계산 (새로 추가)
    /// </summary>
    public double GetVisibilityRatio()
    {
        return TotalCount > 0 ? (double)VisibleCount / TotalCount : 0.0;
    }

    /// <summary>
    /// 뷰 내 가시성 비율 계산 (새로 추가)
    /// </summary>
    public double GetViewVisibilityRatio()
    {
        return TotalCount > 0 ? (double)VisibleInViewCount / TotalCount : 0.0;
    }

    /// <summary>
    /// GeoTIFF 비율 계산 (새로 추가)
    /// </summary>
    public double GetGeoReferencedRatio()
    {
        return TotalCount > 0 ? (double)GeoReferencedCount / TotalCount : 0.0;
    }

    public override string ToString()
    {
        var parts = new List<string>
        {
            $"오버레이 {TotalCount}개",
            $"표시: {VisibleCount}",
            $"뷰내: {VisibleInViewCount}",
            $"TIF: {TifCount}",
            $"GeoTIFF: {GeoReferencedCount}",
            $"평균 투명도: {AverageOpacity:P1}",
            $"메모리: {GetFormattedMemoryUsage()}"
        };

        if (TotalScreenArea > 0)
        {
            parts.Add($"화면점유: {GetFormattedScreenArea()}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// 상세 통계 정보 문자열 (새로 추가)
    /// </summary>
    public string ToDetailedString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== 오버레이 통계 ===");
        sb.AppendLine($"총 개수: {TotalCount}개");
        sb.AppendLine($"표시 중: {VisibleCount}개 ({GetVisibilityRatio():P1})");
        sb.AppendLine($"현재 뷰: {VisibleInViewCount}개 ({GetViewVisibilityRatio():P1})");
        sb.AppendLine($"TIF 파일: {TifCount}개");
        sb.AppendLine($"지리참조: {GeoReferencedCount}개 ({GetGeoReferencedRatio():P1})");
        sb.AppendLine($"평균 투명도: {AverageOpacity:P1}");
        sb.AppendLine($"메모리 사용: {GetFormattedMemoryUsage()}");

        if (TotalScreenArea > 0)
        {
            sb.AppendLine($"화면 점유: {GetFormattedScreenArea()}");
        }

        return sb.ToString();
    }
}