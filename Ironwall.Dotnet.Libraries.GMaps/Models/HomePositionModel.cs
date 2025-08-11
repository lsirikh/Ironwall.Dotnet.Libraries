using Ironwall.Dotnet.Libraries.Base.Models;
using System;
using GMap.NET;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/23/2025 1:50:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class HomePositionModel : IHomePositionModel
{
    #region - Ctors -
    /// <summary>
    /// 기본 생성자 - Configuration Binding에서 사용
    /// </summary>
    public HomePositionModel()
    {
    }

    /// <summary>
    /// PointLatLng 기반 생성자 - GMap.NET 호환
    /// </summary>
    /// <param name="position">위치 정보</param>
    /// <param name="zoom">줌 레벨</param>
    /// <param name="isAvailable">사용 가능 여부</param>
    public HomePositionModel(PointLatLng position, double zoom = DEFAULT_ZOOM, bool isAvailable = false)
    {
        Position = new CoordinateModel(position.Lat, position.Lng, DEFAULT_ALTITUDE);
        Zoom = zoom;
        IsAvailable = isAvailable;
    }

    /// <summary>
    /// 좌표값 직접 지정 생성자
    /// </summary>
    /// <param name="latitude">위도</param>
    /// <param name="longitude">경도</param>
    /// <param name="altitude">고도</param>
    /// <param name="zoom">줌 레벨</param>
    /// <param name="isAvailable">사용 가능 여부</param>
    public HomePositionModel(double latitude, double longitude, double altitude = DEFAULT_ALTITUDE,
                           double zoom = DEFAULT_ZOOM, bool isAvailable = false)
    {
        Position = new CoordinateModel(latitude, longitude, altitude) ?? Position;
        Zoom = zoom;
        IsAvailable = isAvailable;
    }

    /// <summary>
    /// CoordinateModel 기반 생성자 - 타입 안전성 향상 (캐스팅 제거)
    /// </summary>
    /// <param name="position">좌표 모델</param>
    /// <param name="zoom">줌 레벨</param>
    /// <param name="isAvailable">사용 가능 여부</param>
    public HomePositionModel(CoordinateModel? position, double zoom = DEFAULT_ZOOM, bool isAvailable = false)
    {
        Position = position ?? Position;
        Zoom = zoom;
        IsAvailable = isAvailable;
    }

    /// <summary>
    /// 인터페이스 기반 복사 생성자 - 안전한 복제
    /// </summary>
    /// <param name="source">복사할 소스 모델</param>
    /// <exception cref="ArgumentNullException">source가 null인 경우</exception>
    public HomePositionModel(IHomePositionModel source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        // 깊은 복사 수행 - 캐스팅 대신 안전한 생성자 사용
        Position = source.Position ?? Position;

        Zoom = source.Zoom;
        IsAvailable = source.IsAvailable;
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public override string ToString()
    {
        return $"Latitude : {Position?.Latitude}, Longitude : {Position?.Longitude}, Altitude : {Position?.Altitude}";
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    /// <summary>
    /// 위치 좌표 정보
    /// </summary>
    public CoordinateModel? Position { get; set; } = new CoordinateModel(DEFAULT_LATITUDE, DEFAULT_LONGITUDE, DEFAULT_ALTITUDE);

    /// <summary>
    /// 줌 레벨 (1-20 범위)
    /// </summary>
    public double Zoom { get; set; } = DEFAULT_ZOOM;

    /// <summary>
    /// 홈 포지션 사용 가능 여부
    /// </summary>
    public bool IsAvailable { get; set; }
    /// <summary>
    /// GMap.NET 호환을 위한 PointLatLng 속성 (읽기 전용)
    /// </summary>
    [JsonIgnore]
    public PointLatLng PointLatLng => new(Position?.Latitude ?? DEFAULT_LATITUDE,
                                          Position?.Longitude ?? DEFAULT_LONGITUDE);
    #endregion
    #region - Attributes -
    private const double DEFAULT_LATITUDE = 37.648425;
    private const double DEFAULT_LONGITUDE = 126.904284;
    private const double DEFAULT_ALTITUDE = 0.0;
    private const double DEFAULT_ZOOM = 15.0;
    private const double MIN_ZOOM = 1.0;
    private const double MAX_ZOOM = 20.0;
    #endregion
}