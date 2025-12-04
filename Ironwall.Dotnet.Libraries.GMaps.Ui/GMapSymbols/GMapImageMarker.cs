using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
   Purpose      : Image Overlay 마커 (Phase 23) - GMapMarker 기반 구현
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Note: IImageModel은 IBaseModel을 상속하므로 GMapBaseMarker<T>를 사용할 수 없음
         (GMapBaseMarker<T>는 T : ISymbolModel 제약조건)
         따라서 GMapMarker를 직접 상속하여 구현
****************************************************************************/

/// <summary>
/// Image Overlay 마커 클래스
/// </summary>
/// <remarks>
/// - GMapMarker를 상속하여 GMap.NET 호환성 유지
/// - IImageEditableMarker 인터페이스 구현
/// - IImageModel을 내부에 보유하고 속성을 위임
/// </remarks>
public class GMapImageMarker : GMapMarker, IImageEditableMarker, IMarkerControl
{
    #region - Ctors -

    /// <summary>
    /// ImageModel과 함께 마커 생성
    /// </summary>
    /// <param name="log">로그 서비스</param>
    /// <param name="imageModel">이미지 모델</param>
    public GMapImageMarker(ILogService? log, IImageModel imageModel)
        : base(new PointLatLng(imageModel.Latitude, imageModel.Longitude))
    {
        _log = log;
        _imageModel = imageModel ?? throw new ArgumentNullException(nameof(imageModel));

        // 기본 설정
        ZIndex = 5; // 다른 마커보다 낮은 Z-Index (배경 이미지이므로)

        // ImageBounds 초기화
        _imageBounds = imageModel.ToRect();

        // Position 설정 (중심점)
        Position = Center;

        // Shape 생성 (지도에 표시되려면 Shape가 필요)
        // STA 스레드에서만 실행 가능하므로 try-catch로 감싸서 테스트 환경에서도 동작하도록 함
        TryCreateShape();

        _log?.Info($"GMapImageMarker 생성: {imageModel.Title} (Bounds: {Left:F4}-{Right:F4}, {Bottom:F4}-{Top:F4})");
    }

    /// <summary>
    /// Shape 생성 시도 - STA 스레드가 아니면 건너뜀
    /// </summary>
    private void TryCreateShape()
    {
        try
        {
            // STA 스레드 확인
            if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
            {
                _log?.Warning("STA 스레드가 아니므로 Shape 생성 건너뜀 (테스트 환경)");
                return;
            }

            CreateShape();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("thread"))
        {
            // 스레드 관련 예외는 무시 (테스트 환경)
            _log?.Warning($"Shape 생성 건너뜀 (스레드 문제): {ex.Message}");
        }
        catch (Exception ex)
        {
            _log?.Warning($"Shape 생성 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 이미지 Shape 생성 - GMapMarkerImageControl 사용 (데이터 바인딩 지원)
    /// </summary>
    private void CreateShape()
    {
        try
        {
            // 이미지 소스 먼저 로드 (Width/Height 결정 필요)
            LoadImageSource();

            // GMapMarkerImageControl 생성 - 데이터 바인딩으로 자동 업데이트
            var markerControl = new GMapMarkerImageControl(this);

            // Offset 설정 (중심 기준)
            var width = _imageModel.Width > 0 ? _imageModel.Width : 100;
            var height = _imageModel.Height > 0 ? _imageModel.Height : 100;
            Offset = new Point(-width / 2, -height / 2);

            // Shape 설정
            Shape = markerControl;
            _log?.Info($"GMapImageMarker Shape 생성 완료 (GMapMarkerImageControl): {Title}");
        }
        catch (Exception ex)
        {
            _log?.Error($"Shape 생성 실패: {ex.Message}");
            CreateFallbackShape();
        }
    }

    /// <summary>
    /// 이미지 소스 로드 및 크기 결정
    /// </summary>
    private void LoadImageSource()
    {
        if (string.IsNullOrEmpty(_imageModel.FilePath) || !System.IO.File.Exists(_imageModel.FilePath))
        {
            _log?.Warning($"이미지 파일 없음: {_imageModel.FilePath}");
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(_imageModel.FilePath);
            bitmap.EndInit();
            bitmap.Freeze();

            _imageSource = bitmap;

            // 이미지 실제 크기로 업데이트 (Width/Height가 0이면)
            if (_imageModel.Width <= 0)
            {
                _imageModel.Width = bitmap.PixelWidth;
            }
            if (_imageModel.Height <= 0)
            {
                _imageModel.Height = bitmap.PixelHeight;
            }

            _log?.Info($"이미지 로드 성공: {_imageModel.FilePath} (크기: {_imageModel.Width}x{_imageModel.Height})");
        }
        catch (Exception ex)
        {
            _log?.Warning($"이미지 로드 실패: {ex.Message}");
        }
    }


    /// <summary>
    /// 완전히 실패했을 때 대체 Shape
    /// </summary>
    private void CreateFallbackShape()
    {
        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = 50,
            Height = 50,
            Fill = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0)),
            Stroke = Brushes.Red,
            StrokeThickness = 2
        };

        Offset = new Point(-25, -25);
        Shape = rect;
    }

