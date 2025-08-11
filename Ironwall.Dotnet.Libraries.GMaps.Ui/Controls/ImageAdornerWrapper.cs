using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 5:16:06 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
// <summary>
/// GMapCustomImage 전용 Adorner 래퍼
/// </summary>
public class ImageAdornerWrapper : GMapAdornerWrapper
{
    #region Constructor

    public ImageAdornerWrapper(GMapCustomImage customImage, GMapCustomControl mapControl)
    {
        CustomImage = customImage ?? throw new ArgumentNullException(nameof(customImage));
        MapControl = mapControl ?? throw new ArgumentNullException(nameof(mapControl));

        InitializeFromImage();
    }

    #endregion

    #region Properties

    /// <summary>
    /// 연결된 GMapCustomImage
    /// </summary>
    public GMapCustomImage CustomImage { get; }

    /// <summary>
    /// 지리적 경계
    /// </summary>
    public RectLatLng GeoBounds
    {
        get { return (RectLatLng)GetValue(GeoBoundsProperty); }
        set { SetValue(GeoBoundsProperty, value); }
    }

    public static readonly DependencyProperty GeoBoundsProperty =
        DependencyProperty.Register("GeoBounds", typeof(RectLatLng),
            typeof(ImageAdornerWrapper), new PropertyMetadata(RectLatLng.Empty, OnGeoBoundsChanged));

    #endregion

    #region Property Change Handlers

