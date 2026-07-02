using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GMap.NET;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
// 모든 마커가 공통으로 가져야 할 속성/메서드
public interface IEditableMarker : IDisposable
{
    int Id { get; }
    string Title { get; set; }
    double TitleSize { get; set; }
    PointLatLng Position { get; set; }
    double Width { get; set; }
    double Height { get; set; }
    double Zoom { get; set; }
    double Bearing { get; set; }
    bool IsSelected { get; set; }
    bool IsVisible { get; set; }
    bool IsLayerEnabled { get; set; }
    /// <summary>잠금 상태 — true면 맵에서 클릭/선택 불가(모델 IsLocked 연동·영속).</summary>
    bool IsLocked { get; set; }
    bool ShowShape { get; set; }
    bool ShowTitle { get; set; }
    /// <summary>라벨 상대 오프셋(화면 픽셀, 아이콘 하단 기본위치 기준). LabelAdorner 위치·DB 영속(Symbol_Label_Decouple).</summary>
    double LabelOffsetX { get; set; }
    double LabelOffsetY { get; set; }
    EnumColorType FillColor { get; set; }
    EnumColorType StrokeColor { get; set; }
    double StrokeThickness { get; set; }
    int ZOrder { get; set; }
    EnumOperationState OperationState { get; set; }

    // Adorner 시스템이 필요한 메서드들
    void UpdateLocation(PointLatLng newPosition);
    void UpdateSize(double width, double height);
    void UpdateRotation(double bearing);
    bool IsDisposed { get; }
    bool EnableShapeAnimation { get; set; }
}
