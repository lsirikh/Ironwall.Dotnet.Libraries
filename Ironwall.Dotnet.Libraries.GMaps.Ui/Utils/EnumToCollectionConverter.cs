using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      : Enum 타입을 컬렉션으로 변환하는 컨버터
       Created By   : GHLee                                                
       Created On   : 9/10/2025                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EnumToCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Type enumType && enumType.IsEnum)
            {
                return Enum.GetValues(enumType);
            }

            return new object[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("EnumToCollectionConverter는 단방향 변환만 지원합니다.");
        }
    }
}