using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Data;


namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class ImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string fileName = value as string ?? "";

        // C1: 서버 photo_url(절대 URL)은 그대로 ImageSource 로 넘긴다(WPF가 다운로드 렌더). 로컬 파일명 분기와 분리.
        if (fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return fileName;

        var profileDir = Path.Combine(AppContext.BaseDirectory, "Profile");
        Directory.CreateDirectory(profileDir);

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
        // ⚠ 크래시 근본수정: 이미지 로드 실패 시 Image.OnSourceFailed → WPF가 바인딩 되쓰기 시도 → 여기 호출.
        //   기존 throw NotSupportedException 은 미처리 예외로 앱을 죽였다(원격 photo_url 로드 실패 등).
        //   단방향 표시 전용 컨버터이므로 되쓰기를 무시(Binding.DoNothing)해 크래시를 원천 차단한다.
        return Binding.DoNothing;
    }
}
