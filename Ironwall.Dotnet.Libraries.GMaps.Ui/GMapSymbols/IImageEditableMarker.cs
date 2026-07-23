using GMap.NET;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/****************************************************************************
   Purpose      : Image Overlay 마커용 인터페이스 (Phase 23)
   Created By   : Claude Code
   Created On   : 2025-12-03
   PRD Reference: Docs/PRD/PRD_ImageOverlay_Feature.md
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// Image Overlay 마커용 인터페이스
/// </summary>
/// <remarks>
/// IEditableMarker를 상속하여 기존 Adorner 시스템과 호환
/// Image 전용 속성 추가:
/// - 지리적 경계 (Left, Top, Right, Bottom)
/// - 이미지 소스 및 파일 경로
/// - 투명도, 회전, 가로세로비 유지
/// </remarks>
public interface IImageEditableMarker : IEditableMarker
{
    #region - Image 전용 속성 -
    /// <summary>이미지 파일 경로</summary>
    string? FilePath { get; set; }

    /// <summary>투명도 (0.0 ~ 1.0)</summary>
    double Opacity { get; set; }

    /// <summary>지리 참조 여부</summary>
    bool HasGeoReference { get; }

    /// <summary>좌표계</summary>
    string? CoordinateSystem { get; set; }

    /// <summary>이미지 경계 (RectLatLng)</summary>
    RectLatLng ImageBounds { get; set; }

    /// <summary>좌측 경도</summary>
    double Left { get; }

    /// <summary>상단 위도</summary>
    double Top { get; }

    /// <summary>우측 경도</summary>
    double Right { get; }

    /// <summary>하단 위도</summary>
    double Bottom { get; }

    /// <summary>중심점</summary>
    PointLatLng Center { get; }

    /// <summary>가로세로비</summary>
    double AspectRatio { get; }

    /// <summary>가로세로비 유지 여부</summary>
    bool MaintainAspectRatio { get; set; }

    /// <summary>이미지 소스</summary>
    ImageSource? ImageSource { get; }

    /// <summary>ImageModel 참조</summary>
    IImageModel ImageModel { get; }

    /// <summary>라벨 오프셋 U — 시각 footprint 하프익스텐트(가로) 비율(px 아님). 영속 정본(Overlay_Title FR-02).</summary>
    double LabelOffsetU { get; set; }
    /// <summary>라벨 오프셋 V — 하프익스텐트(세로) 비율 (FR-02).</summary>
    double LabelOffsetV { get; set; }
    #endregion

    #region - Image 전용 메서드 -
    /// <summary>
    /// 이미지 경계 업데이트
    /// </summary>
    /// <param name="bounds">새 경계</param>
    void UpdateBounds(RectLatLng bounds);

    /// <summary>
    /// 지정된 점이 이미지 경계 내에 있는지 확인
    /// </summary>
    /// <param name="point">확인할 점</param>
    /// <returns>내부에 있으면 true</returns>
    bool Contains(PointLatLng point);
    #endregion
}
