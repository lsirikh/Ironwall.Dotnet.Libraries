using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 6:06:02 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 부대 종류 그룹별 필터링을 위한 컨버터
    /// </summary>
    public class UnitTypeGroupFilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryUnitType unitType && parameter is string groupType)
            {
                var unitTypeValue = (int)unitType;

                return groupType.ToLower() switch
                {
                    "land" => unitTypeValue >= 100 && unitTypeValue < 1000,      // 육상 부대 (100-999)
                    "air" => unitTypeValue >= 1000 && unitTypeValue < 2000,     // 공군 (1000-1999)
                    "naval" => unitTypeValue >= 2000 && unitTypeValue < 9000,   // 해군 (2000-8999)
                    "command" => unitTypeValue >= 9000,                         // 지휘통제 (9000+)
                    _ => true // 전체 표시
                };
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}