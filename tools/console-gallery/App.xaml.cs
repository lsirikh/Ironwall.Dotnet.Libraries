using System.Windows;

namespace ConsoleGallery;

public partial class App : Application
{
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow();
        window.Show();

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
