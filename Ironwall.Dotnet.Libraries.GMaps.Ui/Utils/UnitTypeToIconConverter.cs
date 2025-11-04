using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils
{
    /****************************************************************************
       Purpose      : 부대 종류를 아이콘 가시성으로 변환하는 컨버터 (27개 UnitType 지원)                                                        
       Created By   : GHLee                                                
       Created On   : 9/8/2025 5:56:57 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// 부대 종류를 아이콘 가시성으로 변환하는 컨버터
    /// 27개 군사 부대 유형을 모두 지원
    /// </summary>
    public class UnitTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EnumMilitaryUnitType unitType && parameter is string iconType)
            {
                return iconType.ToLower() switch
                {
                    // === Land Forces - 육상 부대 (32개) ===

                    // Basic Combat Units
                    "infantry" => unitType == EnumMilitaryUnitType.Infantry ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "armour" => unitType == EnumMilitaryUnitType.Armour ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "artillery" => unitType == EnumMilitaryUnitType.Artillery ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "combinedmanoeuvrearms" => unitType == EnumMilitaryUnitType.CombinedManoeuvreArms ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Specialized Combat Units
                    "airdefence" => unitType == EnumMilitaryUnitType.AirDefence ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "antitank" => unitType == EnumMilitaryUnitType.AntiTank ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "missile" => unitType == EnumMilitaryUnitType.Missile ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "mortar" => unitType == EnumMilitaryUnitType.Mortar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "reconnaissancecavalry" => unitType == EnumMilitaryUnitType.ReconnaissanceCavalry ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Special Forces
                    "specialforces" => unitType == EnumMilitaryUnitType.SpecialForces ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "specialoperationsforces" => unitType == EnumMilitaryUnitType.SpecialOperationsForces ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Engineering & Technical
                    "engineer" => unitType == EnumMilitaryUnitType.Engineer ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "bridging" => unitType == EnumMilitaryUnitType.Bridging ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "explosiveordnancedisposal" => unitType == EnumMilitaryUnitType.ExplosiveOrdnanceDisposal ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Communications & Intelligence
                    "signals" => unitType == EnumMilitaryUnitType.Signals ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "electronicwarfare" => unitType == EnumMilitaryUnitType.ElectronicWarfare ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "electronicranging" => unitType == EnumMilitaryUnitType.ElectronicRanging ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "psychologicaloperations" => unitType == EnumMilitaryUnitType.PsychologicalOperations ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "radar" => unitType == EnumMilitaryUnitType.Radar ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Logistics & Support
                    "combatservicesupport" => unitType == EnumMilitaryUnitType.CombatServiceSupport ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "supply" => unitType == EnumMilitaryUnitType.Supply ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "ammunition" => unitType == EnumMilitaryUnitType.Ammunition ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "fuelpol" => unitType == EnumMilitaryUnitType.FuelPOL ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "transportation" => unitType == EnumMilitaryUnitType.Transportation ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "maintenance" => unitType == EnumMilitaryUnitType.Maintenance ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "ordnance" => unitType == EnumMilitaryUnitType.Ordnance ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Medical & Health
                    "medical" => unitType == EnumMilitaryUnitType.Medical ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "hospital" => unitType == EnumMilitaryUnitType.Hospital ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Security & Law Enforcement
                    "militarypolice" => unitType == EnumMilitaryUnitType.MilitaryPolice ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "cbrndefence" => unitType == EnumMilitaryUnitType.CBRNDefence ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Specialized Services
                    "meteorological" => unitType == EnumMilitaryUnitType.Meteorological ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "topographical" => unitType == EnumMilitaryUnitType.Topographical ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Command & Control
                    "hqunit" => unitType == EnumMilitaryUnitType.HQUnit ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // === Air Forces - 공군 (3개) ===
                    "rotarywingaviation" => unitType == EnumMilitaryUnitType.RotaryWingAviation ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "fixedwingaviation" => unitType == EnumMilitaryUnitType.FixedWingAviation ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,
                    "unmannedairvehicle" => unitType == EnumMilitaryUnitType.UnmannedAirVehicle ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // === Naval Forces - 해군 (1개) ===
                    "navy" => unitType == EnumMilitaryUnitType.Navy ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed,

                    // Default case
                    _ => System.Windows.Visibility.Collapsed
                };
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("UnitTypeToIconConverter는 단방향 바인딩만 지원합니다.");
        }
    }
}