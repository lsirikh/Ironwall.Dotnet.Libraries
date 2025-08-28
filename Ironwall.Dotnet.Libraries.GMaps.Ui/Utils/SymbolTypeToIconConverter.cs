using Ironwall.Dotnet.Libraries.Enums;
using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/21/2025 6:26:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

public class SymbolTypeToIconConverter : IValueConverter
{
    private static readonly Dictionary<string, PackIconKind> IconMappings = new()
    {
        // BASIC_SHAPES
        { "Pin", PackIconKind.Pin },
        //{ "Text", PackIconKind.Text },
        //{ "Flag", PackIconKind.Flag },
        
        // VEHICLES
        { "Car", PackIconKind.Car },
        //{ "Truck", PackIconKind.Truck },
        //{ "Bus", PackIconKind.Bus },
        //{ "Motorcycle", PackIconKind.Motorbike },
        //{ "Bicycle", PackIconKind.Bicycle },
        //{ "Boat", PackIconKind.Ferry },
        //{ "Plane", PackIconKind.Airplane },
        //{ "Helicopter", PackIconKind.Helicopter },
        
        // MILITARY_SYMBOLS
        { "Infantry", PackIconKind.AccountGroup },
        //{ "Armor", PackIconKind.Tank },
        //{ "Air_Defense", PackIconKind.Shield },
        //{ "Command", PackIconKind.AccountTie },
        //{ "Supply", PackIconKind.Package },
        //{ "Engineer", PackIconKind.Wrench },
        
        // PIDS_EQUIPMENT
        { "Controller", PackIconKind.AudioVideo },
        { "Multi", PackIconKind.DoorbellVideo },
        { "Fence", PackIconKind.Fence },
        { "IpCamera", PackIconKind.Camera },
        //{ "Radar", PackIconKind.Radar },
        //{ "Control_Box", PackIconKind.Cube },
        //{ "Gate", PackIconKind.Gate },
        //{ "Barrier", PackIconKind.Barrier },
        
        // AREA_BOUNDARY
        { "Zone", PackIconKind.MapOutline },
        //{ "Perimeter", PackIconKind.BorderAll },
        //{ "Road", PackIconKind.Road },
        //{ "Building", PackIconKind.Building },
        //{ "Bridge", PackIconKind.Bridge },
        //{ "Forest", PackIconKind.Tree },
        
        // ANALYSIS
        { "Predicted_Path", PackIconKind.TrendingUp },
        //{ "Risk_Area", PackIconKind.Alert },
        //{ "Search_Zone", PackIconKind.Magnify },
        //{ "Target_Area", PackIconKind.Target },
        //{ "Escape_Route", PackIconKind.ExitToApp },
        
        // INFRASTRUCTURE
        { "Tower", PackIconKind.TransmissionTower },
        //{ "Power_Plant", PackIconKind.Factory },
        //{ "Substation", PackIconKind.ElectricSwitch },
        //{ "Antenna", PackIconKind.Antenna },
        
        // EVENT_SYMBOLS
        { "Alert", PackIconKind.Alert },
        //{ "Warning", PackIconKind.Warning },
        //{ "Incident", PackIconKind.Information },
        //{ "Fire", PackIconKind.Fire },
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is EnumShapeType shapeType)
        {
            return shapeType switch
            {
                EnumShapeType.Circle => PackIconKind.Circle,
                EnumShapeType.Triangle => PackIconKind.Triangle,
                EnumShapeType.Square => PackIconKind.Square,
                EnumShapeType.Diamond => PackIconKind.Rhombus,
                EnumShapeType.Pentagon => PackIconKind.Pentagon,
                EnumShapeType.Hexagon => PackIconKind.Hexagon,
                EnumShapeType.Star => PackIconKind.Star,
                EnumShapeType.Arrow => PackIconKind.ArrowRight,
                _ => PackIconKind.Circle
            };
        }

        if (value is string stringValue)
        {
            return IconMappings.GetValueOrDefault(stringValue, PackIconKind.Circle);
        }

        return PackIconKind.Circle;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}