using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 5:56:23 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 전투 차원을 프레임 모양 가시성으로 변환하는 컨버터
    /// </summary>
    public class BattleDimensionToShapeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryBattleDimension battleDimension && parameter is string shapeType)
            {
                return shapeType.ToLower() switch
                {
                    "land" or "ground" => battleDimension == EnumMilitaryBattleDimension.Land ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "sea" => battleDimension == EnumMilitaryBattleDimension.Sea ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "subsurface" => battleDimension == EnumMilitaryBattleDimension.Subsurface ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "air" => battleDimension == EnumMilitaryBattleDimension.Air ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "space" => battleDimension == EnumMilitaryBattleDimension.Space ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "electronicwarfare" or "ew" => battleDimension == EnumMilitaryBattleDimension.ElectronicWarfare ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "cyber" => battleDimension == EnumMilitaryBattleDimension.Cyber ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    _ => System.Windows.Visibility.Collapsed
                };
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}