    /// <summary>
    /// Shape 업데이트 (크기, 회전 등 변경 시)
    /// </summary>
    public void RefreshShape()
    {
        // GMapMarkerImageControl은 데이터 바인딩으로 자동 업데이트되므로
        // RefreshFromMarker()만 호출하면 됨
        if (Shape is GMapMarkerImageControl markerControl)
        {
            markerControl.RefreshFromMarker();
        }
    }

    #endregion

    #region - IDisposable -

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // 관리 리소스 정리
            _imageSource = null;
            _log?.Info($"GMapImageMarker Disposed: {Title}");
        }
        _disposed = true;
    }

    ~GMapImageMarker()
    {
        Dispose(false);
    }

    public bool IsDisposed => _disposed;

    #endregion

    #region - IEditableMarker 호환 속성 -

    /// <summary>이미지 ID</summary>
    public int Id => _imageModel.Id;

    /// <summary>이미지 제목</summary>
    public string Title
    {
        get => _imageModel.Title ?? string.Empty;
        set
        {
            if (_imageModel.Title != value)
            {
                _imageModel.Title = value;
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    /// <summary>제목 글자 크기</summary>
    public double TitleSize
    {
        get => _titleSize;
        set
        {
            if (Math.Abs(_titleSize - value) > 0.001)
            {
                _titleSize = value;
                OnPropertyChanged(nameof(TitleSize));
            }
        }
    }

    /// <summary>표시 너비 (픽셀)</summary>
    /// <remarks>
    /// Phase 33.3: Width 설정 시 ImageBounds도 비율에 맞게 업데이트
    /// PropertyWindow에서 Width 입력 시 화면에 반영되도록 수정
    /// </remarks>
    public double Width
    {
        get => _imageModel.Width;
        set
        {
            if (Math.Abs(_imageModel.Width - value) > 0.001)
            {
                var oldWidth = _imageModel.Width;
                _imageModel.Width = value;

                // Phase 33.3: ImageBounds도 비율에 맞게 업데이트
                if (oldWidth > 0.001)
                {
                    var scale = value / oldWidth;
                    UpdateBoundsFromPixelScale(scale, 1.0);
                }

                OnPropertyChanged(nameof(Width));
            }
        }
    }

    /// <summary>표시 높이 (픽셀)</summary>
    /// <remarks>
    /// Phase 33.3: Height 설정 시 ImageBounds도 비율에 맞게 업데이트
    /// PropertyWindow에서 Height 입력 시 화면에 반영되도록 수정
    /// </remarks>
    public double Height
    {
        get => _imageModel.Height;
        set
        {
            if (Math.Abs(_imageModel.Height - value) > 0.001)
            {
                var oldHeight = _imageModel.Height;
                _imageModel.Height = value;

                // Phase 33.3: ImageBounds도 비율에 맞게 업데이트
                if (oldHeight > 0.001)
                {
                    var scale = value / oldHeight;
                    UpdateBoundsFromPixelScale(1.0, scale);
                }

                OnPropertyChanged(nameof(Height));
            }
        }
    }

    /// <summary>
    /// 표시 줌 레벨 (0 = 모든 줌 레벨에서 표시)
    /// 지도 줌이 이 값 이상일 때만 이미지가 표시됨
    /// </summary>
    public double Zoom
    {
        get => _imageModel.Zoom;
        set
        {
            if (Math.Abs(_imageModel.Zoom - value) > 0.001)
            {
                _imageModel.Zoom = value;
                OnPropertyChanged(nameof(Zoom));
            }
        }
    }

    /// <summary>회전 각도 (도)</summary>
    public double Bearing
    {
        get => _imageModel.Rotation;
        set
        {
            // 0-360 범위 정규화
            while (value < 0) value += 360;
            while (value >= 360) value -= 360;

            if (Math.Abs(_imageModel.Rotation - value) > 0.001)
            {
                _imageModel.Rotation = value;
                RefreshShape(); // UI 업데이트
                OnPropertyChanged(nameof(Bearing));
            }
        }
    }

    /// <summary>선택 상태</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    /// <summary>가시성</summary>
    public bool IsVisible
    {
        get => _imageModel.Visibility;
        set
        {
            if (_imageModel.Visibility != value)
            {
                _imageModel.Visibility = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }
    }

    /// <summary>형태 표시 여부</summary>
    public bool ShowShape
    {
        get => _showShape;
        set
        {
            if (_showShape != value)
            {
                _showShape = value;
                OnPropertyChanged(nameof(ShowShape));
            }
        }
    }

    /// <summary>제목 표시 여부</summary>
    public bool ShowTitle
    {
        get => _showTitle;
        set
        {
            if (_showTitle != value)
            {
                _showTitle = value;
                OnPropertyChanged(nameof(ShowTitle));
            }
        }
    }

    /// <summary>채우기 색상</summary>
    public EnumColorType FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor != value)
            {
                _fillColor = value;
                OnPropertyChanged(nameof(FillColor));
            }
        }
    }

    /// <summary>테두리 색상</summary>
    public EnumColorType StrokeColor
    {
        get => _strokeColor;
        set
        {
            if (_strokeColor != value)
            {
                _strokeColor = value;
                OnPropertyChanged(nameof(StrokeColor));
            }
        }
    }

    /// <summary>테두리 두께</summary>
    public double StrokeThickness
    {
        get => _strokeThickness;
        set
        {
            if (Math.Abs(_strokeThickness - value) > 0.001)
            {
                _strokeThickness = value;
                OnPropertyChanged(nameof(StrokeThickness));
            }
        }
    }

    /// <summary>동작 상태</summary>
    public EnumOperationState OperationState
    {
        get => _operationState;
        set
        {
            if (_operationState != value)
            {
                _operationState = value;
                OnPropertyChanged(nameof(OperationState));
            }
        }
    }

    /// <summary>형태 애니메이션 활성화</summary>
    public bool EnableShapeAnimation
    {
        get => _enableShapeAnimation;
        set
        {
            if (_enableShapeAnimation != value)
            {
                _enableShapeAnimation = value;
                OnPropertyChanged(nameof(EnableShapeAnimation));
            }
        }
    }

    #endregion

    #region - Image 전용 속성 -

    /// <summary>이미지 파일 경로</summary>
    public string? FilePath
    {
        get => _imageModel.FilePath;
        set
        {
            if (_imageModel.FilePath != value)
            {
                _imageModel.FilePath = value ?? string.Empty;
                _imageSource = null; // 캐시 무효화
                OnPropertyChanged(nameof(FilePath));
                OnPropertyChanged(nameof(ImageSource));
            }
        }
    }

    /// <summary>투명도 (0.0 ~ 1.0)</summary>
    public double Opacity
    {
        get => _imageModel.Opacity;
        set
        {
            var clampedValue = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_imageModel.Opacity - clampedValue) > 0.001)
            {
                _imageModel.Opacity = clampedValue;
                RefreshShape(); // UI 업데이트
                OnPropertyChanged(nameof(Opacity));
            }
        }
    }

    /// <summary>지리 참조 여부</summary>
    public bool HasGeoReference => _imageModel.HasGeoReference;

    /// <summary>좌표계</summary>
    public string? CoordinateSystem
    {
        get => _imageModel.CoordinateSystem;
        set
        {
            if (_imageModel.CoordinateSystem != value)
            {
                _imageModel.CoordinateSystem = value;
                OnPropertyChanged(nameof(CoordinateSystem));
            }
        }
    }

    /// <summary>이미지 경계 (RectLatLng)</summary>
    public RectLatLng ImageBounds
    {
        get => _imageBounds;
        set
        {
            _imageBounds = value;
            // Model에도 동기화
            _imageModel.Deconstruct(value);
            // Position (중심점) 업데이트
            Position = Center;
            OnPropertyChanged(nameof(ImageBounds));
            OnPropertyChanged(nameof(Left));
            OnPropertyChanged(nameof(Top));
            OnPropertyChanged(nameof(Right));
            OnPropertyChanged(nameof(Bottom));
            OnPropertyChanged(nameof(Center));
        }
    }

    /// <summary>좌측 경도</summary>
    public double Left => _imageModel.Left;

    /// <summary>상단 위도</summary>
    public double Top => _imageModel.Top;

    /// <summary>우측 경도</summary>
    public double Right => _imageModel.Right;

    /// <summary>하단 위도</summary>
    public double Bottom => _imageModel.Bottom;

    /// <summary>중심점</summary>
    public PointLatLng Center => new(
        (_imageModel.Top + _imageModel.Bottom) / 2.0,
        (_imageModel.Left + _imageModel.Right) / 2.0
    );

    /// <summary>가로세로비</summary>
    public double AspectRatio
    {
        get
        {
            var heightLat = _imageBounds.HeightLat;
            if (Math.Abs(heightLat) < 0.0001) return 1.0;
            return _imageBounds.WidthLng / heightLat;
        }
    }

    /// <summary>가로세로비 유지 여부</summary>
    public bool MaintainAspectRatio
    {
        get => _maintainAspectRatio;
        set
        {
            if (_maintainAspectRatio != value)
            {
                _maintainAspectRatio = value;
                OnPropertyChanged(nameof(MaintainAspectRatio));
            }
        }
    }

    /// <summary>이미지 소스 (Lazy Loading)</summary>
    public ImageSource? ImageSource
    {
        get
        {
            if (_imageSource == null && !string.IsNullOrEmpty(FilePath))
            {
                try
                {
                    if (System.IO.File.Exists(FilePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(FilePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze(); // Thread-safe
                        _imageSource = bitmap;
                    }
                }
                catch (Exception ex)
                {
                    _log?.Error($"이미지 로드 실패: {FilePath}, {ex.Message}");
                }
            }
            return _imageSource;
        }
    }

    /// <summary>ImageModel 참조</summary>
    public IImageModel ImageModel => _imageModel;

    #endregion

    #region - 편집 메서드 -

    /// <summary>
    /// 마커 위치 업데이트 (중심점 이동)
    /// </summary>
    /// <param name="newPosition">새 중심 위치</param>
    public void UpdateLocation(PointLatLng newPosition)
    {
        try
        {
            // 현재 크기 계산
            double widthLng = _imageBounds.WidthLng;
            double heightLat = _imageBounds.HeightLat;

            // 새 중심점 기준으로 경계 재계산
            double newLeft = newPosition.Lng - widthLng / 2.0;
            double newRight = newPosition.Lng + widthLng / 2.0;
            double newTop = newPosition.Lat + heightLat / 2.0;
            double newBottom = newPosition.Lat - heightLat / 2.0;

            // ImageBounds 업데이트
            _imageBounds = new RectLatLng(newTop, newLeft, widthLng, heightLat);

            // Model 동기화
            _imageModel.Left = newLeft;
            _imageModel.Right = newRight;
            _imageModel.Top = newTop;
            _imageModel.Bottom = newBottom;
            _imageModel.Latitude = newPosition.Lat;
            _imageModel.Longitude = newPosition.Lng;

            // GMapMarker Position 업데이트
            Position = newPosition;

            OnPropertyChanged(nameof(ImageBounds));
            OnPropertyChanged(nameof(Center));
            OnPropertyChanged(nameof(Left));
            OnPropertyChanged(nameof(Right));
            OnPropertyChanged(nameof(Top));
            OnPropertyChanged(nameof(Bottom));

            _log?.Info($"GMapImageMarker '{Title}' 위치 업데이트: ({newPosition.Lat:F6}, {newPosition.Lng:F6})");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 위치 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 크기 업데이트 (픽셀 또는 위경도 단위 자동 감지)
    /// </summary>
    /// <remarks>
    /// Phase 33 버그 수정: MarkerEditAdorner가 픽셀 단위로 호출해도 정상 동작
    /// - 입력값이 10° 이상이면 픽셀로 간주하고 비율 기반 스케일링
    /// - 입력값이 10° 미만이면 위경도로 처리
    /// </remarks>
    /// <param name="width">너비 (픽셀 또는 경도)</param>
    /// <param name="height">높이 (픽셀 또는 위도)</param>
    public void UpdateSize(double width, double height)
    {
        try
        {
            // Phase 33: 픽셀 vs 위경도 자동 감지
            // 정상적인 이미지 오버레이는 10° 이하 (약 1100km 이하)
            const double MaxReasonableDegrees = 10.0;

            double actualWidthLng;
            double actualHeightLat;

            if (width > MaxReasonableDegrees || height > MaxReasonableDegrees)
            {
                // 픽셀 값으로 간주 - 현재 ImageBounds 비율로 스케일링
                // 픽셀 입력을 현재 위경도 크기 대비 비율로 변환
                double currentWidthLng = _imageBounds.WidthLng;
                double currentHeightLat = _imageBounds.HeightLat;

                // 현재 픽셀 크기 (Width/Height)와 입력 픽셀의 비율 계산
                double scaleX = width / Math.Max(_imageModel.Width, 1.0);
                double scaleY = height / Math.Max(_imageModel.Height, 1.0);

                actualWidthLng = currentWidthLng * scaleX;
                actualHeightLat = currentHeightLat * scaleY;

                // 픽셀 크기도 업데이트
                _imageModel.Width = width;
                _imageModel.Height = height;

                _log?.Info($"GMapImageMarker '{Title}' 픽셀 크기 입력 감지: {width}px x {height}px → {actualWidthLng:F6}° x {actualHeightLat:F6}°");
            }
            else
            {
                // 위경도 값으로 처리
                actualWidthLng = width;
                actualHeightLat = height;

                _log?.Info($"GMapImageMarker '{Title}' 위경도 크기 입력: {actualWidthLng:F6}° x {actualHeightLat:F6}°");
            }

            // 현재 중심점 유지
            var center = Center;

            // 새 경계 계산
            double newLeft = center.Lng - actualWidthLng / 2.0;
            double newRight = center.Lng + actualWidthLng / 2.0;
            double newTop = center.Lat + actualHeightLat / 2.0;
            double newBottom = center.Lat - actualHeightLat / 2.0;

            // ImageBounds 업데이트
            _imageBounds = new RectLatLng(newTop, newLeft, actualWidthLng, actualHeightLat);

            // Model 동기화
            _imageModel.Left = newLeft;
            _imageModel.Right = newRight;
            _imageModel.Top = newTop;
            _imageModel.Bottom = newBottom;

            OnPropertyChanged(nameof(ImageBounds));
            OnPropertyChanged(nameof(Left));
            OnPropertyChanged(nameof(Right));
            OnPropertyChanged(nameof(Top));
            OnPropertyChanged(nameof(Bottom));
            OnPropertyChanged(nameof(AspectRatio));
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 크기 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 마커 회전 업데이트
    /// </summary>
    /// <param name="bearing">새 회전 각도 (도)</param>
    public void UpdateRotation(double bearing)
    {
        try
        {
            Bearing = bearing;
            _log?.Info($"GMapImageMarker '{Title}' 회전 업데이트: {Bearing:F1}°");
        }
        catch (Exception ex)
        {
            _log?.Error($"마커 회전 업데이트 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 이미지 경계 업데이트
    /// </summary>
    /// <param name="bounds">새 경계</param>
    public void UpdateBounds(RectLatLng bounds)
    {
        ImageBounds = bounds;
        _log?.Info($"GMapImageMarker '{Title}' 경계 업데이트: ({bounds.Left:F4}-{bounds.Right:F4}, {bounds.Bottom:F4}-{bounds.Top:F4})");
    }

    /// <summary>
    /// 픽셀 스케일 기반 ImageBounds 업데이트 (Phase 33.3)
    /// </summary>
    /// <remarks>
    /// Width/Height setter에서 호출하여 ImageBounds를 비율에 맞게 업데이트
    /// 중심점은 유지하면서 크기만 변경
    /// </remarks>
    /// <param name="scaleX">가로 스케일 (1.0 = 변경 없음)</param>
    /// <param name="scaleY">세로 스케일 (1.0 = 변경 없음)</param>
    private void UpdateBoundsFromPixelScale(double scaleX, double scaleY)
    {
        try
        {
            // 현재 중심점 저장
            var center = Center;

            // 새 크기 계산
            double newWidthLng = _imageBounds.WidthLng * scaleX;
            double newHeightLat = _imageBounds.HeightLat * scaleY;

            // 새 경계 계산 (중심점 유지)
            double newLeft = center.Lng - newWidthLng / 2.0;
            double newRight = center.Lng + newWidthLng / 2.0;
            double newTop = center.Lat + newHeightLat / 2.0;
            double newBottom = center.Lat - newHeightLat / 2.0;

            // ImageBounds 업데이트
            _imageBounds = new RectLatLng(newTop, newLeft, newWidthLng, newHeightLat);

            // Model 동기화
            _imageModel.Left = newLeft;
            _imageModel.Right = newRight;
            _imageModel.Top = newTop;
            _imageModel.Bottom = newBottom;

            // PropertyChanged 알림
            OnPropertyChanged(nameof(ImageBounds));
            OnPropertyChanged(nameof(Left));
            OnPropertyChanged(nameof(Right));
            OnPropertyChanged(nameof(Top));
            OnPropertyChanged(nameof(Bottom));
            OnPropertyChanged(nameof(AspectRatio));

            _log?.Info($"GMapImageMarker '{Title}' 픽셀 스케일 업데이트: scaleX={scaleX:F2}, scaleY={scaleY:F2}");
        }
        catch (Exception ex)
        {
            _log?.Error($"UpdateBoundsFromPixelScale 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 지정된 점이 이미지 경계 내에 있는지 확인
    /// </summary>
    /// <param name="point">확인할 점</param>
    /// <returns>내부에 있으면 true</returns>
    public bool Contains(PointLatLng point)
    {
        return point.Lat >= Bottom && point.Lat <= Top &&
               point.Lng >= Left && point.Lng <= Right;
    }

    /// <summary>
    /// 지도 줌 변경 시 화면 위치 업데이트 (Phase 28)
    /// </summary>
    /// <remarks>
    /// 지리적 경계(ImageBounds)는 유지하고 화면 픽셀 위치만 업데이트합니다.
    /// GMapMarkerImageControl에서 OnMapZoomChanged 이벤트 핸들러가 호출합니다.
    /// </remarks>
    /// <param name="mapControl">GMap 컨트롤 (null 허용 - 안전 처리)</param>
    public void UpdateScreenPosition(GMapControl? mapControl)
    {
        // null 안전 처리 - 지리적 경계는 변경하지 않음
        if (mapControl == null)
        {
            return;
        }

        // Shape가 GMapMarkerImageControl이면 RefreshFromMarker 호출
        if (Shape is GMapMarkerImageControl markerControl)
        {
            markerControl.RefreshFromMarker();
        }
    }

    #endregion

    #region - Attributes -

    private readonly ILogService? _log;
    private readonly IImageModel _imageModel;
    private RectLatLng _imageBounds;
    private ImageSource? _imageSource;
    private bool _isSelected;
    private bool _maintainAspectRatio = true;

    // IEditableMarker 추가 속성용 필드
    private double _titleSize = 11.0;
    private bool _showShape = true;
    private bool _showTitle;
    private EnumColorType _fillColor = EnumColorType.Transparent;
    private EnumColorType _strokeColor = EnumColorType.Blue;
    private double _strokeThickness = 2.0;
    private EnumOperationState _operationState = EnumOperationState.NONE;
    private bool _enableShapeAnimation;

    #endregion

    #region - IMarkerControl 구현 -

    /// <summary>
    /// IMarkerControl.EditableMarker - 자기 자신을 반환
    /// </summary>
    IEditableMarker IMarkerControl.EditableMarker => this;

    /// <summary>
    /// IMarkerControl.VisualElement - Shape를 FrameworkElement로 반환
    /// </summary>
    FrameworkElement IMarkerControl.VisualElement => Shape as FrameworkElement ?? new System.Windows.Controls.Border();

    /// <summary>
    /// IMarkerControl.RefreshFromMarker - 마커 속성에서 UI 새로고침
    /// </summary>
    void IMarkerControl.RefreshFromMarker()
    {
        RefreshShape();
    }

    #endregion
}
