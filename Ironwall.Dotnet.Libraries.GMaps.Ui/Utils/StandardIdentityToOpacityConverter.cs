using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 6:04:43 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 표준 정체성을 투명도로 변환하는 컨버터 (과거 상태용)
    /// </summary>
    public class StandardIdentityToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryStandardIdentity standardIdentity)
            {
                return standardIdentity switch
                {
                    EnumMilitaryStandardIdentity.Past => 0.6,  // 과거 - 투명도 적용
                    _ => 1.0                                    // 나머지 - 불투명
                };
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}