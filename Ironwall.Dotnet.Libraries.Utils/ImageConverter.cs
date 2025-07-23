using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Data;


namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class ImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var profileDir = Path.Combine(AppContext.BaseDirectory, "Profile");
        Directory.CreateDirectory(profileDir);

        string fileName = value as string ?? "";
        string fullPath = Path.Combine(profileDir, fileName);

        // ▶ 파일이 있으면 절대경로, 없으면 기본 리소스
        if (File.Exists(fullPath))
            return fullPath;

        bool full = (parameter as string) == "Full";
        return full
            ? "/Resources/Images/Profile_default_style.png"
            : "/Resources/Images/Profile_default_style_64.png";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
