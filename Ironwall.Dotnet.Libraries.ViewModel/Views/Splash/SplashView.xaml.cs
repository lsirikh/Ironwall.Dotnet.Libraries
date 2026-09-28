using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ironwall.Dotnet.Libraries.ViewModel.Views.Splash;

/// <summary>
/// 기동 화면(B8). 모양은 XAML 이 토큰으로 그리고, 여기서는 그림 두 장(사진 · 회사 로고)만 불러 준다.
/// </summary>
/// <remarks>
/// <para>그림은 <b>실행 파일(진입 어셈블리)의 리소스</b>다 — 이 라이브러리를 쓰는 앱마다 제 그림을 싣는다.
/// XAML 에 <c>pack://application</c> 경로를 바로 적으면 그림이 없는 앱(미리보기 · 시험)에서 창을 만드는 순간 예외로 죽는다.
/// 그래서 여기서 불러 보고, 못 불러오면 비워 둔다(띠는 가라앉은 표면색으로 남는다).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public partial class SplashView : Window
{
    /// <summary>기본 사진 · 로고 — 실행 파일의 Resources/Images 아래.</summary>
    public const string DefaultArtUri = "pack://application:,,,/Resources/Images/SplashScreen.png";
    public const string DefaultLogoUri = "pack://application:,,,/Resources/Images/Company_Logo.png";

    public static readonly DependencyProperty ArtProperty = DependencyProperty.Register(
        nameof(Art), typeof(ImageSource), typeof(SplashView), new PropertyMetadata(null));

    /// <summary>위쪽 띠의 사진. 비면 띠만 남는다.</summary>
    public ImageSource? Art { get => (ImageSource?)GetValue(ArtProperty); set => SetValue(ArtProperty, value); }

    public static readonly DependencyProperty LogoProperty = DependencyProperty.Register(
        nameof(Logo), typeof(ImageSource), typeof(SplashView), new PropertyMetadata(null));

    /// <summary>상태 줄 오른쪽의 회사 로고. 비면 자리도 없다.</summary>
    public ImageSource? Logo { get => (ImageSource?)GetValue(LogoProperty); set => SetValue(LogoProperty, value); }

    public SplashView()
    {
        InitializeComponent();
        Art = TryLoad(DefaultArtUri);
        Logo = TryLoad(DefaultLogoUri);
    }

    /// <summary>그림 하나를 불러 본다 — 없거나 깨졌으면 <c>null</c>(창은 그래도 뜬다).</summary>
    public static ImageSource? TryLoad(string uri)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(uri, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UriFormatException or NotSupportedException
                                   or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
