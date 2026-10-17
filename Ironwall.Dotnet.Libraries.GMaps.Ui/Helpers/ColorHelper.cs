using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/21/2025 4:23:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// EnumColorType을 실제 색상값으로 변환하는 헬퍼 클래스
/// </summary>
public static class ColorHelper
{
    /// <summary>
    /// EnumColorType을 Hex 색상 문자열로 변환
    /// </summary>
    /// <param name="colorType">색상 타입</param>
    /// <returns>Hex 색상 문자열 (#RRGGBB)</returns>
    public static string ToHexString(this EnumColorType colorType)
    {
        return colorType switch
        {
            EnumColorType.Blue => "#2196F3",           // Material Blue
            EnumColorType.Red => "#F44336",            // Material Red
            EnumColorType.Green => "#4CAF50",          // Material Green
            EnumColorType.Orange => "#FF9800",         // Material Orange
            EnumColorType.Purple => "#9C27B0",         // Material Purple
            EnumColorType.Yellow => "#FFEB3B",         // Material Yellow
            EnumColorType.Pink => "#E91E63",           // Material Pink
            EnumColorType.Teal => "#009688",           // Material Teal
            EnumColorType.Indigo => "#3F51B5",         // Material Indigo
            EnumColorType.Lime => "#CDDC39",           // Material Lime
            EnumColorType.Brown => "#795548",          // Material Brown
            EnumColorType.Gray => "#9E9E9E",           // Material Gray
            EnumColorType.Black => "#212121",          // Material Dark
            EnumColorType.White => "#FFFFFF",          // White
            EnumColorType.DarkBlue => "#1976D2",       // Material Blue 700
            EnumColorType.DarkRed => "#D32F2F",        // Material Red 700
            EnumColorType.DarkGreen => "#388E3C",      // Material Green 700
            EnumColorType.DarkOrange => "#F57C00",     // Material Orange 700
            EnumColorType.DarkPurple => "#7B1FA2",     // Material Purple 700
            EnumColorType.Gold => "#FFD700",           // Gold
            EnumColorType.Silver => "#C0C0C0",         // Silver
            EnumColorType.Magenta => "#FF00FF",        // Magenta
            EnumColorType.Cyan => "#00FFFF",           // Cyan
            EnumColorType.Beige => "#F5F5DC",          // Beige
            EnumColorType.Olive => "#808000",            // Olive
            EnumColorType.Transparent => "#00000000",    // Transparent
            _ => "#2196F3"                             // Default Blue
        };
    }

    /// <summary>
    /// Hex 색상 문자열을 EnumColorType으로 변환 (직접 매핑)
    /// </summary>
    /// <param name="hexColor">Hex 색상 문자열 (#RRGGBB)</param>
    /// <returns>일치하는 EnumColorType, 없으면 Blue 반환</returns>
    public static EnumColorType HexToColorType(string hexColor)
    {
        if (string.IsNullOrEmpty(hexColor))
            return EnumColorType.Blue;

        return hexColor.ToUpperInvariant() switch
        {
            "#2196F3" => EnumColorType.Blue,           // Material Blue
            "#F44336" => EnumColorType.Red,            // Material Red
            "#4CAF50" => EnumColorType.Green,          // Material Green
            "#FF9800" => EnumColorType.Orange,         // Material Orange
            "#9C27B0" => EnumColorType.Purple,         // Material Purple
            "#FFEB3B" => EnumColorType.Yellow,         // Material Yellow
            "#E91E63" => EnumColorType.Pink,           // Material Pink
            "#009688" => EnumColorType.Teal,           // Material Teal
            "#3F51B5" => EnumColorType.Indigo,         // Material Indigo
            "#CDDC39" => EnumColorType.Lime,           // Material Lime
            "#795548" => EnumColorType.Brown,          // Material Brown
            "#9E9E9E" => EnumColorType.Gray,           // Material Gray
            "#212121" => EnumColorType.Black,          // Material Dark
            "#FFFFFF" => EnumColorType.White,          // White
            "#1976D2" => EnumColorType.DarkBlue,       // Material Blue 700
            "#D32F2F" => EnumColorType.DarkRed,        // Material Red 700
            "#388E3C" => EnumColorType.DarkGreen,      // Material Green 700
            "#F57C00" => EnumColorType.DarkOrange,     // Material Orange 700
            "#7B1FA2" => EnumColorType.DarkPurple,     // Material Purple 700
            "#FFD700" => EnumColorType.Gold,           // Gold
            "#C0C0C0" => EnumColorType.Silver,         // Silver
            "#FF00FF" => EnumColorType.Magenta,        // Magenta
            "#00FFFF" => EnumColorType.Cyan,           // Cyan
            "#F5F5DC" => EnumColorType.Beige,          // Beige
            "#808000" => EnumColorType.Olive,          // Olive
            "#00000000" => EnumColorType.Transparent,  // Transparent
            _ => EnumColorType.Blue                    // Default
        };
    }

