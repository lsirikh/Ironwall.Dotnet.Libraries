using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 5:19:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// GMapCustomMarker 전용 Adorner 래퍼
/// </summary>
public class MarkerAdornerWrapper : GMapAdornerWrapper
{
    #region Constructor

    public MarkerAdornerWrapper(GMapCustomMarker customMarker, GMapCustomControl mapControl)
    {
        CustomMarker = customMarker ?? throw new ArgumentNullException(nameof(customMarker));
        MapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));

        InitializeFromMarker();
    }

    #endregion

    #region Properties

    /// <summary>
    /// 연결된 GMapCustomMarker
    /// </summary>
    public GMapCustomMarker CustomMarker { get; }

    /// <summary>
    /// 지리적 위치
    /// </summary>
    public PointLatLng GeoPosition
    {
        get { return (PointLatLng)GetValue(GeoPositionProperty); }
        set { SetValue(GeoPositionProperty, value); }
    }

    public static readonly DependencyProperty GeoPositionProperty =
        DependencyProperty.Register("GeoPosition", typeof(PointLatLng),
            typeof(MarkerAdornerWrapper), new PropertyMetadata(PointLatLng.Empty, OnGeoPositionChanged));

    #endregion

    #region Property Change Handlers

    private static void OnGeoPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkerAdornerWrapper wrapper && wrapper.MapControl != null)
        {
            wrapper.UpdateScreenPosition();
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// GMapCustomMarker로부터 초기화
    /// </summary>
    private void InitializeFromMarker()
    {
        try
        {
            // 마커의 Shape을 Content로 설정
            if (CustomMarker.Shape != null)
            {
                Content = CustomMarker.Shape;
            }

            // 지리적 위치 설정
            GeoPosition = CustomMarker.Position;

            // 크기 설정
            Width = Math.Max(MinWidth, CustomMarker.Width);
            Height = Math.Max(MinHeight, CustomMarker.Height);

            // 회전 설정 (방향각)
            if (CustomMarker.Bearing != 0)
            {
                RenderTransform = new RotateTransform(CustomMarker.Bearing);
            }

            // 화면 위치 업데이트
            UpdateScreenPosition();

            _log?.Info($"마커 초기화 완료: {CustomMarker.Title}, 위치: {GeoPosition}");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 초기화 실패: {ex.Message}");
        }
    }

    #endregion

    #region Position Management

    /// <summary>
    /// 지리적 위치를 화면 좌표로 변환하여 Canvas 위치 업데이트
    /// </summary>
    public override void UpdatePosition()
    {
        UpdateScreenPosition();
    }

    /// <summary>
    /// 지리적 위치를 화면 좌표로 변환하여 Canvas 위치 업데이트
    /// </summary>
    private void UpdateScreenPosition()
    {
        if (MapControl == null || GeoPosition == PointLatLng.Empty) return;

        try
        {
            var screenPoint = MapControl.FromLatLngToLocal(GeoPosition);

            // Canvas 좌표계에서 중심 기준으로 위치 설정
            Canvas.SetLeft(this, screenPoint.X - Width / 2);
            Canvas.SetTop(this, screenPoint.Y - Height / 2);
        }
        catch (Exception ex)
        {
            _log?.Error($"화면 위치 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 화면 위치를 지리적 좌표로 변환하여 업데이트
    /// </summary>
    public override void SyncToGeoCoordinates()
    {
        if (MapControl == null) return;

        try
        {
            var left = Canvas.GetLeft(this);
            var top = Canvas.GetTop(this);

            // 중심점 계산
            var centerX = left + Width / 2;
            var centerY = top + Height / 2;

            var geoPosition = MapControl.FromLocalToLatLng((int)centerX, (int)centerY);

            // 속성 업데이트 (이벤트 루프 방지를 위해 직접 할당)
            SetValue(GeoPositionProperty, geoPosition);

            // 연결된 마커 업데이트
            CustomMarker.UpdateLocation(geoPosition);

            _log?.Info($"지리적 위치 업데이트: {geoPosition}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지리적 위치 업데이트 실패: {ex.Message}");
        }
    }

    #endregion

    #region Rotation Handling

    protected override void OnRotationChanged(double angle)
    {
        CustomMarker.Bearing = angle;
    }

    #endregion

    #region Debug Information

    protected override string GetDebugInfo()
    {
        return $"MarkerAdornerWrapper: {CustomMarker?.Title ?? "Unknown"}";
    }

    #endregion
}