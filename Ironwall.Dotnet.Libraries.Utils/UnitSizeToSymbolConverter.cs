using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/8/2025 10:09:55 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 부대 규모를 NATO 심볼로 변환하는 컨버터
/// </summary>
public class UnitSizeToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is EnumMilitaryUnitSize unitSize)
        {
            return unitSize switch
            {
                EnumMilitaryUnitSize.Individual => "∅",      // 개인
                EnumMilitaryUnitSize.Squad => "•",          // 분대 (점 1개)
                EnumMilitaryUnitSize.Platoon => "••",       // 소대 (점 2개)
                EnumMilitaryUnitSize.Company => "I",        // 중대 (세로선 1개)
                EnumMilitaryUnitSize.Battalion => "II",     // 대대 (세로선 2개)
                EnumMilitaryUnitSize.Regiment => "III",     // 연대 (세로선 3개)
                EnumMilitaryUnitSize.Brigade => "X",        // 여단 (X표시)
                EnumMilitaryUnitSize.Division => "XX",      // 사단 (X표시 2개)
                EnumMilitaryUnitSize.Corps => "XXX",        // 군단 (X표시 3개)
                EnumMilitaryUnitSize.Army => "XXXX",        // 야전군 (X표시 4개)
                EnumMilitaryUnitSize.ArmyGroup => "XXXXX",  // 군집단 (X표시 5개)
                _ => "I"
            };
        }
        return "I";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("UnitSizeToSymbolConverter는 단방향 변환만 지원합니다.");
    }
}