using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapImages;
/****************************************************************************
      Purpose      : 커스텀 이미지 오버레이 (GMapImage 상속) - TIF 등 이미지 파일 표시용                                                          
      Created By   : GHLee                                                
      Created On   : 7/30/2025 8:00:00 PM                                                    
      Department   : SW Team                                                   
      Company      : Sensorway Co., Ltd.                                       
      Email        : lsirikh@naver.com                                         
   ****************************************************************************/
public class GMapCustomImage : GMapImage, IBaseModel, INotifyPropertyChanged
{
    

    #region - Constructor -
    public GMapCustomImage(ILogService log, ImageModel imageModel)
    {
        _log = log;
        _model = imageModel;

        _imageBounds = _model.ToRect();

        // 원본 이미지 크기 저장
        Img = LoadImage(_model.FilePath);
        if (Img == null)
        {
            _log?.Warning($"Failed to load image from path: {_model.FilePath}");
        }
        else
        {
            _originalImageSize = new Size(Img.Width, Img.Height);
        }

        _createdAt = DateTime.Now;
        InitializeProperties();

        // 디버깅 로그 추가
        _log?.Info($"GMapCustomImage 생성: _imageBounds = ({_imageBounds.Left:F6}, {_imageBounds.Bottom:F6}) to ({_imageBounds.Right:F6}, {_imageBounds.Top:F6})");
    }
    #endregion


