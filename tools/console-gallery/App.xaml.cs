using System.IO;
using System.Windows;

namespace ConsoleGallery;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // --matrix <폴더> — 손댄 스타일의 상태 행렬만 뜨고 끝낸다(콘솔 무대는 띄우지 않는다).
        var matrix = Array.IndexOf(e.Args, "--matrix");
        if (matrix >= 0 && matrix + 1 < e.Args.Length)
        {
            var stage = new MatrixWindow();
            PreviewTools.Shared.OffscreenStage.Hide(stage).Show();
            try
            {
                await Task.Delay(600);
                await stage.RunAsync(e.Args[matrix + 1], e.Args.Contains("--legacy"));
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(e.Args[matrix + 1]);
                File.WriteAllText(Path.Combine(e.Args[matrix + 1], "matrix-error.txt"), ex.ToString());
            }
            Shutdown();
            return;
        }

        var window = new MainWindow();
        PreviewTools.Shared.OffscreenStage.Hide(window).Show();

        var index = Array.IndexOf(e.Args, "--snapshot");
        if (index < 0 || index + 1 >= e.Args.Length) return;

        try
        {
            await Task.Delay(600);
            await window.RunSnapshotsAsync(e.Args[index + 1]);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(e.Args[index + 1], "snapshot-error.txt"), ex.ToString());
        }
        Shutdown();
    }
}