    /// <summary>
    /// EnumColorType을 WPF Brush로 변환
    /// </summary>
    /// <param name="colorType">색상 타입</param>
    /// <returns>SolidColorBrush 객체</returns>
    public static SolidColorBrush ToBrush(this EnumColorType colorType)
    {
        var hexColor = colorType.ToHexString();
        var color = (Color)ColorConverter.ConvertFromString(hexColor);
        return new SolidColorBrush(color);
    }

    /// <summary>
    /// EnumColorType을 System.Drawing.Color로 변환
    /// </summary>
    /// <param name="colorType">색상 타입</param>
    /// <returns>System.Drawing.Color 객체</returns>
    public static System.Drawing.Color ToDrawingColor(this EnumColorType colorType)
    {
        var hexColor = colorType.ToHexString();
        return System.Drawing.ColorTranslator.FromHtml(hexColor);
    }

    /// <summary>
    /// 색상 이름 가져오기 (Description 어트리뷰트 사용)
    /// </summary>
    /// <param name="colorType">색상 타입</param>
    /// <returns>색상 이름</returns>
    public static string GetDisplayName(this EnumColorType colorType)
    {
        var type = typeof(EnumColorType);
        var member = type.GetMember(colorType.ToString());
        if (member.Length > 0)
        {
            var attributes = member[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes.Length > 0)
            {
                return ((DescriptionAttribute)attributes[0]).Description;
            }
        }
        return colorType.ToString();
    }

    /// <summary>
    /// 모든 색상 타입과 이름 가져오기 (UI 바인딩용)
    /// </summary>
    /// <returns>색상 타입과 이름의 딕셔너리</returns>
    public static Dictionary<EnumColorType, string> GetAllColors()
    {
        var colors = new Dictionary<EnumColorType, string>();
        foreach (EnumColorType colorType in Enum.GetValues<EnumColorType>())
        {
            colors[colorType] = colorType.GetDisplayName();
        }
        return colors;
    }

    /// <summary>
    /// UI에서 사용할 색상 목록 (자주 사용되는 색상 우선)
    /// </summary>
    /// <returns>UI용 색상 목록</returns>
    public static EnumColorType[] GetCommonColors()
    {
        return new[]
        {
                // 기본 색상 (자주 사용)
        EnumColorType.Blue,         // Material Blue
        EnumColorType.Red,          // Material Red
        EnumColorType.Green,        // Material Green
        EnumColorType.Yellow,       // Material Yellow
        EnumColorType.Orange,       // Material Orange
        EnumColorType.Purple,       // Material Purple
        EnumColorType.Pink,         // Material Pink
        EnumColorType.Brown,        // Material Brown - 인프라/건물에 필수
        
        // 보조 색상
        EnumColorType.Teal,         // Material Teal
        EnumColorType.Indigo,       // Material Indigo
        EnumColorType.Lime,         // Material Lime
        EnumColorType.Cyan,         // Cyan
        EnumColorType.Magenta,      // Magenta
        
        // 중성 색상
        EnumColorType.Gray,         // Material Gray
        EnumColorType.Black,        // Material Dark
        EnumColorType.White,        // White
        EnumColorType.Beige,        // Beige
        EnumColorType.Olive,        // Olive
        
        // 진한 색상 변형
        EnumColorType.DarkBlue,     // Material Blue 700
        EnumColorType.DarkRed,      // Material Red 700
        EnumColorType.DarkGreen,    // Material Green 700
        EnumColorType.DarkOrange,   // Material Orange 700
        EnumColorType.DarkPurple,   // Material Purple 700
        
        // 특수 색상
        EnumColorType.Gold,         // Gold
        EnumColorType.Silver,       // Silver
        EnumColorType.Transparent   // Transparent (필요시)
            };
    }
}