    #region - Overrides -
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 관리 리소스 정리
                _model = null;
                _log = null;
            }

            _disposed = true;
            base.Dispose();
        }
    }

    /// <summary>
    /// GMapImage의 Dispose 오버라이드
    /// </summary>
    public override void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ToString 오버라이드
    /// </summary>
    public override string ToString()
    {
        return GetSummary();
    }
    #endregion

    
   
    #region - Public Methods -
    /// <summary>
    /// 이미지 경계를 중심점과 크기로 설정
    /// </summary>
    public void SetBoundsFromCenter(PointLatLng center, double widthDegrees, double heightDegrees)
    {
        var newBounds = new RectLatLng(
            center.Lat + heightDegrees / 2,
            center.Lng - widthDegrees / 2,
            widthDegrees,
            heightDegrees
        );

        ImageBounds = newBounds;
    }

    /// <summary>
    /// 이미지를 특정 위치로 이동
    /// </summary>
    public void MoveTo(PointLatLng newCenter)
    {
        SetBoundsFromCenter(newCenter, _imageBounds.WidthLng, _imageBounds.HeightLat);
    }

    /// <summary>
    /// 이미지 크기 조정
    /// </summary>
    public void Resize(double widthDegrees, double heightDegrees)
    {
        if (_maintainAspectRatio && AspectRatio > 0)
        {
            var currentRatio = widthDegrees / heightDegrees;
            if (Math.Abs(currentRatio - AspectRatio) > 0.01)
            {
                if (AspectRatio > currentRatio) // 원본이 더 가로로 긴 경우
                {
                    heightDegrees = widthDegrees / AspectRatio;
                }
                else // 원본이 더 세로로 긴 경우
                {
                    widthDegrees = heightDegrees * AspectRatio;
                }
            }
        }

        SetBoundsFromCenter(Center, widthDegrees, heightDegrees);
    }

    /// <summary>
    /// 종횡비에 맞춰 크기 조정
    /// </summary>
    public void FitToAspectRatio()
    {
        if (AspectRatio <= 0) return;

        var currentRatio = _imageBounds.WidthLng / _imageBounds.HeightLat;
        if (Math.Abs(AspectRatio - currentRatio) > 0.01)
        {
            double newWidth, newHeight;

            if (AspectRatio > currentRatio) // 원본이 더 가로로 긴 경우
            {
                newWidth = _imageBounds.WidthLng;
                newHeight = newWidth / AspectRatio;
            }
            else // 원본이 더 세로로 긴 경우
            {
                newHeight = _imageBounds.HeightLat;
                newWidth = newHeight * AspectRatio;
            }

            SetBoundsFromCenter(Center, newWidth, newHeight);
        }
    }

    /// <summary>
    /// 클릭 좌표가 이미지 경계 안에 들어가는지 판정
    /// </summary>
    public bool Contains(PointLatLng point)
    {
        // _imageBounds 대신 ImageBounds 속성 사용
        var bounds = ImageBounds;

        // 방어 코드
        if (bounds.IsEmpty)
        {
            _log?.Info($"이미지 '{Title}': 경계가 비어있음");
            return false;
        }

        // ── 위도 검사 ──
        double top = bounds.Top;
        double bottom = bounds.Bottom;
        bool latInRange = point.Lat >= bottom && point.Lat <= top;

        // ── 경도 검사 ──
        double left = bounds.Left;
        double right = bounds.Right;
        bool lngInRange =
            left <= right
                ? (point.Lng >= left && point.Lng <= right)
                : (point.Lng >= left || point.Lng <= right);

        bool result = latInRange && lngInRange;

        _log?.Info($"이미지 '{Title}' Contains 검사: 점({point.Lat:F6}, {point.Lng:F6}), " +
                   $"경계({left:F6}, {bottom:F6}) to ({right:F6}, {top:F6}), " +
                   $"위도OK={latInRange}, 경도OK={lngInRange}, 결과={result}");

        return result;
    }

    /// <summary>
    /// 이미지와 다른 경계가 겹치는지 검사
    /// </summary>
    public bool IntersectsWith(RectLatLng otherBounds)
    {
        return _imageBounds.IntersectsWith(otherBounds);
    }

    /// <summary>
    /// 경계 좌표 배열 반환 (left, top, right, bottom)
    /// </summary>
    public double[] GetBoundsArray()
    {
        return new[]
        {
                _imageBounds.Left,    // left (westmost longitude)
                _imageBounds.Top,     // top (northmost latitude) 
                _imageBounds.Right,   // right (eastmost longitude)
                _imageBounds.Bottom   // bottom (southmost latitude)
            };
    }

    /// <summary>
    /// 이미지 정보 요약
    /// </summary>
    public string GetSummary()
    {
        return $"{Title} - {_originalImageSize.Width:F0}x{_originalImageSize.Height:F0}" +
               (HasGeoReference ? " (GeoTIFF)" : " (일반 이미지)") +
               $" - 투명도: {Opacity:P0}";
    }

    /// <summary>
    /// 복제본 생성
    /// </summary>
    public GMapCustomImage Clone()
    {
        var imageCopy = new ImageModel(_model) // ImageModel 복사 생성자 사용
        {
            Title = _model.Title + "_Copy"
        };

        var clone = new GMapCustomImage(_log, imageCopy)
        {
            Opacity = imageCopy.Opacity,
            Visibility = imageCopy.Visibility,
            Rotation = imageCopy.Rotation,
        };

        return clone;
    }

    public ImageSource? LoadImage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        try
        {
            if (!File.Exists(filePath))
            {
                _log?.Warning($"Image file not found: {filePath}");
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception ex)
        {
            _log?.Error($"Error loading image from {filePath}: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region - Private Methods -
    private void InitializeProperties()
    {
        CoordinateSystem = "WGS84";
    }
    #endregion
    #region - INotifyPropertyChanged Implementation -
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion

    #region - Properties -
    /// <summary>
    /// 이미지 아이디
    /// </summary>
    public int Id
    {
        get => _model.Id;
        set
        {
            _model.Id = value;
            OnPropertyChanged(nameof(Id));
        }
    }


    /// <summary>
    /// 이미지 이름
    /// </summary>
    public string? Title
    {
        get => _model.Title;
        set
        {
            _model.Title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    /// <summary>
    /// 원본 파일 경로
    /// </summary>
    public string? FilePath
    {
        get => _model.FilePath;
        set
        {
            _model.FilePath = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 이미지 경계 좌표
    /// </summary>
    public RectLatLng ImageBounds
    {
        get => _model.ToRect();
        set
        {
            _model.Deconstruct(value);
            _imageBounds = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(TopLeft));
            OnPropertyChanged(nameof(TopRight));
            OnPropertyChanged(nameof(BottomLeft));
            OnPropertyChanged(nameof(BottomRight));
            OnPropertyChanged(nameof(Center));
            OnPropertyChanged(nameof(BoundsInfo));


            // 디버깅 로그
            _log?.Info($"ImageBounds 설정: ({value.Left:F6}, {value.Bottom:F6}) to ({value.Right:F6}, {value.Top:F6})");
        }
    }

    /// <summary>
    /// 이미지 투명도 (0.0 ~ 1.0)
    /// </summary>
    public double Opacity
    {
        get => _model.Opacity;
        set
        {
            _model.Opacity = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 이미지 표시 여부
    /// </summary>
    public bool Visibility
    {
        get => _model.Visibility;
        set
        {
            _model.Visibility = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 종횡비 유지 여부
    /// </summary>
    public bool MaintainAspectRatio
    {
        get => _maintainAspectRatio;
        set
        {
            _maintainAspectRatio = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 원본 이미지 크기
    /// </summary>
    public Size OriginalImageSize
    {
        get => new Size(_model.Width, _model.Height);
        set
        {
            _model.Width = value.Width;
            _model.Height = value.Height;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AspectRatio));
        }
    }

    /// <summary>
    /// 이미지 종횡비
    /// </summary>
    public double AspectRatio
    {
        get => _originalImageSize.Height > 0 ? _originalImageSize.Width / _originalImageSize.Height : 1.0;
    }

    /// <summary>
    /// 생성 시간
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set
        {
            _createdAt = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// GeoTIFF 여부
    /// </summary>
    public bool HasGeoReference
    {
        get => _model.HasGeoReference;
        set
        {
            _model.HasGeoReference = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 좌표 체계
    /// </summary>
    public string? CoordinateSystem
    {
        get => _model.CoordinateSystem;
        set
        {
            _model.CoordinateSystem = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 너비 (픽셀)
    /// </summary>
    public double Width
    {
        get { return _model.Width; }
        set
        {
            _model.Width = value;
            OnPropertyChanged(nameof(Width)); ;
        }
    }

    /// <summary>
    /// 높이 (픽셀)
    /// </summary>
    public double Height
    {
        get { return _model.Height; }
        set
        {
            _model.Height = value;
            OnPropertyChanged(nameof(Height)); ;
        }
    }


    /// <summary>
    /// 회전 각도 (도 단위)
    /// </summary>
    public double Rotation
    {
        get => _model.Rotation;
        set
        {
            _model.Rotation = value % 360;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected)); ;
        }
    }
    #endregion

    #region - 경계 좌표 Properties -
    /// <summary>
    /// 좌상단 좌표 (TopLeft)
    /// </summary>
    public PointLatLng TopLeft => ImageBounds.LocationTopLeft;

    /// <summary>
    /// 우상단 좌표 (TopRight)
    /// </summary>
    public PointLatLng TopRight => new PointLatLng(ImageBounds.Top, ImageBounds.Right);

    /// <summary>
    /// 좌하단 좌표 (BottomLeft)
    /// </summary>
    public PointLatLng BottomLeft => new PointLatLng(ImageBounds.Bottom, ImageBounds.Left);

    /// <summary>
    /// 우하단 좌표 (BottomRight)
    /// </summary>
    public PointLatLng BottomRight => ImageBounds.LocationRightBottom;

    /// <summary>
    /// 중심 좌표
    /// </summary>
    public PointLatLng Center => ImageBounds.LocationMiddle;

    /// <summary>
    /// 경계 정보 문자열
    /// </summary>
    public string BoundsInfo
    {
        get
        {
            return $"좌상단: ({TopLeft.Lat:F6}, {TopLeft.Lng:F6})\n" +
                   $"우하단: ({BottomRight.Lat:F6}, {BottomRight.Lng:F6})\n" +
                   $"크기: {_imageBounds.WidthLng:F6}° × {_imageBounds.HeightLat:F6}°";
        }
    }

    public IImageModel Model => _model;

    #endregion

    #region - Attributes -
    private bool _disposed = false;

    private bool _maintainAspectRatio = true;
    private Size _originalImageSize;
    private RectLatLng _imageBounds;
    private DateTime _createdAt;
    private ILogService? _log;
    private ImageModel _model;
    private bool _isSelected;
    #endregion
}