    private static void OnGeoBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageAdornerWrapper wrapper && wrapper.MapControl != null)
        {
            wrapper.UpdateScreenBounds();
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// GMapCustomImage로부터 초기화
    /// </summary>
    private void InitializeFromImage()
    {
        try
        {
            _log?.Info($"=== InitializeFromImage 시작: {CustomImage.Title} ===");

            // 이미지를 Content로 설정
            var image = new Image
            {
                Source = CustomImage.Img,
                Stretch = Stretch.Fill,
                Opacity = CustomImage.Opacity,
                // 빨간 테두리로 확실히 보이게
                Effect = new DropShadowEffect
                {
                    Color = Colors.Red,
                    BlurRadius = 2,
                    ShadowDepth = 0
                }
            };

            Content = image;

            // 📌 강제 크기 설정 (테스트용)
            Width = 200;
            Height = 200;

            // 📌 강제 위치 설정 (화면 중앙)
            Canvas.SetLeft(this, 400);
            Canvas.SetTop(this, 300);

            // 강제로 보이도록 설정
            Visibility = Visibility.Visible;
            IsHitTestVisible = true;

            // 📌 눈에 확실히 띄는 배경
            Background = new SolidColorBrush(Color.FromArgb(128, 255, 255, 0)); // 반투명 노란색

            // 테두리 추가
            BorderBrush = Brushes.Red;
            BorderThickness = new Thickness(3);

            // 지리적 경계는 나중에 설정
            GeoBounds = CustomImage.ImageBounds;

            _log?.Info($"강제 설정 완료:");
            _log?.Info($"  - Size: {Width} x {Height}");
            _log?.Info($"  - Position: ({Canvas.GetLeft(this)}, {Canvas.GetTop(this)})");
            _log?.Info($"  - Visibility: {Visibility}");
            _log?.Info($"  - Background: {Background}");

        }
        catch (Exception ex)
        {
            _log?.Error($"이미지 초기화 실패: {ex.Message}");
        }
    }

    #endregion

    #region Position Management

    /// <summary>
    /// 지리적 경계를 화면 좌표로 변환하여 크기/위치 업데이트
    /// </summary>
    public override void UpdatePosition()
    {
        _log?.Info($"=== ImageAdornerWrapper.UpdatePosition 호출: {CustomImage.Title} ===");

        // 📌 임시로 UpdateScreenBounds 호출하지 않고 고정 위치 유지
        _log?.Info($"현재 Position: ({Canvas.GetLeft(this)}, {Canvas.GetTop(this)})");
        _log?.Info($"현재 Size: {Width} x {Height}");

        // 테스트용으로 화면 중앙에 고정
        if (double.IsNaN(Canvas.GetLeft(this)) || Canvas.GetLeft(this) < 0)
        {
            Canvas.SetLeft(this, 400);
            Canvas.SetTop(this, 300);
            _log?.Info($"음수 좌표 수정: (400, 300)으로 강제 설정");
        }


        //UpdateScreenBounds();
    }

    /// <summary>
    /// 지리적 경계를 화면 좌표로 변환하여 크기/위치 업데이트
    /// </summary>
    private void UpdateScreenBounds()
    {
        if (MapControl == null || GeoBounds == RectLatLng.Empty) return;

        try
        {
            _log?.Info($"=== UpdateScreenBounds 시작: {CustomImage.Title} ===");
            _log?.Info($"GeoBounds: {GeoBounds}");

            var topLeft = MapControl.FromLatLngToLocal(GeoBounds.LocationTopLeft);
            var bottomRight = MapControl.FromLatLngToLocal(GeoBounds.LocationRightBottom);

            _log?.Info($"좌표 변환 결과:");
            _log?.Info($"  - TopLeft: {GeoBounds.LocationTopLeft} -> ({topLeft.X}, {topLeft.Y})");
            _log?.Info($"  - BottomRight: {GeoBounds.LocationRightBottom} -> ({bottomRight.X}, {bottomRight.Y})");

            // 📌 크기 계산 (절댓값 사용)
            double calculatedWidth = Math.Abs(bottomRight.X - topLeft.X);
            double calculatedHeight = Math.Abs(bottomRight.Y - topLeft.Y);

            _log?.Info($"계산된 크기: {calculatedWidth} x {calculatedHeight}");

            // 📌 최소/최대 크기 제한
            Width = Math.Max(50, Math.Min(1000, calculatedWidth));   // 50~1000 픽셀 제한
            Height = Math.Max(50, Math.Min(1000, calculatedHeight)); // 50~1000 픽셀 제한

            // 📌 위치 설정 (음수 방지)
            double leftPos = Math.Min(topLeft.X, bottomRight.X);
            double topPos = Math.Min(topLeft.Y, bottomRight.Y);

            // 화면 범위 내로 강제 조정
            leftPos = Math.Max(-Width / 2, Math.Min(MapControl.ActualWidth + Width / 2, leftPos));
            topPos = Math.Max(-Height / 2, Math.Min(MapControl.ActualHeight + Height / 2, topPos));

            Canvas.SetLeft(this, leftPos);
            Canvas.SetTop(this, topPos);

            _log?.Info($"최종 설정:");
            _log?.Info($"  - Size: {Width} x {Height}");
            _log?.Info($"  - Position: ({leftPos}, {topPos})");
            _log?.Info($"  - MapControl Size: {MapControl.ActualWidth} x {MapControl.ActualHeight}");

        }
        catch (Exception ex)
        {
            _log?.Error($"화면 경계 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 화면 크기/위치를 지리적 경계로 변환하여 업데이트
    /// </summary>
    public override void SyncToGeoCoordinates()
    {
        if (MapControl == null) return;

        try
        {
            var left = Canvas.GetLeft(this);
            var top = Canvas.GetTop(this);

            var topLeft = MapControl.FromLocalToLatLng((int)left, (int)top);
            var bottomRight = MapControl.FromLocalToLatLng((int)(left + Width), (int)(top + Height));

            var newBounds = new RectLatLng(
                topLeft.Lat,
                topLeft.Lng,
                bottomRight.Lng - topLeft.Lng,
                topLeft.Lat - bottomRight.Lat
            );

            // 속성 업데이트 (이벤트 루프 방지를 위해 직접 할당)
            SetValue(GeoBoundsProperty, newBounds);

            // 연결된 이미지 업데이트
            CustomImage.ImageBounds = newBounds;

            _log?.Info($"지리적 경계 업데이트: {newBounds}");
        }
        catch (Exception ex)
        {
            _log?.Error($"지리적 경계 업데이트 실패: {ex.Message}");
        }
    }

    #endregion

    #region Rotation Handling

    protected override void OnRotationChanged(double angle)
    {
        CustomImage.Rotation = angle;
    }

    #endregion

    #region Debug Information

    protected override string GetDebugInfo()
    {
        return $"ImageAdornerWrapper: {CustomImage?.Title ?? "Unknown"}";
    }

    #endregion
}
