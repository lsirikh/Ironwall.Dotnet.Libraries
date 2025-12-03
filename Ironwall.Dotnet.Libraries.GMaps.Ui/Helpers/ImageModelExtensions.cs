using GMap.NET;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : ImageModel 확장 메서드 (GMap.NET RectLatLng 변환)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// ImageModel과 RectLatLng 간의 변환을 위한 확장 메서드
/// </summary>
public static class ImageModelExtensions
{
    /// <summary>
    /// ImageModel의 경계 좌표를 RectLatLng으로 변환
    /// </summary>
    /// <param name="model">변환할 ImageModel</param>
    /// <returns>Left, Top, Right, Bottom을 기반으로 한 RectLatLng</returns>
    public static RectLatLng ToRect(this IImageModel model)
    {
        double widthLng = model.Right - model.Left;
        double heightLat = model.Top - model.Bottom;
        return new RectLatLng(model.Top, model.Left, widthLng, heightLat);
    }

    /// <summary>
    /// RectLatLng에서 ImageModel의 경계 좌표를 설정
    /// </summary>
    /// <param name="model">설정할 ImageModel</param>
    /// <param name="rect">소스 RectLatLng</param>
    public static void Deconstruct(this IImageModel model, RectLatLng rect)
    {
        model.Top = rect.Top;
        model.Left = rect.Left;
        model.Right = rect.Left + rect.WidthLng;
        model.Bottom = rect.Top - rect.HeightLat;

        // 중심점도 업데이트
        model.Latitude = (model.Top + model.Bottom) / 2.0;
        model.Longitude = (model.Left + model.Right) / 2.0;
    }
}
