using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 조치보고 창 사진 [크게 보기] 크기 판정 — 창 안(비율 HostRatio)에
                  사진 비율 그대로 꽉 맞춘다. 작은 썸네일도 키워서 보인다.
   Created By   : GHLee
   Created On   : 10/3/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public static class ThumbnailZoomRules
{
    /// <summary>창 크기 중 확대 사진이 차지할 몫 — 둘레가 보여야 '창 위에 뜬 것' 으로 읽힌다.</summary>
    public const double HostRatio = 0.85;

    /// <summary>창 크기를 모를 때(오프스크린 · 시험)의 기준 칸.</summary>
    public static readonly Size FallbackHost = new(1280, 800);

    /// <summary>
    /// 사진(<paramref name="imageWidth"/>×<paramref name="imageHeight"/>)을 창(<paramref name="hostWidth"/>×<paramref name="hostHeight"/>)의
    /// <see cref="HostRatio"/> 안에 비율을 지켜 맞춘 크기. 사진 크기를 모르면 4:3 으로 본다. 창 크기를 모르면 <see cref="FallbackHost"/>.
    /// </summary>
    public static Size Fit(double imageWidth, double imageHeight, double hostWidth, double hostHeight)
    {
        if (!IsUsable(imageWidth) || !IsUsable(imageHeight)) { imageWidth = 4; imageHeight = 3; }
        if (!IsUsable(hostWidth) || !IsUsable(hostHeight)) { hostWidth = FallbackHost.Width; hostHeight = FallbackHost.Height; }

        var boxWidth = hostWidth * HostRatio;
        var boxHeight = hostHeight * HostRatio;
        var scale = Math.Min(boxWidth / imageWidth, boxHeight / imageHeight);
        return new Size(Math.Floor(imageWidth * scale), Math.Floor(imageHeight * scale));
    }

    private static bool IsUsable(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
}
