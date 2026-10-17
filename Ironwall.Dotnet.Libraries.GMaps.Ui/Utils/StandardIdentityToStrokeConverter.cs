using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 6:04:14 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 표준 정체성을 선 스타일로 변환하는 컨버터
    /// Present: 실선, Planned: 점선, Anticipated: 일점쇄선, Past: 실선 + 투명도
    /// </summary>
    public class StandardIdentityToStrokeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryStandardIdentity standardIdentity)
            {
                return standardIdentity switch
                {
                    EnumMilitaryStandardIdentity.Present => null,                          // 현재 - 실선 (기본값)
                    EnumMilitaryStandardIdentity.Planned => new DoubleCollection { 5, 5 }, // 계획 - 점선
                    EnumMilitaryStandardIdentity.Anticipated => new DoubleCollection { 10, 5, 2, 5 }, // 가정 - 일점쇄선
                    EnumMilitaryStandardIdentity.Past => null,                             // 과거 - 실선 (투명도는 별도 처리)
                    _ => null
                };
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}