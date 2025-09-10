using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 5:55:57 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 군사 소속을 색상으로 변환하는 컨버터
    /// NATO APP-6D 표준 색상 적용
    /// </summary>
    public class AffiliationToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryAffiliation affiliation)
            {
                return affiliation switch
                {
                    EnumMilitaryAffiliation.Unknown => new SolidColorBrush(Color.FromRgb(128, 128, 128)),      // 회색 (미확인)
                    EnumMilitaryAffiliation.Friend => new SolidColorBrush(Color.FromRgb(74, 144, 226)),        // 파란색 (아군)
                    EnumMilitaryAffiliation.Neutral => new SolidColorBrush(Color.FromRgb(39, 174, 96)),        // 녹색 (중립)
                    EnumMilitaryAffiliation.Hostile => new SolidColorBrush(Color.FromRgb(231, 76, 60)),        // 빨간색 (적군)
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}