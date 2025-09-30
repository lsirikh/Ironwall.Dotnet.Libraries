using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Streaming.Converters{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/30/2025 8:16:08 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 비교 연산 컨버터
    /// </summary>
    public class ComparisonConverter : IValueConverter
    {
        public static readonly ComparisonConverter GreaterThan = new ComparisonConverter { Operation = ComparisonOperation.GreaterThan };
        public static readonly ComparisonConverter LessThan = new ComparisonConverter { Operation = ComparisonOperation.LessThan };
        public static readonly ComparisonConverter Equal = new ComparisonConverter { Operation = ComparisonOperation.Equal };

        public enum ComparisonOperation
        {
            GreaterThan,
            LessThan,
            Equal,
            GreaterThanOrEqual,
            LessThanOrEqual,
            NotEqual
        }

        public ComparisonOperation Operation { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            try
            {
                var left = System.Convert.ToDouble(value);
                var right = System.Convert.ToDouble(parameter);

                return Operation switch
                {
                    ComparisonOperation.GreaterThan => left > right,
                    ComparisonOperation.LessThan => left < right,
                    ComparisonOperation.Equal => Math.Abs(left - right) < 0.0001,
                    ComparisonOperation.GreaterThanOrEqual => left >= right,
                    ComparisonOperation.LessThanOrEqual => left <= right,
                    ComparisonOperation.NotEqual => Math.Abs(left - right) > 0.0001,
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}