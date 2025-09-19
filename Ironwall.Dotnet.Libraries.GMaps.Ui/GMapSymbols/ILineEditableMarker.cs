using System.Collections.Generic;
using System.Windows;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

public interface ILineEditableMarker : IEditableMarker
{
    /// <summary>
    /// 런타임에서 사용하는 PointLatLng 리스트
    /// </summary>
    List<PointLatLng> RuntimePoints { get; }

    /// <summary>
    /// 모델의 GeoPoint 리스트
    /// </summary>
    List<GeoPoint> LinePoints { get; }

    /// <summary>
    /// 현재 그리기 중인지 여부
    /// </summary>
    bool IsDrawing { get; }

    /// <summary>
    /// 닫힌 경로 여부
    /// </summary>
    bool IsClosedPath { get; set; }

    /// <summary>
    /// 라인 패턴
    /// </summary>
    EnumLinePattern LinePattern { get; set; }

    /// <summary>
    /// 라인 투명도
    /// </summary>
    double LineOpacity { get; set; }

    /// <summary>
    /// 화살표 표시 여부
    /// </summary>
    bool ShowArrowHead { get; set; }

    /// <summary>
    /// 점 추가
    /// </summary>
    void AddPoint(PointLatLng point);

    /// <summary>
    /// 특정 인덱스의 점 업데이트
    /// </summary>
    void UpdatePoint(int index, PointLatLng newPoint);

    /// <summary>
    /// 마지막 점 제거
    /// </summary>
    void RemoveLastPoint();

    /// <summary>
    /// 그리기 시작
    /// </summary>
    void StartDrawing();

    /// <summary>
    /// 그리기 완료
    /// </summary>
    void FinishDrawing();

    /// <summary>
    /// 그리기 취소
    /// </summary>
    void CancelDrawing();

    /// <summary>
    /// 총 라인 길이 (미터)
    /// </summary>
    double TotalDistance { get; }

   
}
