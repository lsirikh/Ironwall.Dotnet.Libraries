using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/8/2025 5:56:57 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 부대 종류를 아이콘 가시성으로 변환하는 컨버터
    /// </summary>
    public class UnitTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryUnitType unitType && parameter is string iconType)
            {
                return iconType.ToLower() switch
                {
                    // Land Forces - 육상 부대
                    "infantry" => unitType == EnumMilitaryUnitType.Infantry ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "mechanizedinfantry" => unitType == EnumMilitaryUnitType.MechanizedInfantry ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "airborne" => unitType == EnumMilitaryUnitType.Airborne ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "marines" => unitType == EnumMilitaryUnitType.Marines ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "specialforces" => unitType == EnumMilitaryUnitType.SpecialForces ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    "armor" => unitType == EnumMilitaryUnitType.Armor ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "mechanized" => unitType == EnumMilitaryUnitType.Mechanized ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    "artillery" => unitType == EnumMilitaryUnitType.Artillery ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "airdefense" => unitType == EnumMilitaryUnitType.AirDefense ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "rocket" => unitType == EnumMilitaryUnitType.Rocket ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    "engineer" => unitType == EnumMilitaryUnitType.Engineer ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "signal" => unitType == EnumMilitaryUnitType.Signal ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "intelligence" => unitType == EnumMilitaryUnitType.Intelligence ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "logistics" => unitType == EnumMilitaryUnitType.Logistics ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "medical" => unitType == EnumMilitaryUnitType.Medical ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "militarypolice" => unitType == EnumMilitaryUnitType.MilitaryPolice ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Air Forces - 공군
                    "fighter" => unitType == EnumMilitaryUnitType.Fighter ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "bomber" => unitType == EnumMilitaryUnitType.Bomber ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "helicopter" => unitType == EnumMilitaryUnitType.Helicopter ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "transport" => unitType == EnumMilitaryUnitType.Transport ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "reconnaissance" => unitType == EnumMilitaryUnitType.Reconnaissance ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Naval Forces - 해군
                    "surface" => unitType == EnumMilitaryUnitType.Surface ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "submarine" => unitType == EnumMilitaryUnitType.Submarine ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "carrier" => unitType == EnumMilitaryUnitType.Carrier ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "destroyer" => unitType == EnumMilitaryUnitType.Destroyer ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Command & Control - 지휘통제
                    "command" => unitType == EnumMilitaryUnitType.Command ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "control" => unitType == EnumMilitaryUnitType.Control ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "headquarters" => unitType == EnumMilitaryUnitType.Headquarters ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

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