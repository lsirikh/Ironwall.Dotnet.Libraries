using Ironwall.Dotnet.Libraries.Enums;
using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/21/2025 6:26:39 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/


public class MarkerCategoryToIconConverter : IValueConverter
{
    private static readonly Dictionary<EnumMarkerCategory, PackIconKind> CategoryIconMappings = new()
    {
        { EnumMarkerCategory.BASIC_SHAPES, PackIconKind.Shape },
        { EnumMarkerCategory.GEOMETRICS, PackIconKind.Triangle },
        { EnumMarkerCategory.VEHICLES, PackIconKind.Car },
        { EnumMarkerCategory.MILITARY_SYMBOLS, PackIconKind.Shield },
        { EnumMarkerCategory.PIDS_EQUIPMENT, PackIconKind.Security },
        { EnumMarkerCategory.AREA_BOUNDARY, PackIconKind.MapOutline },
        { EnumMarkerCategory.INFRASTRUCTURE, PackIconKind.Factory },
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is EnumMarkerCategory category)
        {
            return CategoryIconMappings.GetValueOrDefault(category, PackIconKind.Circle);
        }
        return PackIconKind.Circle;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}