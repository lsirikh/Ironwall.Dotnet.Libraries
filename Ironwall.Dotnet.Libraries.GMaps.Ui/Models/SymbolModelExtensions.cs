using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
/****************************************************************************
   Purpose      : SymbolModel 확장 메서드 - 핵심 기능만                                                          
   Created By   : GHLee                                                
   Created On   : 8/12/2025                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// SymbolModel 확장 메서드 - 핵심 기능만
/// </summary>
public static class SymbolModelExtensions
{
    #region 기본 속성 접근자

    /// <summary>
    /// 마커 위치를 PointLatLng로 반환
    /// </summary>
    public static PointLatLng Position(this ISymbolModel model)
        => new PointLatLng(model.Latitude, model.Longitude);

    /// <summary>
    /// 마커 크기를 Size로 반환
    /// </summary>
    public static Size Size(this ISymbolModel model)
        => new Size(model.Width, model.Height);

    /// <summary>
    /// 마커 정보 요약 문자열
    /// </summary>
    public static string Summary(this ISymbolModel model)
    {
        return $"{model.Title} - 위치: ({model.Latitude:F6}, {model.Longitude:F6}), " +
               $"크기: {model.Width:F0}x{model.Height:F0}, " +
               $"방향: {model.Bearing:F1}°";
    }

    #endregion

    #region 위치 업데이트

    /// <summary>
    /// 마커 위치 업데이트
    /// </summary>
    public static void UpdatePosition(this ISymbolModel model, PointLatLng newPosition)
    {
        model.Latitude = newPosition.Lat;
        model.Longitude = newPosition.Lng;
    }

    /// <summary>
    /// 마커 위치 업데이트 (위도, 경도 직접 입력)
    /// </summary>
    public static void UpdatePosition(this ISymbolModel model, double latitude, double longitude)
    {
        model.Latitude = latitude;
        model.Longitude = longitude;
    }

    #endregion

    #region 크기 및 회전

    /// <summary>
    /// 마커 크기 설정 (최소/최대 크기 제한)
    /// </summary>
    public static void SetSize(this ISymbolModel model, double width, double height)
    {
        model.Width = Math.Max(MIN_SIZE, Math.Min(MAX_SIZE, width));
        model.Height = Math.Max(MIN_SIZE, Math.Min(MAX_SIZE, height));
    }

    /// <summary>
    /// 마커 방향 설정 (0-360도 범위로 정규화)
    /// </summary>
    public static void SetBearing(this ISymbolModel model, double bearing)
    {
        model.Bearing = NormalizeAngle(bearing);
    }

    /// <summary>
    /// 마커 방향을 상대적으로 회전
    /// </summary>
    public static void Rotate(this ISymbolModel model, double deltaAngle)
    {
        model.Bearing = NormalizeAngle(model.Bearing + deltaAngle);
    }

    #endregion

    #region 유효성 검사

    /// <summary>
    /// 마커 데이터 유효성 검사
    /// </summary>
    public static bool IsValid(this ISymbolModel model)
    {
        if (string.IsNullOrEmpty(model.Title)) return false;
        if (model.Latitude < -90 || model.Latitude > 90) return false;
        if (model.Longitude < -180 || model.Longitude > 180) return false;
        if (model.Width <= 0 || model.Height <= 0) return false;
        return true;
    }

    #endregion

    #region 헬퍼 메서드

    /// <summary>
    /// 각도 정규화 (0-360도 범위)
    /// </summary>
    private static double NormalizeAngle(double angle)
    {
        angle = angle % 360;
        return angle < 0 ? angle + 360 : angle;
    }

    #endregion

    #region 상수

    private const double MIN_SIZE = 10;
    private const double MAX_SIZE = 200;

    #endregion
}