using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Security.Cryptography;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 5:55:51 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ImageModel : BaseModel, IImageModel
{

    #region - Ctors -
    /// <summary>
    /// 기본 생성자
    /// </summary>
    public ImageModel()
    {
        Id = 0;
        Title = "Unnamed";

        Latitude = 0.0;
        Longitude = 0.0;
        Altitude = 0.0f;

        Left = 0.0;
        Top = 0.0;
        Right = 0.0;
        Bottom = 0.0;

        FilePath = string.Empty;
        HasGeoReference = false;
        CoordinateSystem = "WGS84";
        Rotation = 0.0;

        Width = 100;
        Height = 100;
        Visibility = true;
        Opacity = 0.8;
        Zoom = 0;  // 기본값: 0 = 모든 줌 레벨에서 표시

        TitleSize = 11.0;      // 기존 GMapImageMarker 인메모리 기본값 승계 (FR-10)
        ShowTitle = false;
        LabelOffsetU = 0.0;
        LabelOffsetV = 0.0;
    }

    /// <summary>
    /// 위치 중심 간단 생성자
    /// </summary>
    public ImageModel(int id, string title, double latitude, double longitude)
        : this()
    {
        Id = id;
        Title = title;
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// 경계 기반 생성자
    /// </summary>
    public ImageModel(int id, string title, double left, double top, double right, double bottom)
        : this()
    {
        Id = id;
        Title = title;

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;

        Latitude = (top + bottom) / 2.0;
        Longitude = (left + right) / 2.0;
    }

    /// <summary>
    /// IImageModel을 복사하여 생성
    /// </summary>
    public ImageModel(IImageModel model)
        : this()
    {
        if (model == null) return;

        Id = model.Id;
        Title = model.Title;

        Latitude = model.Latitude;
        Longitude = model.Longitude;
        Altitude = model.Altitude;

        Left = model.Left;
        Top = model.Top;
        Right = model.Right;
        Bottom = model.Bottom;

        FilePath = model.FilePath;
        HasGeoReference = model.HasGeoReference;
        CoordinateSystem = model.CoordinateSystem;
        Rotation = model.Rotation;

        Width = model.Width;
        Height = model.Height;
        Visibility = model.Visibility;
        IsLocked = model.IsLocked;
        Opacity = model.Opacity;
        Zoom = model.Zoom;
        ZOrder = model.ZOrder;

        TitleSize = model.TitleSize;
        ShowTitle = model.ShowTitle;
        LabelOffsetU = model.LabelOffsetU;
        LabelOffsetV = model.LabelOffsetV;
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #region - 기본 식별 속성 -
    public string? Title { get; set; }
    #endregion

    #region - 위치 및 방향 속성 -
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Altitude { get; set; }


    // Bounds
    public double Left { get; set; }
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }

    public string FilePath { get; set; }

    public bool HasGeoReference { get; set; }
    public string? CoordinateSystem { get; set; }

    #endregion

    #region - 시각적 표현 속성 -
    public double Width { get; set; }
    public double Height { get; set; }
    /// <summary>사용자 편집 회전 각도(도). DB 영속 대상 = GMapCustomImage.UserRotation. 맵 보정(-MapRotation)은 런타임 전용이라 여기 저장하지 않는다.</summary>
    public double Rotation { get; set; }
    public bool Visibility { get; set; }
    /// <summary>잠금 상태 — true면 맵에서 클릭/선택 불가. 기본 false.</summary>
    public bool IsLocked { get; set; }
    public double Opacity { get; set; }

    /// <summary>
    /// 이미지가 표시되는 최소 줌 레벨.
    /// 지도 줌이 이 값 이상일 때만 이미지가 표시됨.
    /// 0 = 모든 줌 레벨에서 표시
    /// </summary>
    public double Zoom { get; set; }

    /// <summary>
    /// 런타임 Z-Order 홀더. MapLayers.ZOrder에서 주입받음 (DB 컬럼 없음).
    /// </summary>
    public int ZOrder { get; set; } = 0;

    /// <summary>라벨 글자 크기(pt) — Images.TitleSize 영속 (FR-10).</summary>
    public double TitleSize { get; set; }
    /// <summary>라벨(제목) 표시 여부 — Images.ShowTitle 영속 (FR-10).</summary>
    public bool ShowTitle { get; set; }
    /// <summary>라벨 오프셋 U(하프익스텐트 비율, px 아님) — Images.LabelOffsetU 영속 (FR-02).</summary>
    public double LabelOffsetU { get; set; }
    /// <summary>라벨 오프셋 V(하프익스텐트 비율) — Images.LabelOffsetV 영속 (FR-02).</summary>
    public double LabelOffsetV { get; set; }
    #endregion
    #endregion
    #region - Attributes -
    #endregion
}