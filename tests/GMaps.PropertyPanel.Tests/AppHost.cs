using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Moq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 속성창 오프스크린 렌더 호스트 — 라이브러리 스타일(BasePropertyStyle → Resources.xaml)이 MDIX 키(MaterialDesignScrollBarMinimal)를
/// StaticResource 로 참조하므로 WPF 파서가 Application.Current.Resources 로 폴백할 수 있어야 한다. Application 은 프로세스당 1개라
/// 전용 STA 스레드 하나에 상주시키고(Housing 테스트처럼 테스트마다 스레드를 만들면 두 번째 생성이 불가) 모든 테스트를 그 디스패처에서 실행한다.
/// 이 어셈블리를 GMaps.Housing.Tests 와 분리한 이유: Application.Current 가 남아 있으면 GMapPidsGroupMarker 등이 UI 마샬 경로로 갈라진다.
/// </summary>
internal static class AppHost
{
    private static readonly Lazy<Dispatcher> _dispatcher = new(Start);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null; Exception? failure = null;
        using var ready = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            try
            {
                IoC.GetInstance = (type, key) => type == typeof(ILogService) ? Mock.Of<ILogService>() : type == typeof(IEventAggregator) ? new EventAggregator() : throw new InvalidOperationException(type.Name);
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                // App.xaml 과 같은 순서: MDIX 번들 → MD3 기본값 → 테마 토큰(Shared/Light)
                app.Resources.MergedDictionaries.Add(new MaterialDesignThemes.Wpf.BundledTheme { BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Light, PrimaryColor = MaterialDesignColors.PrimaryColor.LightBlue, SecondaryColor = MaterialDesignColors.SecondaryColor.Cyan });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml", UriKind.Absolute) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) });
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex) { failure = ex; }
            finally { ready.Set(); }
            if (failure == null) Dispatcher.Run();
        }) { IsBackground = true, Name = "PropertyPanel.AppHost" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); ready.Wait();
        if (failure != null) throw new InvalidOperationException("AppHost 기동 실패", failure);
        return dispatcher!;
    }

    /// <summary>테스트 본문을 Application 스레드에서 실행(예외는 호출 스레드로 전달, 60 s 제한).</summary>
    internal static void Run(System.Action action)
    {
        Exception? failure = null;
        var op = _dispatcher.Value.InvokeAsync(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        Assert.True(op.Wait(TimeSpan.FromSeconds(60)) == DispatcherOperationStatus.Completed, "AppHost timeout");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    internal static void Layout(FrameworkElement view, double width, double height)
    { view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }

    internal static void SaveImage(FrameworkElement view, int width, int height, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
