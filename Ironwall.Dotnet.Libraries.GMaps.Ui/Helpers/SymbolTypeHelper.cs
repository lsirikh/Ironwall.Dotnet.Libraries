using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/21/2025 5:18:18 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public static class SymbolTypeHelper
{
    /// <summary>
    /// 카테고리별 표시 이름 딕셔너리
    /// </summary>
    public static readonly Dictionary<EnumMarkerCategory, string> CategoryDisplayNames = new()
    {
        { EnumMarkerCategory.BASIC_SHAPES, "기본 도형" },
        { EnumMarkerCategory.GEOMETRICS, "기하학도형" },
        { EnumMarkerCategory.VEHICLES, "차량/교통" },
        { EnumMarkerCategory.MILITARY_SYMBOLS, "군사 심볼" },
        { EnumMarkerCategory.PIDS_EQUIPMENT, "PIDS 장비" },
        { EnumMarkerCategory.AREA_BOUNDARY, "지역/경계" },
        { EnumMarkerCategory.INFRASTRUCTURE, "인프라/시설" },
    };

    /// <summary>
    /// 심볼 타입별 표시 이름 딕셔너리
    /// </summary>
    public static readonly Dictionary<string, string> SymbolTypeDisplayNames = new()
    {
        // BASIC_SHAPES
        { "Pin", "핀" },
        //{ "Text", "텍스트" },
        //{ "Flag", "깃발" },
    
        // VEHICLES
        { "Car", "승용차" },
        //{ "Truck", "트럭" },
        //{ "Bus", "버스" },
        //{ "Motorcycle", "오토바이" },
        //{ "Bicycle", "자전거" },
        //{ "Boat", "보트" },
        //{ "Plane", "비행기" },
        //{ "Helicopter", "헬리콥터" },
    
        // MILITARY_SYMBOLS
        { "Infantry", "보병" },
        //{ "Armor", "기갑" },
        //{ "Artillery", "포병" },
        //{ "Air_Defense", "방공" },
        //{ "Command", "지휘소" },
        //{ "Supply", "보급" },
        //{ "Engineer", "공병" },
    
        // PIDS_EQUIPMENT
        { "PIR_Sensor", "PIR 센서" },
        //{ "Fence_Sensor", "펜스 센서" },
        //{ "Camera", "카메라" },
        //{ "Radar", "레이더" },
        //{ "Control_Box", "제어함" },
        //{ "Gate", "게이트" },
        //{ "Barrier", "차단기" },
    
        // AREA_BOUNDARY
        { "Area", "구역" },
        //{ "Perimeter", "경계선" },
        //{ "Road", "도로" },
        //{ "Building", "건물" },
        //{ "Bridge", "교량" },
        //{ "River", "강/하천" },
        //{ "Forest", "산림" },
    
        // INFRASTRUCTURE
        { "Factory", "공장" },
        //{ "Bridge", "교량" },
        //{ "Tunnel", "터널" },
        //{ "Power_Plant", "발전소" },
        //{ "Substation", "변전소" },
        //{ "Antenna", "안테나" },
    
        
    };
    /// <summary>
    /// 기하학적 도형 표시 이름 딕셔너리
    /// </summary>
    public static readonly Dictionary<EnumShapeType, string> ShapeTypeDisplayNames = new()
    {
        { EnumShapeType.Circle, "원형" },
        { EnumShapeType.Triangle, "삼각형" },
        { EnumShapeType.Square, "사각형" },
        { EnumShapeType.Diamond, "다이아몬드" },
        { EnumShapeType.Pentagon, "오각형" },
        { EnumShapeType.Hexagon, "육각형" },
        { EnumShapeType.Star, "별" },
        { EnumShapeType.Arrow, "화살표" },
    };
}